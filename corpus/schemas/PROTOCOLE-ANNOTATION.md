# Protocole d'annotation du corpus de schémas

**Écrit avant la première annotation, et c'est sa raison d'être.** Un doute qu'on
ne tranche pas d'avance se tranche douze fois différemment au fil de
l'annotation, et le banc mesure alors cette incohérence-là plutôt que le moteur.

Ce protocole ne se relit pas au milieu du travail pour l'ajuster à un cas gênant :
s'il faut l'amender, l'amendement est daté, versionné, et **les colonnes déjà
annotées sont repassées**. Une règle changée en cours de route sans reprise
produit un corpus dont on ne sait plus, ligne par ligne, quelle règle l'a produit.

---

## 1. Ce que l'annotateur voit — et ce qu'il ne doit pas regarder

**L'annotateur annote depuis le pivot, et depuis lui seul.** Les neuf champs de
[`releves/`](../../releves/) sont exactement ce que le moteur recevra :

`schema` · `table` · `colonne` · `position` · `type` · `nullable` ·
`commentaire_colonne` · `commentaire_table` · `table_referencee`

⚠️ **Il est interdit d'ouvrir le code source du logiciel amont, sa documentation,
son interface ou ses écrans pour décider d'une étiquette.** Ce n'est pas une
économie d'effort, c'est la condition de validité du banc. Une vérité terrain
établie en lisant le code encoderait un savoir que la production ne donnera
**jamais** au moteur ; le banc mesurerait alors un écart à une cible inatteignable
par construction, et conclurait « le moteur est mauvais » là où il fallait lire
« le régime schéma-seul ne peut pas y arriver ». C'est le décalque exact du
résultat central de [#125](https://github.com/AmauryTISSOT/microservice_rgpd/issues/125) —
un corpus lu dans les fichiers DDL aurait donné au moteur un signal que
`information_schema` ne porte pas — appliqué cette fois à l'annotateur humain.

**La cible est donc explicitement : ce qu'un lecteur humain compétent et de bonne
foi déduit du seul relevé.** C'est le plafond du régime schéma-seul, et c'est la
seule cible contre laquelle [trancher le moteur](https://github.com/AmauryTISSOT/microservice_rgpd/issues/134)
ait un sens.

### Le registre des limites de méthode

Quand l'annotateur **sait par ailleurs** qu'une colonne porte davantage que son
nom ne dit — le cas `sacoche_livret_modaccomp_code`, dont les valeurs sont des PAI
et des PPS, donc des données de santé, derrière un nom parfaitement neutre — il
**n'en tient aucun compte pour l'étiquette** et inscrit la colonne dans
[`limites-de-methode.md`](./limites-de-methode.md), avec ce qu'il sait et d'où il
le sait.

Ce registre n'est pas de la vérité terrain : il ne sert jamais à mesurer. Il est
la matière de [la clause d'incomplétude](https://github.com/AmauryTISSOT/microservice_rgpd/issues/132),
qui hérite de la seconde dimension actée par [#127](https://github.com/AmauryTISSOT/microservice_rgpd/issues/127) —
non plus seulement les **sources** non regardées, mais les **catégories** que ce
régime ne peut pas atteindre. Il se remplit **au fil de l'eau et par hasard** ;
personne ne part à la chasse.

---

## 2. L'étiquette

Une colonne reçoit **une** valeur de `PersonalDataCategory`, et une seule. La
taxonomie et son ordre d'arbitrage sont ceux de
[`docs/contexts/screening/CONTEXT.md`](../../docs/contexts/screening/CONTEXT.md),
tranchés par [#127](https://github.com/AmauryTISSOT/microservice_rgpd/issues/127).
**L'annotateur hérite de cet ordre ; il ne le redécide pas.**

`CriminalOffenceData` > `HealthData` > `SpecialCategoryData` > `AuthenticationSecret` >
`NationalIdentifier` > `FinancialData` > `LocationData` > `ConnectionData` >
`Identity` > `ContactDetails` > `ProfessionalLife` > `PersonalDataUncategorised` >
`Unflagged`

Quand plusieurs valeurs conviennent — `arret_maladie` est santé *et* vie
professionnelle — **la plus haute du tableau l'emporte**, et le motif dit ce qui a
été écarté.

**Le motif est obligatoire dès que la ligne est signalée**, et il est en prose
française. Il dit *pourquoi*, pas *quoi* : « préfixe `adr` reconnu », « la table
est commentée “suivi médical” », « `varchar(320)`, longueur canonique d'un
courriel ». Un motif est ce qui rend l'étiquette **contestable** par le second
codeur ; sans lui le double codage ne mesure rien.

⚠️ **La règle qui départage les deux replis est mécanique et se teste :
motif présent ⇔ ce n'est pas `Unflagged`.**

- `Unflagged` — rien vu. **Pas de motif.** Ce n'est pas « cette colonne est
  inoffensive » : c'est un constat sur le dépistage, jamais sur la donnée.
- `PersonalDataUncategorised` — vu, personnel, mais aucune autre valeur ne va.
  **Motif obligatoire.** C'est un verdict, pas un aveu.

---

## 3. Les cas douteux, tranchés d'avance

Les règles ci-dessous sont **exhaustives pour les familles qu'elles nomment**. Un
cas qui n'y entre pas se porte au journal des arbitrages (§ 6) avant d'être
tranché, jamais après.

### 3.1 Colonnes techniques

| Famille | Étiquette | Motif |
|---|---|---|
| `id`, clé primaire auto-incrémentée | `Unflagged` | Un numéro de ligne interne n'identifie une personne que par la table qui l'héberge, et cette table est déjà annotée pour elle-même. L'y compter ferait entrer toute clé primaire du schéma. |
| `created_at`, `updated_at`, `date_creation`, `tms` | `Unflagged` | Horodatage de l'écriture, pas un fait sur la personne. |
| `fk_user_creat`, `user_modif` et assimilés | `Identity` | ⚠️ **Exception assumée** : ces colonnes désignent **l'agent**, une personne physique salariée, et le RGPD ne distingue pas les personnes concernées selon qu'elles sont clients ou employés. Motif : « désigne l'utilisateur auteur de l'écriture ». |
| `entity`, `rowid`, `import_key`, `deleted`, `active`, `status` | `Unflagged` | Mécanique applicative. |

### 3.2 Clés étrangères

**Une clé étrangère s'annote sur ce qu'elle *désigne*, pas sur ce qu'elle
*stocke*.** `fk_soc` stocke un entier ; il désigne une société.

- FK vers une table de **personnes** (`user`, `adherent`, `eleve`, `patient`,
  `societe`, `contact`) → `Identity`, motif citant `table_referencee` ou le nom.
- FK vers une table de **nomenclature** (`c_pays`, `type_contact`,
  `list_options`) → `Unflagged`.
- ⚠️ **`table_referencee` est vide dans une bonne part du corpus** — GLPI,
  OpenEMR et SACoche ne déclarent **aucune** contrainte de clé étrangère (mesuré :
  0 sur les trois). L'annotateur se rabat alors sur le nom, exactement comme le
  moteur devra le faire. **Il ne va pas chercher la contrainte dans le code.**

### 3.3 Champs libres — le cas dur

`commentaire`, `note`, `description`, `libelle`, `observation`, `remarque`,
`memo`, `note_public`, `note_private`.

⚠️ **Un champ libre *peut* tout contenir, y compris de l'article 9.** C'est
précisément pourquoi il ne faut pas l'étiqueter sur ce qu'il pourrait contenir :
tout champ libre deviendrait alors `HealthData`, et la mesure s'effondrerait.

**Règle** — l'étiquette dépend de ce que la table dit du champ, jamais de son
potentiel :

1. Le champ libre est dans une table **de personnes** (au sens § 3.2) →
   `PersonalDataUncategorised`, motif : « champ libre rattaché à une personne ;
   contenu indéterminable depuis le schéma ». C'est exactement l'emploi pour
   lequel ce repli existe.
2. Le champ libre est dans une table **sans personne** (`produit`, `stock`,
   `ecriture_comptable`) → `Unflagged`.
3. Le champ libre porte une qualification **explicite** dans son nom ou dans un
   commentaire — `note_medicale`, `commentaire_sante` → la catégorie que cette
   qualification nomme.

⚠️ **Le taux de repli est un instrument, pas un défaut** ([#127](https://github.com/AmauryTISSOT/microservice_rgpd/issues/127)).
Un `PersonalDataUncategorised` massif sur les champs libres est un **résultat**
du banc, à publier tel quel — pas un signe qu'il faut inventer une valeur.

### 3.4 Tables de configuration et de nomenclature

Une table qui ne porte **aucune** donnée personnelle — `c_pays`, `c_tva`,
`llx_const`, `glpi_configs`, tables de référence — voit **toutes** ses colonnes
en `Unflagged`.

⚠️ **Elles restent dans le corpus et ne sont jamais retirées.** Ce sont les vrais
négatifs : sans elles, la précision n'est pas mesurable, et GLPI est au corpus
pour cette raison précise. Un corpus dont on a ôté les tables ennuyeuses mesure un
moteur sur un monde qui n'existe pas.

### 3.5 Le nom mensonger

`iban_prefix` chez Dolibarr ne porte pas d'IBAN. **L'annotateur étiquette ce que
le nom dit**, et signale la divergence au registre des limites de méthode (§ 1) si
et seulement s'il la connaît par ailleurs. Il ne part pas la vérifier.

### 3.6 Données d'une personne morale

Le RGPD ne protège que les personnes physiques. Mais une base de gestion
française ne sépare pas les deux : `llx_societe` porte des entreprises **et** des
professionnels indépendants, et le schéma ne le dit pas.

**Règle** — on étiquette **comme personnel** dès que la colonne peut concerner une
personne physique, et le motif le dit : « peut désigner un professionnel
indépendant ». Motif du choix : le régime d'erreur du contexte est l'`Omission
relue`, où l'omission coûte et le faux positif s'écarte d'un geste.

### 3.7 Colonnes mortes mais vivantes

Les colonnes marquées `deprecated` / `not used` (119 chez Dolibarr) s'annotent
**normalement**. Elles existent dans la base du client, portent encore leurs
données, et un recensement RGPD s'y intéresse exactement autant.

---

## 4. Le double codage

> ### ⚠️ Amendement du 2026-08-09 — le double codage est **intra**-annotateur
>
> Ce paragraphe supposait deux personnes. Il n'y en a qu'une, et le dépôt n'a
> personne d'autre à qui faire lire ce protocole. Plutôt que de laisser la
> contradiction dormir jusqu'aux chiffres, elle est tranchée ici, avant la
> première étiquette : **aucune colonne n'était annotée au moment de cet
> amendement**, il n'y a donc rien à repasser.
>
> **Le montage retenu** — le même annotateur code les 3 254 colonnes, puis
> reprend les 300 de l'échantillon depuis le cahier vierge, **une fois le corpus
> entièrement annoté**. Le délai n'est pas décoratif : il est le seul rempart
> contre le souvenir, et le faire courir jusqu'à la fin de l'annotation lui donne
> sa longueur maximale gratuitement. La dilution aide — 300 colonnes noyées dans
> 3 254, on ne se rappelle pas avoir tranché `llx_societe.fk_typent`.
>
> ⚠️ **Ce qui est mesuré change de nom, et il faut le publier sous son vrai
> nom : c'est un accord *intra*-annotateur, pas *inter*.** Il mesure la
> **constance d'une personne**, pas la **reproductibilité du protocole par une
> autre**. Ce ne sont pas les mêmes quantités et l'une ne s'achète pas avec
> l'autre.
>
> ⚠️ **Et il penche dans le sens qui flatte.** Une personne s'accorde avec
> elle-même plus qu'avec autrui : l'accord mesuré ici est donc **majoré**, et le
> désaccord — le bruit — **minoré**. Or c'est le bruit qui sert de plancher au
> banc. Un plancher sous-estimé place la barre **trop bas** et créditerait le
> moteur d'avoir battu un écart qu'aucun second codeur n'aurait validé.
> **Le chiffre publié est donc une borne optimiste**, et
> [#130](https://github.com/AmauryTISSOT/microservice_rgpd/issues/130) doit le
> reprendre comme tel — jamais comme le bruit inter-annotateur qu'il n'est pas.
>
> **Ce que ce montage ne rattrape pas**, et qu'on n'ira pas prétendre : une règle
> du § 3 comprise de travers restera comprise de travers aux deux passes, et
> l'accord sera **excellent**. Un accord intra-annotateur élevé ne dit rien de la
> justesse du protocole ; il ne dit que sa stabilité. La seule chose qui lèverait
> ce doute est un second lecteur, et il n'y en a pas.
>
> Si un second codeur devient disponible, ce montage se remplace par celui
> d'origine ci-dessous — c'est un nouvel amendement, et les 300 colonnes sont
> recodées.

**Sur un échantillon de 300 colonnes**, tiré au hasard parmi les 3 254 à annoter,
tirage stratifié par schéma pour qu'aucun ne soit absent. ⚠️ **Le tirage se fait
avant la première étiquette** — un échantillon tiré après coup se choisit, même
de bonne foi, en connaissance de ce qu'il contient. Il est figé par une graine et
se rejoue à l'identique : `outils/tirer-double-codage.py`.

⚠️ **La seconde passe se fait en aveugle** : elle ne voit ni l'étiquette ni le
motif de la première. Une seconde passe qui relit la première ne mesure pas
l'accord, elle mesure la docilité. Cette garantie est **structurelle** et non
disciplinaire : `double-codage/seconde-passe.jsonl` ne porte que les neuf champs
du pivot, et ne *contient pas* de quoi tricher.

**Ce qu'on publie**, sans exception et avant tout chiffre de performance du
moteur :

- l'**accord brut** (part des colonnes où les deux étiquettes coïncident) ;
- le **kappa de Cohen** sur les treize valeurs ;
- l'accord **restreint aux colonnes signalées** — l'accord global est gonflé par
  la masse des `Unflagged`, et le publier seul serait trompeur ;
- la **matrice des désaccords**, qui dit *quelles* valeurs se confondent.

Ces quatre chiffres sortent de `outils/accord.py`.

⚠️ **Le bruit de l'annotation borne ce que le banc peut prétendre.** Un moteur qui
« bat » un écart inférieur au désaccord entre deux passes n'a rien battu du tout.
Ce chiffre se lit **avant** les résultats du moteur, et
[le protocole de mesure](https://github.com/AmauryTISSOT/microservice_rgpd/issues/130)
le reprend comme plancher — **optimiste**, au sens de l'amendement ci-dessus.

Les désaccords sont réconciliés en reprenant la règle du § 3 qui aurait dû
trancher ; l'exercice **amende ce protocole** s'il révèle une règle manquante, et
les colonnes déjà annotées sont repassées.

---

## 5. Ce que ce corpus ne pourra pas mesurer

Écrit ici, en clair, pour que personne ne le découvre dans les chiffres.

1. ⚠️ **`CriminalOffenceData` : zéro.** Un criblage lexical large sur les 15 048
   colonnes du corpus rend **0** candidat article 10. Le banc ne pourra **rien**
   dire de cette valeur — ni rappel, ni précision, ni même qu'elle se déclenche.
   C'est un trou de corpus, et [#137](https://github.com/AmauryTISSOT/microservice_rgpd/issues/137)
   en hérite.
2. ⚠️ **`SpecialCategoryData` : de l'ordre de dix instances** dans l'échantillon
   d'annotation, presque toutes chez OpenEMR et SACoche. Aucune précision ni aucun
   rappel n'est estimable sur un tel effectif ; tout au plus pourra-t-on dire que
   la valeur se déclenche ou ne se déclenche pas.
3. ⚠️ **`HealthData` est un phénomène mono-schéma.** 509 des 521 candidats santé
   du corpus sont chez OpenEMR, **en anglais et en abréviations médicales**. Un
   moteur qui réussit la santé sur ce corpus a réussi la santé *en anglais chez
   OpenEMR* ; rien ne dit qu'il la réussira en français.
4. ⚠️ **Le corpus est propre.** Pas de `ZZ_TMP_OLD`, un seul `CLI_NOM_1`
   authentique (`sacoche_user.ID_NATIONAL`). Le cas dur — la base sédimentée que
   le client collera — **n'est pas couvert**, et le bruitage *Valentine* a été
   écarté : il n'aurait fabriqué aucune colonne article 10, et aurait mesuré la
   robustesse à une dégradation **de notre main** plutôt qu'à celle du réel.
5. **Aucune couverture PostgreSQL en langue autre que celle de Galette** — la
   requête PostgreSQL est éprouvée sur un seul schéma, celui de Galette, qui est
   par ailleurs déjà au corpus en MariaDB.

---

## 6. Journal des arbitrages

Tout cas qui n'entre dans aucune règle du § 3 s'inscrit dans
[`journal-arbitrages.md`](./journal-arbitrages.md) **avant** d'être tranché : la
colonne, la question, la décision, la date. Quand le journal fait apparaître une
famille, elle remonte en § 3 par amendement daté, et les colonnes concernées sont
repassées.
