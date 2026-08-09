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
| `hash`, `token`, `password`, `salt`, `secret`, `api_key` | `AuthenticationSecret` | ⚠️ **Sauf secret de service** — `api_key` / `consumer_secret` d'un connecteur ne se rattachent à aucune personne → `Unflagged`. C'est le faux ami relevé par #137 sur Passerelle. |
| `email`, `mail`, `courriel`, `tel`, `phone`, `adresse`, `ville`, `cp` | `ContactDetails` | Coordonnées. |
| `code_postal`, `ville`, `pays` dans une **adresse postale** | `ContactDetails` | ⚠️ Arbitrage : `LocationData` est plus haut dans l'ordre, mais il vise la **géolocalisation** (GPS, traces, bornes), pas l'adresse déclarée. Une adresse postale est une coordonnée. |
| `ip`, `ip_address`, `user_agent`, `session_id`, `last_login` | `ConnectionData` | Données de connexion. |
| Colonne d'une table **entièrement de nomenclature** | `Unflagged` | § 3.4 — et elles restent au corpus, ce sont les vrais négatifs. |

⚠️ **Ce que ces conventions ne dispensent pas de faire** : tout cas qui n'y entre
pas se porte au tableau ci-dessous **avant** d'être tranché.

## Arbitrages

| Date | Schéma | Colonne(s) | Question | Décision | Remontée au § 3 ? |
|---|---|---|---|---|---|
| | | | | | |

<!--
Exemple de la forme attendue :
| 2026-08-12 | Dolibarr | llx_societe.tva_intra | Un numéro de TVA intracommunautaire
d'indépendant est-il un NationalIdentifier ? | ProfessionalLife — identifie
l'entreprise, pas la personne, et le § 3.6 fait déjà entrer l'indépendant par le
nom de la société. | oui, § 3.6 |
-->
