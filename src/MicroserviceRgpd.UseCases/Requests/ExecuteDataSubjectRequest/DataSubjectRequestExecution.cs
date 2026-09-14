using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.UseCases.Requests.ReadDataSubjectRequests;

namespace MicroserviceRgpd.UseCases.Requests.ExecuteDataSubjectRequest;

/// <summary>
/// Ce que rend l'exécution d'une demande : <b>la demande telle que le tableau la lit</b>, après
/// l'exécution ou après son refus, et ce qui s'est passé — le motif du refus, ou l'appel parti.
/// </summary>
/// <param name="Request">La demande, à jour : Terminée après un 2xx, inchangée sinon.</param>
/// <param name="Refusal">Le motif de blocage qui a refusé l'exécution avant l'appel, ou <c>null</c> si l'appel est parti.</param>
/// <param name="Call">Ce que l'appel au système hôte a donné, ou <c>null</c> s'il n'est pas parti.</param>
public sealed record DataSubjectRequestExecution(
  RecordedDataSubjectRequest Request,
  ExecutionBlock? Refusal,
  HostSystemCall? Call);
