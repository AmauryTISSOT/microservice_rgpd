using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.UnitTests.Core.Screenings;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Screenings;

/// <summary>
/// Les cinq règles du montage gelé, une par une, sur des colonnes que le pivot témoin ne porte pas :
/// le conteneur libre, l'héritage par la table, la collision FR/EN, et le double déclenchement que
/// l'ordre interne du lexique tranche.
/// </summary>
/// <remarks>
/// <para>
/// <b>Les attendus sortent du montage gelé, jamais du portage</b> — mêmes colonnes passées à
/// <c>ReglesLexique</c> de <c>exploration/banc-screening/banc.py</c> sur les dictionnaires de
/// <c>d413d55</c>. Ce fichier complète
/// <see cref="ScreeningOnTheTemoinPivotTests"/>, qui prouve le portage sur un schéma entier ; ici,
/// chaque cas est là pour qu'une règle abîmée se dénonce <b>par son nom</b> plutôt qu'en décalant
/// une table de 72 lignes.
/// </para>
/// <para>
/// ⚠️ Ce ne sont pas des colonnes inventées pour flatter le moteur : deux d'entre elles rendent un
/// signalement <b>faux</b> — <c>reference_pays.libelle</c> hérite « coordonnées » d'un nom de table
/// qui désigne une nomenclature — et cela reste ce que le montage gelé rend.
/// </para>
/// </remarks>
public class ScreeningRulesTests
{
  /// <summary>
  /// Règle 1 — un jeton du nom de colonne <b>est</b> une entrée du lexique. C'est le degré le plus
  /// direct, et il se trompe encore à 70 % au banc : l'ordre a un sens, la promesse non.
  /// </summary>
  [Fact]
  public async Task RecognisesATokenOfTheColumnNameAsAnExactEntry()
  {
    var line = await Screen(APivot.Column("arret_maladie", table: "salaries", dataType: "date"));

    line.Category.ShouldBe(PersonalDataCategory.HealthData);
    line.Strength.ShouldBe(RuleStrength.ExactName);
    line.Reason.ShouldBe("jeton « maladie » du nom de colonne, entrée du lexique");
  }

  /// <summary>
  /// Règle 2 — un jeton <b>rapproché</b> d'une entrée par un préfixe qui couvre entièrement le plus
  /// court des deux. <c>ancien</c> et <c>anciennete</c> se rapprochent ; le degré dit lequel des
  /// deux chemins a parlé.
  /// </summary>
  [Fact]
  public async Task ApproachesATokenToAnEntryThatSharesItsPrefix()
  {
    var line = await Screen(APivot.Column("id_ancien", table: "clients_ancienne_boutique"));

    line.Category.ShouldBe(PersonalDataCategory.ProfessionalLife);
    line.Strength.ShouldBe(RuleStrength.Morphological);
    line.Reason.ShouldBe("jeton « ancien » rapproché de l'entrée « anciennete »");
  }

  /// <summary>
  /// Règle 3 — un mot du <b>commentaire de colonne</b> égal à une entrée. Le commentaire est la
  /// seule prose que le relevé porte, et c'est pourquoi son absence est nommée par le dialecte
  /// plutôt que laissée à lire comme un silence.
  /// </summary>
  [Fact]
  public async Task ReadsAWordOfTheColumnComment()
  {
    var line = await Screen(APivot.Column(
      "champ_libre",
      table: "divers",
      dataType: "text",
      columnComment: "Adresse postale du client"));

    line.Category.ShouldBe(PersonalDataCategory.ContactDetails);
    line.Strength.ShouldBe(RuleStrength.Morphological);
    line.Reason.ShouldBe("mot « adresse » du commentaire de colonne");
  }

  /// <summary>
  /// Règle 4 — l'<b>héritage du domaine par la table</b>, nom puis commentaire, et <b>seulement si
  /// rien n'a déclenché au niveau de la colonne</b>. Une colonne qui a déjà parlé n'emprunte pas la
  /// voix de sa table.
  /// </summary>
  [Fact]
  public async Task InheritsFromTheTableWhenTheColumnItselfSaidNothing()
  {
    var byName = await Screen(APivot.Column("libelle", table: "reference_pays", dataType: "varchar(60)"));

    byName.Category.ShouldBe(PersonalDataCategory.ContactDetails);
    byName.Strength.ShouldBe(RuleStrength.Morphological);
    byName.Reason.ShouldBe("héritage : jeton « pays » du nom de la table « reference_pays »");

    var byComment = await Screen(APivot.Column(
      "valeur",
      table: "journal",
      dataType: "text",
      tableComment: "Journal de connexion"));

    byComment.Category.ShouldBe(PersonalDataCategory.OnlineIdentifier);
    byComment.Reason.ShouldBe("héritage : mot « connexion » du commentaire de table");
  }

  /// <summary>
  /// ⚠️ L'héritage ne touche <b>jamais</b> les clés purement techniques. Sans cette exception, une
  /// table nommée <c>patients</c> rendrait sa clé primaire médicale, et le rapport signalerait un
  /// entier auto-incrémenté au titre de l'article 9.
  /// </summary>
  [Theory]
  [InlineData("id")]
  [InlineData("rowid")]
  [InlineData("ID")]
  public async Task NeverInheritsOnAPurelyTechnicalKey(string name)
  {
    var line = await Screen(APivot.Column(name, table: "patient", dataType: "int(11)"));

    line.Category.ShouldBe(PersonalDataCategory.Unflagged);
    line.Reason.ShouldBeNull();
    line.Strength.ShouldBeNull();
  }

  /// <summary>
  /// Le témoin qui donne son sens au précédent : dans la <b>même</b> table <c>patient</c>, une
  /// colonne qui n'est pas une clé technique hérite bien — et une colonne qui a parlé d'elle-même
  /// n'hérite pas, parce que l'héritage ne se déclenche qu'à défaut.
  /// </summary>
  [Fact]
  public async Task InheritsInThatSameTableForAnythingThatIsNotATechnicalKey()
  {
    var inherited = await Screen(APivot.Column("reference", table: "patient", dataType: "varchar(20)"));

    inherited.Category.ShouldBe(PersonalDataCategory.HealthData);
    inherited.Reason.ShouldBe("héritage : jeton « patient » du nom de la table « patient »");

    var spokeForItself = await Screen(APivot.Column("email", table: "patient", dataType: "varchar(190)"));

    spokeForItself.Category.ShouldBe(PersonalDataCategory.ContactDetails);
    spokeForItself.Reason.ShouldBe("jeton « email » du nom de colonne, entrée du lexique");
  }

  /// <summary>
  /// Règle 5 — le <b>conteneur libre</b>. C'est la seule règle qui lise le type, transférée
  /// nommément par #132, et elle ne dit pas ce que la colonne est : elle dit que le schéma ne permet
  /// pas de le lire. Elle range la colonne sous <c>FreeTextAboutPerson</c>, avec un motif — donc
  /// pas <c>Unflagged</c>.
  /// </summary>
  [Fact]
  public async Task FilesAFreeContainerUnderFreeTextAboutAPerson()
  {
    var line = await Screen(APivot.Column("donnees", table: "divers", dataType: "json"));

    line.Category.ShouldBe(PersonalDataCategory.FreeTextAboutPerson);
    line.Strength.ShouldBe(RuleStrength.TypeHeuristic);
    line.Reason.ShouldBe("conteneur libre : le contenu n'est pas lisible depuis le schéma");
  }

  /// <summary>
  /// Quand plusieurs valeurs déclenchent, c'est l'<b>ordre interne du lexique</b> qui tranche — du
  /// plus au moins coûteux à omettre — et jamais le degré. Le motif, lui, <b>dit ce qui a été
  /// écarté</b> : c'est cela qui s'arbitre.
  /// </summary>
  [Fact]
  public async Task SettlesSeveralTriggersByTheLexiconOrderAndNamesWhatItSetAside()
  {
    var line = await Screen(APivot.Column("sante_salaire", table: "dossiers", dataType: "json"));

    line.Category.ShouldBe(PersonalDataCategory.HealthData);
    line.Strength.ShouldBe(RuleStrength.ExactName);
    line.Reason.ShouldBe(
      "jeton « sante » du nom de colonne, entrée du lexique "
      + "(a aussi déclenché : FinancialData, FreeTextAboutPerson)");
  }

  /// <summary>
  /// <c>maladie_employeur</c> est santé <i>et</i> vie professionnelle. C'est la santé qui l'emporte,
  /// parce que c'est elle qui coûte le plus cher à omettre — jamais parce qu'une règle aurait été
  /// « plus sûre » qu'une autre.
  /// </summary>
  [Fact]
  public async Task SettlesTheHealthAndProfessionalCollisionOnHealth()
  {
    var line = await Screen(APivot.Column("maladie_employeur", table: "divers"));

    line.Category.ShouldBe(PersonalDataCategory.HealthData);
    line.Reason.ShouldBe(
      "jeton « maladie » du nom de colonne, entrée du lexique (a aussi déclenché : ProfessionalLife)");
  }

  /// <summary><c>email_employeur</c> est coordonnées <i>et</i> vie professionnelle : les coordonnées l'emportent.</summary>
  [Fact]
  public async Task SettlesTheContactAndProfessionalCollisionOnContactDetails()
  {
    var line = await Screen(APivot.Column("email_employeur", table: "divers"));

    line.Category.ShouldBe(PersonalDataCategory.ContactDetails);
    line.Reason.ShouldBe(
      "jeton « email » du nom de colonne, entrée du lexique (a aussi déclenché : ProfessionalLife)");
  }

  /// <summary>L'ordre dans lequel les jetons se présentent ne change rien : le départage est celui du lexique.</summary>
  [Fact]
  public async Task SettlesTheSameWayWhateverOrderTheTokensCameIn()
  {
    var forwards = await Screen(APivot.Column("employeur_nom_email", table: "divers"));
    var backwards = await Screen(APivot.Column("email_nom_employeur", table: "divers"));

    forwards.Category.ShouldBe(PersonalDataCategory.Identity);
    backwards.Category.ShouldBe(forwards.Category);
    backwards.Reason.ShouldBe(forwards.Reason);
  }

  /// <summary>
  /// ⚠️ <b>Les deux valeurs d'art. 9 et 10 retirées se rangent après la santé.</b> Relues à travers la
  /// correspondance, elles tombent toutes deux sur <c>DemographicData</c> ; la santé, seul item de
  /// l'art. 9 que la taxonomie nomme encore, garde la tête — et le motif ne nomme la catégorie
  /// écartée qu'une fois, même quand deux entrées d'avant y ont conduit.
  /// </summary>
  [Fact]
  public async Task RanksTheMergedDemographicDataAfterHealthAndNamesItOnce()
  {
    var line = await Screen(APivot.Column("casier_religion_maladie", table: "divers"));

    line.Category.ShouldBe(PersonalDataCategory.HealthData);
    line.Reason.ShouldBe(
      "jeton « maladie » du nom de colonne, entrée du lexique (a aussi déclenché : DemographicData)");
  }

  /// <summary>
  /// Les valeurs retirées du lexique gelé se lisent <b>à travers la correspondance</b> : le fichier
  /// n'est pas réécrit, et la ligne ne porte jamais que la taxonomie des prototypes.
  /// </summary>
  [Theory]
  [InlineData("casier", "DemographicData")]
  [InlineData("religion", "DemographicData")]
  [InlineData("cookie", "OnlineIdentifier")]
  [InlineData("confidentiel", "FreeTextAboutPerson")]
  public async Task ReadsTheRetiredValuesOfTheFrozenLexiconThroughTheCorrespondence(string name, string expected)
  {
    var line = await Screen(APivot.Column(name, table: "divers", dataType: "varchar(60)"));

    line.Category.Name.ShouldBe(expected);
    line.Strength.ShouldBe(RuleStrength.ExactName);
  }

  /// <summary>
  /// La <b>collision FR/EN</b> du montage : <c>conviction</c> vaut « catégorie particulière » en
  /// français et « infractions » en anglais — deux valeurs que la correspondance ramène toutes deux
  /// à <c>DemographicData</c> —, <c>coord</c> vaut « coordonnées » en français et « localisation » en
  /// anglais. Dans l'union, les deux entrées déclenchent, et c'est l'ordre du lexique qui tranche —
  /// ce n'est pas un défaut du lexique à corriger.
  /// </summary>
  [Theory]
  [InlineData("conviction", "DemographicData")]
  [InlineData("coord", "LocationData")]
  public async Task SettlesTheFrenchEnglishCollisionsByTheSameOrder(string name, string expected)
  {
    var line = await Screen(APivot.Column(name, table: "divers", dataType: "varchar(60)"));

    line.Category.Name.ShouldBe(expected);
    line.Reason.ShouldBe($"jeton « {name} » du nom de colonne, entrée du lexique");
  }

  /// <summary>
  /// La découpe est <b>générique</b> : séparateurs, <c>camelCase</c>, accents et chiffres de bord.
  /// Aucune connaissance du corpus ne vit dans le code — elle est tout entière dans les fichiers
  /// gelés.
  /// </summary>
  [Fact]
  public async Task CutsIdentifiersOnSeparatorsCaseAccentsAndEdgeDigits()
  {
    var line = await Screen(APivot.Column("photoIdentite2", table: "divers"));

    line.Category.ShouldBe(PersonalDataCategory.Identity);
    line.Reason.ShouldBe(
      "jeton « identite » du nom de colonne, entrée du lexique; "
      + "jeton « photo » du nom de colonne, entrée du lexique");
  }

  /// <summary>
  /// Une colonne où <b>rien n'a été vu</b> n'a ni motif ni degré — et ce n'est pas un verdict
  /// d'innocuité : le service n'a jamais vu la donnée.
  /// </summary>
  [Fact]
  public async Task SaysNothingWasSeenRatherThanNothingIsThere()
  {
    var line = await Screen(APivot.Column("prix_centimes", table: "annonces", dataType: "int(11)"));

    line.Category.ShouldBe(PersonalDataCategory.Unflagged);
    line.Category.IsFlagged.ShouldBeFalse();
    line.Reason.ShouldBeNull();
    line.Strength.ShouldBeNull();
  }

  /// <summary>
  /// ⚠️ <b>Le seul endroit où ce moteur s'écarte du montage gelé</b>, et il s'y écarte parce que le
  /// domaine borne la prose d'un motif là où le banc ne la bornait pas : un commentaire de colonne
  /// qui porte cinquante mots du lexique ferait autrement lever
  /// <c>ScreenedColumn.Flagged</c> et emporterait le relevé <b>entier</b> — un rapport qui n'existe
  /// pas, pour un motif trop long à lire.
  /// <para>
  /// La coupe <b>se dit</b> : un motif tronqué en silence se lirait comme entier, ce qui est
  /// l'<c>Omission silencieuse</c> déplacée dans la prose.
  /// </para>
  /// </summary>
  [Fact]
  public async Task SaysSoWhenAReasonHadToBeCutToFitTheDomainsCeiling()
  {
    var everyHealthWordAtOnce =
      "sante maladie medical medicament pathologie diagnostic allergie allergene handicap "
      + "invalidite hospitalisation hopital vaccin vaccination mutuelle grossesse sanguin sang "
      + "ordonnance symptome antecedent psy psychiatrique imc depistage serologie vih covid cancer "
      + "diabete soin infirmier medecin consultation ald rqth cpam prothese tabac alcool poids "
      + "glycemie vitale health med meds diagnosis dx disease illness";

    var line = await Screen(APivot.Column(
      "champ_libre",
      table: "divers",
      dataType: "text",
      columnComment: everyHealthWordAtOnce));

    line.Category.ShouldBe(PersonalDataCategory.HealthData);
    line.Reason!.Length.ShouldBe(ScreenedColumn.MaxReasonLength);
    line.Reason.ShouldEndWith("… (motif abrégé : d'autres règles ont déclenché sur cette colonne)");
    line.Reason.ShouldStartWith("mot « alcool » du commentaire de colonne; mot « ald » du commentaire de colonne");
  }

  /// <summary>
  /// Le moteur <b>propage l'annulation</b> : un relevé détecté pour quelqu'un qui est parti occupe
  /// la place de celui qui est resté.
  /// </summary>
  [Fact]
  public async Task StopsWhenTheCallerHasLeft()
  {
    using var cancelled = new CancellationTokenSource();
    await cancelled.CancelAsync();

    var listing = Ingested(APivot.Paste(APivot.Column("email", table: "clients")));

    await Should.ThrowAsync<OperationCanceledException>(
      async () => await AScreeningEngine.Wired().ScreenAsync(listing, IScreeningEngine.NoPreviews, cancelled.Token));
  }

  /// <summary>Le relevé absent est une programmation fautive, pas un relevé vide.</summary>
  [Fact]
  public async Task RefusesToScreenAnAbsentListing()
  {
    await Should.ThrowAsync<ArgumentNullException>(
      async () => await AScreeningEngine.Wired().ScreenAsync(null!, IScreeningEngine.NoPreviews));
  }

  // ─────────────────────────────────────────────────────────────────────────────────────────────
  // Les six règles de FORME. Elles lisent les valeurs d'un aperçu — jamais un nom, jamais un
  // lexique. ⚠️ Elles ne peuvent qu'AJOUTER un signalement : aucune ne sait en retirer un, et la
  // liste des déclenchements ne sait qu'accueillir.
  // ─────────────────────────────────────────────────────────────────────────────────────────────

  /// <summary>
  /// L'IBAN porte une <b>clé de contrôle</b> — mod 97 — et se contente donc de <b>deux</b> valeurs
  /// comptées : deux clés distinctes qui se vérifient sont deux observations indépendantes, à une
  /// chance sur dix milliards.
  /// </summary>
  [Fact]
  public async Task FlagsTheFinancialDataOfAColumnWhoseValuesCarryAnIbanCheckKey()
  {
    var line = await ScreenPreviewed(
      AMuteColumn,
      "FR1420041010050500013M02606",
      "FR7630006000011234567890189",
      "sans objet",
      "a renseigner",
      "inconnu");

    line.Category.ShouldBe(PersonalDataCategory.FinancialData);
    line.Strength.ShouldBe(RuleStrength.CheckedValueForm);
    line.Reason.ShouldBe("certaines des valeurs lues portent une clé de contrôle d'IBAN");
  }

  /// <summary>
  /// ⚠️ <b>Une seule clé ne suffit pas, et le prix est assumé</b> : une colonne dont une seule
  /// valeur distincte est un IBAN ne signale pas par la forme. Le seuil est attaché au degré, et
  /// non à un réglage qu'on baisserait un jour de déploiement pressé.
  /// </summary>
  [Fact]
  public async Task SaysNothingOfAColumnWhereASingleCheckedValueReachedTheThreshold()
  {
    var line = await ScreenPreviewed(
      AMuteColumn,
      "FR1420041010050500013M02606",
      "sans objet",
      "a renseigner",
      "inconnu",
      "neant");

    line.IsFlagged.ShouldBeFalse();
    line.Strength.ShouldBeNull();
  }

  /// <summary>
  /// ⚠️ <b>Trois valeurs ne comptent nulle part</b> — ni au numérateur, ni au dénominateur : le
  /// <c>NULL</c>, la valeur <b>tronquée</b> par le SGBD, et le <b>doublon</b> d'une valeur déjà
  /// comptée. Ici, le seul IBAN distinct ne fait pas deux parce qu'on l'a lu deux fois.
  /// </summary>
  [Fact]
  public async Task CountsNeitherNullNorTruncatedNorDuplicateValues()
  {
    var line = await ScreenPreviewed(
      AMuteColumn,
      PreviewedValue.Of("FR1420041010050500013M02606", actualLength: 27),
      PreviewedValue.Of("FR1420041010050500013M02606", actualLength: 27),
      PreviewedValue.NullValue,
      PreviewedValue.Of("FR76300060000112345678901", actualLength: 27),
      PreviewedValue.NullValue);

    line.IsFlagged.ShouldBeFalse();
  }

  /// <summary>
  /// Les mêmes trois valeurs ne comptent pas davantage au <b>dénominateur</b> : deux IBAN distincts
  /// noyés dans des <c>NULL</c> déclenchent, et le motif dit « toutes » parce que toutes les valeurs
  /// <i>comptées</i> les portent.
  /// </summary>
  [Fact]
  public async Task ReadsTheThresholdAgainstTheCountedValuesRatherThanTheReadOnes()
  {
    var line = await ScreenPreviewed(
      AMuteColumn,
      PreviewedValue.Of("FR1420041010050500013M02606", actualLength: 27),
      PreviewedValue.NullValue,
      PreviewedValue.Of("FR7630006000011234567890189", actualLength: 27),
      PreviewedValue.NullValue,
      PreviewedValue.NullValue);

    line.Category.ShouldBe(PersonalDataCategory.FinancialData);
    line.Reason.ShouldBe("toutes les valeurs lues portent une clé de contrôle d'IBAN");
  }

  /// <summary>
  /// ⚠️ <b>Une clé de contrôle fausse n'est pas une clé de contrôle</b> : c'est très exactement ce
  /// que la règle achète en échange de son seuil bas. Cinq chaînes qui ont la <i>tête</i> d'un IBAN
  /// ne déclenchent rien.
  /// </summary>
  [Fact]
  public async Task NeverTriggersOnAFalseIbanCheckKey()
  {
    var line = await ScreenPreviewed(
      AMuteColumn,
      "FR1420041010050500013M02607",
      "FR7630006000011234567890188",
      "FR7630001007941234567890186",
      "FR0000000000000000000000000",
      "FR1111111111111111111111111");

    line.IsFlagged.ShouldBeFalse();
  }

  /// <summary>
  /// Le NIR — et le NIA qui partage sa forme — porte lui aussi une clé mod 97. La Corse y met ses
  /// deux départements en lettres, <c>2A</c> et <c>2B</c>, qui se lisent 19 et 18 <b>moins un
  /// million</b> : c'est la règle officielle, et non un ajustement.
  /// </summary>
  [Theory]
  [InlineData("255017511600157", "180027511600162")]
  [InlineData("199092A00100557", "255017511600157")]
  public async Task FlagsTheNationalIdentifierOfAColumnWhoseValuesCarryASocialSecurityCheckKey(
    string first,
    string second)
  {
    var line = await ScreenPreviewed(AMuteColumn, first, second);

    line.Category.ShouldBe(PersonalDataCategory.NationalIdentifier);
    line.Strength.ShouldBe(RuleStrength.CheckedValueForm);
    line.Reason.ShouldBe(
      "toutes les valeurs lues portent une clé de contrôle de numéro de sécurité sociale");
  }

  [Fact]
  public async Task SaysNothingOfASingleSocialSecurityNumber()
  {
    var line = await ScreenPreviewed(AMuteColumn, "255017511600157", "sans objet");

    line.IsFlagged.ShouldBeFalse();
  }

  [Fact]
  public async Task NeverTriggersOnAFalseSocialSecurityCheckKey()
  {
    var line = await ScreenPreviewed(AMuteColumn, "255017511600158", "180027511600163");

    line.IsFlagged.ShouldBeFalse();
  }

  /// <summary>
  /// Le SIREN et le SIRET passent par <b>Luhn</b>, et le SIRET doit en plus porter un SIREN valide
  /// dans ses neuf premiers chiffres — sans quoi quatorze chiffres quelconques passeraient une fois
  /// sur dix.
  /// </summary>
  [Theory]
  [InlineData("552100554", "732829320")]
  [InlineData("73282932000074", "552100554")]
  public async Task FlagsTheProfessionalLifeOfAColumnWhoseValuesCarryASirenOrSiretCheckKey(
    string first,
    string second)
  {
    var line = await ScreenPreviewed(AMuteColumn, first, second);

    line.Category.ShouldBe(PersonalDataCategory.ProfessionalLife);
    line.Strength.ShouldBe(RuleStrength.CheckedValueForm);
    line.Reason.ShouldBe("toutes les valeurs lues portent une clé de contrôle de SIREN ou de SIRET");
  }

  [Fact]
  public async Task SaysNothingOfASingleSiren()
  {
    var line = await ScreenPreviewed(AMuteColumn, "552100554", "sans objet");

    line.IsFlagged.ShouldBeFalse();
  }

  /// <summary>
  /// ⚠️ Un SIRET dont la clé passe mais dont le <b>SIREN interne</b> ne passe pas n'est pas un
  /// SIRET — et <c>000000000</c>, que Luhn déclare valide, est une colonne d'entiers non
  /// renseignés, jamais une vie professionnelle.
  /// </summary>
  [Theory]
  [InlineData("55210055500018", "73282932000075")]
  [InlineData("000000000", "000000000000000")]
  public async Task NeverTriggersOnAFalseSirenOrSiretCheckKey(string first, string second)
  {
    var line = await ScreenPreviewed(AMuteColumn, first, second);

    line.IsFlagged.ShouldBeFalse();
  }

  /// <summary>
  /// ⚠️ <b>Sans clé de contrôle, le seuil est « toutes les valeurs comptées ».</b> Une forme ne
  /// gagne rien à se répéter : « cinq sur cinq » n'est qu'une seule observation affichée cinq fois,
  /// et ce qui porte la règle est qu'<b>aucune</b> valeur comptée ne la contredise.
  /// </summary>
  [Fact]
  public async Task FlagsTheContactDetailsOfAColumnWhoseValuesAllLookLikeAnEmailAddress()
  {
    var line = await ScreenPreviewed(
      AMuteColumn,
      "camille.durand@example.org",
      "s.bernard@example.fr",
      "contact@example.com");

    line.Category.ShouldBe(PersonalDataCategory.ContactDetails);
    line.Strength.ShouldBe(RuleStrength.ValueForm);
    line.Reason.ShouldBe("toutes les valeurs lues ont la forme d'une adresse de courriel");
  }

  /// <summary>Une seule intruse suffit à éteindre une règle sans clé de contrôle.</summary>
  [Fact]
  public async Task SaysNothingWhenOneCountedValueContradictsTheForm()
  {
    var line = await ScreenPreviewed(
      AMuteColumn,
      "camille.durand@example.org",
      "s.bernard@example.fr",
      "a renseigner");

    line.IsFlagged.ShouldBeFalse();
  }

  [Fact]
  public async Task FlagsTheContactDetailsOfAColumnWhoseValuesAllLookLikeAFrenchTelephoneNumber()
  {
    var line = await ScreenPreviewed(AMuteColumn, "+33 6 12 34 56 78", "01.42.68.53.00");

    line.Category.ShouldBe(PersonalDataCategory.ContactDetails);
    line.Strength.ShouldBe(RuleStrength.ValueForm);
    line.Reason.ShouldBe("toutes les valeurs lues ont la forme d'un numéro de téléphone français");
  }

  [Fact]
  public async Task FlagsTheOnlineIdentifierOfAColumnWhoseValuesAllLookLikeAnIpAddress()
  {
    var line = await ScreenPreviewed(AMuteColumn, "192.168.1.14", "2001:db8::8a2e:370:7334");

    line.Category.ShouldBe(PersonalDataCategory.OnlineIdentifier);
    line.Strength.ShouldBe(RuleStrength.ValueForm);
    line.Reason.ShouldBe("toutes les valeurs lues ont la forme d'une adresse IP");
  }

  /// <summary>
  /// ⚠️ <b>Une règle de forme ne peut qu'AJOUTER un signalement, jamais en retirer un.</b> Une
  /// colonne <c>email</c> dont les cinq valeurs lues sont vides reste signalée, <b>au même degré et
  /// dans la même catégorie</b> : les valeurs muettes ne sont pas un démenti, et un moteur qui
  /// déciderait le contraire aurait rétabli l'<c>Omission silencieuse</c> par le contenu.
  /// </summary>
  [Fact]
  public async Task NeverRemovesAFlagRaisedByTheNameWhenTheValuesSayNothing()
  {
    var byTheNameAlone = await Screen(APivot.Column("email", table: "t1"));

    var withMuteValues = await ScreenPreviewed(
      APivot.Column("email", table: "t1"),
      PreviewedValue.EmptyText,
      PreviewedValue.EmptyText,
      PreviewedValue.EmptyText,
      PreviewedValue.EmptyText,
      PreviewedValue.EmptyText);

    withMuteValues.IsFlagged.ShouldBeTrue();
    withMuteValues.Category.ShouldBe(byTheNameAlone.Category);
    withMuteValues.Strength.ShouldBe(byTheNameAlone.Strength);
    withMuteValues.Reason.ShouldBe(byTheNameAlone.Reason);
  }

  /// <summary>
  /// ⚠️ <b>Il n'y a pas de degré « corroboré ».</b> Une colonne <c>iban</c> dont les valeurs sont
  /// des IBAN porte <see cref="RuleStrength.ExactName"/> — celui de la règle qui parle le plus
  /// directement —, et c'est son <b>motif</b> qui porte les deux phrases. Un degré qui monterait
  /// parce que deux règles ont déclenché serait une quantité de preuve, c'est-à-dire un score par un
  /// autre chemin.
  /// </summary>
  [Fact]
  public async Task PutsTheNameBeforeTheFormWhenBothSpokeOfTheSameColumn()
  {
    var line = await ScreenPreviewed(
      APivot.Column("iban", table: "t1"),
      "FR1420041010050500013M02606",
      "FR7630006000011234567890189");

    line.Category.ShouldBe(PersonalDataCategory.FinancialData);
    line.Strength.ShouldBe(RuleStrength.ExactName);
    line.Reason.ShouldBe(
      "jeton « iban » du nom de colonne, entrée du lexique; "
      + "toutes les valeurs lues portent une clé de contrôle d'IBAN");
  }

  /// <summary>
  /// ⚠️ <b>Le motif ne porte ni chiffre, ni valeur lue.</b> Il est <b>enregistré</b> et vit aussi
  /// longtemps que le <c>Screening</c> : une valeur recopiée dedans survivrait à l'aperçu qui l'a
  /// montrée, et ferait tomber <c>Rien de réel ne reste</c> par le plus court des chemins. Quant à
  /// « cinq sur cinq », il se comparerait d'une ligne à l'autre alors que les deux nombres ne
  /// veulent pas dire la même chose.
  /// </summary>
  [Fact]
  public async Task WritesNoDigitAndNoReadValueInTheMotifOfAFormRule()
  {
    var line = await ScreenPreviewed(
      APivot.Column("iban", table: "t1"),
      "FR1420041010050500013M02606",
      "FR7630006000011234567890189");

    var reason = line.Reason.ShouldNotBeNull();

    reason.ShouldNotContain("FR14");
    reason.ShouldNotContain("FR76");
    reason.ShouldAllBe(character => !char.IsAsciiDigit(character));
  }

  /// <summary>
  /// ⚠️ <b>Le motif ne parle jamais de ce qui n'a pas déclenché.</b> Une phrase disant qu'une règle
  /// a failli serait une quantité de preuve rendue par la prose, et l'<c>Operator</c> lirait
  /// « certaines valeurs ressemblent à… » comme un signalement de second rang.
  /// </summary>
  [Fact]
  public async Task SaysNothingOfTheFormRulesThatDidNotTrigger()
  {
    var line = await ScreenPreviewed(
      APivot.Column("iban", table: "t1"),
      "FR1420041010050500013M02606",
      "FR7630006000011234567890189");

    var reason = line.Reason.ShouldNotBeNull();

    reason.ShouldNotContain("courriel");
    reason.ShouldNotContain("téléphone");
    reason.ShouldNotContain("adresse IP");
    reason.ShouldNotContain("sécurité sociale");
  }

  /// <summary>
  /// L'aperçu <b>absent</b> ne fait pas taire les règles de nom, et sa raison est <b>reportée sur la
  /// ligne</b> : c'est ce qui distingue « on a regardé et il n'y avait rien » de « on n'a pas pu
  /// regarder ».
  /// </summary>
  [Fact]
  public async Task CarriesTheReasonWhyNoValueCouldBeReadOntoTheScreenedColumn()
  {
    var line = await ScreenPreviewed(
      APivot.Column("email", table: "t1"),
      ColumnPreview.Absent(PreviewAbsenceReason.AccessDenied));

    line.IsFlagged.ShouldBeTrue();
    line.PreviewAbsence.ShouldBe(PreviewAbsenceReason.AccessDenied);
  }

  /// <summary>
  /// ⚠️ <b>Le chemin collé rend exactement ce qu'il rendait</b>, et il le <b>déclare</b> : ses
  /// formes existent, et elles n'ont rien lu.
  /// </summary>
  [Fact]
  public async Task DeclaresItsFormsInactiveWhenNoPreviewReachedIt()
  {
    var pasted = await AScreeningEngine.Wired().ScreenAsync(
      Ingested(APivot.Paste(APivot.Column("email", table: "t1"))),
      IScreeningEngine.NoPreviews);

    pasted.Engine.Version.ShouldBe("regles-2+formes-inactives+lexiques-d413d55");
  }

  /// <summary>Un aperçu, fût-il muet, fait tourner les formes — et l'identité le dit.</summary>
  [Fact]
  public async Task DeclaresItsFormsActiveAsSoonAsOnePreviewReachedIt()
  {
    var listing = Ingested(APivot.Paste(APivot.Column("email", table: "t1")));

    var scanned = await AScreeningEngine.Wired().ScreenAsync(
      listing,
      new Dictionary<ColumnIdentity, ColumnPreview>
      {
        [listing.Columns.Single().Identity] = ColumnPreview.Read([PreviewedValue.EmptyText]),
      });

    scanned.Engine.Version.ShouldBe("regles-2+formes-1+lexiques-d413d55");
  }

  /// <summary>Le dictionnaire d'aperçus absent est une programmation fautive, pas un chemin collé.</summary>
  [Fact]
  public async Task RefusesToScreenWithAnAbsentPreviewDictionary()
  {
    var listing = Ingested(APivot.Paste(APivot.Column("email", table: "t1")));

    await Should.ThrowAsync<ArgumentNullException>(
      async () => await AScreeningEngine.Wired().ScreenAsync(listing, null!));
  }

  /// <summary>
  /// ⚠️ <b>La règle du conteneur libre ne bouge pas d'un pouce.</b> Une colonne <c>jsonb</c> reste
  /// signalée <b>après</b> qu'on a lu cinq de ses valeurs : la règle ne dit pas ce que la colonne
  /// contient, elle dit que le schéma ne permet pas de le lire, et cinq valeurs n'y changent rien.
  /// L'éteindre parce qu'on a lu des valeurs serait laisser un aperçu <b>retirer</b> un signalement.
  /// </summary>
  [Fact]
  public async Task StillCallsAFreeContainerAFreeContainerAfterReadingFiveOfItsValues()
  {
    var line = await ScreenPreviewed(
      APivot.Column("c1", table: "t1", dataType: "jsonb"),
      "{\"a\":1}",
      "{\"a\":2}",
      "{\"a\":3}",
      "{\"a\":4}",
      "{\"a\":5}");

    line.Category.ShouldBe(PersonalDataCategory.FreeTextAboutPerson);
    line.Strength.ShouldBe(RuleStrength.TypeHeuristic);
    line.Reason.ShouldBe("conteneur libre : le contenu n'est pas lisible depuis le schéma");
  }

  /// <summary>
  /// ⚠️ <b>Une adresse IPv4 n'est jamais un SIREN.</b> Privée de ses points elle fait neuf chiffres,
  /// très exactement la longueur d'un SIREN, et une sur dix passerait Luhn — la colonne se
  /// signalerait alors <i>aussi</i> en <c>ProfessionalLife</c>, et le motif porterait un SIREN qui
  /// n'existe pas. Un SIREN ne s'écrit pas avec des points, et la règle ne les lit pas.
  /// </summary>
  [Fact]
  public async Task NeverReadsAnIpv4AddressAsASiren()
  {
    var line = await ScreenPreviewed(AMuteColumn, "212.27.48.10", "195.154.140.1");

    line.Category.ShouldBe(PersonalDataCategory.OnlineIdentifier);
    line.Strength.ShouldBe(RuleStrength.ValueForm);
    line.Reason.ShouldBe("toutes les valeurs lues ont la forme d'une adresse IP");
  }

  /// <summary>
  /// L'humain écrit les identifiants à clé de contrôle avec des <b>espaces</b>, et la règle les lit
  /// ainsi — sans quoi elle raterait la moitié des colonnes qu'elle existe pour attraper.
  /// </summary>
  [Fact]
  public async Task ReadsTheCheckedFormsThroughTheSpacesAHumanPutsInThem()
  {
    var line = await ScreenPreviewed(
      AMuteColumn,
      "FR14 2004 1010 0505 0001 3M02 606",
      "FR76 3000 6000 0112 3456 7890 189");

    line.Category.ShouldBe(PersonalDataCategory.FinancialData);
    line.Reason.ShouldBe("toutes les valeurs lues portent une clé de contrôle d'IBAN");
  }

  /// <summary>
  /// Une ligne de colonne dont ni le nom, ni la table, ni le type ne disent rien : c'est le seul
  /// moyen de lire ce que les <b>valeurs</b> ont dit, et rien d'autre.
  /// </summary>
  private static string AMuteColumn => APivot.Column("c1", table: "t1", dataType: "text");

  /// <summary>Ce que le moteur dit d'une colonne dont on lui a fourni les valeurs.</summary>
  private static Task<ScreenedColumn> ScreenPreviewed(string column, params string[] values)
  {
    return ScreenPreviewed(
      column,
      ColumnPreview.Read([.. values.Select(value => PreviewedValue.Of(value, value.Length))]));
  }

  private static Task<ScreenedColumn> ScreenPreviewed(string column, params PreviewedValue[] values)
  {
    return ScreenPreviewed(column, ColumnPreview.Read(values));
  }

  private static async Task<ScreenedColumn> ScreenPreviewed(string column, ColumnPreview preview)
  {
    var listing = Ingested(APivot.Paste(column));

    var screened = await AScreeningEngine.Wired().ScreenAsync(
      listing,
      new Dictionary<ColumnIdentity, ColumnPreview> { [listing.Columns.Single().Identity] = preview });

    return screened.Columns.Single();
  }

  /// <summary>Ce que le moteur dit d'une colonne collée seule.</summary>
  private static async Task<ScreenedColumn> Screen(string column)
  {
    var screened = await AScreeningEngine.Wired().ScreenAsync(Ingested(APivot.Paste(column)), IScreeningEngine.NoPreviews);

    return screened.Columns.Single();
  }

  private static ColumnListing Ingested(string paste)
  {
    var outcome = ColumnListingIngestion.Ingest(paste);

    outcome.Refusal.ShouldBeNull();

    return outcome.Listing!;
  }
}
