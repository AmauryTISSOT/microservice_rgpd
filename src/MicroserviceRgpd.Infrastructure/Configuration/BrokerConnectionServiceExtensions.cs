using System.Globalization;
using MicroserviceRgpd.Core.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MicroserviceRgpd.Infrastructure.Configuration;

/// <summary>
/// Branche la <b>connexion du déploiement au broker</b> : ce que le déploiement déclare, et l'état
/// qui en répond (ADR-0028).
/// </summary>
public static class BrokerConnectionServiceExtensions
{
  /// <summary>
  /// Lit la section <c>RabbitMq</c> et enregistre, <b>en singleton</b>, la connexion déclarée et
  /// l'état qui la rend.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>La validation est conditionnelle</b> : pas d'hôte, pas de connexion — état <b>légal</b>, et
  /// rien d'autre n'est lu ni jugé. Le service démarre normalement, et un routage reste
  /// enregistrable avant que le bus existe (ADR-0027).
  /// </para>
  /// <para>
  /// ⚠️ <b>Un hôte présent avec un réglage aberrant arrête le démarrage</b>, exactement comme
  /// <c>HostSystem:TimeoutSeconds</c> : lu comme un port ou un délai arrondi, une faute de frappe
  /// passerait pour un réglage, et le déploiement croirait publier là où il ne publie pas.
  /// </para>
  /// <para>
  /// ⚠️ <b>Rien ne s'ouvre ici</b> : aucune socket, aucune résolution de nom. L'hôte déclaré n'est
  /// pas joint — ni au démarrage, ni jamais par ce chemin.
  /// </para>
  /// </remarks>
  /// <exception cref="ArgumentException">
  /// Un hôte est déclaré, et le port, le drapeau TLS ou le délai de confirmation qui l'accompagne
  /// n'est pas une valeur que ce réglage admet. Le message nomme la clé fautive.
  /// </exception>
  public static IServiceCollection AddBrokerConnection(this IServiceCollection services, IConfiguration configuration)
  {
    ArgumentNullException.ThrowIfNull(configuration);

    services.TryAddSingleton(Declared(configuration));
    services.TryAddSingleton<IBrokerConnectionState, BrokerConnectionState>();

    return services;
  }

  /// <summary>
  /// La connexion telle que le déploiement la déclare — ou celle d'un déploiement sans bus.
  /// <para>
  /// ⚠️ <b>Ce qu'une clé vide vaut n'est pas jugé ici</b> : la question se pose au noyau, sur
  /// <see cref="BrokerConnection.Declaring"/>, qui en est le seul domicile.
  /// </para>
  /// </summary>
  private static RabbitMqOptions Declared(IConfiguration configuration)
  {
    var hostName = configuration[RabbitMqOptions.HostNameKey];

    if (BrokerConnection.Declaring(hostName) is BrokerConnection.Absent)
    {
      return new RabbitMqOptions();
    }

    return new RabbitMqOptions
    {
      // L'hôte est là — le noyau vient de le dire — et les espaces autour d'un nom de machine ne
      // nomment rien.
      HostName = hostName!.Trim(),
      Port = Port(configuration),
      VirtualHost = configuration[RabbitMqOptions.VirtualHostKey] ?? RabbitMqOptions.DefaultVirtualHost,
      UserName = configuration[RabbitMqOptions.UserNameKey],
      Password = configuration[RabbitMqOptions.PasswordKey],
      UseTls = UseTls(configuration),
      PublishTimeoutSeconds = PublishTimeoutSeconds(configuration),
    };
  }

  /// <summary>Le port AMQP : un entier qu'un port peut valoir, ou rien.</summary>
  private static int Port(IConfiguration configuration)
  {
    var raw = configuration[RabbitMqOptions.PortKey];

    if (raw is null)
    {
      return RabbitMqOptions.DefaultPort;
    }

    if (!Integer(raw, out var port) || port is < 1 or > 65535)
    {
      throw new ArgumentException(
        $"Le réglage {RabbitMqOptions.PortKey} vaut « {raw} », qui n'est pas un port entre 1 et 65535.",
        RabbitMqOptions.PortKey);
    }

    return port;
  }

  /// <summary>Le délai d'attente d'une confirmation : un entier strictement positif, ou rien.</summary>
  private static int PublishTimeoutSeconds(IConfiguration configuration)
  {
    var raw = configuration[RabbitMqOptions.PublishTimeoutSecondsKey];

    if (raw is null)
    {
      return RabbitMqOptions.DefaultPublishTimeoutSeconds;
    }

    if (!Integer(raw, out var seconds) || seconds <= 0)
    {
      throw new ArgumentException(
        $"Le réglage {RabbitMqOptions.PublishTimeoutSecondsKey} vaut « {raw} », qui n'est pas un nombre de secondes entier strictement positif.",
        RabbitMqOptions.PublishTimeoutSecondsKey);
    }

    return seconds;
  }

  /// <summary>Le chiffrement : vrai ou faux, et rien qui leur ressemble.</summary>
  private static bool UseTls(IConfiguration configuration)
  {
    var raw = configuration[RabbitMqOptions.UseTlsKey];

    if (raw is null)
    {
      return false;
    }

    if (!bool.TryParse(raw, out var useTls))
    {
      throw new ArgumentException(
        $"Le réglage {RabbitMqOptions.UseTlsKey} vaut « {raw} », qui n'est ni « true » ni « false ».",
        RabbitMqOptions.UseTlsKey);
    }

    return useTls;
  }

  /// <summary>
  /// Un entier écrit sans signe, sans séparateur et sans espace : ce qui est lu est exactement ce
  /// qui est écrit, comme pour <c>HostSystem:TimeoutSeconds</c>.
  /// </summary>
  private static bool Integer(string raw, out int value) =>
    int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out value);
}
