using MicroserviceRgpd.Core.Casework.Ledger;
using MicroserviceRgpd.Core.Qualifications.Audit;
using MicroserviceRgpd.Infrastructure.Data;
using MicroserviceRgpd.Infrastructure.Data.Audit;
using MicroserviceRgpd.Infrastructure.Data.Casework;
using MicroserviceRgpd.Infrastructure.Qualifications;
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

    // Le Ledger non plus : il est hors de l'agrégat par construction — il survit au Case de cinq
    // ans — et le dépôt générique lui aurait rendu la mise à jour et la suppression ligne à ligne
    // que sa définition ferme.
    services.AddScoped<ILedger, Ledger>();

    // L'horloge est injectée pour que l'instant de l'acte se dicte en test, plutôt que d'être lu
    // sur la machine qui l'exécute.
    services.TryAddSingleton(TimeProvider.System);

    services.AddQualificationEngines(config);

    logger.LogInformation("{Project} services registered", "Infrastructure");

    return services;
  }
}
