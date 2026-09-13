-- Données de démonstration du Tableau des demandes : cent demandes fictives.
--
-- ⚠️ CE N'EST PAS UNE MIGRATION. Le script n'est jamais joué au démarrage du service ; il se lance
-- à la main, en local, par `scripts/seed-requests.sh`, qui vise le seul conteneur Postgres d'Aspire.
--
-- Il écrit directement dans `data_subject_requests`, sans passer par le domaine : c'est le seul
-- chemin qui donne des demandes « Terminée » ou « Annulée », qu'aucun geste ne sait encore produire
-- (ADR-0021). Il reprend donc à la main ce que le domaine garantit ailleurs :
--   - les vocabulaires fermés s'écrivent par leur nom (`Email`, `Access`, `InProgress`…) ;
--   - la date limite de réponse est la date de réception plus un mois, comme `AddMonths(1)` ;
--   - la réception n'est jamais postérieure au jour de Paris, et la création jamais antérieure à la
--     réception ni postérieure à l'instant présent ;
--   - la demande est enregistrée par `operator` et n'a jamais été modifiée.
-- `tests/MicroserviceRgpd.IntegrationTests/Scripts/SeedRequestsTests.cs` le rejoue contre les
-- migrations du dépôt et relit chaque demande par EF Core : c'est lui qui tient ce fichier en phase
-- avec le schéma.
--
-- Rejouable : les demandes plantées portent des identifiants fixes, pris dans une plage réservée
-- (`5eed0000-0000-7000-8000-…`). Le script efface d'abord cette plage, et elle seule, puis la
-- replante, dans une même transaction. Une demande saisie à la main n'est jamais touchée.
--
-- Variables psql, toutes facultatives :
--   reset       définie : efface la plage réservée sans rien replanter ;
--   aujourdhui  le jour de Paris dont partent les dates (par défaut, celui de l'instant présent) ;
--   maintenant  l'instant présent (par défaut, `now()`).
--
-- Les dates sont relatives au jour du lancement, pour que les signalements du tableau restent justes.
-- Chaque demande est ancrée de l'une de trois façons :
--   recue                 reçue il y a `jours` jours ;
--   echeance              date limite visée à `jours` jours d'aujourd'hui, jamais dépassée : en fin
--                         de mois, où « un mois avant » n'a pas de réponse exacte, elle tombe un à
--                         trois jours plus tôt ;
--   echeance-au-plus-tot  date limite visée de même, jamais devancée : en fin de mois, elle tombe un
--                         à trois jours plus tard.
-- Les 70 demandes en cours se répartissent ainsi, quel que soit le jour :
--   10 « En retard »        échéance visée à J-1 ou avant ;
--   10 « Échéance proche »  une échéance au plus tôt à J, une au plus tard à J+7, huit entre J+3 et J+6 ;
--   50 sans signalement     reçues dans les 20 derniers jours, donc à échéance au-delà de J+7.
-- S'y ajoutent 20 demandes terminées et 10 annulées, reçues sur les six derniers mois.
--
-- Les personnes sont fictives et leurs emails sur des domaines réservés à l'exemple (RFC 2606) ; les
-- messages viennent de `corpus/demandes-rgpd.fr.jsonl`.

\set ON_ERROR_STOP on

\if :{?aujourdhui}
\else
SELECT (now() AT TIME ZONE 'Europe/Paris')::date AS aujourdhui \gset
\endif

\if :{?maintenant}
\else
SELECT now() AS maintenant \gset
\endif

BEGIN;

DELETE FROM data_subject_requests
WHERE id BETWEEN '5eed0000-0000-7000-8000-000000000000' AND '5eed0000-0000-7000-8000-ffffffffffff';

\if :{?reset}
\else
WITH demandes (n, origine, ancrage, jours, nom, prenom, email, identite_verifiee, droit, statut,
               delai_de_saisie, heure_de_saisie, message) AS (VALUES
  (1, 'Email', 'echeance', -13, NULL, NULL, 'client.fidele1@example.org', false, 'Rectification', 'InProgress', 0, '14:20', 'Vous avez encore mon ancien mail. Le bon c''est prenom.nom@exemple.invalid, changez-le s''il vous plaît.'),
  (2, 'Email', 'recue', 70, 'Vasseur', 'Agnès', 'agnes.vasseur@example.net', true, 'Access', 'Completed', 0, '09:12', 'Je voudrais connaître le score que votre algorithme m''a attribué et sur quelles données il s''appuie.'),
  (3, 'Email', 'recue', 15, NULL, NULL, 'ex.abonne3@example.com', true, 'Access', 'InProgress', 1, '13:58', 'Madame, Monsieur, conformément à l''article 15 du règlement (UE) 2016/679, je vous demande de me communiquer une copie de l''ensemble des données à caractère personnel me concernant que vous traitez, ainsi que les finalités de ce traitement et les destinataires de ces données. Je vous prie d''agréer mes salutations distinguées.'),
  (4, 'Email', 'recue', 19, 'Charpentier', 'Olivier', 'olivier.charpentier@example.org', false, 'Access', 'InProgress', 1, '17:05', 'Madame, Monsieur,

Client de votre enseigne depuis 2011, d''abord en magasin puis sur votre site et votre application, je vous écris pour exercer le droit d''accès que me reconnaît l''article 15 du règlement (UE) 2016/679. Je précise d''emblée que je ne conteste, à ce stade, aucune opération particulière : je souhaite simplement comprendre ce que vous savez de moi, d''où vous le tenez et ce que vous en faites.

Je vous demande, en premier lieu, la confirmation que des données me concernant sont traitées, et une copie de l''ensemble de ces données. J''entends par là non seulement les informations que je vous ai moi-même fournies (identité, coordonnées, adresses de livraison successives, moyens de paiement enregistrés), mais aussi celles que vous avez produites à mon sujet : historique complet de mes commandes et de mes retours, historique de navigation sur votre site lorsque j''étais connecté, points de fidélité et leur utilisation, échanges avec votre service client, qu''ils aient eu lieu par téléphone, par messagerie instantanée ou par courrier, ainsi que les notes internes que vos conseillers auraient pu consigner à l''occasion de ces échanges.

En deuxième lieu, je souhaite connaître les finalités de chacun de ces traitements et, pour chacune, la base légale sur laquelle vous vous appuyez. Si certaines données sont traitées sur le fondement de votre intérêt légitime, merci de me dire lequel. Si d''autres le sont sur le fondement de mon consentement, merci de m''indiquer à quelle date et par quel moyen ce consentement aurait été recueilli.

En troisième lieu, je vous demande la liste des destinataires ou catégories de destinataires auxquels mes données ont été communiquées : prestataires de livraison, établissements de paiement, régies publicitaires, partenaires commerciaux, sociétés de votre groupe. Si certains de ces destinataires sont établis hors de l''Union européenne, je vous remercie de me préciser les garanties qui encadrent ces transferts et de me dire comment en obtenir une copie.

En quatrième lieu, j''aimerais connaître la durée pendant laquelle vous envisagez de conserver chacune de ces catégories de données ou, à défaut, les critères qui vous servent à fixer cette durée. Je n''ai rien commandé depuis près de deux ans, et je m''interroge sur ce qu''il advient des informations d''un client devenu inactif.

En cinquième lieu, lorsque certaines données n''ont pas été collectées auprès de moi, je vous demande toute information disponible sur leur source. J''ai en effet reçu à plusieurs reprises des offres qui semblaient tenir compte de ma situation familiale et de mes centres d''intérêt, sans que je me souvienne de vous les avoir communiqués. Si ces informations ont été obtenues auprès d''un partenaire, d''un courtier en données ou d''un réseau social, merci de me le préciser, en indiquant son nom et la date de la transmission.

Enfin, je souhaite savoir si vous recourez à une prise de décision automatisée, y compris un profilage, me concernant — par exemple pour déterminer les offres qui me sont présentées, le montant d''un plafond de paiement en plusieurs fois ou l''éligibilité à certaines promotions — et, le cas échéant, obtenir des informations utiles sur la logique sous-jacente ainsi que sur l''importance et les conséquences prévues de ce traitement pour moi.

Pour faciliter vos recherches, je vous indique que mon compte client a été ouvert avec l''adresse électronique qui figure dans ce message, et que j''ai également utilisé une carte de fidélité dont je peux vous communiquer le numéro si nécessaire. Je vous remercie de me faire parvenir ces éléments sous une forme électronique d''usage courant, puisque ma demande vous parvient elle-même par voie électronique.

Je vous rappelle que vous disposez d''un délai d''un mois à compter de la réception de cette demande pour y répondre, délai qui ne peut être prolongé qu''en informant la personne concernée des motifs de cette prolongation. À défaut de réponse dans ce délai, je me réserve la possibilité de saisir la Commission nationale de l''informatique et des libertés.

Je vous prie d''agréer, Madame, Monsieur, l''expression de mes salutations distinguées.

Olivier Charpentier'),
  (5, 'Email', 'recue', 33, 'Benali', 'Karim', 'karim.benali@example.net', false, 'Portability', 'Cancelled', 1, '16:12', 'Je change d''opérateur. Pouvez-vous m''envoyer mes données dans un fichier que je puisse réimporter chez eux ?'),
  (6, 'Email', 'recue', 6, NULL, NULL, 'music.lover6@example.com', true, 'Access', 'InProgress', 0, '16:05', 'Pourriez-vous m''expliquer pourquoi vous conservez mon numéro de téléphone et sur quelle base légale ?'),
  (7, 'Email', 'recue', 4, NULL, NULL, 'jardin.secret7@example.org', false, 'Erasure', 'InProgress', 0, '14:05', 'Retirez-moi de vos fichiers, je ne veux plus rien avoir à faire avec vous.'),
  (8, 'Letter', 'recue', 78, 'Barbier', 'Léa', 'lea.barbier@example.net', true, 'Portability', 'Completed', 2, '14:34', 'Madame, Monsieur,

Merci de m''envoyer un export lisible par machine de mes relevés, je souhaite les importer dans mon logiciel de comptabilité.

Cordialement,
Léa Barbier'),
  (9, 'Email', 'recue', 0, 'Gauthier', 'Emma', 'emma.gauthier@example.com', false, 'Objection', 'InProgress', 0, '11:12', 'Cessez d''utiliser mon adresse pour vos campagnes marketing ; je reste par ailleurs client chez vous.'),
  (10, 'Email', 'echeance', -60, NULL, NULL, 'marcheur.du.dimanche10@example.org', true, 'Access', 'InProgress', 1, '17:47', 'bonjour je veut voire mon dossier chez vous et tout ce que vous savez de moi merci davance'),
  (11, 'Letter', 'recue', 6, 'Lefèvre', 'Élodie', 'elodie.lefevre@example.net', true, 'Access', 'InProgress', 3, '16:20', 'Madame, Monsieur,

D''où viennent les informations que vous avez sur moi ? Je ne me souviens pas vous les avoir communiquées.

Cordialement,
Élodie Lefèvre'),
  (12, 'Email', 'recue', 6, NULL, NULL, 'sam.velo12@example.com', true, 'Access', 'InProgress', 1, '08:47', 'Bonjour, j''aimerais savoir quelles informations vous avez sur moi. Pouvez-vous m''en envoyer la liste ?'),
  (13, 'Letter', 'recue', 16, 'Roger', 'Gabriel', 'gabriel.roger@example.org', true, 'Erasure', 'InProgress', 2, '08:05', 'Madame, Monsieur,

Vous conservez mes données depuis huit ans alors que je ne suis plus client depuis 2019. Merci de les supprimer, elles ne sont plus nécessaires au regard des finalités pour lesquelles vous les aviez collectées.

Cordialement,
Gabriel Roger'),
  (14, 'Email', 'recue', 112, 'Martin', 'Jean', 'jean.martin@example.net', false, 'Access', 'Completed', 0, '14:12', 'Qui a eu accès à mes données ces douze derniers mois ? Merci de me communiquer la liste des destinataires.'),
  (15, 'Letter', 'recue', 19, 'Giraud', 'Bruno', NULL, false, 'Erasure', 'Cancelled', 2, '16:34', 'Madame, Monsieur,

Mon fils a ouvert un compte sur votre site alors qu''il a douze ans. Je demande la suppression de l''ensemble des données le concernant.

Cordialement,
Bruno Giraud'),
  (16, 'Letter', 'echeance', -9, 'Caron', 'Justine', NULL, true, 'Access', 'InProgress', 2, '16:05', 'Madame, Monsieur,

Je vous prie de me transmettre copie de mon dossier client, y compris les échanges enregistrés avec votre service client et les annotations internes me concernant.

Cordialement,
Justine Caron'),
  (17, 'Letter', 'recue', 20, 'Noël', 'Victor', NULL, false, 'Access', 'InProgress', 2, '13:58', 'Madame, Monsieur,

Salut, vous avez quoi comme infos sur moi dans vos fichiers ? J''aimerais bien voir ce que vous stockez.

Cordialement,
Victor Noël'),
  (18, 'Email', 'recue', 2, 'Lebrun', 'Isabelle', 'isabelle.lebrun@example.com', false, 'Objection', 'InProgress', 0, '10:47', 'Je refuse que mes données servent à du profilage publicitaire. Le reste du service peut continuer normalement.'),
  (19, 'Email', 'recue', 170, 'Bonnet', 'Hugo', 'hugo.bonnet@example.org', false, 'Erasure', 'Cancelled', 0, '11:05', 'Je retire le consentement que j''avais donné au traitement de mes données à des fins d''analyse comportementale. Par conséquent, je vous demande de les effacer.'),
  (20, 'Email', 'recue', 16, 'Bernard', 'Camille', 'camille.bernard@example.net', true, 'Rectification', 'InProgress', 0, '09:12', 'Conformément à l''article 16 du règlement (UE) 2016/679, je vous demande de rectifier mon adresse postale, erronée dans vos fichiers. L''adresse exacte est le 4 rue des Lilas, 69003 Lyon.'),
  (21, 'Letter', 'echeance', 3, 'Leclerc', 'Françoise', NULL, true, 'Erasure', 'InProgress', 2, '17:58', 'Madame, Monsieur,

Je ne veux plus apparaître nulle part chez vous. Effacez tout.

Cordialement,
Françoise Leclerc'),
  (22, 'Email', 'recue', 38, 'D''Aboville', 'Nathalie', 'nathalie.daboville@example.org', true, 'Access', 'Completed', 0, '13:34', 'Je souhaite exercer mon droit d''accès. Merci de m''indiquer l''origine des informations que vous détenez sur moi et la durée de conservation prévue.'),
  (23, 'Letter', 'recue', 5, 'Van Anh', 'Nguyen', 'nguyen.vananh@example.net', false, 'Erasure', 'InProgress', 2, '12:12', 'Madame, Monsieur,

bonjour je veut que vous effacer tout ce que vous avez sur moi partout merci

Cordialement,
Nguyen Van Anh'),
  (24, 'Email', 'recue', 1, NULL, NULL, 'plume.bleue24@example.com', false, 'Erasure', 'InProgress', 0, '15:05', 'Je souhaite supprimer mon compte et que vous effaciez toutes les données qui y sont rattachées.'),
  (25, 'Letter', 'recue', 0, 'Guérin', 'Raphaël', NULL, false, 'Portability', 'InProgress', 3, '10:34', 'Madame, Monsieur,

Conformément à l''article 20 du RGPD, je vous demande de me transmettre les données que je vous ai fournies dans un format structuré, couramment utilisé et lisible par machine.

Cordialement,
Raphaël Guérin'),
  (26, 'Letter', 'recue', 9, 'Picard', 'Jade', NULL, false, 'Erasure', 'InProgress', 3, '14:47', 'Madame, Monsieur,

Le traitement de mes données de géolocalisation est dépourvu de base légale. Je vous demande leur effacement sans délai.

Cordialement,
Jade Picard'),
  (27, 'Email', 'recue', 8, NULL, NULL, 'randonneuse27@example.com', true, 'Access', 'InProgress', 1, '16:12', 'Je voudrais connaître le score que votre algorithme m''a attribué et sur quelles données il s''appuie.'),
  (28, 'Email', 'recue', 57, 'Arnaud', 'Patrick', 'patrick.arnaud@example.org', true, 'Objection', 'Completed', 0, '16:12', 'Je m''oppose à l''utilisation de mes données à des fins de recherche statistique, en application de l''article 21.6.'),
  (29, 'Email', 'recue', 3, NULL, NULL, 'bibi.du.6929@example.net', false, 'Access', 'InProgress', 0, '14:00', 'Madame, Monsieur, conformément à l''article 15 du règlement (UE) 2016/679, je vous demande de me communiquer une copie de l''ensemble des données à caractère personnel me concernant que vous traitez, ainsi que les finalités de ce traitement et les destinataires de ces données. Je vous prie d''agréer mes salutations distinguées.'),
  (30, 'Email', 'recue', 178, 'Joly', 'Océane', 'oceane.joly@example.com', true, 'Portability', 'Completed', 0, '11:20', 'Merci de m''exporter mon historique de commandes au format CSV.'),
  (31, 'Letter', 'recue', 1, 'Poirier', 'Sylvie', 'sylvie.poirier@example.org', true, 'Erasure', 'InProgress', 2, '10:58', 'Madame, Monsieur,

Merci de supprimer la photo de profil associée à mon compte, je souhaite qu''elle ne figure plus sur votre plateforme.

Cordialement,
Sylvie Poirier'),
  (32, 'Email', 'echeance', 6, 'Colin', 'Laura', 'laura.colin@example.net', true, 'Access', 'InProgress', 0, '12:47', 'Pourriez-vous m''expliquer pourquoi vous conservez mon numéro de téléphone et sur quelle base légale ?'),
  (33, 'Email', 'recue', 4, NULL, NULL, 'la.fourmi33@example.com', true, 'Portability', 'InProgress', 0, '08:12', 'Je souhaite migrer vers un autre service. Comment obtenir une archive complète de ce que j''ai saisi chez vous ?'),
  (34, 'Email', 'recue', 0, 'Garnier', 'François', 'francois.garnier@example.org', false, 'Erasure', 'InProgress', 0, '09:12', 'En application de l''article 17 du règlement (UE) 2016/679, je vous demande de procéder à l''effacement de l''ensemble des données à caractère personnel me concernant, ainsi qu''à leur suppression chez vos éventuels sous-traitants.'),
  (35, 'Letter', 'recue', 101, 'Bézier', 'Jérôme', NULL, false, 'Access', 'Completed', 2, '13:47', 'Madame, Monsieur,

bonjour je veut voire mon dossier chez vous et tout ce que vous savez de moi merci davance

Cordialement,
Jérôme Bézier'),
  (36, 'Email', 'recue', 12, NULL, NULL, 'contact.perso36@example.com', false, 'Erasure', 'InProgress', 1, '11:34', 'Retirez-moi de vos fichiers, je ne veux plus rien avoir à faire avec vous.'),
  (37, 'Letter', 'recue', 1, 'Masson', 'Alice', 'alice.masson@example.org', true, 'Objection', 'InProgress', 1, '09:58', 'Madame, Monsieur,

Arrêtez de me démarcher, j''en ai assez de vos appels.

Cordialement,
Alice Masson'),
  (38, 'Email', 'recue', 14, 'Morin', 'Christelle', 'christelle.morin@example.net', false, 'Erasure', 'InProgress', 1, '09:12', 'Vous conservez mes données depuis huit ans alors que je ne suis plus client depuis 2019. Merci de les supprimer, elles ne sont plus nécessaires au regard des finalités pour lesquelles vous les aviez collectées.'),
  (39, 'Letter', 'recue', 44, 'Diallo', 'Fatou', 'fatou.diallo@example.com', true, 'Access', 'Completed', 3, '15:58', 'Madame, Monsieur,

D''où viennent les informations que vous avez sur moi ? Je ne me souviens pas vous les avoir communiquées.

Cordialement,
Fatou Diallo'),
  (40, 'Email', 'recue', 4, 'Rivière', 'Pauline', 'pauline.riviere@example.org', false, 'Rectification', 'InProgress', 0, '13:05', 'Ma date de naissance est fausse dans mon espace client : vous avez 1987 au lieu de 1978.'),
  (41, 'Email', 'recue', 10, NULL, NULL, 'k.dev41@example.net', true, 'Objection', 'InProgress', 0, '11:58', 'Ne m''envoyez plus de publicité, ni par courriel ni par SMS. Vous pouvez conserver mon compte client, je continue à utiliser vos services.'),
  (42, 'Email', 'recue', 8, 'Muller', 'Sarah', 'sarah.muller@example.com', true, 'Erasure', 'InProgress', 1, '10:34', 'Mon fils a ouvert un compte sur votre site alors qu''il a douze ans. Je demande la suppression de l''ensemble des données le concernant.'),
  (43, 'Letter', 'echeance', -6, 'Mercier', 'Chloé', NULL, true, 'Rectification', 'InProgress', 3, '12:00', 'Madame, Monsieur,

Vous m''avez classé comme professionnel alors que je suis un particulier. Merci de corriger cette information.

Cordialement,
Chloé Mercier'),
  (44, 'Email', 'recue', 19, 'Boyer', 'Josée', 'josee.boyer@example.net', false, 'Access', 'InProgress', 0, '12:47', 'Bonjour, j''aimerais savoir quelles informations vous avez sur moi. Pouvez-vous m''en envoyer la liste ?'),
  (45, 'Email', 'recue', 13, NULL, NULL, 'lolo.4445@example.com', true, 'Rectification', 'InProgress', 0, '16:47', 'bonjour mon numero a changer merci de le metre a jour cest le 06 00 00 00 00 maintenant'),
  (46, 'Email', 'recue', 14, NULL, NULL, 'chat.noir46@example.org', true, 'Restriction', 'InProgress', 0, '16:05', 'Le traitement me paraît illicite, mais je m''oppose à l''effacement de mes données : j''en ai besoin. Je demande donc leur limitation.'),
  (47, 'Email', 'echeance', 7, 'Aubert', 'Thérèse', 'therese.aubert@example.net', true, 'Erasure', 'InProgress', 0, '10:20', 'Je retire le consentement que j''avais donné au traitement de mes données à des fins d''analyse comportementale. Par conséquent, je vous demande de les effacer.'),
  (48, 'Letter', 'recue', 2, 'Leroy', 'Thomas', NULL, false, 'Access', 'InProgress', 1, '12:00', 'Madame, Monsieur,

Qui a eu accès à mes données ces douze derniers mois ? Merci de me communiquer la liste des destinataires.

Cordialement,
Thomas Leroy'),
  (49, 'Email', 'recue', 13, 'Blanchard', 'Zoé', 'zoe.blanchard@example.org', false, 'Restriction', 'InProgress', 1, '13:47', 'J''ai besoin de ces éléments comme preuve pour mon dossier aux prud''hommes, ne les effacez pas mais cessez de les exploiter.'),
  (50, 'Letter', 'recue', 5, 'Da Silva', 'Arthur', 'arthur.dasilva@example.net', true, 'Erasure', 'InProgress', 2, '11:34', 'Madame, Monsieur,

Je ne veux plus apparaître nulle part chez vous. Effacez tout.

Cordialement,
Arthur Da Silva'),
  (51, 'Email', 'recue', 117, 'Roche', 'Denis', 'denis.roche@example.com', false, 'Objection', 'Cancelled', 0, '11:00', 'bonjour je ne veut plus recevoir vos offre par courrier merci de me retirer de vos liste de pub'),
  (52, 'Letter', 'recue', 141, 'Brémond', 'Loïc', NULL, true, 'Objection', 'Completed', 1, '08:05', 'Madame, Monsieur,

Merci de ne plus transmettre mes coordonnées à vos partenaires commerciaux.

Cordialement,
Loïc Brémond'),
  (53, 'Email', 'recue', 63, NULL, NULL, 'ex.abonne53@example.net', false, 'Rectification', 'Completed', 0, '10:20', 'Vous écrivez mon nom « Duran » alors que c''est « Durand », avec un D final. Merci de corriger.'),
  (54, 'Email', 'recue', 133, 'Moreau', 'Anaïs', 'anais.moreau@example.com', true, 'Restriction', 'Completed', 0, '16:20', 'En application de l''article 18 du RGPD, je demande la limitation du traitement de mes données le temps que vous vérifiiez l''exactitude des informations que je conteste.'),
  (55, 'Email', 'echeance', -20, NULL, NULL, 'petitpois55@example.org', false, 'Objection', 'InProgress', 1, '12:20', 'Je ne souhaite plus que mes achats servent à me proposer des recommandations personnalisées.'),
  (56, 'Letter', 'recue', 89, 'Fontaine', 'Inès', NULL, false, 'Access', 'Cancelled', 1, '12:41', 'Madame, Monsieur,

Je vous prie de me transmettre copie de mon dossier client, y compris les échanges enregistrés avec votre service client et les annotations internes me concernant.

Cordialement,
Inès Fontaine'),
  (57, 'Email', 'recue', 17, 'Renaud', 'Mathieu', 'mathieu.renaud@example.com', true, 'Erasure', 'InProgress', 0, '16:47', 'bonjour je veut que vous effacer tout ce que vous avez sur moi partout merci'),
  (58, 'Email', 'recue', 7, 'Collet', 'Rémi', 'remi.collet@example.org', false, 'Objection', 'InProgress', 1, '09:34', 'Conformément à l''article 21.2 du règlement (UE) 2016/679, je m''oppose au traitement de mes données à caractère personnel à des fins de prospection commerciale.'),
  (59, 'Email', 'echeance', 5, NULL, NULL, 'pseudo.anonyme59@example.net', false, 'Erasure', 'InProgress', 0, '12:34', 'Je souhaite supprimer mon compte et que vous effaciez toutes les données qui y sont rattachées.'),
  (60, 'Email', 'recue', 5, NULL, NULL, 'marcheur.du.dimanche60@example.com', false, 'Erasure', 'Cancelled', 0, '11:47', 'Le traitement de mes données de géolocalisation est dépourvu de base légale. Je vous demande leur effacement sans délai.'),
  (61, 'Email', 'echeance', -2, NULL, NULL, 'contact.perso61@example.org', true, 'Access', 'InProgress', 1, '15:41', 'Salut, vous avez quoi comme infos sur moi dans vos fichiers ? J''aimerais bien voir ce que vous stockez.'),
  (62, 'Letter', 'recue', 11, 'Lambert', 'Manon', 'manon.lambert@example.net', true, 'Restriction', 'InProgress', 1, '16:58', 'Madame, Monsieur,

Mettez mon dossier en pause le temps qu''on règle ça, mais gardez tout, hein.

Cordialement,
Manon Lambert'),
  (63, 'Email', 'recue', 2, 'Haddad', 'Yasmine', 'yasmine.haddad@example.com', false, 'Portability', 'InProgress', 0, '17:12', 'Est-il possible de récupérer les photos et messages que j''ai déposés sur la plateforme, dans un format exploitable ailleurs ?'),
  (64, 'Email', 'recue', 10, 'Le Goff', 'Maël', 'mael.legoff@example.org', false, 'Restriction', 'InProgress', 0, '15:20', 'Je conteste le montant d''impayé que vous m''attribuez. Tant que ce n''est pas vérifié, merci de ne pas transmettre ces données à des tiers, sans pour autant les effacer.'),
  (65, 'Letter', 'echeance', 4, 'Dumas', 'Maxime', 'maxime.dumas@example.net', false, 'Restriction', 'InProgress', 2, '14:47', 'Madame, Monsieur,

Suspendez tout usage de mes données jusqu''à ce que vous m''ayez répondu sur leur origine.

Cordialement,
Maxime Dumas'),
  (66, 'Email', 'recue', 15, 'Robin', 'Louis', 'louis.robin@example.com', false, 'Objection', 'InProgress', 0, '10:05', 'Je m''oppose, pour des raisons tenant à ma situation particulière, au traitement de mes données fondé sur votre intérêt légitime.'),
  (67, 'Email', 'recue', 7, 'Lopez', 'Clara', 'clara.lopez@example.org', true, 'Rectification', 'InProgress', 1, '10:58', 'Mon statut marital indiqué chez vous n''est plus exact, je suis divorcé depuis 2024.'),
  (68, 'Letter', 'recue', 165, 'Dupré', 'Hélène', NULL, false, 'Access', 'Completed', 3, '13:00', 'Madame, Monsieur,

Je souhaite exercer mon droit d''accès. Merci de m''indiquer l''origine des informations que vous détenez sur moi et la durée de conservation prévue.

Cordialement,
Hélène Dupré'),
  (69, 'Email', 'recue', 146, NULL, NULL, 'tonton.jo69@example.com', true, 'Access', 'Cancelled', 0, '09:41', 'Je voudrais connaître le score que votre algorithme m''a attribué et sur quelles données il s''appuie.'),
  (70, 'Email', 'recue', 124, 'Fournier', 'Lucas', 'lucas.fournier@example.org', false, 'Restriction', 'Completed', 1, '09:05', 'Merci de geler l''utilisation de mon dossier en attendant le résultat de ma réclamation.'),
  (71, 'Email', 'recue', 47, 'Vidal', 'Adèle', 'adele.vidal@example.net', true, 'Erasure', 'Cancelled', 0, '13:12', 'Merci de supprimer la photo de profil associée à mon compte, je souhaite qu''elle ne figure plus sur votre plateforme.'),
  (72, 'Email', 'echeance', 4, 'Brun', 'Lina', 'lina.brun@example.com', true, 'Restriction', 'InProgress', 0, '15:20', 'bonjour merci de plus utiliser mes donné mais de les garder jusqua ce que mon avocat vous ecrive'),
  (73, 'Email', 'recue', 93, NULL, NULL, 'cuisine.maison73@example.org', true, 'Objection', 'Completed', 1, '13:05', 'Cessez d''utiliser mon adresse pour vos campagnes marketing ; je reste par ailleurs client chez vous.'),
  (74, 'Email', 'recue', 18, NULL, NULL, 'plume.bleue74@example.net', false, 'Rectification', 'InProgress', 0, '17:20', 'J''ai déménagé le mois dernier, merci de mettre à jour mon adresse : 12 avenue Victor Hugo, 33000 Bordeaux.'),
  (75, 'Letter', 'recue', 11, 'Durand', 'Sophie', 'sophie.durand@example.com', true, 'Access', 'InProgress', 1, '17:12', 'Madame, Monsieur,

Madame, Monsieur, conformément à l''article 15 du règlement (UE) 2016/679, je vous demande de me communiquer une copie de l''ensemble des données à caractère personnel me concernant que vous traitez, ainsi que les finalités de ce traitement et les destinataires de ces données. Je vous prie d''agréer mes salutations distinguées.

Cordialement,
Sophie Durand'),
  (76, 'Email', 'recue', 85, 'Henry', 'Antoine', 'antoine.henry@example.org', false, 'Erasure', 'Completed', 1, '10:34', 'En application de l''article 17 du règlement (UE) 2016/679, je vous demande de procéder à l''effacement de l''ensemble des données à caractère personnel me concernant, ainsi qu''à leur suppression chez vos éventuels sous-traitants.'),
  (77, 'Letter', 'recue', 22, 'Lacroix', 'Anne-Sophie', 'anne-sophie.lacroix@example.net', false, 'Portability', 'Completed', 3, '14:12', 'Madame, Monsieur,

Je pars chez un concurrent, envoyez-moi mes données en JSON pour que je puisse tout reprendre là-bas.

Cordialement,
Anne-Sophie Lacroix'),
  (78, 'Email', 'echeance', 3, 'Baron', 'Gérard', 'gerard.baron@example.com', true, 'Objection', 'InProgress', 0, '17:12', 'Je refuse que mes données servent à du profilage publicitaire. Le reste du service peut continuer normalement.'),
  (79, 'Email', 'recue', 7, 'Petit', 'Nicolas', 'nicolas.petit@example.org', true, 'Restriction', 'InProgress', 1, '11:47', 'Ne vous servez plus de mes données pour l''instant, mais ne les supprimez pas : j''en aurai besoin.'),
  (80, 'Email', 'recue', 26, 'Lemaître', 'Benoît', 'benoit.lemaitre@example.net', true, 'Erasure', 'Completed', 1, '16:12', 'Retirez-moi de vos fichiers, je ne veux plus rien avoir à faire avec vous.'),
  (81, 'Letter', 'echeance', -4, 'Schmitt', 'Mathis', NULL, false, 'Access', 'InProgress', 3, '17:20', 'Madame, Monsieur,

Pourriez-vous m''expliquer pourquoi vous conservez mon numéro de téléphone et sur quelle base légale ?

Cordialement,
Mathis Schmitt'),
  (82, 'Letter', 'recue', 68, 'Prévost', 'Solène', NULL, false, 'Portability', 'Cancelled', 3, '17:34', 'Madame, Monsieur,

Je vous demande de transmettre directement mes données à la société ExempleBanque, conformément au paragraphe 2 de l''article 20.

Cordialement,
Solène Prévost'),
  (83, 'Letter', 'echeance', -110, 'Faure', 'Noémie', 'noemie.faure@example.net', false, 'Rectification', 'InProgress', 1, '09:34', 'Madame, Monsieur,

Les données de revenus figurant dans mon dossier sont inexactes et incomplètes. Je vous demande de les rectifier et de les compléter au vu de la déclaration jointe.

Cordialement,
Noémie Faure'),
  (84, 'Email', 'recue', 5, 'Mansouri', 'Aïcha', 'aicha.mansouri@example.com', true, 'Access', 'InProgress', 0, '17:05', 'bonjour je veut voire mon dossier chez vous et tout ce que vous savez de moi merci davance'),
  (85, 'Letter', 'recue', 51, 'Perrot', 'Aurélien', NULL, true, 'Rectification', 'Completed', 1, '13:41', 'Madame, Monsieur,

Vous avez encore mon ancien mail. Le bon c''est prenom.nom@exemple.invalid, changez-le s''il vous plaît.

Cordialement,
Aurélien Perrot'),
  (86, 'Email', 'recue', 12, 'Roux', 'Julie', 'julie.roux@example.net', false, 'Erasure', 'InProgress', 1, '16:00', 'Vous conservez mes données depuis huit ans alors que je ne suis plus client depuis 2019. Merci de les supprimer, elles ne sont plus nécessaires au regard des finalités pour lesquelles vous les aviez collectées.'),
  (87, 'Email', 'recue', 17, 'Le Bihan', 'Ewen', 'ewen.lebihan@example.com', true, 'Access', 'InProgress', 1, '08:00', 'D''où viennent les informations que vous avez sur moi ? Je ne me souviens pas vous les avoir communiquées.'),
  (88, 'Letter', 'echeance', -35, 'Marchand', 'Paul', 'paul.marchand@example.org', false, 'Access', 'InProgress', 2, '14:47', 'Madame, Monsieur,

Bonjour, j''aimerais savoir quelles informations vous avez sur moi. Pouvez-vous m''en envoyer la liste ?

Cordialement,
Paul Marchand'),
  (89, 'Email', 'recue', 9, 'Girard', 'Clément', 'clement.girard@example.net', false, 'Objection', 'InProgress', 0, '09:58', 'Je m''oppose à l''utilisation de mes données à des fins de recherche statistique, en application de l''article 21.6.'),
  (90, 'Email', 'recue', 3, 'Carpentier', 'Théo', 'theo.carpentier@example.com', true, 'Erasure', 'InProgress', 0, '14:58', 'Mon fils a ouvert un compte sur votre site alors qu''il a douze ans. Je demande la suppression de l''ensemble des données le concernant.'),
  (91, 'Letter', 'echeance', 6, 'Hamon', 'Cédric', 'cedric.hamon@example.org', true, 'Portability', 'InProgress', 2, '17:05', 'Madame, Monsieur,

bonjour je voudrai recuperer mes donné pour les mettre chez un autre site est ce possible en fichier exel

Cordialement,
Cédric Hamon'),
  (92, 'Email', 'recue', 12, 'Rousseau', 'Célia', 'celia.rousseau@example.net', true, 'Access', 'Cancelled', 1, '17:05', 'Qui a eu accès à mes données ces douze derniers mois ? Merci de me communiquer la liste des destinataires.'),
  (93, 'Email', 'recue', 20, 'Kervella', 'Pierre-Yves', 'pierre-yves.kervella@example.com', false, 'Access', 'InProgress', 0, '11:58', 'Je vous prie de me transmettre copie de mon dossier client, y compris les échanges enregistrés avec votre service client et les annotations internes me concernant.'),
  (94, 'Email', 'recue', 18, 'Besnard', 'Stéphane', 'stephane.besnard@example.org', false, 'Portability', 'InProgress', 0, '14:47', 'Je change d''opérateur. Pouvez-vous m''envoyer mes données dans un fichier que je puisse réimporter chez eux ?'),
  (95, 'Email', 'echeance', 5, NULL, NULL, 'lolo.4495@example.net', false, 'Access', 'InProgress', 1, '13:47', 'Salut, vous avez quoi comme infos sur moi dans vos fichiers ? J''aimerais bien voir ce que vous stockez.'),
  (96, 'Email', 'recue', 31, 'Meunier', 'Joël', 'joel.meunier@example.com', false, 'Rectification', 'Completed', 0, '13:00', 'Conformément à l''article 16 du règlement (UE) 2016/679, je vous demande de rectifier mon adresse postale, erronée dans vos fichiers. L''adresse exacte est le 4 rue des Lilas, 69003 Lyon.'),
  (97, 'Email', 'echeance', -1, 'Chevalier', 'Gaëlle', 'gaelle.chevalier@example.org', true, 'Erasure', 'InProgress', 0, '13:47', 'Je retire le consentement que j''avais donné au traitement de mes données à des fins d''analyse comportementale. Par conséquent, je vous demande de les effacer.'),
  (98, 'Email', 'recue', 152, 'Lucas', 'Margaux', 'margaux.lucas@example.net', true, 'Rectification', 'Completed', 1, '16:47', 'Ma date de naissance est fausse dans mon espace client : vous avez 1987 au lieu de 1978.'),
  (99, 'Email', 'echeance-au-plus-tot', 0, NULL, NULL, 'plume.bleue99@example.com', false, 'Restriction', 'InProgress', 1, '16:20', 'Le traitement me paraît illicite, mais je m''oppose à l''effacement de mes données : j''en ai besoin. Je demande donc leur limitation.'),
  (100, 'Email', 'recue', 3, NULL, NULL, 'lecteur.curieux100@example.org', true, 'Erasure', 'InProgress', 0, '09:05', 'Je ne veux plus apparaître nulle part chez vous. Effacez tout.')

),
visees AS (
  SELECT demandes.*,
         (:'aujourdhui'::date + jours - interval '1 month')::date AS un_mois_avant_l_echeance
  FROM demandes
),
datees AS (
  SELECT visees.*,
         CASE ancrage
           WHEN 'recue' THEN :'aujourdhui'::date - jours
           WHEN 'echeance' THEN un_mois_avant_l_echeance
           -- Le jour visé est une fin de mois qu'aucune réception n'atteint : on prend le lendemain,
           -- premier du mois suivant, dont l'échéance tombe juste après.
           WHEN 'echeance-au-plus-tot' THEN
             CASE WHEN (un_mois_avant_l_echeance + interval '1 month')::date < :'aujourdhui'::date + jours
                  THEN un_mois_avant_l_echeance + 1
                  ELSE un_mois_avant_l_echeance
             END
         END AS recue_le
  FROM visees
)
INSERT INTO data_subject_requests
  (id, origin, received_on, response_deadline, last_name, first_name, email, identity_verified,
   message, data_subject_right, status, created_by, created_at, modified_by, modified_at)
SELECT format('5eed0000-0000-7000-8000-%s', lpad(n::text, 12, '0'))::uuid,
       origine,
       recue_le,
       (recue_le + interval '1 month')::date,
       nom,
       prenom,
       email,
       identite_verifiee,
       message,
       droit,
       statut,
       'operator',
       LEAST(((recue_le + delai_de_saisie) + heure_de_saisie::time) AT TIME ZONE 'Europe/Paris',
             :'maintenant'::timestamptz),
       NULL,
       NULL
FROM datees;
\endif

COMMIT;
