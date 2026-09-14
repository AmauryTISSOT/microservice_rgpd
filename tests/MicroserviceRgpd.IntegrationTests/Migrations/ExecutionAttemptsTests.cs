using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MicroserviceRgpd.Infrastructure.Data;
using Testcontainers.PostgreSql;

namespace MicroserviceRgpd.IntegrationTests.Migrations;

/// <summary>
/// <b>Le journal d'exécution arrive dans sa propre table, vide, et sans lien de clé avec les
/// demandes.</b> Une base est montée au dernier état d'<i>avant</i> le journal, on y écrit une demande,
/// puis on joue la migration (ADR-0026).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>L'absence de clé étrangère est le fait à tenir</b> : c'est elle qui laisse les tentatives
/// survivre à la suppression d'une demande. Une clé refuserait la suppression, ou emporterait la
/// preuve en cascade.
/// </para>
/// <para>
/// Son propre conteneur, pour la même raison que <see cref="ModificationStampTests"/> : une demande
/// écrite <i>avant</i> la migration ne s'écrit que sur une base qui n'est pas encore migrée.
/// </para>
/// </remarks>
public class ExecutionAttemptsTests : IAsyncLifetime
{
  private const string TheExecutionAttempts = "20260914130420_AddExecutionAttempts";

  private static readonly Guid ARequestOfTheDayBefore = new("dddddddd-dddd-dddd-dddd-dddddddddddd");

  private readonly PostgreSqlContainer _container =
    new PostgreSqlBuilder("postgres:18-alpine").Build();

  private List<string> _migrations = [];

  public async Task InitializeAsync()
  {
    await _container.StartAsync();

    await using var dbContext = NewDbContext();

    _migrations = [.. dbContext.Database.GetMigrations()];

    await dbContext.GetService<IMigrator>().MigrateAsync(_migrations[_migrations.IndexOf(TheExecutionAttempts) - 1]);
    await WriteARequestOfTheDayBeforeAsync(dbContext);
    await dbContext.Database.MigrateAsync();
  }

  public Task DisposeAsync() => _container.DisposeAsync().AsTask();

  /// <summary>La migration vient <b>immédiatement</b> après le vidage des détections pour la taxonomie prototype.</summary>
  [Fact]
  public void ComesRightAfterTheClearingOfTheScreenings()
  {
    _migrations.IndexOf(TheExecutionAttempts)
      .ShouldBe(_migrations.IndexOf("20260914090754_ClearScreeningsForThePrototypeTaxonomy") + 1);
  }

  /// <summary>
  /// Les neuf colonnes arrivent sous leur nom <c>snake_case</c>, dans leur type — seul le statut HTTP
  /// est nullable : il n'y en a pas quand le système hôte n'a pas répondu.
  /// </summary>
  [Fact]
  public async Task CreatesTheNineColumnsOfTheExecutionLog()
  {
    await using var dbContext = NewDbContext();

    var columns = await dbContext.Database.SqlQueryRaw<string>(
      """
      SELECT column_name || ' ' || data_type || ' ' || is_nullable AS "Value"
      FROM information_schema.columns
      WHERE table_name = 'execution_attempts'
      """).ToListAsync();

    columns.ShouldBe(
      [
        "id uuid NO",
        "data_subject_request_id uuid NO",
        "data_subject_right text NO",
        "called_url text NO",
        "started_at timestamp with time zone NO",
        "duration interval NO",
        "outcome text NO",
        "http_status integer YES",
        "created_by text NO",
      ],
      ignoreOrder: true);
  }

  /// <summary>
  /// ⚠️ <b>Une clé primaire, et aucune clé étrangère</b> : la tentative survit à la suppression de la
  /// demande qu'elle référence.
  /// </summary>
  [Fact]
  public async Task DeclaresNoForeignKeyTowardsTheRequests()
  {
    await using var dbContext = NewDbContext();

    var constraints = await dbContext.Database.SqlQueryRaw<string>(
      """
      SELECT constraint_name || ' ' || constraint_type AS "Value"
      FROM information_schema.table_constraints
      WHERE table_name = 'execution_attempts' AND constraint_type IN ('PRIMARY KEY', 'FOREIGN KEY')
      """).ToListAsync();

    constraints.ShouldBe(["pk_execution_attempts PRIMARY KEY"]);
  }

  /// <summary>
  /// <b>La table naît vide, et les demandes d'avant restent telles quelles</b> : aucune tentative n'est
  /// inventée pour une demande qui n'a jamais été exécutée.
  /// </summary>
  [Fact]
  public async Task InventsNoAttemptForTheRequestsOfTheDayBefore()
  {
    await using var dbContext = NewDbContext();

    (await dbContext.Database.SqlQueryRaw<int>("""SELECT count(*)::int AS "Value" FROM execution_attempts""").SingleAsync())
      .ShouldBe(0);

    (await dbContext.Database.SqlQueryRaw<string>("""SELECT status AS "Value" FROM data_subject_requests""").ToListAsync())
      .ShouldBe(["InProgress"]);
  }

  /// <summary>Une demande, écrite avec les colonnes de sa date.</summary>
  private static async Task WriteARequestOfTheDayBeforeAsync(AppDbContext dbContext)
  {
    await dbContext.Database.ExecuteSqlAsync(
      $"""
      INSERT INTO data_subject_requests
        (id, origin, received_on, response_deadline, last_name, first_name, email, identity_verified,
         message, data_subject_right, status, created_by, created_at)
      VALUES
        ({ARequestOfTheDayBefore}, 'Email', DATE '2026-09-01', DATE '2026-10-01', NULL, NULL,
         'jeanne@exemple.fr', true, 'Je souhaite accéder à mes données.', 'Access', 'InProgress',
         'operator', TIMESTAMPTZ '2026-09-01 08:00:00+00');
      """);
  }

  private AppDbContext NewDbContext() =>
    new(new DbContextOptionsBuilder<AppDbContext>()
      .UseNpgsql(_container.GetConnectionString())
      .Options);
}
