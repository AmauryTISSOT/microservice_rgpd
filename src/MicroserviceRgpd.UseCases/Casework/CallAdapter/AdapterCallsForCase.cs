using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.Adapters;
using MicroserviceRgpd.Core.Casework.Ledger;

namespace MicroserviceRgpd.UseCases.Casework.CallAdapter;

/// <summary>
/// L'appel d'un <c>Adapter</c> <b>au titre d'un <see cref="Case"/></b> : il appelle, et d'un refus
/// il ne fait qu'une <b>tentative datée</b> dans la preuve et un désaccord signalé <b>une fois</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Un appel refusé ne modifie rien dans le dossier.</b> Aucun <c>Step</c> ne change d'état,
/// aucun <c>Claim</c> ne bouge : un secret périmé ou un <c>Adapter</c> qui ne sert pas ce système
/// n'apprend rien sur le travail dû — il dit que le service et l'application ne sont pas d'accord.
/// Faire avancer le dossier là-dessus écrirait dans la preuve une conclusion que personne n'a
/// tirée. La règle tient par la <b>forme</b> : ce type ne reçoit pas de <c>Case</c>, seulement son
/// identité, et n'a donc rien à faire avancer.
/// </para>
/// <para>
/// <b>Deux destinataires, deux grains.</b> Le <c>Ledger</c> reçoit la tentative, datée, dans le
/// dossier au titre duquel elle est partie — c'est de la matière de preuve, et le contrôle la lit
/// dossier par dossier. Le signalement de désaccord, lui, s'adresse à qui exploite le service et
/// vaut pour le déploiement entier : une panne unique n'est pas N pannes.
/// </para>
/// <para>
/// ⚠️ <b>Une tentative refusée ne s'inscrit que si le verdict change.</b> La relance existe désormais
/// — rouvrir un dossier fait repartir les appels —, et trente-cinq passages rendant le même refus
/// n'ont aucun signataire : c'est un affichage qui les a déclenchés, non un humain, et ils
/// noieraient sous du bruit de mécanique ce que le contrôle vient lire. La règle est tenue par
/// l'<b>appelant</b>, qui dit ce qu'il avait obtenu la fois d'avant : le <c>Ledger</c>, lui, ne se
/// relit pas — une écriture qui lirait la ligne d'avant serait une écriture qu'une ligne d'avant
/// pourrait faire mentir.
/// </para>
/// <para>
/// <b>Servir et différer ne laissent rien ici.</b> Ce qu'ils deviennent — un <c>Step</c>
/// <c>Awaiting</c> avec son échéance, une réserve à arbitrer — appartient à qui a demandé l'appel,
/// et ce type ne prétend pas le savoir.
/// </para>
/// </remarks>
/// <param name="calls">Les appels sortants. Le sens est unique : le service appelle, l'application ne rappelle jamais.</param>
/// <param name="ledger">La matière de preuve, en ajout seul.</param>
/// <param name="disagreements">Le signalement des désaccords <c>Manifest</c>/<c>Adapter</c>, au grain du déploiement.</param>
/// <param name="clock">
/// L'horloge, injectée pour que la date d'une tentative se dicte en test plutôt que d'être lue sur
/// la machine qui l'exécute.
/// </param>
public sealed class AdapterCallsForCase(
  IAdapterCalls calls,
  ILedger ledger,
  IAdapterDisagreements disagreements,
  TimeProvider clock)
{
  /// <summary>
  /// Porte un appel au titre d'un dossier, et rend la réponse telle quelle — servi, différé, ou l'un
  /// des deux refus.
  /// </summary>
  /// <remarks>
  /// <b>L'ordre est : consigner, puis signaler.</b> Les deux pannes ne se valent pas — une preuve
  /// écrite sans que l'exploitant soit prévenu reste une preuve, un signal émis sans que rien ne
  /// soit consigné est un bruit qui ne laisse aucune trace.
  /// </remarks>
  /// <typeparam name="TServed">Ce que la <see cref="Capability"/> appelée rend quand elle sert.</typeparam>
  /// <param name="caseId">Le dossier au titre duquel l'appel part.</param>
  /// <param name="call">Ce qu'on demande, et à qui.</param>
  /// <param name="previously">
  /// Ce que ce même <c>Adapter</c> avait répondu la dernière fois sur ce système, ou <c>null</c> si
  /// on ne l'avait jamais appelé. C'est ce que l'<b>appelant</b> sait et que le <c>Ledger</c> ne
  /// saura jamais : celui-ci ne se relit pas, une écriture qui lirait la ligne d'avant étant une
  /// écriture qu'une ligne d'avant pourrait faire mentir.
  /// </param>
  /// <param name="cancellationToken">L'annulation de l'échange en cours.</param>
  /// <exception cref="AdapterFailure">L'<c>Adapter</c> n'a rendu ni réponse ni refus.</exception>
  public async Task<AdapterAnswer<TServed>> AskAsync<TServed>(
    CaseId caseId,
    AdapterCall call,
    AdapterOutcome? previously = null,
    CancellationToken cancellationToken = default)
    where TServed : class
  {
    ArgumentNullException.ThrowIfNull(call);

    var answer = await calls.AskAsync<TServed>(call, cancellationToken);

    if (!answer.Outcome.IsRefusal)
    {
      return answer;
    }

    // Le désaccord se signale à chaque fois : c'est un compte d'exploitation au grain du
    // déploiement, et son dédoublonnage vit dans le signalement lui-même. La PREUVE, elle, ne
    // consigne que ce qui change — une relance rendant le même refus n'a aucun signataire.
    if (answer.Outcome == previously)
    {
      disagreements.Signal(call.DeclaredSystem, answer.Outcome);

      return answer;
    }

    await ledger.AppendAsync(
      LedgerEntry.AdapterRefused(caseId, clock.GetUtcNow(), call.DeclaredSystem, answer.Outcome),
      cancellationToken);

    disagreements.Signal(call.DeclaredSystem, answer.Outcome);

    return answer;
  }
}
