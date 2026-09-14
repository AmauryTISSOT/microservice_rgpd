using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.UseCases.Requests.ReadDataSubjectRequests;

namespace MicroserviceRgpd.UseCases.Requests.ExecuteDataSubjectRequest;

/// <summary>
/// Recalcule l'exécutabilité de la demande face au Paramétrage, appelle le système hôte, termine la
/// demande sur un 2xx et écrit la tentative — ou rend « introuvable », ou le refus du premier motif de
/// blocage, sans appel ni tentative (ADR-0026).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Le serveur fait foi, juste avant l'appel.</b> L'écran a pu montrer un bouton actif sur une
/// demande qu'un autre onglet a close, ou dont l'adresse a été retirée : les conditions sont relues
/// ici, sur la demande et le Paramétrage de l'instant. Une demande close est un <c>Conflict</c>, comme
/// pour la modification ; tout autre motif est un <c>Invalid</c>.
/// </para>
/// <para>
/// ⚠️ <b>Le passage à Terminée et la tentative partent dans la même transaction.</b> Les deux dépôts
/// partagent le même contexte : l'ajout de la tentative enregistre du même coup la demande que
/// <see cref="DataSubjectRequest.Complete"/> vient de toucher, en une seule écriture.
/// </para>
/// <para>
/// ⚠️ <b>L'annulation de la requête entrante n'est pas propagée</b> à l'appel ni à l'écriture de sa
/// tentative : l'appel va à son terme même si le navigateur s'en va, et le journal dit ce qui s'est
/// vraiment passé.
/// </para>
/// <para>
/// Aucune nouvelle tentative : un appel, une tentative écrite.
/// </para>
/// </remarks>
/// <param name="requests">Les demandes enregistrées.</param>
/// <param name="attempts">Le journal d'exécution.</param>
/// <param name="settings">Le Paramétrage, en lecture seule.</param>
/// <param name="hostSystem">Le système hôte.</param>
public sealed class ExecuteDataSubjectRequestHandler(
  IRepository<DataSubjectRequest> requests,
  IRepository<ExecutionAttempt> attempts,
  IReadRepository<Settings> settings,
  IHostSystem hostSystem)
  : ICommandHandler<ExecuteDataSubjectRequestCommand, Result<DataSubjectRequestExecution>>
{
  /// <inheritdoc />
  public async ValueTask<Result<DataSubjectRequestExecution>> Handle(
    ExecuteDataSubjectRequestCommand command,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var request = await requests.GetByIdAsync(command.DataSubjectRequest, cancellationToken);

    if (request is null)
    {
      return Result<DataSubjectRequestExecution>.NotFound();
    }

    var current = await ServiceSettings.ReadAsync(settings, cancellationToken);
    var endpoint = current.EndpointFor(request.Right);

    if (request.ExecutionBlockFacing(endpoint) is { } block)
    {
      return new RefusedExecution(
        new DataSubjectRequestExecution(RecordedDataSubjectRequest.Of(request, current), block, null),
        block.FrenchLabelFor(request.Right));
    }

    // Exécutable implique une adresse : le dernier motif est son absence.
    var called = endpoint!.Value;
    var call = await hostSystem.ApplyAsync(called, ExecutionBody.Of(request), CancellationToken.None);

    if (call.Outcome == ExecutionOutcome.Succeeded)
    {
      request.Complete();
    }

    await attempts.AddAsync(ExecutionAttempt.Of(request, called, call), CancellationToken.None);

    return new DataSubjectRequestExecution(RecordedDataSubjectRequest.Of(request, current), null, call);
  }

  /// <summary>
  /// Un refus <b>qui rend la demande</b> : <c>Conflict</c> pour une demande close, <c>Invalid</c> pour
  /// tout autre motif, le motif en français pour seule erreur.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Il existe parce que les fabriques de refus d'<c>Ardalis.Result</c> ne portent pas de
  /// valeur</b>, et que l'écran a besoin de la ligne à jour avec le refus. Seul le constructeur
  /// protégé de <c>Result&lt;T&gt;</c> pose les deux ensemble.
  /// </remarks>
  private sealed class RefusedExecution : Result<DataSubjectRequestExecution>
  {
    public RefusedExecution(DataSubjectRequestExecution execution, string reason)
      : base(execution.Refusal == ExecutionBlock.Closed ? ResultStatus.Conflict : ResultStatus.Invalid)
    {
      Value = execution;

      if (Status == ResultStatus.Conflict)
      {
        Errors = [reason];
      }
      else
      {
        ValidationErrors = [new ValidationError { ErrorMessage = reason }];
      }
    }
  }
}
