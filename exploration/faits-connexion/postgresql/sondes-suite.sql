-- Sondes secondaires, suite : left() sur NULL, et le relevé par information_schema.
-- Jouées séparément pour qu'un échec attendu (left sur entier) n'avorte pas le reste.

\echo '--- left() sur NULL propage-t-il NULL ?'
SELECT left(NULL::text, 64) IS NULL AS null_propage;

\echo '--- Relevé équivalent par information_schema : combien de colonnes ?'
SELECT count(*) AS colonnes_information_schema
FROM information_schema.columns c
JOIN information_schema.tables t
  ON t.table_schema = c.table_schema AND t.table_name = c.table_name
 AND t.table_type = 'BASE TABLE'
WHERE c.table_schema NOT IN ('pg_catalog', 'information_schema');

\echo '--- Relevé par pg_catalog : combien de colonnes ?'
SELECT count(*) AS colonnes_pg_catalog
FROM pg_attribute a
JOIN pg_class cl ON cl.oid = a.attrelid
JOIN pg_namespace n ON n.oid = cl.relnamespace
WHERE cl.relkind = 'r' AND a.attnum > 0 AND NOT a.attisdropped
  AND n.nspname NOT IN ('pg_catalog', 'information_schema')
  AND n.nspname NOT LIKE 'pg_toast%';

\echo '--- Les commentaires sont-ils lisibles ? (information_schema n en expose aucun)'
SELECT col_description(cl.oid, a.attnum) AS commentaire
FROM pg_attribute a
JOIN pg_class cl ON cl.oid = a.attrelid
JOIN pg_namespace n ON n.oid = cl.relnamespace
WHERE n.nspname = 'public' AND cl.relname = 'adherents' AND a.attname = 'courriel';

\echo '--- Un SELECT sur la donnée elle-même passe-t-il ?'
SELECT count(*) FROM public.adherents;
