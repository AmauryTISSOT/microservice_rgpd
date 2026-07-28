using MicroserviceRgpd.Core.Qualifications;

namespace MicroserviceRgpd.Infrastructure.Qualifications;

/// <summary>
/// Branche les moteurs de qualification sur le sidecar qui les héberge.
/// </summary>
public static class QualificationEngineServiceExtensions
{
  /// <summary>
  /// Enregistre le moteur témoin. Il est déclaré <b>par son port</b> : le use case qui le consomme
  /// ne doit pas pouvoir apprendre qu'un sidecar Python existe.
  /// </summary>
  /// <remarks>
  /// L'adresse vit <b>en configuration seule</b>, sans repli codé en dur. Un repli ferait exister
  /// deux vérités qui finiraient par diverger, et surtout il transformerait une configuration
  /// oubliée en pannes de qualification au premier appel, là où elle doit arrêter le démarrage —
  /// exactement le traitement que reçoit déjà la chaîne de connexion.
  /// </remarks>
  public static IServiceCollection AddQualificationEngines(
    this IServiceCollection services,
    IConfiguration configuration)
  {
    ArgumentNullException.ThrowIfNull(configuration);

    var sidecar = configuration["Qualification:SidecarBaseAddress"];
    Guard.Against.NullOrEmpty(sidecar, nameof(sidecar),
      "Aucune adresse de sidecar de qualification configuree : renseigner Qualification:SidecarBaseAddress.");

    services.AddHttpClient<IQualificationEngine, LexiconQualificationEngine>(client =>
    {
      client.BaseAddress = new Uri(sidecar, UriKind.Absolute);
    });

    return services;
  }
}
