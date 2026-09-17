using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.Requests;

namespace MicroserviceRgpd.UseCases.Requests.ReadDataSubjectRequests;

/// <summary>
/// Rend toutes les demandes enregistrées, dans l'ordre par défaut. <b>Aucune demande est un
/// résultat</b> — une liste vide —, que l'écran rend en toutes lettres.
/// </summary>
/// <remarks>
/// ⚠️ <b>Le Paramétrage est lu une seule fois</b>, pour toutes les lignes : chacune y trouve le canal
/// de son droit, face auquel la demande dit si elle s'exécute (ADR-0026). La connexion du déploiement
/// se lit de même — elle ne change pas d'une ligne à l'autre (ADR-0028).
/// </remarks>
/// <param name="requests">Les demandes enregistrées, en lecture seule.</param>
/// <param name="settings">Le Paramétrage, en lecture seule — au plus une ligne.</param>
/// <param name="broker">Ce que ce déploiement déclare savoir publier.</param>
public sealed class ReadDataSubjectRequestsHandler(
  IReadRepository<DataSubjectRequest> requests,
  IReadRepository<Settings> settings,
  IBrokerConnectionState broker)
  : IQueryHandler<ReadDataSubjectRequestsQuery, IReadOnlyList<RecordedDataSubjectRequest>>
{
  /// <inheritdoc />
  public async ValueTask<IReadOnlyList<RecordedDataSubjectRequest>> Handle(
    ReadDataSubjectRequestsQuery query,
    CancellationToken cancellationToken)
  {
    var recorded = await requests.ListAsync(new DataSubjectRequestsInDefaultOrderSpec(), cancellationToken);
    var current = await ServiceSettings.ReadAsync(settings, cancellationToken);
    var connection = broker.Current;

    return [.. recorded.Select(request => RecordedDataSubjectRequest.Of(request, current, connection))];
  }
}
