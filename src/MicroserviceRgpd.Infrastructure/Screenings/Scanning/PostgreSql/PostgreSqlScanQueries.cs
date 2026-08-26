using System.Globalization;
using System.Text;
using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.Infrastructure.Screenings.Scanning.PostgreSql;

/// <summary>
/// Le texte de toutes les requêtes que le scanner PostgreSQL envoie. Elles vivent ici, ensemble et
/// nulle part ailleurs, pour qu'un test puisse les <b>relire</b> : c'est la moitié filet de sécurité
/// qui ne demande aucun conteneur.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Le schéma se relève sur <c>pg_catalog</c>, jamais sur <c>information_schema</c>.</b> Ce
/// n'est pas une préférence de style. Les vues de l'<c>information_schema</c> sont filtrées
/// <b>ligne à ligne par privilège</b> : un compte sans droit sur une table ne la voit simplement
/// pas, et le relevé rendu est alors <b>silencieusement partiel</b> — un rapport sincère, entier de
/// son point de vue, amputé de la moitié des tables du client. C'est très exactement
/// l'<c>Omission silencieuse</c>, et aucune clause d'incomplétude ne la rattraperait puisque rien,
/// nulle part, ne saurait qu'il manque quelque chose. <c>pg_catalog</c>, lui, est lisible de tous :
/// ce qu'il rend est le catalogue entier. Deux des neuf champs du pivot n'existent d'ailleurs que
/// là — <c>col_description</c> et <c>obj_description</c> n'ont aucun équivalent conforme.
/// </para>
/// <para>
/// ⚠️ <b>Aucune ne porte d'étoile.</b> <c>SELECT *</c> prélèverait les colonnes que le schéma a
/// gagnées depuis le relevé — donc des colonnes que le rapport ne connaît pas, prélevées quand même.
/// Les colonnes sont nommées <b>depuis le schéma relevé</b>, et une colonne disparue entre les deux
/// fait rater sa table, ce qui est le bon échec.
/// </para>
/// <para>
/// ⚠️ <b>Aucune ne porte d'<c>ORDER BY</c>, pas même en fenêtre.</b> Le biais du premier venu est
/// <b>énoncé</b>, pas corrigé. La position du pivot, que <c>releves/postgresql.sql</c> recalcule
/// avec un <c>row_number() OVER (… ORDER BY a.attnum)</c>, est ici renumérotée <b>en C#</b> — voir
/// <see cref="PostgreSqlCatalogue"/>. Le relevé est le même à la ligne près, et le lint peut rester
/// ce qu'il doit être : une interdiction sans exception à retenir.
/// </para>
/// <para>
/// ⚠️ <b>La troncature est faite par le SGBD, avant le réseau.</b> <c>left(…, 254)</c> s'exécute
/// dans la base ; lire la valeur entière pour la couper en C# ferait traverser au service des
/// mégaoctets de données personnelles qu'il ne gardera pas — et « rien de réel ne reste » ne tient
/// pas si tout le réel est passé par la mémoire du processus. <c>length(…)</c> voyage à côté : c'est
/// ce qui permet de dire « tronqué » sans avoir vu la fin.
/// </para>
/// <para>
/// ⚠️ <b>Aucune requête de privilège, nulle part.</b> Ni <c>has_table_privilege</c>, ni
/// <c>pg_class.relacl</c>, ni <c>aclexplode</c> : le garde de privilège a été <b>retiré</b>, pas
/// assoupli — voir <c>ADR-0014</c>. Le service tente la lecture et nomme ce qui se passe ; il ne
/// demande jamais la permission d'avance.
/// </para>
/// </remarks>
internal static class PostgreSqlScanQueries
{
  /// <summary>
  /// Ce qui n'appartient pas au client : les deux schémas du système, et les familles engendrées.
  /// Écrit à l'identique de <c>releves/postgresql.sql</c> — deux filtres différents feraient de deux
  /// relevés de la même base deux relevés qui ne se comparent plus.
  /// </summary>
  private const string ApplicationSchemas = """
    n.nspname NOT IN ('pg_catalog', 'information_schema')
       AND n.nspname NOT LIKE 'pg_toast%'
    """;

  /// <summary>
  /// La seule lecture d'existence qui reste, et le nom de la base dans le même aller-retour.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Ce n'est pas un contrôle de droits.</b> Elle n'existe que pour séparer les deux fins à
  /// zéro objet : <b>zéro schéma applicatif</b> — la base est absente du catalogue de schémas, et
  /// l'<c>Operator</c> a un accès à demander — contre <b>zéro table dans des schémas qui
  /// existent</b> — la base est là, elle est vide, et il n'y a rien à faire.
  /// </para>
  /// <para>
  /// ⚠️ <b>Le nom de la base vient de <c>current_database()</c>, jamais de la chaîne de
  /// connexion.</b> Une chaîne peut taire la base — Npgsql retombe alors sur le nom de
  /// l'utilisateur —, et le <c>base</c> du pivot déclarerait un nom de compte là où le chemin collé
  /// déclare un nom de base. Les deux relevés cesseraient de se comparer sur leur en-tête, et c'est
  /// la première ligne que quelqu'un lit le jour où quelque chose cloche.
  /// </para>
  /// </remarks>
  internal const string DatabasePresence = $"""
    SELECT current_database() AS base,
           COUNT(1) AS schemas_applicatifs
      FROM pg_catalog.pg_namespace n
     WHERE {ApplicationSchemas}
    """;

  /// <summary>
  /// Le catalogue, en colonnes brutes : c'est le C# qui sérialise le pivot, pas la requête. C'est la
  /// seule différence avec <c>releves/postgresql.sql</c>, qui rend le texte pivot tout fait parce
  /// que le chemin collé n'a personne pour l'écrire.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b><c>attnum</c> sort tel quel, et il porte des trous.</b> PostgreSQL ne réutilise pas le
  /// numéro d'une colonne supprimée : après un <c>DROP COLUMN</c>, le trou est définitif. Or un trou
  /// dans les positions est l'un des neuf refus du pivot — celui qui attrape la troncature au
  /// milieu. La renumérotation se fait donc en C#, sur ce que la requête a rendu.
  /// </para>
  /// <para>
  /// ⚠️ <b>Le type sort deux fois, et chacun a son emploi.</b> <c>format_type</c> écrit ce que le
  /// pivot déclare — <c>character varying(255)</c>, <c>text[]</c> —, c'est-à-dire le texte que le
  /// chemin collé écrit déjà. Le <c>typname</c>, lui, <b>résolu à travers les domaines</b>, est ce
  /// que la liste noire consulte : un domaine posé sur <c>bytea</c> porte le nom du domaine, et une
  /// liste noire qui lirait <c>format_type</c> prélèverait son binaire sans rien voir. La résolution
  /// est faite <b>d'un cran</b> : un domaine posé sur un domaine posé sur <c>bytea</c> passerait la
  /// liste. Ce qu'il en coûte est un aperçu de binaire rendu en hexadécimal, illisible mais inoffensif
  /// — jamais une valeur qui s'échappe. Le jour où ce cas se rencontre, c'est cette jointure qui
  /// devient récursive.
  /// </para>
  /// </remarks>
  internal const string Catalogue = $"""
    SELECT n.nspname AS schema_nom,
           cl.relname AS table_nom,
           a.attname AS colonne_nom,
           a.attnum AS attnum,
           pg_catalog.format_type(a.atttypid, a.atttypmod) AS type_complet,
           COALESCE(bt.typname, t.typname) AS type_interne,
           (NOT a.attnotnull) AS nullable,
           COALESCE(pg_catalog.col_description(cl.oid, a.attnum), '') AS commentaire_colonne,
           COALESCE(pg_catalog.obj_description(cl.oid, 'pg_class'), '') AS commentaire_table,
           COALESCE((
             SELECT MIN(rc.relname)
               FROM pg_catalog.pg_constraint co
               JOIN pg_catalog.pg_class rc ON rc.oid = co.confrelid
              WHERE co.conrelid = cl.oid
                AND co.contype = 'f'
                AND a.attnum = ANY (co.conkey)
           ), '') AS table_referencee
      FROM pg_catalog.pg_attribute a
      JOIN pg_catalog.pg_class cl ON cl.oid = a.attrelid
      JOIN pg_catalog.pg_namespace n ON n.oid = cl.relnamespace
      JOIN pg_catalog.pg_type t ON t.oid = a.atttypid
      LEFT JOIN pg_catalog.pg_type bt ON bt.oid = NULLIF(t.typbasetype, 0)
     WHERE cl.relkind = 'r'
       AND a.attnum > 0
       AND NOT a.attisdropped
       AND {ApplicationSchemas}
    """;

  /// <summary>
  /// Les types dont on ne prélève <b>aucune</b> valeur, <b>nommés un par un</b>. Ce sont les noms
  /// internes de <c>pg_type</c> — ceux que la requête de catalogue rend —, et le préfixe <c>_</c>
  /// est la façon dont PostgreSQL nomme le tableau d'un type.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Une liste noire, jamais une liste blanche.</b> Une liste blanche tairait <b>par
  /// défaut</b> tout type qu'elle ne connaît pas encore : une colonne d'un type d'extension —
  /// PostGIS, <c>hstore</c>, <c>citext</c> — deviendrait « type non prélevable » sans que personne
  /// n'ait décidé qu'elle l'était. Une colonne PostGIS de coordonnées est de la donnée de
  /// localisation, que l'<c>Operator</c> doit voir pour arbitrer : elle est prélevée, pas exclue en
  /// silence.
  /// </para>
  /// <para>
  /// ⚠️ <b><c>typcategory = 'U'</c> est mort, et il faut l'écrire ici pour qu'il ne renaisse
  /// pas.</b> La catégorie « <c>U</c>ser-defined » de PostgreSQL est un fourre-tout : elle range
  /// <c>uuid</c>, <c>jsonb</c>, <c>json</c>, <c>xml</c> et les types PostGIS <b>avec</b>
  /// <c>bytea</c>. L'employer comme filtre binaire, c'est refuser en silence de prélever un
  /// identifiant de personne et un document JSON entier — précisément les deux colonnes que
  /// l'<c>Operator</c> a le plus besoin de voir.
  /// </para>
  /// <para>
  /// ⚠️ <b>La liste est courte parce que PostgreSQL n'a qu'un seul vrai type binaire, et c'est un
  /// fait, pas un oubli.</b> Les chaînes de bits — <c>bit</c>, <c>varbit</c> — se rendent en
  /// <c>'0101'</c> et se lisent ; le binaire, lui, se rendrait en <c>\x0102039fff</c>, qui ne dit
  /// rien à qui le regarde. Un type binaire de plus s'ajoute par une <b>ligne nommée</b> ici, et
  /// c'est tout ce qu'on demande.
  /// </para>
  /// </remarks>
  internal static readonly IReadOnlySet<string> UnsampleableTypes =
    new HashSet<string>(StringComparer.Ordinal)
    {
      "bytea",
      "_bytea",
    };

  /// <summary>
  /// Le plus grand nombre de colonnes qu'une requête de prélèvement se permet. Chacune y projette
  /// <b>deux</b> expressions — la valeur coupée et sa longueur réelle —, et PostgreSQL borne une
  /// liste de projection à 1664 entrées. Une table plus large est donc prélevée en plusieurs
  /// requêtes, ce qui reste « une requête par table » au sens qui compte : <b>jamais deux tables
  /// dans la même requête</b>.
  /// </summary>
  internal const int MaxColumnsPerQuery = 800;

  /// <summary>
  /// Ce type se prélève-t-il ? La question se pose sur le nom interne rendu par le catalogue,
  /// domaines résolus.
  /// </summary>
  internal static bool IsSampleable(string? internalTypeName)
  {
    return internalTypeName is not null && !UnsampleableTypes.Contains(internalTypeName);
  }

  /// <summary>
  /// Prélève une tranche de colonnes d'<b>une seule</b> table : la valeur coupée et la longueur
  /// réelle de chacune, sur les cinq premières lignes venues.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Une requête par table, et surtout pas un lot.</b> Un <c>NpgsqlBatch</c> est enveloppé
  /// dans une transaction implicite : l'échec d'une seule de ses commandes annule <b>tout</b> le
  /// lot — l'exact opposé de la tolérance attendue, où la table qui rate reçoit une raison et les
  /// autres gardent leurs valeurs. Et grouper rendrait faux le dénominateur de l'avancement, qui se
  /// compte en tables prélevées.
  /// </para>
  /// <para>
  /// ⚠️ <b>Pas de sentinelle, à la différence de SQLite.</b> Sous PostgreSQL le type est porté par
  /// la <b>colonne</b>, pas par la valeur : une colonne binaire est écartée <b>avant</b> la requête,
  /// sur son type relevé, et n'y figure jamais. Zéro ligne ne veut donc plus dire qu'une chose — la
  /// lecture n'a rien retourné —, et il n'y a rien à départager.
  /// </para>
  /// <para>
  /// ⚠️ <b>Le passage par <c>text</c> est explicite.</b> <c>left()</c> ne prend que du texte ;
  /// laisser PostgreSQL choisir la conversion ferait échouer la requête sur un <c>jsonb</c>, un
  /// tableau ou un type d'extension — donc une raison d'absence pour toute la table, à cause d'une
  /// colonne parfaitement lisible.
  /// </para>
  /// </remarks>
  internal static string Sample(string schema, string table, IReadOnlyList<string> columns)
  {
    ArgumentOutOfRangeException.ThrowIfZero(columns.Count);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(columns.Count, MaxColumnsPerQuery);

    var query = new StringBuilder("SELECT ");

    for (var index = 0; index < columns.Count; index++)
    {
      var quoted = Quote(columns[index]);
      var rank = index.ToString(CultureInfo.InvariantCulture);

      if (index > 0)
      {
        query.Append(",\n       ");
      }

      query.Append("left(CAST(")
        .Append(quoted)
        .Append(" AS text), ")
        .Append(ColumnPreview.MaxValueLength.ToString(CultureInfo.InvariantCulture))
        .Append(") AS valeur_")
        .Append(rank)
        .Append(", length(CAST(")
        .Append(quoted)
        .Append(" AS text)) AS longueur_")
        .Append(rank);
    }

    query.Append("\n  FROM ")
      .Append(Quote(schema))
      .Append('.')
      .Append(Quote(table))
      .Append("\n LIMIT ")
      .Append(ColumnPreview.MaxValues.ToString(CultureInfo.InvariantCulture));

    return query.ToString();
  }

  /// <summary>
  /// Encadre un identifiant venu du schéma d'une base tierce. Il n'y a pas de paramètre pour un nom
  /// de table : c'est le <b>seul</b> endroit du scanner où un texte du client entre dans une
  /// requête, et il en ressort toujours entre guillemets, ses guillemets doublés.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Une routine unique, et c'est ce qui la rend relisable.</b> Le prélèvement interpole des
  /// noms venus du catalogue d'une base qu'on ne contrôle pas ; une seconde façon de citer un
  /// identifiant, écrite ailleurs un jour de hâte, serait la seule qu'un relecteur ne regarderait
  /// pas. PostgreSQL n'accepte pas le caractère nul dans un identifiant : il n'y a rien d'autre à en
  /// retirer que le guillemet, qui se double.
  /// </remarks>
  internal static string Quote(string identifier)
  {
    return "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
  }
}
