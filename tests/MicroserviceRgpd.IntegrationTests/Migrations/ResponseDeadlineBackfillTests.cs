using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.Infrastructure.Data;
using Testcontainers.PostgreSql;

namespace MicroserviceRgpd.IntegrationTests.Migrations;

/// <summary>
/// <b>Les demandes enregistrées avant l'ajout de la date limite la reçoivent depuis leur date de réception</b>
/// (ADR-0021). Une base est montée au dernier état d'<i>avant</i> la date limite, on y écrit trois
/// demandes avec les colonnes de cette date-là — dont un 31 janvier et un 31 mars —, puis on joue la
/// migration.
/// </summary>
/// <remarks>
/// ⚠️ <b>C'est le seul endroit d'où la reprise puisse être éprouvée</b>, pour la raison que donne
/// <see cref="ListingOriginBackfillTests"/> : une base neuve n'a aucune demande ancienne, et une
/// migration qui aurait oublié de remplir l'existant y afficherait vert. Il monte donc son
/// <b>propre</b> conteneur.
/// </remarks>
public class ResponseDeadlineBackfillTests : IAsyncLifetime
{
  internal const string TheResponseDeadlineAddition = "20260911191853_AddDataSubjectRequestResponseDeadline";

  private static readonly Guid AnEndOfJanuary = new("88888888-8888-8888-8888-888888888888");
  private static readonly Guid AnEndOfMarch = new("99999999-9999-9999-9999-999999999999");
  private static readonly Guid AnOrdinaryDay = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

  private readonly PostgreSqlContainer _container =
    new PostgreSqlBuilder("postgres:18-alpine").Build();

  private List<string> _migrations = [];

  public async Task InitializeAsync()
  {
    await _container.StartAsync();

    await using var dbContext = NewDbContext();

    _migrations = [.. dbContext.Database.GetMigrations()];

    await dbContext.GetService<IMigrator>().MigrateAsync(RequestStatusBackfillTests.TheStatusAddition);
    await WriteThreeRequestsOfTheDayBeforeAsync(dbContext);
    await dbContext.Database.MigrateAsync();
  }

  public Task DisposeAsync() => _container.DisposeAsync().AsTask();

  /// <summary>La migration vient <b>immédiatement</b> après celle du statut.</summary>
  [Fact]
  public void ComesRightAfterTheStatusAddition()
  {
    _migrations.IndexOf(TheResponseDeadlineAddition)
      .ShouldBe(_migrations.IndexOf(RequestStatusBackfillTests.TheStatusAddition) + 1);
  }

  /// <summary>
  /// Chaque demande écrite avant la migration se relit <b>par le domaine</b> avec la date limite de
  /// RG1 : un mois après sa réception, ramené au dernier jour du mois quand ce jour n'existe pas.
  /// </summary>
  [Fact]
  public async Task GivesEveryRequestOfTheDayBeforeItsResponseDeadline()
  {
    await using var dbContext = NewDbContext();

    var deadlines = await dbContext.DataSubjectRequests
      .Select(request => new { request.Id, request.ResponseDeadline })
      .ToListAsync();

    deadlines.Select(read => (read.Id.Value, read.ResponseDeadline)).ShouldBe(
      [
        (AnEndOfJanuary, new DateOnly(2026, 2, 28)),
        (AnEndOfMarch, new DateOnly(2026, 4, 30)),
        (AnOrdinaryDay, new DateOnly(2026, 10, 1)),
      ],
      ignoreOrder: true);
  }

  /// <summary>
  /// ⚠️ <b>Et la colonne ne garde aucune valeur par défaut.</b> Seule la fabrique fixe la date
  /// limite : un <c>DEFAULT</c> resté en base en serait une seconde source, que rien ne relirait.
  /// </summary>
  [Fact]
  public async Task LeavesNoDefaultBehindOnTheResponseDeadlineColumn()
  {
    await using var dbContext = NewDbContext();

    var defaults = await dbContext.Database.SqlQueryRaw<string?>(
      """
      SELECT column_default AS "Value" FROM information_schema.columns
      WHERE table_name = 'data_subject_requests' AND column_name = 'response_deadline'
      """).ToListAsync();

    defaults.ShouldBe([null]);
  }

  /// <summary>Trois demandes, écrites avec les colonnes de leur date : rien qui dise leur date limite.</summary>
  private static async Task WriteThreeRequestsOfTheDayBeforeAsync(AppDbContext dbContext)
  {
    await dbContext.Database.ExecuteSqlAsync(
      $"""
      INSERT INTO data_subject_requests
        (id, origin, received_on, last_name, first_name, email, identity_verified, message,
         data_subject_right, status, created_by, created_at)
      VALUES
        ({AnEndOfJanuary}, 'Email', DATE '2026-01-31', NULL, NULL, 'jeanne@exemple.fr', false,
         'Je souhaite accéder à mes données.', 'Access', 'InProgress', 'operator',
         TIMESTAMPTZ '2026-02-01 08:00:00+00'),
        ({AnEndOfMarch}, 'Letter', DATE '2026-03-31', 'Martin', 'Paul', NULL, true,
         'Merci d''effacer mon compte.', 'Erasure', 'InProgress', 'operator',
         TIMESTAMPTZ '2026-09-02 09:00:00+00'),
        ({AnOrdinaryDay}, 'Email', DATE '2026-09-01', NULL, NULL, 'paul@exemple.fr', false,
         'Merci de rectifier mon adresse.', 'Rectification', 'InProgress', 'operator',
         TIMESTAMPTZ '2026-09-01 10:00:00+00');
      """);
  }

  private AppDbContext NewDbContext() =>
    new(new DbContextOptionsBuilder<AppDbContext>()
      .UseNpgsql(_container.GetConnectionString())
      .Options);
}
