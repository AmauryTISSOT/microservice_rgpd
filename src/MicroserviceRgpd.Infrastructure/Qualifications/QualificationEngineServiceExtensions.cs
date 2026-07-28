using MicroserviceRgpd.Core.Qualifications;

namespace MicroserviceRgpd.Infrastructure.Qualifications;

/// <summary>
/// Branche les moteurs de qualification sur le sidecar qui les héberge.
/// </summary>
public static class QualificationEngineServiceExtensions
{
  /// <summary>
  /// L'adresse par défaut est le <b>nom du service</b>, non une machine : Aspire le résout, et le
  /// jour où le sidecar déménage, rien de ce qui est écrit ici ne bouge.
  /// </summary>
  private const string DefaultSidecarBaseAddress = "http://qualification-sidecar";

  /// <summary>
  /// Enregistre le moteur témoin. Il est déclaré <b>par son port</b> : le use case qui le consomme
  /// ne doit pas pouvoir apprendre qu'un sidecar Python existe.
  /// </summary>
  public static IServiceCollection AddQualificationEngines(
    this IServiceCollection services,
    IConfiguration configuration)
  {
    ArgumentNullException.ThrowIfNull(configuration);

    var sidecar = configuration["Qualification:SidecarBaseAddress"] ?? DefaultSidecarBaseAddress;

    services.AddHttpClient<IQualificationEngine, LexiconQualificationEngine>(client =>
    {
      client.BaseAddress = new Uri(sidecar, UriKind.Absolute);
    });

    return services;
  }
}
