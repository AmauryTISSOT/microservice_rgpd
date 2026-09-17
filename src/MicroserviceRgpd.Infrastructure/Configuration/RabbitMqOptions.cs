namespace MicroserviceRgpd.Infrastructure.Configuration;

/// <summary>
/// La <b>connexion au broker telle que le déploiement la déclare</b>, sous la section
/// <c>RabbitMq</c> : hôte, port, vhost, identifiants, TLS, et le délai d'attente d'une confirmation
/// de publication (ADR-0028).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Elle vit dans la configuration de déploiement, jamais en base ni à l'écran</b> : aucun
/// secret ne transite par le Paramétrage, qui ne détient rien de tel (ADR-0016, ADR-0027).
/// </para>
/// <para>
/// ⚠️ <b>Aucune chaîne de connexion URI.</b> Les identifiants n'entrent pas dans une URL, pour le
/// motif qui interdit déjà l'<c>userinfo</c> dans une <c>EndpointUrl</c> : une URL se journalise, se
/// recopie et s'affiche.
/// </para>
/// <para>
/// ⚠️ <b>La lire n'ouvre rien.</b> Ce n'est qu'un écrit du déploiement : aucune socket ne s'ouvre à
/// sa lecture, ni au démarrage, ni au rendu d'un écran.
/// </para>
/// </remarks>
public sealed record RabbitMqOptions
{
  /// <summary>La section qui porte la connexion au broker.</summary>
  public const string SectionName = "RabbitMq";

  /// <summary>
  /// L'hôte du broker. <b>Sa seule présence dit que ce déploiement a une connexion</b> ; son absence
  /// est un état légal, jamais une erreur de démarrage.
  /// </summary>
  public const string HostNameKey = $"{SectionName}:{nameof(HostName)}";

  /// <summary>Le port AMQP. Absent, il vaut <see cref="DefaultPort"/>.</summary>
  public const string PortKey = $"{SectionName}:{nameof(Port)}";

  /// <summary>Le vhost. Absent, il vaut <see cref="DefaultVirtualHost"/>.</summary>
  public const string VirtualHostKey = $"{SectionName}:{nameof(VirtualHost)}";

  /// <summary>L'identifiant de connexion. Absent, le client emploiera le sien.</summary>
  public const string UserNameKey = $"{SectionName}:{nameof(UserName)}";

  /// <summary>Le mot de passe de connexion. Absent, le client emploiera le sien.</summary>
  public const string PasswordKey = $"{SectionName}:{nameof(Password)}";

  /// <summary>Le chiffrement de la connexion. Absent, il vaut faux.</summary>
  public const string UseTlsKey = $"{SectionName}:{nameof(UseTls)}";

  /// <summary>
  /// Le délai d'attente de la confirmation d'une publication, en secondes entières. Absent, il vaut
  /// <see cref="DefaultPublishTimeoutSeconds"/>.
  /// </summary>
  public const string PublishTimeoutSecondsKey = $"{SectionName}:{nameof(PublishTimeoutSeconds)}";

  /// <summary>Le port AMQP d'un déploiement qui ne dit rien.</summary>
  public const int DefaultPort = 5672;

  /// <summary>Le vhost d'un déploiement qui ne dit rien.</summary>
  public const string DefaultVirtualHost = "/";

  /// <summary>
  /// Le délai d'un déploiement qui ne dit rien : <b>10 secondes</b>. Un ack local est affaire de
  /// millisecondes, et faire patienter l'<c>Operator</c> une demi-minute n'a pas de contrepartie —
  /// c'est pourquoi ce délai est plus court que celui d'un appel HTTP au système hôte.
  /// </summary>
  public const int DefaultPublishTimeoutSeconds = 10;

  /// <summary>L'hôte déclaré, rogné — ou <c>null</c> quand le déploiement n'en nomme aucun.</summary>
  public string? HostName { get; init; }

  /// <summary>Le port AMQP.</summary>
  public int Port { get; init; } = DefaultPort;

  /// <summary>Le vhost.</summary>
  public string VirtualHost { get; init; } = DefaultVirtualHost;

  /// <summary>L'identifiant de connexion, s'il est déclaré.</summary>
  public string? UserName { get; init; }

  /// <summary>Le mot de passe de connexion, s'il est déclaré.</summary>
  public string? Password { get; init; }

  /// <summary>La connexion est-elle chiffrée ?</summary>
  public bool UseTls { get; init; }

  /// <summary>Le délai d'attente d'une confirmation, en secondes entières.</summary>
  public int PublishTimeoutSeconds { get; init; } = DefaultPublishTimeoutSeconds;

  /// <summary>Le même délai, tel qu'une attente le prend.</summary>
  public TimeSpan PublishTimeout => TimeSpan.FromSeconds(PublishTimeoutSeconds);
}
