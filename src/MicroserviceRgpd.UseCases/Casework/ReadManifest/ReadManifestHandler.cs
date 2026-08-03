using MicroserviceRgpd.Core.Casework;

namespace MicroserviceRgpd.UseCases.Casework.ReadManifest;

/// <summary>
/// Rend la vue du catalogue à cet instant. Un catalogue vide est un résultat, jamais une absence :
/// c'est l'état d'un service qu'on vient d'installer, et l'écran doit pouvoir le dire.
/// </summary>
/// <param name="manifest">Le catalogue persisté, en lecture seule.</param>
public sealed class ReadManifestHandler(IReadRepository<DeclaredSystem> manifest)
  : IQueryHandler<ReadManifestQuery, Manifest>
{
  /// <inheritdoc />
  public async ValueTask<Manifest> Handle(ReadManifestQuery query, CancellationToken cancellationToken)
  {
    return Manifest.Of(await manifest.ListAsync(cancellationToken));
  }
}
