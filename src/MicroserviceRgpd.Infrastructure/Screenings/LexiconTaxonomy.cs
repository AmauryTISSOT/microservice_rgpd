using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.Infrastructure.Screenings;

/// <summary>
/// Ce que le moteur lexique sait des catégories et que la taxonomie ne sait plus : <b>la
/// correspondance</b> des noms gelés vers la taxonomie des prototypes, et <b>l'ordre interne</b> qui
/// départage ses propres déclenchements.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>La correspondance s'applique au chargement, et les lexiques ne sont pas réécrits.</b> Les
/// <c>.tsv</c> sont le gel <c>d413d55</c> et portent encore les noms de la taxonomie d'avant ;
/// les rééditer romprait le gel (#155). Quatre noms retirés se ramènent donc ici, une fois :
/// <c>ConnectionData</c> → <see cref="PersonalDataCategory.OnlineIdentifier"/> ;
/// <c>SpecialCategoryData</c> et <c>CriminalOffenceData</c> →
/// <see cref="PersonalDataCategory.DemographicData"/> ; <c>PersonalDataUncategorised</c> →
/// <see cref="PersonalDataCategory.FreeTextAboutPerson"/>. Les autres gardent leur nom.
/// </para>
/// <para>
/// ⚠️ <b>L'ordre est celui du moteur lexique, et d'aucun autre.</b> Il était l'ordre d'arbitrage de
/// la taxonomie d'avant, du plus au moins coûteux à omettre, relu à travers la correspondance :
/// les deux valeurs fusionnées en <see cref="PersonalDataCategory.DemographicData"/> se rangent
/// <b>après</b> la santé, qui reste la seule catégorie de l'art. 9 que la taxonomie nomme. Un autre
/// moteur ne l'hérite pas — c'est pourquoi il vit ici et non sur la taxonomie.
/// </para>
/// </remarks>
internal static class LexiconTaxonomy
{
  private static readonly Dictionary<string, PersonalDataCategory> Retired = new(StringComparer.Ordinal)
  {
    ["ConnectionData"] = PersonalDataCategory.OnlineIdentifier,
    ["SpecialCategoryData"] = PersonalDataCategory.DemographicData,
    ["CriminalOffenceData"] = PersonalDataCategory.DemographicData,
    ["PersonalDataUncategorised"] = PersonalDataCategory.FreeTextAboutPerson,
  };

  /// <summary>Les seules catégories que le lexique et ses règles produisent, de la plus à la moins coûteuse à omettre.</summary>
  private static readonly PersonalDataCategory[] Order =
  [
    PersonalDataCategory.HealthData,
    PersonalDataCategory.DemographicData,
    PersonalDataCategory.AuthenticationSecret,
    PersonalDataCategory.NationalIdentifier,
    PersonalDataCategory.FinancialData,
    PersonalDataCategory.LocationData,
    PersonalDataCategory.OnlineIdentifier,
    PersonalDataCategory.Identity,
    PersonalDataCategory.ContactDetails,
    PersonalDataCategory.ProfessionalLife,
    PersonalDataCategory.FreeTextAboutPerson,
  ];

  /// <summary>La catégorie qu'un nom gelé désigne dans la taxonomie des prototypes.</summary>
  /// <exception cref="InvalidOperationException">Le nom n'est ni retiré, ni une valeur signalante de la taxonomie.</exception>
  internal static PersonalDataCategory FromFrozenName(string name)
  {
    if (Retired.TryGetValue(name, out var mapped))
    {
      return mapped;
    }

    if (PersonalDataCategory.TryFromName(name, ignoreCase: false, out var category) && Order.Contains(category))
    {
      return category;
    }

    throw new InvalidOperationException(
      $"Le lexique gelé porte la valeur « {name} », que ni la taxonomie ni la correspondance ne "
      + "connaissent. Un lexique n'est pas une configuration : il est le gel d413d55 ou il n'est rien.");
  }

  /// <summary>
  /// Parmi plusieurs catégories déclenchées, celle qui <b>coûte le plus cher à omettre</b> selon
  /// l'ordre du lexique. <c>arret_maladie</c> est santé <i>et</i> vie professionnelle ;
  /// <c>email_pro</c> est coordonnées <i>et</i> vie professionnelle.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Jamais la <see cref="RuleStrength"/>.</b> Comparer deux degrés pour désigner un gagnant
  /// serait un score qui produit une issue. Et le nom dit un ordre, jamais une issue : <c>Arbitrate</c>
  /// est réservé au geste de l'<c>Operator</c>.
  /// </remarks>
  internal static PersonalDataCategory MostCostlyToOmit(IEnumerable<PersonalDataCategory> triggered)
  {
    return triggered.MinBy(Rank)
      ?? throw new ArgumentException(
        "Aucune valeur n'a déclenché : l'absence de signalement s'écrit Unflagged, elle ne se départage pas.",
        nameof(triggered));
  }

  /// <summary>Le rang d'une catégorie dans l'ordre du lexique : plus il est bas, plus elle coûte cher à omettre.</summary>
  internal static int Rank(PersonalDataCategory category)
  {
    var rank = Array.IndexOf(Order, category);

    return rank >= 0
      ? rank
      : throw new InvalidOperationException(
        $"Le moteur lexique ne produit jamais « {category.Name} » : elle n'a pas de rang dans son ordre.");
  }
}
