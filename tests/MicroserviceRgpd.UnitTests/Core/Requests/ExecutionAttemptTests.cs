using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.UnitTests.Core.Requests;

/// <summary>
/// <b>Une tentative d'exécution</b> : une ligne du journal d'exécution, qui dit qu'un droit a été
/// demandé au système hôte, où, quand, en combien de temps et avec quel résultat — et rien de la
/// personne (ADR-0026).
/// </summary>
public class ExecutionAttemptTests
{
  private static readonly DateTimeOffset Now = new(2026, 9, 11, 8, 15, 0, TimeSpan.Zero);

  private static readonly DateTimeOffset StartedAt = new(2026, 9, 14, 10, 0, 0, TimeSpan.Zero);

  [Fact]
  public void CarriesTheRequestTheRightTheCallAndTheOperator()
  {
    var request = ARequest();
    var call = HostSystemCall.Answered(204, StartedAt, TimeSpan.FromMilliseconds(420));

    var attempt = ExecutionAttempt.Of(request, EndpointUrl.From("https://brocanto.example.fr/rgpd/effacement"), call);

    attempt.DataSubjectRequestId.ShouldBe(request.Id);
    attempt.Right.ShouldBe(DataSubjectRight.Erasure);
    attempt.CalledUrl.ShouldBe("https://brocanto.example.fr/rgpd/effacement");
    attempt.StartedAt.ShouldBe(StartedAt);
    attempt.Duration.ShouldBe(TimeSpan.FromMilliseconds(420));
    attempt.Outcome.ShouldBe(ExecutionOutcome.Succeeded);
    attempt.HttpStatus.ShouldBe(204);
    attempt.CreatedBy.ShouldBe(DataSubjectRequest.OperatorAuthor);
  }

  /// <summary>
  /// ⚠️ <b>L'adresse est journalisée sans query string ni fragment</b> : un jeton glissé dans l'URL
  /// n'est pas recopié à chaque tentative.
  /// </summary>
  [Theory]
  [InlineData("https://brocanto.example.fr/rgpd/effacement?token=secret", "https://brocanto.example.fr/rgpd/effacement")]
  [InlineData("https://brocanto.example.fr/rgpd/effacement#ancre", "https://brocanto.example.fr/rgpd/effacement")]
  [InlineData("http://localhost:8080/rights/erasure?status=503&delay_ms=10#x", "http://localhost:8080/rights/erasure")]
  [InlineData("https://brocanto.example.fr?token=secret", "https://brocanto.example.fr/")]
  public void DropsTheQueryStringAndTheFragmentOfTheCalledUrl(string endpoint, string logged)
  {
    var attempt = ExecutionAttempt.Of(ARequest(), EndpointUrl.From(endpoint), HostSystemCall.Unreachable(StartedAt, TimeSpan.Zero));

    attempt.CalledUrl.ShouldBe(logged);
  }

  [Fact]
  public void CarriesNoHttpStatusWhenTheHostDidNotAnswer()
  {
    var attempt = ExecutionAttempt.Of(
      ARequest(),
      EndpointUrl.From("https://brocanto.example.fr/rgpd"),
      HostSystemCall.TimedOut(StartedAt, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30)));

    attempt.Outcome.ShouldBe(ExecutionOutcome.TimedOut);
    attempt.HttpStatus.ShouldBeNull();
  }

  /// <summary>
  /// <b>Un succès non enregistré garde tout de l'appel</b> — son 2xx, son début, sa durée — sauf son
  /// résultat : le droit est appliqué, et le service ne l'a pas enregistré.
  /// </summary>
  [Fact]
  public void RecordsASuccessThatCouldNotBeRecordedWithTheCallItFollows()
  {
    var request = ARequest();
    var call = HostSystemCall.Answered(202, StartedAt, TimeSpan.FromMilliseconds(420));

    var attempt = ExecutionAttempt.SucceededButNotRecorded(
      request, EndpointUrl.From("https://brocanto.example.fr/rgpd/effacement?token=secret"), call);

    attempt.DataSubjectRequestId.ShouldBe(request.Id);
    attempt.Right.ShouldBe(DataSubjectRight.Erasure);
    attempt.CalledUrl.ShouldBe("https://brocanto.example.fr/rgpd/effacement");
    attempt.StartedAt.ShouldBe(StartedAt);
    attempt.Duration.ShouldBe(TimeSpan.FromMilliseconds(420));
    attempt.Outcome.ShouldBe(ExecutionOutcome.SucceededButNotRecorded);
    attempt.HttpStatus.ShouldBe(202);
  }

  /// <summary>⚠️ <b>Seul un appel réussi peut ne pas avoir été enregistré.</b></summary>
  [Fact]
  public void RefusesToRecordAFailedCallAsASuccessThatCouldNotBeRecorded()
  {
    Should.Throw<ArgumentException>(() => ExecutionAttempt.SucceededButNotRecorded(
      ARequest(),
      EndpointUrl.From("https://brocanto.example.fr/rgpd"),
      HostSystemCall.Answered(503, StartedAt, TimeSpan.Zero)));
  }

  /// <summary>
  /// ⚠️ <b>Aucune donnée personnelle</b> : ni l'email, ni le nom, ni le prénom, ni le message ne
  /// figurent sur la tentative — pas même sous une propriété qu'on aurait oublié d'afficher.
  /// </summary>
  [Fact]
  public void HoldsNoPersonalData()
  {
    typeof(ExecutionAttempt).GetProperties().Select(property => property.Name).ShouldBe(
      ["Id", "DataSubjectRequestId", "Right", "CalledUrl", "StartedAt", "Duration", "Outcome", "HttpStatus", "CreatedBy"],
      ignoreOrder: true);
  }

  [Fact]
  public void GivesEachAttemptItsOwnIdentity()
  {
    var request = ARequest();
    var endpoint = EndpointUrl.From("https://brocanto.example.fr/rgpd");
    var call = HostSystemCall.Answered(200, StartedAt, TimeSpan.Zero);

    ExecutionAttempt.Of(request, endpoint, call).Id.ShouldNotBe(ExecutionAttempt.Of(request, endpoint, call).Id);
  }

  private static DataSubjectRequest ARequest() =>
    DataSubjectRequest.Receive(
      new DataSubjectRequestEntry(
        Origin: Origin.Email,
        ReceivedOn: "2026-09-10",
        LastName: "Dupont",
        FirstName: "Jeanne",
        Email: "jeanne.dupont@exemple.fr",
        IdentityVerified: true,
        Message: "Merci d'effacer mon compte.",
        Right: nameof(DataSubjectRight.Erasure)),
      new DateOnly(2026, 9, 11),
      Now).Value;
}
