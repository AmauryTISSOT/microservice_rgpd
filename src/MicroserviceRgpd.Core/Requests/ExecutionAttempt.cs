using System.Globalization;
using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.Core.Requests;

/// <summary>
/// Une <b>tentative d'exécution</b> : une ligne du <b>journal d'exécution</b>, la trace qu'exécuter une
/// demande laisse à chaque remise partie vers le système hôte (ADR-0026, ADR-0028).
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
/// <b>L'exercice dit par où la remise est partie</b>, quel que soit le canal : l'adresse appelée, ou
/// l'exchange et la routing key en toutes lettres. Une seule colonne, aucune colonne discriminante —
/// une adresse est un exercice (ADR-0028).
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
    string exercise,
    HostSystemCall call,
    ExecutionOutcome outcome)
  {
    Id = ExecutionAttemptId.Next();
    DataSubjectRequestId = dataSubjectRequestId;
    Right = right;
    Exercise = exercise;
    StartedAt = call.StartedAt;
    Duration = call.Duration;
    Outcome = outcome;
    HttpStatus = call.StatusCode;
    CreatedBy = DataSubjectRequest.OperatorAuthor;
  }

  /// <summary>Le constructeur qu'EF Core emprunte pour rematérialiser une ligne. Il ne rejoue aucun invariant.</summary>
  private ExecutionAttempt()
  {
    Right = null!;
    Exercise = null!;
    Outcome = null!;
    CreatedBy = null!;
  }

  /// <summary>L'identité engendrée à l'écriture.</summary>
  public ExecutionAttemptId Id { get; private set; }

  /// <summary>La demande exécutée — un identifiant qui peut ne plus mener à rien, une fois la demande supprimée.</summary>
  public DataSubjectRequestId DataSubjectRequestId { get; private set; }

  /// <summary>Le droit dont l'application a été demandée.</summary>
  public DataSubjectRight Right { get; private set; }

  /// <summary>
  /// <b>Par où la remise est partie</b> : l'adresse appelée, <b>sans query string ni fragment</b>, ou
  /// l'exchange et la routing key en toutes lettres.
  /// </summary>
  public string Exercise { get; private set; }

  /// <summary>L'instant où l'appel est parti, en UTC.</summary>
  public DateTimeOffset StartedAt { get; private set; }

  /// <summary>Le temps qu'a pris l'appel.</summary>
  public TimeSpan Duration { get; private set; }

  /// <summary>Ce que la tentative a donné.</summary>
  public ExecutionOutcome Outcome { get; private set; }

  /// <summary>
  /// Le statut HTTP de la réponse, ou <c>null</c> quand le système hôte n'a pas répondu — et sur toute
  /// remise qui n'est pas un appel.
  /// </summary>
  public int? HttpStatus { get; private set; }

  /// <summary>Qui a exécuté la demande : toujours <see cref="DataSubjectRequest.OperatorAuthor"/>.</summary>
  public string CreatedBy { get; private set; }

  /// <summary>
  /// La tentative d'exécuter <paramref name="request"/> par <paramref name="channel"/>, telle que la
  /// remise <paramref name="call"/> l'a rendue.
  /// </summary>
  /// <exception cref="ArgumentNullException"><paramref name="request"/>, <paramref name="channel"/> ou <paramref name="call"/> est absent.</exception>
  /// <exception cref="ArgumentException"><paramref name="channel"/> n'est pas configuré.</exception>
  public static ExecutionAttempt Of(DataSubjectRequest request, ExerciseChannel channel, HostSystemCall call)
  {
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(channel);
    ArgumentNullException.ThrowIfNull(call);

    return new ExecutionAttempt(request.Id, request.Right, ExerciseOf(channel), call, call.Outcome);
  }

  /// <summary>
  /// La tentative d'une remise <b>réussie</b>, <paramref name="call"/>, dont la demande n'a pas pu
  /// passer à Terminée : le droit a été remis au système hôte, et le service ne l'a pas enregistré.
  /// Elle garde tout de la remise — son 2xx compris — sauf son résultat.
  /// </summary>
  /// <remarks>
  /// ⚠️ Elle s'écrit <b>dans une seconde transaction</b>, après l'échec de celle qui portait la
  /// tentative <see cref="ExecutionOutcome.Succeeded"/> et le passage à Terminée (ADR-0026).
  /// </remarks>
  /// <exception cref="ArgumentNullException"><paramref name="request"/>, <paramref name="channel"/> ou <paramref name="call"/> est absent.</exception>
  /// <exception cref="ArgumentException"><paramref name="call"/> n'a pas réussi, ou <paramref name="channel"/> n'est pas configuré.</exception>
  public static ExecutionAttempt SucceededButNotRecorded(DataSubjectRequest request, ExerciseChannel channel, HostSystemCall call)
  {
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(channel);
    ArgumentNullException.ThrowIfNull(call);

    if (call.Outcome != ExecutionOutcome.Succeeded)
    {
      throw new ArgumentException(
        $"Seule une remise réussie peut ne pas avoir été enregistrée ; celle-ci a donné {call.Outcome.Name}.",
        nameof(call));
    }

    return new ExecutionAttempt(
      request.Id,
      request.Right,
      ExerciseOf(channel),
      call,
      ExecutionOutcome.SucceededButNotRecorded);
  }

  /// <summary>
  /// Ce que le journal écrit du canal : l'adresse réduite à son schéma, son autorité et son chemin, ou
  /// le routage sous ses deux valeurs nommées.
  /// </summary>
  /// <exception cref="ArgumentException">Le canal n'est pas configuré : rien ne s'exerce, rien ne se journalise.</exception>
  private static string ExerciseOf(ExerciseChannel channel) => channel switch
  {
    ExerciseChannel.HttpEndpoint http => new Uri(http.Address.Value, UriKind.Absolute).GetLeftPart(UriPartial.Path),
    ExerciseChannel.RabbitMq rabbit => string.Create(
      CultureInfo.InvariantCulture,
      $"exchange {rabbit.Routing.Exchange.Value}, routing key {rabbit.Routing.RoutingKey.Value}"),

    // « Non configuré », le troisième et dernier cas : les motifs de blocage l'écartent avant toute remise.
    _ => throw new ArgumentException("Un droit non configuré ne s'exerce pas, et ne se journalise pas.", nameof(channel)),
  };
}
