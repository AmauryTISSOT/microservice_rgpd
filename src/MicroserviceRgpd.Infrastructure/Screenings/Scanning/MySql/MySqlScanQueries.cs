using System.Globalization;
using System.Text;
using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.Infrastructure.Screenings.Scanning.MySql;

/// <summary>
/// Le texte de toutes les requêtes que le scanner MariaDB/MySQL envoie. Elles vivent ici, ensemble
/// et nulle part ailleurs, pour qu'un test puisse les <b>relire</b> : c'est la moitié filet de
/// sécurité qui ne demande aucun conteneur.
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
/// Le biais du premier venu est donc <b>énoncé</b>, pas corrigé. L'ordre du pivot, lui, est remis
/// en C# après coup : il ne coûte rien à la base du client.
/// </para>
/// <para>
/// ⚠️ <b>Aucune ne demande de privilège.</b> Pas de <c>SHOW GRANTS</c>, pas de lecture de
/// <c>mysql.user</c>, nulle part — le garde de privilège de
/// <see href="https://github.com/AmauryTISSOT/microservice_rgpd/issues/286">#286</see> est
/// <b>retiré, pas assoupli</b> : sur MariaDB, <c>SHOW GRANTS</c> rend la ligne
/// <c>IDENTIFIED BY PASSWORD '&lt;condensat&gt;'</c>, et un condensat de mot de passe qui traverse
/// le service est une donnée que <c>Rien de réel ne reste</c> n'a aucune raison de laisser entrer.
/// Ce que le compte ne voit pas se lit sans le demander : il manque du catalogue.
/// </para>
/// <para>
/// ⚠️ <b>La troncature est faite par le SGBD, avant le réseau.</b> <c>LEFT(col, 254)</c> s'exécute
/// dans la base ; lire la valeur entière pour la couper en C# ferait traverser au service des
/// mégaoctets de données personnelles qu'il ne gardera pas. <c>CHAR_LENGTH(col)</c> voyage à
/// côté : c'est ce qui permet de dire « tronqué » sans avoir vu la fin. ⚠️ <c>CHAR_LENGTH</c> et
/// non <c>LENGTH</c> : <c>LENGTH</c> compte des <b>octets</b>, et une adresse accentuée en UTF-8
/// serait annoncée tronquée alors qu'elle est entière.
/// </para>
/// <para>
/// ⚠️ <b>Le binaire est exclu par liste noire nommée, jamais par liste blanche</b> — voir
/// <see cref="UnsampleableTypes"/>. Une liste blanche ferait taire, par défaut, tout type qu'elle
/// ne connaît pas encore : la colonne <c>inet6</c> qu'une version de MariaDB ajoutera serait rendue
/// « non prélevable » sans que personne ne l'ait décidé.
/// </para>
/// </remarks>
internal static class MySqlScanQueries
{
  /// <summary>
  /// Le seul paramètre des requêtes fixes : le nom de la base à relever. ⚠️ <b>C'est un paramètre,
  /// pas une interpolation.</b> Le nom vient de la chaîne que l'<c>Operator</c> a saisie, et il n'y
  /// a aucune raison de le faire entrer dans le texte quand le protocole sait le porter à part.
  /// </summary>
  internal const string DatabaseParameter = "@base";

  /// <summary>
  /// La seule lecture d'existence qui reste : le catalogue de schémas connaît-il cette base ? Ce
  /// n'est <b>pas</b> un contrôle de droits, et elle n'existe que pour distinguer les deux fins à
  /// zéro objet.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Sous MySQL, <c>information_schema.SCHEMATA</c> ne montre que ce sur quoi le compte a un
  /// privilège.</b> « Absente du catalogue » couvre donc les deux cas d'un seul mot — la base
  /// n'existe pas, ou le compte ne la voit pas —, et c'est exactement ce que l'écran doit dire :
  /// l'<c>Operator</c> va demander un accès, et il n'a pas à savoir lequel des deux l'a amené là.
  /// Le distinguer demanderait précisément la requête de privilège que #286 interdit.
  /// </remarks>
  internal const string DatabasePresence = """
    SELECT COUNT(1) AS presente
      FROM information_schema.SCHEMATA
     WHERE SCHEMA_NAME = @base
    """;

  /// <summary>
  /// Le catalogue, en une seule requête et en colonnes brutes : c'est le C# qui sérialise le pivot,
  /// pas la requête. C'est la seule différence avec <c>releves/mariadb.sql</c>, qui rend le texte
  /// pivot tout fait parce que le chemin collé n'a personne pour l'écrire.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Le <c>type</c> du pivot est <c>COLUMN_TYPE</c>, jamais <c>DATA_TYPE</c>.</b>
  /// <c>DATA_TYPE</c> rend <c>varchar</c> là où <c>COLUMN_TYPE</c> rend <c>varchar(255)</c>, et
  /// c'est le second que <c>releves/mariadb.sql</c> écrit déjà : un relevé connecté et un relevé
  /// collé de la même base doivent se ressembler à l'octet. <c>DATA_TYPE</c> voyage tout de même,
  /// à côté, parce que c'est <b>lui</b> que la liste noire lit.
  /// </para>
  /// <para>
  /// ⚠️ <b>Une colonne peut porter plusieurs clés étrangères, et le pivot n'en veut qu'une.</b> La
  /// jointure sur <c>KEY_COLUMN_USAGE</c> passe donc par un <c>MIN</c> groupé : sans lui, une
  /// colonne à deux contraintes rendrait deux lignes, et l'ingestion refuserait le relevé entier
  /// en <c>DuplicateColumn</c>.
  /// </para>
  /// </remarks>
  internal const string Catalogue = """
    SELECT c.TABLE_SCHEMA                          AS schema_nom,
           c.TABLE_NAME                            AS table_nom,
           c.COLUMN_NAME                           AS colonne_nom,
           c.ORDINAL_POSITION                      AS position,
           c.COLUMN_TYPE                           AS type_complet,
           c.DATA_TYPE                             AS type_famille,
           c.IS_NULLABLE                           AS est_nullable,
           COALESCE(c.COLUMN_COMMENT, '')          AS commentaire_colonne,
           COALESCE(t.TABLE_COMMENT, '')           AS commentaire_table,
           COALESCE(fk.REFERENCED_TABLE_NAME, '')  AS table_referencee
      FROM information_schema.COLUMNS c
      JOIN information_schema.TABLES t
        ON  t.TABLE_SCHEMA = c.TABLE_SCHEMA
        AND t.TABLE_NAME   = c.TABLE_NAME
        AND t.TABLE_TYPE   = 'BASE TABLE'
      LEFT JOIN (
        SELECT TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME,
               MIN(REFERENCED_TABLE_NAME) AS REFERENCED_TABLE_NAME
          FROM information_schema.KEY_COLUMN_USAGE
         WHERE REFERENCED_TABLE_NAME IS NOT NULL
           AND TABLE_SCHEMA = @base
         GROUP BY TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME
      ) fk
        ON  fk.TABLE_SCHEMA = c.TABLE_SCHEMA
        AND fk.TABLE_NAME   = c.TABLE_NAME
        AND fk.COLUMN_NAME  = c.COLUMN_NAME
     WHERE c.TABLE_SCHEMA = @base
    """;

  /// <summary>
  /// Les types dont le service ne prélève <b>aucune</b> valeur, nommés un par un. C'est une liste
  /// <b>noire</b> : ce qui n'y figure pas est prélevé, et un type inconnu du service l'est donc
  /// aussi.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Elle ne dépend pas du nom que le moteur donne au conteneur libre.</b> Une colonne
  /// <c>JSON</c> a pour <c>DATA_TYPE</c> <c>json</c> sur MySQL et <c>longtext</c> sur MariaDB —
  /// MariaDB n'implémente <c>JSON</c> que comme un alias de <c>LONGTEXT</c> assorti d'une contrainte
  /// de contrôle. Ni l'un ni l'autre n'est ici : le conteneur libre est prélevé des deux côtés, et
  /// c'est le moteur de détection, plus haut, qui le signale. Le faire dépendre du nom du type
  /// donnerait deux relevés différents pour la même base selon le serveur qui la sert.
  /// </para>
  /// <para>
  /// ⚠️ <b>Rien d'autre que des octets n'y figure.</b> Les familles listées ont toutes en commun
  /// que le serveur les rend en <b>binaire</b> — les <c>BLOB</c> et les chaînes d'octets
  /// littéralement, <c>BIT</c> comme un paquet de bits, les types spatiaux en WKB. Cinq valeurs
  /// binaires ne diraient rien à qui les regarde, et les lire comme du texte rendrait des octets
  /// mutilés par le décodage. Un type qui n'est pas dans ce cas n'a rien à faire ici : ajouter une
  /// ligne, c'est décider qu'une colonne du client ne sera <b>jamais</b> regardée.
  /// </para>
  /// </remarks>
  internal static readonly IReadOnlySet<string> UnsampleableTypes =
    new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
      "binary",
      "varbinary",
      "tinyblob",
      "blob",
      "mediumblob",
      "longblob",
      "bit",
      "geometry",
      "point",
      "linestring",
      "polygon",
      "multipoint",
      "multilinestring",
      "multipolygon",
      "geometrycollection",
    };

  /// <summary>
  /// Le plus grand nombre de branches qu'une requête de prélèvement se permet. Une table plus large
  /// est prélevée en plusieurs requêtes — ce qui reste « une requête par table » au sens qui
  /// compte : <b>jamais deux tables dans la même requête</b>.
  /// </summary>
  /// <remarks>
  /// ⚠️ MySQL accepte 4096 colonnes par table ; une requête à 4096 branches d'union tiendrait
  /// péniblement dans <c>max_allowed_packet</c> et coûterait un plan démesuré. La borne coupe bien
  /// avant, et l'annulation reprend la main entre deux tranches.
  /// </remarks>
  internal const int MaxBranchesPerQuery = 100;

  /// <summary>
  /// Prélève une tranche de colonnes d'<b>une seule</b> table : une branche par colonne, chacune
  /// avec son propre <c>LIMIT 5</c>.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Il n'y a pas de branche sentinelle, là où SQLite en porte une.</b> Sous SQLite, le type
  /// est porté par la <b>valeur</b> : une colonne peut ne rien rendre parce que tout ce qu'elle
  /// porte est binaire, et il faut une branche qui dise « la table a des lignes » pour ne pas
  /// confondre cela avec une table vide. Ici, le type est <b>déclaré</b> : une colonne binaire est
  /// écartée avant la requête, sur son <c>DATA_TYPE</c>, et toute colonne qui reste rend autant de
  /// valeurs que la table a de lignes — <c>NULL</c> compris. Zéro valeur ne veut donc plus dire
  /// qu'une seule chose, et une branche pour la dire serait une branche pour rien.
  /// </para>
  /// <para>
  /// ⚠️ <b>L'indice de colonne est projeté, pas déduit de l'ordre des lignes.</b> Un
  /// <c>UNION ALL</c> ne promet aucun ordre ; sans l'indice, cinq valeurs pourraient se ranger sous
  /// la mauvaise colonne — et un aperçu rangé sous la mauvaise colonne est pire qu'un aperçu absent.
  /// </para>
  /// <para>
  /// ⚠️ <b>Chaque branche est une table dérivée, et elle porte un alias.</b> MySQL refuse une table
  /// dérivée anonyme (<c>ERROR 1248</c>), et c'est aussi ce qui permet à chaque colonne d'avoir son
  /// <b>propre</b> <c>LIMIT 5</c> : un <c>LIMIT</c> posé sur l'union entière rendrait cinq lignes
  /// pour la table, pas cinq par colonne.
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

    foreach (var (index, column) in columns)
    {
      var quoted = Quote(column);

      if (query.Length > 0)
      {
        query.Append("\nUNION ALL\n");
      }

      query.Append("SELECT ")
        .Append(index.ToString(CultureInfo.InvariantCulture))
        .Append(" AS colonne, valeur, longueur FROM (SELECT LEFT(")
        .Append(quoted)
        .Append(", ")
        .Append(ColumnPreview.MaxValueLength.ToString(CultureInfo.InvariantCulture))
        .Append(") AS valeur, CHAR_LENGTH(")
        .Append(quoted)
        .Append(") AS longueur FROM ")
        .Append(qualified)
        .Append(" LIMIT ")
        .Append(ColumnPreview.MaxValues.ToString(CultureInfo.InvariantCulture))
        .Append(") c")
        .Append(index.ToString(CultureInfo.InvariantCulture));
    }

    return query.ToString();
  }

  /// <summary>
  /// Encadre un identifiant venu du catalogue d'une base tierce. Il n'y a pas de paramètre pour un
  /// nom de table : c'est le seul endroit du scanner où un texte du client entre dans une requête,
  /// et il en ressort toujours entre accents graves, ses accents graves doublés.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>C'est un point de revue nommé, et voici pourquoi il l'est.</b> <c>MySqlConnector</c>
  /// active <c>CLIENT_MULTI_STATEMENTS</c> <b>inconditionnellement</b> — aucune option de la chaîne
  /// ne le coupe. Un identifiant mal encadré ne rendrait donc pas une erreur de syntaxe : il
  /// ouvrirait une <b>seconde instruction</b> sur la base du client, avec les droits du compte de
  /// connexion. Le doublement de l'accent grave est ce qui tient cette porte fermée, et c'est
  /// pourquoi il vit dans une routine <b>unique</b> : deux endroits qui encadrent, c'est un endroit
  /// qui oubliera.
  /// </para>
  /// <para>
  /// ⚠️ <b>L'accent grave reste l'accent grave, quel que soit le <c>sql_mode</c>.</b> Sous
  /// <c>ANSI_QUOTES</c>, le guillemet droit devient un délimiteur d'identifiant et cesse d'ouvrir
  /// une chaîne : une routine qui aurait choisi le guillemet marcherait sur un serveur et pas sur
  /// l'autre. L'accent grave, lui, encadre un identifiant dans les deux modes.
  /// </para>
  /// </remarks>
  internal static string Quote(string identifier)
  {
    return "`" + identifier.Replace("`", "``", StringComparison.Ordinal) + "`";
  }

  /// <summary>
  /// Cette colonne se prélève-t-elle ? La question se pose sur <c>DATA_TYPE</c> — la famille — et
  /// jamais sur <c>COLUMN_TYPE</c>, qui porte la longueur et la signature.
  /// </summary>
  internal static bool IsSampleable(string? dataType)
  {
    return !UnsampleableTypes.Contains(dataType?.Trim() ?? string.Empty);
  }
}
