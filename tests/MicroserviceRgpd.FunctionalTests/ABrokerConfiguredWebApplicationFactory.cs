using MicroserviceRgpd.Infrastructure.Configuration;

namespace MicroserviceRgpd.FunctionalTests;

/// <summary>
/// Le service démarré <b>avec une connexion RabbitMQ configurée sur le déploiement</b> : la clé
/// <see cref="RabbitMqOptions.HostNameKey"/> est posée, et c'est la seule chose qui distingue cet
/// hôte de l'hôte ordinaire.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Aucun broker ne tourne derrière cette clé</b>, et c'est tout l'intérêt : le bandeau se
/// décide sur la <b>seule présence</b> de la clé, sans le moindre test réseau (ADR-0027). Un hôte
/// qui aurait dû joindre un broker pour rendre la page aurait prouvé le contraire de ce qu'on
/// cherche.
/// </para>
/// <para>
/// La clé est posée par <c>UseSetting</c>, sous le nom exact que le déploiement emploie — cité sur
/// la constante de production, jamais recopié.
/// </para>
/// </remarks>
public sealed class ABrokerConfiguredWebApplicationFactory : CustomWebApplicationFactory<Program>
{
  /// <summary>L'hôte du broker que ce déploiement déclare — un nom, jamais joint.</summary>
  public const string HostName = "rabbitmq.brocanto.example.fr";

  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
    base.ConfigureWebHost(builder);

    builder.UseSetting(RabbitMqOptions.HostNameKey, HostName);
  }
}

/// <summary>
/// La collection de <see cref="ABrokerConfiguredWebApplicationFactory"/> : un hôte se bâtit une
/// fois, et un hôte dont le déploiement déclare une connexion est une autre forme d'hôte.
/// </summary>
[CollectionDefinition(Name)]
public class ABrokerConfiguredWebCollection : ICollectionFixture<ABrokerConfiguredWebApplicationFactory>
{
  public const string Name = "Web avec une connexion RabbitMQ configurée";
}
