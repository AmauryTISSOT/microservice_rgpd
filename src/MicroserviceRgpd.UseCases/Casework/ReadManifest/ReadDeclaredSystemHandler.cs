using MicroserviceRgpd.Core.Casework;

namespace MicroserviceRgpd.UseCases.Casework.ReadManifest;

/// <summary>Rend un système déclaré, ou dit qu'aucun ne porte cet identifiant.</summary>
/// <param name="manifest">Le catalogue persisté, en lecture seule.</param>
public sealed class ReadDeclaredSystemHandler(IReadRepository<DeclaredSystem> manifest)
  : IQueryHandler<ReadDeclaredSystemQuery, Result<DeclaredSystem>>
{
  /// <inheritdoc />
  public async ValueTask<Result<DeclaredSystem>> Handle(
    ReadDeclaredSystemQuery query,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    var declared = await manifest.FirstOrDefaultAsync(new DeclaredSystemByIdSpec(query.Id), cancellationToken);

    return declared is null
      ? Result<DeclaredSystem>.NotFound($"L'identifiant « {query.Id.Value} » ne désigne aucun système déclaré.")
      : Result<DeclaredSystem>.Success(declared);
  }
}
