-- Sondes secondaires MySQL / MariaDB. À jouer en root.

SELECT '--- JSON_OBJECT : quelles formes produisent un booléen JSON ?' AS sonde;
SELECT JSON_OBJECT(
         'if_1_0',        IF(1 = 1, 1, 0),
         'comparaison',   (1 = 1),
         'brut_yes',      'YES',
         'tinyint_col',   (SELECT actif FROM adherents WHERE id = 1)
       ) AS formes;

SELECT '--- CAST(... AS JSON) : MariaDB ne connaît pas cette syntaxe' AS sonde;
SELECT JSON_OBJECT('cast_json', CAST(IF(1 = 1, 'true', 'false') AS JSON)) AS forme_cast;

SELECT '--- JSON_COMPACT / repli portable' AS sonde;
SELECT JSON_OBJECT('extrait', JSON_EXTRACT(IF(1 = 1, 'true', 'false'), '$')) AS forme_extract;

SELECT '--- JSON_OBJECT sur IS_NULLABLE brut (le piège de la chaîne)' AS sonde;
SELECT JSON_OBJECT('nullable', IS_NULLABLE) AS naif
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = 'epreuve' AND TABLE_NAME = 'adherents' AND COLUMN_NAME = 'courriel';

SELECT '--- Le type JSON est-il un alias de LONGTEXT ? (voir COLUMN_TYPE de etiquettes)' AS sonde;
SELECT COLUMN_NAME, DATA_TYPE, COLUMN_TYPE
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = 'epreuve' AND TABLE_NAME = 'adherents'
ORDER BY ORDINAL_POSITION;

SELECT '--- ORDINAL_POSITION est-il contigu et part-il de 1, après un DROP COLUMN ?' AS sonde;
SELECT ORDINAL_POSITION, COLUMN_NAME, IS_NULLABLE
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = 'epreuve' AND TABLE_NAME = 'adherents'
ORDER BY ORDINAL_POSITION;

SELECT '--- TABLE_COMMENT porte-t-il un suffixe InnoDB free: ?' AS sonde;
SELECT TABLE_NAME, TABLE_TYPE, CONCAT('[', TABLE_COMMENT, ']') AS commentaire
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = 'epreuve';

SELECT '--- LEFT() est-il multibyte safe ? (25 caractères accentués attendus, pas 25 octets)' AS sonde;
SELECT notes, LEFT(notes, 10) AS tronque, CHAR_LENGTH(LEFT(notes, 10)) AS caracteres,
       LENGTH(LEFT(notes, 10)) AS octets
FROM adherents WHERE notes IS NOT NULL;

SELECT '--- LEFT() sur un BLOB et sur un entier' AS sonde;
SELECT HEX(LEFT(photo, 3)) AS blob_tronque, LEFT(id, 4) AS entier_tronque
FROM adherents WHERE photo IS NOT NULL;

SELECT '--- LEFT() sur NULL' AS sonde;
SELECT LEFT(NULL, 64) IS NULL AS null_propage;

SELECT '--- max_allowed_packet par défaut' AS sonde;
SELECT @@max_allowed_packet AS max_allowed_packet, @@version AS version, @@version_comment AS commentaire;
