# Schémas d'applications libres annotables : candidats, licences, volumétrie

**Ticket** : [#125](https://github.com/AmauryTISSOT/microservice_rgpd/issues/125), enfant de la carte
[#122](https://github.com/AmauryTISSOT/microservice_rgpd/issues/122).
**Date** : 2026-08-08. **Nature** : recherche. Ce document **ne construit pas le corpus** — il dit
lesquels, et à quelles conditions.

---

## 0. Ce que la recherche a établi, en une page

Neuf candidats ont été **clonés et mesurés**, pas seulement lus. Les chiffres qui suivent sortent
d'un comptage sur les fichiers DDL du dépôt amont, à la date du 8 août 2026. **Aucun chiffre de ce
document ne provient d'une source secondaire** : voir la note de méthode au § 1.

| Candidat | Licence | Tables | Colonnes | Langue des identifiants | Commentaires SQL |
| --- | --- | ---: | ---: | --- | ---: |
| [Dolibarr](https://github.com/Dolibarr/dolibarr) | GPL-3.0 | 413 | 5 314 | **Mixte FR/EN, dans la même table** | **0** |
| [PrestaShop](https://github.com/PrestaShop/PrestaShop) | OSL-3.0 (cœur) | 244 | 1 589 | Anglais, résidus FR (`siret`, `ape`) | 0 |
| [GLPI](https://github.com/glpi-project/glpi) | GPL-3.0 | 442 | 4 534 | Anglais strict | 10 |
| [OpenEMR](https://github.com/openemr/openemr) | GPL-3.0 | 282 | 3 877 | Anglais, abréviations médicales | **673** |
| [SACoche](https://forge.apps.education.fr/sesamath/sacoche) | AGPL-3.0 | 103 | 632 | **Français** | 8 *(niveau table)* |
| [Paheko](https://github.com/paheko/paheko) 0.8.0 → HEAD | AGPL-3.0 | 26 → 43 | 130 → 271 | **Français → anglais, même schéma** | 0 *(SQLite)* |
| [Galette](https://github.com/galette/galette) | GPL-3.0 | 31 | 194 | **Français strict** | 0 |
| [SuiteCRM](https://github.com/salesagility/SuiteCRM) | AGPL-3.0 | *aucun DDL versionné* | — | Anglais | — |
| [Odoo Community](https://github.com/odoo/odoo) | LGPL-3.0 | *aucun DDL versionné* | — | Anglais | — |
| *(témoin)* `temoin/db/schema.sql` | ce dépôt | 11 | 72 | Français propre | 0 |

**Trois résultats non attendus commandent tout le reste.**

1. **Le pivot que l'`Operator` collera n'aura presque jamais de commentaire.** La carte #122 décrit
   l'entrée comme « le recensement pivot des colonnes — table, colonne, type, **commentaire**,
   contraintes, produit par une requête `information_schema` ». Or `information_schema.columns.
   column_comment` ne contient que ce qu'une clause SQL `COMMENT '…'` y a écrit. Dolibarr porte
   **1 913 commentaires `--` en fin de ligne de colonne** dans ses fichiers `.sql` — et **zéro
   clause `COMMENT`**. Ces commentaires sont des commentaires *de code source* : ils disparaissent
   à l'installation. Un corpus fabriqué en lisant les fichiers DDL donnerait donc au moteur une
   information que la production ne lui donnera **jamais**. C'est le piège le plus coûteux de ce
   ticket, et il est invisible si l'on se contente de lire les fichiers.
   **Vérifié expérimentalement** (§ 1) : `llx_societe.sql` chargé dans MariaDB 11.8.8 rend un
   `column_comment` **vide** pour `nom`, `statut`, `code_client`, `prefix_comm`, `remise_client`
   et `tva_assuj` — alors que le fichier source porte `-- statut (deprecated)` sur l'une d'elles.
   Le même essai sur `database.sql` d'OpenEMR restitue bien `'Patient ID from patient_data'` et
   `'references users.id for session owner'`.
2. **Le chemin juridiquement le plus sûr est aussi le seul réaliste.** L'analyse de licence
   (§ 2) conclut que le risque résiduel se concentre sur les **commentaires DDL** — prose rédigée
   par un humain, plausiblement protégeable — et recommande l'extraction par **introspection
   `information_schema` sur une instance installée** plutôt que la copie des fichiers `.sql`.
   C'est exactement ce que le point 1 impose pour des raisons de réalisme. Les deux contraintes
   pointent dans la même direction : **installer dans un conteneur jetable, interroger
   `information_schema`, ne jamais recopier un fichier `.sql` amont.**
3. **La saleté qu'on cherchait n'est pas où on la cherchait.** Le ticket décrit `CLI_NOM_1`,
   `ZZ_TMP_OLD`. Le comptage des noms de tables suspects (`_tmp`, `_old`, `_bak`, `zz`) donne
   **2 tables sur 413** chez Dolibarr, 3 sur 442 chez GLPI. Les logiciels libres maintenus
   **nettoient leurs tables mortes** — c'est ce qui les distingue d'une base client. En revanche
   ils accumulent, et lourdement, une saleté d'un autre genre : **abréviations opaques**
   (`morphy`, `thm`, `tjm`, `cle_rib`, `ss`), **bilinguisme interne**, **colonnes marquées mortes
   sans être supprimées** (119 marqueurs `deprecated` / `not used` chez Dolibarr), **noms
   mensongers** (`llx_societe_rib.iban_prefix` contient l'IBAN entier), et **champs génériques
   fourre-tout** (`genericname1`/`genericval1`, `usertext1..17`, `llx_*_extrafields`). Cette
   saleté-là est **plus difficile** pour un détecteur que `ZZ_TMP_OLD`, qui est en réalité un cas
   facile : un nom qui crie qu'il est mort. **Le corpus doit viser la saleté sémantique, pas la
   saleté typographique.**
   *Une exception, et une seule* : `sacoche_user.ID_NATIONAL` — une colonne en **majuscules** au
   milieu d'une table snake_case française (§ 3.6). C'est le seul `CLI_NOM_1` authentique du
   dossier, et il a fallu aller chercher un logiciel scolaire français pour le trouver.

**Recommandation** : **six schémas de banc** — Dolibarr, SACoche, Paheko (trois états), OpenEMR,
GLPI, Galette — plus `temoin/db/` en test de fumée, et PrestaShop en réserve conditionnelle.
Détail et justification en § 4.

---

## 1. Méthode

### Ce qui a été fait

Clonage `--depth 1` des dépôts amont, puis comptage par analyse syntaxique des blocs
`CREATE TABLE` (script *ad hoc*, non versionné : ce ticket ne produit pas d'outillage). Les
chiffres portent sur le `HEAD` de la branche par défaut au 8 août 2026. Pour chaque schéma :
comptage des tables et colonnes, relevé des marqueurs de sédimentation
(`deprecated`, `obsolete`, `not used`, `todo`), comptage des clauses SQL `COMMENT`, détection des
noms de tables suspects, et repérage des tokens français dans les noms de colonnes contre un
lexique fermé.

**Une vérification a été faite en conditions réelles**, et c'est celle qui commande le § 0.1 : un
conteneur **MariaDB 11.8.8** jetable, chargement de `llx_societe.sql` (Dolibarr) puis de
`database.sql` (OpenEMR), et interrogation de `information_schema.columns`. Le manuel MySQL
définit `COLUMN_COMMENT` comme « Any comment included in the column definition »
([doc](https://dev.mysql.com/doc/refman/8.4/en/information-schema-columns-table.html)) — ce qui
désigne la clause SQL `COMMENT`, pas les commentaires `--` du fichier. L'essai le confirme :
Dolibarr rend six `column_comment` vides sur six, OpenEMR rend ses commentaires intacts.

### Règle de provenance

**Tout chiffre, tout nom de colonne et toute licence cités dans ce document ont été produits par
une commande exécutée pour ce ticket sur un clone local**, et non repris d'un résumé, d'une page
de documentation ou d'un rapport intermédiaire. Les licences ont été lues dans le fichier
`LICENSE`/`COPYING` du dépôt cloné, jamais déduites du champ affiché par GitHub — l'écart est
réel : PrestaShop est classé `NOASSERTION` par l'API GitHub alors que `LICENSE.md` dit OSL-3.0,
et Odoo est classé `NOASSERTION` alors que `LICENSE` dit LGPLv3.

Cette règle n'est pas une formalité de style. Une exploration parallèle de candidats francophones
a produit, au cours de ce ticket, un rapport détaillé — URLs, licences, comptages de tables et de
colonnes, exemples de noms de colonnes — qui s'est révélé **entièrement inventé** : les projets
existent pour la plupart, aucune des affirmations les concernant n'était vérifiable. Ce rapport a
été **intégralement écarté**. Les deux candidats francophones ajoutés depuis — SACoche et Paheko —
l'ont été après clonage et lecture directe, et **aucun autre candidat de cette exploration n'a été
retenu**, y compris ceux qui semblaient les plus prometteurs (§ 6).

### Ce qui n'a pas été fait, et pourquoi

**Aucun taux de colonnes personnelles n'a été mesuré au sens strict.** Le § 3 donne, pour chaque
candidat, un chiffre issu d'une **heuristique par mots-clés** — précisément la ligne de base
naïve que le moteur de la carte #122 doit battre. Ce chiffre est un **majorant grossier**, avec
des faux positifs massifs et faciles à exhiber : le comptage marque `llx_c_departements.nom`
(un nom de département, pas de personne), `glpi_authldaps.name` (un nom de serveur LDAP),
`PREFIX_orders.payment` (un mode de règlement), `llx_fichinterdet_rec.tva_tx` (un taux de TVA
attrapé par le motif `tva`). **Il ne faut jamais présenter ces pourcentages comme une vérité
terrain** : ils disent seulement où la matière se trouve, à un ordre de grandeur près, et
combien la ligne de base naïve sur-déclenche. La vérité terrain est le travail d'annotation, qui
n'est pas dans ce ticket.

Nuance importante : sur ces schémas, l'heuristique naïve marque **8 à 13 % des colonnes**. Un
détecteur qui rendrait ce taux sans rien comprendre passerait pour crédible. C'est un argument
pour que le banc mesure la **précision par catégorie**, pas un taux global.

---

## 2. La question bloquante : peut-on verser ces schémas ?

Cette section résume une recherche juridique menée en sources primaires. **Ce n'est pas un avis
juridique.** Elle sert à trancher une décision d'ingénierie, pas à couvrir un risque.

### 2.1 Deux droits, un seul pertinent

- **Directive 96/9/CE, art. 7(1)** : le droit *sui generis* du fabricant de base de données
  interdit l'extraction d'une partie substantielle **du contenu**. Un schéma n'extrait aucun
  contenu — aucune ligne, aucune valeur. **Le droit sui generis est hors-jeu.**
  ([texte](https://eur-lex.europa.eu/legal-content/FR/TXT/HTML/?uri=CELEX:31996L0009))
- **Directive 2009/24/CE, art. 1(2)** : « les idées et principes qui sont à la base de quelque
  élément que ce soit d'un programme d'ordinateur, **y compris ceux qui sont à la base de ses
  interfaces**, ne sont pas protégés ». L'art. 5(3) autorise l'utilisateur habilité à « observer,
  étudier ou tester le fonctionnement » du programme pour en déterminer les idées et principes,
  **sans autorisation**.
  ([texte](https://eur-lex.europa.eu/legal-content/FR/TXT/HTML/?uri=CELEX:32009L0024))

### 2.2 L'arrêt décisif

**CJUE C-406/10, *SAS Institute v World Programming*, 2 mai 2012**, point 39 : « ni la
fonctionnalité d'un programme d'ordinateur ni le langage de programmation **et le format de
fichiers de données** utilisés […] ne constituent une forme d'expression de ce programme ».
Point 40 : protéger la fonctionnalité reviendrait à « monopoliser les idées, au détriment du
progrès technique ».
([arrêt](https://eur-lex.europa.eu/legal-content/FR/TXT/HTML/?uri=CELEX:62010CJ0406))

Un schéma relationnel est l'analogue direct d'un « format de fichiers de données » : la
description structurelle du conteneur que le programme exploite. **L'analogie est forte ; elle
reste une analogie** — *SAS* portait sur des formats propriétaires, pas sur du DDL SQL, et aucune
décision de l'UE ne porte spécifiquement sur l'extraction d'un schéma relationnel.

Le seuil d'originalité vient de **CJUE C-604/10, *Football Dataco*, 1er mars 2012** : il faut des
« choix libres et créatifs » et une « touche personnelle ». Le travail et le savoir-faire, même
considérables, ne suffisent pas. Un schéma normalisé, avec ses conventions de nommage et ses
types imposés par le SGBD, est largement dicté par sa fonction.
([arrêt](https://eur-lex.europa.eu/legal-content/FR/TXT/HTML/?uri=CELEX:62010CJ0604))

### 2.3 Ce que disent les licences elles-mêmes

- **GPLv3 / FAQ de la FSF** : « **The output of a program is not, in general, covered by the
  copyright on the code of the program.** » L'exception est la sortie qui **contient** une copie
  substantielle du programme.
  ([FAQ, `#GPLOutput`](https://www.gnu.org/licenses/gpl-faq.en.html#GPLOutput)) La ligne de partage
  opérationnelle n'est donc pas « quel outil » mais « **combien de texte source original se
  retrouve dans le résultat** ». Un `mysqldump --no-data` recopie la DDL telle qu'elle est écrite ;
  une requête `information_schema` produit une table de faits.
- **AGPLv3 (SuiteCRM)** : le § 13 se déclenche sur deux conditions cumulatives — vous **modifiez**
  le Programme, **et** des utilisateurs interagissent avec lui **à distance**. Un dépôt Git de
  fichiers statiques ne remplit ni l'une ni l'autre. **Pour un corpus statique, AGPLv3 ≡ GPLv3.**
  ([texte](https://opensource.org/license/agpl-v3))
- **OSL 3.0 (PrestaShop)** : c'est bien du copyleft. Le § 1(c) impose que toute copie ou œuvre
  dérivée distribuée soit relicenciée sous OSL ; le **§ 5 « External Deployment »** assimile à une
  distribution tout usage rendant l'œuvre accessible à un tiers, y compris par le réseau — c'est
  l'équivalent OSL du § 13 de l'AGPL, en plus large.
  ([texte](https://opensource.org/license/osl-3-0-php)) La FSF déclare OSL
  **incompatible avec la GPL** : « The Open Software License is a free software license. It is
  incompatible with the GNU GPL in several ways. »
  ([license-list](https://www.gnu.org/licenses/license-list.html#OSL))
  ⚠️ **PrestaShop est donc le candidat où le risque est le plus élevé** : c'est le seul dont la
  licence est déclarée incompatible avec celle de la majorité du corpus, et le § 5 mord sur un
  déploiement réseau. Le copyleft ne s'accroche à rien si le schéma n'est pas protégeable — mais
  c'est là qu'on est le plus dépendant de cette hypothèse.

### 2.4 La pratique établie

Les corpus académiques de schémas relationnels publient des schémas de logiciels et de sources
réels sous licence permissive ou CC, sans autorisation des éditeurs, et sans contentieux connu :
[Spider](https://yale-lily.github.io/spider) (200 bases, Yale) et
[BIRD](https://bird-bench.github.io/) (95 bases réelles, avec un `column_meaning.json` de
descriptions de colonnes) sont sous **CC BY-SA 4.0** ;
[WikiSQL](https://github.com/salesforce/WikiSQL) sous BSD-3-Clause. Précédent d'un titulaire :
Oracle publie `sakila-schema.sql` sous **New BSD** alors que MySQL est sous GPL
([licence Sakila](https://dev.mysql.com/doc/sakila/en/sakila-license.html)).

Noter que Spider et BIRD choisissent **CC BY-SA**, pas MIT. C'est un choix prudent qui limite
l'écart de permissivité, et c'est le précédent à imiter.

### 2.5 Le vrai point de risque : les commentaires

| Couche | Nature | Protégeable ? |
| --- | --- | --- |
| Noms de tables et colonnes, types, contraintes | Interface, largement dictée par la fonction | Très improbable (*SAS* pt 39-42, *Football Dataco*) |
| Structure d'ensemble (choix et disposition) | Éventuellement originale | Improbable, pas exclu (dir. 96/9 art. 3(1)) |
| **Commentaires DDL** | **Prose rédigée par un humain, libre dans sa formulation** | **Plausiblement protégeable** |

Un commentaire est exactement ce qui échappe à la contrainte technique : deux développeurs
décrivant la même colonne écriront deux phrases différentes. *SAS Institute* renvoie d'ailleurs
au point 43 les formats **décrits dans un manuel** vers la protection de droit commun en tant
qu'œuvre littéraire.

### 2.6 Conditions du versement — la décision

**Le versement d'un schéma structurel issu d'un logiciel GPLv3 / AGPLv3 / OSL 3.0 dans ce dépôt
est défendable**, sous les conditions suivantes, par ordre d'efficacité :

1. **Extraire par introspection `information_schema`, jamais en copiant un fichier `.sql`
   amont.** Cette condition est à la fois la mesure juridique la plus efficace (art. 5(3),
   `#GPLOutput`) et la seule qui produise un corpus **fidèle à ce que l'`Operator` collera**.
   Elle est **non négociable**.
2. **N'admettre dans le corpus que les commentaires que `column_comment` rend** — c'est-à-dire
   ceux issus d'une clause SQL `COMMENT`. Cela exclut mécaniquement les 1 913 commentaires `--`
   de Dolibarr, dont la protégeabilité est la plus plausible du lot. Pour les 673 commentaires
   `COMMENT` d'OpenEMR, deux options : les garder en assumant le risque résiduel (ils sont pour
   la plupart brefs et banals : `'created time'`, `'Amendment ID'`), ou les remplacer par une
   reformulation écrite ici. **Décision à prendre au ticket de construction du corpus, pas ici.**
3. **Normaliser vers le pivot du service** — les colonnes de la requête `information_schema` que
   #122 fournira, dans notre ordre et nos types canoniques. La disposition résultante est la
   nôtre.
4. **Attribuer systématiquement** : dépôt amont, tag de version exact, licence d'origine, date
   d'extraction, commande d'extraction. Ce n'est pas une exigence juridique si rien n'est protégé ;
   c'est ce qui désamorce le grief réputationnel, qui est en pratique le risque le plus probable,
   bien avant le contentieux.
5. **Licencier le corpus en CC BY-SA 4.0**, à la manière de Spider et BIRD, et **non** sous la
   licence du code du dépôt. Une licence logicielle s'applique mal à des données ; CC0 serait la
   posture la moins prudente sur une matière dont l'origine n'est pas certaine.
6. **Verser le script d'extraction et les `Dockerfile`/`compose` de reconstruction**, pour qu'un
   tiers puisse refaire le corpus depuis les sources amont sans nous faire confiance.

**Ce qui est explicitement écarté** : copier `install-dev/data/db_structure.sql` de PrestaShop, ou
tout fichier `.sql` amont, dans ce dépôt. C'est le seul scénario où le risque devient sérieux.

---

## 3. Les candidats, un par un

### 3.1 Dolibarr — le pilier français

**Licence** : GPL-3.0, vérifiée dans
[`COPYING`](https://github.com/Dolibarr/dolibarr/blob/develop/COPYING) (texte de la GNU GPL
version 3, 29 juin 2007) et confirmée par l'en-tête de chaque fichier DDL :
« either version 3 of the License, or (at your option) any later version ».

**Extraction sans déployer** : les tables sont versionnées **une par fichier** dans
[`htdocs/install/mysql/tables/`](https://github.com/Dolibarr/dolibarr/tree/develop/htdocs/install/mysql/tables)
— 755 fichiers, dont 413 `CREATE TABLE` et 342 `*.key.sql` qui ne portent que les index et les
clés étrangères. Les migrations vivent dans
[`htdocs/install/mysql/migration/`](https://github.com/Dolibarr/dolibarr/tree/develop/htdocs/install/mysql/migration),
avec **32 fichiers de `3.0.0-3.1.0.sql` à `24.0.0-25.0.0.sql`**. C'est le candidat le plus facile
à lire — mais, conformément au § 2.6, **c'est par un conteneur MariaDB jetable et une requête
`information_schema` qu'il faut passer**, sans quoi on hérite des 1 913 commentaires `--` que la
production n'a pas.

**Volumétrie** : **413 tables, 5 314 colonnes**. Heuristique naïve : **455 colonnes marquées
(8,6 %)**, dont 137 contact, 123 bancaire, 82 identifiant national, 64 identité, 42 connexion,
7 naissance. **Zéro santé, zéro infraction.**

**Langue des identifiants — le point décisif.** Dolibarr est le seul candidat **franchement
bilingue à l'intérieur d'une même table**. `llx_societe` mêle sur 90 colonnes : `nom`,
`code_client`, `code_fournisseur`, `code_compta`, `fk_pays`, `fk_departement`, `fk_forme_juridique`,
`remise_client`, `mode_reglement`, `cond_reglement`, `tva_assuj`, `note_private`, `prefix_comm`,
`fournisseur`, `client` — à côté de `name_alias`, `zip`, `town`, `phone_mobile`, `email`,
`accountancy_code_customer_general`, `outstanding_limit`, `webservices_key`. Les **noms de tables**
sont massivement français : `llx_facture`, `llx_commande`, `llx_societe`, `llx_adherent`,
`llx_entrepot`, `llx_deplacement`, `llx_prelevement`, `llx_paiement`, `llx_projet`, `llx_fichinter`,
`llx_propal`, `llx_contrat`, `llx_don`, `llx_chargesociales`. Comptage : **397 colonnes sur 5 314
(7,5 %) portent au moins un token français strict**, ce qui sous-estime largement le phénomène
puisque le lexique employé est volontairement étroit.

**Âge et sédimentation.** Le dépôt GitHub date de
[juin 2011](https://github.com/Dolibarr/dolibarr), mais les en-têtes de copyright des fichiers DDL
remontent à **2000** (6 fichiers), 2001 (27), 2002 (39), 2004 (64), 2005 (131) : le schéma porte
**vingt-six ans de sédimentation datée dans le fichier lui-même**. C'est le critère de réalisme le
mieux étayé de tout ce dossier, et c'est ainsi qu'il a été jugé — non pas par l'âge du dépôt, mais
par la distribution des années de copyright dans les fichiers de schéma.

Preuves concrètes de sédimentation :

- **119 marqueurs** `deprecated` / `obsolete` / `not used` / `no more used` / `do not use` dans les
  fichiers de tables, répartis sur 48 fichiers. Exemples : `llx_societe.statut tinyint -- statut
  (deprecated)` **coexistant avec** `llx_societe.status tinyint -- active or not` ; `country_iban
  varchar(2) -- deprecated` ; `fk_commercial_signature -- obsolete` ; `remise / remise_percent /
  remise_absolue` tous trois marqués « deprecated (not used) ».
- **Noms mensongers** : `llx_societe_rib.iban_prefix` porte l'IBAN entier, pas un préfixe ; la
  même table porte les vestiges du RIB français (`code_banque`, `code_guichet`, `cle_rib`,
  `domiciliation`, `proprio`) **à côté** de leurs successeurs SEPA (`bic`, `bic_intermediate`,
  `rum`, `frstrecur`) **et** de champs de prestataires modernes (`stripe_account`, `Stancer` —
  seule colonne du schéma commençant par une majuscule, `last_four`, `card_type`,
  `exp_date_month`).
- **Abréviations opaques** : `llx_adherent.morphy` (personne morale ou physique), `llx_user.thm` /
  `tjm` (taux horaire / journalier moyen), `llx_user.idpers1`, `idpers2`, `idpers3`,
  `llx_socpeople.priv`, `llx_socpeople.poste`.
- **Le mécanisme `*_extrafields`** : **86 tables** `llx_*_extrafields` sur 413 — plus d'une table
  sur cinq — dont les colonnes sont créées à
  l'exécution par l'utilisateur final. Dans une base client réelle, ces tables portent des noms de
  colonnes arbitraires — c'est le cas d'usage exact de la carte #122.

**Catégories rares** : bancaire **oui et abondant** (`llx_societe_rib`, `llx_user_rib`,
`llx_prelevement_*`, `llx_paiement*`, 51 colonnes rien que pour `llx_societe_rib`). Identifiants
nationaux **oui** (`llx_user.national_registration_number` — le NIR ; `siren`, `siret`, `ape`,
`tva_intra`, `euid`, `idprof4/5/6`). Données RH sensibles **oui** (`llx_user.salary`,
`salaryextra`, `llx_salary`, `llx_holiday`, `llx_hrm_evaluation`,
`llx_recruitment_recruitmentcandidature`, `llx_user.photo`, `birth`, `birth_place`, `gender`).
**Santé : non. Infractions : non.**

### 3.2 Galette — le petit schéma tout français

**Licence** : GPL-3.0, vérifiée dans
[`LICENSE.md`](https://github.com/galette/galette/blob/master/LICENSE.md).

**Extraction** : DDL complet et unique dans
[`galette/install/scripts/mysql.sql`](https://github.com/galette/galette/blob/master/galette/install/scripts/mysql.sql)
(et un `pgsql.sql` jumeau — intéressant en soi : **deux dialectes pour le même schéma**).
45 scripts de migration `upgrade-to-0.60-mysql.sql` … `upgrade-to-1.201-mysql.sql` dans le même
répertoire.

**Volumétrie** : **31 tables, 194 colonnes**. C'est le plus petit du lot, et c'est un atout : il
est **annotable à la main en entier** en une session, ce qui en fait le pivot de calibrage du
travail d'annotation.

**Densité de données personnelles** : la plus forte du corpus par construction — une table
`galette_adherents` de **35 colonnes dont l'écrasante majorité est personnelle**.

**Langue** : **français strict, avec suffixe systématique `_adh`**. `nom_adh`, `prenom_adh`,
`pseudo_adh`, `societe_adh`, `titre_adh`, **`ddn_adh`** (date de naissance — abréviation opaque
parfaite), **`sexe_adh`**, `adresse_adh`, `cp_adh`, `ville_adh`, `region_adh`, `pays_adh`,
`tel_adh`, `gsm_adh`, `email_adh`, `info_adh`, `info_public_adh`, `prof_adh` (profession),
`login_adh`, **`mdp_adh`** (mot de passe), `date_crea_adh`, `activite_adh`, `bool_admin_adh`,
`lieu_naissance`, `date_echeance`, `num_adh`. Ailleurs : `montant_cotis`, `type_paiement_cotis`,
`libelle_statut`, `priorite_statut`, `libelle_type_cotis`.

**Sédimentation** : modérée. Le suffixe `_adh` **n'est pas appliqué partout** (`lieu_naissance`,
`date_echeance`, `parent_id`, `gpgid`, `fingerprint` ne le portent pas) — incohérence de
convention entre l'ancien noyau et les ajouts récents. `galette_tmplinks` et `galette_tmppasswds`
sont les deux seules tables `tmp_` du corpus entier, et elles sont **vivantes**, pas mortes : un
faux ami exemplaire pour un détecteur qui traiterait `tmp` comme un signal d'abandon. Aucun
marqueur `deprecated`. Dépôt GitHub depuis
[janvier 2013](https://github.com/galette/galette), migrations depuis la version 0.60.

**Catégories rares** : `sexe_adh` (donnée de genre), `gpgid` / `fingerprint` (clé publique — un
identifiant en ligne), `mdp_adh`. Bancaire : marginal (`galette_transactions`,
`galette_payments_schedules`, `galette_paymenttypes` — montants et types, pas d'IBAN). **Santé :
non. Infractions : non.**

### 3.3 OpenEMR — la seule source d'article 9

**Licence** : GPL-3.0, vérifiée dans
[`LICENSE`](https://github.com/openemr/openemr/blob/master/LICENSE).

**Extraction** : DDL complet dans
[`sql/database.sql`](https://github.com/openemr/openemr/blob/master/sql/database.sql), plus
**37 fichiers de migration** de `2_6_0-to-2_6_1_upgrade.sql` à `8_1_0-to-8_1_1_upgrade.sql`.
⚠️ `database.sql` mêle DDL et `INSERT` de données de référence (codes CQM, nomenclatures) : le
comptage doit filtrer, et le pivot ne doit reprendre que la DDL.

**Volumétrie** : **282 tables, 3 877 colonnes**. Heuristique naïve : **427 colonnes marquées
(11,0 %)**, dont **153 santé**, 120 identité, 90 contact, 49 connexion, 8 naissance, 7 identifiant
national, 6 sensible autre.

**Le seul candidat avec de vrais commentaires** : **673 clauses SQL `COMMENT`**, qui apparaîtront
donc dans `information_schema.columns.column_comment` d'une instance réelle. Exemples :
`district VARCHAR(255) COMMENT 'The county or district of the address'`,
`pid bigint(20) COMMENT 'Patient ID from patient_data'`,
`created_by int(11) COMMENT 'references users.id for session owner'`. **C'est la seule matière du
corpus qui permettra de mesurer si le moteur exploite le commentaire quand il existe** — sans
quoi cette colonne du pivot ne serait jamais testée.

**Langue** : anglais, avec des abréviations médicales et administratives américaines. Ce n'est pas
un défaut : c'est le contrepoint anglophone qui empêche le corpus de ne mesurer que du français.

**Catégories rares — c'est la raison d'être de ce candidat.**

- **Santé (art. 9)** : `history_data` (91 colonnes) porte `tobacco`, `alcohol`, `coffee`,
  `recreational_drugs`, `sleep_patterns`, `hazardous_activities`, `relatives_mental_illness`,
  `relatives_suicide`, `relatives_cancer`, `relatives_tuberculosis`, `last_mammogram`,
  `last_prostate_exam`, `last_psa`, `hysterectomy`, `hip_replacement`. `lists` porte `diagnosis`,
  `severity_al`, `injury_type`, `reaction`. `patient_data` porte `pharmacy_id`, `interpreter_needed`.
- **Origine, religion, situation sociale** : `patient_data.race`, `ethnicity`, `ethnoracial`,
  `religion`, `homeless`, `migrantseasonal`, `monthly_income`, `family_size`, `occupation`.
- **Identifiants nationaux** : `person.ssn`, et surtout **`patient_data.ss`** — le numéro de
  sécurité sociale dans une colonne de **deux lettres**. C'est le cas dur exemplaire : aucune
  correspondance lexicale ne l'attrapera, et une heuristique morphologique le confondra avec
  cent autres `ss`. `drivers_license`, `facility_npi`.
- **Bancaire** : `facility.iban`, `ar_session.payment_method`. Marginal.
- **Infractions (art. 10)** : **non**, et c'est un trou du corpus (§ 5).

**Sédimentation** : dépôt GitHub depuis [mai 2010](https://github.com/openemr/openemr),
migrations depuis la 2.6.0. Signes : les champs fourre-tout `genericname1`/`genericval1`,
`genericname2`/`genericval2`, `usertext1` à `usertext8`, `userlist1` à `userlist7` — colonnes dont
le contenu est arbitraire chez chaque déployeur, exactement le problème que la carte #122 doit
affronter. Le préfixe `hipaa_` (`hipaa_mail`, `hipaa_voice`, `hipaa_allowsms`, `hipaa_notice`)
marque une strate réglementaire américaine datée. `form_history_sdoh`, `patient_history`,
`amendments_history` : tables d'historique, pas des tables mortes.

### 3.4 PrestaShop — l'e-commerce, avec une réserve de licence

**Licence — la seule non-GPL du lot.** **OSL-3.0** pour le cœur, **AFL-3.0** pour les modules,
d'après [`LICENSE.md`](https://github.com/PrestaShop/PrestaShop/blob/develop/LICENSE.md) :
« PrestaShop Core is licensed under OSL-3.0 and PrestaShop Modules are licensed under AFL-3.0. »
L'API GitHub renvoie d'ailleurs `NOASSERTION`, ce qui confirme qu'aucun fichier `LICENSE`
standard n'est reconnu automatiquement. Voir § 2.3 : OSL est copyleft, avec une clause § 5
« External Deployment » plus large que l'AGPL, et déclarée **incompatible GPL** par la FSF.

**Extraction** : DDL complet dans
[`install-dev/data/db_structure.sql`](https://github.com/PrestaShop/PrestaShop/blob/develop/install-dev/data/db_structure.sql),
avec un placeholder `PREFIX_` substitué à l'installation (`ps_` par défaut). ⚠️ Les scripts de
migration **ne sont plus dans ce dépôt** : ils vivent dans le module
[`autoupgrade`](https://github.com/PrestaShop/autoupgrade). Il n'y a donc pas de série
diachronique directement disponible ici.

**Volumétrie** : **244 tables, 1 589 colonnes**. Heuristique naïve : **206 colonnes marquées
(13,0 %)** — le taux le plus élevé, cohérent avec le domaine.

**Langue** : anglais, avec deux résidus notables : `PREFIX_customer.siret` et `PREFIX_customer.ape`
(codes d'entreprise **français**) coexistent avec `PREFIX_address.dni` (pièce d'identité
**espagnole**) et `vat_number`. C'est le motif « application internationale portant des champs
nationaux » — un cas réel et fréquent chez un client français.

**Sédimentation** : dépôt GitHub depuis
[novembre 2012](https://github.com/PrestaShop/PrestaShop), projet depuis 2007
(« Copyright (c) Since 2007 PrestaShop and Contributors »). Convention `id_*` **omniprésente et
cohérente** — 619 tokens `id` sur 1 589 colonnes, soit le schéma le plus régulier du corpus.
Peu de saleté : **1 marqueur `deprecated`, 3 `todo`, aucun commentaire DDL**. Signes datés :
préfixe `bo_` pour les préférences du back-office (`bo_color`, `bo_theme`, `bo_css`, `bo_width`,
`bo_menu`), `ip_registration_newsletter`, `has_enabled_gravatar`, `optin`,
`preselect_date_range`.

**Catégories rares** : `customer.birthday`, `customer.passwd`, `customer.secure_key`,
`connections.ip_address`, `order_payment.card_number` et `card_brand`,
`address.dni`. **Santé : non. Infractions : non.**

**Verdict** : à retenir, mais **c'est le candidat que l'on retire en premier** si la question de
licence doit être tranchée par la prudence plutôt que par l'analyse.

### 3.5 GLPI — l'anglais strict, et une série diachronique gratuite

**Licence** : GPL-3.0, vérifiée dans
[`LICENSE`](https://github.com/glpi-project/glpi/blob/main/LICENSE).

**Extraction** : et c'est là son intérêt propre — le dépôt versionne **83 schémas vides
historiques** dans
[`install/mysql/`](https://github.com/glpi-project/glpi/tree/main/install/mysql), de
`glpi-0.85.5-empty.sql` à `glpi-11.0.4-empty.sql`, plus **86 répertoires de migration** dans
[`install/migrations/`](https://github.com/glpi-project/glpi/tree/main/install/migrations).
**Aucun autre candidat n'offre le même schéma à onze ans d'intervalle, prêt à charger.**

**Volumétrie** : **442 tables, 4 534 colonnes** en 11.0.4 ; **237 tables, 2 101 colonnes** en
0.90.5 ; **236 tables, 2 098 colonnes** en 0.85.5. Le diff 0.85.5 → 11.0.4 donne **2 610 colonnes
apparues et 174 disparues** — de la matière datée et vérifiable pour éprouver un détecteur sur
l'ancien et le récent **à domaine constant**, ce qu'aucun autre couple de schémas ne permet.

**Densité de données personnelles : la plus faible du lot**, et c'est un critère de sélection en
soi. GLPI gère du parc informatique : la majorité des colonnes décrivent des machines, pas des
gens. L'heuristique naïve marque 388 colonnes (8,6 %) — mais **279 d'entre elles ne sont marquées
que par le token `name`**, qui chez GLPI désigne un nom de serveur LDAP, de fabricant, de lieu,
de modèle. **C'est le meilleur générateur de faux positifs du corpus, et c'est exactement ce qu'un
banc doit contenir** : un schéma où la ligne de base naïve se trompe massivement, pour que la
précision devienne mesurable.

**Langue** : **anglais strict**, malgré l'origine française du projet (INDEPNET, 2003). C'est un
fait notable en soi : l'origine française d'un éditeur ne prédit pas la langue de ses
identifiants. Convention **au pluriel systématique** pour les clés étrangères
(`entities_id`, `users_id`, `locations_id`, `manufacturers_id`) — convention rare et cohérente,
utile comme cas limite. `completename`, `ancestors_cache`, `sons_cache`, `otherserial`.

**Sédimentation** : **zéro marqueur `deprecated`, zéro `todo`, 92 commentaires `--` seulement,
10 clauses `COMMENT`**. GLPI est le schéma **le plus propre** du corpus — et il faut le prendre
pour ce qu'il est : le pôle « propre mais gros et anglophone », pas une source de saleté.

**Catégories rares** : quasi aucune. `glpi_users` (email, phone, mobile, registration_number),
`glpi_entities` (latitude, longitude), `glpi_authldaps` (rootdn_passwd). **Santé : non.
Infractions : non. Bancaire : non.**

### 3.6 SACoche — le plus grand schéma strictement français

**Licence** : **AGPL-3.0**, vérifiée dans
[`COPYING`](https://forge.apps.education.fr/sesamath/sacoche/-/blob/master/COPYING) (texte de la
GNU AGPL version 3). Voir § 2.3 : pour un corpus de fichiers statiques, AGPLv3 ≡ GPLv3.

**Extraction — la plus commode du dossier.** Le dépôt (Sésamath, sur la forge de l'Éducation
nationale : `forge.apps.education.fr/sesamath/sacoche`) versionne **un fichier `.sql` par table** :
98 dans `_sql/structure/` et 6 dans `_sql/webmestre/`. S'y ajoutent **17 fichiers de migration
annuels**, `requetes_structure_maj_base_2010.inc.php` … `_2026.inc.php` — soit **dix-sept années
de migrations datées à l'année près**, mais enfouies dans du PHP, donc non lues ici.

**Volumétrie** : **103 tables, 632 colonnes**. Heuristique naïve : **74 colonnes marquées
(11,7 %)**.

**Langue : français, et c'est le plus gros du corpus dans ce régime** — six fois Galette. Les
tokens dominants des noms de colonnes sont français de bout en bout : `nom`, `matiere`, `ordre`,
`niveau`, `eleve`, `prof`, `contenu`, `colonne`, `famille`, `acquis`, `adresse`, `seuil`,
`valeur`, `demande`, `referentiel`, `groupe`, `periode`, `rubrique`, `saisie`, `decision`.

**Sédimentation — et l'artefact que le ticket #125 demandait explicitement.** La table
`sacoche_user` porte 33 colonnes, toutes préfixées `user_`… **sauf une** :

```
user_id, user_sconet_id, user_sconet_elenoet, user_reference, ID_NATIONAL, user_profil_sigle,
user_genre, user_nom, user_prenom, user_naissance_date, user_email, user_email_origine,
user_email_refus, user_login, user_password, user_langue, user_daltonisme, ...
eleve_classe_id, eleve_lv1, eleve_lv2, eleve_dnb_mef_id, eleve_uai_origine,
user_id_ent, user_id_gepi, ...
```

**`ID_NATIONAL`, en majuscules, au milieu d'une table snake_case française.** C'est exactement le
`CLI_NOM_1` que le ticket décrivait, et c'est le seul exemplaire authentique trouvé dans tout le
dossier. S'y ajoutent **quatre générations successives d'identifiant d'élève** qui coexistent
(`user_sconet_id`, `user_sconet_elenoet`, `user_reference`, `ID_NATIONAL`), **deux ponts vers
d'autres logiciels** (`user_id_ent`, `user_id_gepi`), et un **changement de préfixe en cours de
table** (`user_*` puis `eleve_*`).

**Commentaires** : **8 clauses `COMMENT=` de niveau table, en français**, et **zéro de niveau
colonne**. Exemples : `"Modalités d'accompagnement"`, `"Livret Scolaire Unique"`,
`"Enseignements Pratiques Interdisciplinaires"`. ⚠️ **Cela ouvre une question de format de pivot** :
ces commentaires vivent dans `information_schema.tables.table_comment`, pas dans
`columns.column_comment`. Le pivot de #122 les capture-t-il ? La question n'est pas tranchée, et
SACoche est le seul candidat qui la pose.

**Catégories rares — et une limite de premier ordre.** `user_genre`, `user_naissance_date`, et
surtout **`user_daltonisme`** (une donnée de santé dans un logiciel scolaire). Mais l'essentiel
est ailleurs, et c'est un **cas limite majeur pour la carte #122** : la table
`sacoche_livret_modaccomp` n'a que deux colonnes, `livret_modaccomp_code` et
`livret_modaccomp_nom` — dont les **valeurs** semées en dur sont `PAI` (projet d'accueil
individualisé), `PPS` (projet personnalisé de scolarisation), `SEGPA`, `ULIS`, `UPE2A`. Rattachées
nominativement à un élève, ce sont des **données de santé et de handicap au sens de l'article 9**.
Or **rien dans le nom des colonnes ne le dit.** La décision de cadrage 1 de #122 — « le service ne
lit que le schéma, jamais les valeurs » — signifie que le détecteur **ne peut pas voir cette
sensibilité-là**. Ce n'est pas un défaut du candidat : c'est la démonstration, sur un cas réel et
français, de ce que la doctrine du dépôt oblige à déclarer non regardé.

### 3.7 Paheko (ex-Garradin) — la trajectoire français → anglais du même schéma

**Licence** : **AGPL-3.0**, vérifiée dans
[`COPYING`](https://github.com/paheko/paheko/blob/master/COPYING) ; `LICENSE.md` la déclare.

**Provenance** : l'amont est un dépôt **Fossil** ([fossil.kd2.org/paheko](https://fossil.kd2.org/paheko/)).
Le miroir Git [github.com/paheko/paheko](https://github.com/paheko/paheko) a été vérifié fidèle :
le commit de tête (8 août 2026, auteur `bohwaz`) porte un trailer `FossilOrigin-Name:` renvoyant
au hash Fossil. C'est le miroir qu'il faut citer, en nommant l'amont.

**Extraction, et ce qui rend ce candidat unique.** Le dépôt embarque **ses propres schémas
historiques** dans `archives/`, ce qui donne le **même schéma dans trois régimes linguistiques
successifs et datés** :

| Fichier | Tables / colonnes | Langue |
| --- | ---: | --- |
| `archives/0.8.0_schema.sql` | 26 / 130 | **Français intégral** |
| `archives/1.0.0_schema.sql` | 26 / 144 | **Hybride** |
| `src/include/migrations/1.3/schema.sql` (HEAD) | 43 / 271 | **Anglais intégral** |

- **0.8.0** : `membres_categories`, `cotisations`, `cotisations_membres`, `rappels`,
  `rappels_envoyes`, `compta_exercices`, `compta_comptes`, `compta_comptes_bancaires`,
  `compta_journal`, `compta_moyens_paiement`, `compta_rapprochement`, `fichiers`,
  `fichiers_membres`, `wiki_suivi`.
- **1.0.0** : les tables françaises `membres_categories`, `membres_sessions`, `fichiers`,
  `fichiers_membres`, `recherches` **coexistent** avec les anglaises `services`, `services_fees`,
  `services_users`, `acc_charts`, `acc_accounts`, `acc_years`, `acc_transactions`. Et l'on y
  trouve **`fichiers_acc_transactions`** — un nom de table **moitié français, moitié anglais**.
- **HEAD** : `users`, `users_categories`, `files`, `searches`, `web_pages`, `acc_*`.

**Aucun autre candidat n'offre le même schéma, même domaine, même projet, en français puis en
anglais.** GLPI donne l'ancien contre le récent à domaine constant ; Paheko donne **la langue** à
domaine constant. Pour un détecteur destiné au marché français, c'est la variable la plus
pertinente qu'on puisse isoler.

**Deux trouvailles qui valent à elles seules le versement.**

1. **La table qui porte les données personnelles n'est pas dans le DDL.** `schema.sql` contient
   littéralement :

   ```sql
   -- CREATE TABLE users (...);
   -- Organization users table, dynamically created, see config_users_fields table
   ```

   Les colonnes réelles sont engendrées à l'installation depuis
   `src/include/data/users_fields_presets.ini`, **en français** : `numero`, `pronom`, `nom`,
   `email`, `adresse`, `code_postal`, `ville`, `telephone`, `pays`, `annee_naissance`,
   `date_naissance`, `photo`. **C'est l'argument le plus fort du § 2.6** : lire les fichiers `.sql`
   ferait manquer *toute* la table des personnes ; seule une installation suivie d'une
   introspection la voit.
2. **Le fichier de presets porte des annotations RGPD écrites par les auteurs.** Sur
   `date_naissance` : « Attention, cette information est très sensible, il est déconseillé par le
   RGPD de la demander aux membres. Il est préférable de demander seulement l'année de
   naissance. » C'est un **jeu d'annotations gratuit et de première main** — non pas une vérité
   terrain, mais un point de comparaison qualitatif qu'aucun autre candidat ne fournit.

**Catégories rares** : `compta_comptes_bancaires (banque, iban, bic)` — **de l'IBAN dans un schéma
en français**, présent de `0.8.0` à `0.9.5` et **disparu en `1.0.0`**. Dolibarr fournit du bancaire
en abondance mais dans un contexte bilingue ; Paheko le fournit en français pur, et daté.
**Santé : non. Infractions : non.**

⚠️ **Réserve technique** : Paheko est en **SQLite**. Or SQLite n'a **ni `information_schema` ni
clause `COMMENT`** — l'introspection y passe par `PRAGMA table_info`. Le pivot de #122 devra donc
soit se restreindre à MySQL/MariaDB et PostgreSQL, soit prévoir un adaptateur. **C'est une
question ouverte que ce candidat force à poser** ; elle n'est pas tranchée ici.

### 3.8 SuiteCRM — écarté, et pourquoi

**Licence** : **AGPL-3.0**, vérifiée dans
[`LICENSE.txt`](https://github.com/salesagility/SuiteCRM/blob/hotfix/LICENSE.txt) (texte de la GNU
AGPL version 3, 19 novembre 2007). Ce n'est **pas** le motif du rejet : § 2.3 établit que pour un
corpus de fichiers statiques, AGPLv3 se comporte comme GPLv3.

**Le motif du rejet est l'extraction.** SuiteCRM **ne versionne aucun DDL**. Le schéma vit dans
des `vardefs.php` — un par module, 123 modules, `modules/Contacts/vardefs.php` fait 33 Ko — que le
moteur SugarBean traduit en `CREATE TABLE` à l'installation. `tests/_data/dump.sql` est **vide**
dans le dépôt. Extraire suppose donc soit d'écrire un analyseur PHP dédié, soit d'installer
l'application entière dans un conteneur — un travail nettement supérieur à celui des cinq autres,
pour une matière que les cinq autres couvrent déjà.

**Ce qu'on perd en l'écartant, et qui mérite d'être noté** : SuiteCRM est le seul candidat à
porter une sédimentation **de préfixes de modules**, celle-là même que le ticket #125 décrit —
héritée du fork de SugarCRM CE (2004) et des sous-projets absorbés : `AOS_` (8 modules), `AOR_`
(5), `AOW_` (4), `jjwg_` (4), `AOD_`, `AOK_`, `AOP_`, `AOBH_`, `AM_`, `FP_`, `EAPM`. S'y ajoute le
mécanisme `_cstm` des champs personnalisés. **Si le banc conclut plus tard qu'il manque au corpus
un cas de préfixes hérités opaques, c'est ici qu'il faut revenir** — et le coût d'extraction sera
alors justifié par un besoin nommé, pas subi par défaut.

### 3.9 Odoo Community — non retenu, et le motif est instructif

**Licence** : **LGPL-3.0**, vérifiée dans
[`LICENSE`](https://github.com/odoo/odoo/blob/master/LICENSE) : « Odoo is published under the GNU
LESSER GENERAL PUBLIC LICENSE, Version 3 ». La plus permissive du dossier. **Ce n'est donc pas la
licence qui l'écarte.**

**Extraction** : le dépôt ne contient que **deux fichiers `.sql`** — `odoo/addons/base/data/
base_data.sql` et `neutralize.sql`, qui sont des données d'amorçage, **pas un schéma**. Le schéma
est dérivé des champs déclarés dans les classes Python de l'ORM. Le rejet ne tient pas seulement
au fait qu'il faut installer : il tient à ce que **la source Python ne permet pas de savoir quelles
colonnes existent**. Sur `addons/hr/models/hr_employee.py`, 105 champs sont déclarés, dont **25
portent `compute=` et 26 `related=`** — et un champ calculé non stocké **ne devient jamais une
colonne**. Distinguer les uns des autres suppose de réimplémenter la logique de persistance de
l'ORM, ou de déployer. S'y ajoute que la volumétrie dépend entièrement du **choix des modules
installés** : il n'existe pas de « schéma d'Odoo », seulement le schéma d'un déploiement donné.

**Non retenu pour la v1**, pour un motif de coût d'extraction — le même que SuiteCRM, en pire, et
sans la compensation des préfixes hérités. À rouvrir seulement si le corpus manque d'un cas
« schéma engendré par un ORM », qui est une famille en soi et que Django partagerait.

---

## 4. Recommandation de composition du corpus

### Six schémas de banc, un témoin, une réserve

| # | Schéma | Ce qu'il apporte que les autres n'apportent pas |
| --- | --- | --- |
| 1 | **Dolibarr** (413 t. / 5 314 c.) | Le **bilinguisme FR/EN à l'intérieur d'une même table**, 26 ans de sédimentation datée, 119 colonnes marquées mortes mais vivantes, le bancaire dense, les abréviations opaques (`morphy`, `thm`, `idpers1`), les noms mensongers (`iban_prefix`). **C'est le pilier ; sans lui le corpus ne mesure rien pour un client français.** |
| 2 | **SACoche** (103 t. / 632 c.) | Le **français à grande échelle** — six fois Galette — le seul `CLI_NOM_1` authentique du dossier (`ID_NATIONAL`), quatre générations d'identifiant qui coexistent, et le cas limite des **catégories rares portées par la valeur et non par le nom** (PAI/PPS). |
| 3 | **Paheko**, trois états (26→43 t.) | **Le même schéma en français, en hybride, puis en anglais** — la seule pièce qui isole *la langue* à domaine constant. Plus l'IBAN dans un schéma français, et une table de personnes **absente du DDL**. |
| 4 | **OpenEMR** (282 t. / 3 877 c.) | La **seule source d'article 9** — santé, origine, religion, situation sociale — et le **seul avec de vrais `column_comment`** (673). Sans lui, le banc ne peut rien dire ni sur les catégories qui comptent le plus, ni sur l'exploitation du commentaire. |
| 5 | **GLPI 11.0.4** (442 t. / 4 534 c.) | L'**anglais strict** et la **densité personnelle la plus faible** — donc le meilleur générateur de faux positifs, indispensable pour que la précision soit mesurable. |
| 6 | **Galette** (31 t. / 194 c.) | La **taille annotable en entier** en une session : le point d'ancrage du protocole d'annotation, et un troisième régime du français (suffixe `_adh` systématique mais incomplet). |
| — | `temoin/db/` (11 t. / 72 c.) | **Test de fumée uniquement, jamais banc.** Français propre, écrit par l'auteur : il vérifie que la chaîne fonctionne, il ne mesure rien. |
| ⚠️ | *(réserve)* **PrestaShop** (244 t. / 1 589 c.) | E-commerce, convention `id_*` régulière, champs nationaux (`siret`, `ape`, `dni`). **À n'ajouter que si la question OSL-3.0 du § 2.3 est tranchée en faveur du versement** — c'est le seul candidat dont la licence est déclarée incompatible avec celle des autres. Le corpus tient sans lui. |

### Pourquoi cette combinaison-là

- **Français** : Dolibarr (bilingue interne), SACoche (français à grande échelle), Paheko 0.8.0
  (français pur, petit), Galette (français abrégé et suffixé) — **quatre régimes distincts du
  français**, là où la première version de cette recommandation n'en avait que deux.
- **Anglais** : GLPI (strict), OpenEMR (abréviations médicales), Paheko HEAD (anglais récent et
  propre) — trois régimes distincts de l'anglais.
- **La langue comme variable isolée** : **Paheko 0.8.0 → 1.0.0 → HEAD**, même projet, même métier,
  même auteur. C'est la pièce la plus précieuse du corpus pour un détecteur destiné au marché
  français : elle mesure directement ce qui se passe quand seul le vocabulaire change.
- **Ancien contre récent** : GLPI 0.85.5 contre 11.0.4 reste disponible **en option** — 2 610
  colonnes apparues, 174 disparues, à domaine constant. Non retenu au noyau parce que Paheko couvre
  l'axe diachronique avec, en prime, le basculement linguistique ; ajouter les deux versions de
  GLPI doublerait le volume anglophone sans rien ajouter au français.
- **Catégories rares** : santé, origine, religion, situation sociale par OpenEMR ; bancaire par
  Dolibarr (abondant, bilingue) **et Paheko ≤ 0.9.5 (`banque`, `iban`, `bic`, en français)** ;
  identifiants nationaux par Dolibarr (`national_registration_number`), OpenEMR (`ss`,
  `drivers_license`) et SACoche (`ID_NATIONAL`, `structure_siret`).
- **Faux positifs** : GLPI et Dolibarr fournissent des `name` / `nom` non personnels en abondance
  (serveurs LDAP, fabricants, départements, modèles). **Un corpus qui n'aurait que des colonnes
  personnelles mesurerait le rappel et rien d'autre.**
- **Volumétrie** : ~1 400 tables et ~15 000 colonnes. Trop pour une annotation exhaustive.
  **L'échantillonnage, sa stratification et le protocole d'annotation sont hors de ce ticket** —
  mais Galette (194 colonnes) et Paheko 0.8.0 (130 colonnes) sont annotables intégralement, et
  donnent deux points d'ancrage, l'un anglophone-adjacent, l'autre strictement français.

### Deux questions de format que ces ajouts forcent à poser

Elles ne se tranchent pas ici, mais elles ne peuvent plus être ignorées :

1. **Le pivot capture-t-il le commentaire de table ?** SACoche porte 8 `COMMENT=` de niveau table,
   en français, et zéro de niveau colonne. Si le pivot ne lit que `columns.column_comment`, cette
   matière est perdue.
2. **Le pivot est-il portable hors MySQL ?** Paheko est en **SQLite**, qui n'a ni
   `information_schema` ni clause `COMMENT` (introspection par `PRAGMA table_info`). Retenir Paheko
   suppose soit un adaptateur, soit une transposition assumée et documentée du schéma vers MariaDB.

### Conditions de versement, rappelées

Aucun schéma n'entre dans `exploration/` autrement que par : **installation en conteneur jetable →
requête `information_schema` → pivot normalisé → attribution complète (dépôt, tag, licence, date,
commande) → corpus licencié CC BY-SA 4.0**. Aucun fichier `.sql` amont n'est copié dans ce dépôt.
Les commentaires `--` du code source amont sont exclus par construction, parce que le pivot ne les
voit pas.

### Ce qui reste à trancher, ailleurs

- **Le sort des 673 `column_comment` d'OpenEMR** : conservés, ou reformulés. C'est le seul point
  où le risque de licence n'est pas nul, et c'est une décision de construction du corpus.
- **La taxonomie fermée des catégories** — prérequis de l'annotation, gouvernée par #122, absente
  de ce ticket.
- **Le protocole d'annotation et l'échantillonnage** — hors périmètre déclaré du #125.

---

## 5. Le trou qu'aucun candidat ne bouche : l'article 10

**Aucun des neuf candidats ne porte de données d'infractions ou de condamnations.** L'heuristique
a cherché `crime`, `criminal`, `offense`, `conviction`, `condamn`, `infraction`, `casier`,
`judiciaire`, `arrest`, `prison`, `probation`, `penal` sur l'ensemble des colonnes relevées :
**zéro correspondance, partout** — y compris sur SACoche et Paheko, ajoutés en second. Ce n'est pas
un artefact de l'heuristique : les logiciels de gestion, de commerce, de parc informatique,
d'association et de scolarité n'en manipulent pas, et le seul candidat santé est américain.

**Conséquence à porter explicitement** : le banc **ne pourra rien mesurer** sur l'article 10 avec
ce corpus. Deux issues, et ce ticket ne tranche pas entre elles :

1. **L'assumer et le dire** — dans la ligne de l'`Omission silencieuse` et de la décision de
   cadrage 11 de #122 : le rapport dit ce qu'il n'a pas regardé, le banc doit dire ce qu'il n'a
   pas mesuré. **C'est l'issue que ce document recommande**, faute de candidat vérifié.
2. **Chercher un candidat** dans le travail social, l'insertion ou la justice. Deux pistes ont
   été relevées, **aucune retenue** :
   - **Chill** ([gitlab.com/Chill-Projet](https://gitlab.com/Chill-Projet/chill-bundles)) —
     travail social français, donc identifiants potentiellement français et catégories des
     articles 9 et 10. ⚠️ **Aucune de ses caractéristiques n'a été vérifiée** : ni la licence, ni
     le schéma, ni la volumétrie, ni la langue (§ 6).
   - **`betagouv/rdv-insertion`** — signalé comme riche en catégories rares. **Écarté sur un fait
     vérifié** : l'API GitHub rend `license: null`, le dépôt **ne porte aucun fichier de licence**
     sur sa branche par défaut. Sans licence, il n'y a pas de droit de redistribution à discuter,
     et le § 2 ne s'applique même pas. **Non redistribuable, quelle que soit l'analyse du § 2.**

Une remarque de fond, tirée de SACoche (§ 3.6) : même *avec* un candidat porteur d'article 10, il
n'est pas acquis que la sensibilité soit lisible **dans le nom des colonnes**. Chez SACoche, PAI et
PPS — des données de santé — sont des **valeurs** derrière un `livret_modaccomp_code` parfaitement
neutre. La décision de cadrage 1 de #122 (« le service ne lit que le schéma, jamais les valeurs »)
implique que cette classe de sensibilité est **structurellement hors de portée**. Le trou de
l'article 10 n'est donc pas seulement un trou de corpus : c'est peut-être un trou de méthode, et
c'est au banc de le dire.

---

## 6. Points non vérifiés en source primaire

Cette section est la condition de lecture du reste.

**Sur les candidats**

- **Chill** (§ 5) : cité comme unique piste sérieuse pour l'article 10, **sans aucune
  vérification** — ni licence, ni schéma, ni volumétrie, ni langue des identifiants. Rien de ce
  qui a pu être écrit ailleurs à son sujet n'a été repris ici.
- **Tout le reste du paysage francophone est resté non évalué** : applications Django et Rails de
  l'État (Etalab, DINUM, beta.gouv, `demarches-simplifiees`), briques Entr'ouvert/Publik, **PMB**,
  **GRR**, **GEPI**, **Framadate**, modules Dolibarr métier, plugins Galette. Une exploration
  parallèle de ce périmètre a produit un rapport détaillé qui s'est révélé **fabriqué**
  (cf. § 1, « Règle de provenance ») ; il a été intégralement écarté, et **aucun de ces candidats
  n'est entré dans la recommandation**. La seule chose qui en a été retenue, parce qu'elle a été
  re-vérifiée à la main, est le fait négatif sur `rdv-insertion` (§ 5). **Ce périmètre reste
  entièrement à faire, et un ticket qui le reprendrait devrait repartir de zéro.**
- **SACoche** : seuls les 104 fichiers `.sql` de `_sql/structure/` et `_sql/webmestre/` ont été
  analysés. Les **17 fichiers de migration annuels** `requetes_structure_maj_base_20XX.inc.php`
  contiennent du DDL noyé dans du PHP et **n'ont pas été lus** : les 103 tables / 632 colonnes sont
  donc un **plancher**, et la sédimentation réelle est probablement supérieure à ce qui est décrit.
- **Paheko** : le miroir GitHub a été vérifié fidèle sur son commit de tête (trailer
  `FossilOrigin-Name`), **pas sur toute son histoire**. Les colonnes réelles de la table `users`
  ont été lues dans `users_fields_presets.ini`, **sans installation** : la liste effective produite
  par une installation par défaut n'a pas été observée. L'ancienneté du projet sous le nom
  *Garradin* (antérieure au miroir) n'a pas été datée.
- **PrestaShop** : les scripts de migration du module `autoupgrade` sont mentionnés, mais leur
  contenu **n'a pas été inspecté** ; l'absence de série diachronique exploitable est une
  déduction, pas une vérification.
- **Galette** : l'existence de plugins officiels portant du DDL additionnel a été évoquée mais
  **n'a pas été vérifiée**, non plus que leur licence ou la langue de leurs identifiants.

**Sur les mesures**

- **Les taux de « colonnes personnelles » (8,6 % à 13,0 %) sont des heuristiques par mots-clés,
  pas des annotations.** Ils sur-comptent massivement — le § 3.5 en donne la démonstration sur
  GLPI. Ils ne doivent jamais servir de vérité terrain ni figurer dans un résultat de banc.
- Les comptages de tables et colonnes viennent d'un analyseur `CREATE TABLE` *ad hoc* écrit pour
  ce ticket, **non versionné et non testé**. Un écart de quelques unités est possible, notamment
  là où la DDL mêle `CREATE TABLE` et `ALTER TABLE` (Dolibarr, où 755 fichiers donnent 413
  `CREATE TABLE`) ou DDL et `INSERT` (OpenEMR).
- **Le pivot exact de la carte #122 n'existe pas encore.** L'affirmation centrale du § 0.1 a été
  vérifiée sur **MariaDB 11.8.8** avec le DDL amont réel, mais **hors du chemin d'installation des
  applications** : Dolibarr et OpenEMR installent par leur propre installeur, qui pourrait ajouter
  des `ALTER TABLE … COMMENT` que les fichiers de tables ne montrent pas. Le comportement de
  PostgreSQL (`COMMENT ON COLUMN`, exposé par `col_description()` et non par
  `information_schema.columns`) **n'a pas été examiné du tout** — or Galette et Chill offrent une
  variante PostgreSQL. **SQLite non plus n'a pas été éprouvé**, alors que Paheko l'impose (§ 4).
- Les chiffres portent sur le `HEAD` de la branche par défaut au **8 août 2026**, pas sur un tag
  stable. Le corpus devra épingler des versions exactes.

**Sur le droit**

- **Aucune décision de l'UE ne porte spécifiquement sur l'extraction d'un schéma relationnel.**
  L'analyse du § 2 repose sur une **analogie** entre schéma relationnel et « format de fichiers de
  données » au sens de *SAS Institute* — analogie forte, mais analogie.
- Les entrées de la FAQ GPL ne sont pas du droit : c'est l'interprétation du licenciant, opposable
  de fait mais pas judiciairement établie.
- La qualification d'OSL-3.0 comme le point le plus risqué du dossier est une **appréciation**,
  fondée sur l'incompatibilité GPL déclarée par la FSF et sur la largeur du § 5, pas sur un
  contentieux.
- **Ce document n'est pas un avis juridique.** Si le corpus devait être largement diffusé ou
  entrer dans un produit commercial, ces éléments constituent un dossier de départ pour un conseil
  en propriété intellectuelle, pas un substitut.
