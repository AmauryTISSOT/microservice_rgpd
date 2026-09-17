namespace MicroserviceRgpd.Core.Requests;

/// <summary>
/// <b>Ce que rend une remise au système hôte</b> — un appel HTTP ou une publication sur un bus : une
/// réponse — 2xx ou non, avec son code —, un délai dépassé ou une erreur réseau, avec l'instant de
/// début et la durée de la remise (ADR-0026, ADR-0028).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Tout 2xx vaut « le droit a été remis »</b>, <c>202 Accepted</c> compris ; tout autre code
/// — une redirection comprise, qui n'est pas suivie — est une réponse non 2xx. La règle n'est écrite
/// qu'ici : l'adaptateur rend le code, il ne le juge pas. <b>Le code reste nul</b> sur toute remise
/// qui n'est pas un appel HTTP.
/// </para>
/// <para>
/// Il ne se construit que par ses trois fabriques : une remise ne rend jamais
/// <see cref="ExecutionOutcome.SucceededButNotRecorded"/>, qui dit ce que le service a fait
/// <i>après</i> la remise.
/// </para>
/// </remarks>
public sealed record HostSystemCall
{
  private HostSystemCall(
    ExecutionOutcome outcome,
    int? statusCode,
    DateTimeOffset startedAt,
    TimeSpan duration,
    TimeSpan? timeout = null)
  {
    ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero);

    Outcome = outcome;
    StatusCode = statusCode;
    StartedAt = startedAt.ToUniversalTime();
    Duration = duration;
    Timeout = timeout;
  }

  /// <summary>Ce que la remise a donné.</summary>
  public ExecutionOutcome Outcome { get; }

  /// <summary>Le statut HTTP de la réponse, ou <c>null</c> quand le système hôte n'a pas répondu.</summary>
  public int? StatusCode { get; }

  /// <summary>L'instant où la remise est partie, en UTC.</summary>
  public DateTimeOffset StartedAt { get; }

  /// <summary>Le temps qu'a pris la remise, jusqu'à la réponse ou à l'échec.</summary>
  public TimeSpan Duration { get; }

  /// <summary>
  /// Le délai qui a couru, quand le système hôte n'a pas répondu à temps — <c>null</c> sinon.
  /// L'<c>Operator</c> le lit : « dans les 30 secondes ».
  /// </summary>
  public TimeSpan? Timeout { get; }

  /// <summary>Le système hôte a répondu <paramref name="statusCode"/>.</summary>
  public static HostSystemCall Answered(int statusCode, DateTimeOffset startedAt, TimeSpan duration) =>
    new(
      statusCode is >= 200 and < 300 ? ExecutionOutcome.Succeeded : ExecutionOutcome.NonSuccessResponse,
      statusCode,
      startedAt,
      duration);

  /// <summary>Le système hôte n'a pas répondu dans le délai <paramref name="timeout"/>.</summary>
  public static HostSystemCall TimedOut(DateTimeOffset startedAt, TimeSpan duration, TimeSpan timeout) =>
    new(ExecutionOutcome.TimedOut, null, startedAt, duration, timeout);

  /// <summary>Le système hôte n'a pas pu être joint.</summary>
  public static HostSystemCall Unreachable(DateTimeOffset startedAt, TimeSpan duration) =>
    new(ExecutionOutcome.NetworkError, null, startedAt, duration);
}
