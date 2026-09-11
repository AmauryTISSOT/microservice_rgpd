using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MicroserviceRgpd.Infrastructure.Data;
using Testcontainers.PostgreSql;

namespace MicroserviceRgpd.IntegrationTests.Migrations;

/// <summary>
/// <b><c>DropCasework</c> retire exactement les treize tables de <c>Casework</c>, et rien d'autre.</b>
/// Une base est montée au dernier état d'<i>avant</i> le retrait, on y relève les tables, puis on
/// joue la migration et on les relève encore.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>C'est la différence des deux relevés qui est éprouvée, pas l'état final seul.</b> Une base
/// neuve où les treize tables manqueraient ne dirait rien d'une migration qui en aurait emporté une
/// quatorzième : c'est ce qu'elle a <i>retiré</i> qui doit être exactement la liste. Il monte donc son
/// <b>propre</b> conteneur, comme <see cref="ListingOriginBackfillTests"/> — celui de
/// <c>PostgreSqlFixture</c> est déjà migré jusqu'au bout.
/// </para>
/// <para>
/// <b>Monter la base à la migration d'avant prouve aussi que les migrations de <c>Casework</c> déjà
/// appliquées sont intactes</b> : elles doivent encore créer les treize tables pour que la
/// migration ait quelque chose à retirer. Il n'y a aucune donnée à préserver.
/// </para>
/// </remarks>
public class DropCaseworkTests : IAsyncLifetime
{
  /// <summary>La dernière migration écrite avant le retrait de <c>Casework</c>.</summary>
  private const string BeforeTheDrop = "20260910205826_CreateSettings";

  private static readonly string[] CaseworkTables =
  [
    "case_claims",
    "case_designations",
    "case_locating_references",
    "case_locatings",
    "case_questions",
    "case_readings",
    "case_reservation_designations",
    "case_reservations",
    "case_retrieved_data",
    "case_steps",
    "cases",
    "declared_systems",
    "evidence_log_entries",
  ];

  private readonly PostgreSqlContainer _container =
    new PostgreSqlBuilder("postgres:18-alpine").Build();

  private List<string> _before = [];
  private List<string> _after = [];

  public async Task InitializeAsync()
  {
    await _container.StartAsync();

    await using var dbContext = NewDbContext();

    await dbContext.GetService<IMigrator>().MigrateAsync(BeforeTheDrop);
    _before = await TablesAsync(dbContext);

    await dbContext.Database.MigrateAsync();
    _after = await TablesAsync(dbContext);
  }

  public Task DisposeAsync() => _container.DisposeAsync().AsTask();

  [Fact]
  public void DropsExactlyTheThirteenCaseworkTables()
  {
    _before.Except(_after).Order().ShouldBe(CaseworkTables);
  }

  [Fact]
  public void CreatesNoTable()
  {
    _after.Except(_before).ShouldBeEmpty();
  }

  private static async Task<List<string>> TablesAsync(AppDbContext dbContext) =>
    await dbContext.Database.SqlQueryRaw<string>(
      """
      SELECT table_name AS "Value" FROM information_schema.tables
      WHERE table_schema = 'public' AND table_type = 'BASE TABLE'
      """).ToListAsync();

  private AppDbContext NewDbContext() =>
    new(new DbContextOptionsBuilder<AppDbContext>()
      .UseNpgsql(_container.GetConnectionString())
      .Options);
}
