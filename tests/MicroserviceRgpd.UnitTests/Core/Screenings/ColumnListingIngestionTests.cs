using System.Globalization;
using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UnitTests.Core.Screenings;

/// <summary>
/// L'ingestion du relevé : un collage est <b>entier ou refusé</b>. Il n'existe pas d'ingestion
/// partielle, parce qu'un <c>Screening</c> bâti sur 99 % d'un relevé se lirait comme complet et que
/// les colonnes manquantes seraient précisément celles que personne ne relirait jamais.
/// </summary>
public class ColumnListingIngestionTests
{
  /// <summary>
  /// Le cas nominal, et il porte deux promesses : l'ordre du relevé est celui du schéma, et l'en-tête
  /// est recopié tel quel — <c>Enregistré, jamais vérifié</c>, rien n'est vérifié.
  /// </summary>
  [Fact]
  public void AcceptsASincerePasteAndKeepsTheListingOrder()
  {
    var outcome = ColumnListingIngestion.Ingest(APivot.ASincerePaste());

    outcome.IsAccepted.ShouldBeTrue();

    var listing = outcome.Listing.ShouldNotBeNull();
    listing.Dialect.ShouldBe("postgresql");
    listing.Database.ShouldBe("galette_prod");
    listing.GeneratedOn.ShouldBe(APivot.GeneratedOn);
    listing.DeclaredColumnCount.ShouldBe(4);
    listing.Columns.Select(column => column.Identity.Column)
      .ShouldBe(["id_adh", "adr_l1", "id_cotis", "dt_naiss"]);
  }

  /// <summary>
  /// Les neuf champs arrivent sur la ligne : ceux qui portent un nom, et les trois qui n'entrent que
  /// pour écarter. Une absence est <c>null</c>, jamais la chaîne vide.
  /// </summary>
  [Fact]
  public void CopiesTheNineFieldsOfALineAsTheyWereGiven()
  {
    var outcome = ColumnListingIngestion.Ingest(APivot.Paste(
      APivot.Column(
        "email",
        table: "contacts",
        schema: "archive",
        position: 7,
        dataType: "varchar(320)",
        isNullable: false,
        columnComment: "courriel de contact",
        tableComment: "les contacts",
        referencedTable: "clients")));

    var column = outcome.Listing.ShouldNotBeNull().Columns.Single();

    column.Identity.ShouldBe(ColumnIdentity.Of("archive", "contacts", "email"));
    column.Position.ShouldBe(7);
    column.DataType.ShouldBe("varchar(320)");
    column.IsNullable.ShouldBe(false);
    column.ColumnComment.ShouldBe("courriel de contact");
    column.TableComment.ShouldBe("les contacts");
    column.ReferencedTable.ShouldBe("clients");
  }

  /// <summary>
  /// Un champ que le SGBD ne sait pas produire arrive <c>null</c> — SQLite ne rend aucun commentaire
  /// — et l'absence se compte pareil qu'elle s'écrive <c>null</c> ou vide.
  /// </summary>
  [Fact]
  public void ReadsAFieldTheDialectCannotProduceAsAnAbsence()
  {
    var outcome = ColumnListingIngestion.Ingest(APivot.Paste(
      APivot.Column("id_adh", columnComment: null, tableComment: null, isNullable: null)));

    var column = outcome.Listing.ShouldNotBeNull().Columns.Single();

    column.ColumnComment.ShouldBeNull();
    column.TableComment.ShouldBeNull();
    column.IsNullable.ShouldBeNull();
    column.CarriesAComment.ShouldBeFalse();
  }

  /// <summary>
  /// Les colonnes sont groupées par table — l'unité de travail de l'arbitrage — sans que le
  /// groupement ne réordonne quoi que ce soit : les tables viennent dans l'ordre où le relevé les
  /// rencontre, et leurs colonnes dans l'ordre du schéma.
  /// </summary>
  [Fact]
  public void GroupsColumnsByTableWithoutReorderingTheListing()
  {
    var listing = ColumnListingIngestion.Ingest(APivot.ASincerePaste()).Listing.ShouldNotBeNull();

    listing.Tables.Select(table => table.Identity.Table).ShouldBe(["adherents", "cotisations"]);
    listing.Tables[0].Columns.Select(column => column.Identity.Column)
      .ShouldBe(["id_adh", "adr_l1", "dt_naiss"]);
    listing.Tables[1].Columns.Select(column => column.Identity.Column).ShouldBe(["id_cotis"]);
  }

  /// <summary>Cas n° 1 — l'en-tête absent. Le premier ce qui arrive est une ligne de colonne : rien ne déclare le format.</summary>
  [Fact]
  public void RefusesAPasteWhoseHeaderIsMissing()
  {
    var outcome = ColumnListingIngestion.Ingest(
      string.Join('\n', APivot.Column("id_adh"), APivot.Footer(1)));

    outcome.ShouldBeRefusedFor(RefusalCause.MissingHeader, line: 1);
  }

  /// <summary>Cas n° 1 — l'en-tête illisible : ce n'est pas du JSON, et il n'y a rien à en tirer.</summary>
  [Fact]
  public void RefusesAPasteWhoseHeaderDoesNotParse()
  {
    var outcome = ColumnListingIngestion.Ingest(
      string.Join('\n', "-- relevé des colonnes", APivot.Column("id_adh"), APivot.Footer(1)));

    outcome.ShouldBeRefusedFor(RefusalCause.MissingHeader, line: 1);
  }

  /// <summary>Cas n° 1 — l'en-tête est du JSON, mais il ne dit pas de quel SGBD le relevé vient.</summary>
  [Fact]
  public void RefusesAHeaderThatDoesNotDeclareItsDialect()
  {
    var outcome = ColumnListingIngestion.Ingest(
      string.Join('\n', APivot.Header(dialect: string.Empty), APivot.Column("id_adh"), APivot.Footer(1)));

    outcome.ShouldBeRefusedFor(RefusalCause.MissingHeader, line: 1);
  }

  /// <summary>
  /// Cas n° 8 — une version de format qu'on ne connaît pas. Refuser plutôt que tenter est ce qui
  /// empêche un pivot d'une forme future d'être lu de travers et rendu comme complet.
  /// </summary>
  [Fact]
  public void RefusesAFormatVersionItDoesNotKnow()
  {
    var outcome = ColumnListingIngestion.Ingest(
      string.Join('\n', APivot.Header(format: "screening-pivot/2"), APivot.Column("id_adh"), APivot.Footer(1)));

    outcome.ShouldBeRefusedFor(RefusalCause.UnknownFormatVersion, line: 1);
  }

  /// <summary>
  /// Cas n° 2 — <b>le cas nommé du ticket</b> : le collage s'arrête au milieu, et la ligne de fin
  /// n'est jamais arrivée. C'est la troncature au presse-papier, et c'est elle que la ligne de fin
  /// existe pour rendre détectable.
  /// </summary>
  [Fact]
  public void RefusesAPasteThatStopsBeforeItsClosingLine()
  {
    var outcome = ColumnListingIngestion.Ingest(
      string.Join('\n', APivot.Header(), APivot.Column("id_adh"), APivot.Column("adr_l1", position: 2)));

    outcome.ShouldBeRefusedFor(RefusalCause.MissingClosingLine, line: 3);
  }

  /// <summary>Cas n° 2 — l'en-tête seul, sans rien derrière : la ligne de fin manque avant même qu'on parle des colonnes.</summary>
  [Fact]
  public void RefusesAPasteThatCarriesNothingButItsHeader()
  {
    var outcome = ColumnListingIngestion.Ingest(APivot.Header());

    outcome.ShouldBeRefusedFor(RefusalCause.MissingClosingLine, line: 1);
  }

  /// <summary>Cas n° 3 — la troncature au milieu : le compte annoncé dépasse les lignes reçues.</summary>
  [Fact]
  public void RefusesWhenFewerLinesArriveThanTheClosingLineAnnounced()
  {
    var outcome = ColumnListingIngestion.Ingest(APivot.Paste(
      declaredColumnCount: 3,
      APivot.Column("id_adh")));

    outcome.ShouldBeRefusedFor(RefusalCause.CountMismatch, line: 3);
  }

  /// <summary>
  /// Cas n° 3, dans l'autre sens — plus de lignes que le compte annoncé. C'est la signature de deux
  /// morceaux collés qui se recouvrent, et refuser dans les deux sens est ce qui la rend visible.
  /// </summary>
  [Fact]
  public void RefusesWhenMoreLinesArriveThanTheClosingLineAnnounced()
  {
    var outcome = ColumnListingIngestion.Ingest(APivot.Paste(
      declaredColumnCount: 1,
      APivot.Column("id_adh"),
      APivot.Column("adr_l1", position: 2)));

    outcome.ShouldBeRefusedFor(RefusalCause.CountMismatch, line: 4);
  }

  /// <summary>Cas n° 4 — une ligne de colonne qui ne s'analyse pas, et le refus nomme laquelle.</summary>
  [Fact]
  public void RefusesAColumnLineThatDoesNotParse()
  {
    var outcome = ColumnListingIngestion.Ingest(APivot.Paste(
      declaredColumnCount: 2,
      APivot.Column("id_adh"),
      "{\"schema\":\"public\",\"table\":"));

    outcome.ShouldBeRefusedFor(RefusalCause.UnreadableColumnLine, line: 3);
  }

  /// <summary>
  /// Cas n° 4 — une clé manque. Les neuf champs sont émis par la requête, <c>null</c> compris :
  /// une clé absente n'est pas une valeur absente, c'est une ligne qui ne vient pas de la requête.
  /// </summary>
  [Theory]
  [InlineData("schema")]
  [InlineData("table")]
  [InlineData("colonne")]
  [InlineData("position")]
  [InlineData("type")]
  [InlineData("nullable")]
  [InlineData("commentaire_colonne")]
  [InlineData("commentaire_table")]
  [InlineData("table_referencee")]
  public void RefusesAColumnLineMissingOneOfTheNineKeys(string missingKey)
  {
    var mutilated = RemoveKeyFrom(APivot.Column("id_adh"), missingKey);

    var outcome = ColumnListingIngestion.Ingest(APivot.Paste(mutilated));

    outcome.ShouldBeRefusedFor(RefusalCause.UnreadableColumnLine, line: 2);
  }

  /// <summary>
  /// Cas n° 4 — une clé présente mais <b>mal typée</b>. C'est le cas qui manquait, et il est le plus
  /// dangereux des quatre : l'<c>information_schema</c> de MariaDB rend la nullabilité en
  /// <c>'YES'</c>/<c>'NO'</c>, et un <c>"nullable":"YES"</c> lu comme une absence désactiverait le
  /// filtre de nullabilité sur <b>toute</b> une base, sans un mot, dans un rapport qui se lit comme
  /// complet.
  /// </summary>
  [Theory]
  [InlineData("\"nullable\":true", "\"nullable\":\"YES\"")]
  [InlineData("\"nullable\":true", "\"nullable\":1")]
  [InlineData("\"type\":\"varchar(255)\"", "\"type\":12")]
  [InlineData("\"commentaire_colonne\":null", "\"commentaire_colonne\":{}")]
  [InlineData("\"commentaire_table\":null", "\"commentaire_table\":[]")]
  [InlineData("\"table_referencee\":null", "\"table_referencee\":7")]
  [InlineData("\"position\":1", "\"position\":\"1\"")]
  [InlineData("\"colonne\":\"id_adh\"", "\"colonne\":null")]
  public void RefusesAColumnLineWhoseValueCarriesTheWrongJsonType(string sincere, string mistyped)
  {
    var mutilated = APivot.Column("id_adh").Replace(sincere, mistyped, StringComparison.Ordinal);

    mutilated.ShouldNotBe(APivot.Column("id_adh"));

    var outcome = ColumnListingIngestion.Ingest(APivot.Paste(mutilated));

    outcome.ShouldBeRefusedFor(RefusalCause.UnreadableColumnLine, line: 2);
  }

  /// <summary>Cas n° 4 — un nom vide ne désigne aucune colonne, et le triplet ne se forge pas dessus.</summary>
  [Fact]
  public void RefusesAColumnLineWhoseNameIsEmpty()
  {
    var outcome = ColumnListingIngestion.Ingest(APivot.Paste(APivot.Column(column: "  ")));

    outcome.ShouldBeRefusedFor(RefusalCause.UnreadableColumnLine, line: 2);
  }

  /// <summary>Cas n° 4 — un rang qui n'est pas un entier positif n'ordonne rien.</summary>
  [Fact]
  public void RefusesAColumnLineWhoseRankIsNotAWholeNumber()
  {
    var outcome = ColumnListingIngestion.Ingest(APivot.Paste(APivot.Column("id_adh", position: -1)));

    outcome.ShouldBeRefusedFor(RefusalCause.UnreadableColumnLine, line: 2);
  }

  /// <summary>
  /// Cas n° 5 — le trou dans les rangs d'une table : la troncature <b>au milieu</b>, celle qu'un
  /// compte global ne voit pas quand deux morceaux collés se recouvrent mal. Les trois dialectes
  /// rendent des rangs contigus ; un trou n'est jamais légitime.
  /// </summary>
  [Fact]
  public void RefusesAGapInTheRanksOfATable()
  {
    var outcome = ColumnListingIngestion.Ingest(APivot.Paste(
      APivot.Column("id_adh", position: 1),
      APivot.Column("dt_naiss", position: 3)));

    outcome.ShouldBeRefusedFor(RefusalCause.RankGap);
  }

  /// <summary>
  /// Deux tables ont chacune leurs rangs : que la seconde reparte de son propre début n'est pas un
  /// trou, et confondre les deux refuserait tous les relevés réels.
  /// </summary>
  [Fact]
  public void DoesNotSeeAGapWhereASecondTableSimplyStartsOver()
  {
    var outcome = ColumnListingIngestion.Ingest(APivot.Paste(
      APivot.Column("id_adh", position: 1),
      APivot.Column("id_cotis", table: "cotisations", position: 1)));

    outcome.IsAccepted.ShouldBeTrue();
  }

  /// <summary>
  /// Les rangs de SQLite partent de zéro là où ceux de PostgreSQL partent de un. Ce qui n'est
  /// jamais légitime est le <b>trou</b>, jamais le point de départ — exiger 1 refuserait tout SQLite.
  /// </summary>
  [Fact]
  public void AcceptsATableWhoseRanksStartAtZero()
  {
    var outcome = ColumnListingIngestion.Ingest(APivot.Paste(
      APivot.Column("id_adh", position: 0),
      APivot.Column("adr_l1", position: 1)));

    outcome.IsAccepted.ShouldBeTrue();
  }

  /// <summary>Cas n° 5 — deux lignes distinctes au même rang : la table n'a plus d'ordre, et l'écran non plus.</summary>
  [Fact]
  public void RefusesTwoColumnsOfATableSharingTheSameRank()
  {
    var outcome = ColumnListingIngestion.Ingest(APivot.Paste(
      APivot.Column("id_adh", position: 1),
      APivot.Column("adr_l1", position: 1)));

    outcome.ShouldBeRefusedFor(RefusalCause.RankGap);
  }

  /// <summary>
  /// Cas n° 6 — le doublon de triplet, signature de deux pivots collés bout à bout. Deux lignes qui
  /// le partagent parlent de la même colonne, et l'un des deux arbitrages écraserait l'autre.
  /// </summary>
  [Fact]
  public void RefusesTwoLinesSharingTheSameTriple()
  {
    var outcome = ColumnListingIngestion.Ingest(APivot.Paste(
      APivot.Column("id_adh", position: 1),
      APivot.Column("id_adh", position: 2)));

    outcome.ShouldBeRefusedFor(RefusalCause.DuplicateColumn, line: 3);
  }

  /// <summary>Le même nom dans deux schémas n'est pas un doublon : c'est ce que le couple (table, colonne) aurait confondu.</summary>
  [Fact]
  public void TellsApartTheSameColumnNameLivingInTwoSchemas()
  {
    var outcome = ColumnListingIngestion.Ingest(APivot.Paste(
      APivot.Column("email", schema: "public"),
      APivot.Column("email", schema: "archive")));

    outcome.IsAccepted.ShouldBeTrue();
  }

  /// <summary>
  /// Cas n° 7 — <b>le moins intuitif, et le plus important après la troncature</b> : un pivot vide et
  /// parfaitement formé est ce que rend une requête lancée contre le mauvais schéma, et il
  /// produirait un <c>Screening</c> vide et rassurant.
  /// </summary>
  [Fact]
  public void RefusesAPivotThatIsEmptyAndPerfectlyWellFormed()
  {
    var outcome = ColumnListingIngestion.Ingest(APivot.Paste());

    outcome.ShouldBeRefusedFor(RefusalCause.EmptyListing);
  }

  /// <summary>
  /// Cas n° 9 — au plafond, on refuse ; on ne tronque <b>jamais</b>. Tronquer en silence est
  /// l'<c>Omission silencieuse</c> sous sa forme la plus pure : un rapport complet en apparence sur
  /// une base vue aux deux tiers.
  /// </summary>
  [Fact]
  public void RefusesBeyondTheCeilingAndNeverTruncates()
  {
    var columns = Enumerable
      .Range(1, ColumnListing.MaxColumns + 1)
      .Select(rank => APivot.Column($"col_{rank}", position: rank))
      .ToArray();

    var outcome = ColumnListingIngestion.Ingest(APivot.Paste(columns));

    outcome.ShouldBeRefusedFor(RefusalCause.CeilingExceeded);
    outcome.Listing.ShouldBeNull();
  }

  /// <summary>
  /// Le plafond se lit sur le <b>compte annoncé</b>, et c'est ce qui rend le refus lisible avant
  /// qu'on ait analysé trente mille lignes : « ton relevé annonce 31 000 colonnes, le plafond est
  /// 20 000 ».
  /// </summary>
  [Fact]
  public void ReadsTheCeilingOnTheAnnouncedCountBeforeAnythingElse()
  {
    var outcome = ColumnListingIngestion.Ingest(APivot.Paste(
      declaredColumnCount: 31_000,
      APivot.Column("id_adh")));

    outcome.ShouldBeRefusedFor(RefusalCause.CeilingExceeded);
    outcome.Refusal.ShouldNotBeNull().Observed.ShouldContain("31000");
  }

  /// <summary>Le relevé exactement au plafond passe : la borne est incluse, sans quoi le nombre écrit n'est pas celui qui s'applique.</summary>
  [Fact]
  public void AcceptsAListingSittingExactlyOnTheCeiling()
  {
    var columns = Enumerable
      .Range(1, ColumnListing.MaxColumns)
      .Select(rank => APivot.Column($"col_{rank}", position: rank))
      .ToArray();

    ColumnListingIngestion.Ingest(APivot.Paste(columns)).IsAccepted.ShouldBeTrue();
  }

  /// <summary>
  /// Le refus <b>nomme</b> le numéro de ligne et ce qui était attendu. « Format invalide » est un
  /// refus qui pousse à recommencer au hasard, et l'<c>Operator</c> a fait un long trajet.
  /// </summary>
  [Fact]
  public void NamesTheLineAndWhatWasExpected()
  {
    var outcome = ColumnListingIngestion.Ingest(APivot.Paste(
      declaredColumnCount: 2,
      APivot.Column("id_adh"),
      "ceci n'est pas une ligne de colonne"));

    var refusal = outcome.Refusal.ShouldNotBeNull();
    refusal.LineNumber.ShouldBe(3);
    refusal.Cause.Expectation.ShouldNotBeNullOrWhiteSpace();
    refusal.Observed.ShouldNotBeNullOrWhiteSpace();
  }

  /// <summary>
  /// ⚠️ <b>Les trois champs du filtre sont collectés, et aucun n'alimente un signal.</b> Un
  /// <c>boolean</c>, un <c>decimal(10,2)</c>, une clé vers une table de référence <b>écartent</b> des
  /// catégories plutôt qu'ils n'en désignent une. Le premier banc qui mesurera leur pouvoir prédictif
  /// isolé conclura « inutiles » ; sans cette séparation écrite et testée, quelqu'un les retirera du
  /// pivot et supprimera le filtre du même geste.
  /// </summary>
  [Theory]
  [InlineData("type")]
  [InlineData("nullab")]
  [InlineData("référence")]
  public void CollectsTheThreeFilterFieldsWithoutEverFeedingASignal(string field)
  {
    var listing = ColumnListingIngestion.Ingest(APivot.ASincerePaste()).Listing.ShouldNotBeNull();

    listing.Columns.ShouldContain(column => column.DataType != null);
    listing.Columns.ShouldContain(column => column.IsNullable != null);
    listing.Columns.ShouldContain(column => column.ReferencedTable == "adherents");

    var perimeter = IncompletenessClause
      .For(AScreening.Of([.. listing.Columns.Select(ScreenedColumn.NothingSeen)]))
      .Perimeter;

    perimeter.ReadOnlyToFilter.ShouldContain(entry => entry.Contains(field, StringComparison.OrdinalIgnoreCase));
    perimeter.ReadAsSignal.ShouldNotContain(entry => entry.Contains(field, StringComparison.OrdinalIgnoreCase));
  }

  /// <summary>Les neuf cas de refus sont nommés, et chacun se distingue des huit autres.</summary>
  [Fact]
  public void NamesNineDistinctCauses()
  {
    RefusalCause.List.Count.ShouldBe(9);
    RefusalCause.List.Select(cause => cause.Value).Distinct().Count().ShouldBe(9);
    RefusalCause.List.ShouldAllBe(cause => !string.IsNullOrWhiteSpace(cause.Expectation));
  }

  /// <summary>Un tour par le presse-papier ajoute souvent une fin de ligne. Ce n'est pas une troncature.</summary>
  [Fact]
  public void ReadsAPasteThatTrailsBlankLines()
  {
    ColumnListingIngestion.Ingest(APivot.ASincerePaste() + "\r\n\r\n").IsAccepted.ShouldBeTrue();
  }

  /// <summary>
  /// Un collage passé par <c>Out-File</c>, le Bloc-notes ou un export SSMS porte une marque d'ordre
  /// d'octets. Ce n'est pas un blanc pour .NET, et sans un retrait explicite un pivot sincère se
  /// verrait répondre « en-tête absent ou illisible » à propos d'un en-tête correct — un refus qui
  /// nomme le mauvais problème fait relancer une requête qui rendra le même collage.
  /// </summary>
  [Fact]
  public void ReadsAPasteThatCarriesAByteOrderMark()
  {
    ColumnListingIngestion.Ingest('﻿' + APivot.ASincerePaste()).IsAccepted.ShouldBeTrue();
  }

  /// <summary>
  /// Les trois conventions de fin de ligne se lisent pareil, et les numéros de ligne restent ceux du
  /// collage : découper sur les deux caractères compterait chaque <c>CRLF</c> pour deux lignes et
  /// ferait citer à l'<c>Operator</c> une ligne qu'il ne trouverait pas.
  /// </summary>
  [Theory]
  [InlineData("\n")]
  [InlineData("\r\n")]
  [InlineData("\r")]
  public void ReadsThePasteWhateverItsLineEndingConvention(string ending)
  {
    var paste = APivot.ASincerePaste().Replace("\n", ending, StringComparison.Ordinal);

    ColumnListingIngestion.Ingest(paste).IsAccepted.ShouldBeTrue();
  }

  /// <summary>Et les numéros cités restent ceux du collage, quelle que soit la convention.</summary>
  [Fact]
  public void NamesTheLineOfTheOriginalPasteWhateverItsLineEndings()
  {
    var paste = APivot
      .Paste(2, APivot.Column("id_adh"), "pas une ligne de colonne")
      .Replace("\n", "\r\n", StringComparison.Ordinal);

    ColumnListingIngestion.Ingest(paste)
      .ShouldBeRefusedFor(RefusalCause.UnreadableColumnLine, line: 3);
  }

  /// <summary>
  /// ⚠️ <b>L'instant de génération doit porter son décalage.</b> Sans lui il prendrait celui du
  /// serveur : le même collage lu à Tokyo puis en UTC donnerait deux instants distants de neuf
  /// heures, et un relevé vieux d'une journée s'afficherait comme frais.
  /// </summary>
  [Theory]
  [InlineData("2026-08-10T09:30:00")]
  [InlineData("08/10/2026 09:30")]
  [InlineData("2026-08-10")]
  public void RefusesAHeaderWhoseInstantDoesNotCarryItsOffset(string instant)
  {
    var header = APivot.Header().Replace(
      APivot.GeneratedOn.ToString("O", CultureInfo.InvariantCulture),
      instant,
      StringComparison.Ordinal);

    var outcome = ColumnListingIngestion.Ingest(
      string.Join('\n', header, APivot.Column("id_adh"), APivot.Footer(1)));

    outcome.ShouldBeRefusedFor(RefusalCause.MissingHeader, line: 1);
  }

  /// <summary>Un décalage explicite est lu tel quel, <c>Z</c> compris — c'est ce que les trois requêtes émettent.</summary>
  [Theory]
  [InlineData("2026-08-10T09:30:00Z", 0)]
  [InlineData("2026-08-10T09:30:00+02:00", 2)]
  [InlineData("2026-08-10T09:30:00.0000000+00:00", 0)]
  public void KeepsTheOffsetTheHeaderDeclares(string instant, int offsetHours)
  {
    var header = APivot.Header().Replace(
      APivot.GeneratedOn.ToString("O", CultureInfo.InvariantCulture),
      instant,
      StringComparison.Ordinal);

    var listing = ColumnListingIngestion
      .Ingest(string.Join('\n', header, APivot.Column("id_adh"), APivot.Footer(1)))
      .Listing.ShouldNotBeNull();

    listing.GeneratedOn.Offset.ShouldBe(TimeSpan.FromHours(offsetHours));
    listing.GeneratedOn.UtcDateTime.ShouldBe(new DateTime(2026, 8, 10, 9 - offsetHours, 30, 0, DateTimeKind.Utc));
  }

  /// <summary>
  /// L'en-tête passe le <b>même garde que le domaine</b>. Sans cela, un <c>base</c> de 150 caractères
  /// — le chemin d'un fichier SQLite, cas courant — traverserait l'ingestion et ferait lever
  /// <c>Screening</c> : une erreur nue à la surface au lieu de l'un des neuf refus nommés.
  /// </summary>
  [Fact]
  public void RefusesAHeaderWhoseDatabaseNameIsBeyondWhatTheDomainAccepts()
  {
    var header = APivot.Header(database: new string('c', Screening.MaxDatabaseNameLength + 1));

    var outcome = ColumnListingIngestion.Ingest(
      string.Join('\n', header, APivot.Column("id_adh"), APivot.Footer(1)));

    outcome.ShouldBeRefusedFor(RefusalCause.MissingHeader, line: 1);
  }

  /// <summary>Et ce qu'elle accepte, le domaine l'accepte : c'est l'invariant que la frontière promet.</summary>
  [Fact]
  public void OnlyLetsThroughAHeaderTheDomainWillTake()
  {
    var listing = ColumnListingIngestion.Ingest(APivot.ASincerePaste()).Listing.ShouldNotBeNull();

    var screening = Screening.Of(
      ScreeningId.Next(),
      listing.Database,
      listing.Dialect,
      AScreening.Engine,
      listing.DeclaredColumnCount,
      listing.Columns.Select(ScreenedColumn.NothingSeen),
      APivot.GeneratedOn);

    screening.ColumnCount.ShouldBe(4);
  }

  /// <summary>
  /// Un compte annoncé qu'un <see cref="int"/> ne porte pas est un <b>plafond franchi</b>, jamais une
  /// ligne de fin illisible : répondre « troncature au presse-papier » à un collage qui n'en est pas
  /// une ferait recoller indéfiniment.
  /// </summary>
  [Fact]
  public void ReadsAnAnnouncedCountTooLargeForAnIntegerAsTheCeiling()
  {
    var paste = string.Join(
      '\n',
      APivot.Header(),
      APivot.Column("id_adh"),
      "{\"fin\":true,\"colonnes\":99999999999}");

    ColumnListingIngestion.Ingest(paste).ShouldBeRefusedFor(RefusalCause.CeilingExceeded);
  }

  /// <summary>Un compte écrit <c>412.0</c> reste un compte : c'est le compte qui tranche, pas son encodage.</summary>
  [Fact]
  public void ReadsAnAnnouncedCountWrittenWithADecimalPoint()
  {
    var paste = string.Join(
      '\n',
      APivot.Header(),
      APivot.Column("id_adh"),
      "{\"fin\":true,\"colonnes\":1.0}");

    ColumnListingIngestion.Ingest(paste).IsAccepted.ShouldBeTrue();
  }

  /// <summary>
  /// Le refus cite la ligne <b>où la suite se casse</b>, jamais la dernière de la table : sur une
  /// table de deux cents colonnes, la dernière ligne est parfaitement formée et l'<c>Operator</c>
  /// n'aurait aucun moyen de retrouver le trou.
  /// </summary>
  [Fact]
  public void NamesTheLineWhereTheRanksBreakRatherThanTheLastOfTheTable()
  {
    var outcome = ColumnListingIngestion.Ingest(APivot.Paste(
      APivot.Column("id_adh", position: 1),
      APivot.Column("adr_l1", position: 2),
      APivot.Column("dt_naiss", position: 4),
      APivot.Column("cp", position: 5),
      APivot.Column("ville", position: 6)));

    outcome.ShouldBeRefusedFor(RefusalCause.RankGap, line: 4);
  }

  /// <summary>
  /// Un collage qui s'arrête après l'en-tête ne cite pas l'en-tête comme une ligne de fin cassée :
  /// ce serait pointer l'<c>Operator</c> vers la seule ligne qui était correcte.
  /// </summary>
  [Fact]
  public void DoesNotBlameTheHeaderForBeingABrokenClosingLine()
  {
    var refusal = ColumnListingIngestion.Ingest(APivot.Header()).Refusal.ShouldNotBeNull();

    refusal.Cause.ShouldBe(RefusalCause.MissingClosingLine);
    refusal.Observed.ShouldNotContain(ColumnListing.FormatVersion);
  }

  /// <summary>Rien de collé du tout n'a pas d'en-tête, et c'est le premier refus qui s'applique.</summary>
  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   \n\n  ")]
  public void RefusesAPasteThatCarriesNothingAtAll(string? paste)
  {
    ColumnListingIngestion.Ingest(paste).ShouldBeRefusedFor(RefusalCause.MissingHeader);
  }

  private static string RemoveKeyFrom(string line, string key)
  {
    var start = line.IndexOf($"\"{key}\":", StringComparison.Ordinal);
    var end = line.IndexOf(',', start);

    return end < 0
      ? string.Concat(line.AsSpan(0, start - 1), "}")
      : line.Remove(start, end - start + 1);
  }
}

/// <summary>Ce qu'on dit d'un refus : quel cas, et sur quelle ligne.</summary>
internal static class IngestionOutcomeAssertions
{
  internal static void ShouldBeRefusedFor(
    this IngestionOutcome outcome,
    RefusalCause cause,
    int? line = null)
  {
    outcome.IsAccepted.ShouldBeFalse();
    outcome.Listing.ShouldBeNull();

    var refusal = outcome.Refusal.ShouldNotBeNull();
    refusal.Cause.ShouldBe(cause);

    if (line is { } expected)
    {
      refusal.LineNumber.ShouldBe(expected);
    }
  }
}
