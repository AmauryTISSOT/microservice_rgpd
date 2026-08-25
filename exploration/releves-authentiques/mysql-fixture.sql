-- Base d'épreuve MySQL / MariaDB.

CREATE TABLE adherents (
  id           BIGINT AUTO_INCREMENT PRIMARY KEY,
  a_supprimer  VARCHAR(50),
  adr_l1       VARCHAR(255),
  courriel     VARCHAR(255) NOT NULL COMMENT 'adresse de contact',
  photo        BLOB,
  signature    VARBINARY(255),
  notes        LONGTEXT,
  solde        DECIMAL(12,2),
  cree_le      DATETIME,
  actif        TINYINT(1),
  jeton        CHAR(36),
  etiquettes   JSON
) ENGINE=InnoDB COMMENT='les adhérents';

ALTER TABLE adherents DROP COLUMN a_supprimer;

CREATE TABLE cotisations (
  id           BIGINT AUTO_INCREMENT PRIMARY KEY,
  adherent_id  BIGINT,
  montant      DECIMAL(10,2),
  CONSTRAINT fk_cot_adh FOREIGN KEY (adherent_id) REFERENCES adherents(id)
) ENGINE=InnoDB;

CREATE VIEW v_adherents AS SELECT id, courriel FROM adherents;

INSERT INTO adherents (adr_l1, courriel, photo, notes, solde, actif)
VALUES ('12 rue des Lilas', 'ada@example.org', 0x0102039FFF, 'éàçüö texte accentué long', 42.50, 1),
       (NULL,               'bob@example.org', NULL,         NULL,                          NULL,  0);

-- Trois comptes : l'un voit la base entière, l'un ne voit qu'une colonne, l'un ne voit rien.
CREATE USER 'plein'@'%'   IDENTIFIED BY 'plein';
CREATE USER 'partiel'@'%' IDENTIFIED BY 'partiel';
CREATE USER 'nu'@'%'      IDENTIFIED BY 'nu';

GRANT SELECT ON epreuve.* TO 'plein'@'%';
GRANT SELECT (courriel) ON epreuve.adherents TO 'partiel'@'%';
FLUSH PRIVILEGES;
