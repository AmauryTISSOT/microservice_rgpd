using MicroserviceRgpd.Core.Requests;

namespace MicroserviceRgpd.UnitTests.Core.Requests;

/// <summary>
/// Le <b>résultat d'une tentative d'exécution</b> : une valeur fermée de cinq, chacune avec son
/// libellé français, pour que le journal se compte et se filtre sans interpréter du texte (ADR-0026).
/// </summary>
public class ExecutionOutcomeTests
{
  [Fact]
  public void ListsExactlyFiveOutcomesInTheirOrder()
  {
    ExecutionOutcome.List.OrderBy(outcome => outcome.Value).Select(outcome => outcome.Name).ShouldBe(
      ["Succeeded", "NonSuccessResponse", "TimedOut", "NetworkError", "SucceededButNotRecorded"]);
  }

  [Theory]
  [InlineData("Succeeded", "Succès")]
  [InlineData("NonSuccessResponse", "Réponse non 2xx")]
  [InlineData("TimedOut", "Délai dépassé")]
  [InlineData("NetworkError", "Erreur réseau")]
  [InlineData("SucceededButNotRecorded", "Succès non enregistré")]
  public void CarriesTheFrenchLabelOfEachOutcome(string name, string frenchLabel)
  {
    ExecutionOutcome.FromName(name).FrenchLabel.ShouldBe(frenchLabel);
  }
}
