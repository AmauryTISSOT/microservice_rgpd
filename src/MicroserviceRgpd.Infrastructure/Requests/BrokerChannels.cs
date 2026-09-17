using MicroserviceRgpd.Infrastructure.Configuration;
using RabbitMQ.Client;

namespace MicroserviceRgpd.Infrastructure.Requests;

/// <summary>
/// La <b>connexion unique et partagée</b> du service au broker, ouverte paresseusement au premier
/// besoin, et les channels qu'on y taille (ADR-0028).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>La reprise automatique du client AMQP est désactivée</b>, et c'est la décision la plus
/// lourde de ce type. Active par défaut, elle rouvre connexion et channels dans le dos du service et
/// peut <b>rejouer</b> ce qu'il croyait perdu : un droit serait remis au système hôte sans qu'aucun
/// <c>Operator</c> l'ait demandé. C'est le pendant exact du <c>RemoveAllResilienceHandlers</c> posé
/// sur le client HTTP — <b>aucune nouvelle tentative automatique</b>.
/// </para>
/// <para>
/// ⚠️ <b>Rien ne s'ouvre au démarrage.</b> La disponibilité du service ne dépend jamais de celle du
/// bus : un déploiement avec une connexion déclarée devant un broker éteint démarre, rend ses
/// écrans, et n'échoue qu'à la publication (ADR-0027).
/// </para>
/// <para>
/// <b>Un seul ouvreur à la fois</b> : le verrou évite que deux exécutions concurrentes ouvrent deux
/// connexions dont l'une serait aussitôt perdue.
/// </para>
/// </remarks>
/// <param name="options">La connexion telle que le déploiement la déclare.</param>
public sealed class BrokerChannels(RabbitMqOptions options) : IBrokerChannels, IAsyncDisposable
{
  /// <summary>
  /// Le nom sous lequel le service se présente au broker, visible dans la console de l'exploitant.
  /// </summary>
  public const string ClientName = "microservice-rgpd";

  /// <summary>
  /// ⚠️ <b>Publisher confirms ET suivi des confirmations</b> : c'est ce second drapeau qui fait
  /// attendre l'accusé à <c>BasicPublishAsync</c>, et qui rend un message non routable plutôt que de
  /// le laisser disparaître en silence.
  /// </summary>
  private static readonly CreateChannelOptions PublisherConfirms = new(
    publisherConfirmationsEnabled: true,
    publisherConfirmationTrackingEnabled: true);

  private readonly SemaphoreSlim _opening = new(1, 1);

  private IConnection? _connection;

  /// <inheritdoc />
  public async Task<IChannel> OpenAsync(CancellationToken cancellationToken)
  {
    var connection = await ConnectionAsync(cancellationToken);

    return await connection.CreateChannelAsync(PublisherConfirms, cancellationToken);
  }

  /// <inheritdoc />
  public async ValueTask DiscardAsync()
  {
    await _opening.WaitAsync(CancellationToken.None);

    try
    {
      if (_connection is { } discarded)
      {
        _connection = null;

        // La connexion est déjà cassée dans le cas qui nous amène ici : la fermer proprement est un
        // effort de politesse, et son échec ne doit pas masquer l'échec de la publication.
        try
        {
          await discarded.DisposeAsync();
        }
        catch (Exception closing) when (closing is RabbitMQ.Client.Exceptions.RabbitMQClientException or IOException or System.Net.Sockets.SocketException)
        {
          // Rien à dire : elle ne servira plus.
        }
      }
    }
    finally
    {
      _opening.Release();
    }
  }

  /// <inheritdoc />
  public async ValueTask DisposeAsync()
  {
    await DiscardAsync();
    _opening.Dispose();
  }

  /// <summary>
  /// La connexion partagée — celle qui est ouverte, ou une neuve. ⚠️ <b>Une connexion fermée est
  /// remplacée</b> : sans reprise automatique, personne ne la rouvrira à notre place.
  /// </summary>
  private async Task<IConnection> ConnectionAsync(CancellationToken cancellationToken)
  {
    if (_connection is { IsOpen: true } opened)
    {
      return opened;
    }

    await _opening.WaitAsync(cancellationToken);

    try
    {
      // Relu sous le verrou : une exécution concurrente a pu ouvrir la connexion pendant l'attente.
      if (_connection is { IsOpen: true } concurrent)
      {
        return concurrent;
      }

      _connection = await Factory().CreateConnectionAsync(cancellationToken);

      return _connection;
    }
    finally
    {
      _opening.Release();
    }
  }

  /// <summary>
  /// La fabrique, telle que le déploiement la déclare — <b>sans reprise</b>, et sans chaîne de
  /// connexion URI : les identifiants n'entrent pas dans une URL (ADR-0028).
  /// </summary>
  private ConnectionFactory Factory()
  {
    var factory = new ConnectionFactory
    {
      HostName = options.HostName!,
      Port = options.Port,
      VirtualHost = options.VirtualHost,
      ClientProvidedName = ClientName,

      // ⚠️ Les deux ensemble : la reprise de topologie ne vit que sous la reprise automatique, et
      // les écrire toutes deux dit l'intention plutôt que de la laisser à un défaut.
      AutomaticRecoveryEnabled = false,
      TopologyRecoveryEnabled = false,
    };

    if (options.UserName is { } userName)
    {
      factory.UserName = userName;
    }

    if (options.Password is { } password)
    {
      factory.Password = password;
    }

    if (options.UseTls)
    {
      factory.Ssl = new SslOption { Enabled = true, ServerName = options.HostName! };
    }

    return factory;
  }
}
