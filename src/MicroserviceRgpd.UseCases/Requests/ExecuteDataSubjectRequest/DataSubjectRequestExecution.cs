using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.UseCases.Requests.ReadDataSubjectRequests;

namespace MicroserviceRgpd.UseCases.Requests.ExecuteDataSubjectRequest;

/// <summary>
/// Ce que rend l'exécution d'une demande : <b>la demande telle que le tableau la lit</b>, après
/// l'exécution ou après son refus, et ce qui s'est passé — le motif de blocage, ou l'appel parti.
/// </summary>
/// <param name="Request">
/// La demande, telle qu'elle est en base : Terminée après un 2xx enregistré, inchangée sinon — y compris
/// après un succès non enregistré.
/// </param>
/// <param name="Block">Le motif de blocage qui a arrêté l'exécution avant l'appel, ou <c>null</c> si l'appel est parti.</param>
/// <param name="Call">Ce que l'appel au système hôte a donné, ou <c>null</c> s'il n'est pas parti.</param>
/// <param name="Outcome">
/// Le résultat de la tentative écrite, ou <c>null</c> si l'appel n'est pas parti. ⚠️ Il diffère de celui
/// de <paramref name="Call"/> pour un seul cas : <see cref="ExecutionOutcome.SucceededButNotRecorded"/>.
/// </param>
public sealed record DataSubjectRequestExecution(
  RecordedDataSubjectRequest Request,
  ExecutionBlock? Block,
  HostSystemCall? Call,
  ExecutionOutcome? Outcome);
