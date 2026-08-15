using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.UseCases.Screenings.ReadArchivedScreening;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MicroserviceRgpd.Web.Pages.Screenings;

/// <summary>
/// Le <b>sommaire d'un rapport de détection archivé</b> : ce qu'un moteur avait vu à une date
/// donnée, et ce qu'un humain en avait dit — <b>en lecture, et rien d'autre</b>.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Cet écran ne porte aucun geste d'arbitrage, et il n'en portera jamais</b> : ce qu'il rend
/// est un type qui n'en a pas. « Non arbitrable » n'est pas une condition posée dans un rendu —
/// c'est ce que le geste de lecture rapporte, et le geste d'écriture, lui, ne sait charger que le
/// courant.
/// </para>
/// <para>
/// ⚠️ <b>Il ne porte pas non plus le verrou d'inachèvement.</b> « Ce rapport de détection est
/// inachevé — relisez les colonnes où rien n'a été vu » appellerait ici un geste qui n'existe plus.
/// </para>
/// <para>
/// <b>Le rapport de détection nommé peut avoir cessé d'être archivé, ou d'exister</b> — le courant
/// qui l'archivait a pu être supprimé, ou lui-même l'a pu. Les deux ramènent à l'historique, qui dit ce
/// que le déploiement a réellement.
/// </para>
/// </remarks>
public class ArchiveModel(IMediator mediator) : PageModel
{
  /// <summary>
  /// Le rapport de détection archivé qu'on ouvre. ⚠️ <b>Il passe en paramètre de requête</b>, comme
  /// le schéma et la table de l'écran d'arbitrage : la surface de ce contexte n'a aucun paramètre de route.
  /// </summary>
  [BindProperty(SupportsGet = true)]
  public string? Screening { get; set; }

  /// <summary>Le sommaire de l'archivé, et la clause qui l'accompagne obligatoirement.</summary>
  public ScreeningAnswer<ArchivedScreeningReport>? Answer { get; private set; }

  public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
  {
    if (!Guid.TryParse(Screening, out var named) || named == Guid.Empty)
    {
      // Une adresse qui ne nomme aucun rapport de détection ne rend pas un écran vide :
      // l'historique les nomme tous.
      return RedirectToPage("History");
    }

    Answer = await mediator.Send(
      new ReadArchivedScreeningQuery(ScreeningId.From(named)), cancellationToken);

    // Supprimé, ou redevenu le courant. Dans les deux cas l'écran affiché est périmé, et
    // l'historique dit ce qui est vrai — y compris que ce rapport de détection-ci s'arbitre de
    // nouveau.
    return Answer is null ? RedirectToPage("History") : Page();
  }
}
