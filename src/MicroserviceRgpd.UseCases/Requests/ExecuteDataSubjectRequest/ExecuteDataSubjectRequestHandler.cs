using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.UseCases.Requests.ReadDataSubjectRequests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MicroserviceRgpd.UseCases.Requests.ExecuteDataSubjectRequest;

/// <summary>
/// Recalcule l'exécutabilité de la demande face au Paramétrage, appelle le système hôte, termine la
/// demande sur un 2xx et écrit la tentative — ou rend « introuvable », ou le refus du premier motif de
/// blocage, sans appel ni tentative (ADR-0026).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Le serveur fait foi, juste avant l'appel.</b> L'écran a pu montrer un bouton actif sur une
/// demande qu'un autre onglet a close, ou dont le canal a été retiré : les conditions sont relues
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
/// ⚠️ <b>Un appel qui n'aboutit pas est un échec rendu, pas une exception</b> : la tentative s'écrit
/// seule, la demande reste En cours, et le résultat est une <c>Error</c> qui porte la demande, le
/// résultat typé et le texte que l'<c>Operator</c> lira.
/// </para>
/// <para>
/// ⚠️ <b>Succès non enregistré.</b> Si la transaction qui porte le passage à Terminée échoue après un
/// 2xx, le contexte partagé garde les modifications refusées : une <b>seconde transaction</b>, ouverte
/// dans une portée neuve, écrit la tentative <see cref="ExecutionOutcome.SucceededButNotRecorded"/>,
/// et l'échec est journalisé en erreur. La demande rendue est celle d'avant l'appel — celle que la base
/// porte encore.
/// </para>
/// <para>
/// Aucune nouvelle tentative : un appel, une tentative écrite.
/// </para>
/// </remarks>
/// <param name="requests">Les demandes enregistrées.</param>
/// <param name="attempts">Le journal d'exécution.</param>
/// <param name="settings">Le Paramétrage, en lecture seule.</param>
/// <param name="hostSystem">Le système hôte.</param>
/// <param name="scopes">De quoi ouvrir la portée de la seconde transaction, après un succès non enregistré.</param>
/// <param name="logger">Les logs applicatifs, où un succès non enregistré s'écrit en erreur.</param>
public sealed class ExecuteDataSubjectRequestHandler(
  IRepository<DataSubjectRequest> requests,
  IRepository<ExecutionAttempt> attempts,
  IReadRepository<Settings> settings,
  IHostSystem hostSystem,
  IServiceScopeFactory scopes,
  ILogger<ExecuteDataSubjectRequestHandler> logger)
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
    var channel = current.ChannelFor(request.Right);

    if (request.ExecutionBlockFacing(channel) is { } block)
    {
      return new BlockedExecution(
        new DataSubjectRequestExecution(RecordedDataSubjectRequest.Of(request, current), block, null, null),
        block.FrenchLabelFor(request.Right));
    }

    // Exécutable implique une adresse HTTP : les deux derniers motifs disent tout autre canal.
    var called = ((ExerciseChannel.HttpEndpoint)channel).Address;

    // La demande telle que la base la porte avant l'appel : celle que rend tout échec.
    var unchanged = RecordedDataSubjectRequest.Of(request, current);

    var call = await hostSystem.ApplyAsync(called, ExecutionBody.Of(request), CancellationToken.None);

    if (call.Outcome != ExecutionOutcome.Succeeded)
    {
      await attempts.AddAsync(ExecutionAttempt.Of(request, called, call), CancellationToken.None);

      return new FailedExecution(new DataSubjectRequestExecution(unchanged, null, call, call.Outcome), FailureOf(call));
    }

    request.Complete();

    try
    {
      await attempts.AddAsync(ExecutionAttempt.Of(request, called, call), CancellationToken.None);
    }
    catch (Exception notRecorded)
    {
      // Rattrapage large assumé : quelle que soit la raison du refus, le droit est appliqué, et le
      // journal d'exécution doit le dire. L'annulation n'est pas à craindre : aucun jeton annulable n'est passé.
      logger.LogError(
        notRecorded,
        "Le système hôte a appliqué le droit {Right} pour la demande {DataSubjectRequestId} (HTTP {HttpStatus}), mais la demande n'a pas pu passer à Terminée.",
        request.Right.Name,
        request.Id.Value,
        call.StatusCode);

      await using var scope = scopes.CreateAsyncScope();

      // ⚠️ Si cette seconde écriture échoue aussi, l'exception remonte : l'erreur est déjà dans les
      // logs, et aucune troisième tentative d'écrire n'est faite.

      await scope.ServiceProvider.GetRequiredService<IRepository<ExecutionAttempt>>()
        .AddAsync(ExecutionAttempt.SucceededButNotRecorded(request, called, call), CancellationToken.None);

      return new FailedExecution(
        new DataSubjectRequestExecution(unchanged, null, call, ExecutionOutcome.SucceededButNotRecorded),
        ExecutionFailure.SucceededButNotRecorded);
    }

    return new DataSubjectRequestExecution(RecordedDataSubjectRequest.Of(request, current), null, call, call.Outcome);
  }

  /// <summary>Ce que l'<c>Operator</c> lit d'un appel qui n'a pas abouti.</summary>
  private static string FailureOf(HostSystemCall call) =>
    call.Outcome == ExecutionOutcome.TimedOut
      ? ExecutionFailure.TimedOut((int)call.Timeout!.Value.TotalSeconds)
      : call.Outcome == ExecutionOutcome.NetworkError
        ? ExecutionFailure.Unreachable
        : ExecutionFailure.NonSuccessResponse(call.StatusCode!.Value);

  /// <summary>
  /// Un échec <b>qui rend la demande</b> : <c>Error</c>, le texte destiné à l'<c>Operator</c> pour seule
  /// erreur — pour la même raison que <see cref="BlockedExecution"/>.
  /// </summary>
  private sealed class FailedExecution : Result<DataSubjectRequestExecution>
  {
    public FailedExecution(DataSubjectRequestExecution execution, string message)
      : base(ResultStatus.Error)
    {
      Value = execution;
      Errors = [message];
    }
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
  private sealed class BlockedExecution : Result<DataSubjectRequestExecution>
  {
    public BlockedExecution(DataSubjectRequestExecution execution, string reason)
      : base(execution.Block == ExecutionBlock.Closed ? ResultStatus.Conflict : ResultStatus.Invalid)
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
