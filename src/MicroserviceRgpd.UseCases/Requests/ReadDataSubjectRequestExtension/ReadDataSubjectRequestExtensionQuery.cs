using MicroserviceRgpd.Core.Requests;

namespace MicroserviceRgpd.UseCases.Requests.ReadDataSubjectRequestExtension;

/// <summary>
/// Relire <b>ce que la prolongation d'une demande changerait</b> : le récapitulatif que la modale
/// montre avant le geste — la demande, la date limite en vigueur, et celle qui en résultera
/// (ADR-0029).
/// </summary>
/// <remarks>
/// ⚠️ <b>Lire n'est pas prolonger</b> : aucune date n'est écrite, et la demande ressort telle quelle.
/// </remarks>
/// <param name="DataSubjectRequest">La demande dont on relit la prolongation.</param>
public sealed record ReadDataSubjectRequestExtensionQuery(DataSubjectRequestId DataSubjectRequest)
  : IQuery<Result<DataSubjectRequestExtensionSummary>>;
