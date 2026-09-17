using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.UnitTests.Core.Requests;

/// <summary>
/// <b>Une tentative d'exécution</b> : une ligne du journal d'exécution, qui dit qu'un droit a été
/// demandé au système hôte, <b>par où</b>, quand, en combien de temps et avec quel résultat — et rien
/// de la personne (ADR-0026, ADR-0028).
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

    var attempt = ExecutionAttempt.Of(request, AnAddress("https://brocanto.example.fr/rgpd/effacement"), call);

    attempt.DataSubjectRequestId.ShouldBe(request.Id);
    attempt.Right.ShouldBe(DataSubjectRight.Erasure);
    attempt.Exercise.ShouldBe("https://brocanto.example.fr/rgpd/effacement");
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
  public void DropsTheQueryStringAndTheFragmentOfTheExercisedAddress(string endpoint, string logged)
  {
    var attempt = ExecutionAttempt.Of(ARequest(), AnAddress(endpoint), HostSystemCall.Unreachable(StartedAt, TimeSpan.Zero));

    attempt.Exercise.ShouldBe(logged);
  }

  [Fact]
  public void CarriesNoHttpStatusWhenTheHostDidNotAnswer()
  {
    var attempt = ExecutionAttempt.Of(
      ARequest(),
      AnAddress("https://brocanto.example.fr/rgpd"),
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
      request, AnAddress("https://brocanto.example.fr/rgpd/effacement?token=secret"), call);

    attempt.DataSubjectRequestId.ShouldBe(request.Id);
    attempt.Right.ShouldBe(DataSubjectRight.Erasure);
    attempt.Exercise.ShouldBe("https://brocanto.example.fr/rgpd/effacement");
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
      AnAddress("https://brocanto.example.fr/rgpd"),
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
      ["Id", "DataSubjectRequestId", "Right", "Exercise", "StartedAt", "Duration", "Outcome", "HttpStatus", "CreatedBy"],
      ignoreOrder: true);
  }

  [Fact]
  public void GivesEachAttemptItsOwnIdentity()
  {
    var request = ARequest();
    var channel = AnAddress("https://brocanto.example.fr/rgpd");
    var call = HostSystemCall.Answered(200, StartedAt, TimeSpan.Zero);

    ExecutionAttempt.Of(request, channel, call).Id.ShouldNotBe(ExecutionAttempt.Of(request, channel, call).Id);
  }

  /// <summary><b>Un routage s'écrit en toutes lettres</b> : ses deux valeurs, nommées (ADR-0028).</summary>
  [Fact]
  public void WritesARabbitMqRoutingInFullWords()
  {
    var attempt = ExecutionAttempt.Of(
      ARequest(),
      new ExerciseChannel.RabbitMq(new RabbitMqRouting(ExchangeName.From("rgpd.rights"), RoutingKey.From("rights.erasure"))),
      HostSystemCall.Unreachable(StartedAt, TimeSpan.FromMilliseconds(12)));

    attempt.Exercise.ShouldBe("exchange rgpd.rights, routing key rights.erasure");
  }

  /// <summary>
  /// <b>Une publication laisse une ligne sans statut HTTP</b>, dont l'exercice dit l'exchange et la
  /// routing key, et dont la durée court jusqu'à l'accusé du broker (ADR-0028).
  /// </summary>
  [Theory]
  [InlineData("Succeeded")]
  [InlineData("Unroutable")]
  [InlineData("Rejected")]
  public void WritesAPublicationWithItsRoutingItsDurationAndNoHttpStatus(string outcomeName)
  {
    var outcome = ExecutionOutcome.FromName(outcomeName);
    var duration = TimeSpan.FromMilliseconds(37);

    var call = outcome == ExecutionOutcome.Succeeded ? HostSystemCall.Acknowledged(StartedAt, duration)
      : outcome == ExecutionOutcome.Unroutable ? HostSystemCall.Unroutable(StartedAt, duration)
      : HostSystemCall.Rejected(StartedAt, duration);

    var attempt = ExecutionAttempt.Of(ARequest(), ARouting(), call);

    attempt.Exercise.ShouldBe("exchange rgpd.rights, routing key rights.erasure");
    attempt.Outcome.ShouldBe(outcome);
    attempt.HttpStatus.ShouldBeNull("Une publication ne porte pas de statut HTTP.");
    attempt.Duration.ShouldBe(duration);
    attempt.Duration.ShouldNotBe(TimeSpan.Zero, "Une publication ne dure pas zéro au journal.");
  }

  /// <summary>
  /// Un succès non enregistré dit lui aussi le routage par lequel la remise est partie — et reste
  /// sans statut HTTP, comme la publication qu'il suit.
  /// </summary>
  [Fact]
  public void WritesTheRoutingOfASuccessThatCouldNotBeRecorded()
  {
    var attempt = ExecutionAttempt.SucceededButNotRecorded(
      ARequest(), ARouting(), HostSystemCall.Acknowledged(StartedAt, TimeSpan.FromMilliseconds(12)));

    attempt.Exercise.ShouldBe("exchange rgpd.rights, routing key rights.erasure");
    attempt.Outcome.ShouldBe(ExecutionOutcome.SucceededButNotRecorded);
    attempt.HttpStatus.ShouldBeNull();
  }

  /// <summary>
  /// ⚠️ <b>Un droit « non configuré » ne s'exerce pas</b> : les motifs de blocage l'écartent bien avant
  /// la remise, et aucune tentative ne s'écrit sur un canal qui n'en est pas un.
  /// </summary>
  [Fact]
  public void RefusesToRecordAnAttemptOnARightThatIsNotConfigured()
  {
    Should.Throw<ArgumentException>(() => ExecutionAttempt.Of(
      ARequest(),
      ExerciseChannel.NotConfigured.Instance,
      HostSystemCall.Answered(204, StartedAt, TimeSpan.Zero)));
  }

  /// <summary>Le routage RabbitMQ du Paramétrage, tel que le handler le lit.</summary>
  private static ExerciseChannel ARouting() =>
    new ExerciseChannel.RabbitMq(new RabbitMqRouting(ExchangeName.From("rgpd.rights"), RoutingKey.From("rights.erasure")));

  /// <summary>Le canal HTTP du Paramétrage, tel que le handler le lit.</summary>
  private static ExerciseChannel AnAddress(string address) =>
    new ExerciseChannel.HttpEndpoint(EndpointUrl.From(address));

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
