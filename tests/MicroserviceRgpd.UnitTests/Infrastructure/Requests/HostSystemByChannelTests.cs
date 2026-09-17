using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.Infrastructure.Requests;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Requests;

/// <summary>
/// Le <b>seul endroit où le canal se filtre pour appeler</b> refuse les deux cas que les motifs de
/// blocage écartent avant toute remise (ADR-0028).
/// </summary>
/// <remarks>
/// ⚠️ <b>La délégation d'une adresse HTTP ne se vérifie pas ici</b> : la couture fonctionnelle la
/// prouve mieux, contre un vrai système hôte sur un port réel. Ne restent ici que les deux refus, qui
/// n'ont d'observable nulle part ailleurs.
/// </remarks>
public class HostSystemByChannelTests
{
  /// <summary>
  /// ⚠️ <b>Un routage RabbitMQ n'est pas atteignable ici</b> : le service ne sait pas encore publier,
  /// et le motif de blocage le refuse avant. Le dire plutôt que le supposer.
  /// </summary>
  [Fact]
  public async Task RefusesARoutedChannel()
  {
    var routed = new ExerciseChannel.RabbitMq(
      new RabbitMqRouting(ExchangeName.From("rgpd.exercice"), RoutingKey.From("droit.acces")));

    await Should.ThrowAsync<ArgumentException>(
      async () => await HostSystem().ApplyAsync(routed, ABody(), CancellationToken.None));
  }

  /// <summary>⚠️ <b>Un droit non configuré ne s'exerce pas</b> : rien ne part, et rien ne se suppose.</summary>
  [Fact]
  public async Task RefusesANotConfiguredChannel()
  {
    await Should.ThrowAsync<ArgumentException>(
      async () => await HostSystem().ApplyAsync(
        ExerciseChannel.NotConfigured.Instance, ABody(), CancellationToken.None));
  }

  private static ExecutionBody ABody() => new(
    DataSubjectRequestId.Next(),
    DataSubjectRight.Access,
    EmailAddress.From("jeanne.dupont@exemple.fr"),
    null,
    null);

  /// <summary>
  /// Le dispatcher et son unique destinataire. ⚠️ <b>Aucun appel ne part</b> dans ces deux cas : le
  /// client HTTP n'est jamais demandé.
  /// </summary>
  private static HostSystemByChannel HostSystem() =>
    new(new HttpHostSystem(Substitute.For<IHttpClientFactory>(), TimeProvider.System));
}
