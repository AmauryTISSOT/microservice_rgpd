using MicroserviceRgpd.Core.Configuration;
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
/// <para>
/// <b>Elle avertit que le déploiement ne sait pas publier</b>, quand il n'a pas de connexion
/// configurée et qu'un routage a pourtant été posé : voir <see cref="LacksABrokerConnection"/>.
/// </para>
/// </remarks>
/// <param name="mediator">Le médiateur par lequel la face lit et écrit le Paramétrage.</param>
/// <param name="configuration">
/// La configuration du déploiement, lue <b>au rendu</b> pour la seule présence de la clé de
/// connexion. Elle est injectée plutôt que lue par un type d'options : un type d'options viendra
/// avec l'US de publication, quand port, vhost et identifiants s'ajouteront à l'hôte.
/// </param>
public class ParametrageRabbitMqModel(IMediator mediator, IConfiguration configuration)
  : ParametrageFaceModel<RightRabbitMqForm>(mediator)
{
  /// <summary>
  /// La clé de déploiement dont la <b>seule présence</b> dit que ce déploiement a une connexion
  /// RabbitMQ. Elle est publique pour que les tests la <b>citent</b> plutôt que de la recopier :
  /// recopiée, elle aurait divergé, et le bandeau se serait mis à parler d'une clé que personne ne
  /// pose.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Elle n'est validée nulle part au démarrage.</b> Son absence n'est pas une erreur de
  /// configuration : un routage doit rester enregistrable avant que le bus existe (ADR-0027). Le
  /// port, le vhost et les identifiants ne sont pas de ce ticket — ils viendront avec la
  /// publication, et un type d'options avec eux.
  /// </remarks>
  public const string BrokerHostNameKey = "RabbitMq:HostName";

  /// <summary>
  /// Le déploiement <b>n'a aucune connexion RabbitMQ configurée</b> : rien ne partira sur le bus,
  /// quels que soient les routages posés ici.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Aucun test réseau.</b> La décision se prend sur la seule présence de la clé, jamais sur
  /// un broker joignable : l'affichage de cette page ne dépend ainsi jamais de la disponibilité du
  /// bus (ADR-0027). Le prix en est assumé — une clé posée et un broker éteint n'avertissent de
  /// rien.
  /// <para>
  /// Une clé vide ou faite d'espaces vaut une clé absente : un hôte de broker qui ne nomme aucune
  /// machine ne connecte rien, et avertir l'intégrateur reste alors la vérité.
  /// </para>
  /// </remarks>
  public bool LacksABrokerConnection => string.IsNullOrWhiteSpace(configuration[BrokerHostNameKey]);

  /// <summary>
  /// Le bandeau <b>concerne-t-il l'intégrateur</b> ? Il ne paraît que si le déploiement n'a pas de
  /// connexion <b>et</b> qu'au moins un routage a été posé : avertir d'un manque avant qu'il gêne
  /// qui que ce soit aurait fait du bandeau un meuble qu'on cesse de lire.
  /// </summary>
  public bool WarnsThatNothingWillBePublished =>
    LacksABrokerConnection && Settings.Rights.Any(right => right.Channel is ExerciseChannel.RabbitMq);

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
