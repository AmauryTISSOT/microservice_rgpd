using MicroserviceRgpd.UseCases.Configuration.ReadSettings;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MicroserviceRgpd.Web.Pages.Configuration;

/// <summary>
/// L'écran du <c>Settings</c>, « Paramétrage du microservice RGPD » : on y relit les six droits RGPD
/// et l'adresse à laquelle le service exercera chacun — ou leur état « non configuré ».
/// </summary>
/// <remarks>
/// <para>
/// <b>En lecture seule ici.</b> La saisie et l'effacement des adresses viennent dans les tickets
/// suivants ; cet écran est la colonne vertébrale : les six droits d'emblée, chacun avec son libellé
/// français et son article.
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
  /// <summary>Le Paramétrage tel qu'il se lit à cet instant — six droits, configurés ou non.</summary>
  public Core.Configuration.Settings Settings { get; private set; } = Core.Configuration.Settings.Unconfigured();

  public async Task OnGetAsync(CancellationToken cancellationToken)
  {
    Settings = await mediator.Send(new ReadSettingsQuery(), cancellationToken);
  }
}
