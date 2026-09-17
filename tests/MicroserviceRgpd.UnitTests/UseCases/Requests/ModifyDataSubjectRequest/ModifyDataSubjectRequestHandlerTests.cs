using Ardalis.Result;
using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.UseCases.Requests.ModifyDataSubjectRequest;
using MicroserviceRgpd.UseCases.Requests.ReadDataSubjectRequests;

namespace MicroserviceRgpd.UnitTests.UseCases.Requests.ModifyDataSubjectRequest;

/// <summary>
/// Ce que le use case de modification ajoute au domaine, et rien de plus : il retrouve la demande,
/// lit l'horloge <b>une seule fois</b>, confie la correction à l'agrégat, rend la demande corrigée
/// telle que le tableau la lit et propage son <c>Result</c> tel quel.
/// </summary>
/// <remarks>
/// Les règles de la correction — les refus, leur ordre, l'empreinte — se vérifient sur l'agrégat, qui
/// se teste sans horloge ni dépôt. Ce qui se vérifie ici est l'orchestration.
/// </remarks>
public class ModifyDataSubjectRequestHandlerTests
{
  private static readonly DateTimeOffset Now = new(2026, 9, 11, 8, 15, 0, TimeSpan.Zero);

  private readonly IRepository<DataSubjectRequest> _requests = Substitute.For<IRepository<DataSubjectRequest>>();

  private readonly IReadRepository<Settings> _settings = Substitute.For<IReadRepository<Settings>>();

  private readonly AClockStuckAt _clock = new(Now);

  /// <summary>
  /// Un déploiement qui déclare une connexion au broker : la modification ne parle pas du bus, et un
  /// déploiement muet ferait apparaître un motif de blocage qui n'est pas son sujet.
  /// </summary>
  private readonly IBrokerConnectionState _broker = Substitute.For<IBrokerConnectionState>();

  public ModifyDataSubjectRequestHandlerTests()
  {
    _settings.ListAsync(Arg.Any<CancellationToken>()).Returns([]);
  }

  /// <summary>
  /// <b>La demande corrigée dit si elle s'exécute</b>, face au Paramétrage : attester l'identité
  /// d'une demande dont le droit a une adresse rallume l'exécution sur la ligne rendue.
  /// </summary>
  [Fact]
  public async Task RendersTheCorrectedRequestWithItsExecutionBlockFacingTheSettings()
  {
    var settings = Settings.Unconfigured();
    settings.SetChannel(
      DataSubjectRight.Access,
      new ExerciseChannel.HttpEndpoint(EndpointUrl.From("https://brocanto.example.fr/rgpd/acces")));
    _settings.ListAsync(Arg.Any<CancellationToken>()).Returns([settings]);

    var request = ARequestInProgress();

    (await HandleAsync(request, AValidEntry())).Value.ExecutionBlock.ShouldBe(ExecutionBlock.IdentityNotVerified);
    (await HandleAsync(request, AValidEntry() with { IdentityVerified = true })).Value.ExecutionBlock.ShouldBeNull();
  }

  private static DataSubjectRequestEntry AValidEntry() => new(
    Origin: Origin.Email,
    ReceivedOn: "2026-09-10",
    LastName: "Dupont",
    FirstName: "Jeanne",
    Email: "jeanne.dupont@exemple.fr",
    IdentityVerified: false,
    Message: "Je souhaite accéder à mes données.",
    Right: "Access");

  /// <summary>
  /// ⚠️ <b>L'horloge est lue une seule fois</b>, pour la raison que porte le handler : lus
  /// séparément, « aujourd'hui à Paris » et l'instant de la correction pourraient tomber de part et
  /// d'autre de minuit.
  /// </summary>
  [Fact]
  public async Task ReadsTheClockOnlyOnce()
  {
    await HandleAsync(ARequestInProgress(), AValidEntry() with { LastName = "Durand" });

    _clock.Readings.ShouldBe(1);
  }

  /// <summary>La correction est confiée au domaine, et ce qu'il a touché est enregistré.</summary>
  [Fact]
  public async Task HandsTheCorrectionToTheDomainAndSavesWhatItTouched()
  {
    var request = ARequestInProgress();

    var result = await HandleAsync(request, AValidEntry() with { LastName = "Durand" });

    result.IsSuccess.ShouldBeTrue();
    request.LastName!.Value.Value.ShouldBe("Durand");
    request.ModifiedAt.ShouldBe(Now);
    await _requests.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
  }

  /// <summary>
  /// <b>Le succès rend la demande corrigée</b>, telle que le tableau la lit : l'écran en rend la ligne
  /// sans relire la base.
  /// </summary>
  [Fact]
  public async Task RendersTheCorrectedRequestAsTheTableReadsIt()
  {
    var request = ARequestInProgress();

    var result = await HandleAsync(request, AValidEntry() with { ReceivedOn = "2026-08-31" });

    result.Value.Id.ShouldBe(request.Id);
    result.Value.ReceivedOn.ShouldBe(new DateOnly(2026, 8, 31));
    result.Value.ResponseDeadline.ShouldBe(new DateOnly(2026, 9, 30));
  }

  /// <summary>
  /// ⚠️ <b>Une correction qui ne change rien rend la demande quand même</b> : le succès n'a qu'une
  /// forme, et la ligne rendue reste correcte — elle n'a pas changé.
  /// </summary>
  [Fact]
  public async Task RendersTheRequestEvenWhenNothingChanged()
  {
    var request = ARequestInProgress();

    var result = await HandleAsync(request, AValidEntry());

    result.IsSuccess.ShouldBeTrue();
    result.Value.Id.ShouldBe(request.Id);
    request.ModifiedAt.ShouldBeNull();
  }

  /// <summary>
  /// <b>Le refus d'une demande close traverse tel quel</b> — <c>Conflict</c>, et rien n'est
  /// enregistré.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Le conflit se vérifie ici comme l'<c>Invalid</c></b>, bien que ce soit l'agrégat qui le
  /// décide : le use case rend désormais la demande, et la projection ne doit <b>pas</b> aplatir le
  /// statut du refus en la traversant.
  /// </remarks>
  [Theory]
  [InlineData("Completed")]
  [InlineData("Cancelled")]
  public async Task PropagatesTheConflictOfAClosedRequest(string status)
  {
    var result = await HandleAsync(AClosedRequest(status), AValidEntry() with { LastName = "Durand" });

    result.Status.ShouldBe(ResultStatus.Conflict);
    await _requests.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
  }

  /// <summary>
  /// <b>Le refus du domaine traverse tel quel</b>, et rien n'est enregistré : c'est l'agrégat qui dit
  /// ce qu'un refus vaut, pas le use case.
  /// </summary>
  [Fact]
  public async Task PropagatesTheRefusalOfTheDomainWithoutWritingAnything()
  {
    var result = await HandleAsync(ARequestInProgress(), AValidEntry() with { Message = null });

    result.Status.ShouldBe(ResultStatus.Invalid);
    result.ValidationErrors.Select(error => error.Identifier).ShouldBe(["message"]);
    await _requests.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
  }

  /// <summary>
  /// <b>Une demande introuvable n'est pas une faute de programmation</b> : deux onglets ouverts sur le
  /// tableau, et l'autre l'a déjà supprimée.
  /// </summary>
  [Fact]
  public async Task RendersNotFoundWhenTheRequestIsGone()
  {
    var identity = DataSubjectRequestId.Next();
    _requests.GetByIdAsync(identity, Arg.Any<CancellationToken>()).Returns((DataSubjectRequest?)null);

    var result = await Handler().Handle(
      new ModifyDataSubjectRequestCommand(identity, AValidEntry()),
      CancellationToken.None);

    result.Status.ShouldBe(ResultStatus.NotFound);
    await _requests.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
  }

  private static DataSubjectRequest ARequestInProgress() =>
    DataSubjectRequest.Receive(AValidEntry(), new DateOnly(2026, 9, 11), Now).Value;

  /// <summary>Une demande close — le statut posé par réflexion : aucun geste ne le fait changer.</summary>
  private static DataSubjectRequest AClosedRequest(string status)
  {
    var request = ARequestInProgress();

    typeof(DataSubjectRequest)
      .GetProperty(nameof(DataSubjectRequest.Status))!
      .SetValue(request, RequestStatus.FromName(status));

    return request;
  }

  private async Task<Result<RecordedDataSubjectRequest>> HandleAsync(
    DataSubjectRequest request,
    DataSubjectRequestEntry entry)
  {
    _requests.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);

    return await Handler().Handle(
      new ModifyDataSubjectRequestCommand(request.Id, entry),
      CancellationToken.None);
  }

  private ModifyDataSubjectRequestHandler Handler()
  {
    _broker.Current.Returns(BrokerConnection.Configured.Instance);

    return new ModifyDataSubjectRequestHandler(_requests, _settings, _broker, _clock);
  }
}
