using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.Core.Requests;

/// <summary>
/// Une <b>tentative d'exécution</b> : une ligne du <b>journal d'exécution</b>, la trace qu'exécuter une
/// demande laisse à chaque appel parti vers le système hôte (ADR-0026).
/// </summary>
/// <remarks>
/// <para>
/// <b>Un agrégat à part, et non une entité de la demande.</b> Il référence la demande par son
/// <see cref="DataSubjectRequestId"/>, <b>sans clé étrangère</b> : supprimer la demande retire toutes
/// ses données (ADR-0022) mais laisse ses tentatives, qui gardent un identifiant ne menant plus à
/// personne et prouvent qu'un droit a été demandé au système hôte.
/// </para>
/// <para>
/// ⚠️ <b>Aucune donnée personnelle, aucun corps de réponse</b> : ni email, ni nom, ni prénom, ni
/// message. Le journal ne doit pas devenir un second fichier de personnes.
/// </para>
/// <para>
/// ⚠️ <b>L'adresse est journalisée sans query string ni fragment</b> : un jeton glissé dans l'URL part
/// au système hôte, mais n'est pas recopié à chaque tentative.
/// </para>
/// </remarks>
public sealed class ExecutionAttempt : IAggregateRoot
{
  private ExecutionAttempt(
    DataSubjectRequestId dataSubjectRequestId,
    DataSubjectRight right,
    string calledUrl,
    HostSystemCall call)
  {
    Id = ExecutionAttemptId.Next();
    DataSubjectRequestId = dataSubjectRequestId;
    Right = right;
    CalledUrl = calledUrl;
    StartedAt = call.StartedAt;
    Duration = call.Duration;
    Outcome = call.Outcome;
    HttpStatus = call.StatusCode;
    CreatedBy = DataSubjectRequest.OperatorAuthor;
  }

  /// <summary>Le constructeur qu'EF Core emprunte pour rematérialiser une ligne. Il ne rejoue aucun invariant.</summary>
  private ExecutionAttempt()
  {
    Right = null!;
    CalledUrl = null!;
    Outcome = null!;
    CreatedBy = null!;
  }

  /// <summary>L'identité engendrée à l'écriture.</summary>
  public ExecutionAttemptId Id { get; private set; }

  /// <summary>La demande exécutée — un identifiant qui peut ne plus mener à rien, une fois la demande supprimée.</summary>
  public DataSubjectRequestId DataSubjectRequestId { get; private set; }

  /// <summary>Le droit dont l'application a été demandée.</summary>
  public DataSubjectRight Right { get; private set; }

  /// <summary>L'adresse appelée, <b>sans query string ni fragment</b>.</summary>
  public string CalledUrl { get; private set; }

  /// <summary>L'instant où l'appel est parti, en UTC.</summary>
  public DateTimeOffset StartedAt { get; private set; }

  /// <summary>Le temps qu'a pris l'appel.</summary>
  public TimeSpan Duration { get; private set; }

  /// <summary>Ce que la tentative a donné.</summary>
  public ExecutionOutcome Outcome { get; private set; }

  /// <summary>Le statut HTTP de la réponse, ou <c>null</c> quand le système hôte n'a pas répondu.</summary>
  public int? HttpStatus { get; private set; }

  /// <summary>Qui a exécuté la demande : toujours <see cref="DataSubjectRequest.OperatorAuthor"/>.</summary>
  public string CreatedBy { get; private set; }

  /// <summary>
  /// La tentative d'exécuter <paramref name="request"/> à <paramref name="endpoint"/>, telle que
  /// l'appel <paramref name="call"/> l'a rendue.
  /// </summary>
  /// <exception cref="ArgumentNullException"><paramref name="request"/> ou <paramref name="call"/> est absent.</exception>
  public static ExecutionAttempt Of(DataSubjectRequest request, EndpointUrl endpoint, HostSystemCall call)
  {
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(call);

    return new ExecutionAttempt(request.Id, request.Right, WithoutQueryNorFragment(endpoint), call);
  }

  /// <summary>L'adresse réduite à son schéma, son autorité et son chemin.</summary>
  private static string WithoutQueryNorFragment(EndpointUrl endpoint) =>
    new Uri(endpoint.Value, UriKind.Absolute).GetLeftPart(UriPartial.Path);
}
