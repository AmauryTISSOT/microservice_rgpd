using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.Adapters;
using MicroserviceRgpd.Core.Casework.Ledger;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.UseCases.Casework.CallAdapter;

namespace MicroserviceRgpd.UseCases.Casework.Read;

/// <summary>
/// Appelle <c>Read</c> sur chaque <c>DeclaredSystem</c> <b>où le dossier a rattaché</b> et qui
/// déclare la <see cref="Capability.Read"/>, au titre de chaque droit réclamé, et garde ce qui en
/// revient <b>hors de l'agrégat</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Le droit part, la forme jamais.</b> L'appel porte le <see cref="DataSubjectRight"/> au titre
/// duquel on lit et rien de ce qu'on attend en retour : le périmètre matériel de l'art. 20 est plus
/// étroit que celui de l'art. 15 et se décide ligne par ligne, chez le client seul. C'est ce qui
/// permettra à l'application de le trancher elle-même le jour où <c>Portability</c> arrivera, sans
/// qu'une ligne de ce fichier ne change.
/// </para>
/// <para>
/// <b>On ne lit que là où l'on a trouvé.</b> Un système sans rattachement n'est pas appelé : demander
/// les données d'une personne à une application qui vient de dire ne pas la connaître ferait
/// remonter une pièce dont personne ne saurait de qui elle parle. ⚠️ <b>Une réserve en attente n'est
/// pas un rattachement</b> — ce qui manque est un regard, et lire avant qu'un humain n'ait tranché
/// ferait rapatrier les données d'un homonyme.
/// </para>
/// <para>
/// <b>Il n'attend jamais et ne barre jamais la route</b>, comme <c>Locate</c>. Une panne laisse le
/// couple (droit, système) <b>sans lecture</b> plutôt qu'avec une pièce vide : « pas appelé » et
/// « appelé, rien » sont deux déclarations différentes, et confondre la première avec la seconde
/// ferait écrire un constat que personne n'a fait.
/// </para>
/// <para>
/// <b>La pièce est gardée hors du dossier, et le dossier ne retient que le fait.</b> Les deux
/// écritures sont séparées parce que leurs durées de vie le sont : la remise détruira la pièce sans
/// réécrire le <c>Case</c>. L'ordre est <b>appeler, garder la pièce, écrire le dossier, puis
/// consigner</b> — une ligne de preuve pour une lecture que rien ne porte serait un faux, tandis
/// qu'une pièce détenue dont la ligne manque reste <b>visible</b>.
/// </para>
/// </remarks>
/// <param name="cases">Le seul dépôt de ce contexte : les règles sont écrites une fois, sur la racine.</param>
/// <param name="manifest">Le catalogue, relu à chaque passage — il vieillit exprès.</param>
/// <param name="calls">Les appels sortants au titre d'un dossier, tentatives refusées comprises.</param>
/// <param name="retrieved">Les pièces détenues, hors de l'agrégat et pour une durée qui n'est pas la sienne.</param>
/// <param name="ledger">La matière de preuve, en ajout seul.</param>
/// <param name="clock">L'horloge, injectée pour que la date d'une tentative se dicte en test.</param>
public sealed class ReadHandler(
  IRepository<Case> cases,
  IReadRepository<DeclaredSystem> manifest,
  AdapterCallsForCase calls,
  IRetrievedData retrieved,
  ILedger ledger,
  TimeProvider clock)
  : ICommandHandler<ReadCommand, Result>
{
  /// <inheritdoc />
  public async ValueTask<Result> Handle(ReadCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var opened = await cases.FirstOrDefaultAsync(new CaseByIdSpec(command.Case), cancellationToken);

    if (opened is null)
    {
      return Result.NotFound();
    }

    var reachable = (await manifest.ListAsync(cancellationToken))
      .Where(system => system.AdapterAddress is not null && system.Capabilities.Contains(Capability.Read))
      .Where(system => opened.HoldsAnAttachmentIn(system.Id))
      .OrderBy(system => system.Id.Value, StringComparer.Ordinal)
      .ToArray();

    var consigned = new List<LedgerEntry>();
    var askedAt = clock.GetUtcNow();

    // L'ordre est celui de la taxonomie puis du catalogue, et non celui de la saisie : deux dossiers
    // portant les mêmes droits sur le même paysage appellent dans le même ordre, et personne n'a
    // besoin d'un ordre instable pour lire une suite de tentatives.
    foreach (var claim in opened.Claims.OrderBy(claim => claim.Right.Value))
    {
      foreach (var system in reachable)
      {
        await ReadAsync(opened, claim.Right, system, askedAt, consigned, cancellationToken);
      }
    }

    await cases.UpdateAsync(opened, cancellationToken);

    foreach (var entry in consigned)
    {
      await ledger.AppendAsync(entry, cancellationToken);
    }

    return Result.Success();
  }

  private async Task ReadAsync(
    Case opened,
    DataSubjectRight right,
    DeclaredSystem system,
    DateTimeOffset askedAt,
    List<LedgerEntry> consigned,
    CancellationToken cancellationToken)
  {
    var known = opened.ReadingIn(right, system.Id);

    if (!WorthReading(known, opened.Designations.Count, askedAt))
    {
      return;
    }

    // Ce que la preuve gardera ne dépend pas de ce qu'elle porte déjà : c'est l'appelant qui sait
    // sous quoi il avait lu et ce qu'on lui avait répondu.
    var changes = known is null || known.DesignationsAtCall != opened.Designations.Count;

    AdapterAnswer<RetrievedPiece> answer;

    try
    {
      answer = await calls.ReadAsync(
        opened.Id,
        // Le sac part en COPIE, comme pour un Locate : ce qui traverse la frontière est ce sous quoi
        // cet appel-là a lu, et un arbitrage postérieur ne doit pas pouvoir réécrire après coup la
        // question qu'on a posée.
        new AdapterCall(system.AdapterAddress!.Value, system.Id, Capability.Read, [.. opened.Designations]),
        right,
        known?.LastOutcome,
        cancellationToken);
    }
    catch (AdapterFailure)
    {
      // Ni réponse ni refus : personne ne sait qu'en conclure, et le dossier n'apprend rien. La
      // lecture reste ce qu'elle était — non appelée, ou telle que le dernier appel l'a laissée —
      // et aucune pièce n'est gardée. Les autres systèmes sont appelés quand même.
      return;
    }

    changes = changes || known?.LastOutcome != answer.Outcome;

    if (answer.Outcome == AdapterOutcome.Served)
    {
      // La pièce d'abord, le dossier ensuite : une lecture inscrite au dossier dont la pièce n'aurait
      // pas été gardée ferait promettre à l'écran une pièce que la Delivery ne trouverait pas.
      await retrieved.KeepAsync(
        RetrievedData.Of(opened.Id, right, system.Id, answer.Served!, askedAt),
        cancellationToken);

      opened.ReadServed(right, system.Id, askedAt);

      if (changes)
      {
        consigned.Add(
          LedgerEntry.ReadServed(opened.Id, askedAt, system.Id, right, opened.Designations.Count));
      }

      return;
    }

    if (answer.Outcome == AdapterOutcome.Deferred)
    {
      var deadline = answer.DeclaredDeadline!.Value;

      opened.ReadDeferred(right, system.Id, deadline, askedAt);

      if (changes)
      {
        consigned.Add(
          LedgerEntry.ReadDeferred(opened.Id, askedAt, system.Id, right, deadline, opened.Designations.Count));
      }

      return;
    }

    // Un refus ne fait rien avancer du dossier, et sa tentative datée a déjà été consignée par
    // l'appel lui-même, qui sait ce qu'il avait obtenu la fois d'avant.
    opened.ReadRefused(right, system.Id, answer.Outcome, askedAt);
  }

  /// <summary>
  /// Faut-il lire ce système maintenant ? <b>Quatre raisons, et aucune n'est une minuterie</b> — ce
  /// sont celles d'un <c>Locate</c>, et elles valent ici pour les mêmes motifs.
  /// </summary>
  /// <remarks>
  /// <b>Une lecture déjà servie ne repart pas.</b> Sa pièce est détenue, elle répond à la question
  /// qu'on pose sous le sac d'aujourd'hui, et repasser ferait rapatrier une seconde fois les données
  /// de quelqu'un — c'est-à-dire allonger le séjour que tout ce dispositif cherche à raccourcir.
  /// </remarks>
  private static bool WorthReading(Reading? known, int designations, DateTimeOffset now)
  {
    if (known is null)
    {
      return true;
    }

    // Le sac s'est enrichi : la question n'est plus la même, et la pièce d'hier répondait à une
    // question plus étroite. C'est très exactement ce qu'un arbitrage vient d'ouvrir.
    if (known.DesignationsAtCall != designations)
    {
      return true;
    }

    // La relance d'un 202, et le seul endroit où elle ait lieu : l'ouverture du dossier.
    if (known.LastOutcome == AdapterOutcome.Deferred)
    {
      return known.DeclaredDeadline <= now;
    }

    // Un refus se répare ailleurs — dans la configuration de déploiement, ou dans le Manifest — et
    // l'ouverture du dossier est le geste par lequel on va voir si ça l'a été.
    return known.LastOutcome.IsRefusal;
  }
}
