using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.UseCases.Screenings.ReadCurrentScreening;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MicroserviceRgpd.Web.Pages.Screenings;

/// <summary>
/// Le <b>rapport sommaire</b> du dépistage courant : son entête, ses tables retriées, ses comptes,
/// son verrou et sa <c>Clause d'incomplétude</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>« Courant » est un calcul refait à chaque affichage</b>, jamais un état lu quelque part. Le
/// rapport le plus récemment lancé est le courant ; les autres sont archivés par le seul fait qu'il
/// existe, et rien n'a été écrit pour cela.
/// </para>
/// <para>
/// ⚠️ <b>Quand aucun dépistage n'a été lancé, on rend l'écran de dépôt seul.</b> Un rapport vide
/// portant « ce dépistage n'a pas regardé le CRM en SaaS, les tableurs partagés, les journaux… »
/// serait un <b>aveu sans acte</b>, et userait la clause avant son premier usage réel.
/// </para>
/// <para>
/// ⚠️ <b>Le verrou et les comptes ne sont mémorisés nulle part.</b> Ils se recalculent ici, à
/// l'instant où l'<c>Operator</c> regarde : un compte persisté aurait eu besoin de quelque chose
/// pour le mettre à jour, et ce quelque chose serait le processus de fond que ce dépôt interdit.
/// </para>
/// </remarks>
public class ReportModel(IMediator mediator) : PageModel
{
  /// <summary>Le sommaire du rapport courant, et la clause qui l'accompagne obligatoirement.</summary>
  public ScreeningAnswer<ScreeningSummary>? Answer { get; private set; }

  public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
  {
    Answer = await mediator.Send(new ReadCurrentScreeningQuery(), cancellationToken);

    // Aucun dépistage n'a encore été lancé chez ce client : l'écran de dépôt, seul, et sans clause
    // — il n'y a rien dont on puisse déclarer l'incomplétude.
    return Answer is null ? RedirectToPage("Deposit") : Page();
  }
}
