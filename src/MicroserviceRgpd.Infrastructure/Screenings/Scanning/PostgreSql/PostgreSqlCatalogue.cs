using System.Data.Common;
using System.Globalization;
using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.Infrastructure.Screenings.Scanning.PostgreSql;

/// <summary>
/// Une colonne telle que <c>pg_catalog</c> vient de la rendre : ce qui partira dans le pivot, et le
/// seul fait qui n'y part pas — son type se prélève-t-il.
/// </summary>
/// <remarks>
/// ⚠️ <b>La décision est prise ici, sur le catalogue, et non au bord de la requête.</b> Sous
/// PostgreSQL le type est porté par la colonne : savoir qu'une colonne est binaire <b>avant</b> de
/// l'interroger, c'est ne jamais l'interroger — et c'est ce qui permet à la requête de prélèvement
/// de n'avoir aucun filtre, donc aucune sentinelle à démêler.
/// </remarks>
internal sealed record CataloguedColumn(ScannedColumn Scanned, bool IsSampleable);

/// <summary>
/// Lit le catalogue rendu par <see cref="PostgreSqlScanQueries.Catalogue"/> et le range en colonnes
/// du pivot, positions renumérotées.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>La renumérotation est la raison d'être de ce fichier.</b> <c>attnum</c> porte un trou
/// définitif à chaque <c>DROP COLUMN</c> — PostgreSQL ne réutilise jamais le numéro d'une colonne
/// supprimée. Or un trou dans les positions est l'un des neuf refus du pivot, celui qui attrape la
/// troncature au milieu : rendre <c>attnum</c> tel quel ferait refuser des relevés parfaitement
/// sincères, et l'<c>Operator</c> lirait « relevé tronqué » d'une base qui a simplement vécu.
/// </para>
/// <para>
/// ⚠️ <b>Elle se fait en C#, et <c>releves/postgresql.sql</c> la fait en SQL — le résultat est le
/// même.</b> La requête collée s'en remet à <c>row_number() OVER (… ORDER BY a.attnum)</c> parce
/// qu'elle n'a personne pour compter à sa place ; ici, compter en C# est ce qui laisse le lint
/// interdire <c>ORDER BY</c> <b>sans exception à retenir</b> — et une interdiction à exception est
/// une interdiction qu'on finit par ne plus lire.
/// </para>
/// <para>
/// ⚠️ <b>Il prend un <see cref="DbDataReader"/>, pas un lecteur du pilote.</b> C'est ce qui permet
/// de rejouer la fixture PostgreSQL authentique <b>sans conteneur</b> : le relevé mesuré une fois
/// hors du dépôt repasse par ce code, puis par l'ingestion, et se compare à la capture. Le pilote
/// n'apporte rien de plus à ce niveau qu'un tableau de lignes.
/// </para>
/// </remarks>
internal static class PostgreSqlCatalogue
{
  private const int Schema = 0;
  private const int Table = 1;
  private const int Column = 2;
  private const int AttributeNumber = 3;
  private const int FullType = 4;
  private const int InternalType = 5;
  private const int Nullable = 6;
  private const int ColumnComment = 7;
  private const int TableComment = 8;
  private const int ReferencedTable = 9;

  internal static async Task<List<CataloguedColumn>> ReadAsync(
    DbDataReader reader,
    CancellationToken cancellationToken)
  {
    var rows = new List<CataloguedRow>();

    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
    {
      rows.Add(
        new CataloguedRow(
          reader.GetString(Schema),
          reader.GetString(Table),
          reader.GetString(Column),
          Convert.ToInt32(reader.GetValue(AttributeNumber), CultureInfo.InvariantCulture),
          reader.IsDBNull(FullType) ? null : reader.GetString(FullType),
          reader.IsDBNull(InternalType) ? null : reader.GetString(InternalType),
          reader.GetBoolean(Nullable),
          reader.GetString(ColumnComment),
          reader.GetString(TableComment),
          reader.GetString(ReferencedTable)));
    }

    return Renumber(rows);
  }

  /// <summary>
  /// Range les lignes par table et leur donne une position <b>continue</b>, dans l'ordre où
  /// PostgreSQL a créé les colonnes.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>L'ordre est ordinal, et il l'est aussi dans la requête collée.</b> Le tri d'une culture
  /// rangerait <c>audit</c> et <c>Audit</c> l'un à côté de l'autre selon la machine qui exécute le
  /// scan, et deux relevés de la même base ne se compareraient plus ligne à ligne.
  /// </remarks>
  private static List<CataloguedColumn> Renumber(List<CataloguedRow> rows)
  {
    return
    [
      .. rows
        .GroupBy(row => (row.Schema, row.Table))
        .OrderBy(table => table.Key.Schema, StringComparer.Ordinal)
        .ThenBy(table => table.Key.Table, StringComparer.Ordinal)
        .SelectMany(table => table
          .OrderBy(row => row.AttributeNumber)
          .Select((row, rank) => Of(row, rank + 1))),
    ];
  }

  private static CataloguedColumn Of(CataloguedRow row, int position)
  {
    var scanned = new ScannedColumn(
      ColumnIdentity.Of(row.Schema, row.Table, row.Column),
      position,
      row.FullType,
      row.Nullable,
      row.ReferencedTable)
    {
      ColumnComment = row.ColumnComment,
      TableComment = row.TableComment,

      // ⚠️ Les noms qui repartiront dans une requête sont ceux du catalogue, jamais ceux que le
      // domaine a normalisés — voir ScannedColumn.RawSchema.
      RawSchema = row.Schema,
      RawTable = row.Table,
      RawColumn = row.Column,
    };

    return new CataloguedColumn(
      scanned,
      PostgreSqlScanQueries.IsSampleable(row.InternalType));
  }

  private sealed record CataloguedRow(
    string Schema,
    string Table,
    string Column,
    int AttributeNumber,
    string? FullType,
    string? InternalType,
    bool Nullable,
    string ColumnComment,
    string TableComment,
    string ReferencedTable);
}
