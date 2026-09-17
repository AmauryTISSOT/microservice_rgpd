using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MicroserviceRgpd.Infrastructure.Data;

namespace MicroserviceRgpd.IntegrationTests.Migrations;

/// <summary>
/// <b><c>CreateDataSubjectRequests</c> suit <c>DropCasework</c>, et pose exactement la table
/// <c>data_subject_requests</c>.</b> Une base est montée au retrait de <c>Casework</c>, on y relève
/// les tables, puis on joue la migration et on les relève encore.
/// </summary>
/// <remarks>
/// ⚠️ <b>L'ordre est gardé, et pas seulement l'état final.</b> Le Tableau des demandes repart d'une
/// page blanche <i>après</i> le retrait de l'ancien : une migration qui le précéderait ferait
/// coexister, sur une base en retard, les deux tableaux le temps d'un déploiement. Il monte sa
/// <b>propre</b> base, comme <see cref="DropCaseworkTests"/>.
/// </remarks>
public class CreateDataSubjectRequestsTests : IAsyncLifetime
{
  internal const string TheCreation = "20260911161754_CreateDataSubjectRequests";

  private string _database = string.Empty;

  private List<string> _before = [];
  private List<string> _after = [];
  private List<string> _migrations = [];

  public async Task InitializeAsync()
  {
    _database = await PostgreSqlServer.NewDatabaseAsync(nameof(CreateDataSubjectRequestsTests));

    await using var dbContext = NewDbContext();
    var migrator = dbContext.GetService<IMigrator>();

    _migrations = [.. dbContext.Database.GetMigrations()];

    await migrator.MigrateAsync(DropCaseworkTests.TheDrop);
    _before = await TablesAsync(dbContext);

    await migrator.MigrateAsync(TheCreation);
    _after = await TablesAsync(dbContext);
  }

  public Task DisposeAsync() => Task.CompletedTask;

  /// <summary>La migration vient <b>immédiatement</b> après le retrait de <c>Casework</c>.</summary>
  [Fact]
  public void ComesRightAfterTheCaseworkDrop()
  {
    _migrations.IndexOf(TheCreation).ShouldBe(_migrations.IndexOf(DropCaseworkTests.TheDrop) + 1);
  }

  /// <summary>Elle crée la table des demandes, et elle seule.</summary>
  [Fact]
  public void CreatesExactlyTheRequestsTable()
  {
    _after.Except(_before).ShouldBe(["data_subject_requests"]);
  }

  /// <summary>Et elle ne retire rien : un geste de création ne défait l'œuvre de personne.</summary>
  [Fact]
  public void DropsNoTable()
  {
    _before.Except(_after).ShouldBeEmpty();
  }

  private static async Task<List<string>> TablesAsync(AppDbContext dbContext) =>
    await dbContext.Database.SqlQueryRaw<string>(
      """
      SELECT table_name AS "Value" FROM information_schema.tables
      WHERE table_schema = 'public' AND table_type = 'BASE TABLE'
      """).ToListAsync();

  private AppDbContext NewDbContext() =>
    new(new DbContextOptionsBuilder<AppDbContext>()
      .UseNpgsql(_database)
      .Options);
}
