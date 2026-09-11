using MicroserviceRgpd.Core.SharedKernel;
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
/// <b>Une mini-form par droit, indépendantes.</b> Chacune n'envoie que son droit : enregistrer
/// l'adresse de l'un ne touche jamais celle d'un autre. L'effacement vient dans un ticket suivant.
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
      var set = await mediator.Send(new SetRightEndpointCommand(saving.Right, saving.Endpoint), cancellationToken);

      if (set.IsSuccess)
      {
        // Une redirection après l'écriture : recharger la page ne renvoie rien, et l'écran relit
        // l'adresse telle qu'elle a été enregistrée plutôt que telle qu'elle a été saisie.
        return RedirectToPage();
      }

      foreach (var refusal in set.ValidationErrors)
      {
        ModelState.AddModelError($"{FormPrefix}.{refusal.Identifier}", refusal.ErrorMessage);
      }
    }

    // Le refus se rend sur la page même, sans redirection : une redirection l'aurait perdu en
    // chemin, et l'intégrateur n'aurait jamais su pourquoi son adresse n'avait pas été retenue.
    Refused = Form.Designated;

    await OnGetAsync(cancellationToken);

    return Page();
  }
}
