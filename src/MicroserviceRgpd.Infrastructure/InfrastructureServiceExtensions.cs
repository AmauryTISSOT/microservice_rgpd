using MicroserviceRgpd.Core.Qualifications.Audit;
using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.Infrastructure.Configuration;
using MicroserviceRgpd.Infrastructure.Data;
using MicroserviceRgpd.Infrastructure.Data.Audit;
using MicroserviceRgpd.Infrastructure.Data.Screenings;
using MicroserviceRgpd.Infrastructure.Qualifications;
using MicroserviceRgpd.Infrastructure.Requests;
using MicroserviceRgpd.Infrastructure.Screenings;
using MicroserviceRgpd.Infrastructure.Screenings.Scanning;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MicroserviceRgpd.Infrastructure;
public static class InfrastructureServiceExtensions
{
  public static IServiceCollection AddInfrastructureServices(
    this IServiceCollection services,
    ConfigurationManager config,
    ILogger logger)
  {
    // PostgreSQL est le seul provider supporte. Chaines de connexion, par ordre de priorite :
    // 1. "cleanarchitecture" - fournie par Aspire via .WithReference(cleanArchDb)
    // 2. "DefaultConnection" - PostgreSQL local, hors Aspire
    string? connectionString = config.GetConnectionString("cleanarchitecture")
                               ?? config.GetConnectionString("DefaultConnection");
    Guard.Against.NullOrEmpty(connectionString, nameof(connectionString),
      "Aucune chaine de connexion PostgreSQL configuree : lancer l AppHost Aspire, ou renseigner ConnectionStrings:DefaultConnection.");

    services.AddScoped<EventDispatchInterceptor>();
    services.AddScoped<IDomainEventDispatcher, MediatorDomainEventDispatcher>();

    services.AddDbContext<AppDbContext>((provider, options) =>
    {
      var eventDispatchInterceptor = provider.GetRequiredService<EventDispatchInterceptor>();

      options.UseNpgsql(connectionString);
      options.AddInterceptors(eventDispatchInterceptor);
    });

    services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>))
           .AddScoped(typeof(IReadRepository<>), typeof(EfRepository<>));

    // La trace d'audit ne passe pas par le dépôt générique ci-dessus : celui-ci est contraint aux
    // agrégats racines, et l'emprunter aurait déclaré agrégat ce qui n'est que l'écrit d'un acte.
    services.AddScoped<IQualificationAuditTrail, QualificationAuditTrail>();

    // Les colonnes détectées non plus : elles ont leur propre DbSet sans être une racine, et l'écran
    // n'en ouvre qu'une table à la fois. Le dépôt générique, contraint aux racines, aurait obligé à
    // rematérialiser cinq mille lignes pour en montrer treize.
    services.AddScoped<IScreenedColumns, ScreenedColumns>();

    // L'horloge est injectée pour que l'instant de l'acte se dicte en test, plutôt que d'être lu
    // sur la machine qui l'exécute.
    services.TryAddSingleton(TimeProvider.System);

    services.AddQualificationEngines(config);

    // Le système hôte, que l'exécution d'une demande appelle : un client sans reprise ni redirection,
    // au délai HostSystem:TimeoutSeconds (ADR-0026).
    services.AddHostSystem(config);

    // La connexion du déploiement au broker : ce qu'il déclare sous la section RabbitMq, et l'état
    // qui répond « ce déploiement sait publier » (ADR-0028). ⚠️ Aucune socket ne s'ouvre ici : pas
    // d'hôte, pas de connexion — un état légal, et le service démarre.
    services.AddBrokerConnection(config);

    // Le moteur de détection : le lexique par défaut, démarré avec le service ; A2 si le drapeau
    // Screening:Embeddings:Enabled l'allume, et Ollama entre alors dans la pile. Un seul des deux,
    // choisi ici une fois pour toutes (ADR-0025).
    services.AddScreeningEngine(config);

    // Le port par lequel le service ira lire une base tierce, et le seul dialecte dont il a le
    // pilote aujourd'hui. Il ne se configure pas : la chaîne de connexion arrive par
    // l'écran, à l'appel, et ne se pose nulle part.
    services.AddDatabaseScanner();

    // Ce qui fait courir un scan hors de la requête qui l'a demandé : le fait du déploiement — un
    // seul en vol —, le jeu d'aperçus vivant, le geste et son lanceur. ⚠️ Rien de tout cela ne
    // tourne en fond : il n'y a de scan que parce qu'un Operator vient d'en lancer un.
    services.AddScanning();

    // Le rendu de la Cartographie en fichier. ⚠️ Il est enregistré par son port et sans état : il ne
    // lit aucun dépôt, ne consulte aucune horloge et n'écrit nulle part — ce qui sépare cet export
    // du pont interdit vers le Manifest est qu'il a un destinataire humain qui l'a demandé.
    services.AddSingleton<IScreeningExport, ScreeningExportService>();

    logger.LogInformation("{Project} services registered", "Infrastructure");

    return services;
  }
}
