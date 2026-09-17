using MicroserviceRgpd.Core.Requests;

namespace MicroserviceRgpd.UnitTests.Core.Requests;

/// <summary>
/// Le <b>motif de blocage de la prolongation</b> : une valeur fermée de trois, dont le libellé
/// français est rendu par le serveur (ADR-0029).
/// </summary>
/// <remarks>
/// ⚠️ <b>L'ordre est le contrat</b> : c'est lui qui décide lequel se dit quand plusieurs conditions
/// manquent. Ce test le garde, avec les libellés que l'<c>Operator</c> lit.
/// </remarks>
public class ExtensionBlockTests
{
  [Fact]
  public void ListsExactlyThreeBlocksInTheirOrder()
  {
    ExtensionBlock.List.OrderBy(block => block.Value).Select(block => block.Name).ShouldBe(
      ["Closed", "AlreadyExtended", "DeadlineElapsed"]);
  }

  [Theory]
  [InlineData("Closed", "Demande close")]
  [InlineData("AlreadyExtended", "Demande déjà prolongée")]
  [InlineData("DeadlineElapsed", "Date limite de réponse dépassée")]
  public void SaysItsReasonInFrench(string name, string frenchLabel)
  {
    ExtensionBlock.FromName(name).FrenchLabel.ShouldBe(frenchLabel);
  }

  /// <summary>
  /// ⚠️ <b>Aucun motif ne nomme le droit</b> : aucune des trois raisons n'en dépend, et un libellé à
  /// trou y serait rendu tel quel.
  /// </summary>
  [Fact]
  public void NamesNoRight()
  {
    ExtensionBlock.List.Select(block => block.FrenchLabel)
      .ShouldAllBe(label => !label.Contains('{', StringComparison.Ordinal));
  }
}
