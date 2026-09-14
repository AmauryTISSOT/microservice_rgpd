using MicroserviceRgpd.Core.Requests;

namespace MicroserviceRgpd.UnitTests.Core.Requests;

/// <summary>
/// <b>Ce que rend un appel au système hôte</b> : une réponse 2xx, une réponse non 2xx avec son code,
/// un délai dépassé ou une erreur réseau — avec l'instant de début et la durée (ADR-0026).
/// </summary>
public class HostSystemCallTests
{
  private static readonly DateTimeOffset StartedAt = new(2026, 9, 14, 10, 0, 0, TimeSpan.FromHours(2));

  private static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(420);

  /// <summary><b>Tout 2xx vaut « le droit a été appliqué »</b>, <c>202 Accepted</c> compris.</summary>
  [Theory]
  [InlineData(200)]
  [InlineData(202)]
  [InlineData(204)]
  [InlineData(299)]
  public void ReadsEvery2xxAsASuccess(int statusCode)
  {
    var call = HostSystemCall.Answered(statusCode, StartedAt, Duration);

    call.Outcome.ShouldBe(ExecutionOutcome.Succeeded);
    call.StatusCode.ShouldBe(statusCode);
  }

  /// <summary>⚠️ <b>Un 3xx n'est pas suivi</b> : c'est une réponse non 2xx, comme un 4xx ou un 5xx.</summary>
  [Theory]
  [InlineData(199)]
  [InlineData(302)]
  [InlineData(404)]
  [InlineData(503)]
  public void ReadsAnyOtherStatusAsANonSuccessResponse(int statusCode)
  {
    var call = HostSystemCall.Answered(statusCode, StartedAt, Duration);

    call.Outcome.ShouldBe(ExecutionOutcome.NonSuccessResponse);
    call.StatusCode.ShouldBe(statusCode);
  }

  [Fact]
  public void CarriesNoStatusCodeWhenTheHostDidNotAnswerInTime()
  {
    var call = HostSystemCall.TimedOut(StartedAt, Duration);

    call.Outcome.ShouldBe(ExecutionOutcome.TimedOut);
    call.StatusCode.ShouldBeNull();
  }

  [Fact]
  public void CarriesNoStatusCodeWhenTheHostWasUnreachable()
  {
    var call = HostSystemCall.Unreachable(StartedAt, Duration);

    call.Outcome.ShouldBe(ExecutionOutcome.NetworkError);
    call.StatusCode.ShouldBeNull();
  }

  /// <summary>L'instant de début se tient en UTC, la durée telle qu'elle a été mesurée.</summary>
  [Fact]
  public void HoldsTheStartInUtcAndTheDuration()
  {
    var call = HostSystemCall.Answered(200, StartedAt, Duration);

    call.StartedAt.Offset.ShouldBe(TimeSpan.Zero);
    call.StartedAt.ShouldBe(StartedAt);
    call.Duration.ShouldBe(Duration);
  }

  [Fact]
  public void RefusesANegativeDuration()
  {
    Should.Throw<ArgumentOutOfRangeException>(() => HostSystemCall.TimedOut(StartedAt, TimeSpan.FromTicks(-1)));
  }
}
