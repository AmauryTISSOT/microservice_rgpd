using System.Globalization;
using MicroserviceRgpd.Core.Requests;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MicroserviceRgpd.Infrastructure.Requests;

/// <summary>
/// Branche le système hôte, que l'exécution d'une demande appelle (ADR-0026, ADR-0028) : le canal
/// filtré, le client qui joint une adresse, et la connexion sur laquelle se publie un routage.
/// </summary>
/// <remarks>
/// ⚠️ <b>Il suppose <c>AddBrokerConnection</c> posé</b> : l'adaptateur RabbitMQ lit la connexion que
/// le déploiement déclare, et c'est la <c>Configuration</c> qui l'enregistre. Les deux se posent
/// ensemble, dans <c>InfrastructureServiceExtensions</c>.
/// </remarks>
public static class HostSystemServiceExtensions
{
  /// <summary>
  /// Le délai d'un appel au système hôte, en secondes entières. <b>Absent, il vaut
  /// <see cref="DefaultTimeoutSeconds"/></b> ; présent, il doit être un entier strictement positif.
  /// </summary>
  public const string TimeoutSecondsKey = "HostSystem:TimeoutSeconds";

  /// <summary>Le délai d'un déploiement qui ne dit rien.</summary>
  public const int DefaultTimeoutSeconds = 30;

  /// <summary>
  /// Le nom du client HTTP vers le système hôte. Public pour qu'un hôte de test le reconnaisse — et
  /// rien d'autre : les tests appellent un système hôte factice sur un port réel, par ce client-là.
  /// </summary>
  public const string ClientName = "requests-host-system";

  /// <summary>
  /// Enregistre le système hôte <b>par son port</b> — le canal filtré par
  /// <see cref="HostSystemByChannel"/> —, le client qui joint une adresse, et de quoi publier un
  /// routage.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Aucune nouvelle tentative.</b> <c>ServiceDefaults</c> pose un pipeline de résilience standard
  /// sur tous les clients, reprises comprises : il est <b>retiré</b>, comme pour les moteurs de
  /// qualification et de détection. Aucun droit n'est appliqué sans le geste de l'<c>Operator</c>.
  /// </para>
  /// <para>
  /// ⚠️ <b>Les redirections ne sont pas suivies</b> : un 3xx est une réponse non 2xx, et les données ne
  /// partent pas vers une adresse que le Paramétrage ne nomme pas.
  /// </para>
  /// <para>
  /// <b>Le délai est celui du client</b>, sur l'appel entier, corps de la réponse compris. Aucun en-tête
  /// d'authentification n'est posé : l'authentification vers le système hôte reste fermée (ADR-0016).
  /// </para>
  /// </remarks>
  /// <exception cref="ArgumentException"><see cref="TimeoutSecondsKey"/> n'est pas un entier strictement positif.</exception>
  public static IServiceCollection AddHostSystem(this IServiceCollection services, IConfiguration configuration)
  {
    ArgumentNullException.ThrowIfNull(configuration);

    var timeout = Timeout(configuration);

#pragma warning disable EXTEXP0001
    services
      .AddHttpClient(ClientName, client => client.Timeout = timeout)
      .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false })
      .RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001

    services.AddScoped<HttpHostSystem>();

    // ⚠️ La connexion au broker est un SINGLETON : une seule, partagée, ouverte paresseusement au
    // premier besoin. Le channel, lui, naît et meurt avec une exécution (ADR-0028).
    services.TryAddSingleton<IBrokerChannels, BrokerChannels>();
    services.AddScoped<RabbitMqHostSystem>();

    services.AddScoped<IHostSystem, HostSystemByChannel>();

    return services;
  }

  /// <summary>
  /// Lit le délai, ou refuse. Une valeur qui n'est pas un entier strictement positif arrête le
  /// démarrage : lue comme un délai nul ou arrondi, une faute de frappe passerait pour un réglage.
  /// </summary>
  private static TimeSpan Timeout(IConfiguration configuration)
  {
    var raw = configuration[TimeoutSecondsKey];

    if (raw is null)
    {
      return TimeSpan.FromSeconds(DefaultTimeoutSeconds);
    }

    if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) || seconds <= 0)
    {
      throw new ArgumentException(
        $"Le réglage {TimeoutSecondsKey} vaut « {raw} », qui n'est pas un nombre de secondes entier strictement positif.",
        TimeoutSecondsKey);
    }

    return TimeSpan.FromSeconds(seconds);
  }
}
