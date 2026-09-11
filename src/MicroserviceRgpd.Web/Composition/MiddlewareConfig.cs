using System.Text.Json.Serialization;
using Ardalis.ListStartupServices;
using MicroserviceRgpd.Infrastructure.Data;
using Scalar.AspNetCore;

namespace MicroserviceRgpd.Web.Composition;

public static class MiddlewareConfig
{
  public static async Task<IApplicationBuilder> UseAppMiddlewareAndSeedDatabase(this WebApplication app)
  {
    // Une exception non gérée sort en problem+json, dans tous les environnements. Ni la page
    // d'exception de développement (HTML) ni UseDefaultExceptionHandler de FastEndpoints
    // (format maison ErrorResponse) ne conviennent : chacune ferait une seconde forme d'erreur.
    app.UseExceptionHandler();

    // Les codes que la plateforme rend sans jamais atteindre l'application — route inconnue,
    // méthode non supportée, type de contenu refusé — sortent dans la même forme plutôt
    // qu'avec un corps vide.
    app.UseStatusCodePages();

    if (app.Environment.IsDevelopment())
    {
      app.UseShowAllServicesMiddleware(); // see https://github.com/ardalis/AspNetCoreStartupServices
    }
    else
    {
      app.UseHsts();
    }

    // Sans ce réglage explicite, les échecs de validation sortiraient dans le format maison
    // ErrorResponse de FastEndpoints et deux formes d'erreur coexisteraient dans la même API.
    app.UseFastEndpoints(c =>
    {
      c.Errors.UseProblemDetails();

      // Les noms de champs sont en camelCase — c'est le défaut de FastEndpoints —, les valeurs de
      // taxonomie en PascalCase. Une énumération sortie en entier ferait de son ordre de
      // déclaration un élément du contrat public, et le premier réordonnancement mentirait.
      c.Serializer.Options.Converters.Add(new JsonStringEnumConverter());

      // Un champ facultatif absent, jamais présent et nul : le contrat distingue « rendu » de
      // « non fourni », et un `null` explicite forcerait l'appelant à traiter trois états.
      c.Serializer.Options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

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

    // La feuille de style et la police des écrans, servies par le service lui-même. C'est le seul
    // canal par lequel elles arrivent : aucune ressource tierce n'est chargée par la surface —
    // un service qui outille le RGPD ne peut pas faire fuiter l'adresse IP de ses utilisateurs vers
    // un hébergeur de polices pour afficher une page.
    app.UseStaticFiles();

    // Les écrans de l'Operator. Ils vivent à côté de l'API sans la traverser : celle-ci qualifie un
    // texte, elle n'instruit jamais rien — le seul chemin vers un geste humain passe par un écran
    // que le service écrit lui-même.
    app.MapRazorPages();

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
