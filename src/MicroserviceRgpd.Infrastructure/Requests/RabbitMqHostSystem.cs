using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.Infrastructure.Configuration;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace MicroserviceRgpd.Infrastructure.Requests;

/// <summary>
/// Le système hôte joint <b>par RabbitMQ</b> : une publication persistante sur l'exchange et la
/// routing key du Paramétrage, en <b>publisher confirms</b> et avec le drapeau <b><c>mandatory</c></b>,
/// dont le corps porte exactement les cinq clés du corps HTTP (ADR-0028). Il est l'un des
/// destinataires de <see cref="HostSystemByChannel"/>, et <b>ne connaît que le routage</b> — jamais le
/// genre du canal.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Il ne déclare ni ne vérifie jamais l'exchange, la file ou le binding.</b> La topologie du bus
/// reste la propriété de l'exploitant (ADR-0027) : le service publie là où on lui dit de publier, et
/// un routage qui ne mène nulle part est un <see cref="ExecutionOutcome.Unroutable"/>, non une
/// topologie à créer.
/// </para>
/// <para>
/// ⚠️ <b>Sans <c>mandatory</c>, un message publié sur un exchange sans binding disparaît en
/// silence</b>, et la demande passerait à Terminée sur un mensonge. Le drapeau est ce qui l'empêche,
/// et la couture fonctionnelle du non routable est ce qui empêche le drapeau de disparaître.
/// </para>
/// <para>
/// <b>La traduction qu'il fait est la décision</b> : un accusé vaut aboutissement, un retour vaut
/// « personne ne l'a reçu », un <c>nack</c> vaut refus, l'annulation par le délai vaut délai dépassé,
/// et tout ce qui casse la connexion vaut erreur réseau. Rien d'autre du service ne rejuge ce verdict.
/// </para>
/// <para>
/// ⚠️ <b>Il ne lève pas pour un échec de la publication</b>, et <b>ne republie jamais</b> : une
/// publication, un résultat, une ligne de journal.
/// </para>
/// </remarks>
/// <param name="channels">De quoi ouvrir un channel sur la connexion partagée.</param>
/// <param name="options">La connexion déclarée, dont le délai d'attente d'une confirmation.</param>
/// <param name="clock">L'horloge du service, qui date le départ et mesure la durée jusqu'à l'accusé.</param>
public sealed class RabbitMqHostSystem(IBrokerChannels channels, RabbitMqOptions options, TimeProvider clock)
{
  /// <summary>
  /// Publie sur <paramref name="routing"/> le droit que porte <paramref name="body"/>, et rend ce que
  /// la publication a donné — une fois le broker, ou son silence, revenu.
  /// </summary>
  /// <exception cref="ArgumentNullException"><paramref name="routing"/> ou <paramref name="body"/> est absent.</exception>
  public async Task<HostSystemCall> PublishAsync(
    RabbitMqRouting routing,
    ExecutionBody body,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(routing);
    ArgumentNullException.ThrowIfNull(body);

    var startedAt = clock.GetUtcNow();
    var started = clock.GetTimestamp();

    IChannel channel;

    try
    {
      channel = await channels.OpenAsync(cancellationToken);
    }
    catch (Exception unreachable) when (BrokerFailure.IsUnreachable(unreachable))
    {
      // Rien n'est parti : la connexion est jetée, et la prochaine exécution en rouvrira une.
      await channels.DiscardAsync();

      return HostSystemCall.Unreachable(startedAt, clock.GetElapsedTime(started));
    }

    try
    {
      // ⚠️ Le délai porte sur l'attente de la confirmation : c'est BasicPublishAsync qui attend, et
      // l'annuler est la seule façon de ne pas faire patienter l'Operator indéfiniment.
      using var confirmation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
      confirmation.CancelAfter(options.PublishTimeout);

      try
      {
        await channel.BasicPublishAsync(
          exchange: routing.Exchange.Value,
          routingKey: routing.RoutingKey.Value,
          mandatory: true,
          basicProperties: PropertiesOf(body),
          body: ExecutionWireBody.BytesOf(body),
          cancellationToken: confirmation.Token);

        return HostSystemCall.Acknowledged(startedAt, clock.GetElapsedTime(started));
      }
      catch (PublishException published)
      {
        // Le seul endroit du service où ces deux cas se distinguent : un retour dit « aucune file ne
        // l'a reçu », son absence dit « le broker a refusé ».
        return published.IsReturn
          ? HostSystemCall.Unroutable(startedAt, clock.GetElapsedTime(started))
          : HostSystemCall.Rejected(startedAt, clock.GetElapsedTime(started));
      }
      catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
      {
        // Le délai se dit par une annulation que personne n'a demandée — comme pour le client HTTP.
        return HostSystemCall.TimedOut(startedAt, clock.GetElapsedTime(started), options.PublishTimeout);
      }
      catch (Exception unreachable) when (BrokerFailure.IsUnreachable(unreachable))
      {
        await channels.DiscardAsync();

        return HostSystemCall.Unreachable(startedAt, clock.GetElapsedTime(started));
      }
    }
    finally
    {
      // Un channel par exécution, jeté après — qu'elle ait abouti ou non.
      await DiscardAsync(channel);
    }
  }

  /// <summary>
  /// Les propriétés du message : <c>application/json</c>, <b>persistant</b>, et un
  /// <c>message-id</c> égal à l'identifiant de la demande — de quoi dédupliquer une republication
  /// <b>sans ouvrir le corps</b>. L'idempotence reste la charge du destinataire, ici comme en HTTP.
  /// </summary>
  private static BasicProperties PropertiesOf(ExecutionBody body) => new()
  {
    ContentType = "application/json",
    MessageId = body.RequestId.Value.ToString(),
    Persistent = true,
  };

  /// <summary>
  /// Jette le channel. ⚠️ <b>Son échec ne masque pas le résultat de la publication</b> : un channel
  /// dont la connexion vient de tomber ne se ferme pas proprement, et ce n'est pas ce que
  /// l'<c>Operator</c> a besoin de lire.
  /// </summary>
  private static async ValueTask DiscardAsync(IChannel channel)
  {
    try
    {
      await channel.DisposeAsync();
    }
    catch (Exception closing) when (BrokerFailure.IsNetwork(closing))
    {
      // Rien à dire : il ne servira plus.
    }
  }
}
