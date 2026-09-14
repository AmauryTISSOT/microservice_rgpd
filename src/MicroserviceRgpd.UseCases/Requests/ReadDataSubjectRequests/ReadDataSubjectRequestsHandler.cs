using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.Requests;

namespace MicroserviceRgpd.UseCases.Requests.ReadDataSubjectRequests;

/// <summary>
/// Rend toutes les demandes enregistrées, dans l'ordre par défaut. <b>Aucune demande est un
/// résultat</b> — une liste vide —, que l'écran rend en toutes lettres.
/// </summary>
/// <remarks>
/// ⚠️ <b>Le Paramétrage est lu une seule fois</b>, pour toutes les lignes : chacune y trouve
/// l'adresse de son droit, face à laquelle la demande dit si elle s'exécute (ADR-0026).
/// </remarks>
/// <param name="requests">Les demandes enregistrées, en lecture seule.</param>
/// <param name="settings">Le Paramétrage, en lecture seule — au plus une ligne.</param>
public sealed class ReadDataSubjectRequestsHandler(
  IReadRepository<DataSubjectRequest> requests,
  IReadRepository<Settings> settings)
  : IQueryHandler<ReadDataSubjectRequestsQuery, IReadOnlyList<RecordedDataSubjectRequest>>
{
  /// <inheritdoc />
  public async ValueTask<IReadOnlyList<RecordedDataSubjectRequest>> Handle(
    ReadDataSubjectRequestsQuery query,
    CancellationToken cancellationToken)
  {
    var recorded = await requests.ListAsync(new DataSubjectRequestsInDefaultOrderSpec(), cancellationToken);
    var current = await ServiceSettings.ReadAsync(settings, cancellationToken);

    return [.. recorded.Select(request => RecordedDataSubjectRequest.Of(request, current))];
  }
}
