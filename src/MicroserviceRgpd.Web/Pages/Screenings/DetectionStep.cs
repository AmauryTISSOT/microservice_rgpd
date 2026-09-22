using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.UseCases.Screenings.ReadCurrentScreening;

namespace MicroserviceRgpd.Web.Pages.Screenings;

/// <summary>
/// <b>Les trois temps du parcours de la détection</b>, tels que l'écran les montre : relever le
/// schéma, arbitrer ses colonnes, exporter la réponse.
/// </summary>
/// <remarks>
/// ⚠️ <b>Ce n'est pas un état du rapport</b>, et rien ne le mémorise : c'est l'écran qui dit où il se
/// trouve dans le parcours. L'avancement, lui, est un compte — voir <c>ArbitrationProgress</c>.
/// </remarks>
public enum DetectionStep
{
  /// <summary>Le relevé du schéma, collé ou scanné.</summary>
  Listing = 1,

  /// <summary>L'arbitrage, table par table.</summary>
  Arbitration = 2,

  /// <summary>L'export de la réponse.</summary>
  Export = 3,
}

/// <summary>
/// Ce que la <b>liste des tables</b> rend : le sommaire du rapport, et la table ouverte s'il y en a
/// une.
/// </summary>
/// <param name="Summary">Le sommaire du rapport de détection courant.</param>
/// <param name="Open">La table ouverte à droite, ou <c>null</c> sur le rapport lui-même.</param>
public sealed record TablesList(ScreeningSummary Summary, TableIdentity? Open);
