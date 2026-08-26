using MicroserviceRgpd.Core.Casework.Adapters;
using MicroserviceRgpd.Core.Qualifications;
using MicroserviceRgpd.Core.Qualifications.Audit;
using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.Infrastructure.Data;
using MicroserviceRgpd.Infrastructure.Casework.Adapters;
using MicroserviceRgpd.Infrastructure.Data.Audit;
using MicroserviceRgpd.FunctionalTests.Platform;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace MicroserviceRgpd.FunctionalTests;

public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram>, IAsyncLifetime where TProgram : class
{
  // Docker est requis : PostgreSQL est le seul provider supporte, il n existe plus de repli local.
  private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder("postgres:18-alpine").Build();

  /// <summary>Serialise la pose de la chaine de connexion et la construction de l hote qui la lit.</summary>
  private static readonly Lock HostBuilds = new();

  /// <summary>
  /// Le moteur dont l avis fait verdict, substitue. La doublure se pose <b>sur le port du domaine</b>,
  /// et non sur le fil HTTP : aucun test .NET n appelle le sidecar reel, et aucun n approche un GPU.
  /// Un test qui exigerait un GPU ne tournerait jamais, et un test qui ne tourne jamais ment.
  /// <para>
  /// Elle n est pas posee dans un hote demarre LLM eteint : ce role y est laisse au cablage reel,
  /// qui ne le pourvoit par rien. Voir <see cref="SubstitutesTheVerdictRole"/>.
  /// </para>
  /// </summary>
  public QualificationEngineDouble Verdict { get; } = new(
    Core.Qualifications.DeclaredConfidence.High,
    "Le texte demande la suppression des donnees.");

  /// <summary>Le moteur lexical, substitue lui aussi : ni confiance, ni justification.</summary>
  public QualificationEngineDouble Lexicon { get; } = new();

  /// <summary>
  /// De quoi faire echouer l ecriture de la trace. La trace elle-meme n est pas substituee : elle
  /// ecrit dans le vrai PostgreSQL, et c est ce qui donne du sens a « qualifier, ecrire, repondre ».
  /// </summary>
  public AuditTrailControl AuditTrail { get; } = new();

  /// <summary>
  /// L'<c>Adapter</c> du client, posé <b>sur le fil</b> et non sur le port du domaine. L'en-tête de
  /// secret, le <c>system_id</c> en paramètre et le corps du sac <b>sont</b> le contrat : doubler le
  /// port les aurait cachés au-dessus de la couture, et les tests n'auraient plus prouvé que le
  /// comportement d'une doublure.
  /// </summary>
  public ABrocantoOnTheWire Adapter { get; } = new();

  /// <summary>
  /// Le scanner de base, substitué <b>sur le port du domaine</b>. C'est le seul point du contexte
  /// <c>Screening</c> qui touche une base d'un tiers : aucun test fonctionnel n'ouvre de base
  /// réelle, et un écran de scan s'éprouve sans conteneur.
  /// </summary>
  public DatabaseScannerDouble Scanner { get; } = new();

  /// <summary>
  /// Tout ce que le service journalise. ⚠️ <b>C'est le canari de la chaîne de connexion</b> : sans
  /// un collecteur posé sur le vrai pipeline de journalisation, « elle n'apparaît dans aucun
  /// journal » ne serait qu'une intention écrite dans un commentaire.
  /// </summary>
  public CapturedLogs Logs { get; } = new();

  /// <summary>
  /// L'horloge du service, qu'un test peut faire avancer. ⚠️ <b>C'est le seul levier sur la durée de
  /// vie des aperçus</b> : le cache n'est pas une couture de test, et un test qui voudrait le voir
  /// expirer n'a que le temps à tourner — exactement comme l'exploitation.
  /// </summary>
  public AClockTheTestsAdvance Clock { get; } = new();

  /// <summary>
  /// Le role de verdict est-il substitue ? <b>Non</b> dans un hote demarre LLM eteint : le laisser
  /// au cablage reel est la seule facon de prouver quelque chose du drapeau — une doublure posee
  /// par-dessus ne prouverait que la presence de cette doublure.
  /// </summary>
  protected virtual bool SubstitutesTheVerdictRole => true;

  public Task InitializeAsync() => _dbContainer.StartAsync();

  public new Task DisposeAsync() => _dbContainer.DisposeAsync().AsTask();

  /// <summary>
  /// Overriding CreateHost to avoid creating a separate ServiceProvider per this thread:
  /// https://github.com/dotnet-architecture/eShopOnWeb/issues/465
  /// </summary>
  /// <param name="builder"></param>
  /// <returns></returns>
  protected override IHost CreateHost(IHostBuilder builder)
  {
    builder.UseEnvironment("Testing"); // will not send real emails

    IHost host;

    // Le ConfigurationManager de Program est construit ici meme, pendant Build : la variable d
    // environnement est le seul moyen de fournir la chaine assez tot, et la poser juste avant est
    // ce qui donne a chaque hote le conteneur de SA fabrique. Plusieurs fabriques coexistent — une
    // par forme d hote demarree —, et le nom de la variable, lui, est global au processus : sans ce
    // verrou, un hote batirait son contexte sur le conteneur d une autre fabrique.
    lock (HostBuilds)
    {
      Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _dbContainer.GetConnectionString());

      host = builder.Build();
    }

    host.Start();

    // Get service provider.
    var serviceProvider = host.Services;

    // Create a scope to obtain a reference to the database
    // context (AppDbContext).
    using (var scope = serviceProvider.CreateScope())
    {
      var scopedServices = scope.ServiceProvider;
      var db = scopedServices.GetRequiredService<AppDbContext>();

      var logger = scopedServices
          .GetRequiredService<ILogger<CustomWebApplicationFactory<TProgram>>>();

      try
      {
        // PostgreSQL via Testcontainers: apply migrations to create the schema
        db.Database.Migrate();
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "An error occurred creating the " +
                            "test database. Error: {exceptionMessage}", ex.Message);
      }
    }

    return host;
  }

  /// <summary>
  /// Les seules choses substituees sont les moteurs, chacun sous le role par lequel l application
  /// le demande. Tout le reste est l application telle quelle : elle resout elle-meme sa chaine de
  /// connexion depuis <c>ConnectionStrings:DefaultConnection</c>, exactement comme hors tests.
  /// <para>
  /// Un hote demarre LLM eteint ne substitue que le lexique, et laisse le role de verdict au cablage
  /// reel — qui ne le pourvoit alors par rien. Aucune doublure ne se pose sur le fil HTTP dans un
  /// cas comme dans l autre : aucun test .NET n approche un modele.
  /// </para>
  /// </summary>
  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
    builder.ConfigureTestServices(services =>
    {
      services.RemoveAllKeyed<IQualificationEngine>(QualificationEngineRole.Lexicon);
      services.AddKeyedSingleton<IQualificationEngine>(QualificationEngineRole.Lexicon, Lexicon);

      if (SubstitutesTheVerdictRole)
      {
        services.RemoveAllKeyed<IQualificationEngine>(QualificationEngineRole.Verdict);
        services.AddKeyedSingleton<IQualificationEngine>(QualificationEngineRole.Verdict, Verdict);
      }

      services.RemoveAll<IDatabaseScanner>();
      services.AddSingleton<IDatabaseScanner>(Scanner);

      // ⚠️ L'horloge est SUBSTITUÉE, jamais ajoutée : le câblage réel la pose en TryAdd, et un
      // second enregistrement aurait laissé la première gagner. Elle dit l'heure réelle tant qu'un
      // test ne l'avance pas — ce qui laisse intacts tous ceux qui datent à la journée.
      services.RemoveAll<TimeProvider>();
      services.AddSingleton<TimeProvider>(Clock);

      // Le collecteur s'AJOUTE aux fournisseurs en place : rien n'est retiré, et ce que le service
      // journalise en test est exactement ce qu'il journalise ailleurs.
      services.AddSingleton<ILoggerProvider>(Logs);

      // L Adapter du client est pose sur le FIL : le vrai HttpAdapterCalls reste en place, avec son
      // en-tete de secret, son system_id en parametre et son corps de sac. C est le contrat qu on
      // eprouve, pas une doublure de port.
      services.AddHttpClient<IAdapterCalls, HttpAdapterCalls>()
        .ConfigurePrimaryHttpMessageHandler(() => Adapter);

      // L adaptateur reel reste au bout de la chaine : la surveillance n intercepte que pour lui
      // dicter une panne, jamais pour se substituer a l ecriture.
      services.AddScoped<IQualificationAuditTrail>(provider => new SupervisedAuditTrail(
        new QualificationAuditTrail(provider.GetRequiredService<AppDbContext>()), AuditTrail));
    });
  }
}
