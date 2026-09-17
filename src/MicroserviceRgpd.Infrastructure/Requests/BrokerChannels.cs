using MicroserviceRgpd.Core.Configuration;
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
  /// <exception cref="InvalidOperationException">
  /// ⚠️ <b>Ce déploiement ne déclare aucune connexion</b>, et rien n'aurait dû demander à publier :
  /// le motif <c>BrokerConnectionMissing</c> écarte un droit routé bien avant la remise, et le
  /// serveur le revérifie juste avant. Le dire plutôt que le supposer — et le dire en faute de
  /// programmation, non en panne de réseau, qu'un <c>ExecutionOutcome</c> ferait passer pour un
  /// broker éteint.
  /// </exception>
  public async Task<IChannel> OpenAsync(CancellationToken cancellationToken)
  {
    if (BrokerConnection.Declaring(options.HostName) is BrokerConnection.Absent)
    {
      throw new InvalidOperationException(
        "Ce déploiement ne déclare aucune connexion au broker : il n'y a nulle part où publier.");
    }

    var connection = await ConnectionAsync(cancellationToken);

    return await connection.CreateChannelAsync(PublisherConfirms, cancellationToken);
  }

  /// <inheritdoc />
  public ValueTask DiscardAsync() => CloseAsync(onlyIfBroken: true);

  /// <inheritdoc />
  public async ValueTask DisposeAsync()
  {
    // À l'arrêt, la connexion se ferme qu'elle soit saine ou non.
    await CloseAsync(onlyIfBroken: false);

    _opening.Dispose();
  }

  /// <summary>Ferme la connexion partagée, s'il y en a une.</summary>
  /// <remarks>
  /// ⚠️ <b><paramref name="onlyIfBroken"/> évite qu'une exécution en échec emporte la connexion
  /// qu'une autre vient de rouvrir.</b> Deux exécutions concurrentes partagent une seule connexion :
  /// la jeter à l'aveugle ferait payer à la seconde la panne de la première. Le client AMQP ferme la
  /// sienne dès qu'elle casse — c'est ce qu'on lit, plutôt que de le supposer. Une connexion
  /// réellement morte que le client n'a pas encore marquée fermée survit à ce tour, et la
  /// publication suivante échouera à y tailler un channel : elle rappellera ici, et la trouvera
  /// fermée.
  /// </remarks>
  private async ValueTask CloseAsync(bool onlyIfBroken)
  {
    await _opening.WaitAsync(CancellationToken.None);

    try
    {
      if (_connection is not { } current || (onlyIfBroken && current.IsOpen))
      {
        return;
      }

      _connection = null;

      // Dans le cas qui nous amène ici, la connexion est déjà cassée : la fermer proprement est un
      // effort de politesse, et son échec ne doit pas masquer l'échec de la publication.
      try
      {
        await current.DisposeAsync();
      }
      catch (Exception closing) when (BrokerFailure.IsNetwork(closing))
      {
        // Rien à dire : elle ne servira plus.
      }
    }
    finally
    {
      _opening.Release();
    }
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
