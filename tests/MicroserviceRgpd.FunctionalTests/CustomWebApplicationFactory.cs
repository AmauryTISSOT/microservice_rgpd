using MicroserviceRgpd.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace MicroserviceRgpd.FunctionalTests;

public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram>, IAsyncLifetime where TProgram : class
{
  private PostgreSqlContainer? _dbContainer;

  /// <summary>
  /// False quand Docker est indisponible : les tests tournent alors sur le repli SQLite,
  /// qui ne couvre pas les specificites PostgreSQL.
  /// </summary>
  public bool UsesPostgres => _dbContainer is not null;

  public async Task InitializeAsync()
  {
    try
    {
      _dbContainer = new PostgreSqlBuilder("postgres:18-alpine").Build();
      await _dbContainer.StartAsync();
    }
    catch (Exception)
    {
      // Docker is not available; fall back to SQLite (configured via appsettings.Testing.json)
      _dbContainer = null;
    }
  }

  public new async Task DisposeAsync()
  {
    if (_dbContainer != null)
    {
      await _dbContainer.DisposeAsync();
    }
  }

  /// <summary>
  /// Overriding CreateHost to avoid creating a separate ServiceProvider per this thread:
  /// https://github.com/dotnet-architecture/eShopOnWeb/issues/465
  /// </summary>
  /// <param name="builder"></param>
  /// <returns></returns>
  protected override IHost CreateHost(IHostBuilder builder)
  {
    builder.UseEnvironment("Testing"); // will not send real emails
    var host = builder.Build();
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
        if (_dbContainer != null)
        {
          // PostgreSQL via Testcontainers: apply migrations to create the schema
          db.Database.Migrate();
        }
        else
        {
          // SQLite fallback: EnsureCreated is used because the migrations use PostgreSQL syntax
          db.Database.EnsureCreated();
        }
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "An error occurred creating the " +
                            "test database. Error: {exceptionMessage}", ex.Message);
      }
    }

    return host;
  }

  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
    builder
        .ConfigureAppConfiguration((context, config) =>
        {
          if (_dbContainer != null)
          {
            // Set the connection string to use the Testcontainer
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
              ["ConnectionStrings:DefaultConnection"] = _dbContainer.GetConnectionString()
            });
          }
        })
        .ConfigureServices(services =>
        {
          if (_dbContainer != null)
          {
            // Remove the app's ApplicationDbContext registration
            var descriptors = services.Where(
              d => d.ServiceType == typeof(AppDbContext) ||
                   d.ServiceType == typeof(DbContextOptions<AppDbContext>))
                  .ToList();

            foreach (var descriptor in descriptors)
            {
              services.Remove(descriptor);
            }

            // Add ApplicationDbContext using the Testcontainers PostgreSQL instance
            services.AddDbContext<AppDbContext>((provider, options) =>
            {
              options.UseNpgsql(_dbContainer.GetConnectionString());
              var interceptor = provider.GetRequiredService<EventDispatchInterceptor>();
              options.AddInterceptors(interceptor);
            });
          }
        });
  }
}
