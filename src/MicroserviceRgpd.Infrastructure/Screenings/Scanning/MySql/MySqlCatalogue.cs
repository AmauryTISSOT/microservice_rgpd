using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.Infrastructure.Screenings.Scanning.MySql;

/// <summary>
/// Une colonne relevée sur MariaDB/MySQL : ce que le pivot en écrira, et la <b>famille</b> de son
/// type, que seul le prélèvement lit.
/// </summary>
/// <remarks>
/// ⚠️ <b>La famille ne rejoint pas <see cref="ScannedColumn"/>, et c'est voulu.</b>
/// <see cref="ScannedColumn"/> est le vocabulaire que les trois dialectes se partagent : y poser un
/// champ dont un seul se sert ferait porter à PostgreSQL et à SQLite une notion qui ne veut rien
/// dire chez eux. La famille vit donc à côté, le temps du scan, et meurt avec lui.
/// </remarks>
internal sealed record MySqlCataloguedColumn(ScannedColumn Scanned, string DataType);

/// <summary>
/// La lecture du catalogue MariaDB/MySQL : une ligne de <c>information_schema</c> devient une
/// <see cref="ScannedColumn"/>, et l'ordre du pivot se remet ici.
/// </summary>
/// <remarks>
/// ⚠️ <b>Ce qui se traduit ici est ce que <c>releves/mariadb.sql</c> traduit déjà en SQL.</b> Les
/// deux chemins doivent rendre le <b>même</b> pivot pour la même base : c'est ce que la fixture
/// authentique rejouée contre l'ingestion éprouve, et c'est pourquoi cette traduction vit dans une
/// fonction qu'un test peut appeler sans serveur.
/// </remarks>
internal static class MySqlCatalogue
{
  /// <summary>
  /// Ce que l'<c>information_schema</c> écrit pour « cette colonne accepte <c>NULL</c> ».
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>La nullabilité arrive en texte, et elle doit repartir en booléen.</b>
  /// <c>IS_NULLABLE</c> rend <c>'YES'</c>/<c>'NO'</c> ; un pivot qui écrirait <c>"nullable":"YES"</c>
  /// serait refusé ligne à ligne (<c>UnreadableColumnLine</c>) — et devait l'être, un <c>"YES"</c>
  /// pris pour <c>null</c> désactivant silencieusement le filtre de nullabilité sur toute la base.
  /// C'est le défaut n° 2 de #284, ici du côté connecté.
  /// </remarks>
  internal const string Nullable = "YES";

  /// <summary>Une ligne du catalogue, telle que le pilote l'a rendue, devient une colonne relevée.</summary>
  /// <remarks>
  /// ⚠️ <b>Les commentaires absents s'écrivent <c>""</c>, jamais <c>null</c>.</b>
  /// <c>releves/mariadb.sql</c> les <c>COALESCE</c> déjà à la chaîne vide : rendre <c>null</c> ici
  /// ferait deux pivots différents pour la même base selon le chemin emprunté, et « un seul format
  /// pivot » ne survit pas à deux écritures du même champ vide.
  /// </remarks>
  internal static MySqlCataloguedColumn ToColumn(
    string schema,
    string table,
    string column,
    int position,
    string columnType,
    string dataType,
    string isNullable,
    string columnComment,
    string tableComment,
    string referencedTable)
  {
    return new MySqlCataloguedColumn(
      new ScannedColumn(
        ColumnIdentity.Of(schema, table, column),
        position,
        string.IsNullOrWhiteSpace(columnType) ? null : columnType,
        string.Equals(isNullable, Nullable, StringComparison.OrdinalIgnoreCase),
        referencedTable)
      {
        ColumnComment = columnComment,
        TableComment = tableComment,
        RawSchema = schema,
        RawTable = table,
        RawColumn = column,
      },
      dataType);
  }

  /// <summary>
  /// L'ordre dans lequel le pivot écrit ses lignes : par table, puis par position.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Le tri est fait ici et non par la base.</b> <c>information_schema</c> ne promet aucun
  /// ordre, et un <c>ORDER BY</c> le lui demanderait sur le serveur du client — ce que le lint
  /// interdit précisément pour ne rien lui coûter. Trier quelques centaines de lignes déjà lues ne
  /// coûte rien à personne, et c'est ce qui permet de poser côte à côte un relevé connecté et un
  /// relevé collé de la même base.
  /// </remarks>
  internal static List<ScannedColumn> InPivotOrder(IEnumerable<MySqlCataloguedColumn> columns)
  {
    return
    [
      .. columns
        .OrderBy(column => column.Scanned.RawTable, StringComparer.Ordinal)
        .ThenBy(column => column.Scanned.Position)
        .Select(column => column.Scanned),
    ];
  }
}
