using Ardalis.Specification;
using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.UseCases.Requests.ReadDataSubjectRequests;

namespace MicroserviceRgpd.UnitTests.UseCases.Requests.ReadDataSubjectRequests;

/// <summary>
/// Ce que la lecture rend d'une demande enregistrée porte <b>l'origine</b> et <b>le message</b> :
/// les deux informations qui disent ce que la personne demande vraiment, et que rien ne faisait
/// jusqu'ici traverser la lecture.
/// </summary>
/// <remarks>
/// ⚠️ <b>Ni l'origine ni le message ne sont recomposés</b> : la lecture rend l'<see cref="Origin"/>
/// du domaine — son libellé français vient avec lui — et le <see cref="RequestMessage"/> tel qu'il a
/// été enregistré.
/// </remarks>
public class ReadDataSubjectRequestsHandlerTests
{
  private static readonly DateTimeOffset Now = new(2026, 9, 11, 8, 15, 0, TimeSpan.Zero);

  private readonly IReadRepository<DataSubjectRequest> _requests =
    Substitute.For<IReadRepository<DataSubjectRequest>>();

  private readonly IReadRepository<Settings> _settings = Substitute.For<IReadRepository<Settings>>();

  /// <summary>Les deux canaux, avec le libellé que chacun porte.</summary>
  public static TheoryData<Origin, string> TheOrigins => new()
  {
    { Origin.Email, "Email" },
    { Origin.Letter, "Courrier" },
  };

  /// <summary>La lecture rend l'origine de la demande, et son libellé français vient avec elle.</summary>
  [Theory]
  [MemberData(nameof(TheOrigins))]
  public async Task RendersTheOriginOfTheRequestWithItsFrenchLabel(Origin origin, string frenchLabel)
  {
    var read = await ReadAsync(ARequestFrom(origin));

    read.Origin.ShouldBe(origin);
    read.Origin.FrenchLabel.ShouldBe(frenchLabel);
  }

  /// <summary>
  /// <b>Le message est celui qui a été enregistré, au caractère près</b> — retours à la ligne
  /// compris : c'est le corps de l'email ou la transcription du courrier, et rien ne le reformate.
  /// </summary>
  [Fact]
  public async Task RendersTheMessageAsItWasRecorded()
  {
    const string Message = "Madame, Monsieur,\n\nJe souhaite accéder à mes données.\n— Jeanne";

    var request = ARequestFrom(Origin.Letter, Message);

    var read = await ReadAsync(request);

    read.Message.ShouldBe(request.Message);
    read.Message.Value.ShouldBe(Message);
  }

  /// <summary>
  /// <b>Chaque demande porte son motif de blocage</b>, calculé par l'agrégat face à l'adresse de
  /// <i>son</i> droit : deux demandes vérifiées, avec un email, l'une au droit configuré et l'autre
  /// non.
  /// </summary>
  [Fact]
  public async Task CarriesForEachRequestTheBlockFacingTheEndpointOfItsRight()
  {
    var settings = Settings.Unconfigured();
    settings.SetChannel(
      DataSubjectRight.Access,
      new ExerciseChannel.HttpEndpoint(EndpointUrl.From("https://brocanto.example.fr/rgpd/acces")));

    var read = await ReadAllAsync(
      settings,
      ARequestFrom(Origin.Email, identityVerified: true, right: "Access"),
      ARequestFrom(Origin.Email, identityVerified: true, right: "Erasure"),
      ARequestFrom(Origin.Email, identityVerified: false, right: "Access"));

    read.Select(request => request.ExecutionBlock).ShouldBe(
      [null, ExecutionBlock.RightNotConfigured, ExecutionBlock.IdentityNotVerified]);
  }

  /// <summary>
  /// ⚠️ <b>Le Paramétrage est lu une seule fois</b>, quel que soit le nombre de lignes : une lecture
  /// par demande ferait autant d'allers-retours en base que le tableau porte de lignes.
  /// </summary>
  [Fact]
  public async Task ReadsTheSettingsOnlyOnce()
  {
    await ReadAllAsync(null, ARequestFrom(Origin.Email), ARequestFrom(Origin.Letter), ARequestFrom(Origin.Email));

    await _settings.Received(1).ListAsync(Arg.Any<CancellationToken>());
  }

  /// <summary>
  /// <b>Un Paramétrage jamais enregistré est vierge</b>, pas absent : chaque droit y est « non
  /// configuré ».
  /// </summary>
  [Fact]
  public async Task ReadsAServiceWithoutSettingsAsUnconfigured()
  {
    var read = await ReadAllAsync(null, ARequestFrom(Origin.Email, identityVerified: true));

    read.ShouldHaveSingleItem().ExecutionBlock.ShouldBe(ExecutionBlock.RightNotConfigured);
  }

  /// <summary>Ce que la lecture rend de la demande nommée, elle seule étant enregistrée.</summary>
  private async Task<RecordedDataSubjectRequest> ReadAsync(DataSubjectRequest request) =>
    (await ReadAllAsync(null, request)).ShouldHaveSingleItem();

  /// <summary>Ce que la lecture rend de ces demandes, sous ce Paramétrage — ou sans aucun enregistré.</summary>
  private async Task<IReadOnlyList<RecordedDataSubjectRequest>> ReadAllAsync(
    Settings? settings,
    params DataSubjectRequest[] requests)
  {
    _requests
      .ListAsync(Arg.Any<ISpecification<DataSubjectRequest>>(), Arg.Any<CancellationToken>())
      .Returns([.. requests]);

    _settings.ListAsync(Arg.Any<CancellationToken>()).Returns(settings is null ? [] : [settings]);

    return await new ReadDataSubjectRequestsHandler(_requests, _settings, ADeploymentThatCanPublish(), new AClockStuckAt(Now))
      .Handle(new ReadDataSubjectRequestsQuery(), CancellationToken.None);
  }

  /// <summary>
  /// Un déploiement qui déclare une connexion au broker : ces lectures ne parlent pas du bus, et un
  /// déploiement muet y ferait apparaître un motif de blocage qui n'est pas leur sujet.
  /// </summary>
  private static IBrokerConnectionState ADeploymentThatCanPublish()
  {
    var broker = Substitute.For<IBrokerConnectionState>();
    broker.Current.Returns(BrokerConnection.Configured.Instance);

    return broker;
  }

  private static DataSubjectRequest ARequestFrom(
    Origin origin,
    string message = "Je souhaite accéder à mes données.",
    bool identityVerified = false,
    string right = "Access") =>
    DataSubjectRequest.Receive(
      new DataSubjectRequestEntry(
        Origin: origin,
        ReceivedOn: "2026-09-10",
        LastName: "Dupont",
        FirstName: "Jeanne",
        Email: "jeanne.dupont@exemple.fr",
        IdentityVerified: identityVerified,
        Message: message,
        Right: right),
      new DateOnly(2026, 9, 11),
      Now).Value;
}
