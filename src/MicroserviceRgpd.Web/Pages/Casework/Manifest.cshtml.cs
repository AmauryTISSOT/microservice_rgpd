using MicroserviceRgpd.UseCases.Casework.DeclareSystem;
using MicroserviceRgpd.UseCases.Casework.ReadManifest;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MicroserviceRgpd.Web.Pages.Casework;

/// <summary>
/// L'écran du <c>Manifest</c> : on y relit le paysage déclaré, et on y déclare un système de plus.
/// </summary>
/// <remarks>
/// <para>
/// <b>Aucun nombre agrégé, aucun taux, aucun ratio.</b> Ni « 6 systèmes déclarés », ni
/// « 2 couverts sur 6 », ni « couverture : 33 % » : un dénominateur qui décrit le paysage du client
/// est une déclaration faussable en silence, et lui donner l'autorité d'un chiffre serait présenter
/// un recensement comme complet. L'écran <b>énumère</b>, il ne compte pas.
/// </para>
/// <para>
/// <b>Une seule vue, sans pagination ni filtre.</b> Un catalogue est un paysage de quelques
/// systèmes, et une page suivante qu'on n'ouvre pas est exactement la forme d'<c>Omission
/// silencieuse</c> qu'un écran peut fabriquer.
/// </para>
/// </remarks>
public class ManifestModel(IMediator mediator) : PageModel
{
  /// <summary>Le préfixe de liaison du formulaire, cité tel quel lorsqu'un champ est refusé.</summary>
  public const string FormPrefix = nameof(Form);

  /// <summary>Ce que l'humain saisit pour déclarer un système de plus.</summary>
  [BindProperty]
  public DeclaredSystemForm Form { get; set; } = new();

  /// <summary>Le catalogue tel qu'il se lit à cet instant.</summary>
  public Core.Casework.Manifest Manifest { get; private set; } = Core.Casework.Manifest.Empty;

  public async Task OnGetAsync(CancellationToken cancellationToken)
  {
    Manifest = await mediator.Send(new ReadManifestQuery(), cancellationToken);
  }

  public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
  {
    var fields = Form.Read(ModelState, FormPrefix);

    if (fields is not null)
    {
      var declared = await mediator.Send(
        new DeclareSystemCommand(fields.Id, fields.Label, fields.Contents, fields.Capabilities, fields.AdapterAddress),
        cancellationToken);

      if (declared.IsSuccess)
      {
        // Une redirection après l'écriture : recharger la page ne redéclare rien, et le formulaire
        // repart vide plutôt que de garder la saisie de la déclaration précédente.
        return RedirectToPage();
      }

      FormBoundary.Deposit(ModelState, FormPrefix, declared.ValidationErrors);
    }

    await OnGetAsync(cancellationToken);

    return Page();
  }
}
