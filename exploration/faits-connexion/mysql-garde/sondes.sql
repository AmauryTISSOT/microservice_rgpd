-- Sondes du garde de privilege (#286), rejouees a l'identique par chaque compte.
-- Aucun `USE epreuve` : le compte `nu` n'a pas le droit d'ouvrir la base, et le
-- garde doit de toute facon savoir repondre avant d'y entrer.

SELECT '=== 1. SHOW GRANTS FOR CURRENT_USER()' AS sonde;
SHOW GRANTS FOR CURRENT_USER();

SELECT '=== 2. information_schema.USER_PRIVILEGES (le droit global)' AS sonde;
SELECT GRANTEE, PRIVILEGE_TYPE FROM information_schema.USER_PRIVILEGES ORDER BY 1,2;

SELECT '=== 3. information_schema.SCHEMA_PRIVILEGES (le grain de la base)' AS sonde;
SELECT GRANTEE, TABLE_SCHEMA, PRIVILEGE_TYPE FROM information_schema.SCHEMA_PRIVILEGES ORDER BY 1,2,3;

SELECT '=== 4. information_schema.TABLE_PRIVILEGES (le grain de la table)' AS sonde;
SELECT GRANTEE, TABLE_SCHEMA, TABLE_NAME, PRIVILEGE_TYPE FROM information_schema.TABLE_PRIVILEGES ORDER BY 1,2,3,4;

SELECT '=== 5. information_schema.COLUMN_PRIVILEGES (le grain de la colonne)' AS sonde;
SELECT GRANTEE, TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME, PRIVILEGE_TYPE FROM information_schema.COLUMN_PRIVILEGES ORDER BY 1,2,3,4;

SELECT '=== 6. CATALOGUE : les objets de epreuve vus par ce compte' AS sonde;
SELECT TABLE_NAME, TABLE_TYPE FROM information_schema.TABLES WHERE TABLE_SCHEMA='epreuve' ORDER BY 1;

SELECT '=== 7. CATALOGUE : colonnes par objet, vues par ce compte' AS sonde;
SELECT TABLE_NAME, COUNT(*) AS colonnes FROM information_schema.COLUMNS WHERE TABLE_SCHEMA='epreuve' GROUP BY TABLE_NAME ORDER BY 1;

SELECT '=== 8. GARDE CANDIDAT : objets visibles NON couverts par un droit base/global/table' AS sonde;
SELECT t.TABLE_NAME AS objet_sans_droit_plein
FROM information_schema.TABLES t
WHERE t.TABLE_SCHEMA = 'epreuve'
  AND NOT EXISTS (SELECT 1 FROM information_schema.USER_PRIVILEGES up
                  WHERE up.PRIVILEGE_TYPE = 'SELECT')
  AND NOT EXISTS (SELECT 1 FROM information_schema.SCHEMA_PRIVILEGES sp
                  WHERE sp.TABLE_SCHEMA = 'epreuve' AND sp.PRIVILEGE_TYPE = 'SELECT')
  AND NOT EXISTS (SELECT 1 FROM information_schema.TABLE_PRIVILEGES tp
                  WHERE tp.TABLE_SCHEMA = 'epreuve' AND tp.TABLE_NAME = t.TABLE_NAME
                    AND tp.PRIVILEGE_TYPE = 'SELECT')
ORDER BY 1;

SELECT '=== 9. GARDE CANDIDAT : le verdict en un nombre (0 = laisse passer)' AS sonde;
SELECT COUNT(*) AS objets_amputables
FROM information_schema.TABLES t
WHERE t.TABLE_SCHEMA = 'epreuve'
  AND NOT EXISTS (SELECT 1 FROM information_schema.USER_PRIVILEGES up
                  WHERE up.PRIVILEGE_TYPE = 'SELECT')
  AND NOT EXISTS (SELECT 1 FROM information_schema.SCHEMA_PRIVILEGES sp
                  WHERE sp.TABLE_SCHEMA = 'epreuve' AND sp.PRIVILEGE_TYPE = 'SELECT')
  AND NOT EXISTS (SELECT 1 FROM information_schema.TABLE_PRIVILEGES tp
                  WHERE tp.TABLE_SCHEMA = 'epreuve' AND tp.TABLE_NAME = t.TABLE_NAME
                    AND tp.PRIVILEGE_TYPE = 'SELECT');

SELECT '=== 10. LE POINT AVEUGLE : le catalogue est-il lui-meme ampute ?' AS sonde;
SELECT COUNT(*) AS objets_au_catalogue FROM information_schema.TABLES WHERE TABLE_SCHEMA='epreuve';
SELECT COUNT(*) AS colonnes_au_catalogue FROM information_schema.COLUMNS WHERE TABLE_SCHEMA='epreuve';

SELECT '=== 11. La base epreuve figure-t-elle seulement dans SCHEMATA ?' AS sonde;
SELECT SCHEMA_NAME FROM information_schema.SCHEMATA ORDER BY 1;
