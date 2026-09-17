using MicroserviceRgpd.Core.Requests;

namespace MicroserviceRgpd.UnitTests.Core.Requests;

/// <summary>
/// Le <b>motif de prolongation</b> : <b>deux valeurs, et pas une troisième</b> — l'article 12 §3
/// n'en ouvre pas d'autre, et il n'y a pas de « Autre » (ADR-0029).
/// </summary>
public class ExtensionGroundTests
{
  /// <summary>
  /// ⚠️ <b>L'ordre est celui de l'écran</b> : c'est dans cet ordre que la modale propose les deux
  /// motifs.
  /// </summary>
  [Fact]
  public void ListsExactlyTheTwoGroundsOfTheRegulation()
  {
    ExtensionGround.List.Select(ground => ground.Name).ShouldBe(["Complexity", "NumberOfRequests"]);
  }

  /// <summary>Chaque motif porte son libellé français, celui que la modale propose.</summary>
  [Theory]
  [InlineData("Complexity", "Complexité de la demande")]
  [InlineData("NumberOfRequests", "Nombre de demandes")]
  public void CarriesTheFrenchLabelOfEachGround(string name, string frenchLabel)
  {
    ExtensionGround.FromName(name).FrenchLabel.ShouldBe(frenchLabel);
  }
}
