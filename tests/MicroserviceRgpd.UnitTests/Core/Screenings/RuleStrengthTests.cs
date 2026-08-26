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
  public void ListsExactlyTheFiveFamiliesOfRules()
  {
    RuleStrength.List.Select(strength => strength.Name).ShouldBe(
      ["ExactName", "CheckedValueForm", "Morphological", "ValueForm", "TypeHeuristic"],
      ignoreOrder: true);
  }

  /// <summary>
  /// <b>L'ordre est celui dans lequel une règle parle le plus directement</b>, et c'est le seul
  /// usage qu'on en fasse jamais : départager les règles <b>retenues</b> d'une même catégorie. Les
  /// deux membres de forme s'y insèrent par la <b>clé de contrôle</b> — une clé qui se vérifie deux
  /// fois parle plus directement qu'un affixe, une forme sans clé moins.
  /// </summary>
  [Fact]
  public void OrdersTheDegreesByHowDirectlyTheirRuleSpeaks()
  {
    RuleStrength.List.OrderBy(strength => strength.Value).Select(strength => strength.Name).ShouldBe(
      ["ExactName", "CheckedValueForm", "Morphological", "ValueForm", "TypeHeuristic"]);
  }

  /// <summary>
  /// ⚠️ <b>La persistance passe par le <c>Name</c>, jamais par l'ordinal</b> : c'est ce qui a rendu
  /// la renumérotation gratuite le jour où deux membres se sont insérés au milieu. Une base écrite
  /// avant l'insertion se relit sans migration.
  /// </summary>
  [Theory]
  [InlineData("ExactName")]
  [InlineData("CheckedValueForm")]
  [InlineData("Morphological")]
  [InlineData("ValueForm")]
  [InlineData("TypeHeuristic")]
  public void ReadsBackFromItsNameRatherThanFromItsOrdinal(string name)
  {
    RuleStrength.FromName(name).Name.ShouldBe(name);
  }

  [Theory]
  [InlineData("ExactName", "correspondance exacte")]
  [InlineData("CheckedValueForm", "clé de contrôle des valeurs")]
  [InlineData("Morphological", "rapprochement morphologique")]
  [InlineData("ValueForm", "forme des valeurs")]
  [InlineData("TypeHeuristic", "heuristique de type")]
  public void CarriesTheFrenchLabelOfEachDegree(string name, string frenchLabel)
  {
    RuleStrength.FromName(name).FrenchLabel.ShouldBe(frenchLabel);
  }

  /// <summary>
  /// ⚠️ <b>Le seuil est attaché au membre, comme <c>Rule</c> l'est déjà, et jamais un paramètre de
  /// configuration.</b> Un seuil réglable ferait du degré une chose qu'on accorde ; ici il <b>est</b>
  /// la règle, seuil compris.
  /// </summary>
  [Theory]
  [InlineData("ExactName")]
  [InlineData("Morphological")]
  [InlineData("TypeHeuristic")]
  public void CarriesNoThresholdOnTheDegreesThatReadNoValue(string name)
  {
    RuleStrength.FromName(name).Threshold.ShouldBeNull();
  }

  /// <summary>
  /// ⚠️ <b>Les deux membres de forme se séparent sur la clé de contrôle, jamais sur le nombre.</b>
  /// Un test de forme ne se multiplie pas sur cinq valeurs — « cinq sur cinq » n'est qu'une seule
  /// observation affichée cinq fois —, là où deux clés distinctes qui se vérifient valident à une
  /// chance sur dix milliards.
  /// </summary>
  [Fact]
  public void SeparatesTheTwoValueDegreesOnTheCheckKeyAndNotOnTheCount()
  {
    RuleStrength.CheckedValueForm.Threshold.ShouldBe(ValueFormThreshold.AtLeastTwoCountedValues);
    RuleStrength.ValueForm.Threshold.ShouldBe(ValueFormThreshold.EveryCountedValue);
  }

  /// <summary>
  /// <b>La dérivation est structurelle.</b> Chaque membre dit quelle règle l'a produit : le degré
  /// n'est pas une mesure attachée après coup à une règle, il <b>est</b> la règle.
  /// </summary>
  [Fact]
  public void NamesTheRuleThatProducedEachDegree()
  {
    RuleStrength.List.ShouldAllBe(strength => strength.Rule.Length > 0);
    RuleStrength.List.Select(strength => strength.Rule).Distinct().Count().ShouldBe(5);
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
