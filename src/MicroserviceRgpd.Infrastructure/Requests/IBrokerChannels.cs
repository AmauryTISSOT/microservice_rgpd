using RabbitMQ.Client;

namespace MicroserviceRgpd.Infrastructure.Requests;

/// <summary>
/// <b>De quoi publier</b> : un channel ouvert en publisher confirms, sur la connexion unique et
/// partagée du service (ADR-0028).
/// </summary>
/// <remarks>
/// <para>
/// <b>Un port d'infrastructure, non un port du domaine.</b> Il ne vit pas dans <c>Core</c> : rien du
/// domaine ne connaît un channel AMQP. Il existe pour que <see cref="RabbitMqHostSystem"/> —
/// l'adaptateur qui traduit ce que la publication a donné en résultat de tentative — s'éprouve sur
/// un <c>IChannel</c> substitué pour le seul cas qu'un broker sain ne produit pas : un <c>nack</c>.
/// </para>
/// <para>
/// ⚠️ <b>Un channel par exécution, jeté après</b> : l'appelant en dispose, toujours. La connexion,
/// elle, survit et se partage — c'est <see cref="DiscardAsync"/>, et elle seule, qui la jette quand
/// elle est tombée.
/// </para>
/// </remarks>
public interface IBrokerChannels
{
  /// <summary>
  /// Ouvre un channel en <b>publisher confirms avec suivi des confirmations</b>, en ouvrant la
  /// connexion si elle n'est pas déjà là. ⚠️ <b>Elle s'ouvre paresseusement</b> : aucune socket au
  /// démarrage du service ni au rendu d'un écran.
  /// </summary>
  /// <exception cref="RabbitMQ.Client.Exceptions.RabbitMQClientException">Le broker n'a pas pu être joint.</exception>
  Task<IChannel> OpenAsync(CancellationToken cancellationToken);

  /// <summary>
  /// <b>Jette la connexion partagée si elle est tombée</b> : la prochaine publication en rouvrira
  /// une.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Elle ne s'appelle que sur une erreur réseau</b> — un <c>nack</c> ou un message non
  /// routable n'ont rien cassé, et jeter la connexion pour eux ferait payer un aller-retour TCP à
  /// chaque refus.
  /// </para>
  /// <para>
  /// ⚠️ <b>Une connexion saine survit à cet appel</b> : deux exécutions concurrentes la partagent, et
  /// l'échec de l'une ne doit pas emporter celle que l'autre vient de rouvrir.
  /// </para>
  /// </remarks>
  ValueTask DiscardAsync();
}
