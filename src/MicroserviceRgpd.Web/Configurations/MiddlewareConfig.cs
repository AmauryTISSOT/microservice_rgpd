using Ardalis.ListStartupServices;
using MicroserviceRgpd.Infrastructure.Data;
using Scalar.AspNetCore;

namespace MicroserviceRgpd.Web.Configurations;

public static class MiddlewareConfig
{
  public static async Task<IApplicationBuilder> UseAppMiddlewareAndSeedDatabase(this WebApplication app)
  {
    if (app.Environment.IsDevelopment())
    {
      app.UseDeveloperExceptionPage();
      app.UseShowAllServicesMiddleware(); // see https://github.com/ardalis/AspNetCoreStartupServices
    }
    else
    {   
      app.UseDefaultExceptionHandler(); // from FastEndpoints
      app.UseHsts();
    }

    app.UseFastEndpoints();

    if (app.Environment.IsDevelopment())
    {
      app.UseSwaggerGen(options =>
      {
        options.Path = "/openapi/{documentName}.json";
      },
      settings =>
      {
        settings.Path = "/swagger";
        settings.DocumentPath = "/openapi/{documentName}.json";
      });
  
      app.MapScalarApiReference(options =>
      {
        options.WithTitle("Clean Architecture API");
        options.WithOpenApiRoutePattern("/openapi/{documentName}.json");
      });
    }

    app.UseHttpsRedirection(); // Note this will drop Authorization headers

    // Run migrations in Development or when explicitly requested via environment variable
    var shouldMigrate = app.Environment.IsDevelopment() ||
                        app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup");

    if (shouldMigrate)
    {
      await MigrateDatabaseAsync(app);
    }

    return app;
  }

  static async Task MigrateDatabaseAsync(WebApplication app)
  {
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
      logger.LogInformation("Applying database migrations...");
      var context = services.GetRequiredService<AppDbContext>();
      
      await context.Database.MigrateAsync();
      logger.LogInformation("Database migrations applied successfully");
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "An error occurred migrating the DB. {exceptionMessage}", ex.Message);
      throw; // Re-throw to make startup fail if migrations fail
    }
  }
}
