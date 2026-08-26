using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.UseCases.Screenings.RunScan;

namespace MicroserviceRgpd.Infrastructure.Screenings.Scanning;

/// <summary>
/// Câble ce qui fait courir un <c>Scan</c> : le fait du déploiement, le cache d'aperçus, le geste et
/// son lanceur.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Le fait du déploiement et le cache sont des <i>singletons</i>, et c'est tout le propos.</b>
/// Un scan par déploiement, un jeu d'aperçus vivant, deux <c>Operator</c> qui voient le même compte :
/// aucune de ces trois phrases ne serait vraie d'un objet par requête.
/// </para>
/// <para>
/// ⚠️ <b>Le geste est <i>scoped</i>, parce qu'il touche au dépôt.</b> Le lanceur lui ouvre une portée
/// à lui, qui vit aussi longtemps que la lecture — et non aussi longtemps que l'onglet de
/// l'<c>Operator</c>.
/// </para>
/// </remarks>
public static class ScanningServiceExtensions
{
  public static IServiceCollection AddScanning(this IServiceCollection services)
  {
    services.TryAddSingleton(TimeProvider.System);

    services.TryAddSingleton<ScansInFlight>();
    services.TryAddSingleton<ScanPreviews>();
    services.TryAddScoped<ScanGesture>();
    services.TryAddSingleton<IScanLauncher, ScanLauncher>();

    return services;
  }
}
