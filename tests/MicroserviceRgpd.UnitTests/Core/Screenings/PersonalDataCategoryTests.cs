using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UnitTests.Core.Screenings;

/// <summary>
/// La taxonomie est fermée, et l'<b>ordre</b> de ses valeurs n'est pas une commodité de lecture :
/// c'est l'ordre d'arbitrage des collisions, figé. Ces tests sont ce qui rend bruyant le fait d'y
/// toucher.
/// </summary>
public class PersonalDataCategoryTests
{
  [Fact]
  public void ListsExactlyThirteenValues()
  {
    PersonalDataCategory.List.Count.ShouldBe(13);
  }

  /// <summary>
  /// L'ordre de la liste <b>est</b> l'ordre d'arbitrage, du plus au moins coûteux à omettre. Le
  /// changer change ce que le service rend sur une colonne où deux règles déclenchent.
  /// </summary>
  [Fact]
  public void NamesItsThirteenValuesInTheFrozenArbitrationOrder()
  {
    PersonalDataCategory.List
      .OrderBy(category => category.ArbitrationRank)
      .Select(category => category.Name)
      .ShouldBe(
      [
        "CriminalOffenceData",
        "HealthData",
        "SpecialCategoryData",
        "AuthenticationSecret",
        "NationalIdentifier",
        "FinancialData",
        "LocationData",
        "ConnectionData",
        "Identity",
        "ContactDetails",
        "ProfessionalLife",
        "PersonalDataUncategorised",
        "Unflagged",
      ]);
  }

  [Theory]
  [InlineData("CriminalOffenceData", "données relatives aux infractions")]
  [InlineData("HealthData", "données concernant la santé")]
  [InlineData("SpecialCategoryData", "autre catégorie particulière")]
  [InlineData("AuthenticationSecret", "secret d'authentification")]
  [InlineData("NationalIdentifier", "identifiant national")]
  [InlineData("FinancialData", "données économiques et financières")]
  [InlineData("LocationData", "données de localisation")]
  [InlineData("ConnectionData", "données de connexion")]
  [InlineData("Identity", "état civil et identité")]
  [InlineData("ContactDetails", "coordonnées")]
  [InlineData("ProfessionalLife", "vie professionnelle")]
  [InlineData("PersonalDataUncategorised", "donnée personnelle sans catégorie")]
  [InlineData("Unflagged", "rien signalé")]
  public void CarriesTheFrenchLabelOfEachValueAsAttachedData(string name, string frenchLabel)
  {
    PersonalDataCategory.FromName(name).FrenchLabel.ShouldBe(frenchLabel);
  }

  /// <summary>
  /// <c>arret_maladie</c> est santé <i>et</i> vie professionnelle. C'est la santé qui l'emporte,
  /// parce que c'est elle qui coûte le plus cher à omettre — jamais parce qu'une règle aurait été
  /// « plus sûre » qu'une autre.
  /// </summary>
  [Fact]
  public void SettlesTheHealthAndProfessionalCollisionOnHealth()
  {
    var settled = PersonalDataCategory.MostCostlyToOmit(
      [PersonalDataCategory.ProfessionalLife, PersonalDataCategory.HealthData]);

    settled.ShouldBe(PersonalDataCategory.HealthData);
  }

  /// <summary><c>email_pro</c> est coordonnées <i>et</i> vie professionnelle : les coordonnées l'emportent.</summary>
  [Fact]
  public void SettlesTheContactAndProfessionalCollisionOnContactDetails()
  {
    var settled = PersonalDataCategory.MostCostlyToOmit(
      [PersonalDataCategory.ProfessionalLife, PersonalDataCategory.ContactDetails]);

    settled.ShouldBe(PersonalDataCategory.ContactDetails);
  }

  /// <summary>L'ordre de présentation des règles déclenchées ne change rien : le départage est celui du tableau.</summary>
  [Fact]
  public void SettlesTheSameWayWhateverOrderTheRulesFiredIn()
  {
    var candidates = new[]
    {
      PersonalDataCategory.ProfessionalLife,
      PersonalDataCategory.ContactDetails,
      PersonalDataCategory.CriminalOffenceData,
      PersonalDataCategory.Identity,
    };

    PersonalDataCategory.MostCostlyToOmit(candidates).ShouldBe(PersonalDataCategory.CriminalOffenceData);
    PersonalDataCategory.MostCostlyToOmit(candidates.Reverse()).ShouldBe(PersonalDataCategory.CriminalOffenceData);
  }

  /// <summary>Une seule règle déclenchée se départage en elle-même, sans cas particulier.</summary>
  [Fact]
  public void SettlesASingleTriggeredValueOnItself()
  {
    PersonalDataCategory.MostCostlyToOmit([PersonalDataCategory.LocationData])
      .ShouldBe(PersonalDataCategory.LocationData);
  }

  /// <summary>
  /// « Rien n'a déclenché » ne se départage pas : c'est <c>Unflagged</c>, une valeur nommée qu'on
  /// pose. Rendre une valeur par défaut ici ferait naître un signalement de nulle part.
  /// </summary>
  [Fact]
  public void RefusesToSettleWhenNoRuleFiredAtAll()
  {
    Should.Throw<ArgumentException>(() => PersonalDataCategory.MostCostlyToOmit([]));
  }

  /// <summary>
  /// La sensibilité est un <b>calcul</b> sur la catégorie, jamais un drapeau parallèle : deux champs
  /// qu'un chemin d'écriture peut dissocier finissent par se dissocier.
  /// </summary>
  [Fact]
  public void ReadsSensitivityAsACalculationOverExactlyTheThreeStatutoryValues()
  {
    PersonalDataCategory.List
      .Where(category => category.IsClosedByStatute)
      .Select(category => category.Name)
      .ShouldBe(["CriminalOffenceData", "HealthData", "SpecialCategoryData"], ignoreOrder: true);
  }

  /// <summary>
  /// La gouvernance a deux étages parce que les valeurs n'ont pas toutes le même auteur : trois
  /// viennent du texte, les dix autres de notre découpage.
  /// </summary>
  [Theory]
  [InlineData("CriminalOffenceData", "RGPD art. 10")]
  [InlineData("HealthData", "RGPD art. 9")]
  [InlineData("SpecialCategoryData", "RGPD art. 9")]
  public void TracesTheThreeStatutoryValuesBackToTheirArticle(string name, string origin)
  {
    var category = PersonalDataCategory.FromName(name);

    category.Origin.ShouldBe(origin);
    category.IsClosedByStatute.ShouldBeTrue();
  }

  [Fact]
  public void ClaimsNoStatutoryAuthorityForTheOrdinaryValues()
  {
    PersonalDataCategory.List
      .Where(category => !category.IsClosedByStatute)
      .ShouldAllBe(category => !category.Origin.StartsWith("RGPD", StringComparison.Ordinal));
  }

  /// <summary>
  /// L'invariant du contexte, vu du côté de la taxonomie : <c>Unflagged</c> est la seule valeur qui
  /// ne signale rien, et le repli en est bien une autre.
  /// </summary>
  [Fact]
  public void FlagsEveryValueButUnflagged()
  {
    PersonalDataCategory.List
      .Where(category => !category.IsFlagged)
      .ShouldBe([PersonalDataCategory.Unflagged]);

    PersonalDataCategory.PersonalDataUncategorised.IsFlagged.ShouldBeTrue();
  }

  /// <summary>Le mot « catégorie » est interdit ailleurs : le nom complet est porté ici pour que cette interdiction n'ait pas d'exception.</summary>
  [Fact]
  public void IsSealedSoNoOneAddsAFourteenthValue()
  {
    typeof(PersonalDataCategory).IsSealed.ShouldBeTrue();
  }
}
