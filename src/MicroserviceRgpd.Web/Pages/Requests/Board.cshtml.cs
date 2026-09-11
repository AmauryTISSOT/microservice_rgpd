using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.Core.SharedKernel;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MicroserviceRgpd.Web.Pages.Requests;

/// <summary>
/// Le <b>tableau des demandes RGPD</b> : le nom de l'écran, le bouton « Créer une demande », et la
/// modale de création que le serveur rend fermée, avec son formulaire à ses valeurs par défaut.
/// </summary>
/// <remarks>
/// ⚠️ <b>« Aujourd'hui » se lit sur l'horloge du service, à Paris</b> — voir <see cref="ParisCalendar"/>.
/// C'est la valeur par défaut et la borne haute du champ de date au chargement ; le module les
/// recalcule à chaque ouverture, pour une page restée ouverte au-delà de minuit.
/// </remarks>
public class BoardModel(TimeProvider clock) : PageModel
{
  /// <summary>
  /// Les droits qu'une personne peut invoquer, dans l'ordre des articles. ⚠️ Jamais
  /// <see cref="DataSubjectRight.OutOfScope"/> : c'est un verdict de qualification, pas un droit.
  /// </summary>
  public static IReadOnlyList<DataSubjectRight> InvocableRights { get; } =
  [
    .. DataSubjectRight.List
      .Where(right => right != DataSubjectRight.OutOfScope)
      .OrderBy(right => right.Value),
  ];

  /// <summary>Les canaux d'arrivée, l'email d'abord : c'est l'origine par défaut.</summary>
  public static IReadOnlyList<Origin> Origins { get; } = [.. Origin.List.OrderBy(origin => origin.Value)];

  /// <summary>Aujourd'hui à Paris, à l'instant où la page est rendue.</summary>
  public DateOnly Today { get; private set; }

  public void OnGet()
  {
    Today = ParisCalendar.Today(clock);
  }
}
