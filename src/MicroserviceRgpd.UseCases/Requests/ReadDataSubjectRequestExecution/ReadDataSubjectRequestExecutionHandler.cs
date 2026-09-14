using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.Requests;

namespace MicroserviceRgpd.UseCases.Requests.ReadDataSubjectRequestExecution;

/// <summary>
/// Rend le récapitulatif de l'exécution de la demande nommée, <b>relu à l'instant</b> sur la demande et
/// le Paramétrage — ou « introuvable » quand elle n'existe plus.
/// </summary>
/// <remarks>
/// ⚠️ <b>Relu, jamais repris de la page</b> : depuis son chargement, un autre onglet a pu clore la
/// demande, ou changer l'adresse de son droit. L'<c>Operator</c> confirme ce que le système hôte recevrait maintenant.
/// </remarks>
/// <param name="requests">Les demandes enregistrées.</param>
/// <param name="settings">Le Paramétrage, en lecture seule.</param>
public sealed class ReadDataSubjectRequestExecutionHandler(
  IReadRepository<DataSubjectRequest> requests,
  IReadRepository<Settings> settings)
  : IQueryHandler<ReadDataSubjectRequestExecutionQuery, Result<DataSubjectRequestExecutionSummary>>
{
  /// <inheritdoc />
  public async ValueTask<Result<DataSubjectRequestExecutionSummary>> Handle(
    ReadDataSubjectRequestExecutionQuery query,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    var request = await requests.GetByIdAsync(query.DataSubjectRequest, cancellationToken);

    if (request is null)
    {
      return Result<DataSubjectRequestExecutionSummary>.NotFound();
    }

    return DataSubjectRequestExecutionSummary.Of(request, await ServiceSettings.ReadAsync(settings, cancellationToken));
  }
}
