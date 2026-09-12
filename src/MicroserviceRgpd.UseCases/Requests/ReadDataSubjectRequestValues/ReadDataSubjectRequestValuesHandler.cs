using MicroserviceRgpd.Core.Requests;

namespace MicroserviceRgpd.UseCases.Requests.ReadDataSubjectRequestValues;

/// <summary>
/// Rend les valeurs saisies de la demande nommée — ou « introuvable » quand elle n'existe plus.
/// </summary>
/// <remarks>
/// <b>Une demande introuvable n'est pas une faute de programmation</b> : deux onglets ouverts sur le
/// tableau, et l'autre l'a supprimée entre-temps.
/// </remarks>
/// <param name="requests">Les demandes enregistrées.</param>
public sealed class ReadDataSubjectRequestValuesHandler(IReadRepository<DataSubjectRequest> requests)
  : IQueryHandler<ReadDataSubjectRequestValuesQuery, Result<DataSubjectRequestValues>>
{
  /// <inheritdoc />
  public async ValueTask<Result<DataSubjectRequestValues>> Handle(
    ReadDataSubjectRequestValuesQuery query,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    var recorded = await requests.GetByIdAsync(query.DataSubjectRequest, cancellationToken);

    if (recorded is null)
    {
      return Result<DataSubjectRequestValues>.NotFound();
    }

    return DataSubjectRequestValues.Of(recorded);
  }
}
