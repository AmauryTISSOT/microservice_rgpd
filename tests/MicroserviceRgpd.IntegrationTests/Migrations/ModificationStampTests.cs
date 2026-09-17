using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MicroserviceRgpd.Infrastructure.Data;

namespace MicroserviceRgpd.IntegrationTests.Migrations;

/// <summary>
/// <b>L'empreinte de modification arrive sur des colonnes nullables, et n'invente rien.</b> Une base
/// est montée au dernier état d'<i>avant</i> l'empreinte, on y écrit deux demandes avec les colonnes
/// de cette date-là, puis on joue la migration.
/// </summary>
/// <remarks>
/// ⚠️ <b>Il n'y a rien à reprendre, et c'est précisément le fait à tenir.</b> <c>null</c> est la
/// vérité des demandes existantes : elles n'ont jamais été modifiées. Une migration qui les aurait
/// remplies leur prêterait une correction qui n'a pas eu lieu.
/// <para>
/// <b>C'est ce qui lui vaut sa propre base</b>, contre l'usage que pose
/// <c>docs/testing/testcontainers.md</c> — et pour la même raison que
/// <see cref="ResponseDeadlineBackfillTests"/> : <b>l'absence de reprise ne s'éprouve que sur des
/// demandes écrites avant la migration</b>. La base partagée de la suite arrive déjà migrée, où
/// une migration qui aurait rempli l'existant afficherait vert faute de demande ancienne.
/// </para>
/// </remarks>
public class ModificationStampTests : IAsyncLifetime
{
  private const string TheModificationAddition = "20260912095930_AddDataSubjectRequestModification";

  private static readonly Guid AnEmailRequest = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
  private static readonly Guid ALetterRequest = new("cccccccc-cccc-cccc-cccc-cccccccccccc");

  private string _database = string.Empty;

  private List<string> _migrations = [];

  public async Task InitializeAsync()
  {
    _database = await PostgreSqlServer.NewDatabaseAsync(nameof(ModificationStampTests));

    await using var dbContext = NewDbContext();

    _migrations = [.. dbContext.Database.GetMigrations()];

    await dbContext.GetService<IMigrator>()
      .MigrateAsync(ResponseDeadlineBackfillTests.TheResponseDeadlineAddition);
    await WriteTwoRequestsOfTheDayBeforeAsync(dbContext);
    await dbContext.Database.MigrateAsync();
  }

  public Task DisposeAsync() => Task.CompletedTask;

  /// <summary>La migration vient <b>immédiatement</b> après celle de la date limite de réponse.</summary>
  [Fact]
  public void ComesRightAfterTheResponseDeadlineAddition()
  {
    _migrations.IndexOf(TheModificationAddition)
      .ShouldBe(_migrations.IndexOf(ResponseDeadlineBackfillTests.TheResponseDeadlineAddition) + 1);
  }

  /// <summary>
  /// Les deux colonnes arrivent sous leur nom <c>snake_case</c>, dans leur type, et <b>nullables</b>.
  /// </summary>
  [Fact]
  public async Task AddsTheTwoNullableStampColumns()
  {
    await using var dbContext = NewDbContext();

    var columns = await dbContext.Database.SqlQueryRaw<string>(
      """
      SELECT column_name || ' ' || data_type || ' ' || is_nullable AS "Value"
      FROM information_schema.columns
      WHERE table_name = 'data_subject_requests' AND column_name LIKE 'modified%'
      """).ToListAsync();

    columns.ShouldBe(
      ["modified_at timestamp with time zone YES", "modified_by text YES"],
      ignoreOrder: true);
  }

  /// <summary>
  /// ⚠️ <b>Et les demandes d'avant restent sans empreinte</b> : elles n'ont jamais été modifiées.
  /// </summary>
  [Fact]
  public async Task LeavesEveryRequestOfTheDayBeforeWithoutAStamp()
  {
    await using var dbContext = NewDbContext();

    var stamps = await dbContext.Database.SqlQueryRaw<string>(
      """
      SELECT id || ' ' || coalesce(modified_by, 'null') || ' ' || coalesce(modified_at::text, 'null') AS "Value"
      FROM data_subject_requests
      """).ToListAsync();

    stamps.ShouldBe(
      [$"{AnEmailRequest} null null", $"{ALetterRequest} null null"],
      ignoreOrder: true);
  }

  /// <summary>
  /// ⚠️ <b>Aucune des deux colonnes ne garde de valeur par défaut.</b> Seul le geste de modification
  /// posera l'empreinte ; un <c>DEFAULT</c> en base la poserait à la réception.
  /// </summary>
  [Fact]
  public async Task LeavesNoDefaultBehindOnTheStampColumns()
  {
    await using var dbContext = NewDbContext();

    var defaults = await dbContext.Database.SqlQueryRaw<string?>(
      """
      SELECT column_default AS "Value" FROM information_schema.columns
      WHERE table_name = 'data_subject_requests' AND column_name LIKE 'modified%'
      """).ToListAsync();

    defaults.ShouldBe([null, null]);
  }

  /// <summary>Deux demandes, écrites avec les colonnes de leur date : rien qui dise leur empreinte.</summary>
  private static async Task WriteTwoRequestsOfTheDayBeforeAsync(AppDbContext dbContext)
  {
    await dbContext.Database.ExecuteSqlAsync(
      $"""
      INSERT INTO data_subject_requests
        (id, origin, received_on, response_deadline, last_name, first_name, email, identity_verified,
         message, data_subject_right, status, created_by, created_at)
      VALUES
        ({AnEmailRequest}, 'Email', DATE '2026-09-01', DATE '2026-10-01', NULL, NULL,
         'jeanne@exemple.fr', false, 'Je souhaite accéder à mes données.', 'Access', 'InProgress',
         'operator', TIMESTAMPTZ '2026-09-01 08:00:00+00'),
        ({ALetterRequest}, 'Letter', DATE '2026-03-31', DATE '2026-04-30', 'Martin', 'Paul', NULL,
         true, 'Merci d''effacer mon compte.', 'Erasure', 'InProgress', 'operator',
         TIMESTAMPTZ '2026-09-02 09:00:00+00');
      """);
  }

  private AppDbContext NewDbContext() =>
    new(new DbContextOptionsBuilder<AppDbContext>()
      .UseNpgsql(_database)
      .Options);
}
