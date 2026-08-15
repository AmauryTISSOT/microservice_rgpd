using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.UnitTests.Core.Screenings;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Screenings;

/// <summary>
/// Les cinq règles du montage gelé, une par une, sur des colonnes que le pivot témoin ne porte pas :
/// le conteneur libre, l'héritage par la table, la collision FR/EN, et le double déclenchement que
/// l'ordre d'arbitrage tranche.
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

    byComment.Category.ShouldBe(PersonalDataCategory.ConnectionData);
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
  /// pas de le lire. D'où le repli, qui est un verdict, et un motif — donc pas <c>Unflagged</c>.
  /// </summary>
  [Fact]
  public async Task FallsBackOnTheUncategorisedVerdictForAFreeContainer()
  {
    var line = await Screen(APivot.Column("donnees", table: "divers", dataType: "json"));

    line.Category.ShouldBe(PersonalDataCategory.PersonalDataUncategorised);
    line.Strength.ShouldBe(RuleStrength.TypeHeuristic);
    line.Reason.ShouldBe("conteneur libre : le contenu n'est pas lisible depuis le schéma");
  }

  /// <summary>
  /// Quand plusieurs valeurs déclenchent, c'est l'<b>ordre d'arbitrage</b> de la taxonomie qui
  /// tranche — du plus au moins coûteux à omettre — et jamais le degré. Le motif, lui, <b>dit ce qui
  /// a été écarté</b> : c'est cela qui s'arbitre.
  /// </summary>
  [Fact]
  public async Task SettlesSeveralTriggersByTheArbitrationOrderAndNamesWhatItSetAside()
  {
    var line = await Screen(APivot.Column("sante_salaire", table: "dossiers", dataType: "json"));

    line.Category.ShouldBe(PersonalDataCategory.HealthData);
    line.Strength.ShouldBe(RuleStrength.ExactName);
    line.Reason.ShouldBe(
      "jeton « sante » du nom de colonne, entrée du lexique "
      + "(a aussi déclenché : FinancialData, PersonalDataUncategorised)");
  }

  /// <summary>
  /// La <b>collision FR/EN</b> du montage : <c>conviction</c> vaut « catégorie particulière » en
  /// français et « infractions » en anglais, <c>coord</c> vaut « coordonnées » en français et
  /// « localisation » en anglais. Dans l'union, les deux entrées déclenchent, et c'est l'ordre
  /// d'arbitrage qui tranche — ce n'est pas un défaut du lexique à corriger.
  /// </summary>
  [Theory]
  [InlineData("conviction", "CriminalOffenceData")]
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
      async () => await AScreeningEngine.Wired().ScreenAsync(listing, cancelled.Token));
  }

  /// <summary>Le relevé absent est une programmation fautive, pas un relevé vide.</summary>
  [Fact]
  public async Task RefusesToScreenAnAbsentListing()
  {
    await Should.ThrowAsync<ArgumentNullException>(
      async () => await AScreeningEngine.Wired().ScreenAsync(null!));
  }

  /// <summary>Ce que le moteur dit d'une colonne collée seule.</summary>
  private static async Task<ScreenedColumn> Screen(string column)
  {
    var screened = await AScreeningEngine.Wired().ScreenAsync(Ingested(APivot.Paste(column)));

    return screened.Columns.Single();
  }

  private static ColumnListing Ingested(string paste)
  {
    var outcome = ColumnListingIngestion.Ingest(paste);

    outcome.Refusal.ShouldBeNull();

    return outcome.Listing!;
  }
}
