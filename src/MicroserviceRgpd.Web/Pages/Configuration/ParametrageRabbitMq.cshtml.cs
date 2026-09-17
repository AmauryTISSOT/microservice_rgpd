using MicroserviceRgpd.UseCases.Configuration.ReadSettings;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MicroserviceRgpd.Web.Pages.Configuration;

/// <summary>
/// La <b>seconde face du Paramétrage</b> — « Configuration RabbitMQ » : on y relit, droit par droit,
/// le routage par lequel le service exercera chacun des six droits RGPD, ou leur état « non
/// configuré ».
/// </summary>
/// <remarks>
/// <para>
/// <b>Une face, pas un écran de plus.</b> Elle porte le même titre que la face HTTP et se rejoint
/// par des onglets ; le panneau latéral ne bouge pas, et « Paramétrage » y reste marqué courant.
/// </para>
/// <para>
/// ⚠️ <b>Elle est en lecture seule à ce stade.</b> Poser et effacer un routage depuis cet écran
/// viennent au ticket des formulaires ; ce que cette page garantit aujourd'hui est que l'état se
/// lit.
/// </para>
/// </remarks>
public class ParametrageRabbitMqModel(IMediator mediator) : PageModel
{
  /// <summary>Le Paramétrage tel qu'il se lit à cet instant — six droits, configurés ou non.</summary>
  public Core.Configuration.Settings Settings { get; private set; } = Core.Configuration.Settings.Unconfigured();

  public async Task OnGetAsync(CancellationToken cancellationToken)
  {
    Settings = await mediator.Send(new ReadSettingsQuery(), cancellationToken);
  }
}
