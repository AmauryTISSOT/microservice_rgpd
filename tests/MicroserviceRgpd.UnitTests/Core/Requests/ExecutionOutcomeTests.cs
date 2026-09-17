using MicroserviceRgpd.Core.Requests;

namespace MicroserviceRgpd.UnitTests.Core.Requests;

/// <summary>
/// Le <b>résultat d'une tentative d'exécution</b> : une valeur fermée de sept, chacune avec son
/// libellé français, pour que le journal se compte et se filtre sans interpréter du texte — une seule
/// valeur pour les deux canaux (ADR-0026, ADR-0028).
/// </summary>
public class ExecutionOutcomeTests
{
  [Fact]
  public void ListsExactlySevenOutcomesInTheirOrder()
  {
    ExecutionOutcome.List.OrderBy(outcome => outcome.Value).Select(outcome => outcome.Name).ShouldBe(
      [
        "Succeeded",
        "NonSuccessResponse",
        "TimedOut",
        "NetworkError",
        "SucceededButNotRecorded",
        "Unroutable",
        "Rejected",
      ]);
  }

  /// <summary>
  /// ⚠️ <b>Les cinq premiers gardent leur rang</b> : le journal stocke le nom, et les deux cas du bus
  /// s'ajoutent à la suite plutôt que de renuméroter ce qui est déjà écrit en base.
  /// </summary>
  [Theory]
  [InlineData("Succeeded", 0)]
  [InlineData("NonSuccessResponse", 1)]
  [InlineData("TimedOut", 2)]
  [InlineData("NetworkError", 3)]
  [InlineData("SucceededButNotRecorded", 4)]
  public void KeepsTheRankOfEveryOutcomeThatExistedBefore(string name, int value)
  {
    ExecutionOutcome.FromName(name).Value.ShouldBe(value);
  }

  [Theory]
  [InlineData("Succeeded", "Succès")]
  [InlineData("NonSuccessResponse", "Réponse non 2xx")]
  [InlineData("TimedOut", "Délai dépassé")]
  [InlineData("NetworkError", "Erreur réseau")]
  [InlineData("SucceededButNotRecorded", "Succès non enregistré")]
  [InlineData("Unroutable", "Message non routable")]
  [InlineData("Rejected", "Publication refusée par le broker")]
  public void CarriesTheFrenchLabelOfEachOutcome(string name, string frenchLabel)
  {
    ExecutionOutcome.FromName(name).FrenchLabel.ShouldBe(frenchLabel);
  }
}
