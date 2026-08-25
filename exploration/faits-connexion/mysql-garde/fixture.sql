-- Base d'épreuve du garde de privilège (#286).
-- Même forme que la fixture de #275, mais l'enjeu n'est plus le relevé : ce sont
-- les comptes. Sept profils de droits, dont quatre qui voient tout par des voies
-- différentes et deux qui amputent par des voies différentes.

CREATE TABLE adherents (
  id           BIGINT AUTO_INCREMENT PRIMARY KEY,
  adr_l1       VARCHAR(255),
  courriel     VARCHAR(255) NOT NULL COMMENT 'adresse de contact',
  photo        BLOB,
  notes        LONGTEXT,
  solde        DECIMAL(12,2),
  cree_le      DATETIME,
  actif        TINYINT(1),
  jeton        CHAR(36)
) ENGINE=InnoDB COMMENT='les adhérents';

CREATE TABLE cotisations (
  id           BIGINT AUTO_INCREMENT PRIMARY KEY,
  adherent_id  BIGINT,
  montant      DECIMAL(10,2),
  CONSTRAINT fk_cot_adh FOREIGN KEY (adherent_id) REFERENCES adherents(id)
) ENGINE=InnoDB;

CREATE TABLE journaux (
  id     BIGINT AUTO_INCREMENT PRIMARY KEY,
  ligne  VARCHAR(255)
) ENGINE=InnoDB;

CREATE VIEW v_adherents AS SELECT id, courriel FROM adherents;

INSERT INTO adherents (adr_l1, courriel, solde, actif)
VALUES ('12 rue des Lilas', 'ada@example.org', 42.50, 1);

-- ---------------------------------------------------------------- les comptes

-- 1. voit tout, par un droit au grain de la base
CREATE USER 'plein'@'%'    IDENTIFIED BY 'plein';
GRANT SELECT ON epreuve.* TO 'plein'@'%';

-- 2. voit tout, par un droit global — SCHEMA_PRIVILEGES ne le porte pas
CREATE USER 'global'@'%'   IDENTIFIED BY 'global';
GRANT SELECT ON *.* TO 'global'@'%';

-- 3. voit tout, par des droits table par table couvrant TOUS les objets
--    le cas qui decide du prix du garde : un garde exigeant le grain de la
--    base le refuserait a tort.
CREATE USER 'tableatable'@'%' IDENTIFIED BY 'tableatable';
GRANT SELECT ON epreuve.adherents   TO 'tableatable'@'%';
GRANT SELECT ON epreuve.cotisations TO 'tableatable'@'%';
GRANT SELECT ON epreuve.journaux    TO 'tableatable'@'%';
GRANT SELECT ON epreuve.v_adherents TO 'tableatable'@'%';

-- 4. ampute au grain de la COLONNE — le cas mesure par #275
CREATE USER 'partiel'@'%'  IDENTIFIED BY 'partiel';
GRANT SELECT (courriel) ON epreuve.adherents TO 'partiel'@'%';

-- 5. ampute au grain de la TABLE — droits table par table NE couvrant pas tout.
--    Indistinguable de 3 ? C'est la question mortelle.
CREATE USER 'partieltable'@'%' IDENTIFIED BY 'partieltable';
GRANT SELECT ON epreuve.adherents TO 'partieltable'@'%';

-- 6. voit tout, par un ROLE — les droits herites remontent-ils ?
CREATE ROLE 'r_lecture';
GRANT SELECT ON epreuve.* TO 'r_lecture';
CREATE USER 'parrole'@'%' IDENTIFIED BY 'parrole';
GRANT 'r_lecture' TO 'parrole'@'%';

-- 7. ne voit rien
CREATE USER 'nu'@'%'       IDENTIFIED BY 'nu';

FLUSH PRIVILEGES;
