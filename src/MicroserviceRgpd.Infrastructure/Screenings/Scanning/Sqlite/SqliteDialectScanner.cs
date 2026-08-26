using System.Data;
using System.Globalization;
using Microsoft.Data.Sqlite;
using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.Infrastructure.Screenings.Scanning.Sqlite;

/// <summary>
/// Le dialecte SQLite : il ouvre le fichier en lecture seule, relève son catalogue, prélève ses
/// valeurs, et n'en laisse rien sortir d'autre que le pivot et les aperçus.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Le pilote n'a pas d'asynchrone, et son jeton d'annulation est inerte.</b>
/// <c>Microsoft.Data.Sqlite</c> exécute ses <c>ExecuteReaderAsync</c> de façon synchrone et ignore
/// le <see cref="CancellationToken"/> : un scan SQLite occupe donc un fil du début à la fin, ce qui
/// est assumé ici et pas ailleurs. L'annulation passe par
/// <c>SQLitePCL.raw.sqlite3_interrupt</c> sur la poignée de la connexion — l'API gérée, sans
/// <c>P/Invoke</c> écrit à la main — et c'est ce qui coupe la <b>requête en cours</b> plutôt que la
/// boucle qui l'entoure.
/// </para>
/// <para>
/// ⚠️ <b>Le fichier est ouvert en lecture seule, et ce n'est pas de la prudence décorative.</b>
/// Ouvert en écriture, SQLite <b>crée</b> silencieusement le fichier absent et rend une base sans
/// table : une faute de frappe dans le chemin deviendrait « base sans table » au lieu de « fichier
/// introuvable », et le service aurait écrit un fichier chez le client par-dessus le marché.
/// </para>
/// <para>
/// ⚠️ <b>Le pool est coupé.</b> Une connexion rendue au pool garde le fichier du client ouvert
/// après la fin du scan ; ici, la fin du scan est la fin de la connexion, et rien ne survit à
/// l'écran.
/// </para>
/// <para>
/// ⚠️ <b>La famille <see cref="ScanFailureFamily.Network"/> ne sort jamais d'ici.</b> Un fichier
/// SQLite est local : il n'y a pas d'hôte à joindre. Elle vit chez PostgreSQL, et attend MySQL.
/// </para>
/// </remarks>
internal sealed class SqliteDialectScanner : IDialectScanner
{
  private const int Interrupted = 9;
  private const int PermissionDenied = 3;
  private const int AuthorisationDenied = 23;
  private const int CannotOpen = 14;
  private const int NotADatabase = 26;

  private readonly TimeProvider _clock;

  public SqliteDialectScanner(TimeProvider clock)
  {
    ArgumentNullException.ThrowIfNull(clock);

    _clock = clock;
  }

  /// <inheritdoc />
  public DatabaseDialect Dialect => DatabaseDialect.Sqlite;

  /// <inheritdoc />
  public Task<ScanOutcome> ScanAsync(
    string connectionString,
    IProgress<ScanStep>? progress,
    CancellationToken cancellationToken)
  {
    return Task.Run(() => Scan(connectionString, progress, cancellationToken), cancellationToken);
  }

  private static bool TryPrepare(
    string connectionString,
    out string prepared,
    out string database)
  {
    prepared = string.Empty;
    database = string.Empty;

    SqliteConnectionStringBuilder builder;

    try
    {
      builder = new SqliteConnectionStringBuilder(connectionString);
    }
    catch (ArgumentException)
    {
      // Une chaîne que le pilote ne sait même pas lire. Rien de ce qu'elle contient ne ressort :
      // c'est le message du pilote qui porterait le chemin du fichier.
      return false;
    }

    if (string.IsNullOrWhiteSpace(builder.DataSource))
    {
      return false;
    }

    // ⚠️ Seul le nom du fichier part dans le pivot — jamais le chemin, jamais le dossier parent. Un
    // chemin dit où vit la base du client, et « rien de réel ne reste » couvre aussi cela.
    var fileName = Path.GetFileName(builder.DataSource.Trim());

    // ⚠️ Un nom que le domaine refuserait ferait rendre un pivot que l'ingestion rejetterait en
    // MissingHeader — un scan qui a réussi, rendu par un refus parlant d'en-tête. Mieux vaut le dire
    // ici, où c'est encore « ce qui a été fourni au service ».
    if (string.IsNullOrWhiteSpace(fileName) || fileName.Any(char.IsControl))
    {
      return false;
    }

    database = fileName.Length > Screening.MaxDatabaseNameLength
      ? fileName[..Screening.MaxDatabaseNameLength]
      : fileName;

    builder.Mode = SqliteOpenMode.ReadOnly;
    builder.Pooling = false;
    prepared = builder.ConnectionString;

    return true;
  }

  private static bool IsCatalogued(SqliteConnection connection)
  {
    using var command = connection.CreateCommand();
    command.CommandText = SqliteScanQueries.DatabasePresence;

    return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture) > 0;
  }

  private static List<ScannedColumn> ReadCatalogue(SqliteConnection connection)
  {
    using var command = connection.CreateCommand();
    command.CommandText = SqliteScanQueries.Catalogue;

    using var reader = command.ExecuteReader();
    var columns = new List<ScannedColumn>();

    while (reader.Read())
    {
      var declaredType = reader.IsDBNull(4) ? null : reader.GetString(4);
      var schema = reader.GetString(0);
      var table = reader.GetString(1);
      var column = reader.GetString(2);

      columns.Add(
        new ScannedColumn(
          ColumnIdentity.Of(schema, table, column),
          reader.GetInt32(3),
          string.IsNullOrWhiteSpace(declaredType) ? null : declaredType,
          reader.GetInt64(5) != 0,
          reader.IsDBNull(6) ? null : reader.GetString(6))
        {
          RawSchema = schema,
          RawTable = table,
          RawColumn = column,
        });
    }

    return columns;
  }

  private static void Interrupt(SqliteConnection connection)
  {
    try
    {
      if (connection.State == ConnectionState.Open && connection.Handle is { } handle)
      {
        SQLitePCL.raw.sqlite3_interrupt(handle);
      }
    }
    catch (ObjectDisposedException)
    {
      // La connexion s'est refermée entre-temps : il n'y a plus de requête à couper, et c'est très
      // exactement ce que l'annulation voulait.
    }
    catch (InvalidOperationException)
    {
      // Idem : la poignée n'est plus lisible parce que la connexion n'est plus ouverte.
    }
  }

  private static bool IsInterrupt(SqliteException failure)
  {
    return failure.SqliteErrorCode == Interrupted;
  }

  private static ScanFailureFamily FamilyOf(SqliteException failure)
  {
    return failure.SqliteErrorCode switch
    {
      CannotOpen or NotADatabase => ScanFailureFamily.Supplied,
      _ => ScanFailureFamily.Database,
    };
  }

  private static PreviewAbsenceReason ReasonFor(SqliteException failure)
  {
    return failure.SqliteErrorCode switch
    {
      PermissionDenied or AuthorisationDenied => PreviewAbsenceReason.AccessDenied,
      _ => PreviewAbsenceReason.ReadFailed,
    };
  }

  private static PreviewedValue ReadValue(SqliteDataReader reader)
  {
    if (reader.IsDBNull(1))
    {
      return PreviewedValue.NullValue;
    }

    var text = reader.GetString(1);

    if (text.Length == 0)
    {
      return PreviewedValue.EmptyText;
    }

    var fitted = SampledText.Fit(text);
    var declared = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);

    // ⚠️ La longueur se compare à ce que le SGBD a <b>envoyé</b>, pas à ce que le garde-fou a gardé.
    // Prendre la longueur d'après coupe ferait dire « intacte » d'une valeur d'émojis dont le
    // garde-fou vient de retirer la moitié — et « tronqué » est ce qui écarte une valeur du compte
    // d'une règle de format.
    return PreviewedValue.Of(fitted, Math.Max(declared, text.Length));
  }

  private ScanOutcome Scan(
    string connectionString,
    IProgress<ScanStep>? progress,
    CancellationToken cancellationToken)
  {
    if (!TryPrepare(connectionString, out var prepared, out var database))
    {
      return ScanOutcome.Failed(ScanPhase.Connecting, ScanFailureFamily.Supplied);
    }

    using var connection = new SqliteConnection(prepared);

    try
    {
      connection.Open();
    }
    catch (SqliteException failure)
    {
      return ScanOutcome.Failed(ScanPhase.Connecting, FamilyOf(failure));
    }
    catch (InvalidOperationException)
    {
      return ScanOutcome.Failed(ScanPhase.Connecting, ScanFailureFamily.Supplied);
    }

    using var interruption = cancellationToken.Register(() => Interrupt(connection));

    // ⚠️ <c>sqlite3_interrupt</c> ne coupe que ce qui court <b>déjà</b> : il ne marque pas la
    // connexion pour les requêtes à venir. Une annulation arrivée avant la première d'entre elles
    // serait donc perdue, et le catalogue du client serait lu en entier pour rien.
    cancellationToken.ThrowIfCancellationRequested();

    List<ScannedColumn> columns;
    List<IGrouping<TableIdentity, ScannedColumn>> tables;

    try
    {
      if (!IsCatalogued(connection))
      {
        return ScanOutcome.DatabaseAbsentFromCatalogue();
      }

      columns = ReadCatalogue(connection);
      tables = [.. columns.GroupBy(column => column.Identity.TableIdentity)];
    }
    catch (SqliteException failure)
    {
      if (IsInterrupt(failure))
      {
        throw new OperationCanceledException(cancellationToken);
      }

      return ScanOutcome.Failed(ScanPhase.Cataloguing, FamilyOf(failure));
    }
    catch (ArgumentException)
    {
      // ⚠️ Un nom d'objet que le domaine refuse — vide, démesuré, porteur d'un caractère de
      // contrôle — vient du schéma du client, pas d'un défaut du service. Le laisser remonter ferait
      // traverser le port une exception, là où l'écran attend une fin nommée ; et la recopier dans
      // un message rendrait au passage un nom de table du client.
      return ScanOutcome.Failed(ScanPhase.Cataloguing, ScanFailureFamily.Database);
    }

    progress?.Report(ScanStep.CatalogueRead(tables.Count));

    if (columns.Count == 0)
    {
      return ScanOutcome.NoTable();
    }

    var previews = new Dictionary<ColumnIdentity, ColumnPreview>();
    var sampled = 0;

    foreach (var table in tables)
    {
      cancellationToken.ThrowIfCancellationRequested();

      SampleTable(connection, [.. table], previews, cancellationToken);
      progress?.Report(ScanStep.TableSampled(++sampled, tables.Count));
    }

    var pivot = PivotWriter.Write(Dialect, database, _clock.GetUtcNow(), columns);

    return ScanOutcome.Listed(pivot, previews);
  }

  private static void SampleTable(
    SqliteConnection connection,
    IReadOnlyList<ScannedColumn> columns,
    Dictionary<ColumnIdentity, ColumnPreview> previews,
    CancellationToken cancellationToken)
  {
    // ⚠️ Les noms qui entrent dans la requête sont ceux du catalogue, jamais ceux que le domaine a
    // normalisés — voir ScannedColumn.RawSchema.
    var schema = columns[0].RawSchema;
    var table = columns[0].RawTable;

    for (var start = 0; start < columns.Count; start += SqliteScanQueries.MaxBranchesPerQuery)
    {
      // Une table très large se prélève en plusieurs requêtes : sans ce contrôle, l'annulation
      // n'aurait aucune prise entre deux d'entre elles.
      cancellationToken.ThrowIfCancellationRequested();

      var slice = columns
        .Skip(start)
        .Take(SqliteScanQueries.MaxBranchesPerQuery)
        .Select((column, index) => (Index: index, Column: column.RawColumn))
        .ToList();

      try
      {
        SampleSlice(connection, schema, table, slice, columns, start, previews);
      }
      catch (SqliteException failure)
      {
        if (IsInterrupt(failure))
        {
          throw new OperationCanceledException(cancellationToken);
        }

        // ⚠️ L'échec d'une table ne fait pas tomber le scan : chaque colonne qu'elle porte reçoit
        // une raison nommée, et le relevé reste entier. Aucune exception du pilote ne remonte, et
        // rien de son message n'est recopié.
        var reason = ReasonFor(failure);

        foreach (var column in columns.Skip(start).Take(SqliteScanQueries.MaxBranchesPerQuery))
        {
          previews.TryAdd(column.Identity, ColumnPreview.Absent(reason));
        }
      }
    }
  }

  private static void SampleSlice(
    SqliteConnection connection,
    string schema,
    string table,
    IReadOnlyList<(int Index, string Column)> slice,
    IReadOnlyList<ScannedColumn> columns,
    int start,
    Dictionary<ColumnIdentity, ColumnPreview> previews)
  {
    var readable = new Dictionary<int, List<PreviewedValue>>();
    var carriesRows = false;

    using (var command = connection.CreateCommand())
    {
      command.CommandText = SqliteScanQueries.Sample(schema, table, slice);

      using var reader = command.ExecuteReader();

      while (reader.Read())
      {
        var index = reader.GetInt32(0);

        if (index == SqliteScanQueries.SentinelIndex)
        {
          carriesRows = true;
          continue;
        }

        if (!readable.TryGetValue(index, out var values))
        {
          values = [];
          readable[index] = values;
        }

        values.Add(ReadValue(reader));
      }
    }

    foreach (var (index, _) in slice)
    {
      var column = columns[start + index];

      previews[column.Identity] = readable.TryGetValue(index, out var values) && values.Count > 0
        ? ColumnPreview.Read(values)

        // ⚠️ Zéro valeur veut dire deux choses, et la sentinelle est ce qui les sépare : une table
        // sans ligne n'a rien à rendre, une table pleine dont cette colonne ne rend rien ne porte
        // que du binaire — le filtre au grain de la valeur les a toutes écartées.
        : ColumnPreview.Absent(
          carriesRows
            ? PreviewAbsenceReason.UnsampleableType
            : PreviewAbsenceReason.NoValueReturned);
    }
  }
}
