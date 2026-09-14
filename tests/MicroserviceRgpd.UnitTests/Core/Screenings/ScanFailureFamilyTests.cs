using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UnitTests.Core.Screenings;

/// <summary>
/// Les familles de ce qui fait tomber un scan : chacune a son libellé et sa phrase, écrits dans les
/// mots du service.
/// </summary>
public class ScanFailureFamilyTests
{
  /// <summary>
  /// ⚠️ <b>La famille du moteur porte le nom exact que le dépôt collé affiche</b> : une panne d'A2 se
  /// dit de la même façon sur les deux chemins, et l'<c>Operator</c> reconnaît la même panne.
  /// </summary>
  [Fact]
  public void NamesTheUnavailableEngineAsTheDepositScreenDoes()
  {
    ScanFailureFamily.EngineUnavailable.FrenchLabel.ShouldBe(ScreeningEngineUnavailable.FrenchLabel);
  }

  /// <summary>
  /// La phrase dit qu'aucun rapport de détection n'a été produit, et ce que l'<c>Operator</c> peut
  /// faire : relancer plus tard, prévenir l'exploitant.
  /// </summary>
  [Fact]
  public void SaysNoReportWasProducedAndThatTheScanCanBeLaunchedAgain()
  {
    var statement = ScanFailureFamily.EngineUnavailable.Statement;

    statement.ShouldContain(ScreeningEngineUnavailable.Statement);
    statement.ShouldContain("relancez le scan");
    statement.ShouldContain("exploitant");
  }

  /// <summary>
  /// ⚠️ <b>Aucun mot d'Ollama</b> : la panne se dit dans la langue du contexte, et la cause fine est
  /// au journal.
  /// </summary>
  [Fact]
  public void CitesNothingOfOllama()
  {
    ScanFailureFamily.EngineUnavailable.Statement.ShouldNotContain("ollama", Case.Insensitive);
    ScanFailureFamily.EngineUnavailable.FrenchLabel.ShouldNotContain("ollama", Case.Insensitive);
  }

  /// <summary>Chaque famille a sa phrase : l'écran n'en nomme qu'une, et elle se distingue des autres.</summary>
  [Fact]
  public void GivesEveryFamilyAStatementOfItsOwn()
  {
    ScanFailureFamily.List.Select(family => family.Statement).ShouldBeUnique();
    ScanFailureFamily.List.Select(family => family.FrenchLabel).ShouldBeUnique();
  }
}
