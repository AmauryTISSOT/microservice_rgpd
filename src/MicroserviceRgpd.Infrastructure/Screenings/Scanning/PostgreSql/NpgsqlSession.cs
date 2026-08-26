using System.Globalization;
using MicroserviceRgpd.Core.Screenings;
using Npgsql;

namespace MicroserviceRgpd.Infrastructure.Screenings.Scanning.PostgreSql;

/// <summary>
/// La session vue par le pilote : elle envoie les trois requêtes embarquées sur une connexion
/// ouverte, et rend ce qu'elles disent. <b>Elle ne décide de rien</b> — c'est
/// <see cref="PostgreSqlScan"/> qui décide.
/// </summary>
/// <remarks>
/// ⚠️ <b>Ce qui rate ici remonte tel quel, et c'est voulu.</b> Une <c>PostgresException</c> attrapée
/// et retraduite à cet étage serait retraduite trois fois, une par requête, et la troisième
/// oublierait une famille. Elle traverse cette classe, et meurt un cran plus haut, où
/// <see cref="PostgreSqlScan"/> la nomme.
/// </remarks>
internal sealed class NpgsqlSession : IPostgreSqlSession
{
  private readonly NpgsqlConnection _connection;

  internal NpgsqlSession(NpgsqlConnection connection)
  {
    ArgumentNullException.ThrowIfNull(connection);

    _connection = connection;
  }

  /// <inheritdoc />
  public async Task<PostgreSqlPresence> ReadPresenceAsync(CancellationToken cancellationToken)
  {
    await using var command = new NpgsqlCommand(PostgreSqlScanQueries.DatabasePresence, _connection);
    await using var reader = await command.ExecuteReaderAsync(cancellationToken)
      .ConfigureAwait(false);

    if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
    {
      return new PostgreSqlPresence(string.Empty, 0);
    }

    return new PostgreSqlPresence(Named(reader.GetString(0)), reader.GetInt64(1));
  }

  /// <inheritdoc />
  public async Task<List<CataloguedColumn>> ReadCatalogueAsync(CancellationToken cancellationToken)
  {
    await using var command = new NpgsqlCommand(PostgreSqlScanQueries.Catalogue, _connection);
    await using var reader = await command.ExecuteReaderAsync(cancellationToken)
      .ConfigureAwait(false);

    return await PostgreSqlCatalogue.ReadAsync(reader, cancellationToken).ConfigureAwait(false);
  }

  /// <inheritdoc />
  public async Task<IReadOnlyList<IReadOnlyList<PreviewedValue>>> SampleAsync(
    string schema,
    string table,
    IReadOnlyList<string> columns,
    CancellationToken cancellationToken)
  {
    var read = columns.Select(_ => new List<PreviewedValue>()).ToList();

    await using var command = new NpgsqlCommand(
      PostgreSqlScanQueries.Sample(schema, table, columns),
      _connection);

    await using var reader = await command.ExecuteReaderAsync(cancellationToken)
      .ConfigureAwait(false);

    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
    {
      for (var index = 0; index < columns.Count; index++)
      {
        read[index].Add(ReadValue(reader, index));
      }
    }

    return read;
  }

  /// <summary>
  /// Le nom de base tel que le pivot le déclarera : rogné à la borne du domaine.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Un nom que le domaine refuserait ferait rendre un pivot que l'ingestion rejetterait en
  /// <c>MissingHeader</c></b> — un scan qui a parfaitement réussi, rendu par un refus parlant
  /// d'en-tête. PostgreSQL borne un identifiant à 63 octets et le service à cent caractères : la
  /// coupe ne mord jamais en pratique, et elle est là pour le jour où l'une des deux bornes bouge.
  /// </remarks>
  private static string Named(string database)
  {
    return database.Length > Screening.MaxDatabaseNameLength
      ? database[..Screening.MaxDatabaseNameLength]
      : database;
  }

  /// <summary>
  /// Lit la valeur coupée et sa longueur réelle, projetées côte à côte pour chaque colonne.
  /// </summary>
  private static PreviewedValue ReadValue(NpgsqlDataReader reader, int column)
  {
    var value = column * 2;
    var length = value + 1;

    if (reader.IsDBNull(value))
    {
      return PreviewedValue.NullValue;
    }

    var text = reader.GetString(value);

    if (text.Length == 0)
    {
      return PreviewedValue.EmptyText;
    }

    var fitted = SampledText.Fit(text);
    var declared = reader.IsDBNull(length)
      ? 0
      : Convert.ToInt32(reader.GetValue(length), CultureInfo.InvariantCulture);

    // ⚠️ La longueur se compare à ce que le SGBD a <b>envoyé</b>, pas à ce que le garde-fou a gardé.
    // Prendre la longueur d'après coupe ferait dire « intacte » d'une valeur d'émojis dont le
    // garde-fou vient de retirer la moitié — et « tronqué » est ce qui écarte une valeur du compte
    // d'une règle de format.
    return PreviewedValue.Of(fitted, Math.Max(declared, text.Length));
  }
}
