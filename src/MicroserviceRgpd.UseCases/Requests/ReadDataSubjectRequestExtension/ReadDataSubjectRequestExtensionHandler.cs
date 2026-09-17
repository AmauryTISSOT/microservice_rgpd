using MicroserviceRgpd.Core.Requests;

namespace MicroserviceRgpd.UseCases.Requests.ReadDataSubjectRequestExtension;

/// <summary>
/// Rend le récapitulatif de la prolongation de la demande nommée, <b>relu à l'instant</b> sur la
/// demande — ou « introuvable » quand elle n'existe plus.
/// </summary>
/// <remarks>
/// ⚠️ <b>Relu, jamais repris de la page</b> : depuis son chargement, un autre onglet a pu corriger la
/// date de réception de la demande, et donc sa date limite. L'<c>Operator</c> décide sur les dates de
/// l'instant.
/// </remarks>
/// <param name="requests">Les demandes enregistrées.</param>
public sealed class ReadDataSubjectRequestExtensionHandler(IReadRepository<DataSubjectRequest> requests)
  : IQueryHandler<ReadDataSubjectRequestExtensionQuery, Result<DataSubjectRequestExtensionSummary>>
{
  /// <inheritdoc />
  public async ValueTask<Result<DataSubjectRequestExtensionSummary>> Handle(
    ReadDataSubjectRequestExtensionQuery query,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    var request = await requests.GetByIdAsync(query.DataSubjectRequest, cancellationToken);

    return request is null
      ? Result<DataSubjectRequestExtensionSummary>.NotFound()
      : DataSubjectRequestExtensionSummary.Of(request);
  }
}
