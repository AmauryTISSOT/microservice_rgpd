using Ardalis.Result;
using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.UnitTests.Infrastructure.Screenings;
using MicroserviceRgpd.UseCases.Requests.ExecuteDataSubjectRequest;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute.ExceptionExtensions;

namespace MicroserviceRgpd.UnitTests.UseCases.Requests.ExecuteDataSubjectRequest;

/// <summary>
/// Ce que le use case d'exécution ajoute au domaine : il retrouve la demande et le Paramétrage,
/// <b>recalcule l'exécutabilité juste avant l'appel</b>, appelle le système hôte, termine la demande
/// sur un 2xx et écrit la tentative avec elle — et rend la demande dans tous les cas (ADR-0026).
/// </summary>
/// <remarks>
/// Les règles — l'ordre des motifs, le passage à Terminée, ce que la tentative retient — se vérifient
/// sur l'agrégat. Ce qui se vérifie ici est l'orchestration.
/// </remarks>
public class ExecuteDataSubjectRequestHandlerTests
{
  private static readonly DateTimeOffset Now = new(2026, 9, 11, 8, 15, 0, TimeSpan.Zero);

  private static readonly EndpointUrl Endpoint = EndpointUrl.From("https://brocanto.example.fr/rgpd/acces?jeton=secret");

  private readonly IRepository<DataSubjectRequest> _requests = Substitute.For<IRepository<DataSubjectRequest>>();

  private readonly IRepository<ExecutionAttempt> _attempts = Substitute.For<IRepository<ExecutionAttempt>>();

  private readonly IReadRepository<Settings> _settings = Substitute.For<IReadRepository<Settings>>();

  private readonly IHostSystem _hostSystem = Substitute.For<IHostSystem>();

  /// <summary>Le journal d'exécution tel que la seconde transaction le voit, dans sa portée neuve.</summary>
  private readonly IRepository<ExecutionAttempt> _recovery = Substitute.For<IRepository<ExecutionAttempt>>();

  private readonly IServiceScopeFactory _scopes = Substitute.For<IServiceScopeFactory>();

  private readonly RecordedLogs _logs = new();

  public ExecuteDataSubjectRequestHandlerTests()
  {
    var settings = Settings.Unconfigured();
    settings.SetEndpoint(DataSubjectRight.Access, Endpoint);
    _settings.ListAsync(Arg.Any<CancellationToken>()).Returns([settings]);

    var scope = Substitute.For<IServiceScope>();
    scope.ServiceProvider.GetService(typeof(IRepository<ExecutionAttempt>)).Returns(_recovery);
    _scopes.CreateScope().Returns(scope);

    HostAnswers(204);
  }

  /// <summary>
  /// <b>Sur un 2xx, la demande passe à Terminée</b>, et la tentative <c>Succeeded</c> est ajoutée :
  /// une seule écriture, qui enregistre les deux ensemble.
  /// </summary>
  [Fact]
  public async Task CompletesTheRequestAndAddsASucceededAttemptOnA2xx()
  {
    var request = AnExecutableRequest();

    var result = await HandleAsync(request);

    result.IsSuccess.ShouldBeTrue();
    request.Status.ShouldBe(RequestStatus.Completed);

    await _attempts.Received(1).AddAsync(
      Arg.Is<ExecutionAttempt>(attempt =>
        attempt.DataSubjectRequestId == request.Id
        && attempt.Outcome == ExecutionOutcome.Succeeded
        && attempt.HttpStatus == 204
        && attempt.CalledUrl == "https://brocanto.example.fr/rgpd/acces"),
      Arg.Any<CancellationToken>());

    // ⚠️ La demande n'est pas enregistrée à part : c'est l'ajout de la tentative qui l'emporte, dans
    // la même transaction.
    await _requests.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
  }

  /// <summary><b>La demande rendue est Terminée</b>, et s'exécute désormais sous le motif « Demande close ».</summary>
  [Fact]
  public async Task RendersTheCompletedRequestAndTheCall()
  {
    var request = AnExecutableRequest();

    var result = await HandleAsync(request);

    result.Value.Request.Id.ShouldBe(request.Id);
    result.Value.Request.Status.ShouldBe(RequestStatus.Completed);
    result.Value.Request.ExecutionBlock.ShouldBe(ExecutionBlock.Closed);
    result.Value.Block.ShouldBeNull();
    result.Value.Call.ShouldNotBeNull().Outcome.ShouldBe(ExecutionOutcome.Succeeded);
    result.Value.Outcome.ShouldBe(ExecutionOutcome.Succeeded);
  }

  /// <summary>
  /// <b>Le système hôte reçoit l'adresse du Paramétrage telle quelle</b> — query string comprise — et
  /// les cinq valeurs de la demande.
  /// </summary>
  [Fact]
  public async Task CallsTheHostAtTheEndpointOfTheRightWithTheFiveValues()
  {
    var request = AnExecutableRequest();

    await HandleAsync(request);

    await _hostSystem.Received(1).ApplyAsync(
      Endpoint,
      new ExecutionBody(request.Id, DataSubjectRight.Access, request.Email!.Value, request.FirstName, request.LastName),
      Arg.Any<CancellationToken>());
  }

  /// <summary>
  /// ⚠️ <b>L'appel va à son terme même si le navigateur s'en va</b> : l'annulation de la requête
  /// entrante n'est pas propagée à l'appel, ni à l'écriture de sa tentative.
  /// </summary>
  [Fact]
  public async Task DoesNotPropagateTheCancellationOfTheIncomingRequest()
  {
    var request = AnExecutableRequest();
    _requests.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
    using var browserLeft = new CancellationTokenSource();

    await Handler().Handle(new ExecuteDataSubjectRequestCommand(request.Id), browserLeft.Token);

    // ⚠️ Lu sur l'appel reçu plutôt que par Arg.Any : l'adresse est un objet valeur que NSubstitute ne
    // sait pas construire par défaut.
    _hostSystem.ReceivedCalls().ShouldHaveSingleItem().GetArguments()[2]
      .ShouldBeOfType<CancellationToken>().CanBeCanceled.ShouldBeFalse();
    await _attempts.Received(1).AddAsync(
      Arg.Any<ExecutionAttempt>(), Arg.Is<CancellationToken>(token => !token.CanBeCanceled));
  }

  /// <summary>
  /// <b>Une demande close est refusée en conflit</b>, sans appel ni tentative — et rendue, pour que
  /// l'écran remette la ligne à jour.
  /// </summary>
  [Fact]
  public async Task RefusesAClosedRequestInConflictWithoutCallingNorLogging()
  {
    var request = AnExecutableRequest();
    request.Complete();

    var result = await HandleAsync(request);

    result.Status.ShouldBe(ResultStatus.Conflict);
    result.Errors.ShouldBe([ExecutionBlock.Closed.FrenchLabelFor(DataSubjectRight.Access)]);
    result.Value.Block.ShouldBe(ExecutionBlock.Closed);
    result.Value.Request.Id.ShouldBe(request.Id);
    result.Value.Call.ShouldBeNull();
    result.Value.Outcome.ShouldBeNull();
    await NothingWasCalledNorWrittenAsync();
  }

  /// <summary>
  /// <b>Tout autre motif est un refus de la demande telle qu'elle est</b> — <c>Invalid</c> —, sans
  /// appel ni tentative, et la demande est rendue.
  /// </summary>
  [Theory]
  [InlineData(nameof(ExecutionBlock.IdentityNotVerified))]
  [InlineData(nameof(ExecutionBlock.EmailMissing))]
  [InlineData(nameof(ExecutionBlock.NoEndpoint))]
  public async Task RefusesEveryOtherBlockAsInvalidWithoutCallingNorLogging(string blockName)
  {
    var block = ExecutionBlock.FromName(blockName);

    if (block == ExecutionBlock.NoEndpoint)
    {
      _settings.ListAsync(Arg.Any<CancellationToken>()).Returns([]);
    }

    var request = ARequest(
      verified: block != ExecutionBlock.IdentityNotVerified,
      email: block == ExecutionBlock.EmailMissing ? null : "jeanne.dupont@exemple.fr");

    var result = await HandleAsync(request);

    result.Status.ShouldBe(ResultStatus.Invalid);
    result.ValidationErrors.Select(error => error.ErrorMessage).ShouldBe([block.FrenchLabelFor(DataSubjectRight.Access)]);
    result.Value.Block.ShouldBe(block);
    result.Value.Request.Id.ShouldBe(request.Id);
    request.Status.ShouldBe(RequestStatus.InProgress);
    await NothingWasCalledNorWrittenAsync();
  }

  /// <summary><b>Une demande disparue rend « introuvable »</b>, sans appel ni tentative.</summary>
  [Fact]
  public async Task RendersNotFoundWhenTheRequestIsGone()
  {
    var identity = DataSubjectRequestId.Next();
    _requests.GetByIdAsync(identity, Arg.Any<CancellationToken>()).Returns((DataSubjectRequest?)null);

    var result = await Handler().Handle(new ExecuteDataSubjectRequestCommand(identity), CancellationToken.None);

    result.Status.ShouldBe(ResultStatus.NotFound);
    await NothingWasCalledNorWrittenAsync();
  }

  /// <summary>
  /// <b>Sur un appel qui n'aboutit pas, la demande reste En cours</b>, la tentative s'écrit seule, et
  /// l'échec est rendu typé : son résultat, et le texte que l'<c>Operator</c> lira.
  /// </summary>
  [Theory]
  [MemberData(nameof(FailedCalls))]
  public async Task LeavesTheRequestInProgressAddsTheAttemptAloneAndRendersTheFailure(
    string outcomeName,
    int? httpStatus,
    string message)
  {
    var outcome = ExecutionOutcome.FromName(outcomeName);
    _hostSystem.ApplyAsync(Endpoint, null!, default).ReturnsForAnyArgs(FailedCall(outcome, httpStatus));
    var request = AnExecutableRequest();

    var result = await HandleAsync(request);

    result.Status.ShouldBe(ResultStatus.Error);
    result.Errors.ShouldBe([message]);
    request.Status.ShouldBe(RequestStatus.InProgress);
    result.Value.Request.Status.ShouldBe(RequestStatus.InProgress);
    result.Value.Outcome.ShouldBe(outcome);
    result.Value.Call.ShouldNotBeNull().Outcome.ShouldBe(outcome);
    await _attempts.Received(1).AddAsync(
      Arg.Is<ExecutionAttempt>(attempt => attempt.Outcome == outcome && attempt.HttpStatus == httpStatus),
      Arg.Any<CancellationToken>());
    _scopes.ReceivedCalls().ShouldBeEmpty();
  }

  public static TheoryData<string, int?, string> FailedCalls() => new()
  {
    { nameof(ExecutionOutcome.NonSuccessResponse), 503, "Le système hôte a répondu 503. La demande reste En cours." },
    { nameof(ExecutionOutcome.NonSuccessResponse), 302, "Le système hôte a répondu 302. La demande reste En cours." },
    { nameof(ExecutionOutcome.TimedOut), null, "Le système hôte n'a pas répondu dans les 30 secondes. La demande reste En cours." },
    { nameof(ExecutionOutcome.NetworkError), null, "Le système hôte est injoignable. La demande reste En cours." },
  };

  /// <summary>
  /// ⚠️ <b>Succès non enregistré</b> : le système hôte a répondu 2xx, mais la transaction qui portait
  /// le passage à Terminée a échoué. Une seconde transaction, dans une portée neuve, écrit la tentative
  /// <c>SucceededButNotRecorded</c> ; l'échec est journalisé en erreur, et rendu.
  /// </summary>
  [Fact]
  public async Task AddsASucceededButNotRecordedAttemptInASecondTransactionWhenCompletingFails()
  {
    var failure = new InvalidOperationException("La base a refusé l'écriture.");
    _attempts.AddAsync(Arg.Is<ExecutionAttempt>(attempt => attempt.Outcome == ExecutionOutcome.Succeeded), Arg.Any<CancellationToken>())
      .ThrowsAsync(failure);
    var request = AnExecutableRequest();

    var result = await HandleAsync(request);

    await _recovery.Received(1).AddAsync(
      Arg.Is<ExecutionAttempt>(attempt =>
        attempt.DataSubjectRequestId == request.Id
        && attempt.Outcome == ExecutionOutcome.SucceededButNotRecorded
        && attempt.HttpStatus == 204),
      Arg.Is<CancellationToken>(token => !token.CanBeCanceled));

    result.Status.ShouldBe(ResultStatus.Error);
    result.Errors.ShouldBe(["Le système hôte a appliqué le droit, mais la demande n'a pas pu passer à Terminée."]);
    result.Value.Outcome.ShouldBe(ExecutionOutcome.SucceededButNotRecorded);

    // ⚠️ La ligne rendue est celle d'avant : la demande n'est pas Terminée en base.
    result.Value.Request.Status.ShouldBe(RequestStatus.InProgress);

    var logged = _logs.Written.ShouldHaveSingleItem();
    logged.Level.ShouldBe(LogLevel.Error);
    logged.Rendered.ShouldContain(request.Id.Value.ToString());
  }

  private void HostAnswers(int statusCode) =>
    _hostSystem.ApplyAsync(Endpoint, null!, default)
      .ReturnsForAnyArgs(HostSystemCall.Answered(statusCode, Now, TimeSpan.FromMilliseconds(120)));

  private static HostSystemCall FailedCall(ExecutionOutcome outcome, int? httpStatus) =>
    outcome == ExecutionOutcome.TimedOut ? HostSystemCall.TimedOut(Now, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30))
    : outcome == ExecutionOutcome.NetworkError ? HostSystemCall.Unreachable(Now, TimeSpan.FromMilliseconds(3))
    : HostSystemCall.Answered(httpStatus!.Value, Now, TimeSpan.FromMilliseconds(120));

  private async Task NothingWasCalledNorWrittenAsync()
  {
    _hostSystem.ReceivedCalls().ShouldBeEmpty();
    await _attempts.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    await _requests.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
  }

  private static DataSubjectRequest AnExecutableRequest() => ARequest(verified: true, email: "jeanne.dupont@exemple.fr");

  private static DataSubjectRequest ARequest(bool verified, string? email) =>
    DataSubjectRequest.Receive(
      new DataSubjectRequestEntry(
        Origin: Origin.Email,
        ReceivedOn: "2026-09-10",
        LastName: "Dupont",
        FirstName: "Jeanne",
        Email: email,
        IdentityVerified: verified,
        Message: "Je souhaite accéder à mes données.",
        Right: nameof(DataSubjectRight.Access)),
      new DateOnly(2026, 9, 11),
      Now).Value;

  private async Task<Result<DataSubjectRequestExecution>> HandleAsync(DataSubjectRequest request)
  {
    _requests.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);

    return await Handler().Handle(new ExecuteDataSubjectRequestCommand(request.Id), CancellationToken.None);
  }

  private ExecuteDataSubjectRequestHandler Handler() =>
    new(_requests, _attempts, _settings, _hostSystem, _scopes, new LoggerFactory([_logs]).CreateLogger<ExecuteDataSubjectRequestHandler>());
}
