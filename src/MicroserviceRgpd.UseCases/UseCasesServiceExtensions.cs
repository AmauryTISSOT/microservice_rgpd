using MicroserviceRgpd.UseCases.Casework.CallAdapter;
using Microsoft.Extensions.DependencyInjection;

namespace MicroserviceRgpd.UseCases;

/// <summary>
/// Le point de montage de la couche : ce que les cas d'usage demandent à l'injection de dépendances
/// en plus des gestionnaires, que Mediator enregistre seul.
/// </summary>
/// <remarks>
/// ⚠️ <b>Il vit ici pour que la couche web ne nomme plus aucun type de <c>Casework</c></b>, dont
/// elle a perdu les écrans et la route. Les gestionnaires de <c>Casework</c> restent enregistrés
/// jusqu'à leur retrait, et l'hôte de développement, qui valide ses services au démarrage, refuserait
/// de démarrer sur une dépendance manquante.
/// </remarks>
public static class UseCasesServiceExtensions
{
  public static IServiceCollection AddUseCasesServices(this IServiceCollection services)
  {
    // L'appel d'un Adapter au titre d'un Case. Il vit ici plutôt qu'avec le client HTTP :
    // l'Infrastructure n'a pas à apprendre qu'un dossier existe. Scoped, comme l'EvidenceLog dont
    // il écrit la ligne.
    services.AddScoped<AdapterCallsForCase>();

    return services;
  }
}
