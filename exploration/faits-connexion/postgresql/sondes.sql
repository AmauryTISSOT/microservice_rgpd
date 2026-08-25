-- Sondes secondaires PostgreSQL. À jouer en superutilisateur.

\echo '--- attnum porte-t-il un trou après DROP COLUMN, et row_number() le rebouche-t-il ?'
SELECT a.attnum,
       row_number() OVER (ORDER BY a.attnum) AS position_recalculee,
       a.attname
FROM pg_attribute a
JOIN pg_class cl ON cl.oid = a.attrelid
JOIN pg_namespace n ON n.oid = cl.relnamespace
WHERE n.nspname = 'public' AND cl.relname = 'adherents'
  AND a.attnum > 0 AND NOT a.attisdropped
ORDER BY a.attnum;

\echo '--- ORDINAL_POSITION d information_schema est-il, lui, contigu après le DROP ?'
SELECT ordinal_position, column_name
FROM information_schema.columns
WHERE table_schema = 'public' AND table_name = 'adherents'
ORDER BY ordinal_position;

\echo '--- typcategory par type concret (U = user-defined/binaire au sens de la doc)'
SELECT a.attname,
       format_type(a.atttypid, a.atttypmod) AS type_rendu,
       t.typname, t.typcategory
FROM pg_attribute a
JOIN pg_class cl ON cl.oid = a.attrelid
JOIN pg_namespace n ON n.oid = cl.relnamespace
JOIN pg_type t ON t.oid = a.atttypid
WHERE n.nspname = 'public' AND cl.relname = 'adherents'
  AND a.attnum > 0 AND NOT a.attisdropped
ORDER BY a.attnum;

\echo '--- bytea::text : hexadécimal ou échappement ?'
SHOW bytea_output;
SELECT photo::text AS bytea_en_texte, left(photo::text, 6) AS tronque
FROM public.adherents WHERE photo IS NOT NULL;

\echo '--- left() sur un entier : lève-t-il sans cast ?'
SELECT left(id, 4) FROM public.adherents LIMIT 1;
