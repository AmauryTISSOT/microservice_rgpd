using System.Data;
using System.Globalization;
using MicroserviceRgpd.Core.Screenings;
using MySqlConnector;

namespace MicroserviceRgpd.Infrastructure.Screenings.Scanning.MySql;

/// <summary>
/// Le dialecte MariaDB/MySQL : il joint le serveur sans se placer sur une base, relève son
/// catalogue en une requête, prélève ses valeurs une table à la fois, et n'en laisse rien sortir
/// d'autre que le pivot et les aperçus.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Le pilote est <c>MySqlConnector</c>, et le choix n'est pas de goût.</b>
/// <c>MySql.Data</c> — le pilote d'Oracle — est sous GPL à exception, ce qui engage la licence du
/// service, et son asynchrone est un habillage synchrone : chaque scan bloquerait un fil du pool
/// pendant tout le relevé d'une base tierce. <c>MySqlConnector</c> est MIT, asynchrone jusqu'au
/// socket, et son <see cref="CancellationToken"/> coupe la <b>requête en cours</b> plutôt que la
/// boucle qui l'entoure — ce que le port exige en toutes lettres.
/// </para>
/// <para>
/// ⚠️ <b>Rien n'est demandé sur les privilèges du compte, nulle part.</b> Le garde de
/// <see href="https://github.com/AmauryTISSOT/microservice_rgpd/issues/286">#286</see> est
/// <b>retiré</b> : plus aucun <c>SHOW GRANTS</c>, et donc plus de condensat de mot de passe
/// remonté par MariaDB dans la foulée. Ce que le compte ne voit pas manque du catalogue, et c'est
/// tout ce que le service en saura — voir <see cref="MySqlScanQueries.DatabasePresence"/>.
/// </para>
/// <para>
/// ⚠️ <b>Le prélèvement écarte le binaire par le type déclaré, pas par la valeur.</b> C'est la
/// différence avec SQLite, dont le typage est dynamique : sur MariaDB/MySQL, une colonne
/// <c>BLOB</c> ne porte que des octets, et une colonne <c>TEXT</c> n'en porte jamais. La liste noire
/// suffit donc, et une colonne écartée ne coûte <b>aucune</b> requête — elle reçoit
/// <see cref="PreviewAbsenceReason.UnsampleableType"/> sans qu'on soit allé voir.
/// </para>
/// <para>
/// ⚠️ <b>L'échec d'une table ne fait pas tomber le scan.</b> Chaque colonne qu'elle porte reçoit une
/// raison nommée — « droits refusés » quand c'en est une —, et le relevé reste entier. C'est la
/// forme que prend « aucune exception du pilote ne traverse » au grain de la colonne.
/// </para>
/// </remarks>
internal sealed class MySqlDialectScanner : IDialectScanner
{
  private readonly TimeProvider _clock;

  public MySqlDialectScanner(TimeProvider clock)
  {
    ArgumentNullException.ThrowIfNull(clock);

    _clock = clock;
  }

  /// <inheritdoc />
  public DatabaseDialect Dialect => DatabaseDialect.MySql;

  /// <inheritdoc />
  public async Task<ScanOutcome> ScanAsync(
    string connectionString,
    IProgress<ScanStep>? progress,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    if (!MySqlConnectionSettings.TryPrepare(connectionString, out var settings))
    {
      return ScanOutcome.Failed(ScanPhase.Connecting, ScanFailureFamily.Supplied);
    }

    await using var connection = new MySqlConnection(settings.ConnectionString);

    try
    {
      await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
    }
    catch (MySqlException failure)
    {
      if (MySqlFailures.IsInterrupt(cancellationToken))
      {
        throw new OperationCanceledException(cancellationToken);
      }

      return ScanOutcome.Failed(ScanPhase.Connecting, MySqlFailures.FamilyOf(failure));
    }
    catch (InvalidOperationException)
    {
      // Une chaîne que le pilote a bien lue et qu'il refuse d'ouvrir telle quelle — un mode
      // d'authentification qu'elle réclame et qu'il n'a pas. Rien de son message ne ressort.
      return ScanOutcome.Failed(ScanPhase.Connecting, ScanFailureFamily.Supplied);
    }

    List<MySqlCataloguedColumn> columns;
    List<IGrouping<TableIdentity, MySqlCataloguedColumn>> tables;

    try
    {
      if (!await IsCataloguedAsync(connection, settings.Database, cancellationToken)
        .ConfigureAwait(false))
      {
        return ScanOutcome.DatabaseAbsentFromCatalogue();
      }

      columns = await ReadCatalogueAsync(connection, settings.Database, cancellationToken)
        .ConfigureAwait(false);

      tables = [.. columns.GroupBy(column => column.Scanned.Identity.TableIdentity)];
    }
    catch (MySqlException failure)
    {
      if (MySqlFailures.IsInterrupt(cancellationToken))
      {
        throw new OperationCanceledException(cancellationToken);
      }

      return ScanOutcome.Failed(ScanPhase.Cataloguing, MySqlFailures.FamilyOf(failure));
    }
    catch (ArgumentException)
    {
      // ⚠️ Un nom d'objet que le domaine refuse — vide, démesuré, porteur d'un caractère de
      // contrôle — vient du schéma du client, pas d'un défaut du service. Le laisser remonter ferait
      // traverser le port une exception, là où l'écran attend une fin nommée ; et le recopier dans
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

      await SampleTableAsync(connection, [.. table], previews, cancellationToken)
        .ConfigureAwait(false);

      progress?.Report(ScanStep.TableSampled(++sampled, tables.Count));
    }

    var pivot = PivotWriter.Write(
      Dialect,
      settings.Database,
      _clock.GetUtcNow(),
      MySqlCatalogue.InPivotOrder(columns));

    return ScanOutcome.Listed(pivot, previews);
  }

  private static async Task<bool> IsCataloguedAsync(
    MySqlConnection connection,
    string database,
    CancellationToken cancellationToken)
  {
    await using var command = connection.CreateCommand();
    command.CommandText = MySqlScanQueries.DatabasePresence;
    command.Parameters.AddWithValue(MySqlScanQueries.DatabaseParameter, database);

    var present = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

    return Convert.ToInt64(present, CultureInfo.InvariantCulture) > 0;
  }

  private static async Task<List<MySqlCataloguedColumn>> ReadCatalogueAsync(
    MySqlConnection connection,
    string database,
    CancellationToken cancellationToken)
  {
    await using var command = connection.CreateCommand();
    command.CommandText = MySqlScanQueries.Catalogue;
    command.Parameters.AddWithValue(MySqlScanQueries.DatabaseParameter, database);

    await using var reader = await command.ExecuteReaderAsync(cancellationToken)
      .ConfigureAwait(false);

    var columns = new List<MySqlCataloguedColumn>();

    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
    {
      // ⚠️ Nommés un par un, et ce n'est pas de la décoration : six chaînes se suivent, et deux
      // d'entre elles sont les deux commentaires. Interverties, elles compileraient sans un mot et
      // le relevé porterait le commentaire de la table sur la colonne.
      columns.Add(
        MySqlCatalogue.ToColumn(
          schema: reader.GetString(0),
          table: reader.GetString(1),
          column: reader.GetString(2),
          position: reader.GetInt32(3),
          columnType: Text(reader, 4),
          dataType: Text(reader, 5),
          isNullable: Text(reader, 6),
          columnComment: Text(reader, 7),
          tableComment: Text(reader, 8),
          referencedTable: Text(reader, 9)));
    }

    return columns;
  }

  private static string Text(MySqlDataReader reader, int ordinal)
  {
    return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
  }

  /// <summary>
  /// Coupe une valeur qui, malgré le <c>LEFT</c> du SGBD, compte plus de 254 unités UTF-16 : MySQL
  /// compte en caractères Unicode, .NET en unités UTF-16, et un caractère hors du plan multilingue
  /// de base en vaut deux. Le garde-fou ne coupe jamais une paire de substituts en deux.
  /// </summary>
  private static string Fit(string text)
  {
    if (text.Length <= ColumnPreview.MaxValueLength)
    {
      return text;
    }

    var length = ColumnPreview.MaxValueLength;

    if (char.IsHighSurrogate(text[length - 1]))
    {
      length--;
    }

    return text[..length];
  }

  /// <summary>
  /// Une valeur lue, ou <c>null</c> quand le serveur a rendu des <b>octets</b> : c'est le filet
  /// sous la liste noire.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Un type binaire que la liste noire ne connaîtrait pas encore arriverait ici en
  /// <c>byte[]</c>.</b> Le décoder en texte rendrait des octets mutilés par le remplacement Unicode
  /// — un « aperçu » que personne ne peut lire, présenté comme une valeur. La colonne est alors
  /// déclarée non prélevable, ce qui est vrai, et le service n'a pas eu à nommer le type par avance.
  /// </remarks>
  private static PreviewedValue? ReadValue(MySqlDataReader reader)
  {
    if (reader.IsDBNull(1))
    {
      return PreviewedValue.NullValue;
    }

    if (reader.GetValue(1) is byte[])
    {
      return null;
    }

    var text = reader.GetString(1);

    if (text.Length == 0)
    {
      return PreviewedValue.EmptyText;
    }

    var fitted = Fit(text);
    var declared = reader.IsDBNull(2)
      ? 0
      : (int)Math.Min(int.MaxValue, Convert.ToInt64(reader.GetValue(2), CultureInfo.InvariantCulture));

    // ⚠️ La longueur se compare à ce que le SGBD a envoyé, pas à ce que le garde-fou a gardé.
    // Prendre la longueur d'après coupe ferait dire « intacte » d'une valeur d'émojis dont le
    // garde-fou vient de retirer la moitié — et « tronqué » est ce qui écarte une valeur du compte
    // d'une règle de format.
    return PreviewedValue.Of(fitted, Math.Max(declared, text.Length));
  }

  private static async Task SampleTableAsync(
    MySqlConnection connection,
    IReadOnlyList<MySqlCataloguedColumn> columns,
    Dictionary<ColumnIdentity, ColumnPreview> previews,
    CancellationToken cancellationToken)
  {
    // ⚠️ Les noms qui entrent dans la requête sont ceux du catalogue, jamais ceux que le domaine a
    // normalisés — voir ScannedColumn.RawSchema.
    var schema = columns[0].Scanned.RawSchema;
    var table = columns[0].Scanned.RawTable;

    // Le binaire ne coûte pas une requête : il est écarté sur le type déclaré, avant d'aller voir.
    // ⚠️ Une seule partition, et non deux filtres complémentaires : deux prédicats qui se veulent
    // contraires finissent par cesser de l'être, et la colonne tombée entre les deux n'aurait alors
    // aucun aperçu — pas même une raison.
    var sampleable = new List<MySqlCataloguedColumn>();
    var binary = new List<MySqlCataloguedColumn>();

    foreach (var column in columns)
    {
      (MySqlScanQueries.IsSampleable(column.DataType) ? sampleable : binary).Add(column);
    }

    MarkAbsent(binary, PreviewAbsenceReason.UnsampleableType, previews);

    for (var start = 0; start < sampleable.Count; start += MySqlScanQueries.MaxBranchesPerQuery)
    {
      // Une table très large se prélève en plusieurs requêtes : sans ce contrôle, l'annulation
      // n'aurait aucune prise entre deux d'entre elles.
      cancellationToken.ThrowIfCancellationRequested();

      var slice = sampleable
        .Skip(start)
        .Take(MySqlScanQueries.MaxBranchesPerQuery)
        .ToList();

      // ⚠️ Une connexion tombée ne se réinterroge pas table après table. Sans ce contrôle, une base
      // de trois cents tables paierait trois cents allers-retours pour trois cents fois la même
      // panne, et l'écran d'attente les compterait comme du travail fait.
      if (connection.State != ConnectionState.Open)
      {
        MarkAbsent(slice, PreviewAbsenceReason.ReadFailed, previews);
        continue;
      }

      try
      {
        await SampleSliceAsync(connection, schema, table, slice, previews, cancellationToken)
          .ConfigureAwait(false);
      }
      catch (MySqlException failure)
      {
        if (MySqlFailures.IsInterrupt(cancellationToken))
        {
          throw new OperationCanceledException(cancellationToken);
        }

        // ⚠️ L'échec d'une table ne fait pas tomber le scan : chaque colonne qu'elle porte reçoit
        // une raison nommée, et le relevé reste entier. Aucune exception du pilote ne remonte, et
        // rien de son message n'est recopié.
        MarkAbsent(slice, MySqlFailures.ReasonFor(failure), previews);
      }
    }
  }

  /// <summary>
  /// Ce qu'une table qui a raté laisse derrière elle : une raison nommée sur <b>chacune</b> de ses
  /// colonnes.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Jamais une case vide.</b> Sans raison, « cette colonne ne contenait rien » et « on n'a
  /// pas regardé cette colonne » se liraient pareil à l'écran, ce qui est l'<c>Omission
  /// silencieuse</c> réintroduite par une cellule vide.
  /// <para>
  /// ⚠️ <b>Elle n'écrase jamais un aperçu déjà posé.</b> C'est la seule règle d'écriture des raisons,
  /// et elle vaut pour les deux appelants : une valeur lue vaut mieux qu'une raison, et une raison
  /// posée la première est la plus proche de la cause.
  /// </para>
  /// </remarks>
  internal static void MarkAbsent(
    IEnumerable<MySqlCataloguedColumn> columns,
    PreviewAbsenceReason reason,
    Dictionary<ColumnIdentity, ColumnPreview> previews)
  {
    foreach (var column in columns)
    {
      previews.TryAdd(column.Scanned.Identity, ColumnPreview.Absent(reason));
    }
  }

  private static async Task SampleSliceAsync(
    MySqlConnection connection,
    string schema,
    string table,
    IReadOnlyList<MySqlCataloguedColumn> slice,
    Dictionary<ColumnIdentity, ColumnPreview> previews,
    CancellationToken cancellationToken)
  {
    var readable = new Dictionary<int, List<PreviewedValue>>();
    var octets = new HashSet<int>();

    await using (var command = connection.CreateCommand())
    {
      command.CommandText = MySqlScanQueries.Sample(
        schema,
        table,
        [.. slice.Select((column, index) => (Index: index, Column: column.Scanned.RawColumn))]);

      await using var reader = await command.ExecuteReaderAsync(cancellationToken)
        .ConfigureAwait(false);

      while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
      {
        var index = reader.GetInt32(0);

        if (ReadValue(reader) is not { } value)
        {
          octets.Add(index);
          continue;
        }

        if (!readable.TryGetValue(index, out var values))
        {
          values = [];
          readable[index] = values;
        }

        values.Add(value);
      }
    }

    for (var index = 0; index < slice.Count; index++)
    {
      var column = slice[index].Scanned;

      previews[column.Identity] = readable.TryGetValue(index, out var values) && values.Count > 0
        ? ColumnPreview.Read(values)

        // ⚠️ Zéro valeur ne veut dire qu'une chose ici — la table n'a pas de ligne —, sauf si le
        // serveur a rendu des octets sous un type que la liste noire n'a pas encore nommé.
        : ColumnPreview.Absent(
          octets.Contains(index)
            ? PreviewAbsenceReason.UnsampleableType
            : PreviewAbsenceReason.NoValueReturned);
    }
  }
}
