using System.Diagnostics;

namespace MicroserviceRgpd.Web.Composition;

/// <summary>
/// L'API n'a qu'une seule forme d'erreur : <c>application/problem+json</c> conforme RFC 9457.
/// Ce que la plateforme produit avant l'application — route inconnue, méthode non supportée,
/// type de contenu refusé, exception non gérée — passe par ici ; ce que FastEndpoints produit
/// passe par <c>Errors.UseProblemDetails()</c>, posé au moment d'installer le middleware.
/// </summary>
public static class ProblemDetailsConfigs
{
  public static IServiceCollection AddProblemDetailsConfigs(this IServiceCollection services)
    => services.AddProblemDetails(options =>
    {
      options.CustomizeProblemDetails = context =>
      {
        // En erreur, il n'y a aucune qualification à référencer dans l'audit : le seul
        // identifiant qui vaille est celui du diagnostic. FastEndpoints pose déjà le sien
        // sur ses propres réponses ; on aligne celles de la plateforme.
        context.ProblemDetails.Extensions["traceId"] =
          Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
      };
    });
}
