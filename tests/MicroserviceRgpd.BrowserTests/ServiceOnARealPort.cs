using MicroserviceRgpd.Core.Qualifications;
using MicroserviceRgpd.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MicroserviceRgpd.BrowserTests;

/// <summary>
/// <b>Le service tel qu'il tourne</b>, sous Kestrel et sur un vrai port que l'OS attribue — pas le
/// serveur en mémoire des tests fonctionnels, qu'aucun navigateur ne sait atteindre.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Seuls les moteurs de qualification sont substitués</b>, chacun sous son rôle et <b>sur le
/// port du domaine</b>, par la même doublure que <c>CustomWebApplicationFactory</c> : un scénario
/// navigateur ne peut pas attendre un GPU, ni lire une réponse qu'il n'a pas dictée. Rien d'autre
/// ne l'est — ni scanner, ni horloge, ni trace d'audit : aucun scénario navigateur ne l'a encore
/// exigé.
/// </para>
/// <para>
/// La base est un vrai PostgreSQL, que la fixture démarre et dont elle passe la chaîne de connexion.
/// </para>
/// </remarks>
internal sealed class ServiceOnARealPort : WebApplicationFactory<Program>
{
  private readonly string _connectionString;

  /// <summary>
  /// Le moteur dont l'avis fait verdict, substitué — avec la confiance et la justification par défaut
  /// de <c>CustomWebApplicationFactory</c> : un même scénario rend le même écran dans les deux suites.
  /// </summary>
  public QualificationEngineDouble Verdict { get; } = new(
    DeclaredConfidence.High,
    "Le texte demande la suppression des donnees.");

  /// <summary>Le moteur lexical, substitué lui aussi : ni confiance, ni justification.</summary>
  public QualificationEngineDouble Lexicon { get; } = new();

  public ServiceOnARealPort(string connectionString)
  {
    _connectionString = connectionString;

    // Le port 0 laisse l'OS choisir : deux suites qui tournent ensemble ne se disputent aucun port.
    // ⚠️ UseKestrel ne fait que retenir le port ; seul base.CreateHost le pose sur le serveur. Un
    // CreateHost qui s'en passerait laisserait Kestrel sur son adresse par défaut, localhost:5000.
    UseKestrel(0);
  }

  /// <summary>Démarre le service et rend l'adresse à laquelle il écoute, lue sur le serveur.</summary>
  public Uri Start()
  {
    StartServer();

    var addresses = Services.GetRequiredService<IServer>().Features.GetRequiredFeature<IServerAddressesFeature>();

    return new Uri(addresses.Addresses.Single());
  }

  /// <summary>
  /// Pose les deux doublures sous les rôles par lesquels l'application demande ses moteurs, <b>à la
  /// place</b> du câblage réel — qui, sans elles, appellerait le sidecar.
  /// </summary>
  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
    builder.ConfigureTestServices(services =>
    {
      services.RemoveAllKeyed<IQualificationEngine>(QualificationEngineRole.Verdict);
      services.AddKeyedSingleton<IQualificationEngine>(QualificationEngineRole.Verdict, Verdict);

      services.RemoveAllKeyed<IQualificationEngine>(QualificationEngineRole.Lexicon);
      services.AddKeyedSingleton<IQualificationEngine>(QualificationEngineRole.Lexicon, Lexicon);
    });
  }

  protected override IHost CreateHost(IHostBuilder builder)
  {
    builder.UseEnvironment("Testing");

    // Le ConfigurationManager de Program se construit pendant Build : la variable d'environnement est
    // le seul moyen de lui fournir la chaîne assez tôt. Les fabriques de ce processus démarrent l'une
    // après l'autre — toute la collection s'exécute en série — et sur la même base : aucun verrou
    // n'est donc nécessaire, à la différence des tests fonctionnels.
    Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _connectionString);

    // La base construit l'hôte, lui pose le port retenu par UseKestrel, puis le démarre.
    var host = base.CreateHost(builder);

    using var scope = host.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();

    return host;
  }
}
