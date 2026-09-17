using MicroserviceRgpd.UseCases.Configuration.SetRightRabbitMqRouting;
using Microsoft.AspNetCore.Mvc;

namespace MicroserviceRgpd.Web.Pages.Configuration;

/// <summary>
/// La <b>seconde face du Paramétrage</b> — « Configuration RabbitMQ » : on y relit, droit par droit,
/// le routage par lequel le service exercera chacun des six droits RGPD — ou leur état « non
/// configuré » —, et on y <b>déclare</b> ce routage : un exchange, une routing key.
/// </summary>
/// <remarks>
/// <para>
/// <b>Une face, pas un écran de plus.</b> Elle porte le même titre que la face HTTP et se rejoint
/// par des onglets ; le panneau latéral ne bouge pas, et « Paramétrage » y reste marqué courant.
/// </para>
/// <para>
/// <b>Une mini-form par droit, indépendantes</b>, comme sur la face HTTP : chacune n'envoie que son
/// droit, et il n'y a ni « tout enregistrer » ni « tout effacer ». Un droit portant un routage offre
/// en plus <b>Effacer</b>, qui le ramène à « non configuré » — le geste des deux faces, tenu par
/// <see cref="ParametrageFaceModel{TForm}"/>.
/// </para>
/// <para>
/// ⚠️ <b>Elle configure, elle n'appelle pas.</b> Enregistrer est une écriture locale : aucune
/// connexion au broker, aucun exchange déclaré ni vérifié, rien de publié (ADR-0027). Un routage
/// reste donc enregistrable alors même que le broker n'existe pas encore.
/// </para>
/// <para>
/// ⚠️ <b>Le refus vient du serveur, jamais du navigateur.</b> Les champs ne portent pas de
/// <c>maxlength</c> — l'attribut compte des unités UTF-16, la contrainte des octets UTF-8 —, et
/// c'est le domaine qui juge, en français, sous le champ fautif.
/// </para>
/// </remarks>
public class ParametrageRabbitMqModel(IMediator mediator) : ParametrageFaceModel<RightRabbitMqForm>(mediator)
{
  public async Task<IActionResult> OnPostSetAsync(CancellationToken cancellationToken)
  {
    var fields = Form.Read(ModelState, FormPrefix);

    if (fields is { } saving)
    {
      return await WrittenAsync(
        await Mediator.Send(new SetRightRabbitMqRoutingCommand(saving.Right, saving.Routing), cancellationToken),
        cancellationToken);
    }

    return await RefusedAsync(cancellationToken);
  }
}
