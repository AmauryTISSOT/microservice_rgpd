using System.Globalization;

namespace MicroserviceRgpd.Core.Configuration;

/// <summary>
/// Le <b>routage RabbitMQ</b> par lequel le service fera exercer un droit : l'<see
/// cref="ExchangeName"/> sur lequel publier, et la <see cref="RoutingKey"/> avec laquelle publier.
/// C'est une <b>espèce</b> de canal d'exercice, jamais le genre — voir <see cref="ExerciseChannel"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Aucune validation propre</b>, et c'est délibéré : ce type n'a rien à vérifier que ses deux
/// champs n'aient déjà refusé à la construction. Chaque message d'erreur se range ainsi sous son
/// champ de formulaire, comme l'<see cref="EndpointUrl"/> le fait sous le sien.
/// </para>
/// <para>
/// <b>Le construire n'appelle rien</b> : ni connexion au broker, ni résolution de nom, ni
/// déclaration d'exchange. La topologie du bus reste la propriété de l'exploitant (ADR-0027).
/// </para>
/// <para>
/// ⚠️ <b>Aucun secret ici.</b> Hôte, port, vhost et identifiants du broker relèvent de la
/// configuration de déploiement, jamais de la base ni de la surface — le même motif qui interdit
/// l'<c>userinfo</c> dans une <see cref="EndpointUrl"/>.
/// </para>
/// </remarks>
/// <param name="Exchange">L'exchange sur lequel publier.</param>
/// <param name="RoutingKey">La routing key avec laquelle publier.</param>
public sealed record RabbitMqRouting(ExchangeName Exchange, RoutingKey RoutingKey)
{
  /// <summary>
  /// Le routage <b>en toutes lettres</b> : ses deux valeurs, nommées — « exchange rgpd.rights,
  /// routing key rights.erasure ».
  /// </summary>
  /// <remarks>
  /// La phrase est écrite <b>ici et nulle part ailleurs</b> : le récapitulatif de la modale la montre
  /// à l'<c>Operator</c>, le journal d'exécution l'écrit comme exercice, et les deux disent le même
  /// canal avec les mêmes mots.
  /// </remarks>
  public string InFullWords() =>
    string.Create(CultureInfo.InvariantCulture, $"exchange {Exchange.Value}, routing key {RoutingKey.Value}");
}
