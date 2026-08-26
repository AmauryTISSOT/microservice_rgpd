using System.Globalization;
using System.Text;
using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.Infrastructure.Screenings.Scanning.Sqlite;

/// <summary>
/// Le texte de toutes les requêtes que le scanner SQLite envoie. Elles vivent ici, ensemble et
/// nulle part ailleurs, pour qu'un test puisse les <b>relire</b> : c'est la moitié filet de sécurité
/// qui ne demande aucun conteneur.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Aucune ne porte d'étoile.</b> <c>SELECT *</c> ramènerait les colonnes que le schéma a
/// gagnées depuis le relevé — donc des colonnes que le rapport ne connaît pas, prélevées quand
/// même. Les colonnes sont nommées <b>depuis le schéma relevé</b>, et une colonne disparue entre
/// les deux fait rater sa table, ce qui est le bon échec.
/// </para>
/// <para>
/// ⚠️ <b>Aucune ne porte d'<c>ORDER BY</c>.</b> Trier cinq valeurs sur une table de dix millions de
/// lignes coûte un tri complet ou un index, sur la base de production d'un client, pour un aperçu.
/// Le biais du premier venu est donc <b>énoncé</b>, pas corrigé : l'aperçu montre ce qui vient en
/// premier, et l'écran le dit.
/// </para>
/// <para>
/// ⚠️ <b>La troncature est faite par le SGBD, avant le réseau.</b> <c>substr(col, 1, 254)</c>
/// s'exécute dans la base ; lire la valeur entière pour la couper en C# ferait traverser au service
/// des mégaoctets de données personnelles qu'il ne gardera pas — et « rien de réel ne reste » ne
/// tient pas si tout le réel est passé par la mémoire du processus. <c>length(col)</c> voyage à
/// côté : c'est ce qui permet de dire « tronqué » sans avoir vu la fin.
/// </para>
/// <para>
/// ⚠️ <b>Le binaire est exclu par liste noire nommée, jamais par liste blanche.</b> Sous SQLite le
/// typage est dynamique : le filtre porte sur la <b>valeur</b>, <c>typeof(col) &lt;&gt; 'blob'</c>,
/// et non sur le type déclaré de la colonne — une colonne <c>TEXT</c> peut porter un blob ligne à
/// ligne. Une liste blanche ferait taire, par défaut, tout type qu'elle ne connaît pas encore.
/// </para>
/// </remarks>
internal static class SqliteScanQueries
{
  /// <summary>
  /// La seule lecture d'existence qui reste : le catalogue de schémas connaît-il cette base ? Ce
  /// n'est <b>pas</b> un contrôle de droits, et elle n'existe que pour distinguer les deux fins à
  /// zéro objet.
  /// </summary>
  internal const string DatabasePresence =
    "SELECT COUNT(1) AS presente FROM pragma_database_list WHERE name = 'main'";

  /// <summary>
  /// Le catalogue, en colonnes brutes : c'est le C# qui sérialise le pivot, pas la requête. C'est la
  /// seule différence avec <c>releves/sqlite.sql</c>, qui rend le texte pivot tout fait parce que le
  /// chemin collé n'a personne pour l'écrire.
  /// </summary>
  internal const string Catalogue = """
    SELECT 'main' AS schema_nom,
           m.name AS table_nom,
           ti.name AS colonne_nom,
           ti.cid + 1 AS position,
           ti.type AS type_nom,
           CASE WHEN ti."notnull" = 0 THEN 1 ELSE 0 END AS nullable,
           (SELECT MIN(fk."table")
              FROM pragma_foreign_key_list(m.name) fk
             WHERE fk."from" = ti.name) AS table_referencee
      FROM sqlite_master m
      JOIN pragma_table_info(m.name) ti
     WHERE m.type = 'table'
       AND m.name NOT LIKE 'sqlite_%'
    """;

  /// <summary>
  /// Le plus grand nombre de branches qu'une requête de prélèvement se permet. SQLite refuse au-delà
  /// de <c>SQLITE_MAX_COMPOUND_SELECT</c> (cinq cents par défaut) ; une table plus large est donc
  /// prélevée en plusieurs requêtes — ce qui reste « une requête par table » au sens qui compte :
  /// <b>jamais deux tables dans la même requête</b>.
  /// </summary>
  internal const int MaxBranchesPerQuery = 400;

  /// <summary>
  /// La branche sentinelle, projetée sous l'indice <see cref="SentinelIndex"/> : elle ne rend une
  /// ligne que si la table en porte au moins une.
  /// </summary>
  internal const int SentinelIndex = -1;

  /// <summary>
  /// Prélève une tranche de colonnes d'<b>une seule</b> table : une branche par colonne, chacune
  /// avec son propre <c>LIMIT 5</c>, plus la branche sentinelle.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>La sentinelle existe parce que zéro ligne veut dire deux choses.</b> Une colonne qui ne
  /// rend aucune valeur, c'est soit une table vide — « aucune valeur retournée » —, soit une colonne
  /// dont tout est binaire — « type non prélevable ». Sans une branche qui dit « la table a des
  /// lignes », le service devrait deviner, et il choisirait le mauvais mot la moitié du temps.
  /// </para>
  /// <para>
  /// ⚠️ <b>L'indice de colonne est projeté, pas déduit de l'ordre des lignes.</b> Un
  /// <c>UNION ALL</c> ne promet aucun ordre ; sans l'indice, cinq valeurs pourraient se ranger sous
  /// la mauvaise colonne — et un aperçu rangé sous la mauvaise colonne est pire qu'un aperçu absent.
  /// </para>
  /// </remarks>
  internal static string Sample(
    string schema,
    string table,
    IReadOnlyList<(int Index, string Column)> columns)
  {
    ArgumentOutOfRangeException.ThrowIfZero(columns.Count);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(columns.Count, MaxBranchesPerQuery);

    var qualified = Quote(schema) + "." + Quote(table);
    var query = new StringBuilder();

    query.Append("SELECT ")
      .Append(SentinelIndex.ToString(CultureInfo.InvariantCulture))
      .Append(" AS colonne, valeur, longueur FROM (SELECT NULL AS valeur, NULL AS longueur FROM ")
      .Append(qualified)
      .Append(" LIMIT 1)");

    foreach (var (index, column) in columns)
    {
      var quoted = Quote(column);

      query.Append("\nUNION ALL\nSELECT ")
        .Append(index.ToString(CultureInfo.InvariantCulture))
        .Append(" AS colonne, valeur, longueur FROM (SELECT substr(")
        .Append(quoted)
        .Append(", 1, ")
        .Append(ColumnPreview.MaxValueLength.ToString(CultureInfo.InvariantCulture))
        .Append(") AS valeur, length(")
        .Append(quoted)
        .Append(") AS longueur FROM ")
        .Append(qualified)
        .Append(" WHERE typeof(")
        .Append(quoted)
        .Append(") <> 'blob' LIMIT ")
        .Append(ColumnPreview.MaxValues.ToString(CultureInfo.InvariantCulture))
        .Append(')');
    }

    return query.ToString();
  }

  /// <summary>
  /// Encadre un identifiant venu du schéma du client. Il n'y a pas de paramètre pour un nom de
  /// table : c'est le seul endroit du scanner où un texte du client entre dans une requête, et il en
  /// ressort toujours entre guillemets, ses guillemets doublés.
  /// </summary>
  internal static string Quote(string identifier)
  {
    return "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
  }
}
