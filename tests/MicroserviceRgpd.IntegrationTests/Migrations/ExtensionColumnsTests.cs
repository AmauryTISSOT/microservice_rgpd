using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MicroserviceRgpd.Infrastructure.Data;
using Testcontainers.PostgreSql;

namespace MicroserviceRgpd.IntegrationTests.Migrations;

/// <summary>
/// <b>Les quatre colonnes de la prolongation arrivent nullables, et n'inventent rien.</b> Une base
/// est montée au dernier état d'<i>avant</i> la prolongation, on y écrit deux demandes avec les
/// colonnes de cette date-là, puis on joue la migration (ADR-0029).
/// </summary>
/// <remarks>
/// ⚠️ <b>Il n'y a rien à reprendre, et c'est précisément le fait à tenir.</b> <c>null</c> est la
/// vérité des demandes existantes : aucune n'a été prolongée. Une migration qui les aurait remplies
/// leur prêterait une décision qui n'a pas été prise.
/// <para>
/// <b>C'est ce qui lui vaut son propre conteneur</b>, contre l'usage que pose
/// <c>docs/testing/testcontainers.md</c> — et pour la même raison que
/// <see cref="ModificationStampTests"/> : <b>l'absence de reprise ne s'éprouve que sur des demandes
/// écrites avant la migration</b>. Le conteneur partagé de la suite arrive déjà migré.
/// </para>
/// </remarks>
public class ExtensionColumnsTests : IAsyncLifetime
{
  private const string TheExtensionAddition = "20260917165950_AddDataSubjectRequestExtension";

  private const string BeforeTheExtension = "20260917152612_RenameCalledUrlToExercise";

  /// <summary>Les quatre colonnes, dans l'ordre de leur nom, avec leur type et leur nullabilité.</summary>
  private const string TheFourColumns = """
    SELECT column_name || ' ' || data_type || ' ' || is_nullable AS "Value"
    FROM information_schema.columns
    WHERE table_name = 'data_subject_requests'
      AND column_name IN ('initial_response_deadline', 'extension_ground', 'extension_justification', 'extended_at')
    ORDER BY column_name
    """;

  private static readonly Guid AnEmailRequest = new("dddddddd-dddd-dddd-dddd-dddddddddddd");
  private static readonly Guid ALetterRequest = new("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

  private readonly PostgreSqlContainer _container =
    new PostgreSqlBuilder("postgres:18-alpine").Build();

  private List<string> _migrations = [];

  public async Task InitializeAsync()
  {
    await _container.StartAsync();

    await using var dbContext = NewDbContext();

    _migrations = [.. dbContext.Database.GetMigrations()];

    await dbContext.GetService<IMigrator>().MigrateAsync(BeforeTheExtension);
    await WriteTwoRequestsOfTheDayBeforeAsync(dbContext);
    await dbContext.Database.MigrateAsync();
  }

  public Task DisposeAsync() => _container.DisposeAsync().AsTask();

  /// <summary>La migration vient <b>immédiatement</b> après le renommage de l'exercice.</summary>
  [Fact]
  public void ComesRightAfterTheExerciseRenaming()
  {
    _migrations.IndexOf(TheExtensionAddition).ShouldBe(_migrations.IndexOf(BeforeTheExtension) + 1);
  }

  /// <summary>
  /// Les quatre colonnes arrivent sous leur nom <c>snake_case</c>, dans leur type, et
  /// <b>nullables</b>. ⚠️ <c>initial_response_deadline</c> est une <c>date</c>, comme
  /// <c>response_deadline</c> : c'en est une.
  /// </summary>
  [Fact]
  public async Task AddsTheFourNullableExtensionColumns()
  {
    await using var dbContext = NewDbContext();

    (await dbContext.Database.SqlQueryRaw<string>(TheFourColumns).ToListAsync()).ShouldBe(
    [
      "extended_at timestamp with time zone YES",
      "extension_ground text YES",
      "extension_justification text YES",
      "initial_response_deadline date YES",
    ]);
  }

  /// <summary>
  /// ⚠️ <b>Et les demandes d'avant restent non prolongées</b> : aucune décision ne leur est prêtée.
  /// </summary>
  [Fact]
  public async Task LeavesEveryRequestOfTheDayBeforeUnextended()
  {
    await using var dbContext = NewDbContext();

    var extensions = await dbContext.Database.SqlQueryRaw<string>(
      """
      SELECT id || ' '
        || coalesce(initial_response_deadline::text, 'null') || ' '
        || coalesce(extension_ground, 'null') || ' '
        || coalesce(extension_justification, 'null') || ' '
        || coalesce(extended_at::text, 'null') AS "Value"
      FROM data_subject_requests
      """).ToListAsync();

    extensions.ShouldBe(
      [$"{AnEmailRequest} null null null null", $"{ALetterRequest} null null null null"],
      ignoreOrder: true);
  }

  /// <summary>
  /// ⚠️ <b>Aucune des quatre colonnes ne garde de valeur par défaut.</b> Seul le geste de
  /// prolongation les pose ; un <c>DEFAULT</c> en base les poserait à la réception.
  /// </summary>
  [Fact]
  public async Task LeavesNoDefaultBehindOnTheExtensionColumns()
  {
    await using var dbContext = NewDbContext();

    var defaults = await dbContext.Database.SqlQueryRaw<string?>(
      """
      SELECT column_default AS "Value" FROM information_schema.columns
      WHERE table_name = 'data_subject_requests'
        AND column_name IN ('initial_response_deadline', 'extension_ground', 'extension_justification', 'extended_at')
      """).ToListAsync();

    defaults.ShouldBe([null, null, null, null]);
  }

  /// <summary>Deux demandes, écrites avec les colonnes de leur date : rien qui dise une prolongation.</summary>
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
      .UseNpgsql(_container.GetConnectionString())
      .Options);
}
