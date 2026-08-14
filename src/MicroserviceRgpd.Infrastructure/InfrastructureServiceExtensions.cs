using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.EvidenceLog;
using MicroserviceRgpd.Core.Qualifications.Audit;
using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.Infrastructure.Data;
using MicroserviceRgpd.Infrastructure.Data.Audit;
using MicroserviceRgpd.Infrastructure.Casework.Adapters;
using MicroserviceRgpd.Infrastructure.Data.Casework;
using MicroserviceRgpd.Infrastructure.Data.Screenings;
using MicroserviceRgpd.Infrastructure.Qualifications;
using MicroserviceRgpd.Infrastructure.Screenings;
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

    // L'EvidenceLog non plus : il est hors de l'agrégat par construction — il survit au Case de cinq
    // ans — et le dépôt générique lui aurait rendu la mise à jour et la suppression ligne à ligne
    // que sa définition ferme.
    services.AddScoped<IEvidenceLog, EvidenceLog>();

    // La seule suppression du dispositif vit dans son propre type, à part de l'ajout : l'adaptateur
    // qui écrit la preuve ne sait toujours ni la relire ni l'effacer, et celui qui détruit un
    // EvidenceLog échu ne sait rien écrire. ⚠️ Rien ne l'appelle qu'un clic d'Operator.
    services.AddScoped<IExpiredEvidenceLogs, ExpiredEvidenceLogs>();

    // Les pièces lues non plus : elles sont hors de l'agrégat, avec leur durée de vie propre — la
    // remise les détruira sans réécrire le Case —, et le dépôt générique aurait fait d'un contenu
    // personnel une racine que tout le service pourrait charger.
    services.AddScoped<IRetrievedData, RetrievedDataStore>();

    // Les colonnes dépistées non plus : elles ont leur propre DbSet sans être une racine, et l'écran
    // n'en ouvre qu'une table à la fois. Le dépôt générique, contraint aux racines, aurait obligé à
    // rematérialiser cinq mille lignes pour en montrer treize.
    services.AddScoped<IScreenedColumns, ScreenedColumns>();

    // L'horloge est injectée pour que l'instant de l'acte se dicte en test, plutôt que d'être lu
    // sur la machine qui l'exécute.
    services.TryAddSingleton(TimeProvider.System);

    services.AddQualificationEngines(config);

    // Le moteur de dépistage, lui, ne se configure pas : ADR-0004 l'a mis en C# ici même, sans
    // sidecar, sans adresse et sans échéance. Il démarre avec le service.
    services.AddScreeningEngine();

    // Les appels sortants vers les Adapter du client. Le secret absent arrête le démarrage : aucun
    // mode « sans » ne survit à l'intégration.
    services.AddAdapterCalls(config);

    logger.LogInformation("{Project} services registered", "Infrastructure");

    return services;
  }
}
