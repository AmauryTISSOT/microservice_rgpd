using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.Infrastructure.Configuration;
using MicroserviceRgpd.Infrastructure.Requests;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Requests;

/// <summary>
/// Le <b>seul endroit où le canal se filtre pour remettre un droit</b> refuse le cas que les motifs de
/// blocage écartent avant toute remise (ADR-0028).
/// </summary>
/// <remarks>
/// ⚠️ <b>Ni la délégation d'une adresse HTTP ni celle d'un routage ne se vérifient ici</b> : la couture
/// fonctionnelle les prouve mieux, contre un vrai système hôte sur un port réel et un vrai broker en
/// conteneur. Ne reste ici que le refus, qui n'a d'observable nulle part ailleurs.
/// </remarks>
public class HostSystemByChannelTests
{
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
  /// Le dispatcher et ses deux destinataires. ⚠️ <b>Rien ne part</b> dans ce cas : ni le client HTTP,
  /// ni un channel du broker ne sont jamais demandés.
  /// </summary>
  private static HostSystemByChannel HostSystem() =>
    new(
      new HttpHostSystem(Substitute.For<IHttpClientFactory>(), TimeProvider.System),
      new RabbitMqHostSystem(
        Substitute.For<IBrokerChannels>(), new RabbitMqOptions(), TimeProvider.System));
}
