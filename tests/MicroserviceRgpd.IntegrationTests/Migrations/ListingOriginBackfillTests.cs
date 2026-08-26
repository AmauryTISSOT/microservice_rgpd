using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.Infrastructure.Data;
using Testcontainers.PostgreSql;

namespace MicroserviceRgpd.IntegrationTests.Migrations;

/// <summary>
/// <b>Les rapports d'avant la connexion sont relus en <c>Collé</c>, et ils restent arbitrables.</b>
/// Une base est montée au dernier état d'<i>avant</i> l'origine du relevé, on y écrit un rapport
/// avec les colonnes de cette date-là, puis on joue la migration.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>C'est le seul endroit d'où le remplissage puisse être éprouvé.</b> Une suite qui monte un
/// schéma neuf n'a aucun rapport ancien à relire : la colonne y serait posée non nullable sur zéro
/// ligne, et une migration qui aurait oublié de remplir l'existant afficherait vert. C'est la même
/// exception assumée que <c>VocabularyRenameSurvivalTests</c>, et elle monte son <b>propre</b>
/// conteneur pour la même raison — celui de <c>PostgreSqlFixture</c> est déjà migré jusqu'au bout.
/// </para>
/// <para>
/// <b>« Collé » est vrai par construction</b> : aucun chemin connecté n'existait quand ces rapports
/// ont été produits. Aucun ne se met donc à mentir, et la clause d'incomplétude qu'ils rendent dit
/// après ce qu'elle disait avant.
/// </para>
/// </remarks>
public class ListingOriginBackfillTests : IAsyncLifetime
{
  /// <summary>La dernière migration écrite avant que le rapport ne porte son origine.</summary>
  private const string BeforeTheOrigin = "20260825160151_DropScreeningArbitrationSignatory";

  private static readonly Guid ReportId = new("44444444-4444-4444-4444-444444444444");
  private static readonly Guid ColumnId = new("55555555-5555-5555-5555-555555555555");

  private static readonly DateTimeOffset RenderedOn = new(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);

  private readonly PostgreSqlContainer _container =
    new PostgreSqlBuilder("postgres:18-alpine").Build();

  public async Task InitializeAsync()
  {
    await _container.StartAsync();

    await using var dbContext = NewDbContext();

    await dbContext.GetService<IMigrator>().MigrateAsync(BeforeTheOrigin);
    await WriteAReportOfTheDayBeforeAsync(dbContext);
    await dbContext.Database.MigrateAsync();
  }

  public Task DisposeAsync() => _container.DisposeAsync().AsTask();

  /// <summary>
  /// Le rapport écrit avant la migration se relit <b>par le domaine</b>, et il se lit
  /// <c>Collé</c> — pas « origine non renseignée », qui aurait fait lever le rendu sur un rapport
  /// parfaitement sincère.
  /// </summary>
  [Fact]
  public async Task ReadsEveryReportOfTheDayBeforeAsPasted()
  {
    await using var dbContext = NewDbContext();

    var reread = await dbContext.Screenings
      .Include(one => one.Columns)
      .SingleAsync(one => one.Id == ScreeningId.From(ReportId));

    reread.Origin.ShouldBe(ListingOrigin.Pasted);
    reread.ColumnCount.ShouldBe(1);
  }

  /// <summary>
  /// <b>Et ils restent arbitrables.</b> Un rapport relu mais qu'on ne pourrait plus trancher aurait
  /// périmé du travail humain sans qu'aucune ligne de doctrine ne le dise.
  /// </summary>
  [Fact]
  public async Task LeavesTheReportsOfTheDayBeforeArbitrable()
  {
    await using (var dbContext = NewDbContext())
    {
      var screening = await dbContext.Screenings
        .Include(one => one.Columns)
        .SingleAsync(one => one.Id == ScreeningId.From(ReportId));

      screening.Arbitrate(
        ColumnIdentity.Of("public", "adherents", "adr_l1"),
        ScreenedColumnState.Retained,
        RenderedOn).ShouldNotBeNull();

      await dbContext.SaveChangesAsync();
    }

    await using var reading = NewDbContext();

    var reread = await reading.Screenings
      .Include(one => one.Columns)
      .SingleAsync(one => one.Id == ScreeningId.From(ReportId));

    reread.RetainedCount.ShouldBe(1);
  }

  /// <summary>
  /// <b>La raison d'absence d'aperçu reste nulle sur ces lignes</b>, et ce n'est pas un trou : ces
  /// rapports n'ont jamais eu d'aperçu. Les quatre comptes de la clause y sont <b>absents</b>,
  /// jamais à zéro — « zéro colonne sans aperçu » se lirait comme un prélèvement qui a tout réussi.
  /// </summary>
  [Fact]
  public async Task LeavesThePreviewAbsenceReasonNullOnEveryOldLine()
  {
    await using var dbContext = NewDbContext();

    var reasons = await ReadAsync<string?>(
      dbContext,
      $"""
      SELECT preview_absence_reason AS "Value" FROM screened_columns
      WHERE screening_id = '{ReportId}'
      """);

    reasons.ShouldBe([null]);
  }

  /// <summary>
  /// ⚠️ <b>Et la colonne ne garde aucune valeur par défaut.</b> Un <c>DEFAULT 'Pasted'</c> resté en
  /// base serait la porte de derrière que le cas nul de <c>ListingOrigin</c> existe pour fermer : un
  /// chemin d'écriture neuf qui oublierait l'origine obtiendrait « collé » en silence, sur un
  /// rapport scanné. C'est le seul test qui puisse le dire, parce qu'il interroge le schéma et non
  /// le modèle.
  /// </summary>
  [Fact]
  public async Task LeavesNoDefaultBehindOnTheOriginColumn()
  {
    await using var dbContext = NewDbContext();

    var defaults = await ReadAsync<string?>(
      dbContext,
      """
      SELECT column_default AS "Value" FROM information_schema.columns
      WHERE table_name = 'screenings' AND column_name = 'listing_origin'
      """);

    defaults.ShouldBe([null]);
  }

  /// <summary>
  /// Un rapport et sa colonne, écrits avec les colonnes de leur date : rien qui dise d'où venait le
  /// relevé, puisque rien ne le disait encore.
  /// </summary>
  private static async Task WriteAReportOfTheDayBeforeAsync(AppDbContext dbContext)
  {
    await dbContext.Database.ExecuteSqlAsync(
      $"""
      INSERT INTO screenings
        (id, database_name, dialect, declared_column_count, launched_on, engine_name, engine_version)
      VALUES
        ({ReportId}, 'galette_prod', 'postgresql', 1, TIMESTAMPTZ '2026-08-06 09:30:00+00',
         'lexique-fr-en', '1.0.0');
      """);

    await dbContext.Database.ExecuteSqlAsync(
      $"""
      INSERT INTO screened_columns
        (id, screening_id, schema_name, table_name, column_name, position, category, strength,
         reason)
      VALUES
        ({ColumnId}, {ReportId}, 'public', 'adherents', 'adr_l1', 1, 'ContactDetails',
         'Morphological', 'préfixe « adr » reconnu dans « adr_l1 »');
      """);
  }

  private static async Task<List<T>> ReadAsync<T>(AppDbContext dbContext, string sql) =>
    await dbContext.Database.SqlQueryRaw<T>(sql).ToListAsync();

  private AppDbContext NewDbContext() =>
    new(new DbContextOptionsBuilder<AppDbContext>()
      .UseNpgsql(_container.GetConnectionString())
      .Options);
}
