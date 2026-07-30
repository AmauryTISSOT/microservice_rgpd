-- Brocanto — schéma de la base
-- MariaDB 11 / InnoDB / utf8mb4
--
-- Pas de migrations : on édite ce fichier et on recharge en recette.
-- (TODO reparler de flyway avec Karim — ouvert depuis 2024)

SET NAMES utf8mb4;

CREATE TABLE clients (
    id                INT AUTO_INCREMENT PRIMARY KEY,
    email             VARCHAR(190)  NOT NULL,
    prenom            VARCHAR(120),
    nom               VARCHAR(120),
    telephone         VARCHAR(30),
    mot_de_passe_hash VARCHAR(120)  NOT NULL,
    cree_le           DATETIME      NOT NULL,
    derniere_connexion DATETIME,
    INDEX idx_clients_email (email)
    -- pas d'unicité sur l'email : le support crée parfois un second compte
    -- quand le client dit « je n'arrive plus à me connecter »
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE adresses (
    id           INT AUTO_INCREMENT PRIMARY KEY,
    client_id    INT NOT NULL,
    destinataire VARCHAR(160) NOT NULL,  -- pas forcément le titulaire du compte
    ligne1       VARCHAR(200) NOT NULL,
    ligne2       VARCHAR(200),
    code_postal  VARCHAR(12)  NOT NULL,
    ville        VARCHAR(120) NOT NULL,
    pays         VARCHAR(60)  NOT NULL DEFAULT 'France',
    principale   TINYINT(1)   NOT NULL DEFAULT 0,
    CONSTRAINT fk_adresses_client FOREIGN KEY (client_id) REFERENCES clients (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE annonces (
    id            INT AUTO_INCREMENT PRIMARY KEY,
    vendeur_id    INT NOT NULL,
    titre         VARCHAR(200) NOT NULL,
    description   TEXT,
    prix_centimes INT NOT NULL,
    etat          ENUM('brouillon','en_ligne','vendue','archivee') NOT NULL DEFAULT 'brouillon',
    publiee_le    DATETIME,
    CONSTRAINT fk_annonces_vendeur FOREIGN KEY (vendeur_id) REFERENCES clients (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE annonce_photos (
    id         INT AUTO_INCREMENT PRIMARY KEY,
    annonce_id INT NOT NULL,
    fichier    VARCHAR(200) NOT NULL,  -- chemin relatif dans medias/
    CONSTRAINT fk_photos_annonce FOREIGN KEY (annonce_id) REFERENCES annonces (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE commandes (
    id                 INT AUTO_INCREMENT PRIMARY KEY,
    reference          VARCHAR(20)  NOT NULL UNIQUE,
    client_id          INT,                        -- NULL quand la commande a été passée sans compte
    courriel_acheteur  VARCHAR(190) NOT NULL,      -- recopié à la commande, sert aux mails de suivi
    annonce_id         INT,
    nom_livraison      VARCHAR(160) NOT NULL,
    adresse_livraison  TEXT NOT NULL,
    total_centimes     INT NOT NULL,
    statut             ENUM('payee','expediee','livree','annulee') NOT NULL DEFAULT 'payee',
    passee_le          DATETIME NOT NULL,
    CONSTRAINT fk_commandes_client FOREIGN KEY (client_id) REFERENCES clients (id),
    CONSTRAINT fk_commandes_annonce FOREIGN KEY (annonce_id) REFERENCES annonces (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Comptabilité : pièces scellées, conservation dix ans (art. L123-22 c. com.).
-- Ne jamais modifier une ligne après émission — l'expert-comptable relit.
CREATE TABLE factures (
    id                   INT AUTO_INCREMENT PRIMARY KEY,
    numero               VARCHAR(20) NOT NULL UNIQUE,
    commande_id          INT NOT NULL,
    emise_le             DATE NOT NULL,
    destinataire_nom     VARCHAR(160) NOT NULL,
    destinataire_adresse TEXT NOT NULL,
    destinataire_courriel VARCHAR(190) NOT NULL,
    total_centimes       INT NOT NULL,
    CONSTRAINT fk_factures_commande FOREIGN KEY (commande_id) REFERENCES commandes (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Le détail de la carte est chez le prestataire ; on ne garde que de quoi
-- retrouver la transaction dans son back-office.
CREATE TABLE paiements (
    id              INT AUTO_INCREMENT PRIMARY KEY,
    commande_id     INT NOT NULL,
    prestataire     VARCHAR(30)  NOT NULL,
    reference_psp   VARCHAR(60)  NOT NULL,
    porteur_nom     VARCHAR(160),
    carte_4_derniers CHAR(4),
    montant_centimes INT NOT NULL,
    paye_le         DATETIME NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Reprise de l'ancienne boutique PrestaShop (mars 2019).
-- Gardée « le temps de vérifier », jamais rapprochée de `clients`.
CREATE TABLE clients_ancienne_boutique (
    id_ancien   INT PRIMARY KEY,
    mail        VARCHAR(190),
    nom_complet VARCHAR(200),
    ville       VARCHAR(120),
    importe_le  DATETIME
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Aucune clé vers `clients` : on s'inscrit à la lettre sans avoir de compte.
CREATE TABLE newsletter (
    courriel      VARCHAR(190) PRIMARY KEY,
    inscrit_le    DATETIME NOT NULL,
    source        VARCHAR(40),
    desinscrit_le DATETIME
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Messagerie support. L'auteur est identifié par son adresse, pas par son compte,
-- parce que les gens écrivent aussi depuis leur boîte perso.
CREATE TABLE messages (
    id              INT AUTO_INCREMENT PRIMARY KEY,
    fil_id          INT NOT NULL,
    auteur_courriel VARCHAR(190) NOT NULL,
    auteur_role     ENUM('client','support') NOT NULL,
    corps           TEXT NOT NULL,
    envoye_le       DATETIME NOT NULL,
    INDEX idx_messages_fil (fil_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Agrégats alimentés chaque nuit par un cron. Ne portent aucune identité :
-- il faut passer par `annonces.vendeur_id` pour savoir de qui on parle.
CREATE TABLE stats_annonces_jour (
    jour              DATE NOT NULL,
    annonce_id        INT  NOT NULL,
    vues              INT  NOT NULL DEFAULT 0,
    visiteurs_uniques INT  NOT NULL DEFAULT 0,
    PRIMARY KEY (jour, annonce_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
