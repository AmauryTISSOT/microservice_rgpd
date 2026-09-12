using Ardalis.Result;
using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.UseCases.Requests.ModifyDataSubjectRequest;

namespace MicroserviceRgpd.UnitTests.UseCases.Requests.ModifyDataSubjectRequest;

/// <summary>
/// Ce que le use case de modification ajoute au domaine, et rien de plus : il retrouve la demande,
/// lit l'horloge <b>une seule fois</b>, confie la correction à l'agrégat et propage son
/// <c>Result</c> tel quel.
/// </summary>
/// <remarks>
/// Les règles de la correction — les refus, leur ordre, l'empreinte — se vérifient sur l'agrégat, qui
/// se teste sans horloge ni dépôt. Ce qui se vérifie ici est l'orchestration.
/// </remarks>
public class ModifyDataSubjectRequestHandlerTests
{
  private static readonly DateTimeOffset Now = new(2026, 9, 11, 8, 15, 0, TimeSpan.Zero);

  private readonly IRepository<DataSubjectRequest> _requests = Substitute.For<IRepository<DataSubjectRequest>>();

  private readonly AClockStuckAt _clock = new(Now);

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

  private async Task<Result> HandleAsync(DataSubjectRequest request, DataSubjectRequestEntry entry)
  {
    _requests.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);

    return await Handler().Handle(
      new ModifyDataSubjectRequestCommand(request.Id, entry),
      CancellationToken.None);
  }

  private ModifyDataSubjectRequestHandler Handler() => new(_requests, _clock);
}
