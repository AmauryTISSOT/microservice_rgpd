using MicroserviceRgpd.Core.Requests;

namespace MicroserviceRgpd.UseCases.Requests.ReadDataSubjectRequests;

/// <summary>
/// Rend toutes les demandes enregistrées, dans l'ordre par défaut. <b>Aucune demande est un
/// résultat</b> — une liste vide —, que l'écran rend en toutes lettres.
/// </summary>
/// <param name="requests">Les demandes enregistrées, en lecture seule.</param>
public sealed class ReadDataSubjectRequestsHandler(IReadRepository<DataSubjectRequest> requests)
  : IQueryHandler<ReadDataSubjectRequestsQuery, IReadOnlyList<RecordedDataSubjectRequest>>
{
  /// <inheritdoc />
  public async ValueTask<IReadOnlyList<RecordedDataSubjectRequest>> Handle(
    ReadDataSubjectRequestsQuery query,
    CancellationToken cancellationToken)
  {
    var recorded = await requests.ListAsync(new DataSubjectRequestsInDefaultOrderSpec(), cancellationToken);

    return [.. recorded.Select(RecordedDataSubjectRequest.Of)];
  }
}
