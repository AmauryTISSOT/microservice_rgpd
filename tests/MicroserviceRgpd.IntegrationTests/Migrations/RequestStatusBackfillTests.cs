using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.Infrastructure.Data;
using Testcontainers.PostgreSql;

namespace MicroserviceRgpd.IntegrationTests.Migrations;

/// <summary>
/// <b>Les demandes enregistrées avant le statut sont reprises <c>InProgress</c></b> (ADR-0021). Une
/// base est montée au dernier état d'<i>avant</i> le statut, on y écrit deux demandes avec les
/// colonnes de cette date-là, puis on joue la migration.
/// </summary>
/// <remarks>
/// ⚠️ <b>C'est le seul endroit d'où la reprise puisse être éprouvée</b>, pour la raison que donne
/// <see cref="ListingOriginBackfillTests"/> : une base neuve n'a aucune demande ancienne, et une
/// migration qui aurait oublié de remplir l'existant y afficherait vert. Il monte donc son
/// <b>propre</b> conteneur.
/// </remarks>
public class RequestStatusBackfillTests : IAsyncLifetime
{
  internal const string TheStatus = "20260911185225_AddDataSubjectRequestStatus";

  private static readonly Guid AnEmail = new("66666666-6666-6666-6666-666666666666");
  private static readonly Guid ALetter = new("77777777-7777-7777-7777-777777777777");

  private readonly PostgreSqlContainer _container =
    new PostgreSqlBuilder("postgres:18-alpine").Build();

  private List<string> _migrations = [];

  public async Task InitializeAsync()
  {
    await _container.StartAsync();

    await using var dbContext = NewDbContext();

    _migrations = [.. dbContext.Database.GetMigrations()];

    await dbContext.GetService<IMigrator>().MigrateAsync(CreateDataSubjectRequestsTests.TheCreation);
    await WriteTwoRequestsOfTheDayBeforeAsync(dbContext);
    await dbContext.Database.MigrateAsync();
  }

  public Task DisposeAsync() => _container.DisposeAsync().AsTask();

  /// <summary>La migration vient <b>immédiatement</b> après la création de la table.</summary>
  [Fact]
  public void ComesRightAfterTheCreationOfTheTable()
  {
    _migrations.IndexOf(TheStatus).ShouldBe(_migrations.IndexOf(CreateDataSubjectRequestsTests.TheCreation) + 1);
  }

  /// <summary>
  /// Chaque demande écrite avant la migration se relit <b>par le domaine</b>, et elle se lit
  /// <c>InProgress</c> : aucun <c>Gesture</c> n'existait qui l'aurait terminée ou annulée.
  /// </summary>
  [Fact]
  public async Task ReadsEveryRequestOfTheDayBeforeInProgress()
  {
    await using var dbContext = NewDbContext();

    var statuses = await dbContext.DataSubjectRequests
      .Where(request => request.Id == DataSubjectRequestId.From(AnEmail) || request.Id == DataSubjectRequestId.From(ALetter))
      .Select(request => request.Status)
      .ToListAsync();

    statuses.ShouldBe([RequestStatus.InProgress, RequestStatus.InProgress]);
  }

  /// <summary>
  /// ⚠️ <b>Et la colonne ne garde aucune valeur par défaut.</b> Seule la fabrique fixe le statut de
  /// naissance : un <c>DEFAULT</c> resté en base en serait une seconde source, que rien ne relirait.
  /// </summary>
  [Fact]
  public async Task LeavesNoDefaultBehindOnTheStatusColumn()
  {
    await using var dbContext = NewDbContext();

    var defaults = await dbContext.Database.SqlQueryRaw<string?>(
      """
      SELECT column_default AS "Value" FROM information_schema.columns
      WHERE table_name = 'data_subject_requests' AND column_name = 'status'
      """).ToListAsync();

    defaults.ShouldBe([null]);
  }

  /// <summary>Deux demandes, écrites avec les colonnes de leur date : rien qui dise où elles en sont.</summary>
  private static async Task WriteTwoRequestsOfTheDayBeforeAsync(AppDbContext dbContext)
  {
    await dbContext.Database.ExecuteSqlAsync(
      $"""
      INSERT INTO data_subject_requests
        (id, origin, received_on, last_name, first_name, email, identity_verified, message,
         data_subject_right, created_by, created_at)
      VALUES
        ({AnEmail}, 'Email', DATE '2026-09-01', NULL, NULL, 'jeanne@exemple.fr', false,
         'Je souhaite accéder à mes données.', 'Access', 'operator', TIMESTAMPTZ '2026-09-01 08:00:00+00'),
        ({ALetter}, 'Letter', DATE '2026-08-20', 'Martin', 'Paul', NULL, true,
         'Merci d''effacer mon compte.', 'Erasure', 'operator', TIMESTAMPTZ '2026-09-02 09:00:00+00');
      """);
  }

  private AppDbContext NewDbContext() =>
    new(new DbContextOptionsBuilder<AppDbContext>()
      .UseNpgsql(_container.GetConnectionString())
      .Options);
}
