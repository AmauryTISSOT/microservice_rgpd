using MicroserviceRgpd.Infrastructure.Data;

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

    logger.LogInformation("{Project} services registered", "Infrastructure");

    return services;
  }
}
