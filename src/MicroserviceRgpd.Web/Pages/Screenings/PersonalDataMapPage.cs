using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.UseCases.Screenings.ExportPersonalDataMap;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MicroserviceRgpd.Web.Pages.Screenings;

/// <summary>
/// Ce qui est commun aux deux téléchargements de la <c>Cartographie</c> : lire le rapport courant,
/// le rendre en fichier, ou renvoyer là où il n'y en a pas.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Ce sont deux <c>GET</c> nus servis par des Razor Pages, jamais un point d'API.</b> Un lien
/// collé dans un courriel annonce ce qu'il rend, s'ouvre d'un clic et se met en favori ; un point
/// d'API aurait fait de ce contexte le premier à en exposer un — <c>NoApiScreensADatabase</c> le
/// refuse, et le refuse pour la raison qui a écarté le pont vers le <c>Manifest</c> : un point
/// d'API a des appelants qu'on ne voit pas.
/// </para>
/// <para>
/// <b>Deux routes plutôt qu'une route et un paramètre de format.</b> Une seule adresse rendant
/// tantôt du JSON tantôt du CSV aurait obligé l'écran à porter un menu — donc du script — là où deux
/// liens suffisent, et aurait rendu indéchiffrable une adresse recopiée à la main.
/// </para>
/// <para>
/// ⚠️ <b>Le rendu ne décide rien.</b> Il reçoit une <see cref="PersonalDataMap"/> déjà calculée et
/// des octets déjà écrits : il ne sait ni ce qu'un BOM est, ni comment un CSV se cite.
/// </para>
/// </remarks>
/// <param name="mediator">Par où la cartographie se demande.</param>
/// <param name="export">Ce qui la rend en fichier.</param>
public abstract class PersonalDataMapPage(IMediator mediator, IScreeningExport export) : PageModel
{
  /// <summary>Le rendu que cette page-ci sert.</summary>
  protected abstract Func<IScreeningExport, PersonalDataMap, ExportedFile> Render { get; }

  /// <summary>
  /// Le fichier, ou le renvoi vers l'écran de dépôt quand aucune détection n'a été lancée.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Sans rapport, on ne rend pas un fichier vide.</b> Un CSV n'ayant qu'une ligne d'en-tête
  /// se lit comme « ce client n'a aucune donnée personnelle », c'est-à-dire la seule affirmation
  /// qu'aucun artefact d'ici ne peut porter.
  /// </remarks>
  public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
  {
    var map = await mediator.Send(new ExportPersonalDataMapQuery(), cancellationToken);

    if (map is null)
    {
      return RedirectToPage("Deposit");
    }

    var file = Render(export, map);

    // Le nom passe ici, et c'est lui qui remplit le Content-Disposition : côté CSV, il est tout ce
    // que le fichier dit de sa provenance.
    return File(file.Content, file.ContentType, file.Name);
  }
}
