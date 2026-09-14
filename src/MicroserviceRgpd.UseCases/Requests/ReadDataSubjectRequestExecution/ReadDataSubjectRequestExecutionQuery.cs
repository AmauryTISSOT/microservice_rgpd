using MicroserviceRgpd.Core.Requests;

namespace MicroserviceRgpd.UseCases.Requests.ReadDataSubjectRequestExecution;

/// <summary>
/// Relire <b>ce que le système hôte recevrait d'une exécution, et à quelle adresse</b> : le récapitulatif que la modale de
/// confirmation montre avant le geste irréversible (ADR-0026).
/// </summary>
/// <remarks>
/// ⚠️ <b>Lire n'est pas exécuter</b> : rien ne part au système hôte, et aucune tentative n'est écrite.
/// Une demande qui ne s'exécute plus se lit comme une autre, avec son motif de blocage.
/// </remarks>
/// <param name="DataSubjectRequest">La demande dont on relit l'exécution.</param>
public sealed record ReadDataSubjectRequestExecutionQuery(DataSubjectRequestId DataSubjectRequest)
  : IQuery<Result<DataSubjectRequestExecutionSummary>>;
