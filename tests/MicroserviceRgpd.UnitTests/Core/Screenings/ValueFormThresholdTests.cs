using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UnitTests.Core.Screenings;

/// <summary>
/// Le seuil d'une règle de forme, <b>attaché au membre de <see cref="RuleStrength"/> qui le
/// porte</b>. Ces tests sont ce qui empêche qu'il redevienne un nombre qu'on accorde à un
/// déploiement pressé.
/// </summary>
public class ValueFormThresholdTests
{
  /// <summary>
  /// La clé de contrôle se multiplie réellement : <b>deux</b> valeurs comptées reconnues suffisent,
  /// et les autres peuvent être quelconques.
  /// </summary>
  [Theory]
  [InlineData(0, 5, false)]
  [InlineData(1, 5, false)]
  [InlineData(2, 5, true)]
  [InlineData(2, 2, true)]
  [InlineData(5, 5, true)]
  public void ReachesTheCheckKeyThresholdOnTwoCountedValuesWhateverTheRestSays(
    int recognised,
    int counted,
    bool reached)
  {
    ValueFormThreshold.AtLeastTwoCountedValues.IsReachedBy(recognised, counted).ShouldBe(reached);
  }

  /// <summary>
  /// Sans clé, une forme ne gagne rien à se répéter : ce qui la porte est qu'<b>aucune</b> valeur
  /// comptée ne la contredise. Une seule intruse suffit à éteindre la règle.
  /// </summary>
  [Theory]
  [InlineData(0, 0, false)]
  [InlineData(1, 1, true)]
  [InlineData(4, 5, false)]
  [InlineData(5, 5, true)]
  public void ReachesTheFormThresholdOnlyWhenNoCountedValueContradictsIt(
    int recognised,
    int counted,
    bool reached)
  {
    ValueFormThreshold.EveryCountedValue.IsReachedBy(recognised, counted).ShouldBe(reached);
  }

  /// <summary>
  /// ⚠️ <b>Reconnaître plus de valeurs qu'on n'en a comptées est une programmation fautive</b>, et
  /// c'est très exactement la façon la plus courte de fabriquer une preuve : un numérateur qui
  /// dépasse son dénominateur est un doublon compté deux fois, ou une valeur qu'on n'a pas eue.
  /// </summary>
  [Fact]
  public void RefusesToJudgeMoreRecognisedValuesThanCountedOnes()
  {
    Should.Throw<ArgumentOutOfRangeException>(
      () => ValueFormThreshold.EveryCountedValue.IsReachedBy(recognised: 3, counted: 2));
  }

  /// <summary>Il n'y a que <b>deux</b> seuils, et aucun chemin n'en construit un troisième.</summary>
  [Fact]
  public void OffersNoWayToBuildAThresholdNoRuleCarries()
  {
    typeof(ValueFormThreshold).GetConstructors().ShouldBeEmpty();
    typeof(ValueFormThreshold).IsSealed.ShouldBeTrue();
  }
}
