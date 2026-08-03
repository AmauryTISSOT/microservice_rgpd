namespace MicroserviceRgpd.Core.Casework.Adapters;

/// <summary>
/// Les appels sortants vers les <c>Adapter</c> du client. <b>Le sens est unique</b> : le service
/// appelle toujours, l'application ne rappelle jamais.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ce port ne cache pas le contrat, il le nomme.</b> L'en-tête de secret, le différé et son
/// échéance, les deux refus : tout ce que l'intégrateur doit connaître se lit dans
/// <see cref="AdapterAnswer{TServed}"/> et dans <see cref="AdapterVerdict"/>. Un port qui rendrait
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
  /// Porte un appel jusqu'à l'<c>Adapter</c> et en rapporte le verdict — servi, différé, ou l'un
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
}
