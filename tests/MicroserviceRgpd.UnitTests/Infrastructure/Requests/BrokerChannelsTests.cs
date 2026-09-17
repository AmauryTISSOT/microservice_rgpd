using MicroserviceRgpd.Infrastructure.Configuration;
using MicroserviceRgpd.Infrastructure.Requests;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Requests;

/// <summary>
/// La connexion unique et partagée au broker, sur ce qu'elle refuse et ce qu'elle n'ouvre pas
/// (ADR-0028).
/// </summary>
/// <remarks>
/// ⚠️ <b>Rien ici ne joint un broker</b> : ce qu'une vraie connexion donne s'éprouve contre un vrai
/// broker en conteneur, dans la couture fonctionnelle. Ne restent ici que le refus et la paresse,
/// qui n'ont d'observable nulle part ailleurs.
/// </remarks>
public class BrokerChannelsTests
{
  /// <summary>
  /// ⚠️ <b>Un déploiement sans connexion n'a nulle part où publier</b>, et rien n'aurait dû le lui
  /// demander : le motif <c>BrokerConnectionMissing</c> l'écarte avant. C'est une faute de
  /// programmation — jamais une erreur réseau, qui ferait passer un déploiement muet pour un broker
  /// éteint.
  /// </summary>
  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  public async Task RefusesToOpenAChannelWhenTheDeploymentDeclaresNoConnection(string? hostName)
  {
    await using var channels = new BrokerChannels(new RabbitMqOptions { HostName = hostName });

    await Should.ThrowAsync<InvalidOperationException>(
      async () => await channels.OpenAsync(CancellationToken.None));
  }

  /// <summary>
  /// ⚠️ <b>Construire n'ouvre rien, et jeter non plus</b> : la connexion est paresseuse, et aucune
  /// socket ne s'ouvre au démarrage du service ni au rendu d'un écran (ADR-0027).
  /// </summary>
  [Fact]
  public async Task OpensNothingUntilSomethingPublishes()
  {
    // L'hôte est déclaré mais ne se résout nulle part : si la construction ou la fermeture joignait
    // le broker, elles n'auraient pas où aller.
    var channels = new BrokerChannels(new RabbitMqOptions { HostName = "broker.qui.nexiste.pas.invalid" });

    await Should.NotThrowAsync(async () => await channels.DiscardAsync());
    await Should.NotThrowAsync(async () => await channels.DisposeAsync());
  }
}
