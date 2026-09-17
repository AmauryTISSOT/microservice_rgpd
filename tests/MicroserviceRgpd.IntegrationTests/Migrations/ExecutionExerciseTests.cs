using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MicroserviceRgpd.Infrastructure.Data;
using Testcontainers.PostgreSql;

namespace MicroserviceRgpd.IntegrationTests.Migrations;

/// <summary>
/// <b>La colonne du journal d'exécution qui portait l'URL appelée devient l'exercice, et les lignes
/// écrites avant gardent leur valeur.</b> Une base est montée au dernier état d'<i>avant</i> le
/// renommage, on y écrit une tentative avec la colonne de cette date-là, puis on joue la migration
/// (ADR-0028).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Le fait à tenir est la conservation</b> : une adresse <i>est</i> un exercice. Une migration
/// qui aurait ajouté une colonne et vidé l'ancienne perdrait la preuve qu'un droit a été remis au
/// système hôte.
/// </para>
/// <para>
/// Son propre conteneur, pour la même raison que <see cref="ExecutionAttemptsTests"/> : une tentative
/// écrite <i>avant</i> la migration ne s'écrit que sur une base qui n'est pas encore migrée.
/// </para>
/// </remarks>
public class ExecutionExerciseTests : IAsyncLifetime
{
  private const string TheExerciseRenaming = "20260917152612_RenameCalledUrlToExercise";

  private const string TheExercisedAddress = "https://brocanto.example.fr/rgpd/effacement";

  private static readonly Guid AnAttemptOfTheDayBefore = new("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

  private static readonly Guid ARequestOfTheDayBefore = new("ffffffff-ffff-ffff-ffff-ffffffffffff");

  private readonly PostgreSqlContainer _container =
    new PostgreSqlBuilder("postgres:18-alpine").Build();

  private List<string> _migrations = [];

  public async Task InitializeAsync()
  {
    await _container.StartAsync();

    await using var dbContext = NewDbContext();

    _migrations = [.. dbContext.Database.GetMigrations()];

    await dbContext.GetService<IMigrator>().MigrateAsync(_migrations[_migrations.IndexOf(TheExerciseRenaming) - 1]);
    await WriteAnAttemptOfTheDayBeforeAsync(dbContext);
    await dbContext.Database.MigrateAsync();
  }

  public Task DisposeAsync() => _container.DisposeAsync().AsTask();

  /// <summary>
  /// <b>La colonne a changé de nom, et rien d'autre n'a bougé</b> : ni colonne ajoutée, ni colonne
  /// discriminante qui redirait le canal.
  /// </summary>
  [Fact]
  public async Task RenamesTheColumnWithoutAddingAny()
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
        "exercise text NO",
        "started_at timestamp with time zone NO",
        "duration interval NO",
        "outcome text NO",
        "http_status integer YES",
        "created_by text NO",
      ],
      ignoreOrder: true);
  }

  /// <summary>
  /// ⚠️ <b>Une tentative écrite avant la migration garde sa valeur</b> : l'adresse qu'elle portait est
  /// désormais son exercice, telle quelle.
  /// </summary>
  [Fact]
  public async Task KeepsTheAddressOfTheAttemptsOfTheDayBefore()
  {
    await using var dbContext = NewDbContext();

    var exercises = await dbContext.Database.SqlQueryRaw<string>(
      """SELECT exercise AS "Value" FROM execution_attempts""").ToListAsync();

    exercises.ShouldBe([TheExercisedAddress]);
  }

  /// <summary>Une tentative, écrite avec les colonnes de sa date.</summary>
  private static async Task WriteAnAttemptOfTheDayBeforeAsync(AppDbContext dbContext)
  {
    await dbContext.Database.ExecuteSqlAsync(
      $"""
      INSERT INTO execution_attempts
        (id, data_subject_request_id, data_subject_right, called_url, started_at, duration, outcome,
         http_status, created_by)
      VALUES
        ({AnAttemptOfTheDayBefore}, {ARequestOfTheDayBefore}, 'Erasure', {TheExercisedAddress},
         TIMESTAMPTZ '2026-09-16 08:00:00+00', INTERVAL '420 milliseconds', 'Succeeded', 204,
         'operator');
      """);
  }

  private AppDbContext NewDbContext() =>
    new(new DbContextOptionsBuilder<AppDbContext>()
      .UseNpgsql(_container.GetConnectionString())
      .Options);
}
