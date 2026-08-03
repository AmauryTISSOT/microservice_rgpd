using MicroserviceRgpd.Core.Casework;

namespace MicroserviceRgpd.UnitTests.Core.Casework;

/// <summary>
/// Le catalogue des capacités : quatre valeurs déclarables, dont deux seulement seront appelées
/// dans ce lot. Une capacité déclarée que personne n'exerce est une déclaration exacte ; une
/// capacité indéclarable serait un mensonge du catalogue.
/// </summary>
public class CapabilityTests
{
  [Fact]
  public void ExposesExactlyLocateReadEraseAndRectify()
  {
    Capability.List.Select(capability => capability.Name)
      .ShouldBe(["Locate", "Read", "Erase", "Rectify"], ignoreOrder: true);
  }

  /// <summary>
  /// Elle se lit sur le fil et en base sous son <b>nom canonique anglais</b>, jamais sous son
  /// ordinal : un entier ferait de l'ordre de déclaration un élément du schéma, et le premier
  /// réordonnancement mentirait.
  /// </summary>
  [Theory]
  [InlineData("Locate")]
  [InlineData("Read")]
  [InlineData("Erase")]
  [InlineData("Rectify")]
  public void RoundTripsThroughItsCanonicalEnglishName(string name)
  {
    Capability.FromName(name).Name.ShouldBe(name);
  }

  /// <summary>Chaque capacité porte le mot que l'<c>Operator</c> lit ; le français reste hors des identifiants.</summary>
  [Fact]
  public void CarriesTheFrenchWordTheOperatorReads()
  {
    Capability.List.ShouldAllBe(capability => capability.FrenchLabel.Length > 0);
    Capability.Locate.FrenchLabel.ShouldBe("localiser");
  }
}
