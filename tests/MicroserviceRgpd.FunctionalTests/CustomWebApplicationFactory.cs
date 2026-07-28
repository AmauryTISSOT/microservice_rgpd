using MicroserviceRgpd.Core.Qualifications;
using MicroserviceRgpd.Infrastructure.Data;
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

  /// <summary>
  /// Le moteur de qualification, substitue. La doublure se pose <b>sur le port du domaine</b>, et
  /// non sur le fil HTTP : aucun test .NET n appelle le sidecar reel, et aucun n approche un GPU.
  /// Un test qui exigerait un GPU ne tournerait jamais, et un test qui ne tourne jamais ment.
  /// </summary>
  public WitnessDouble Witness { get; } = new();

  public async Task InitializeAsync()
  {
    await _dbContainer.StartAsync();

    // Le ConfigurationManager de Program est construit avant tout ConfigureAppConfiguration :
    // la variable d environnement est le seul moyen de fournir la chaine assez tot.
    Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _dbContainer.GetConnectionString());
  }

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
  /// La seule chose substituee est le moteur. Tout le reste est l application telle quelle : elle
  /// resout elle-meme sa chaine de connexion depuis <c>ConnectionStrings:DefaultConnection</c>,
  /// exactement comme hors tests.
  /// </summary>
  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
    builder.ConfigureTestServices(services =>
    {
      services.RemoveAll<IQualificationEngine>();
      services.AddSingleton<IQualificationEngine>(Witness);
    });
  }
}
