using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.UseCases.Casework.ReadManifest;
using MicroserviceRgpd.UseCases.Casework.ReviseSystem;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MicroserviceRgpd.Web.Pages.Casework;

/// <summary>
/// L'écran où l'on reprend ce qu'on avait déclaré d'un système.
/// </summary>
/// <remarks>
/// <b>L'identifiant n'y est pas modifiable</b> : c'est ce que l'<c>Adapter</c> connaît de ce
/// système, et le laisser réécrire ferait disparaître une ligne et en créer une autre sous couvert
/// de correction. Il s'affiche, il ne se saisit pas.
/// </remarks>
public class ManifestReviseModel(IMediator mediator) : PageModel
{
  /// <summary>Le préfixe de liaison du formulaire, cité tel quel lorsqu'un champ est refusé.</summary>
  public const string FormPrefix = nameof(Form);

  /// <summary>Ce que l'humain reprend de ce système.</summary>
  [BindProperty]
  public DeclaredSystemForm Form { get; set; } = new();

  /// <summary>L'identifiant du système repris, tel qu'il est arrivé par la route.</summary>
  [BindProperty(SupportsGet = true)]
  public string Id { get; set; } = string.Empty;

  /// <summary>Le jour où ce système a été déclaré pour la dernière fois — celui que la révision remplacera.</summary>
  public DateTimeOffset DeclaredOn { get; private set; }

  public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
  {
    var declared = await ReadAsync(cancellationToken);

    if (declared is null)
    {
      return NotFound();
    }

    Form = DeclaredSystemForm.Of(declared);
    DeclaredOn = declared.DeclaredOn;

    return Page();
  }

  public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
  {
    var declared = await ReadAsync(cancellationToken);

    if (declared is null)
    {
      return NotFound();
    }

    DeclaredOn = declared.DeclaredOn;

    // L'identifiant vient de la route, jamais du corps du formulaire : un champ caché rejouable
    // laisserait une révision atterrir sur un autre système que celui qu'on regardait.
    Form.Id = declared.Id.Value;

    var fields = Form.Read(ModelState, FormPrefix);

    if (fields is not null)
    {
      var revised = await mediator.Send(
        new ReviseSystemCommand(fields.Id, fields.Label, fields.Contents, fields.Capabilities, fields.AdapterAddress),
        cancellationToken);

      if (revised.IsSuccess)
      {
        return RedirectToPage("Manifest");
      }

      DeclaredSystemForm.Refuse(ModelState, FormPrefix, revised.ValidationErrors);
    }

    return Page();
  }

  /// <summary>
  /// Le système visé, ou rien. Un identifiant hors du jeu de caractères déclarable ne désigne aucun
  /// système : il se traite comme une adresse inconnue, jamais comme une erreur de saisie.
  /// </summary>
  private async Task<DeclaredSystem?> ReadAsync(CancellationToken cancellationToken)
  {
    if (!DeclaredSystemId.TryFrom(Id, out var id))
    {
      return null;
    }

    var declared = await mediator.Send(new ReadDeclaredSystemQuery(id), cancellationToken);

    return declared.IsSuccess ? declared.Value : null;
  }
}
