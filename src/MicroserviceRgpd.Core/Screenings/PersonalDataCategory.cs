namespace MicroserviceRgpd.Core.Screenings;

/// <summary>
/// La taxonomie fermée de quinze valeurs dans laquelle une <see cref="ScreenedColumn"/> puise :
/// <b>les quatorze prototypes du modèle A2</b>, et <see cref="Unflagged"/>. Elle reconnaît
/// <b>toujours</b> une valeur : l'absence de signalement est elle-même une valeur nommée, jamais une
/// ligne absente.
/// <para>
/// Chaque membre porte son <b>nom canonique anglais</b>, son <b>libellé français attaché</b> et le
/// <b>nom du prototype</b> dont il vient — une seule source de vérité, aucune table de
/// correspondance parallèle à maintenir ailleurs.
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Elle n'est pas <c>DataSubjectRight</c> et ne s'y raccroche jamais.</b> Une colonne
/// « courriel » ne relève pas d'un droit plutôt qu'un autre, elle relève de tous. Elle est propre à
/// ce contexte, et son auteur n'est pas le RGPD.
/// </para>
/// <para>
/// ⚠️ <b>Elle ne porte plus les art. 9 (hors santé) et 10.</b> <c>CriminalOffenceData</c> et
/// <c>SpecialCategoryData</c> sont retirées avec le passage aux prototypes :
/// <see cref="DemographicData"/> couvre religion et nationalité sans rien dire de l'art. 9. Ce que la
/// détection ne sait pas voir se dit dans la clause d'incomplétude, jamais par une valeur.
/// </para>
/// <para>
/// ⚠️ <b>L'ordre de déclaration n'est pas un ordre d'arbitrage.</b> Il a cessé d'être une propriété
/// de la taxonomie : un moteur qui a besoin de départager ses propres déclenchements garde son ordre
/// chez lui, et ne l'impose à aucun autre.
/// </para>
/// </remarks>
public sealed class PersonalDataCategory : SmartEnum<PersonalDataCategory>
{
  /// <summary>Le nom, le prénom, la date de naissance, la photographie.</summary>
  public static readonly PersonalDataCategory Identity =
    new(nameof(Identity), 0, "état civil et identité", "identity");

  /// <summary>Une adresse postale, un courriel, un numéro de téléphone.</summary>
  public static readonly PersonalDataCategory ContactDetails =
    new(nameof(ContactDetails), 1, "coordonnées", "contact");

  /// <summary>Une position, un trajet, une géolocalisation.</summary>
  public static readonly PersonalDataCategory LocationData =
    new(nameof(LocationData), 2, "données de localisation", "location");

  /// <summary>Un identifiant attribué par l'État : NIR, numéro fiscal, numéro d'allocataire.</summary>
  public static readonly PersonalDataCategory NationalIdentifier =
    new(nameof(NationalIdentifier), 3, "identifiant national", "government_id");

  /// <summary>Un IBAN, un montant, un encours — la situation économique de la personne.</summary>
  public static readonly PersonalDataCategory FinancialData =
    new(nameof(FinancialData), 4, "données économiques et financières", "financial");

  /// <summary>Un mot de passe, une empreinte, un jeton — ce dont la fuite ouvre la porte au reste.</summary>
  public static readonly PersonalDataCategory AuthenticationSecret =
    new(nameof(AuthenticationSecret), 5, "secret d'authentification", "authentication");

  /// <summary>Une adresse IP, un identifiant de session, un pseudonyme, un identifiant de traceur.</summary>
  public static readonly PersonalDataCategory OnlineIdentifier =
    new(nameof(OnlineIdentifier), 6, "identifiant en ligne", "online_identifier");

  /// <summary>
  /// Le sexe, la nationalité, la religion. ⚠️ Elle ne dit rien de l'art. 9 : une colonne
  /// « confession » y tombe sans que la valeur la distingue d'une colonne « nationalité ».
  /// </summary>
  public static readonly PersonalDataCategory DemographicData =
    new(nameof(DemographicData), 7, "données démographiques", "demographic");

  /// <summary>L'emploi, l'employeur, le service, la carrière.</summary>
  public static readonly PersonalDataCategory ProfessionalLife =
    new(nameof(ProfessionalLife), 8, "vie professionnelle", "professional");

  /// <summary>Des habitudes, des préférences, un historique d'actions.</summary>
  public static readonly PersonalDataCategory BehaviouralData =
    new(nameof(BehaviouralData), 9, "données de comportement", "behavioural");

  /// <summary>La santé, seul item de l'art. 9 que la taxonomie nomme encore.</summary>
  public static readonly PersonalDataCategory HealthData =
    new(nameof(HealthData), 10, "données concernant la santé", "health");

  /// <summary>Un conjoint, un enfant, un contact d'urgence — une autre personne que celle de la ligne.</summary>
  public static readonly PersonalDataCategory RelatedPerson =
    new(nameof(RelatedPerson), 11, "personne liée", "relation");

  /// <summary>
  /// Une note, un commentaire, un conteneur libre — du texte qui peut dire n'importe quoi d'une
  /// personne. C'est aussi le logement d'une colonne <c>json</c>/<c>jsonb</c> : « conteneur libre :
  /// le contenu n'est pas lisible depuis le schéma » est un motif rédigeable, donc ce n'est pas
  /// <see cref="Unflagged"/>.
  /// </summary>
  public static readonly PersonalDataCategory FreeTextAboutPerson =
    new(nameof(FreeTextAboutPerson), 12, "texte libre sur une personne", "free_text");

  /// <summary>Une clé qui désigne une personne tenue ailleurs : <c>client_id</c>, <c>auteur_id</c>.</summary>
  public static readonly PersonalDataCategory PersonReference =
    new(nameof(PersonReference), 13, "référence à une personne", "person_link");

  /// <summary>
  /// La valeur rendue quand la détection <b>n'a rien signalé</b> sur une colonne.
  /// <para>
  /// ⚠️ <b>Elle dit ce que le service n'a pas fait, jamais ce que la colonne est.</b> Une colonne
  /// <see cref="Unflagged"/> n'est pas une colonne sans données personnelles — c'est une colonne où
  /// <b>rien n'a été vu</b>, ce qui est un constat sur la détection et non sur la donnée. Le service
  /// n'a jamais vu la donnée. C'est <c>Enregistré, jamais vérifié</c> appliqué au seul endroit de ce
  /// contexte où il serait tentant de l'oublier : une machine qui déclare une colonne inoffensive
  /// porte très exactement le témoignage qu'elle n'a pas les moyens de porter.
  /// </para>
  /// <para>
  /// ⚠️ <b>Il n'y a pas de second repli.</b> Une valeur « non personnelle » a été explicitement
  /// écartée : elle porterait un verdict d'innocuité sur une donnée que le service n'a jamais vue.
  /// </para>
  /// <para>
  /// La règle qui la départage des autres est mécanique et se teste : <b>motif présent ⇔ ce n'est
  /// pas <see cref="Unflagged"/></b>.
  /// </para>
  /// </summary>
  public static readonly PersonalDataCategory Unflagged =
    new(nameof(Unflagged), 14, "rien signalé", prototypeName: null);

  private PersonalDataCategory(string name, int value, string frenchLabel, string? prototypeName)
    : base(name, value)
  {
    FrenchLabel = frenchLabel;
    PrototypeName = prototypeName;
  }

  /// <summary>Le libellé destiné à l'<c>Operator</c>. Le français reste hors des identifiants.</summary>
  public string FrenchLabel { get; }

  /// <summary>
  /// Le nom du prototype du modèle A2 dont la valeur vient — <c>government_id</c>,
  /// <c>person_link</c>. <c>null</c> pour <see cref="Unflagged"/>, qui n'est proche d'aucun.
  /// </summary>
  public string? PrototypeName { get; }

  /// <summary>
  /// Cette valeur signale-t-elle quelque chose ? Vrai partout sauf <see cref="Unflagged"/>. C'est la
  /// moitié gauche de l'invariant du contexte — <b>motif présent ⇔ ce n'est pas
  /// <see cref="Unflagged"/></b> — et la seule lecture autorisée de « la ligne dit quelque chose ».
  /// </summary>
  public bool IsFlagged => this != Unflagged;
}
