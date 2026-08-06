using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.Adapters;
using MicroserviceRgpd.Core.Casework.Ledger;
using MicroserviceRgpd.UseCases.Casework.CallAdapter;

namespace MicroserviceRgpd.UseCases.Casework.Locate;

/// <summary>
/// Appelle <c>Locate</c> sur chaque <c>DeclaredSystem</c> qui le déclare, retient ce qui en revient,
/// et fait naître une <c>OpenQuestion</c> le jour où <b>tous</b> les appels rendent zéro.
/// </summary>
/// <remarks>
/// <para>
/// <b>Il n'attend jamais et ne barre jamais la route.</b> Une panne — serveur muet, statut hors
/// contrat, corps que le contrat ne prévoit pas — laisse le système <b>sans localisation</b> plutôt
/// qu'avec un zéro : « pas appelé » et « appelé, rien trouvé » sont deux déclarations différentes,
/// et confondre la première avec la seconde ferait écrire un constat que personne n'a fait. Les
/// autres systèmes sont appelés quand même, et l'écran s'affiche.
/// </para>
/// <para>
/// <b>Quand rappelle-t-on ?</b> Jamais par une minuterie, et toujours pour une raison qu'un humain
/// pourrait dire à voix haute : on n'avait jamais appelé ; le sac s'est enrichi, et la question posée
/// n'est plus la même ; l'échéance d'un <c>202</c> est passée ; l'appel avait été refusé, et ce qui
/// se répare des deux côtés a pu l'être. Un <c>Locate</c> déjà servi sous le sac d'aujourd'hui ne
/// repart pas : sa réponse est encore la réponse à la question qu'on pose.
/// </para>
/// <para>
/// <b>Ce qui entre au <c>Ledger</c> est ce qui change.</b> Rouvrir un dossier relance les appels, et
/// trente-cinq passages rendant le même verdict n'ont aucun signataire — c'est un affichage qui les a
/// déclenchés, non un humain. La règle est tenue <b>ici</b>, par l'appelant, le <c>Ledger</c> ne se
/// relisant jamais.
/// </para>
/// <para>
/// <b>L'ordre est : appeler, écrire le dossier, puis consigner</b> — comme partout ailleurs. Les
/// deux pannes ne se valent pas : une ligne de preuve pour un rattachement que le dossier ne porte
/// pas est un faux ; un rattachement porté dont la ligne manque est <b>visible</b> à l'écran.
/// </para>
/// </remarks>
/// <param name="cases">Le seul dépôt de ce contexte : les règles sont écrites une fois, sur la racine.</param>
/// <param name="manifest">Le catalogue, relu à chaque passage — il vieillit exprès.</param>
/// <param name="calls">Les appels sortants au titre d'un dossier, tentatives refusées comprises.</param>
/// <param name="ledger">La matière de preuve, en ajout seul.</param>
/// <param name="clock">L'horloge, injectée pour que la date d'une tentative se dicte en test.</param>
public sealed class LocateHandler(
  IRepository<Case> cases,
  IReadRepository<DeclaredSystem> manifest,
  AdapterCallsForCase calls,
  ILedger ledger,
  TimeProvider clock)
  : ICommandHandler<LocateCommand, Result>
{
  /// <inheritdoc />
  public async ValueTask<Result> Handle(LocateCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var opened = await cases.FirstOrDefaultAsync(new CaseByIdSpec(command.Case), cancellationToken);

    if (opened is null)
    {
      return Result.NotFound();
    }

    var reachable = (await manifest.ListAsync(cancellationToken))
      .Where(system => system.AdapterAddress is not null && system.Capabilities.Contains(Capability.Locate))
      .OrderBy(system => system.Id.Value, StringComparer.Ordinal)
      .ToArray();

    var consigned = new List<LedgerEntry>();
    var askedAt = clock.GetUtcNow();

    foreach (var system in reachable)
    {
      await AskAsync(opened, system, askedAt, consigned, cancellationToken);
    }

    consigned.AddRange(QuestionOf(opened, reachable, askedAt));

    await cases.UpdateAsync(opened, cancellationToken);

    foreach (var entry in consigned)
    {
      await ledger.AppendAsync(entry, cancellationToken);
    }

    return Result.Success();
  }

  private async Task AskAsync(
    Case opened,
    DeclaredSystem system,
    DateTimeOffset askedAt,
    List<LedgerEntry> consigned,
    CancellationToken cancellationToken)
  {
    var known = opened.LocatingIn(system.Id);

    if (!WorthAsking(known, opened.Designations.Count, askedAt))
    {
      return;
    }

    // Ce que la preuve gardera ne dépend pas de ce qu'elle porte déjà : c'est l'appelant qui sait
    // sous quoi il avait cherché et ce qu'on lui avait répondu.
    var changes = known is null
      || known.DesignationsAtCall != opened.Designations.Count;

    AdapterAnswer<LocateOnTheWire> answer;

    try
    {
      answer = await calls.AskAsync<LocateOnTheWire>(
        opened.Id,
        // Le sac part en COPIE : ce qui traverse la frontière est ce sous quoi cet appel-là a
        // cherché, et un arbitrage postérieur ne doit pas pouvoir réécrire après coup la question
        // qu'on a posée.
        new AdapterCall(system.AdapterAddress!.Value, system.Id, Capability.Locate, [.. opened.Designations]),
        known?.LastOutcome,
        cancellationToken);
    }
    catch (AdapterFailure)
    {
      // Ni réponse ni refus : personne ne sait qu'en conclure, et le dossier n'apprend rien. La
      // localisation reste ce qu'elle était — non appelée, ou telle que le dernier appel l'a laissée.
      return;
    }

    changes = changes || known?.LastOutcome != answer.Outcome;

    if (answer.Outcome == AdapterOutcome.Served)
    {
      LocateFindings findings;

      try
      {
        findings = LocateFindings.ReadFrom(answer.Served, system.Id);
      }
      catch (AdapterFailure)
      {
        return;
      }

      opened.LocateServed(system.Id, findings, askedAt);

      if (changes)
      {
        consigned.Add(LedgerEntry.LocateServed(opened.Id, askedAt, system.Id, opened.Designations.Count));
      }

      return;
    }

    if (answer.Outcome == AdapterOutcome.Deferred)
    {
      var deadline = answer.DeclaredDeadline!.Value;

      opened.LocateDeferred(system.Id, deadline, askedAt);

      if (changes)
      {
        consigned.Add(
          LedgerEntry.LocateDeferred(opened.Id, askedAt, system.Id, deadline, opened.Designations.Count));
      }

      return;
    }

    // Un refus ne fait rien avancer du dossier — aucun rattachement, aucun Step —, et sa tentative
    // datée a déjà été consignée par l'appel lui-même, qui sait ce qu'il avait obtenu la fois d'avant.
    opened.LocateRefused(system.Id, answer.Outcome, askedAt);
  }

  /// <summary>
  /// La ligne de preuve de la <c>OpenQuestion</c> qui vient de naître, ou rien.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>Elle ne naît que lorsque tous les <c>Locate</c> ont répondu, et qu'ils ont tous rendu
  /// zéro.</b> Un système qui a différé n'a pas répondu, un système refusé non plus : conclure « la
  /// désignation ne suffit pas » avant de les avoir entendus ferait poser une question dont on ne
  /// sait pas encore si elle se pose.
  /// </para>
  /// <para>
  /// <b>Un dossier sans aucun système atteignable ne pose aucune question.</b> Il n'y a alors pas
  /// six zéros à mal lire : il n'y a eu aucun appel, et le <c>Manifest</c> le dit déjà.
  /// </para>
  /// </remarks>
  private static IEnumerable<LedgerEntry> QuestionOf(
    Case opened,
    IReadOnlyList<DeclaredSystem> reachable,
    DateTimeOffset askedOn)
  {
    var everyoneAnswered = reachable.Count > 0
      && reachable.All(system => opened.LocatingIn(system.Id)?.LastOutcome == AdapterOutcome.Served);

    if (!everyoneAnswered || opened.HoldsAnyAttachment)
    {
      yield break;
    }

    if (opened.Ask(OpenQuestionSubject.Designation, askedOn))
    {
      yield return LedgerEntry.QuestionRaised(opened.Id, askedOn, opened.Designations.Count);
    }
  }

  /// <summary>
  /// Faut-il appeler ce système maintenant ? <b>Quatre raisons, et aucune n'est une minuterie.</b>
  /// </summary>
  private static bool WorthAsking(Locating? known, int designations, DateTimeOffset now)
  {
    if (known is null)
    {
      return true;
    }

    // Le sac s'est enrichi : la question n'est plus la même, et la réponse d'hier répondait à une
    // question plus étroite. C'est très exactement ce qu'un arbitrage vient d'ouvrir.
    if (known.DesignationsAtCall != designations)
    {
      return true;
    }

    // La relance d'un 202, et le seul endroit où elle ait lieu : l'ouverture du dossier. Avant
    // l'échéance déclarée, repasser ferait redire à l'Adapter ce qu'il vient de dire.
    if (known.LastOutcome == AdapterOutcome.Deferred)
    {
      return known.DeclaredDeadline <= now;
    }

    // Un refus se répare ailleurs — dans la configuration de déploiement, ou dans le Manifest — et
    // l'ouverture du dossier est le geste par lequel on va voir si ça l'a été. Le Ledger, lui, ne
    // gardera que le verdict qui change.
    return known.LastOutcome.IsRefusal;
  }
}
