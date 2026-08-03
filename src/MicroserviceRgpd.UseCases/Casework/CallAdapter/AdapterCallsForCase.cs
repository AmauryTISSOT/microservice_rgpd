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
/// ⚠️ <b>Une tentative refusée s'inscrit à chaque fois.</b> La règle « le <c>Ledger</c> consigne ce
/// qui change, jamais la répétition » vise les <b>relances</b> — trente-cinq passages rendant le
/// même différé, déclenchés par un affichage et par aucun humain. Ici, chaque appel part d'un geste
/// distinct, et le <c>Ledger</c> ne se relit pas pour savoir ce qu'il portait déjà : une écriture
/// qui lirait la ligne d'avant serait une écriture qu'une ligne d'avant pourrait faire mentir. Le
/// jour où une relance existera, c'est <b>l'appelant</b> qui décidera de ne pas la répéter.
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
  /// Porte un appel au titre d'un dossier, et rend le verdict tel quel — servi, différé, ou l'un
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
  /// <param name="cancellationToken">L'annulation de l'échange en cours.</param>
  /// <exception cref="AdapterFailure">L'<c>Adapter</c> n'a rendu ni réponse ni refus.</exception>
  public async Task<AdapterAnswer<TServed>> AskAsync<TServed>(
    CaseId caseId,
    AdapterCall call,
    CancellationToken cancellationToken = default)
    where TServed : class
  {
    ArgumentNullException.ThrowIfNull(call);

    var answer = await calls.AskAsync<TServed>(call, cancellationToken);

    if (!answer.Verdict.IsRefusal)
    {
      return answer;
    }

    await ledger.AppendAsync(
      LedgerEntry.AdapterRefused(caseId, clock.GetUtcNow(), call.DeclaredSystem, answer.Verdict),
      cancellationToken);

    disagreements.Signal(call.DeclaredSystem, answer.Verdict);

    return answer;
  }
}
