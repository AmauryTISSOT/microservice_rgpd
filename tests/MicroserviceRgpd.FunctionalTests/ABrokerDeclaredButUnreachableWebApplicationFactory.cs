using System.Globalization;
using System.Net;
using System.Net.Sockets;
using MicroserviceRgpd.Infrastructure.Configuration;

namespace MicroserviceRgpd.FunctionalTests;

/// <summary>
/// Le service démarré <b>avec une connexion déclarée vers un broker qui n'existe pas</b> : un port
/// de la boucle locale sur lequel personne n'écoute.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>C'est le cas prévu par l'ADR-0028, pas une erreur de configuration</b> : une connexion
/// <c>Configured</c> ne promet aucun broker joignable, et c'est l'exécution — et elle seule — qui
/// découvre l'échec. Le bouton reste allumé, la publication rend une erreur réseau, la demande reste
/// En cours, et le journal le dit.
/// </para>
/// <para>
/// <b>Un hôte plutôt qu'un conteneur qu'on éteindrait</b> : un port fermé est instantané, et
/// arrêter le broker partagé d'une autre collection en plein vol ferait dépendre le résultat de
/// l'ordre des tests. C'est le même procédé que le système hôte injoignable des appels HTTP.
/// </para>
/// </remarks>
public sealed class ABrokerDeclaredButUnreachableWebApplicationFactory : CustomWebApplicationFactory<Program>
{
  /// <summary>L'hôte déclaré : la boucle locale, où le service ira vraiment frapper.</summary>
  public const string HostName = "127.0.0.1";

  /// <summary>Un port sur lequel personne n'écoute, choisi une fois pour cet hôte.</summary>
  public int Port { get; } = AClosedPort();

  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
    base.ConfigureWebHost(builder);

    builder.UseSetting(RabbitMqOptions.HostNameKey, HostName);
    builder.UseSetting(RabbitMqOptions.PortKey, Port.ToString(CultureInfo.InvariantCulture));
  }

  /// <summary>
  /// Un port libre, rendu aussitôt : l'OS en attribue un, et l'écoute se referme avant que personne
  /// s'en serve.
  /// </summary>
  private static int AClosedPort()
  {
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();

    return port;
  }
}

/// <summary>
/// La collection de <see cref="ABrokerDeclaredButUnreachableWebApplicationFactory"/> : un hôte se
/// bâtit une fois, et un déploiement qui déclare un broker absent est une autre forme d'hôte.
/// </summary>
[CollectionDefinition(Name)]
public class ABrokerDeclaredButUnreachableWebCollection
  : ICollectionFixture<ABrokerDeclaredButUnreachableWebApplicationFactory>
{
  public const string Name = "Web avec un broker déclaré mais injoignable";
}
