using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.Infrastructure.Screenings;

/// <summary>
/// Branche le moteur de détection. Il n'y a rien à configurer, et c'est le rendu d'ADR-0004 : pas
/// d'adresse, pas d'échéance, pas de drapeau — le moteur démarre avec le service.
/// </summary>
public static class ScreeningEngineServiceExtensions
{
  /// <summary>
  /// Enregistre le moteur <b>par son port</b> : ce qui le consomme ne doit pouvoir apprendre ni
  /// qu'il est fait de règles, ni qu'il vit dans cet assemblage.
  /// </summary>
  /// <remarks>
  /// <b>Un seul exemplaire pour tout le service.</b> Les lexiques gelés sont chargés une fois et ne
  /// changent plus : le moteur n'a aucun état, aucune horloge et aucun amont, et un exemplaire par
  /// requête relirait cinq cents entrées pour rendre exactement la même chose.
  /// </remarks>
  public static IServiceCollection AddScreeningEngine(this IServiceCollection services)
  {
    services.AddSingleton<IScreeningEngine, RulesAndLexiconScreeningEngine>();

    return services;
  }
}
