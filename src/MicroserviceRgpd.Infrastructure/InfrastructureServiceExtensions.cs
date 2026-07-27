using MicroserviceRgpd.Infrastructure.Data;

namespace MicroserviceRgpd.Infrastructure;
public static class InfrastructureServiceExtensions
{
  public static IServiceCollection AddInfrastructureServices(
    this IServiceCollection services,
    ConfigurationManager config,
    ILogger logger)
  {
    // Chaines de connexion, par ordre de priorite :
    // 1. "cleanarchitecture" - fournie par Aspire via .WithReference(cleanArchDb) -> PostgreSQL
    // 2. "DefaultConnection" - PostgreSQL local, hors Aspire
    // 3. "SqliteConnection"  - repli local sans Docker
    string? postgresConnection = config.GetConnectionString("cleanarchitecture")
                                 ?? config.GetConnectionString("DefaultConnection");

    string? connectionString = postgresConnection ?? config.GetConnectionString("SqliteConnection");
    Guard.Against.Null(connectionString);

    services.AddScoped<EventDispatchInterceptor>();
    services.AddScoped<IDomainEventDispatcher, MediatorDomainEventDispatcher>();

    services.AddDbContext<AppDbContext>((provider, options) =>
    {
      var eventDispatchInterceptor = provider.GetRequiredService<EventDispatchInterceptor>();

      if (postgresConnection is not null)
      {
        options.UseNpgsql(postgresConnection);
      }
      else
      {
        options.UseSqlite(connectionString);
      }

      options.AddInterceptors(eventDispatchInterceptor);
    });

    services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>))
           .AddScoped(typeof(IReadRepository<>), typeof(EfRepository<>));

    logger.LogInformation("{Project} services registered", "Infrastructure");

    return services;
  }
}
