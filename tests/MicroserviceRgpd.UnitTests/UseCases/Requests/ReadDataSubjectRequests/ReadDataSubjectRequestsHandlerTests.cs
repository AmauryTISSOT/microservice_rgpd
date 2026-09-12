using Ardalis.Specification;
using MicroserviceRgpd.Core.Requests;
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

  /// <summary>Ce que la lecture rend de la demande nommée, elle seule étant enregistrée.</summary>
  private async Task<RecordedDataSubjectRequest> ReadAsync(DataSubjectRequest request)
  {
    _requests
      .ListAsync(Arg.Any<ISpecification<DataSubjectRequest>>(), Arg.Any<CancellationToken>())
      .Returns([request]);

    var read = await new ReadDataSubjectRequestsHandler(_requests)
      .Handle(new ReadDataSubjectRequestsQuery(), CancellationToken.None);

    return read.ShouldHaveSingleItem();
  }

  private static DataSubjectRequest ARequestFrom(Origin origin, string message = "Je souhaite accéder à mes données.") =>
    DataSubjectRequest.Receive(
      new DataSubjectRequestEntry(
        Origin: origin,
        ReceivedOn: "2026-09-10",
        LastName: "Dupont",
        FirstName: "Jeanne",
        Email: "jeanne.dupont@exemple.fr",
        IdentityVerified: false,
        Message: message,
        Right: "Access"),
      new DateOnly(2026, 9, 11),
      Now).Value;
}
