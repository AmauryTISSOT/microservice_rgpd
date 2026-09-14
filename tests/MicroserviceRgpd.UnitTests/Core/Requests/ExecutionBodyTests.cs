using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.UnitTests.Core.Requests;

/// <summary>
/// <b>Ce qui part au système hôte</b> : les cinq valeurs d'une demande, le prénom et le nom
/// facultatifs, l'email jamais (ADR-0026).
/// </summary>
public class ExecutionBodyTests
{
  private static readonly DateTimeOffset Now = new(2026, 9, 11, 8, 15, 0, TimeSpan.Zero);

  [Fact]
  public void CarriesTheFiveValuesOfTheRequest()
  {
    var request = ARequest(email: "jeanne.dupont@exemple.fr", lastName: "Dupont", firstName: "Jeanne");

    var body = ExecutionBody.Of(request);

    body.RequestId.ShouldBe(request.Id);
    body.Right.ShouldBe(DataSubjectRight.Erasure);
    body.Email.Value.ShouldBe("jeanne.dupont@exemple.fr");
    body.FirstName!.Value.Value.ShouldBe("Jeanne");
    body.LastName!.Value.Value.ShouldBe("Dupont");
  }

  [Fact]
  public void LeavesTheMissingNamesAbsent()
  {
    var body = ExecutionBody.Of(ARequest(email: "jeanne.dupont@exemple.fr", lastName: null, firstName: null));

    body.FirstName.ShouldBeNull();
    body.LastName.ShouldBeNull();
  }

  /// <summary>Une demande sans email ne s'exécute pas : son corps n'existe pas.</summary>
  [Fact]
  public void RefusesARequestWithoutEmail()
  {
    Should.Throw<InvalidOperationException>(() => ExecutionBody.Of(ARequest(email: null, lastName: "Dupont", firstName: "Jeanne")));
  }

  private static DataSubjectRequest ARequest(string? email, string? lastName, string? firstName) =>
    DataSubjectRequest.Receive(
      new DataSubjectRequestEntry(
        Origin: Origin.Email,
        ReceivedOn: "2026-09-10",
        LastName: lastName,
        FirstName: firstName,
        Email: email,
        IdentityVerified: true,
        Message: "Merci d'effacer mon compte.",
        Right: nameof(DataSubjectRight.Erasure)),
      new DateOnly(2026, 9, 11),
      Now).Value;
}
