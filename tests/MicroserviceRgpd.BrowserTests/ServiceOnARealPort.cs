using MicroserviceRgpd.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;

namespace MicroserviceRgpd.BrowserTests;

/// <summary>
/// <b>Le service tel qu'il tourne</b>, sous Kestrel et sur un vrai port que l'OS attribue — pas le
/// serveur en mémoire des tests fonctionnels, qu'aucun navigateur ne sait atteindre.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Rien n'est substitué.</b> Le parcours du panneau latéral n'approche ni un moteur, ni un
/// scanner, ni un <c>Adapter</c> : poser des doublures ici n'aurait rien prouvé de plus. Le jour où
/// un scénario navigateur en exigera une, elle se posera ici, sur le port du domaine, comme dans
/// <c>CustomWebApplicationFactory</c>.
/// </para>
/// <para>
/// La base est un vrai PostgreSQL, que la fixture démarre et dont elle passe la chaîne de connexion.
/// </para>
/// </remarks>
internal sealed class ServiceOnARealPort : WebApplicationFactory<Program>
{
  private readonly string _connectionString;

  public ServiceOnARealPort(string connectionString)
  {
    _connectionString = connectionString;

    // Le port 0 laisse l'OS choisir : deux suites qui tournent ensemble ne se disputent aucun port.
    UseKestrel(0);
  }

  /// <summary>L'adresse à laquelle le service écoute, lue sur le serveur une fois démarré.</summary>
  public Uri Address
  {
    get
    {
      StartServer();

      var addresses = Services.GetRequiredService<IServer>().Features.GetRequiredFeature<IServerAddressesFeature>();

      return new Uri(addresses.Addresses.Single());
    }
  }

  protected override IHost CreateHost(IHostBuilder builder)
  {
    builder.UseEnvironment("Testing");

    // Le ConfigurationManager de Program se construit pendant Build : la variable d'environnement est
    // le seul moyen de lui fournir la chaîne assez tôt. Une seule fabrique vit dans ce processus,
    // aucun verrou n'est donc nécessaire, à la différence des tests fonctionnels.
    Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _connectionString);

    var host = builder.Build();
    host.Start();

    using var scope = host.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();

    return host;
  }
}
