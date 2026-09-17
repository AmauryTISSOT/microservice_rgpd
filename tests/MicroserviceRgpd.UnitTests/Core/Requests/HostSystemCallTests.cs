using MicroserviceRgpd.Core.Requests;

namespace MicroserviceRgpd.UnitTests.Core.Requests;

/// <summary>
/// <b>Ce que rend une remise au système hôte</b> : une réponse 2xx, une réponse non 2xx avec son code,
/// un accusé du broker, un message non routable, une publication refusée, un délai dépassé ou une
/// erreur réseau — avec l'instant de début et la durée (ADR-0026, ADR-0028).
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

  /// <summary>
  /// <b>Un délai dépassé porte le délai qui a couru</b> — l'<c>Operator</c> lit « dans les 30 secondes »
  /// —, et aucun statut.
  /// </summary>
  [Fact]
  public void CarriesTheTimeoutAndNoStatusCodeWhenTheHostDidNotAnswerInTime()
  {
    var call = HostSystemCall.TimedOut(StartedAt, Duration, TimeSpan.FromSeconds(30));

    call.Outcome.ShouldBe(ExecutionOutcome.TimedOut);
    call.StatusCode.ShouldBeNull();
    call.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
  }

  /// <summary>
  /// <b>Un accusé du broker vaut aboutissement</b>, et <b>sans statut HTTP</b> : une publication n'a
  /// pas de code de réponse (ADR-0028).
  /// </summary>
  [Fact]
  public void ReadsAnAcknowledgementAsASuccessWithoutAnyHttpStatus()
  {
    var call = HostSystemCall.Acknowledged(StartedAt, Duration);

    call.Outcome.ShouldBe(ExecutionOutcome.Succeeded);
    call.StatusCode.ShouldBeNull();
    call.Timeout.ShouldBeNull();
    call.Duration.ShouldBe(Duration);
  }

  /// <summary>
  /// <b>Les deux cas que seul un bus produit</b> : un message qu'aucune file n'a reçu, et une
  /// publication refusée. Ni l'un ni l'autre ne porte de statut HTTP.
  /// </summary>
  [Fact]
  public void ReadsTheTwoOutcomesThatOnlyABusProduces()
  {
    var unroutable = HostSystemCall.Unroutable(StartedAt, Duration);
    var rejected = HostSystemCall.Rejected(StartedAt, Duration);

    unroutable.Outcome.ShouldBe(ExecutionOutcome.Unroutable);
    rejected.Outcome.ShouldBe(ExecutionOutcome.Rejected);

    unroutable.StatusCode.ShouldBeNull();
    rejected.StatusCode.ShouldBeNull();
    unroutable.Timeout.ShouldBeNull();
    rejected.Timeout.ShouldBeNull();
  }

  [Fact]
  public void CarriesNoTimeoutWhenTheHostAnsweredOrWasUnreachable()
  {
    HostSystemCall.Answered(503, StartedAt, Duration).Timeout.ShouldBeNull();
    HostSystemCall.Unreachable(StartedAt, Duration).Timeout.ShouldBeNull();
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
    Should.Throw<ArgumentOutOfRangeException>(() => HostSystemCall.TimedOut(StartedAt, TimeSpan.FromTicks(-1), TimeSpan.FromSeconds(30)));
  }
}
