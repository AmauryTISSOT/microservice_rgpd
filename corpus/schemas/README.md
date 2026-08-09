# Corpus de schémas annotés

Vérité terrain servant à **trancher** puis à **évaluer** le moteur de dépistage du
contexte [`Screening`](../../docs/contexts/screening/CONTEXT.md).

- Ticket : [Constituer le corpus de schémas annotés](https://github.com/AmauryTISSOT/microservice_rgpd/issues/129)
- Carte : [Détecter les données personnelles dans le schéma d'une base du client](https://github.com/AmauryTISSOT/microservice_rgpd/issues/122)
- Protocole : [`PROTOCOLE-ANNOTATION.md`](./PROTOCOLE-ANNOTATION.md) — **à lire avant d'annoter une seule colonne**

## Emplacement

**`corpus/schemas/`, et non `exploration/`.** Le ticket posait `exploration/` en
hypothèse par défaut ; le précédent du dépôt tranche l'inverse, et il est net :
[`corpus/README.md`](../README.md) porte déjà la **vérité terrain annotée** du
projet (les 120 demandes RGPD), tandis qu'`exploration/` porte le **banc et ses
sorties** — `banc.py`, les fichiers de prédictions, les carnets de réglage. Ce
corpus-ci est de la vérité terrain annotée à la main ; il rejoint la vérité
terrain. Le banc qui le consommera, lui, ira dans `exploration/`.

Même argument que celui écrit dans `corpus/README.md` : l'actif est consommé par
au moins deux mondes, et le rattacher à l'un d'eux présumerait d'une décision qui
n'est pas prise.

## Ce que contient ce répertoire

| Chemin | Contenu |
|---|---|
| [`pivots/`](./pivots/) | Les relevés bruts, **sortie directe de l'introspection**. Un fichier par schéma, au format `screening-pivot/1`. |
| [`annotation/`](./annotation/) | Les colonnes **à annoter**, une ligne par colonne, champs `categorie` / `motif` / `annotateur` / `date` à remplir. |
| [`plan-de-sondage.json`](./plan-de-sondage.json) | Régime, strates et probabilités d'inclusion, par schéma. |
| [`PROTOCOLE-ANNOTATION.md`](./PROTOCOLE-ANNOTATION.md) | Les règles, écrites avant l'annotation. |
| [`limites-de-methode.md`](./limites-de-methode.md) | Les colonnes hors de portée du régime schéma-seul. Jamais de la mesure. |
| [`journal-arbitrages.md`](./journal-arbitrages.md) | Les cas non prévus par le protocole. |
| [`double-codage/`](./double-codage/) | **Tentative 1**, close et figée : les 300 colonnes tirées, le cahier humain, son [verdict](./double-codage/VERDICT.md) (κ = 0,040), la [règle d'arbitrage pré-enregistrée](./double-codage/ARBITRAGE.md) et le [classement des 79 désaccords](./double-codage/piles.md). |
| [`double-codage-2/`](./double-codage-2/) | **Tentative 2**, en cours : 300 colonnes neuves tirées sur le **complémentaire** des 300 brûlées, cahier **vierge**. |
| [`outils/`](./outils/) | `extraire.sh` (clonage → conteneur → introspection) et `echantillonner.py`. Puis l'outillage d'annotation, ci-dessous. |

⚠️ **Une tentative de double codage ne s'écrase pas, elle s'ajoute.** Le cahier
de la tentative 1 est un **document daté** : le corriger effacerait la mesure au
lieu de l'expliquer. `commun.TENTATIVE_COURANTE` dit laquelle est vivante ;
`accord.py --tentative 1` relit l'ancienne sans la recalculer.

## L'outillage d'annotation

Aucun de ces outils ne juge une étiquette — la vérité terrain est humaine, et le
§ 1 du protocole dit pourquoi. Ils vérifient ce que le protocole a rendu
**testable**, tirent ce qui doit être tiré d'avance, et calculent les chiffres.

| Outil | Rôle |
|---|---|
| `valider.py` | Applique la règle mécanique du § 2 — *motif présent ⇔ ce n'est pas `Unflagged`* — plus taxonomie, doublons, lignes à moitié remplies. `--strict` exige le corpus complet. |
| `tirer-double-codage.py` | Tire les 300 colonnes du § 4, stratifiées par schéma, **sur le complémentaire des tentatives précédentes**, et écrit le cahier vierge. |
| `coder.py` | La saisie à la main des 300, une par une. Porte les **quatre durcissements** du 2026-08-09 (#145) — voir plus bas. |
| `accord.py` | Accord brut, kappa de Cohen, **accord restreint aux colonnes signalées**, matrice des désaccords. `--tentative N`. |
| `incoherences.py` | Les colonnes de même nom étiquetées différemment. Classe, ne corrige pas. |
| `piles.py` | Classe les 79 désaccords de la tentative 1 selon la règle **pré-enregistrée** d'`ARBITRAGE.md`, et évalue ses conditions de réfutation. |
| `distribution.py` | Distribution par catégorie re-pondérée par les probabilités d'inclusion, et taux de repli. |

**L'ordre compte, et il n'est pas commode par hasard :**

```bash
python3 outils/tirer-double-codage.py   # AVANT de coder — sinon l'échantillon se choisit
python3 outils/valider.py --strict      # le corpus machine tient debout
python3 outils/coder.py                 # les 300, à la main, en aveugle
python3 outils/accord.py --markdown     # publié AVANT tout chiffre du moteur
python3 outils/distribution.py --markdown
```

⚠️ **Le tirage des 300 se fait avant la première étiquette.** Un échantillon tiré
après coup se choisit, même de bonne foi, en connaissance de ce qu'il contient.
Le tirage est figé par un seed et se rejoue à l'identique.

⚠️ **La passe humaine n'ouvre jamais `annotation/`.** Le cahier ne porte que les
champs du pivot — ni étiquette, ni `strate`, ni `proba_inclusion` : la blindness
est garantie par ce que le fichier **ne contient pas**, et non par la discipline
de qui le remplit. Une passe qui relit l'autre mesure la docilité, pas l'accord.

### Les quatre durcissements de `coder.py` — 2026-08-09

La tentative 1 a rendu κ = 0,040 avec, sur son cahier, quatre défauts
**constatés**. Chacun a son remède, et le premier est le seul non négociable :

1. **le motif doit citer** une sous-chaîne du nom de colonne, du nom de table, du
   type, ou un `§` — 16 motifs sur 47 disaient « Nom de la colonne », or c'est le
   motif qui rend un désaccord **arbitrable** ;
2. **la catégorie se tape par son nom**, jamais par un rang — `ConnectionData` et
   `Identity` étaient voisines au menu, et des étiquettes contredisaient leur
   propre motif ;
3. **`valider.py` passe avant la clôture** — un motif « erreur ici$ » est allé
   jusqu'au cahier publié ;
4. **le § 3.2 s'affiche sur les clés étrangères**, première source de désaccord.

⚠️ **Aucun de ces remèdes ne suggère de catégorie.** Le § 3.10 n'a
délibérément *pas* reçu le rappel du remède 4 : signaler « ce nom est opaque »
souffle qu'il faut hériter du domaine de la table. Rappeler le § 3.2 nomme une
propriété du pivot ; rappeler le § 3.10 nommerait un début de réponse.

⚠️ **Le double codage est *intra*-annotateur** — amendement du 2026-08-09 au § 4
du protocole, faute d'un second lecteur. Il mesure la **constance** d'une
personne, pas la **reproductibilité** du protocole ; l'accord en est **majoré**
et le plancher qu'il donne au banc **optimiste**. La seconde passe se fait une
fois les 3 254 colonnes terminées, pour que le délai soit le plus long possible.

⚠️ **`echantillonner.py` refuse désormais de tourner** dès qu'une étiquette
existe : il réécrit `annotation/` de bout en bout et détruirait plusieurs séances
de travail humain. L'avertissement était écrit ; il est maintenant mécanique.

## L'extraction se fait par introspection, jamais en lisant le DDL

⚠️ **C'est la contrainte qui commande toute la construction**, et elle vient de
[#125](https://github.com/AmauryTISSOT/microservice_rgpd/issues/125). Le DDL amont
est chargé dans un conteneur jetable, puis c'est le **catalogue du SGBD** qui est
interrogé, avec **les requêtes mêmes** que le service fournira à l'`Operator` —
celles de [`releves/`](../../releves/), et non une copie.

Mesuré ici, sur les 5 382 colonnes de Dolibarr : **0 commentaire de colonne
atteint `information_schema`**, alors que les fichiers DDL en portent 1 913 sous
forme de `--`. Un corpus lu dans les fichiers aurait donc nourri le banc d'un
signal que la production ne donnera **jamais** au moteur.

La contrainte juridique — extraire par introspection pour rester dans la
directive 2009/24 art. 1(2) et la CJUE *SAS Institute* pt 39 — et la contrainte de
réalisme imposent ici **la même chose**. C'est une convergence, elle est exploitée.

## Volumétrie mesurée

Comptages **par introspection**, le 8 août 2026. Ils remplacent, et parfois
corrigent, ceux de #125 qui étaient tirés des fichiers.

| Schéma | Dialecte | Tables | Colonnes | Comm. colonne | Comm. table | FK |
|---|---|---:|---:|---:|---:|---:|
| Dolibarr | MariaDB | 413 | 5 382 | **0** | 0 | 234 |
| GLPI 11.0.9-dev | MariaDB | 442 | 4 519 | 8 | 28 | 0 |
| OpenEMR | MariaDB | 283 | 3 780 | **647** | 200 | 0 |
| SACoche | MariaDB | 98 | 548 | **152** | 42 | 0 |
| Galette | MariaDB | 31 | 194 | 0 | 0 | 24 |
| Paheko 0.8.0 | SQLite | 31 | 149 | — | — | 33 |
| Paheko 1.0.0 | SQLite | 32 | 167 | — | — | 33 |
| Paheko HEAD | SQLite | 54 | 309 | — | — | 49 |
| **Total banc** | | **1 384** | **15 048** | | | |
| *Galette (PostgreSQL)* | *PostgreSQL* | *31* | *194* | *0* | *0* | *24* |
| *`temoin/db/`* — test de fumée, **jamais** banc | *MariaDB* | *11* | *72* | *0* | *0* | *6* |

Les huit pivots passent les **neuf contrôles d'intégrité** de
[#128](https://github.com/AmauryTISSOT/microservice_rgpd/issues/128) : aucun trou
de `position`, aucun doublon `(schema, table, colonne)`, aucun champ manquant, et
pied de relevé égal au nombre de lignes dans tous les cas.

### Quatre écarts avec #125, mesurés et assumés

1. ⚠️ **SACoche porte 152 commentaires de colonne**, pas 8 au niveau table. #125
   les avait manqués. C'est le seul gisement de commentaires **en français** du
   corpus — les 647 d'OpenEMR sont en anglais médical — et c'est donc le seul
   endroit où l'exploitation du commentaire est mesurable pour un client français.
2. ⚠️ **PostgreSQL a désormais une couverture de banc.** #128 la notait absente.
   Galette livre son propre `pgsql.sql` : le **même** schéma rend **194 colonnes
   dans les deux dialectes**, ce qui éprouve la requête PostgreSQL et vérifie
   l'uniformité du pivot d'un dialecte à l'autre.
3. **SACoche : 98 tables et non 103.** Le logiciel livre **deux bases distinctes** —
   `_sql/structure/` (98 tables, les données de l'établissement) et
   `_sql/webmestre/` (6 tables, l'administration du service hébergé). Seule la
   première est au corpus : c'est celle qu'un établissement client déploie. Le
   chiffre reste un **plancher**, les migrations PHP annuelles n'ayant pas été
   lues — comme #125 l'avait déjà signalé.
4. **GLPI est en 11.0.9-dev**, pas 11.0.4 : c'est la version livrée par `HEAD` au
   jour du clonage.

## Provenance

⚠️ **Règle de provenance** ([#125](https://github.com/AmauryTISSOT/microservice_rgpd/issues/125)) :
aucun schéma, comptage ou licence n'entre ici sans **clonage et lecture directe**.
Toute donnée rapportée par un tiers non vérifié est traitée comme absente. Un
corpus est un instrument de mesure ; un chiffre inventé n'y est pas une erreur,
c'est un banc qui ment sans le dire.

Licences relevées dans le fichier `COPYING`/`LICENSE` du dépôt cloné, **jamais**
dans le badge affiché par la forge.

| Schéma | Source clonée | Commit | Licence (lue dans le dépôt) |
|---|---|---|---|
| Dolibarr | `github.com/Dolibarr/dolibarr` | `fa08bb6` | GNU GPL v3 (`COPYING`) |
| GLPI | `github.com/glpi-project/glpi` | `343a103` | GNU GPL v3 (`LICENSE`) |
| OpenEMR | `github.com/openemr/openemr` | `2bf9abe` | GNU GPL v3 (`LICENSE`) |
| Galette | `github.com/galette/galette` | `c8fb939` | GNU GPL v3 (`LICENSE.md`) |
| SACoche | `forge.apps.education.fr/sesamath/sacoche` | `13feaab` | GNU AGPL v3 (`COPYING`) |
| Paheko | `github.com/paheko/paheko` | `eaf5710` | GNU AGPL v3 (`COPYING`) |

**PrestaShop reste en réserve** et n'est pas ici, l'OSL-3.0 n'étant pas arbitrée.

### Licence du corpus

**CC BY-SA 4.0.** Le corpus ne redistribue pas le code amont : il porte des
**noms de tables et de colonnes relevés par introspection**, accompagnés
d'annotations produites à la main. Le versement est défendable au titre de la
directive 2009/24 art. 1(2) et de la CJUE *SAS Institute* pt 39, **à la condition
expresse** que l'extraction passe par le catalogue et non par les fichiers — ce
que `outils/extraire.sh` garantit et ce que la volumétrie ci-dessus démontre.

## Régime d'annotation

Les 15 048 colonnes ne s'annotent pas à la main. Le plan retenu combine deux
régimes ; le détail par schéma vit dans [`plan-de-sondage.json`](./plan-de-sondage.json).

| Régime | Schémas | Colonnes à annoter |
|---|---|---:|
| **Recensement** (intégral) | Galette, Paheko ×3, SACoche, `temoin` | 1 439 |
| **Sondage stratifié en grappes** | Dolibarr, GLPI, OpenEMR | 1 815 |
| | **Total** | **3 254** |

- La **grappe est la table**, jamais la colonne isolée : le pivot porte
  `commentaire_table` précisément parce qu'un commentaire de table éclaire toutes
  ses colonnes, et tirer des colonnes isolées priverait l'annotateur d'un contexte
  que le moteur, lui, recevra.
- ⚠️ **Paheko est annoté intégralement dans ses trois états**, sans exception.
  Ses trois versions ne valent que comparées à schéma constant ; un tirage
  indépendant dans chacune confondrait « la langue a changé » avec « le tirage a
  changé », et détruirait la seule variable isolée du corpus.
- Les strates viennent d'un **pré-criblage lexical grossier**, qui n'est **pas**
  une annotation et n'entre jamais au corpus. La **probabilité d'inclusion** de
  chaque grappe est enregistrée : sans elle, aucune prévalence ne serait
  estimable depuis un tirage biaisé à dessein.
- Le tirage est **rejouable** : seed figé à `20260808` dans
  `outils/echantillonner.py`.

## Ce que ce corpus ne pourra pas mesurer

Le détail et les motifs sont au § 5 du [protocole](./PROTOCOLE-ANNOTATION.md). En
un paragraphe, parce que personne ne doit le découvrir dans les chiffres :

⚠️ **`CriminalOffenceData` est à zéro** — un criblage large sur les 15 048
colonnes rend **0** candidat article 10. Le banc ne pourra rien en dire, et
[#137](https://github.com/AmauryTISSOT/microservice_rgpd/issues/137) en hérite.
**`SpecialCategoryData` tient en une dizaine d'instances**, trop peu pour une
précision ou un rappel. **`HealthData` est mono-schéma** : 509 des 521 candidats
sont chez OpenEMR, en anglais médical. Et **le corpus est propre** : le cas dur —
la base sédimentée de 2009 — n'est pas couvert.

⚠️ **Le bruitage *Valentine* a été écarté**, et le motif est écrit ici pour ne pas
être reposé : il n'aurait fabriqué **aucune** colonne article 10 — il abîme des
noms existants, il n'invente pas de sémantique — et aurait fait mesurer la
robustesse à une dégradation **de notre propre main** plutôt qu'à celle du réel.
Le trou est déclaré ; il n'est pas comblé par un artefact.

## Rejouer l'extraction

```bash
corpus/schemas/outils/extraire.sh          # clonage → conteneur → introspection
python3 corpus/schemas/outils/echantillonner.py   # plan de sondage + fichiers d'annotation
```

⚠️ Ces deux commandes **écrasent** `pivots/` et `annotation/`. Les relancer après
le début de l'annotation détruirait le travail humain : le fichier d'annotation
porte les étiquettes.
