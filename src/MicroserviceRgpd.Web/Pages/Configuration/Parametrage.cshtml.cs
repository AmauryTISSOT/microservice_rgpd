using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.UseCases.Configuration.ClearRightChannel;
using MicroserviceRgpd.UseCases.Configuration.ReadSettings;
using MicroserviceRgpd.UseCases.Configuration.SetRightEndpoint;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MicroserviceRgpd.Web.Pages.Configuration;

/// <summary>
/// L'écran du <c>Settings</c>, « Paramétrage du microservice RGPD » : on y relit les six droits RGPD
/// et l'adresse à laquelle le service exercera chacun — ou leur état « non configuré » —, et on y
/// pose, droit par droit, cette adresse.
/// </summary>
/// <remarks>
/// <para>
/// <b>Une mini-form par droit, indépendantes.</b> Chacune n'envoie que son droit : enregistrer ou
/// effacer l'adresse de l'un ne touche jamais celle d'un autre. Un droit configuré offre en plus
/// <b>Effacer</b>, qui le ramène à « non configuré ».
/// </para>
/// <para>
/// <b>Aucun nombre agrégé, aucun taux, aucun ratio.</b> Ni « 4 droits configurés sur 6 », ni
/// « couverture : 66 % » : un tel dénominateur présenterait la configuration comme complète. L'écran
/// <b>énumère</b>, il ne compte pas. Les seuls chiffres qu'il porte sont les <b>articles</b> du RGPD
/// — un chiffre qui n'est ni un compte ni une mesure, qui se lit et s'ignore.
/// </para>
/// </remarks>
public class ParametrageModel(IMediator mediator) : PageModel
{
  /// <summary>Le préfixe de liaison des mini-forms, cité tel quel lorsqu'un champ est refusé.</summary>
  public const string FormPrefix = nameof(Form);

  /// <summary>Ce que l'intégrateur a saisi dans la mini-form d'un droit.</summary>
  [BindProperty]
  public RightEndpointForm Form { get; set; } = new();

  /// <summary>Le Paramétrage tel qu'il se lit à cet instant — six droits, configurés ou non.</summary>
  public Core.Configuration.Settings Settings { get; private set; } = Core.Configuration.Settings.Unconfigured();

  /// <summary>
  /// Le droit dont la mini-form vient d'être refusée — c'est dans sa section que le refus se dit, et
  /// que la saisie refusée reste à corriger. <c>null</c> hors d'un refus, ou si le droit envoyé
  /// n'est pas l'un des six : le refus se dit alors en tête d'écran.
  /// </summary>
  public DataSubjectRight? Refused { get; private set; }

  public async Task OnGetAsync(CancellationToken cancellationToken)
  {
    Settings = await mediator.Send(new ReadSettingsQuery(), cancellationToken);
  }

  public async Task<IActionResult> OnPostSetAsync(CancellationToken cancellationToken)
  {
    var fields = Form.Read(ModelState, FormPrefix);

    if (fields is { } saving)
    {
      return await WrittenAsync(
        await mediator.Send(new SetRightEndpointCommand(saving.Right, saving.Endpoint), cancellationToken),
        cancellationToken);
    }

    return await RefusedAsync(cancellationToken);
  }

  public async Task<IActionResult> OnPostClearAsync(CancellationToken cancellationToken)
  {
    // L'effacement n'envoie que le droit : il n'y a pas d'adresse à juger, et celle qui traînerait
    // dans le champ voisin n'est pas lue.
    if (Form.ReadRight(ModelState, FormPrefix) is { } right)
    {
      return await WrittenAsync(
        await mediator.Send(new ClearRightChannelCommand(right), cancellationToken),
        cancellationToken);
    }

    return await RefusedAsync(cancellationToken);
  }

  /// <summary>
  /// L'issue d'une écriture — enregistrement ou effacement : une redirection si elle a réussi, sinon
  /// ses refus rendus dans la section du droit envoyé.
  /// </summary>
  private async Task<IActionResult> WrittenAsync(Result written, CancellationToken cancellationToken)
  {
    if (written.IsSuccess)
    {
      // Une redirection après l'écriture : recharger la page ne renvoie rien, et l'écran relit
      // l'état tel qu'il a été enregistré plutôt que tel qu'il a été saisi.
      return RedirectToPage();
    }

    foreach (var refusal in written.ValidationErrors)
    {
      ModelState.AddModelError($"{FormPrefix}.{refusal.Identifier}", refusal.ErrorMessage);
    }

    return await RefusedAsync(cancellationToken);
  }

  /// <summary>
  /// Rend l'écran sur le refus d'un envoi, dit dans la section du droit envoyé. Le refus se rend sur
  /// la page même, sans redirection : une redirection l'aurait perdu en chemin, et l'intégrateur
  /// n'aurait jamais su pourquoi son envoi n'avait pas été retenu.
  /// </summary>
  private async Task<IActionResult> RefusedAsync(CancellationToken cancellationToken)
  {
    Refused = Form.Designated;

    await OnGetAsync(cancellationToken);

    return Page();
  }
}
