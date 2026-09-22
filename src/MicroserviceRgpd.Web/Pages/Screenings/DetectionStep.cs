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
/// Ce que le <b>fil d'Ariane de la détection</b> rend : l'étape de l'écran, le rapport s'il y en a
/// un, la table ouverte, et le nom du geste de relevé quand l'écran en est un.
/// </summary>
/// <param name="Step">Le temps du parcours où se trouve l'écran.</param>
/// <param name="Summary">
/// Le sommaire du rapport courant, ou <c>null</c> sur un écran de relevé : le rapport n'existe pas
/// encore, et le fil ne nomme ni base ni compte.
/// </param>
/// <param name="Open">La table ouverte, ou <c>null</c> sur le rapport et l'export.</param>
/// <param name="Listing">Le geste de relevé qu'on est en train de faire — déposer, ou connecter.</param>
public sealed record DetectionTrail(
  DetectionStep Step,
  ScreeningSummary? Summary = null,
  TableIdentity? Open = null,
  string? Listing = null);

/// <summary>
/// Ce que la <b>liste des tables</b> rend : le sommaire du rapport, et la table ouverte s'il y en a
/// une.
/// </summary>
/// <param name="Summary">Le sommaire du rapport de détection courant.</param>
/// <param name="Open">La table ouverte à droite, ou <c>null</c> sur le rapport lui-même.</param>
public sealed record TablesList(ScreeningSummary Summary, TableIdentity? Open);
