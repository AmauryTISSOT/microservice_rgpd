using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UnitTests.Core.Screenings;

/// <summary>
/// La taxonomie est fermée : quatorze prototypes et <c>Unflagged</c>. Ces tests sont ce qui rend
/// bruyant le fait d'y toucher.
/// </summary>
/// <remarks>
/// ⚠️ <b>Aucun ordre d'arbitrage n'est éprouvé ici</b>, et ce n'est pas un oubli : l'ordre a cessé
/// d'être une propriété de la taxonomie. Le moteur lexique garde le sien pour départager ses propres
/// déclenchements, et ses tests vivent avec lui — voir <c>ScreeningRulesTests</c>.
/// </remarks>
public class PersonalDataCategoryTests
{
  [Fact]
  public void ListsExactlyFifteenValues()
  {
    PersonalDataCategory.List.Count.ShouldBe(15);
  }

  [Fact]
  public void NamesItsFourteenPrototypesAndUnflagged()
  {
    PersonalDataCategory.List
      .OrderBy(category => category.Value)
      .Select(category => category.Name)
      .ShouldBe(
      [
        "Identity",
        "ContactDetails",
        "LocationData",
        "NationalIdentifier",
        "FinancialData",
        "AuthenticationSecret",
        "OnlineIdentifier",
        "DemographicData",
        "ProfessionalLife",
        "BehaviouralData",
        "HealthData",
        "RelatedPerson",
        "FreeTextAboutPerson",
        "PersonReference",
        "Unflagged",
      ]);
  }

  [Theory]
  [InlineData("Identity", "état civil et identité")]
  [InlineData("ContactDetails", "coordonnées")]
  [InlineData("LocationData", "données de localisation")]
  [InlineData("NationalIdentifier", "identifiant national")]
  [InlineData("FinancialData", "données économiques et financières")]
  [InlineData("AuthenticationSecret", "secret d'authentification")]
  [InlineData("OnlineIdentifier", "identifiant en ligne")]
  [InlineData("DemographicData", "données démographiques")]
  [InlineData("ProfessionalLife", "vie professionnelle")]
  [InlineData("BehaviouralData", "données de comportement")]
  [InlineData("HealthData", "données concernant la santé")]
  [InlineData("RelatedPerson", "personne liée")]
  [InlineData("FreeTextAboutPerson", "texte libre sur une personne")]
  [InlineData("PersonReference", "référence à une personne")]
  [InlineData("Unflagged", "rien signalé")]
  public void CarriesTheFrenchLabelOfEachValueAsAttachedData(string name, string frenchLabel)
  {
    PersonalDataCategory.FromName(name).FrenchLabel.ShouldBe(frenchLabel);
  }

  /// <summary>
  /// Le nom de prototype est écrit <b>une seule fois</b>, sur la valeur : c'est lui qu'un moteur à
  /// prototypes résoudra, et une table de correspondance tenue ailleurs finirait par diverger.
  /// </summary>
  [Theory]
  [InlineData("Identity", "identity")]
  [InlineData("ContactDetails", "contact")]
  [InlineData("LocationData", "location")]
  [InlineData("NationalIdentifier", "government_id")]
  [InlineData("FinancialData", "financial")]
  [InlineData("AuthenticationSecret", "authentication")]
  [InlineData("OnlineIdentifier", "online_identifier")]
  [InlineData("DemographicData", "demographic")]
  [InlineData("ProfessionalLife", "professional")]
  [InlineData("BehaviouralData", "behavioural")]
  [InlineData("HealthData", "health")]
  [InlineData("RelatedPerson", "relation")]
  [InlineData("FreeTextAboutPerson", "free_text")]
  [InlineData("PersonReference", "person_link")]
  public void CarriesThePrototypeNameOfEachFlaggedValueAsAttachedData(string name, string prototypeName)
  {
    PersonalDataCategory.FromName(name).PrototypeName.ShouldBe(prototypeName);
  }

  /// <summary><c>Unflagged</c> n'est proche d'aucun prototype : elle dit que rien n'a été signalé.</summary>
  [Fact]
  public void GivesUnflaggedNoPrototype()
  {
    PersonalDataCategory.Unflagged.PrototypeName.ShouldBeNull();
  }

  /// <summary>
  /// Les valeurs retirées ne se lisent plus : un nom de la taxonomie d'avant qui passerait encore
  /// ferait renaître en silence une catégorie qu'aucun écran ne sait plus rendre.
  /// </summary>
  [Theory]
  [InlineData("CriminalOffenceData")]
  [InlineData("SpecialCategoryData")]
  [InlineData("ConnectionData")]
  [InlineData("PersonalDataUncategorised")]
  public void NoLongerKnowsTheRetiredValues(string name)
  {
    PersonalDataCategory.TryFromName(name, out _).ShouldBeFalse();
  }

  /// <summary>
  /// L'invariant du contexte, vu du côté de la taxonomie : <c>Unflagged</c> est la seule valeur qui
  /// ne signale rien.
  /// </summary>
  [Fact]
  public void FlagsEveryValueButUnflagged()
  {
    PersonalDataCategory.List
      .Where(category => !category.IsFlagged)
      .ShouldBe([PersonalDataCategory.Unflagged]);
  }

  [Fact]
  public void IsSealedSoNoOneAddsASixteenthValue()
  {
    typeof(PersonalDataCategory).IsSealed.ShouldBeTrue();
  }
}
