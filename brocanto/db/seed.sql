-- Brocanto — jeu de données de recette
-- Rechargé par `docker compose down -v && docker compose up`.

SET NAMES utf8mb4;

INSERT INTO clients (id, email, prenom, nom, telephone, mot_de_passe_hash, cree_le, derniere_connexion) VALUES
 (1, 'jean.dupont@example.fr',     'Jean',    'Dupont',   '06 12 34 56 78', 'sha1$8f2a1c', '2023-03-11 19:04:22', '2026-07-21 08:12:40'),
 (2, 'amina.belkacem@example.fr',  'Amina',   'Belkacem', '07 88 45 12 03', 'sha1$c41b90', '2023-09-02 12:41:07', '2026-07-28 21:33:19'),
 (3, 'luc.moreau@example.org',     'Luc',     'Moreau',   NULL,             'sha1$1de770', '2024-01-18 09:15:51', '2026-06-30 17:02:08'),
 (4, 'sophie.nguyen@example.fr',   'Sophie',  'Nguyen',   '06 45 78 22 91', 'sha1$aa03f5', '2024-05-27 22:10:33', '2026-07-29 12:55:02'),
 (5, 'patrick.roussel@example.fr', 'Patrick', 'Roussel',  '05 56 21 44 09', 'sha1$77b2ee', '2024-11-04 14:22:16', '2026-02-11 10:41:37'),
 (6, 'claire.faure@example.fr',    'Claire',  'Faure',    NULL,             'sha1$3c9d18', '2025-02-19 08:33:44', '2026-07-25 19:20:11'),
 (7, 'thomas.girard@example.fr',   'Thomas',  'Girard',   '06 71 03 88 54', 'sha1$b50a4c', '2025-06-08 16:47:29', '2026-05-14 07:38:56'),
 -- créé au téléphone par le support le jour où M. Dupont ne retrouvait plus son mot de passe.
 (8, 'Jean.Dupont@Example.fr',     'jean',    'dupont',   '0612345678',     'sha1$e0f3b7', '2025-10-02 11:26:04', '2026-03-09 20:15:48');

INSERT INTO adresses (client_id, destinataire, ligne1, ligne2, code_postal, ville, pays, principale) VALUES
 (1, 'Jean Dupont',       '14 rue des Lilas',            'Bâtiment C',      '35000', 'Rennes',    'France', 1),
 (1, 'Yvette Dupont',     '3 impasse du Verger',         NULL,              '22100', 'Dinan',     'France', 0),
 (2, 'Amina Belkacem',    '87 boulevard Gambetta',       'Appartement 12',  '59000', 'Lille',     'France', 1),
 (3, 'Luc Moreau',        "9 chemin de l'Ancienne Gare", NULL,              '31400', 'Toulouse',  'France', 1),
 (4, 'Sophie Nguyen',     '22 quai de la Fosse',         NULL,              '44000', 'Nantes',    'France', 1),
 (5, 'Patrick Roussel',   '5 allée des Chênes',          NULL,              '33200', 'Bordeaux',  'France', 1),
 (6, 'Claire Faure',      '61 rue Saint-Michel',         '3e étage gauche', '67000', 'Strasbourg','France', 1),
 (7, 'Thomas Girard',     '2 place du Marché',           NULL,              '21000', 'Dijon',     'France', 1),
 (8, 'Jean Dupont',       '14 rue des Lilas',            'Bat C',           '35000', 'Rennes',    'France', 1);

INSERT INTO annonces (id, vendeur_id, titre, description, prix_centimes, etat, publiee_le) VALUES
 (1, 2, 'Buffet Henri II en chêne',
     "Buffet de famille, deux portes, quelques traces d'usage sur le plateau. Enlèvement sur place à Lille, je ne livre pas. Pour organiser, appelez-moi de préférence le soir au 07 88 45 12 03.",
     18000, 'en_ligne', '2026-04-12 10:22:41'),
 (2, 1, 'Lot de 40 disques vinyle 33 tours',
     "Variété française des années 70-80, pochettes en bon état. Visibles au 14 rue des Lilas à Rennes, sonner au 3e.",
     6500, 'vendue', '2026-03-02 18:51:09'),
 (3, 2, 'Machine à coudre Singer 1952',
     "Fonctionne, révisée l'an dernier. Facture de la révision fournie.",
     12000, 'en_ligne', '2026-05-20 09:14:57'),
 (4, 5, 'Établi de menuisier',
     "Hêtre massif, 2m10. Trop lourd pour être expédié. Écrivez-moi ici ou au 05 56 21 44 09.",
     25000, 'en_ligne', '2026-06-01 15:39:12'),
 (5, 7, 'Service de table Limoges 24 pièces',
     "Complet, jamais servi, encore dans son carton d'origine.",
     9000, 'vendue', '2026-02-14 11:05:33'),
 (6, 4, 'Vélo de course Peugeot PX10',
     "Cadre Reynolds, roues d'origine. Vendu en l'état.",
     32000, 'archivee', '2025-11-08 20:44:02'),
 (7, 6, 'Malle de voyage en osier',
     "Charnières à revoir. Je peux la déposer à Strasbourg centre.",
     4500, 'en_ligne', '2026-07-03 08:27:50'),
 (8, 2, 'Lampe de bureau en laiton',
     "Câble refait aux normes.",
     3500, 'brouillon', NULL);

INSERT INTO annonce_photos (annonce_id, fichier) VALUES
 (1, 'medias/annonces/1/buffet-face.jpg'),
 (1, 'medias/annonces/1/buffet-interieur.jpg'),
 (2, 'medias/annonces/2/vinyles-lot.jpg'),
 (3, 'medias/annonces/3/singer.jpg'),
 (4, 'medias/annonces/4/etabli.jpg'),
 (5, 'medias/annonces/5/limoges.jpg'),
 (7, 'medias/annonces/7/malle-osier.jpg');

INSERT INTO commandes (id, reference, client_id, courriel_acheteur, annonce_id, nom_livraison, adresse_livraison, total_centimes, statut, passee_le) VALUES
 (1, 'BRC-2026-0041', 3,    'luc.moreau@example.org',    2, 'Luc Moreau',    "9 chemin de l'Ancienne Gare\n31400 Toulouse", 6500,  'livree',   '2026-03-05 14:12:38'),
 (2, 'BRC-2026-0042', 1,    'jean.dupont@example.fr',    5, 'Yvette Dupont', "3 impasse du Verger\n22100 Dinan",            9000,  'livree',   '2026-02-18 09:47:20'),
 -- passée sans compte : la seule identité de cette personne dans la base est son adresse.
 (3, 'BRC-2026-0043', NULL, 'helene.petit@example.fr',   6, 'Hélène Petit',  "48 rue Pasteur\n69007 Lyon",                  32000, 'livree',   '2025-11-22 21:03:15'),
 (4, 'BRC-2026-0044', 6,    'claire.faure@example.fr',   NULL,'Claire Faure', "61 rue Saint-Michel\n67000 Strasbourg",       4500,  'expediee', '2026-07-11 16:58:44'),
 (5, 'BRC-2026-0045', 8,    'Jean.Dupont@Example.fr',    3, 'Jean Dupont',   "14 rue des Lilas\n35000 Rennes",              12000, 'annulee',  '2026-01-27 12:30:09');

INSERT INTO factures (numero, commande_id, emise_le, destinataire_nom, destinataire_adresse, destinataire_courriel, total_centimes) VALUES
 ('F-2026-0041', 1, '2026-03-05', 'Luc Moreau',   "9 chemin de l'Ancienne Gare\n31400 Toulouse", 'luc.moreau@example.org',  6500),
 ('F-2026-0042', 2, '2026-02-18', 'Jean Dupont',  "14 rue des Lilas\n35000 Rennes",              'jean.dupont@example.fr',  9000),
 ('F-2025-0387', 3, '2025-11-22', 'Hélène Petit', "48 rue Pasteur\n69007 Lyon",                  'helene.petit@example.fr', 32000),
 ('F-2026-0044', 4, '2026-07-11', 'Claire Faure', "61 rue Saint-Michel\n67000 Strasbourg",       'claire.faure@example.fr', 4500);

INSERT INTO paiements (commande_id, prestataire, reference_psp, porteur_nom, carte_4_derniers, montant_centimes, paye_le) VALUES
 (1, 'stripe',  'pi_3PkQ2wF1mZ8vT0xa', 'LUC MOREAU',   '4242', 6500,  '2026-03-05 14:12:51'),
 (2, 'stripe',  'pi_3Pa9LmF1mZ8vT0bd', 'JEAN DUPONT',  '1881', 9000,  '2026-02-18 09:47:33'),
 (3, 'paypal',  '8XJ42910KL9927145',   'HELENE PETIT', NULL,   32000, '2025-11-22 21:03:41'),
 (4, 'stripe',  'pi_3PzR7hF1mZ8vT0mn', 'CLAIRE FAURE', '0093', 4500,  '2026-07-11 16:58:59');

INSERT INTO clients_ancienne_boutique (id_ancien, mail, nom_complet, ville, importe_le) VALUES
 (1042, 'patrick.roussel@example.fr', 'Patrick ROUSSEL',   'Bordeaux',  '2019-03-14 03:11:00'),
 (1188, 'g.lemercier@example.fr',     'Gérard LEMERCIER',  'Le Mans',   '2019-03-14 03:11:00'),
 (1203, 'jean.dupont@example.fr',     'J. DUPONT',         'Rennes',    '2019-03-14 03:11:00'),
 (1291, 'nadia.oussekine@example.fr', 'Nadia OUSSEKINE',   'Marseille', '2019-03-14 03:11:00');

INSERT INTO newsletter (courriel, inscrit_le, source, desinscrit_le) VALUES
 ('jean.dupont@example.fr',    '2023-03-11 19:05:02', 'inscription',  NULL),
 ('amina.belkacem@example.fr', '2023-09-02 12:42:10', 'inscription',  NULL),
 ('sophie.nguyen@example.fr',  '2024-05-27 22:11:04', 'inscription',  '2026-01-09 08:22:31'),
 ('helene.petit@example.fr',   '2025-11-22 21:04:02', 'commande',     NULL),
 ('marc.leroy@example.fr',     '2025-04-30 13:18:55', 'pied-de-page', NULL),
 ('r.benhamou@example.fr',     '2026-06-17 10:02:44', 'pied-de-page', NULL),
 ('claire.faure@example.fr',   '2025-02-19 08:34:12', 'inscription',  NULL);

INSERT INTO messages (fil_id, auteur_courriel, auteur_role, corps, envoye_le) VALUES
 (1, 'luc.moreau@example.org',   'client',  "Bonjour, le colis est annoncé pour mardi mais je serai absent. Peut-on le faire livrer chez ma soeur, Mme Karine Moreau, 12 rue Bayard à Toulouse ? Mon portable si besoin : 06 22 41 07 65.", '2026-03-03 10:14:22'),
 (1, 'support@brocanto.fr',      'support', "Bonjour M. Moreau, c'est noté, j'ai changé l'adresse auprès du transporteur.", '2026-03-03 11:02:47'),
 (2, 'jean.dupont@example.fr',   'client',  "Je n'arrive plus à me connecter avec mon adresse habituelle. Pouvez-vous regarder ? J'ai commandé sous jean.dupont@example.fr en mars.", '2025-10-02 11:18:33'),
 (2, 'support@brocanto.fr',      'support', "Bonjour, je vous ai recréé un accès, vous recevrez le mot de passe par mail.", '2025-10-02 11:26:19'),
 (3, 'helene.petit@example.fr',  'client',  "Bonjour, j'ai acheté le vélo Peugeot le mois dernier sans créer de compte. Je voudrais la facture pour mon assurance. Hélène Petit, 48 rue Pasteur, 69007 Lyon.", '2025-12-04 18:41:56'),
 (4, 'sophie.nguyen@example.fr', 'client',  "Merci de ne plus m'envoyer la lettre d'information, je me suis désinscrite deux fois déjà.", '2026-01-09 08:20:15');

INSERT INTO stats_annonces_jour (jour, annonce_id, vues, visiteurs_uniques) VALUES
 ('2026-07-27', 1, 48, 31), ('2026-07-27', 3, 12, 11), ('2026-07-27', 4, 27, 22), ('2026-07-27', 7, 9,  9),
 ('2026-07-28', 1, 55, 40), ('2026-07-28', 3, 15, 14), ('2026-07-28', 4, 31, 25), ('2026-07-28', 7, 14, 12),
 ('2026-07-29', 1, 39, 28), ('2026-07-29', 3, 8,  8),  ('2026-07-29', 4, 19, 18), ('2026-07-29', 7, 21, 17);
