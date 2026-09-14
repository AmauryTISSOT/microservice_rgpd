namespace MicroserviceRgpd.Core.Requests;

/// <summary>
/// <b>Ce que rend un appel au système hôte</b> : une réponse — 2xx ou non, avec son code —, un délai
/// dépassé ou une erreur réseau, avec l'instant de début et la durée de l'appel (ADR-0026).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Tout 2xx vaut « le droit a été appliqué »</b>, <c>202 Accepted</c> compris ; tout autre code
/// — une redirection comprise, qui n'est pas suivie — est une réponse non 2xx. La règle n'est écrite
/// qu'ici : l'adaptateur rend le code, il ne le juge pas.
/// </para>
/// <para>
/// Il ne se construit que par ses trois fabriques : un appel ne rend jamais
/// <see cref="ExecutionOutcome.SucceededButNotRecorded"/>, qui dit ce que le service a fait
/// <i>après</i> l'appel.
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

  /// <summary>Ce que l'appel a donné.</summary>
  public ExecutionOutcome Outcome { get; }

  /// <summary>Le statut HTTP de la réponse, ou <c>null</c> quand le système hôte n'a pas répondu.</summary>
  public int? StatusCode { get; }

  /// <summary>L'instant où l'appel est parti, en UTC.</summary>
  public DateTimeOffset StartedAt { get; }

  /// <summary>Le temps qu'a pris l'appel, jusqu'à la réponse ou à l'échec.</summary>
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
