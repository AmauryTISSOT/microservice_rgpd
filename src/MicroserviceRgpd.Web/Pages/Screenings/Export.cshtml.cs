using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.UseCases.Screenings.ReadCurrentScreening;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MicroserviceRgpd.Web.Pages.Screenings;

/// <summary>
/// Le <b>troisième temps</b> du parcours : exporter la <c>Cartographie</c> du rapport de détection
/// courant, avec ce qu'il en reste à dire.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>L'écran n'exporte rien lui-même</b> : il mène aux deux adresses de la cartographie, qui se
/// collent dans un courriel et s'ouvrent d'un clic. Ce qu'il ajoute, c'est ce qu'il faut savoir
/// <b>avant</b> de les ouvrir — combien de lignes partent encore en attente, et que la clause ne
/// voyage pas dans le fichier.
/// </para>
/// <para>
/// ⚠️ <b>Il reste atteignable avant la fin de l'arbitrage.</b> Le fichier porte aussi les lignes en
/// attente : c'est un fait qu'on dit ici, pas une porte qu'on verrouille.
/// </para>
/// <para>
/// <b>Sans rapport courant, il renvoie au rapport</b>, qui offre les deux voies pour en produire un :
/// un écran d'export vide aurait promis un fichier que rien n'alimente.
/// </para>
/// </remarks>
public class ExportModel(IMediator mediator) : PageModel
{
  /// <summary>Le sommaire du rapport de détection courant, et la clause qui l'accompagne.</summary>
  public ScreeningAnswer<ScreeningSummary>? Answer { get; private set; }

  public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
  {
    Answer = await mediator.Send(new ReadCurrentScreeningQuery(), cancellationToken);

    return Answer is null ? RedirectToPage("Report") : Page();
  }
}
