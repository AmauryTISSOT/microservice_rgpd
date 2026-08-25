-- Relevé de colonnes — dialecte MariaDB / MySQL
--
-- Produit un `ColumnListing` au format pivot `screening-pivot/1` : une ligne JSON
-- par colonne, encadrée d'une ligne d'en-tête et d'une ligne de fin.
--
-- ⚠️ La requête PRODUIT le pivot, elle ne fournit pas la donnée à mettre en forme.
-- Ne reformatez rien, ne réalignez rien, n'exportez pas vers un tableur : copiez le
-- bloc de lignes tel quel. Voir docs/contexts/screening/CONTEXT.md.
--
-- ⚠️ **`nullable` s'écrit `(est_nullable = 'YES')`, jamais `IS_NULLABLE`.**
-- L'`information_schema` rend la nullabilité en `'YES'`/`'NO'` : un
-- `JSON_OBJECT('nullable', IS_NULLABLE)` naïf émet `"nullable":"YES"`, une chaîne,
-- que le pivot refuse (`UnreadableColumnLine`) — et devait refuser, un `"YES"` pris
-- pour `null` désactivant silencieusement le filtre de nullabilité sur toute la base.
-- La comparaison, elle, rend un vrai booléen JSON : attesté `true` sur MySQL 8.4 **et**
-- MariaDB 11.8. C'est aussi pourquoi la ligne de fin écrit `(1 = 1)` plutôt que `TRUE` :
-- même mécanique, un booléen produit par une comparaison. ⚠️ `CAST(… AS JSON)` n'est
-- **pas** portable — syntaxe inconnue de MariaDB (`ERROR 1064`).
--
-- ⚠️ Client en ligne de commande : utilisez `mysql -N -B -r` (--skip-column-names
-- --batch --raw). Sans `--raw`, le client rééchappe les antislashs et les JSON
-- porteurs de caractères échappés arrivent corrompus.
--
-- Exécuter en étant connecté à la base à relever : la requête lit `DATABASE()`.

WITH cols AS (
  SELECT
    c.TABLE_SCHEMA                                        AS nom_schema,
    c.TABLE_NAME                                          AS nom_table,
    c.COLUMN_NAME                                         AS nom_colonne,
    c.ORDINAL_POSITION                                    AS position,
    c.COLUMN_TYPE                                         AS type_complet,
    c.IS_NULLABLE                                         AS est_nullable,
    COALESCE(c.COLUMN_COMMENT, '')                        AS commentaire_colonne,
    COALESCE(t.TABLE_COMMENT, '')                         AS commentaire_table,
    COALESCE(fk.REFERENCED_TABLE_NAME, '')                AS table_referencee
  FROM information_schema.COLUMNS c
  JOIN information_schema.TABLES t
    ON  t.TABLE_SCHEMA = c.TABLE_SCHEMA
    AND t.TABLE_NAME   = c.TABLE_NAME
    AND t.TABLE_TYPE   = 'BASE TABLE'
  LEFT JOIN (
    -- Une colonne peut porter plusieurs clés étrangères ; on n'en rend qu'une,
    -- choisie déterministement, pour ne jamais dupliquer une ligne de colonne.
    SELECT TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME,
           MIN(REFERENCED_TABLE_NAME) AS REFERENCED_TABLE_NAME
    FROM information_schema.KEY_COLUMN_USAGE
    WHERE REFERENCED_TABLE_NAME IS NOT NULL
    GROUP BY TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME
  ) fk
    ON  fk.TABLE_SCHEMA = c.TABLE_SCHEMA
    AND fk.TABLE_NAME   = c.TABLE_NAME
    AND fk.COLUMN_NAME  = c.COLUMN_NAME
  WHERE c.TABLE_SCHEMA = DATABASE()
)
SELECT ligne FROM (
  SELECT 0 AS bloc, '' AS tri_table, 0 AS tri_position,
         JSON_OBJECT(
           'format',    'screening-pivot/1',
           'dialecte',  'mariadb',
           'base',      DATABASE(),
           'genere_le', DATE_FORMAT(UTC_TIMESTAMP(), '%Y-%m-%dT%H:%i:%sZ')
         ) AS ligne
  UNION ALL
  SELECT 1, nom_table, position,
         JSON_OBJECT(
           'schema',              nom_schema,
           'table',               nom_table,
           'colonne',             nom_colonne,
           'position',            position,
           'type',                type_complet,
           'nullable',            (est_nullable = 'YES'),
           'commentaire_colonne', commentaire_colonne,
           'commentaire_table',   commentaire_table,
           'table_referencee',    table_referencee
         )
  FROM cols
  UNION ALL
  -- Le compte sort de la MÊME lecture du catalogue que les lignes : `cols` est
  -- évaluée une fois. Un second passage pourrait diverger si un ALTER TABLE
  -- s'intercalait, et le service refuserait alors un relevé sincère.
  SELECT 2, '', 0,
         JSON_OBJECT('fin', (1 = 1), 'colonnes', (SELECT COUNT(*) FROM cols))
) pivot
ORDER BY bloc, tri_table, tri_position;
