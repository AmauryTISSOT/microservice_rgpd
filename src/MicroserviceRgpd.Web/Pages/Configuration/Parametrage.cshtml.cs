using MicroserviceRgpd.UseCases.Configuration.SetRightEndpoint;
using Microsoft.AspNetCore.Mvc;

namespace MicroserviceRgpd.Web.Pages.Configuration;

/// <summary>
/// La <b>première face du Paramétrage</b> — « Configuration HTTP » : on y relit les six droits RGPD
/// et l'adresse à laquelle le service exercera chacun — ou leur état « non configuré » —, et on y
/// pose, droit par droit, cette adresse.
/// </summary>
/// <remarks>
/// <para>
/// <b>Une mini-form par droit, indépendantes.</b> Chacune n'envoie que son droit : enregistrer ou
/// effacer l'adresse de l'un ne touche jamais celle d'un autre. Un droit configuré offre en plus
/// <b>Effacer</b>, qui le ramène à « non configuré » — le geste des deux faces, tenu par
/// <see cref="ParametrageFaceModel{TForm}"/>.
/// </para>
/// <para>
/// <b>Aucun nombre agrégé, aucun taux, aucun ratio.</b> Ni « 4 droits configurés sur 6 », ni
/// « couverture : 66 % » : un tel dénominateur présenterait la configuration comme complète. L'écran
/// <b>énumère</b>, il ne compte pas. Les seuls chiffres qu'il porte sont les <b>articles</b> du RGPD
/// — un chiffre qui n'est ni un compte ni une mesure, qui se lit et s'ignore.
/// </para>
/// </remarks>
public class ParametrageModel(IMediator mediator) : ParametrageFaceModel<RightEndpointForm>(mediator)
{
  public async Task<IActionResult> OnPostSetAsync(CancellationToken cancellationToken)
  {
    var fields = Form.Read(ModelState, FormPrefix);

    if (fields is { } saving)
    {
      return await WrittenAsync(
        await Mediator.Send(new SetRightEndpointCommand(saving.Right, saving.Endpoint), cancellationToken),
        cancellationToken);
    }

    return await RefusedAsync(cancellationToken);
  }
}
