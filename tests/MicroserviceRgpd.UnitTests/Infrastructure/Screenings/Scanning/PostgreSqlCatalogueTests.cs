using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.Infrastructure.Screenings.Scanning;
using MicroserviceRgpd.Infrastructure.Screenings.Scanning.PostgreSql;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Screenings.Scanning;

/// <summary>
/// La fixture PostgreSQL authentique, <b>rejouée</b> : son catalogue repasse par le relevé connecté,
/// devient un pivot, et ce pivot est confronté à l'ingestion puis à la capture que le banc a laissée.
/// C'est la moitié du filet de sécurité que le lint sur le texte des requêtes ne peut pas tenir.
/// </summary>
/// <remarks>
/// ⚠️ <b>Aucun conteneur n'est démarré, et c'est assumé.</b> Les faits par dialecte ont été mesurés
/// une fois, sur conteneur, hors du dépôt (#275) ; ce qui reste à éprouver ici est tout ce qui vient
/// <b>après</b> le pilote — et c'est là que vivent la renumérotation des positions, les neuf refus,
/// et « un seul format pivot ».
/// </remarks>
public class PostgreSqlCatalogueTests
{
  private static readonly DateTimeOffset Noon = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

  /// <summary>
  /// ⚠️ <b>Le relevé connecté repasse par l'ingestion, comme un collage.</b> S'il rendait un
  /// <c>ColumnListing</c> tout fait, la voie connectée serait une seconde entrée dans le domaine, et
  /// les neuf refus ne la garderaient plus.
  /// </summary>
  [Fact]
  public async Task ProducesAPivotThatIngestionAccepts()
  {
    var ingested = ColumnListingIngestion.Ingest(await PivotOfTheFixtureAsync());

    ingested.IsAccepted.ShouldBeTrue(ingested.Refusal?.Observed);
    ingested.Listing!.Dialect.ShouldBe("postgresql");
    ingested.Listing.Database.ShouldBe("epreuve");
    ingested.Listing.ColumnCount.ShouldBe(20);
    ingested.Listing.TableCount.ShouldBe(3);
  }

  /// <summary>
  /// ⚠️ <b>Les deux chemins relèvent la même base de la même façon.</b> Le pivot produit depuis le
  /// catalogue de la fixture est confronté, colonne par colonne, à la capture que
  /// <c>releves/postgresql.sql</c> a laissée sur cette même fixture : si les deux requêtes
  /// divergeaient, deux relevés de la même base cesseraient de se comparer — et le format pivot ne
  /// serait plus qu'un même nom sur deux choses.
  /// </summary>
  [Fact]
  public async Task ReadsTheSameColumnsAsTheAuthenticCapture()
  {
    var scanned = ColumnListingIngestion.Ingest(await PivotOfTheFixtureAsync());
    var captured = ColumnListingIngestion.Ingest(TheAuthenticCapture.For("postgresql"));

    captured.IsAccepted.ShouldBeTrue(captured.Refusal?.Observed);
    scanned.IsAccepted.ShouldBeTrue(scanned.Refusal?.Observed);

    Described(scanned.Listing!).ShouldBe(Described(captured.Listing!), ignoreOrder: true);
  }

  /// <summary>
  /// ⚠️ <b>Le multi-schéma est un vrai multi-schéma.</b> <c>public.adherents</c> et
  /// <c>audit.adherents</c> sont deux tables ; les confondre ferait de deux relevés un seul, avec
  /// des positions en double — l'un des neuf refus, pour une base parfaitement ordinaire.
  /// </summary>
  [Fact]
  public async Task KeepsTwoTablesOfTheSameNameApart()
  {
    var listing = ColumnListingIngestion.Ingest(await PivotOfTheFixtureAsync()).Listing!;

    listing.Columns.Count(column =>
      column.Identity.Table == "adherents" && column.Identity.Schema == "public").ShouldBe(14);
    listing.Columns.Count(column =>
      column.Identity.Table == "adherents" && column.Identity.Schema == "audit").ShouldBe(3);
  }

  /// <summary>
  /// ⚠️ <b>Les positions sont renumérotées, et le trou de <c>attnum</c> ne traverse pas.</b>
  /// <c>public.adherents</c> a perdu sa deuxième colonne par un <c>DROP COLUMN</c> : rendre
  /// <c>attnum</c> tel quel ferait refuser le relevé pour trou dans les positions — le refus qui
  /// attrape la troncature au milieu — sur une base qui a simplement vécu.
  /// </summary>
  [Fact]
  public async Task RenumbersPositionsSoADroppedColumnLeavesNoHole()
  {
    var listing = ColumnListingIngestion.Ingest(await PivotOfTheFixtureAsync()).Listing!;

    var positions = listing.Columns
      .Where(column => column.Identity.Schema == "public" && column.Identity.Table == "adherents")
      .Select(column => column.Position)
      .Order()
      .ToList();

    positions.ShouldBe([.. Enumerable.Range(1, 14)]);
  }

  /// <summary>
  /// ⚠️ <b>Les commentaires viennent de <c>pg_catalog</c>, et c'est pour eux que
  /// l'<c>information_schema</c> ne suffirait pas.</b> <c>col_description</c> et
  /// <c>obj_description</c> n'y existent pas : deux des neuf champs du pivot y seraient
  /// inatteignables.
  /// </summary>
  [Fact]
  public async Task CarriesTheCommentsOnlyPgCatalogKnows()
  {
    var listing = ColumnListingIngestion.Ingest(await PivotOfTheFixtureAsync()).Listing!;

    var courriel = listing.Columns.Single(column =>
      column.Identity == ColumnIdentity.Of("public", "adherents", "courriel"));

    courriel.ColumnComment.ShouldBe("adresse de contact");
    courriel.TableComment.ShouldBe("les adhérents");
  }

  /// <summary>
  /// ⚠️ <b>La liste noire se décide sur le catalogue, avant toute requête.</b> Une colonne
  /// <c>bytea</c> ne sera jamais interrogée ; <c>uuid</c>, <c>jsonb</c>, <c>inet</c>, un tableau et
  /// une colonne de coordonnées le seront — <c>typcategory = 'U'</c> les aurait toutes rangées avec
  /// le binaire.
  /// </summary>
  [Fact]
  public async Task MarksTheBinaryColumnsAndOnlyThem()
  {
    var catalogued = await PostgreSqlCatalogue.ReadAsync(
      APostgreSqlCatalogue.Reader(),
      CancellationToken.None);

    var refused = catalogued
      .Where(column => !column.IsSampleable)
      .Select(column => column.Scanned.Identity.Column)
      .Order(StringComparer.Ordinal)
      .ToList();

    refused.ShouldBe(["photo", "signature"]);
  }

  /// <summary>
  /// ⚠️ <b>Le nom qui repartira dans une requête est celui du catalogue, jamais celui que le domaine
  /// a normalisé.</b> Le domaine rogne les blancs de bordure — à raison —, mais PostgreSQL accepte
  /// parfaitement une table nommée <c>" adherents "</c>, et la requête qui demanderait
  /// <c>"adherents"</c> tomberait sur « table inconnue », donc sur une raison d'absence pour toutes
  /// ses colonnes, alors que le schéma a été lu sans encombre.
  /// </summary>
  [Fact]
  public async Task KeepsTheRawNamesTheEngineWillAnswerTo()
  {
    var catalogued = await PostgreSqlCatalogue.ReadAsync(
      APostgreSqlCatalogue.Reader(
        [["public", " adherents ", " courriel ", (short)1, "text", "text", true, "", "", ""]]),
      CancellationToken.None);

    var column = catalogued.ShouldHaveSingleItem().Scanned;

    column.Identity.Table.ShouldBe("adherents");
    column.RawTable.ShouldBe(" adherents ");
    column.RawColumn.ShouldBe(" courriel ");
  }

  private static async Task<string> PivotOfTheFixtureAsync()
  {
    var catalogued = await PostgreSqlCatalogue.ReadAsync(
      APostgreSqlCatalogue.Reader(),
      CancellationToken.None);

    return PivotWriter.Write(
      DatabaseDialect.PostgreSql,
      "epreuve",
      Noon,
      [.. catalogued.Select(column => column.Scanned)]);
  }

  private static IEnumerable<string> Described(ColumnListing listing)
  {
    return listing.Columns.Select(column =>
      $"{column.Identity}|{column.Position}|{column.DataType}|{column.IsNullable}"
      + $"|{column.ReferencedTable}|{column.ColumnComment}|{column.TableComment}");
  }
}
