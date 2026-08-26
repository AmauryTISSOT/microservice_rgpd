using System.Text;
using Microsoft.Data.Sqlite;
using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.Infrastructure.Screenings.Scanning;
using MicroserviceRgpd.Infrastructure.Screenings.Scanning.Sqlite;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Screenings.Scanning;

/// <summary>
/// Le scanner SQLite éprouvé sur une base <b>authentique</b> : c'est la moitié du filet de sécurité
/// que le lint sur le texte des requêtes ne peut pas tenir.
/// </summary>
public class SqliteDialectScannerTests
{
  private static readonly DateTimeOffset Noon = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

  /// <summary>
  /// Le schéma témoin : une table pleine dont chaque colonne éprouve un cas d'aperçu, une table qui
  /// porte une clé étrangère, et une table vide.
  /// </summary>
  private const string Schema = """
    CREATE TABLE abonne (
      id       INTEGER PRIMARY KEY,
      courriel TEXT NOT NULL,
      bio      TEXT,
      surnom   TEXT,
      photo    BLOB,
      note     TEXT
    );
    CREATE TABLE commande (
      id        INTEGER PRIMARY KEY,
      abonne_id INTEGER REFERENCES abonne(id),
      montant   REAL
    );
    CREATE TABLE inventaire (
      id      INTEGER PRIMARY KEY,
      libelle TEXT
    );
    """;

  /// <summary>
  /// Sept lignes, pour que le plafond de cinq se voie. ⚠️ Chaque colonne porte la <b>même</b> nature
  /// de valeur sur toutes ses lignes : le prélèvement ne trie pas, et un test qui dépendrait de
  /// l'ordre des lignes serait un test qui ment un jour sur deux.
  /// </summary>
  private static string Rows =>
    string.Concat(
      Enumerable.Range(1, 7).Select(row =>
        "INSERT INTO abonne (id, courriel, bio, surnom, photo, note) VALUES "
        + $"({row}, 'abonne{row}@example.com', NULL, '', x'00ff', '{new string('e', 300)}');\n"));

  /// <summary>
  /// ⚠️ <b>Le relevé connecté repasse par l'ingestion, comme un collage.</b> S'il rendait un
  /// <c>ColumnListing</c> tout fait, la voie connectée serait une seconde entrée dans le domaine,
  /// et les neuf refus ne la garderaient plus.
  /// </summary>
  [Fact]
  public async Task ProducesAPivotThatIngestionAccepts()
  {
    using var database = ASqliteBase.Holding(Schema + Rows);

    var outcome = await ScanAsync(database);

    outcome.Ending.ShouldBe(ScanEnding.Listed);

    var ingested = ColumnListingIngestion.Ingest(outcome.Pivot);

    ingested.IsAccepted.ShouldBeTrue(ingested.Refusal?.Observed);
    ingested.Listing!.Dialect.ShouldBe("sqlite");
    ingested.Listing.ColumnCount.ShouldBe(11);
    ingested.Listing.TableCount.ShouldBe(3);
  }

  /// <summary>
  /// ⚠️ <b>Les deux chemins relèvent la même base de la même façon.</b> Le collé et le connecté
  /// sont éprouvés côte à côte sur le <b>même</b> fichier : si la requête embarquée en C# et
  /// <c>releves/sqlite.sql</c> divergeaient, deux relevés de la même base cesseraient de se
  /// comparer — et le format pivot ne serait plus qu'un même nom sur deux choses.
  /// </summary>
  [Fact]
  public async Task ReadsTheSameColumnsAsTheAuthenticCapture()
  {
    using var database = ASqliteBase.Holding(Schema + Rows);

    var scanned = ColumnListingIngestion.Ingest((await ScanAsync(database)).Pivot);
    var captured = ColumnListingIngestion.Ingest(CaptureWithThePastedQuery(database));

    scanned.IsAccepted.ShouldBeTrue(scanned.Refusal?.Observed);
    captured.IsAccepted.ShouldBeTrue(captured.Refusal?.Observed);

    Described(scanned.Listing!).ShouldBe(Described(captured.Listing!), ignoreOrder: true);
  }

  /// <summary>Cinq valeurs au plus, et la borne se lit sur le domaine, pas sur un chiffre écrit ici.</summary>
  [Fact]
  public async Task ReadsAtMostFiveValuesPerColumn()
  {
    using var database = ASqliteBase.Holding(Schema + Rows);

    var preview = PreviewOf(await ScanAsync(database), "abonne", "courriel");

    preview.CarriesValues.ShouldBeTrue();
    preview.Values.Count.ShouldBe(ColumnPreview.MaxValues);
    preview.Values.ShouldAllBe(value => value.Text!.EndsWith("@example.com"));
  }

  /// <summary>
  /// ⚠️ <b>Au-delà de 254, la valeur arrive déjà coupée, sa longueur réelle à côté.</b> Couper en
  /// C# ferait traverser au service la valeur entière — donc du réel qui ne devait jamais rester.
  /// </summary>
  [Fact]
  public async Task TruncatesLongTextInTheEngineAndKeepsItsRealLength()
  {
    using var database = ASqliteBase.Holding(Schema + Rows);

    var preview = PreviewOf(await ScanAsync(database), "abonne", "note");

    preview.CarriesValues.ShouldBeTrue();
    preview.Values.ShouldAllBe(value => value.Text!.Length == ColumnPreview.MaxValueLength);
    preview.Values.ShouldAllBe(value => value.ActualLength == 300);
    preview.Values.ShouldAllBe(value => value.IsTruncated);
  }

  /// <summary>
  /// ⚠️ <b><c>NULL</c> est une valeur lue.</b> Le lire comme une absence ferait dire « on n'a pas
  /// regardé » là où la base a répondu « rien ici », et ce sont deux choses différentes.
  /// </summary>
  [Fact]
  public async Task ReadsNullAsAValue()
  {
    using var database = ASqliteBase.Holding(Schema + Rows);

    var preview = PreviewOf(await ScanAsync(database), "abonne", "bio");

    preview.CarriesValues.ShouldBeTrue();
    preview.Values.ShouldAllBe(value => value.IsNull);
    preview.Values.ShouldAllBe(value => value.Display == PreviewedValue.NullMarker);
  }

  /// <summary>La chaîne vide aussi est une valeur lue, et elle se dit autrement que <c>NULL</c>.</summary>
  [Fact]
  public async Task ReadsTheEmptyStringAsAValue()
  {
    using var database = ASqliteBase.Holding(Schema + Rows);

    var preview = PreviewOf(await ScanAsync(database), "abonne", "surnom");

    preview.CarriesValues.ShouldBeTrue();
    preview.Values.ShouldAllBe(value => value.IsEmpty);
    preview.Values.ShouldAllBe(value => value.Display == PreviewedValue.EmptyMarker);
  }

  /// <summary>
  /// ⚠️ <b>Une colonne dont rien n'est lisible rend zéro valeur, donc une raison pleine.</b> Sous
  /// SQLite le filtre porte sur la valeur : l'aperçu mixte est dissous, et « les cinq premières
  /// lisibles » n'a ici aucune lisible à rendre.
  /// </summary>
  [Fact]
  public async Task SaysTheTypeIsNotSampleableWhenEveryValueIsBinary()
  {
    using var database = ASqliteBase.Holding(Schema + Rows);

    var preview = PreviewOf(await ScanAsync(database), "abonne", "photo");

    preview.CarriesValues.ShouldBeFalse();
    preview.Absence.ShouldBe(PreviewAbsenceReason.UnsampleableType);
  }

  /// <summary>
  /// ⚠️ <b>Une table sans ligne ne dit pas la même chose qu'une colonne illisible.</b> Zéro ligne
  /// veut dire deux choses, et la sentinelle est ce qui les sépare.
  /// </summary>
  [Fact]
  public async Task SaysNoValueWasReturnedWhenTheTableIsEmpty()
  {
    using var database = ASqliteBase.Holding(Schema + Rows);

    var preview = PreviewOf(await ScanAsync(database), "inventaire", "libelle");

    preview.CarriesValues.ShouldBeFalse();
    preview.Absence.ShouldBe(PreviewAbsenceReason.NoValueReturned);
  }

  /// <summary>Chaque colonne du relevé reçoit un aperçu — aucune case n'est laissée vide.</summary>
  [Fact]
  public async Task GivesEveryListedColumnItsOwnPreview()
  {
    using var database = ASqliteBase.Holding(Schema + Rows);

    var outcome = await ScanAsync(database);
    var listing = ColumnListingIngestion.Ingest(outcome.Pivot).Listing!;

    outcome.Previews.Count.ShouldBe(listing.ColumnCount);
    listing.Columns.ShouldAllBe(column => outcome.Previews.ContainsKey(column.Identity));
  }

  /// <summary>
  /// ⚠️ <b>Le <c>base</c> du pivot ne porte que le nom du fichier.</b> Un chemin dit où vit la base
  /// du client, et le dossier parent en dit souvent davantage que le fichier lui-même.
  /// </summary>
  [Fact]
  public async Task NamesTheFileAndNeverItsPath()
  {
    using var database = ASqliteBase.Holding(Schema + Rows);

    var outcome = await ScanAsync(database);
    var pivot = outcome.Pivot!;

    ColumnListingIngestion.Ingest(pivot).Listing!.Database.ShouldBe(database.FileName);
    pivot.ShouldNotContain(Path.GetDirectoryName(database.Path)!, Case.Insensitive);
    pivot.ShouldNotContain(Path.GetTempPath(), Case.Insensitive);
  }

  /// <summary>
  /// ⚠️ <b>Le pool est coupé.</b> Une connexion rendue au pool garderait le fichier du client
  /// ouvert après l'écran ; ici, le fichier se supprime dès la fin du scan — ce qui, sous Windows,
  /// est impossible tant qu'une poignée traîne.
  /// </summary>
  [Fact]
  public async Task LeavesNoConnectionBehindWhenTheScanEnds()
  {
    var database = ASqliteBase.Holding(Schema + Rows);

    try
    {
      (await ScanAsync(database)).Ending.ShouldBe(ScanEnding.Listed);

      Should.NotThrow(() => File.Delete(database.Path));
      File.Exists(database.Path).ShouldBeFalse();
    }
    finally
    {
      database.Dispose();
    }
  }

  /// <summary>
  /// ⚠️ <b>Une base sans table est une fin, pas un rapport de zéro colonne.</b> Un <c>Screening</c>
  /// vide ferait reculer le rapport courant et détruirait des jours d'arbitrage.
  /// </summary>
  [Fact]
  public async Task SaysTheDatabaseCarriesNoTable()
  {
    using var database = ASqliteBase.Empty();

    var outcome = await ScanAsync(database);

    outcome.Ending.ShouldBe(ScanEnding.NoTable);
    outcome.Pivot.ShouldBeNull();
    outcome.Previews.ShouldBeEmpty();
  }

  /// <summary>
  /// ⚠️ <b>Un fichier absent est un échec, jamais une base sans table.</b> C'est ce que
  /// <c>Mode=ReadOnly</c> achète : ouvert en écriture, SQLite aurait créé le fichier de la faute de
  /// frappe et rendu, très sincèrement, une base sans table.
  /// </summary>
  [Fact]
  public async Task FailsOnAMissingFileInsteadOfCreatingIt()
  {
    var absent = Path.Combine(Path.GetTempPath(), $"absente-{Guid.NewGuid():N}.db");

    var outcome = await ScannerUnderTest()
      .ScanAsync(DatabaseDialect.Sqlite, $"Data Source={absent}");

    outcome.Ending.ShouldBe(ScanEnding.Failed);
    outcome.Failure!.Phase.ShouldBe(ScanPhase.Connecting);
    outcome.Failure.Family.ShouldBe(ScanFailureFamily.Supplied);
    File.Exists(absent).ShouldBeFalse();
  }

  /// <summary>
  /// ⚠️ <b>Un fichier qui n'est pas une base SQLite est un échec nommé, pas une exception.</b> Rien
  /// du message du pilote, du chemin ni de la chaîne de connexion ne ressort de ce qui est rendu.
  /// </summary>
  [Fact]
  public async Task LetsNoDriverWordCrossThePort()
  {
    var path = Path.Combine(Path.GetTempPath(), $"pas-une-base-{Guid.NewGuid():N}.db");
    File.WriteAllText(path, "ceci n'est pas une base SQLite, et le pilote va le dire fort");

    try
    {
      var outcome = await ScannerUnderTest()
        .ScanAsync(DatabaseDialect.Sqlite, $"Data Source={path}");

      outcome.Ending.ShouldBe(ScanEnding.Failed);

      var rendered = outcome.Failure!.Phase.FrenchLabel
        + outcome.Failure.Family.FrenchLabel
        + outcome.Failure.Family.Statement;

      rendered.ShouldNotContain(path, Case.Insensitive);
      rendered.ShouldNotContain("SQLite", Case.Insensitive);
      rendered.ShouldNotContain("file is not a database", Case.Insensitive);
    }
    finally
    {
      File.Delete(path);
    }
  }

  /// <summary>
  /// ⚠️ <b>Le catalogue apporte le dénominateur, et rien ne se compte avant lui.</b> Un total
  /// annoncé plus tôt serait le chiffre inventé que l'écran d'attente refuse.
  /// </summary>
  [Fact]
  public async Task ReportsTheCatalogueBeforeItCountsAnything()
  {
    using var database = ASqliteBase.Holding(Schema + Rows);

    var record = new ARecordOfSteps();

    await ScanAsync(database, record);

    var steps = record.Steps;

    steps[0].Phase.ShouldBe(ScanPhase.Cataloguing);
    steps[0].Total.ShouldBe(3);
    steps.Skip(1).ShouldAllBe(step => step.Phase == ScanPhase.Sampling);
    steps.Skip(1).ShouldAllBe(step => step.Total == 3);
    steps[^1].Done.ShouldBe(3);
  }

  /// <summary>
  /// ⚠️ <b>Un dialecte sans pilote n'est pas un échec de scan.</b> Un <c>Failed</c> promettrait à
  /// l'<c>Operator</c> qu'un geste peut le sauver ; c'est un défaut de câblage, et il doit
  /// s'entendre.
  /// </summary>
  [Fact]
  public async Task RefusesToPretendItCanReachADialectItHasNoDriverFor()
  {
    await Should.ThrowAsync<ArgumentOutOfRangeException>(
      () => ScannerUnderTest().ScanAsync(DatabaseDialect.PostgreSql, "Host=nulle-part"));
  }

  /// <summary>
  /// ⚠️ <b>Une table qui rate ne fait pas tomber le relevé.</b> Le schéma d'un client bouge pendant
  /// qu'on le lit ; si l'échec d'une table emportait le scan, un relevé de trois cents tables
  /// n'aboutirait jamais sur une base vivante. La table perdue reçoit une raison nommée, les autres
  /// gardent leurs valeurs, et aucune exception du pilote ne traverse.
  /// </summary>
  [Fact]
  public async Task GivesAReasonToATableThatFailsWithoutLosingTheRest()
  {
    using var database = ASqliteBase.Holding(Schema + Rows);

    // La table disparaît entre le relevé du catalogue et son prélèvement : c'est exactement ce que
    // fait une migration passée pendant le scan.
    var record = new ARecordOfSteps(step =>
    {
      if (step.Phase == ScanPhase.Cataloguing)
      {
        Apply(database, "DROP TABLE inventaire;");
      }
    });

    var outcome = await ScanAsync(database, record);

    outcome.Ending.ShouldBe(ScanEnding.Listed);
    PreviewOf(outcome, "inventaire", "libelle").Absence.ShouldBe(PreviewAbsenceReason.ReadFailed);
    PreviewOf(outcome, "abonne", "courriel").CarriesValues.ShouldBeTrue();
  }

  /// <summary>
  /// ⚠️ <b>La requête vise le nom du catalogue, jamais celui que le domaine a normalisé.</b> Le
  /// domaine rogne les blancs de bordure — à raison, deux colonnes qui ne diffèrent que par une
  /// espace de tête ne sont pas deux colonnes pour un lecteur. Mais le SGBD, lui, accepte
  /// <c>" abonne "</c> : demander <c>"abonne"</c> tomberait sur « table inconnue », et toutes ses
  /// colonnes recevraient une raison d'absence alors que le schéma a été lu sans encombre.
  /// </summary>
  [Fact]
  public async Task SamplesAnObjectWhoseNameTheDomainWouldHaveTrimmed()
  {
    using var database = ASqliteBase.Holding(
      """
      CREATE TABLE " abonne " (" courriel " TEXT);
      INSERT INTO " abonne " (" courriel ") VALUES ('marie@example.com');
      """);

    var outcome = await ScanAsync(database);

    outcome.Ending.ShouldBe(ScanEnding.Listed);

    var preview = PreviewOf(outcome, "abonne", "courriel");

    preview.CarriesValues.ShouldBeTrue();
    preview.Values.ShouldHaveSingleItem().Text.ShouldBe("marie@example.com");
  }

  /// <summary>
  /// ⚠️ <b>« Tronqué » se mesure sur ce que le SGBD a envoyé, pas sur ce qui reste après le
  /// garde-fou.</b> SQLite compte en caractères Unicode, .NET en unités UTF-16 : au-delà du plan
  /// multilingue de base, <c>substr(col, 1, 254)</c> rend jusqu'à 508 unités, et le garde-fou en
  /// retire la moitié. Une valeur ainsi amputée qui se dirait intacte serait comptée par une règle
  /// de format comme si rien ne lui manquait.
  /// </summary>
  [Fact]
  public async Task SaysAValueIsTruncatedEvenWhenTheGuardRailDidTheLastCut()
  {
    // 200 émojis : 200 caractères pour SQLite, 400 unités UTF-16 pour .NET.
    using var database = ASqliteBase.Holding(
      $"""
      CREATE TABLE humeur (trace TEXT);
      INSERT INTO humeur (trace) VALUES ('{string.Concat(Enumerable.Repeat("😀", 200))}');
      """);

    var preview = PreviewOf(await ScanAsync(database), "humeur", "trace");

    var value = preview.Values.ShouldHaveSingleItem();

    value.Text!.Length.ShouldBeLessThanOrEqualTo(ColumnPreview.MaxValueLength);
    value.IsTruncated.ShouldBeTrue();
  }

  /// <summary>
  /// ⚠️ <b>Un nom d'objet que le domaine refuse est une fin nommée, pas une exception.</b> Le schéma
  /// d'un client n'a pas à respecter ce que le service sait porter ; c'est au port de le dire.
  /// </summary>
  [Fact]
  public async Task FailsWithoutThrowingWhenAnObjectNameIsOneTheDomainRefuses()
  {
    // Un nom de table porteur d'un saut de ligne : SQLite l'accepte, ColumnIdentity le refuse.
    using var database = ASqliteBase.Holding("CREATE TABLE \"abo\nnne\" (id INTEGER);");

    var outcome = await ScanAsync(database);

    outcome.Ending.ShouldBe(ScanEnding.Failed);
    outcome.Failure!.Phase.ShouldBe(ScanPhase.Cataloguing);
    outcome.Failure.Family.ShouldBe(ScanFailureFamily.Database);
  }

  private static void Apply(ASqliteBase database, string statement)
  {
    using var connection = new SqliteConnection(
      new SqliteConnectionStringBuilder
      {
        DataSource = database.Path,
        Mode = SqliteOpenMode.ReadWrite,
        Pooling = false,
      }.ConnectionString);

    connection.Open();

    using var command = connection.CreateCommand();
    command.CommandText = statement;
    command.ExecuteNonQuery();
  }

  private static DatabaseScanner ScannerUnderTest()
  {
    return new DatabaseScanner([new SqliteDialectScanner(new AClockStuckAt(Noon))]);
  }

  private static Task<ScanOutcome> ScanAsync(
    ASqliteBase database,
    IProgress<ScanStep>? progress = null)
  {
    return ScannerUnderTest()
      .ScanAsync(DatabaseDialect.Sqlite, database.ConnectionString, progress);
  }

  private static ColumnPreview PreviewOf(ScanOutcome outcome, string table, string column)
  {
    return outcome.Previews[ColumnIdentity.Of("main", table, column)];
  }

  private static string CaptureWithThePastedQuery(ASqliteBase database)
  {
    using var connection = new SqliteConnection(
      new SqliteConnectionStringBuilder
      {
        DataSource = database.Path,
        Mode = SqliteOpenMode.ReadOnly,
        Pooling = false,
      }.ConnectionString);

    connection.Open();

    using var command = connection.CreateCommand();
    command.CommandText = ThePastedQuery.For("sqlite");

    using var reader = command.ExecuteReader();
    var capture = new StringBuilder();

    while (reader.Read())
    {
      capture.Append(reader.GetString(0)).Append('\n');
    }

    return capture.ToString();
  }

  private static IEnumerable<string> Described(ColumnListing listing)
  {
    return listing.Columns.Select(column =>
      $"{column.Identity}|{column.Position}|{column.DataType}|{column.IsNullable}"
      + $"|{column.ReferencedTable}|{column.ColumnComment}|{column.TableComment}");
  }
}
