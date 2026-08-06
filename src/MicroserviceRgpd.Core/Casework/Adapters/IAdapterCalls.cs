using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.Core.Casework.Adapters;

/// <summary>
/// Les appels sortants vers les <c>Adapter</c> du client. <b>Le sens est unique</b> : le service
/// appelle toujours, l'application ne rappelle jamais.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ce port ne cache pas le contrat, il le nomme.</b> L'en-tête de secret, le différé et son
/// échéance, les deux refus : tout ce que l'intégrateur doit connaître se lit dans
/// <see cref="AdapterAnswer{TServed}"/> et dans <see cref="AdapterOutcome"/>. Un port qui rendrait
/// « le résultat ou rien » aurait mis au-dessus de la couture ce qui <b>est</b> le contrat, et les
/// tests auraient alors prouvé le comportement d'une doublure plutôt que celui du fil.
/// </para>
/// <para>
/// ⚠️ <b>La couture de test est donc sur le fil</b>, pas ici : un <c>HttpMessageHandler</c> injecté
/// dans le client de l'implémentation. Doubler ce port dans un test d'<c>Adapter</c> ne prouverait
/// rien de l'en-tête ni du <c>202</c>.
/// </para>
/// </remarks>
public interface IAdapterCalls
{
  /// <summary>
  /// Porte un appel jusqu'à l'<c>Adapter</c> et en rapporte la réponse — servi, différé, ou l'un
  /// des deux refus. <b>Aucune connexion n'est tenue</b> : un travail long se répond par un différé
  /// et une échéance, jamais par une attente.
  /// </summary>
  /// <typeparam name="TServed">Ce que la <see cref="Capability"/> appelée rend quand elle sert.</typeparam>
  /// <param name="call">Ce qu'on demande, et à qui.</param>
  /// <param name="cancellationToken">L'annulation de l'échange en cours.</param>
  /// <exception cref="AdapterFailure">
  /// L'<c>Adapter</c> n'a rendu ni réponse ni refus : muet, illisible, statut hors contrat, ou
  /// différé sans échéance déclarée.
  /// </exception>
  Task<AdapterAnswer<TServed>> AskAsync<TServed>(AdapterCall call, CancellationToken cancellationToken = default)
    where TServed : class;

  /// <summary>
  /// Porte un <c>Read</c> jusqu'à l'<c>Adapter</c> et en rapporte un <b>flux d'octets</b> dont le
  /// service ne connaît que la <see cref="TransportEnvelope"/>.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>Elle est à part, et le type de retour est la raison.</b> Les trois autres capacités rendent
  /// du JSON d'une forme que le contrat fixe ; <c>Read</c> rend ce que l'application veut, et le
  /// service n'en lira jamais le corps. Passer cela sous <c>AskAsync&lt;TServed&gt;</c> aurait exigé un
  /// <c>TServed</c> désérialisable — c'est-à-dire une forme imposée à l'<c>Adapter</c>, très
  /// exactement ce que ce ticket refuse.
  /// </para>
  /// <para>
  /// <b>Le droit voyage, la forme jamais.</b> L'appel porte le <see cref="DataSubjectRight"/>
  /// <b>au titre duquel</b> on lit, et rien de ce qu'on attend en retour : le périmètre matériel de
  /// l'art. 20 est plus étroit que celui de l'art. 15 et se décide ligne par ligne, chez le client
  /// seul, qui tranche du même geste le périmètre et la forme.
  /// </para>
  /// </remarks>
  /// <param name="call">Ce qu'on demande, et à qui. Sa <see cref="Capability"/> est <c>Read</c>.</param>
  /// <param name="right">Le droit au titre duquel on lit.</param>
  /// <param name="cancellationToken">L'annulation de l'échange en cours.</param>
  /// <exception cref="AdapterFailure">
  /// L'<c>Adapter</c> n'a rendu ni réponse ni refus : muet, statut hors contrat, ou différé sans
  /// échéance déclarée.
  /// </exception>
  Task<AdapterAnswer<RetrievedPiece>> ReadAsync(
    AdapterCall call,
    DataSubjectRight right,
    CancellationToken cancellationToken = default);
}
