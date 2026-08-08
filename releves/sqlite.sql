-- Relevé de colonnes — dialecte SQLite
--
-- Produit un `ColumnListing` au format pivot `screening-pivot/1` : une ligne JSON
-- par colonne, encadrée d'une ligne d'en-tête et d'une ligne de fin.
--
-- ⚠️ SQLite ne rend AUCUN commentaire — il n'a ni `information_schema` ni clause
-- `COMMENT`. `commentaire_colonne` et `commentaire_table` sortent donc toujours
-- vides, et c'est structurel, pas accidentel. C'est très exactement pourquoi le
-- pivot déclare son dialecte : sans lui, « cette colonne n'a pas de commentaire »
-- et « ce SGBD n'en rend jamais » se liraient pareil.
--
-- ⚠️ Client en ligne de commande : `sqlite3 -noheader -list base.sqlite < sqlite.sql`.
--
-- Requiert SQLite ≥ 3.38 pour `json_object()`, et ≥ 3.16 pour les pragmas
-- utilisés comme fonctions de table.

WITH cols AS (
  SELECT
    'main'                                   AS nom_schema,
    m.name                                   AS nom_table,
    ti.name                                  AS nom_colonne,
    ti.cid + 1                               AS position,
    ti.type                                  AS type_complet,
    CASE WHEN ti."notnull" = 0 THEN 1 ELSE 0 END AS nullable,
    ''                                       AS commentaire_colonne,
    ''                                       AS commentaire_table,
    COALESCE((
      SELECT MIN(fk."table")
      FROM pragma_foreign_key_list(m.name) fk
      WHERE fk."from" = ti.name
    ), '')                                   AS table_referencee
  FROM sqlite_master m
  JOIN pragma_table_info(m.name) ti
  WHERE m.type = 'table'
    AND m.name NOT LIKE 'sqlite_%'
)
SELECT ligne FROM (
  SELECT 0 AS bloc, '' AS tri_table, 0 AS tri_position,
         json_object(
           'format',    'screening-pivot/1',
           'dialecte',  'sqlite',
           'base',      COALESCE((SELECT file FROM pragma_database_list WHERE name = 'main'), ''),
           'genere_le', strftime('%Y-%m-%dT%H:%M:%SZ', 'now')
         ) AS ligne
  UNION ALL
  SELECT 1, nom_table, position,
         json_object(
           'schema',              nom_schema,
           'table',               nom_table,
           'colonne',             nom_colonne,
           'position',            position,
           'type',                type_complet,
           'nullable',            nullable,
           'commentaire_colonne', commentaire_colonne,
           'commentaire_table',   commentaire_table,
           'table_referencee',    table_referencee
         )
  FROM cols
  UNION ALL
  -- Même lecture du catalogue que les lignes : `cols` n'est évaluée qu'une fois.
  SELECT 2, '', 0,
         json_object('colonnes', (SELECT COUNT(*) FROM cols))
)
ORDER BY bloc, tri_table, tri_position;
