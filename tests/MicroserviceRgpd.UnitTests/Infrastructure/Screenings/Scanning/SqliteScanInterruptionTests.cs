using System.Diagnostics;
using Microsoft.Data.Sqlite;
using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.Infrastructure.Screenings.Scanning;
using MicroserviceRgpd.Infrastructure.Screenings.Scanning.Sqlite;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Screenings.Scanning;

/// <summary>
/// L'annulation coupe la <b>requête</b>, pas seulement la boucle qui l'entoure.
/// </summary>
/// <remarks>
/// ⚠️ <b>Un jeton qui ne ferait que sortir de la boucle laisserait un <c>SELECT</c> courir sur la
/// base du client</b> après que l'<c>Operator</c> a quitté l'écran — et le service ne saurait même
/// pas qu'il le fait courir. Sous SQLite, le pilote n'a pas d'asynchrone et son jeton est inerte :
/// la seule voie est <c>sqlite3_interrupt</c> sur la poignée de la connexion.
/// </remarks>
public class SqliteScanInterruptionTests
{
  private static readonly DateTimeOffset Noon = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

  private const int Columns = 12;
  private const int Rows = 30_000;

  /// <summary>
  /// Une table large et longue, dont <b>chaque</b> colonne est binaire : le filtre au grain de la
  /// valeur écarte tout, aucun <c>LIMIT 5</c> n'est jamais atteint, et chaque branche parcourt donc
  /// la table entière. C'est la fenêtre pendant laquelle l'annulation a quelque chose à couper.
  /// </summary>
  private static string ALongScan()
  {
    var columns = string.Join(", ", Enumerable.Range(0, Columns).Select(index => $"c{index} BLOB"));
    var values = string.Join(", ", Enumerable.Range(0, Columns).Select(_ => "randomblob(64)"));
    var names = string.Join(", ", Enumerable.Range(0, Columns).Select(index => $"c{index}"));

    return $"""
      CREATE TABLE gros (id INTEGER PRIMARY KEY, {columns});
      INSERT INTO gros (id, {names})
      WITH RECURSIVE n(i) AS (
        SELECT 1 UNION ALL SELECT i + 1 FROM n WHERE i < {Rows}
      )
      SELECT i, {values} FROM n;
      """;
  }

  /// <summary>
  /// ⚠️ <b>Aucun <c>SqliteException</c> ne s'échappe, et aucune table n'est rapportée prélevée.</b>
  /// Le second point est ce qui distingue une requête <b>coupée</b> d'une boucle simplement quittée
  /// : si le scan avait attendu la fin du <c>SELECT</c>, il aurait rapporté la table.
  /// </summary>
  [Fact]
  public async Task CutsTheRunningQueryRatherThanTheLoopAroundIt()
  {
    using var database = ASqliteBase.Holding(ALongScan());
    using var abandon = new CancellationTokenSource();

    // Le pas du catalogue est rapporté sur le fil du scan : l'annulation part d'un autre fil, un
    // instant plus tard, pour tomber pendant que le prélèvement court.
    var record = new ARecordOfSteps(step =>
    {
      if (step.Phase == ScanPhase.Cataloguing)
      {
        _ = Task.Run(async () =>
        {
          await Task.Delay(TimeSpan.FromMilliseconds(150), CancellationToken.None);
          abandon.Cancel();
        });
      }
    });

    var scanner = new DatabaseScanner([new SqliteDialectScanner(new AClockStuckAt(Noon))]);
    var stopwatch = Stopwatch.StartNew();

    var abandoned = await Should.ThrowAsync<OperationCanceledException>(
      () => scanner.ScanAsync(
        DatabaseDialect.Sqlite,
        database.ConnectionString,
        record,
        abandon.Token));

    stopwatch.Stop();

    abandoned.ShouldNotBeOfType<SqliteException>();
    abandoned.InnerException.ShouldNotBeOfType<SqliteException>();

    record.Steps.ShouldNotContain(step => step.Phase == ScanPhase.Sampling);
    stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(30));
  }

  /// <summary>
  /// L'hypothèse sur laquelle repose tout ce qui précède, ancrée pour elle-même : une requête
  /// interrompue lève un <see cref="SqliteException"/> de code <b>9</b>. Le jour où le pilote
  /// changera cela, c'est ce test qui le dira — plutôt qu'un scan qui rendrait un échec « la base »
  /// à chaque fois qu'un <c>Operator</c> quitte l'écran.
  /// </summary>
  [Fact]
  public async Task TheDriverSignalsAnInterruptionWithErrorCodeNine()
  {
    using var database = ASqliteBase.Holding(ALongScan());

    using var connection = new SqliteConnection(
      new SqliteConnectionStringBuilder
      {
        DataSource = database.Path,
        Mode = SqliteOpenMode.ReadOnly,
        Pooling = false,
      }.ConnectionString);

    connection.Open();

    var running = Task.Run(() =>
    {
      using var command = connection.CreateCommand();
      // Un produit cartésien : long, mais borné. Une requête infinie ferait pendre indéfiniment le
      // jour où l'interruption cesserait de fonctionner — c'est-à-dire le jour où ce test compte.
      command.CommandText = "SELECT COUNT(1) FROM gros a, gros b WHERE a.id <> b.id";

      command.ExecuteScalar();
    });

    await Task.Delay(TimeSpan.FromMilliseconds(200), CancellationToken.None);
    SQLitePCL.raw.sqlite3_interrupt(connection.Handle!);

    var interrupted = await Should.ThrowAsync<SqliteException>(() => running);

    interrupted.SqliteErrorCode.ShouldBe(9);
  }
}
