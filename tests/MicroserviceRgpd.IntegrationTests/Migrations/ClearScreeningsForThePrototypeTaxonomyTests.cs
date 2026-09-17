using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MicroserviceRgpd.Infrastructure.Data;

namespace MicroserviceRgpd.IntegrationTests.Migrations;

/// <summary>
/// <b>Le passage aux prototypes vide <c>screenings</c> et <c>screened_columns</c>, et ne touche à
/// aucune colonne.</b> Une base est montée au dernier état d'<i>avant</i>, on y écrit un rapport dans
/// la taxonomie d'alors, puis on joue la migration.
/// </summary>
/// <remarks>
/// ⚠️ <b>C'est le seul endroit d'où la suppression puisse être éprouvée</b> : une base neuve n'a
/// aucun rapport à supprimer, et une migration vide y afficherait vert. Il monte donc sa
/// <b>propre</b> base, comme <see cref="ListingOriginBackfillTests"/>.
/// </remarks>
public class ClearScreeningsForThePrototypeTaxonomyTests : IAsyncLifetime
{
  /// <summary>La dernière migration écrite avant le passage aux prototypes.</summary>
  internal const string BeforeTheClearing = "20260912095930_AddDataSubjectRequestModification";

  private static readonly Guid ReportId = new("66666666-6666-6666-6666-666666666666");

  private string _database = string.Empty;

  private List<string> _columnsBefore = [];
  private List<string> _columnsAfter = [];

  public async Task InitializeAsync()
  {
    _database = await PostgreSqlServer.NewDatabaseAsync(nameof(ClearScreeningsForThePrototypeTaxonomyTests));

    await using var dbContext = NewDbContext();

    await dbContext.GetService<IMigrator>().MigrateAsync(BeforeTheClearing);
    await WriteAReportInTheRetiredTaxonomyAsync(dbContext);
    _columnsBefore = await ColumnsAsync(dbContext);

    await dbContext.Database.MigrateAsync();
    _columnsAfter = await ColumnsAsync(dbContext);
  }

  public Task DisposeAsync() => Task.CompletedTask;

  /// <summary>
  /// Aucun rapport ne survit, et aucune ligne : une colonne qui porterait encore
  /// <c>SpecialCategoryData</c> ferait lever la lecture du rapport entier.
  /// </summary>
  [Fact]
  public async Task EmptiesBothScreeningTables()
  {
    await using var dbContext = NewDbContext();

    (await ReportsAsync(dbContext)).ShouldBe(0);
    (await LinesAsync(dbContext)).ShouldBe(0);
  }

  /// <summary>
  /// Et elle ne fait rien d'autre : catégorie, degré, nom et version du moteur gardent leurs champs.
  /// </summary>
  [Fact]
  public void ChangesNoColumnOfEitherTable()
  {
    _columnsAfter.ShouldBe(_columnsBefore);
  }

  /// <summary>Un rapport et deux lignes, dont une dans une valeur que la taxonomie a retirée.</summary>
  private static async Task WriteAReportInTheRetiredTaxonomyAsync(AppDbContext dbContext)
  {
    await dbContext.Database.ExecuteSqlAsync(
      $"""
      INSERT INTO screenings
        (id, database_name, dialect, declared_column_count, launched_on, engine_name, engine_version,
         listing_origin)
      VALUES
        ({ReportId}, 'galette_prod', 'postgresql', 2, TIMESTAMPTZ '2026-09-01 09:30:00+00',
         'regles-lexique-fr-en', 'regles-2+formes-inactives+lexiques-d413d55', 'Pasted');
      """);

    await dbContext.Database.ExecuteSqlAsync(
      $"""
      INSERT INTO screened_columns
        (id, screening_id, schema_name, table_name, column_name, position, category, strength,
         reason)
      VALUES
        ({Guid.NewGuid()}, {ReportId}, 'public', 'adherents', 'confession', 1, 'SpecialCategoryData',
         'ExactName', 'jeton « confession » du nom de colonne, entrée du lexique'),
        ({Guid.NewGuid()}, {ReportId}, 'public', 'adherents', 'email', 2, 'ContactDetails',
         'ExactName', 'jeton « email » du nom de colonne, entrée du lexique');
      """);

    (await LinesAsync(dbContext)).ShouldBe(2);
  }

  private static async Task<int> ReportsAsync(AppDbContext dbContext) =>
    await dbContext.Database.SqlQueryRaw<int>("""SELECT count(*)::int AS "Value" FROM screenings""").SingleAsync();

  private static async Task<int> LinesAsync(AppDbContext dbContext) =>
    await dbContext.Database.SqlQueryRaw<int>("""SELECT count(*)::int AS "Value" FROM screened_columns""").SingleAsync();

  private static async Task<List<string>> ColumnsAsync(AppDbContext dbContext) =>
    await dbContext.Database.SqlQueryRaw<string>(
      """
      SELECT table_name || '.' || column_name || ':' || data_type || ':' || is_nullable AS "Value"
      FROM information_schema.columns
      WHERE table_name IN ('screenings', 'screened_columns')
      ORDER BY table_name, column_name
      """).ToListAsync();

  private AppDbContext NewDbContext() =>
    new(new DbContextOptionsBuilder<AppDbContext>()
      .UseNpgsql(_database)
      .Options);
}
