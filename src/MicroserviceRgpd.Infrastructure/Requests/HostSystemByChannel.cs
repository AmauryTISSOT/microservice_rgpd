using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.Requests;

namespace MicroserviceRgpd.Infrastructure.Requests;

/// <summary>
/// Le système hôte joint <b>par le canal que le Paramétrage a déclaré</b> : il lit l'espèce du canal
/// et remet le droit à l'adaptateur qui sait l'exercer (ADR-0028).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>C'est le seul endroit du service où le canal se filtre pour remettre un droit.</b> Le canal
/// se lit ailleurs — pour dire ce qui bloque, pour écrire le journal, pour l'afficher —, mais nul
/// autre endroit ne choisit <i>comment</i> exercer : le reste du service ne connaît que le port, et
/// qu'un geste.
/// </para>
/// <para>
/// <b>Il ne juge rien et n'ajoute rien</b> : ni délai, ni reprise, ni lecture de ce que l'adaptateur
/// rend. Ce que <see cref="HttpHostSystem"/> ou <see cref="RabbitMqHostSystem"/> rend est ce qu'il rend.
/// </para>
/// </remarks>
/// <param name="http">Le système hôte joint par HTTP, à l'adresse déclarée.</param>
/// <param name="rabbit">Le système hôte joint par une publication RabbitMQ, sur le routage déclaré.</param>
public sealed class HostSystemByChannel(HttpHostSystem http, RabbitMqHostSystem rabbit) : IHostSystem
{
  /// <inheritdoc />
  /// <exception cref="ArgumentNullException"><paramref name="channel"/> est absent.</exception>
  /// <exception cref="ArgumentException">
  /// Le canal est « non configuré » : le motif de blocage l'écarte avant toute remise.
  /// </exception>
  public Task<HostSystemCall> ApplyAsync(
    ExerciseChannel channel,
    ExecutionBody body,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(channel);

    return channel switch
    {
      ExerciseChannel.HttpEndpoint endpoint => http.ApplyAsync(endpoint.Address, body, cancellationToken),
      ExerciseChannel.RabbitMq routed => rabbit.PublishAsync(routed.Routing, body, cancellationToken),

      // Le dernier cas n'est pas atteignable : le handler d'exécution refuse un droit non configuré
      // sans rien remettre. Le dire plutôt que le supposer.
      ExerciseChannel.NotConfigured => throw new ArgumentException(
        "Un droit non configuré ne s'exerce pas : il n'y a rien à qui le remettre.",
        nameof(channel)),

      // Le genre est fermé à ces trois cas : ce bras n'existe que parce que le compilateur l'exige.
      _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, "Espèce de canal inconnue."),
    };
  }
}
