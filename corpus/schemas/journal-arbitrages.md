# Journal des arbitrages d'annotation

Tout cas qui n'entre dans **aucune** règle du § 3 du
[protocole](./PROTOCOLE-ANNOTATION.md) s'inscrit ici **avant** d'être tranché.

⚠️ L'ordre compte. Trancher puis noter produit un journal qui justifie après coup ;
noter puis trancher produit un journal qui **contraint** les colonnes suivantes,
ce qui est tout l'objet d'un protocole écrit d'avance.

Quand une famille se dégage de ce journal, elle **remonte en § 3 du protocole**
par un amendement daté, et **les colonnes déjà annotées sont repassées**.

## Conventions communes aux annotateurs — 2026-08-09

⚠️ **Tranchées avant l'annotation, et pour une raison précise** : les 3 254
colonnes sont réparties sur **quinze annotateurs** (amendement n° 2 du § 4). Le
corpus ne contient que 1 784 noms de colonne distincts, et les plus fréquents —
`name` (42×), `label` (34×), `code` (21×), `description` (23×) — traversent
jusqu'à sept schémas. Sans convention écrite, quinze annotateurs les trancheraient
de quinze façons, et le banc mesurerait ce désaccord-là plutôt que le moteur.

Ces conventions **dérivent** du § 3 et de l'ordre d'arbitrage ; elles n'ajoutent
aucune règle nouvelle, elles nomment l'application des règles existantes aux cas
fréquents. ⚠️ **Elles sont contestables, et les 300 colonnes de la référence
humaine sont exactement ce qui les conteste.**

| Famille | Étiquette | Fondement |
|---|---|---|
| `name`, `label`, `libelle`, `title`, `intitule` **dans une table de personnes** | `Identity` | § 3.2 — c'est le nom de la personne. |
| … **dans une table de nomenclature ou d'objets** (`c_pays`, `produit`, `glpi_configs`) | `Unflagged` | § 3.4 — la table ne porte aucune personne. |
| `code`, `ref`, `reference`, `slug` | `Unflagged` | Référence applicative. ⚠️ Sauf si le nom qualifie une personne (`code_adherent` → `Identity`). |
| `description`, `comment`, `commentaire`, `note`, `memo`, `observation` | selon le § 3.3 | Table de personnes → `PersonalDataUncategorised` ; table sans personne → `Unflagged` ; qualification explicite → la catégorie nommée. |
| `date`, `date_mod`, `date_creation` sans autre qualification | `Unflagged` | § 3.1 — horodatage d'écriture. ⚠️ `date_naissance` est `Identity`, la qualification prime. |
| `type`, `statut`, `status`, `etat`, `is_*`, `has_*` | `Unflagged` | § 3.1 — mécanique applicative. |
| `user_id`, `id_user`, `users_id`, `fk_user`, `prof_id`, `pid` | `Identity` | § 3.2 — FK désignant une personne, y compris un agent (§ 3.1). |
| `entities_id`, `is_recursive` (GLPI) | `Unflagged` | Cloisonnement multi-entités, pas une personne. |
| `token`, `password`, `salt`, `secret`, `api_key` | `AuthenticationSecret` | ⚠️ **Sauf secret de service** — `api_key` / `consumer_secret` d'un connecteur ne se rattachent à aucune personne → `Unflagged`. C'est le faux ami relevé par #137 sur Passerelle. |
| `hash`, `md5`, `checksum`, `fingerprint` | **selon le § 3.8** | ⚠️ **Amendée le 2026-08-09** — cette ligne rangeait toute empreinte en `AuthenticationSecret` ; c'était trop grossier. L'empreinte prend l'étiquette de sa source (§ 3.8), et une somme d'intégrité de fichier est `Unflagged`. |
| `email`, `mail`, `courriel`, `tel`, `phone`, `adresse`, `ville`, `cp` | `ContactDetails` | Coordonnées. |
| `code_postal`, `ville`, `pays` dans une **adresse postale** | `ContactDetails` | ⚠️ Arbitrage : `LocationData` est plus haut dans l'ordre, mais il vise la **géolocalisation** (GPS, traces, bornes), pas l'adresse déclarée. Une adresse postale est une coordonnée. |
| `ip`, `ip_address`, `user_agent`, `session_id`, `last_login` | `ConnectionData` | Données de connexion. |
| Colonne d'une table **entièrement de nomenclature** | `Unflagged` | § 3.4 — et elles restent au corpus, ce sont les vrais négatifs. |

⚠️ **Ce que ces conventions ne dispensent pas de faire** : tout cas qui n'y entre
pas se porte au tableau ci-dessous **avant** d'être tranché.

## Arbitrages

| Date | Schéma | Colonne(s) | Question | Décision | Remontée au § 3 ? |
|---|---|---|---|---|---|
| 2026-08-09 | tous (26 col.) | `hash`, `md5`, `checksum`, `fingerprint` | La convention ci-dessus range toute empreinte en `AuthenticationSecret`. Elle est trop grossière : les lots ont produit `Unflagged` (16), `AuthenticationSecret` (8), `ContactDetails` (2) sur la même famille, faute de règle. Une somme d'intégrité de fichier est-elle un secret ? | **Non.** Une empreinte prend l'étiquette de **ce dont elle est l'empreinte** : mot de passe → `AuthenticationSecret`, email ou téléphone → `ContactDetails`, fichier ou révision → `Unflagged`. Fondement : le condensat d'une donnée personnelle reste une donnée personnelle (pseudonymisation), mais le condensat d'un artefact ne le devient pas. Une colonne `hash` dont la table ne dit rien retombe au § 3.5 → `Unflagged`. | **oui, § 3.8** (nouveau) |
| 2026-08-09 | tous (31 col.) | `montant`, `amount`, `iban`, `bic`, `numero_compte`, `num_chq`, `solde`, `policy_number`, `fk_account`, `fk_bank` | Le § 3 ne comportait **aucune** règle `FinancialData`, alors que la taxonomie la porte. Les lots ont tranché `Unflagged` (17) contre `FinancialData` (14) sans fondement écrit. Un montant est-il financier en lui-même ? | **Rattachement exigé, sauf identifiant bancaire.** `FinancialData` demande un rattachement à une personne visible dans le schéma (table de personnes, ou FK vers une personne) ; un agrégat comptable non rattaché reste `Unflagged` ; un `iban` / `bic` / `numero_compte` est `FinancialData` **même sans FK**, il identifie son titulaire à lui seul. Le montant est retenu au même titre que l'IBAN : le montant d'un don ou d'une cotisation renseigne sur la situation de la personne. | **oui, § 3.9** (nouveau) |
| 2026-08-09 | tous (109 col.) | `comment`, `commentaire`, `note`, `description`, `memo`, `observation`, `contenu` | Le § 3.3 ne prévoyait que deux mondes : table de personnes, ou table sans personne. Il ne disait rien du cas le plus fréquent du corpus — un champ libre dans une **table d'objet qui porte une FK vers une personne** (ticket, rendez-vous, prêt). Les lots ont produit `Unflagged` (91), `PersonalDataUncategorised` (15), `HealthData` (3). | **`PersonalDataUncategorised`.** Le rattachement est visible dans le schéma : ce n'est pas un pari sur le contenu, c'est le constat qu'une note portée par un objet nominatif dit quelque chose de la personne nommée. ⚠️ Le § 3.3 cas 4 (qualification explicite, y compris le domaine que la table nomme sans ambiguïté) **prime** : un champ libre d'une table de dossier médical reste `HealthData`. Sans FK vers une personne → `Unflagged`, inchangé. | **oui, § 3.3 cas 2** (inséré, liste renumérotée) |

<!--
Exemple de la forme attendue :
| 2026-08-12 | Dolibarr | llx_societe.tva_intra | Un numéro de TVA intracommunautaire
d'indépendant est-il un NationalIdentifier ? | ProfessionalLife — identifie
l'entreprise, pas la personne, et le § 3.6 fait déjà entrer l'indépendant par le
nom de la société. | oui, § 3.6 |
-->
