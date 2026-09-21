using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.UseCases.Configuration.SetRightRabbitMqRouting;
using Microsoft.AspNetCore.Mvc;

namespace MicroserviceRgpd.Web.Pages.Configuration;

/// <summary>
/// La <b>seconde face du Paramétrage</b> — « Routage RabbitMQ » : on y relit, droit par droit,
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
/// <para>
/// <b>Elle avertit que le déploiement ne sait pas publier</b>, quand il n'a pas de connexion
/// configurée et qu'un routage a pourtant été posé : voir <see cref="LacksABrokerConnection"/>.
/// </para>
/// </remarks>
/// <param name="mediator">Le médiateur par lequel la face lit et écrit le Paramétrage.</param>
/// <param name="broker">
/// L'état de la connexion du déploiement au broker, <b>lu au rendu</b>. La face ne relit pas la
/// configuration : elle pose la question sous le nom qu'elle porte au domaine, et la réponse est
/// celle que tous les lecteurs obtiennent (ADR-0028).
/// </param>
public class ParametrageRabbitMqModel(IMediator mediator, IBrokerConnectionState broker)
  : ParametrageFaceModel<RightRabbitMqForm>(mediator)
{
  /// <summary>
  /// Le déploiement <b>n'a aucune connexion RabbitMQ configurée</b> : rien ne partira sur le bus,
  /// quels que soient les routages posés ici.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Aucun test réseau.</b> La décision se prend sur ce que le déploiement déclare, jamais sur
  /// un broker joignable : l'affichage de cette page ne dépend ainsi jamais de la disponibilité du
  /// bus (ADR-0027). Le prix en est assumé — une clé posée et un broker éteint n'avertissent de
  /// rien.
  /// <para>
  /// ⚠️ <b>Le bandeau ne juge plus lui-même.</b> Ce qu'une clé absente, vide ou faite d'espaces
  /// signifie est écrit une seule fois, sur la <c>BrokerConnection</c> du noyau : le bandeau et le
  /// motif de blocage d'une demande routée lisent ainsi la même vérité (ADR-0028).
  /// </para>
  /// </remarks>
  public bool LacksABrokerConnection => broker.Current is BrokerConnection.Absent;

  /// <summary>
  /// Le bandeau <b>concerne-t-il l'intégrateur</b> ? Il ne paraît que si le déploiement n'a pas de
  /// connexion <b>et</b> qu'au moins un routage a été posé : avertir d'un manque avant qu'il gêne
  /// qui que ce soit aurait fait du bandeau un meuble qu'on cesse de lire.
  /// </summary>
  public bool WarnsThatNothingWillBePublished =>
    LacksABrokerConnection && Settings.Rights.Any(right => right.Channel is ExerciseChannel.RabbitMq);

  /// <inheritdoc />
  public override string Face => ParametrageFaces.RabbitMq;

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
