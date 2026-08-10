using System.Reflection;

using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UnitTests.Core.Screenings;

/// <summary>
/// Le degré est <b>dérivé de la règle qui a déclenché</b>, jamais d'une auto-évaluation. C'est
/// l'homonyme le plus dangereux du dépôt — <c>DeclaredConfidence</c> a été mesurée dégénérée, 118
/// <c>Low</c> et 2 <c>High</c> sur 120 — et ces tests sont ce qui empêche la même chose de revenir
/// sous un autre nom.
/// </summary>
public class RuleStrengthTests
{
  [Fact]
  public void ListsExactlyTheThreeFamiliesOfRules()
  {
    RuleStrength.List.Select(strength => strength.Name).ShouldBe(
      ["ExactName", "Morphological", "TypeHeuristic"],
      ignoreOrder: true);
  }

  [Theory]
  [InlineData("ExactName", "correspondance exacte")]
  [InlineData("Morphological", "rapprochement morphologique")]
  [InlineData("TypeHeuristic", "heuristique de type")]
  public void CarriesTheFrenchLabelOfEachDegree(string name, string frenchLabel)
  {
    RuleStrength.FromName(name).FrenchLabel.ShouldBe(frenchLabel);
  }

  /// <summary>
  /// <b>La dérivation est structurelle.</b> Chaque membre dit quelle règle l'a produit : le degré
  /// n'est pas une mesure attachée après coup à une règle, il <b>est</b> la règle.
  /// </summary>
  [Fact]
  public void NamesTheRuleThatProducedEachDegree()
  {
    RuleStrength.List.ShouldAllBe(strength => strength.Rule.Length > 0);
    RuleStrength.List.Select(strength => strength.Rule).Distinct().Count().ShouldBe(3);
  }

  /// <summary>
  /// ⚠️ <b>Aucun chemin ne construit un degré autrement qu'en nommant une règle.</b> Un constructeur
  /// public — ou une fabrique — rouvrirait la porte à un moteur qui s'auto-évalue, et le vocabulaire
  /// n'y pourrait plus rien.
  /// </summary>
  [Fact]
  public void OffersNoWayToBuildADegreeThatNoRuleProduced()
  {
    typeof(RuleStrength)
      .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
      .ShouldBeEmpty();
  }

  /// <summary>
  /// ⚠️ <b>Il n'y a de nombre nulle part.</b> Un score — « 0,72 » — est très exactement ce que
  /// l'<c>Aide à la décision</c> refuse : il ne s'arbitre pas, il se croit.
  /// </summary>
  [Fact]
  public void CarriesNoScoreOfAnyKindOnItsSurface()
  {
    var scoreLike = new[] { typeof(double), typeof(float), typeof(decimal) };

    typeof(RuleStrength)
      .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
      .Where(property => scoreLike.Contains(property.PropertyType))
      .ShouldBeEmpty();
  }

  [Fact]
  public void IsSealedSoNoOneAddsADegreeNoRuleProduces()
  {
    typeof(RuleStrength).IsSealed.ShouldBeTrue();
  }
}
