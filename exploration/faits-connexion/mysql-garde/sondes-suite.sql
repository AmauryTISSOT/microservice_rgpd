-- Sondes complementaires (#286). Deux questions que la premiere salve a ouvertes.

SELECT '=== A. Reste-t-il une TRACE des objets invisibles ailleurs dans le catalogue ?' AS sonde;
SELECT '--- A1 SHOW TABLES equivalent' AS sonde;
SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA='epreuve' ORDER BY 1;
SELECT '--- A2 contraintes : une FK peut-elle nommer une table invisible ?' AS sonde;
SELECT CONSTRAINT_NAME, TABLE_NAME, REFERENCED_TABLE_NAME
FROM information_schema.KEY_COLUMN_USAGE WHERE TABLE_SCHEMA='epreuve' ORDER BY 1,2;
SELECT '--- A3 REFERENTIAL_CONSTRAINTS' AS sonde;
SELECT CONSTRAINT_NAME, TABLE_NAME, REFERENCED_TABLE_NAME
FROM information_schema.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_SCHEMA='epreuve' ORDER BY 1;
SELECT '--- A4 STATISTICS (index)' AS sonde;
SELECT DISTINCT TABLE_NAME FROM information_schema.STATISTICS WHERE TABLE_SCHEMA='epreuve' ORDER BY 1;
SELECT '--- A5 VIEWS' AS sonde;
SELECT TABLE_NAME FROM information_schema.VIEWS WHERE TABLE_SCHEMA='epreuve' ORDER BY 1;

SELECT '=== B. Les roles : une voie rend-elle le droit herite au compte lui-meme ?' AS sonde;
SELECT '--- B1 CURRENT_ROLE()' AS sonde;
SELECT CURRENT_ROLE();
SELECT '--- B2 APPLICABLE_ROLES' AS sonde;
SELECT * FROM information_schema.APPLICABLE_ROLES;
SELECT '--- B3 ENABLED_ROLES' AS sonde;
SELECT * FROM information_schema.ENABLED_ROLES;
SELECT '--- B4 SHOW GRANTS FOR le role lui-meme' AS sonde;
SHOW GRANTS FOR 'r_lecture';

SELECT '=== C. LE GARDE PRAGMATIQUE : un SELECT a vide par objet du catalogue' AS sonde;
SELECT '--- C1 adherents' AS sonde;
SELECT * FROM epreuve.adherents LIMIT 0;
SELECT '--- C2 cotisations' AS sonde;
SELECT * FROM epreuve.cotisations LIMIT 0;
SELECT '--- C3 journaux' AS sonde;
SELECT * FROM epreuve.journaux LIMIT 0;
SELECT '--- C4 v_adherents' AS sonde;
SELECT * FROM epreuve.v_adherents LIMIT 0;
