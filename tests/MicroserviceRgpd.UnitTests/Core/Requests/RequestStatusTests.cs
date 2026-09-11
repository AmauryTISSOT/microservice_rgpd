using MicroserviceRgpd.Core.Requests;

namespace MicroserviceRgpd.UnitTests.Core.Requests;

/// <summary>
/// Le statut d'une demande : <b>un état qu'elle tient</b>, pas la trace d'un <c>Gesture</c>
/// (ADR-0021). Trois valeurs posées ensemble, dont deux qu'aucun <c>Gesture</c> n'atteint encore.
/// </summary>
public class RequestStatusTests
{
  [Fact]
  public void ListsExactlyThreeStatuses()
  {
    RequestStatus.List.Select(status => status.Name).ShouldBe(
      ["InProgress", "Completed", "Cancelled"],
      ignoreOrder: true);
  }

  [Theory]
  [InlineData("InProgress", "En cours")]
  [InlineData("Completed", "Terminée")]
  [InlineData("Cancelled", "Annulée")]
  public void CarriesTheFrenchLabelOfEachStatus(string name, string frenchLabel)
  {
    RequestStatus.FromName(name).FrenchLabel.ShouldBe(frenchLabel);
  }
}
