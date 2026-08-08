-- Relevé de colonnes — dialecte PostgreSQL
--
-- Produit un `ColumnListing` au format pivot `screening-pivot/1` : une ligne JSON
-- par colonne, encadrée d'une ligne d'en-tête et d'une ligne de fin.
--
-- ⚠️ Cette requête lit `pg_catalog`, et NON `information_schema`. Ce n'est pas une
-- préférence de style : l'`information_schema` de PostgreSQL, si conforme soit-il,
-- **n'expose aucun commentaire**. `col_description()` et `obj_description()`
-- n'existent que côté `pg_catalog`. Deux des neuf champs du pivot y sont donc
-- inatteignables.
--
-- ⚠️ `position` est recalculé par `row_number()` et n'est PAS `attnum`. PostgreSQL
-- ne réutilise pas le numéro d'une colonne supprimée : après un `DROP COLUMN`,
-- `attnum` porte un trou définitif. Or un trou dans les positions est l'un des
-- neuf cas de refus du pivot — celui qui attrape la troncature au milieu. Rendre
-- `attnum` tel quel ferait refuser des relevés parfaitement sincères.
--
-- ⚠️ Client en ligne de commande : `psql -At -f postgresql.sql`.
--
-- Relève tous les schémas applicatifs de la base courante : PostgreSQL a un vrai
-- multi-schéma, et sans lui `public.user` et `audit.user` seraient la même table.

WITH cols AS (
  SELECT
    n.nspname                                     AS nom_schema,
    cl.relname                                    AS nom_table,
    a.attname                                     AS nom_colonne,
    row_number() OVER (
      PARTITION BY n.nspname, cl.relname ORDER BY a.attnum
    )                                             AS position,
    format_type(a.atttypid, a.atttypmod)          AS type_complet,
    (NOT a.attnotnull)                            AS nullable,
    COALESCE(col_description(cl.oid, a.attnum), '') AS commentaire_colonne,
    COALESCE(obj_description(cl.oid, 'pg_class'), '') AS commentaire_table,
    COALESCE((
      SELECT MIN(rc.relname)
      FROM pg_constraint co
      JOIN pg_class rc ON rc.oid = co.confrelid
      WHERE co.conrelid = cl.oid
        AND co.contype  = 'f'
        AND a.attnum    = ANY (co.conkey)
    ), '')                                        AS table_referencee
  FROM pg_attribute a
  JOIN pg_class     cl ON cl.oid = a.attrelid
  JOIN pg_namespace n  ON n.oid  = cl.relnamespace
  WHERE cl.relkind = 'r'          -- tables ordinaires seulement
    AND a.attnum   > 0            -- écarte les colonnes système (ctid, xmin…)
    AND NOT a.attisdropped
    AND n.nspname NOT IN ('pg_catalog', 'information_schema')
    AND n.nspname NOT LIKE 'pg_toast%'
)
SELECT ligne FROM (
  SELECT 0 AS bloc, '' AS tri_schema, '' AS tri_table, 0::bigint AS tri_position,
         json_build_object(
           'format',    'screening-pivot/1',
           'dialecte',  'postgresql',
           'base',      current_database(),
           'genere_le', to_char(now() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS"Z"')
         )::text AS ligne
  UNION ALL
  SELECT 1, nom_schema, nom_table, position,
         json_build_object(
           'schema',              nom_schema,
           'table',               nom_table,
           'colonne',             nom_colonne,
           'position',            position,
           'type',                type_complet,
           'nullable',            CASE WHEN nullable THEN 1 ELSE 0 END,
           'commentaire_colonne', commentaire_colonne,
           'commentaire_table',   commentaire_table,
           'table_referencee',    table_referencee
         )::text
  FROM cols
  UNION ALL
  -- Même lecture du catalogue que les lignes : `cols` n'est évaluée qu'une fois.
  SELECT 2, '', '', 0::bigint,
         json_build_object('colonnes', (SELECT COUNT(*) FROM cols))::text
) pivot
ORDER BY bloc, tri_schema, tri_table, tri_position;
