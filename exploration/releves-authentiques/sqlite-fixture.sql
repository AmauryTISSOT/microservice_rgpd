-- Base d'épreuve SQLite : de quoi exercer les neuf champs du pivot.
--
-- SQLite ne rend aucun commentaire : `commentaire_colonne` et `commentaire_table`
-- sortent structurellement vides. La fixture exerce donc ce qui reste — la
-- nullabilité (les deux valeurs), la clé étrangère, et l'absence de type.

CREATE TABLE adherents (
  id         INTEGER PRIMARY KEY,
  adr_l1     TEXT,
  courriel   TEXT NOT NULL,
  photo      BLOB,
  solde      NUMERIC,
  cree_le    TEXT,
  actif      INTEGER,
  piece      ANY
);

CREATE TABLE cotisations (
  id          INTEGER PRIMARY KEY,
  adherent_id INTEGER REFERENCES adherents(id),
  montant     NUMERIC NOT NULL
);

CREATE VIEW v_adherents AS SELECT id, courriel FROM adherents;

INSERT INTO adherents (adr_l1, courriel, photo, solde, actif, piece)
VALUES ('12 rue des Lilas', 'ada@example.org', x'0102039fff', 42.50, 1, 'une chaîne'),
       (NULL,               'bob@example.org', NULL,          NULL,  0, NULL);
