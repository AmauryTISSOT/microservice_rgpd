using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.UseCases.Configuration.ClearRightChannel;
using MicroserviceRgpd.UseCases.Configuration.ReadSettings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;

namespace MicroserviceRgpd.Web.Pages.Configuration;

/// <summary>
/// Ce que les <b>deux faces du Paramétrage</b> font de la même façon : relire les six droits, poser
/// le refus d'un envoi dans le détail du droit envoyé, et <b>effacer</b> le canal d'un droit.
/// Chaque face n'écrit ensuite que <b>l'espèce de canal qu'elle configure</b>.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>L'effacement est ici, et une seule fois</b> : effacer un routage et effacer une adresse sont
/// le <b>même geste</b>, servi par une seule commande (ADR-0027, écart n° 1). Recopié sur chaque
/// face, il aurait divergé — et les deux faces n'auraient plus ramené au même « non configuré ».
/// </para>
/// <para>
/// <b>Le Post-Redirect-Get et le rendu d'un refus</b> sont de la même nature : ce qui décide n'est
/// pas l'espèce du canal, mais qu'une écriture ait réussi ou non. Ils vivent donc ici, et une face
/// ne les réécrit pas.
/// </para>
/// <para>
/// <b>Aucun nombre agrégé, aucun taux, aucun ratio</b>, sur aucune des deux faces : un dénominateur
/// présenterait la configuration comme complète. L'écran <b>énumère</b>, il ne compte pas.
/// </para>
/// </remarks>
/// <typeparam name="TForm">Ce que la mini-form d'un droit porte sur cette face.</typeparam>
/// <param name="mediator">Le médiateur par lequel la face lit et écrit le Paramétrage.</param>
public abstract class ParametrageFaceModel<TForm>(IMediator mediator) : PageModel
  where TForm : RightForm, new()
{
  /// <summary>Le préfixe de liaison des mini-forms, cité tel quel lorsqu'un champ est refusé.</summary>
  public const string FormPrefix = nameof(Form);

  /// <summary>Ce que l'intégrateur a saisi dans la mini-form d'un droit.</summary>
  [BindProperty]
  public TForm Form { get; set; } = new();

  /// <summary>Le Paramétrage tel qu'il se lit à cet instant — six droits, configurés ou non.</summary>
  public Core.Configuration.Settings Settings { get; private set; } = Core.Configuration.Settings.Unconfigured();

  /// <summary>
  /// Le droit dont la mini-form vient d'être refusée — c'est son détail que l'écran rouvre, où le refus se dit, et
  /// que la saisie refusée reste à corriger. <c>null</c> hors d'un refus, ou si le droit envoyé
  /// n'est pas l'un des six : le refus se dit alors en tête d'écran.
  /// </summary>
  public DataSubjectRight? Refused { get; private set; }

  /// <summary>
  /// Le droit que l'adresse demande d'ouvrir, par son nom canonique — <c>?droit=Erasure</c>. Lu tel
  /// quel : un nom que l'écran ignore n'est pas une faute, il ouvre simplement le premier droit.
  /// </summary>
  [BindProperty(SupportsGet = true, Name = ParametrageFaces.Choice)]
  public string? Choice { get; set; }

  /// <summary>
  /// <b>Le droit dont le détail est ouvert.</b> Celui dont l'envoi vient d'être refusé d'abord — c'est
  /// là que le refus se lit et que la saisie reste à corriger —, puis celui que l'adresse demande, et
  /// à défaut le premier des six.
  /// </summary>
  public DataSubjectRight Selected =>
    Refused
    ?? Core.Configuration.Settings.ConfigurableRights.FirstOrDefault(right => right.Name == Choice)
    ?? Core.Configuration.Settings.ConfigurableRights[0];

  /// <summary>L'adresse de cette face — celle où ses onglets, sa liste et ses redirections ramènent.</summary>
  public abstract string Face { get; }

  /// <summary>Ce que la liste, l'en-tête du droit et les onglets rendent, sur cette face.</summary>
  public ParametrageScene Scene => new(Settings, Selected, Face);

  /// <summary>Le médiateur, dont la face se sert pour écrire le canal qu'elle configure.</summary>
  protected IMediator Mediator => mediator;

  public async Task OnGetAsync(CancellationToken cancellationToken)
  {
    Settings = await mediator.Send(new ReadSettingsQuery(), cancellationToken);
  }

  public async Task<IActionResult> OnPostClearAsync(CancellationToken cancellationToken)
  {
    // L'effacement n'envoie que le droit : il n'y a pas de canal à juger, et celui qui traînerait
    // dans les champs voisins n'est pas lu.
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
  /// ses refus rendus dans le détail du droit envoyé.
  /// </summary>
  protected async Task<IActionResult> WrittenAsync(Result written, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(written);

    if (written.IsSuccess)
    {
      // Une redirection après l'écriture : recharger la page ne renvoie rien, et l'écran relit
      // l'état tel qu'il a été enregistré plutôt que tel qu'il a été saisi. Elle rouvre le droit
      // qu'on vient d'écrire : c'est lui qu'on veut relire, pas le premier de la liste.
      return RedirectToPage(new RouteValueDictionary { [ParametrageFaces.Choice] = Form.Designated?.Name });
    }

    foreach (var refusal in written.ValidationErrors)
    {
      ModelState.AddModelError($"{FormPrefix}.{refusal.Identifier}", refusal.ErrorMessage);
    }

    return await RefusedAsync(cancellationToken);
  }

  /// <summary>
  /// Rend l'écran sur le refus d'un envoi, dit dans le détail du droit envoyé. Le refus se rend sur
  /// la page même, sans redirection : une redirection l'aurait perdu en chemin, et l'intégrateur
  /// n'aurait jamais su pourquoi son envoi n'avait pas été retenu.
  /// </summary>
  protected async Task<IActionResult> RefusedAsync(CancellationToken cancellationToken)
  {
    Refused = Form.Designated;

    await OnGetAsync(cancellationToken);

    return Page();
  }
}
