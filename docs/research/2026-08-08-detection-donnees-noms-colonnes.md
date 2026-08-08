# État de l'art : détecter des données personnelles à partir de noms de colonnes

> Recherche pour l'issue [#124](https://github.com/AmauryTISSOT/microservice_rgpd/issues/124), enfant de la carte [#122](https://github.com/AmauryTISSOT/microservice_rgpd/issues/122). **Ce document ne choisit pas le moteur.** Il expose les familles et ce qui les départage, pour alimenter le protocole de mesure et le banc d'essai à venir.
>
> Date : 2026-08-08. Sources primaires uniquement — papiers arXiv/ACM/VLDB/IEEE, documentation d'éditeur, code source lu. La section finale liste ce qui n'a **pas** pu être vérifié en source primaire.

---

## 0. Le résultat qui commande tout le reste

**La littérature de *semantic type detection* — Sherlock, Sato, DoDuo, TURL, Pylon et leurs successeurs — travaille sur les valeurs des colonnes, pas sur leurs noms.** Ce n'est pas un détail de mise en œuvre : c'est une position revendiquée, argumentée, et l'argument avancé contre les noms est **exactement notre problème**.

Sherlock l'écrit noir sur blanc, avec un exemple qui pourrait sortir d'un schéma client :

> « missing headers or incomprehensible headers are not uncommon. For example, SAP's system table `T005` contains country information and column `NMFMT` is the standard name field, whereas `INTCA` refers to the ISO code or `XPLZS` to zip-code. »
> — [Sherlock, KDD 2019, § 1](https://arxiv.org/abs/1905.10688)

`NMFMT`, `INTCA`, `XPLZS`. C'est `dt_naiss`, `CLI_NOM_1`, `usr_mail_1` dans une autre langue. **L'état de l'art le plus cité de la discipline a regardé notre signal et l'a jugé trop pauvre pour être son entrée.**

La conséquence, pour la carte #122, est double et il faut la porter des deux côtés :

1. **Décharge.** La décision de cadrage 1 (« le service ne lit que le schéma ») ne se heurte à aucun résultat qui dirait « les noms suffisent ». Elle se heurte à une littérature qui n'a **jamais mesuré les noms seuls**, parce qu'elle a choisi l'autre branche. Il n'y a pas de chiffre à battre ; il n'y a pas non plus de chiffre sur lequel s'appuyer.
2. **Charge.** Les seules mesures existantes sur les noms seuls viennent d'une tâche voisine (l'**expansion** de noms abrégés, § 3) et d'un travail d'évaluation en appariement de schémas (§ 5), et elles sont **basses ou fragiles**. Le banc d'essai de #122 devra donc produire ses propres chiffres — personne ne les a produits pour lui.

---

## 1. Papier par papier : quelle est l'entrée exacte du modèle ?

C'est la question décisive du ticket. Réponse, source par source.

| Travail | Entrée exacte du modèle | Le nom de colonne y est-il ? |
| --- | --- | --- |
| **Sherlock** (KDD 2019) | 1 588 traits extraits des **valeurs** : distributions de caractères, plongements de mots des valeurs, vecteurs de paragraphe, statistiques globales | **Non.** Les en-têtes servent uniquement à **fabriquer les étiquettes** de vérité terrain |
| **Sato** (VLDB 2020) | Sherlock **+ contexte de table** : un modèle de sujets (LDA) sur les **valeurs agrégées de toute la table**, plus un CRF entre colonnes | **Non** |
| **DoDuo** (SIGMOD 2022) | Sérialisation `[CLS] v11 … [CLS] v1n … vmn [SEP]` — un `[CLS]` par colonne, suivi des **valeurs** de cette colonne, dans BERT | **Non** — variante `Doduo+metadata` seulement (voir plus bas) |
| **TURL** (VLDB 2020) | Pré-entraînement sur tables Wikipédia ; la représentation d'entrée inclut légende, en-têtes et cellules-entités | **Partiellement** — non vérifié en détail (§ 10) |
| **Pylon** (arXiv 2301.04901) | **Ce n'est pas un détecteur de type sémantique.** C'est de la *table union search* par apprentissage contrastif auto-supervisé sur les **valeurs** des colonnes | **Non** |
| **CTA par ChatGPT** (Korini & Bizer, 2023) | Prompt contenant **les valeurs des cinq premières lignes** | **Non** (selon la lecture faite ici — voir § 10) |

### 1.1 Sherlock : les en-têtes sont l'étiquette, jamais l'entrée

[Sherlock](https://arxiv.org/abs/1905.10688) construit son corpus d'entraînement par **appariement exact entre 78 types sémantiques de DBpedia et les en-têtes de colonnes** de VizNet, ce qui donne 686 765 colonnes. Puis :

> « We consider each column as a mapping from column values to a column header. […] Treating column headers as ground truth labels of the semantic type, we formulate semantic type detection as a multiclass classification problem. »

Autrement dit : **l'en-tête est la cible, les valeurs sont l'entrée.** Un système entraîné ainsi ne sait rien faire d'un nom de colonne à l'inférence.

Les 78 types incluent `address`, `birth date`, `name`, `person`, `city`, `country` — la taxonomie est proche de ce que #122 voudra.

### 1.2 Sato : le « contexte » est fait de valeurs, pas de métadonnées

[Sato](https://arxiv.org/abs/1911.06311) ajoute deux choses à Sherlock : un **sujet de table** obtenu par modèle LDA, et une **prédiction structurée** (CRF) qui fait dialoguer les colonnes d'une même table. Le mot « contexte » induit en erreur : le sujet de table est calculé sur les **valeurs agrégées de la table**, pas sur le nom de la table ni sur les en-têtes.

C'est un point qui vaut d'être noté pour #122 : l'idée « une colonne s'interprète mieux au vu de ses voisines » est validée dans la littérature — mais elle y est validée **sur les valeurs**. Personne n'a mesuré l'équivalent sur les noms (« `nom` à côté de `prenom` et `dt_naiss` est plus sûrement un nom de personne que `nom` à côté de `ref_produit` »). C'est une piste ouverte pour le banc, pas un acquis.

### 1.3 DoDuo : le nom ajouté aux valeurs ne rapporte presque rien

[DoDuo](https://ar5iv.labs.arxiv.org/html/2104.01785) sérialise la table entière et n'y met que des valeurs :

> « the column type/relation prediction models of Doduo *only* considers the table content (i.e., cell values) as input. »

Les auteurs testent pourtant une variante, **`Doduo+metadata`**, qui « appends the column name to column values for each column before serialization ». Le gain mesuré, sur WikiTable :

| | Type de colonne (F1) | Relation entre colonnes (F1) |
| --- | --- | --- |
| Doduo | 92,45 | 91,72 |
| Doduo+metadata | 92,79 | 92,82 |

**+0,34 point sur le type.** Le nom de colonne, ajouté à des valeurs déjà abondantes, est un signal **quasi nul**.

⚠️ **Ce chiffre ne dit pas ce qu'on aimerait qu'il dise.** Il mesure l'apport *marginal* du nom quand les valeurs sont déjà là et saturent la tâche. Il ne mesure **pas** ce que le nom vaut seul. L'expérience symétrique — retirer les valeurs, garder le nom — n'existe dans aucun des papiers lus. C'est précisément le trou que le banc de #122 devra combler, et il faut résister à la tentation de lire « +0,34 » comme « le nom ne vaut rien ».

Détail d'exploitation : DoDuo est un BERT-base 12 couches, entraîné 30 époques sur un V100 16 Gio (AWS p3.8xlarge). Aucun chiffre de latence à l'inférence n'est publié.

### 1.4 Pylon n'est pas ce que le ticket croyait

Le ticket range Pylon dans la lignée Sherlock/Sato/DoDuo. Vérification faite, [Pylon](https://arxiv.org/abs/2301.04901) (Cong, Nargesian, Jagadish) résout la **recherche de tables unionables dans un lac de données** par apprentissage contrastif auto-supervisé, et le contraste porte sur les **valeurs** : le modèle rapproche les colonnes « with semantically similar values » et éloigne les autres. Ce n'est ni un détecteur de type sémantique, ni un travail sur les noms.

**Correction à porter au dossier de la carte** : Pylon ne fournit rien à #122 au-delà de confirmer, une fois de plus, que le signal retenu par la discipline est la valeur.

### 1.5 Les LLM sur les tables : les valeurs, toujours

[Korini & Bizer, « Column Type Annotation using ChatGPT »](https://arxiv.org/abs/2306.00745) — évalué sur un sous-échantillon de SOTAB (32 types, 4 domaines), avec des prompts qui présentent « the first five rows of a table ». Zéro-tir avec instructions : 85,25 % F1 ; pipeline en deux étapes : 89,47 %.

Même dans la génération LLM, **la valeur reste l'entrée**, y compris quand le format de prompt s'appelle « table ».

---

## 2. Le seul travail récent qui vise exactement notre problème : la détection *contextuelle* de données sensibles

C'est la trouvaille la plus directement exploitable de cette recherche, et elle est récente.

[**« Towards Contextual Sensitive Data Detection »**, Telkamp & Hulsebos, arXiv 2512.04120](https://arxiv.org/html/2512.04120) — Madelon Hulsebos est la première autrice de Sherlock, ce qui donne au dossier une continuité rare : la même chercheuse qui avait écarté les noms de colonnes en 2019 revient en 2025 sur la détection de données sensibles, et y remet les noms.

**Ce que fait le travail.** Classification de sensibilité **colonne par colonne**, en deux mécanismes : *type contextualization* (détecter le type PII, puis réévaluer la sensibilité réelle au vu de la table entière) et *domain contextualization* (retrouver la règle de domaine applicable, puis classer).

**L'entrée par colonne** : « column name and five values » pour la détection de type ; puis, pour la réflexion, « a markdown-formatted representation of the full table, including headers and five sample rows per column ». **Le nom de colonne est donc bien un signal de premier rang, mais jamais seul.**

**Les chiffres qui nous concernent** (66 tables réelles de GitTables, 2 061 colonnes annotées à la main) :

| Système | Précision | Rappel | F1 |
| --- | --- | --- | --- |
| Google Cloud DLP | 0,53 | 0,63 | **0,58** |
| Microsoft Presidio | 0,52 | 0,62 | **0,57** |
| Qwen3 8B affiné → GPT-4o-mini | 0,90 | 0,86 | **0,88** |

⚠️ **Deux outils industriels de référence plafonnent à F1 ≈ 0,57–0,58 sur des tables réelles.** C'est un point de comparaison précieux pour le critère de #122, et il est bas. Il faut cependant le lire avec sa réserve : ces outils y étaient nourris de **valeurs** (leur mode nominal), pas de noms — la comparaison mesure la difficulté de la tâche, pas la qualité de notre signal.

**L'argument central du papier est un argument contre le lexique pur**, et il vise notre décision de cadrage 6 (le motif en prose) autant que le moteur :

> « An address may be harmless in the context of a public organizational website but expose a private address in another document, causing naïve type-based detection yield false positives. »

Traduit dans le vocabulaire de #122 : une colonne `adr_l1` dans une table `FOURNISSEUR` et la même dans une table `CLIENT_PARTICULIER` n'ont pas la même charge. Un moteur qui ne regarde que le nom de la colonne, sans le nom de la table, se trompera **systématiquement et silencieusement** sur cette classe de cas. La sortie de #122 étant une *catégorie de données* et non un booléen de sensibilité, cet écueil est atténué mais pas supprimé.

**Le coût, publié — et c'est rare** (§ 7) :

| Modèle | Latence | Mémoire |
| --- | --- | --- |
| GPT-4o-mini | 1,20 s / colonne | (API, 0,05 $/table) |
| Qwen3 8B | **0,46 s / colonne** | **8,0 Gio de GPU** |
| Qwen3 14B | 1,62 s / colonne | 11,4 Gio de GPU |

⚠️ **Lecture pour ce dépôt, à porter au protocole de mesure.** `qwen3:8b` sur 8 Gio de VRAM, c'est **exactement** la configuration de production décrite par l'[ADR-0001](../adr/0001-architecture-polyglotte-et-moteur-auto-heberge.md) et par la carte #122. 0,46 s par colonne sur un GPU **déjà pris et sérialisé par la `Qualification`** : un schéma de 800 colonnes coûterait ~6 minutes de GPU exclusif, pendant lesquelles toute demande de qualification attend. Ce n'est pas une latence, c'est la contention que le § 5.8 de la spec a déjà nommée. Le chiffre est un renseignement, pas un verdict : il dit ce qu'un moteur LLM coûterait, il ne dit pas qu'un lexique fera mieux en exactitude.

Enfin : **aucune configuration nom-seul** n'est évaluée, et **aucune langue autre que l'anglais**.

---

## 3. Les familles de méthodes sur le signal « nom »

### 3.1 Lexique et règles

C'est ce que fait l'industrie, sans exception (§ 6). Mécanique : une table `motif → catégorie`, appliquée en insensible à la casse sur le nom de colonne.

Ce que la littérature en dit, quand elle daigne le mesurer : Sherlock compare deux lignes de base d'appariement, un **dictionnaire** (les 1 000 valeurs les plus fréquentes par type) et des **expressions régulières apprises** — mais **sur les valeurs**, pas sur les noms. Résultats : F1 0,16 et 0,04, contre 0,89 pour Sherlock. **Ces chiffres ne transposent pas à un lexique de noms** et ne doivent pas être cités comme s'ils le faisaient : un dictionnaire de valeurs et un lexique de noms de colonnes ne font pas le même travail. Ce qui transpose, c'est le **coût** : 0,01 s par colonne, 0,5 Mo de modèle, contre 0,42 s et 6,2 Mo pour Sherlock (§ 7).

Force : coût nul, déterminisme total, **motif en prose trivial à produire** — « préfixe `adr` reconnu » (décision de cadrage 6). Faiblesse : couverture bornée à ce qui est écrit, et faux positifs par sous-chaîne, dont l'industrie fournit les meilleurs exemples documentés (§ 6.2).

### 3.2 Morphologie et segmentation d'identifiants

Le génie logiciel a une littérature dédiée, vieille de vingt ans, sur le découpage d'identifiants (`dtNaissCli`, `dtnaiss`) et l'expansion d'abréviations. **Elle ne parle pas de bases de données, mais elle parle exactement de notre chaîne de caractères.**

**L'ampleur du problème est mesurée.** Markovtsev et al. ([arXiv 1805.11651](https://arxiv.org/pdf/1805.11651)), sur le Public Git Archive (182 014 dépôts) : **97 % des jetons de nommage sont multi-parties**, **7,5 % des identifiants ne sont pas découpables par heuristique de style**, et **15 % du reste contiennent encore des parties découpables après heuristique**. `dt_naiss` et `CLI_NOM_1` sont dans le régime où la casse ne porte aucune information.

**Ligne de base : délimiteurs et camelCase.** Découpe aux séparateurs durs, aux chiffres et aux transitions de casse. Coût linéaire, aucune table. C'est ce que fait `split_column_name` d'OpenMetadata (§ 6.2), et c'est l'état de l'art *industriel*.

**Samurai** ([Enslen, Hill, Pollock, Vijay-Shanker, MSR 2009](https://www.ptidej.net/courses/inf6306/fall09/resources/EnslenandHillandPollockandVijayShanker.pdf)) — le pivot académique. Deux tables de fréquences de sous-chaînes, une spécifique au programme et une globale minée sur ~9 000 programmes, avec amortissement logarithmique de la fréquence globale. Un découpage n'est accepté que si le score amorti de chacune des deux sous-chaînes dépasse celui de la chaîne entière.

⚠️ **Propriété capitale pour le français** : *« We require no predefined dictionary. »* **Samurai n'est pas lexical, il est statistique.** Il ne connaît aucune langue ; il connaît la distribution des sous-chaînes de son corpus.

Mais ses chiffres, lus dans le détail, sont une douche froide pour notre cas. Sur un jeu de référence de 8 466 jetons :

| Technique | Exactitude globale | **Découpes mono-casse réussies (sur 249)** | Sur-segmentation |
| --- | --- | --- | --- |
| Aucune découpe | 75,4 % | 0 | — |
| Greedy (à dictionnaire) | 95,3 % | **125** | 10 % |
| camelCase | 96,8 % | — | — |
| **Samurai** | **97,0 %** | **29** | **1 %** |

⚠️ **Le chiffre global est trompeur** : 6 391 des 8 466 jetons ne demandent aucune découpe. Sur la sous-population qui nous intéresse — **les jetons mono-casse, c'est-à-dire `dtnaiss`** — Samurai réussit 29 cas sur 249, contre 125 pour un greedy à dictionnaire. **La méthode agnostique à la langue est celle qui échoue le plus sur notre morphologie ; celle qui réussit exige un dictionnaire.** C'est le nœud du problème français, et il est mesuré.

**Ronin**, successeur de Samurai dans [Spiral](https://github.com/casics/spiral) (Hucka, Caltech), ajoute précisément ce qui manquait : dictionnaires **NLTK `words` + WordNet**, table de termes techniques en dur, fréquences minées sur 46 000 projets. Exactitude 92,09 % sur INTT, 84,42 % sur Ludiso. **Ronin est donc explicitement anglophone**, là où Samurai ne l'était pas.

**TIDIER / TRIS** ([Guerrouj, thèse Polytechnique Montréal 2013](https://publications.polymtl.ca/1203/1/2013_LatifaGuerrouj.pdf) ; [TIDIER, JSEP 25(6) 2013](https://onlinelibrary.wiley.com/doi/10.1002/smr.539)) — l'approche contextuelle, et **le chiffre le plus important de cette section**. TIDIER traite l'identifiant comme un signal de parole et l'apparie aux mots d'un dictionnaire par *dynamic time warping* à coûts de transformation, avec des dictionnaires spécialisés par niveau de contexte (fonction, fichier, application, domaine). Sur un jeu d'identifiants **difficiles en C, sans camelCase** :

| Approche | Découpes correctes |
| --- | --- |
| camelCase | **30 %** |
| Samurai | **31 %** |
| **TIDIER** (dictionnaire programme + domaine) | **54 %** |

⚠️ **+23 points, et ils ne viennent pas de l'algorithme : ils viennent du dictionnaire de domaine.** C'est le message convergent de toute cette littérature (voir aussi Cupid, § 5.2). Pour l'expansion, TIDIER atteint 48 % de précision sur 73 abréviations. Coût : la thèse énonce que **la complexité cubique de TIDIER est sa limitation principale**, ce qui a motivé TRIS (recherche de chemin de coût minimal dans une arborescence de transformations, bien plus rapide).

**Un résultat côté humain qui vaut avertissement.** [Guerrouj et al., EMSE 2013](https://link.springer.com/article/10.1007/s10664-013-9260-1) mesure que **le niveau d'anglais des participants a un effet statistiquement significatif** sur leur exactitude de découpe et d'expansion, et que les mots pleins sont correctement traités dans plus de 81 % des cas contre **deux à quatre fois moins pour les abréviations**. Même chez l'humain, la connaissance de la langue sous-jacente est le facteur limitant, et l'abréviation est le vrai mur.

**Coût machine** : ⚠️ **aucun chiffre publié en ms/identifiant, ni pour Samurai ni pour Ronin.** Le papier MSR 2009 ne dit que « the frequency analysis runs in linear time with respect to the number of strings ». Ce sont des algorithmes de chaîne sur tables de hachage, donc négligeables à notre échelle — mais c'est une déduction, pas un résultat.

### 3.3 Expansion d'abréviations par modèle de langue : NameGuess et Columbo

C'est la famille la plus proche de notre besoin, et la seule qui publie des chiffres **sur les noms de colonnes eux-mêmes**. Elle ne classe pas — elle **expanse** : `dt_naiss` → *date de naissance*. Un lexique appliqué ensuite sur la forme expansée devient un moteur en deux temps.

**[NameGuess](https://arxiv.org/html/2310.13196v1) (Zhang et al., EMNLP 2023).** Définit la tâche comme une génération conditionnée : `y = f(x | t)` où `x` est le nom abrégé et `t` le contexte de table (nom de table, autres en-têtes, valeurs échantillonnées). Corpus d'entraînement de **384 333 paires** fabriquées synthétiquement (noms « propres » filtrés par WordNet, puis abrégés par table de correspondance et règles de troncature/suppression de voyelles) ; banc d'évaluation humain de **9 218 exemples** issus des données ouvertes de Los Angeles, San Francisco et Chicago, 895 tables.

Le tableau qui nous intéresse — **avec et sans le contenu de la table**, c'est-à-dire *nom + contexte* contre *nom seul* :

| Modèle | Avec contenu (EM %) | **Sans contenu (EM %)** |
| --- | --- | --- |
| GPT-2 124M (affiné) | 25,7 | **10,4** |
| GPT-Neo 2,7B (affiné) | 43,8 | **40,6** |
| Falcon-40B | 53,4 | **51,2** |
| LLaMA-65B | 56,2 | **53,3** |
| GPT-4 | 73,4 | **69,3** |
| Humain | 43,4 | — |

⚠️ **C'est le seul chiffre nom-seul de tout ce dossier, et il est instructif dans les deux sens.** D'une part, un gros modèle atteint **69,3 % d'exactitude exacte sur le nom seul** — le nom porte donc beaucoup. D'autre part, le petit modèle s'effondre (25,7 → 10,4) : **la capacité du modèle achète la robustesse au manque de contexte**, ce qui est exactement la mauvaise nouvelle pour un dépôt dont le CPU et le GPU sont pris.

Réserves à porter : (a) les paires d'entraînement sont **fabriquées**, donc les abréviations suivent des règles plus régulières que la réalité ; (b) le banc humain est en **anglais**, tiré de données ouvertes américaines ; (c) **aucune mention du multilingue**, le vocabulaire de référence étant WordNet anglais ; (d) affinage sur **8 A100** ; aucun chiffre de latence.

L'exemple canonique du papier vaut d'être cité, il donne la mesure du problème :

> « the maximum length for column names in an SQL database is 256 bytes, leading to the use of abbreviations such as `D_ID` for *Department ID* and `E_NAME` for *Employee Name*. »

**[Columbo](https://arxiv.org/html/2508.09403v1) (2025, EMNLP Findings).** Successeur direct, et il est **entièrement dans notre cadre** :

> « In numerous application contexts, we cannot access the data tuples of the tables, for reasons of privacy, compliance, performance, etc. »

C'est mot pour mot la décision de cadrage 1 de #122, formulée par une équipe de recherche. Columbo n'utilise donc **pas les valeurs** ; il exploite quatre signaux de contexte, tous métadonnées : nom de la table, schémas de tables apparentées (jusqu'à 100 tables du même groupe), résumés de groupe, et un raisonnement pas-à-pas au niveau du jeton.

Résultats (exactitude exacte), sur cinq jeux dont trois d'entreprise :

| Jeu | NameGuess | Columbo |
| --- | --- | --- |
| NameGuess (public) | 81,5 % | 85,2 % |
| Finance (privé) | 73,8 % | **93,3 %** |
| University (privé) | 68,4 % | **92,2 %** |
| Adventure Works | 63,2 % | 87,6 % |
| EDI | 70,0 % | 89,6 % |

Ablation sur University : sans contexte 76,6 % (−15,6), sans les règles 63,6 % (−28,6), sans raisonnement pas-à-pas 85,4 % (−6,8), sans analyse par jeton 87,9 % (−4,3).

⚠️ **Deux réserves lourdes, à porter au banc.** (a) Columbo tourne **exclusivement sur GPT-4o** (`gpt-4o-2024-08-06`) — service tiers, donc **incompatible avec l'auto-hébergement strict de l'ADR-0001** ; aucun résultat n'est publié sur un modèle local. (b) **Aucun chiffre de latence, de coût ni de matériel** n'est publié. Ce que l'ablation permet de retenir malgré tout, et qui est réutilisable localement : **les « règles » pèsent plus que le modèle** (−28,6 points quand on les retire, contre −6,8 pour le raisonnement pas-à-pas). Le sac de règles n'est pas l'accessoire du LLM ; c'est l'inverse.

(c) Ni NameGuess ni Columbo ne traitent le **non-anglais**.

### 3.4 Similarité par plongements sur les noms

Trois résultats mesurés, et ils vont tous dans le même sens : **les plongements pré-entraînés appliqués à des noms de colonnes déçoivent, et pour une raison structurelle.**

**SemProp** ([Fernandez et al., « Seeping Semantics », ICDE 2018](https://cs.uwaterloo.ca/~ilyas/papers/FernendazICDE2018.pdf)) est le travail de référence sur les plongements de mots appliqués aux **noms d'attributs**. Il combine un appariement syntaxique sur les noms (`SYNM`) et un appariement sémantique (`SEMA`) par plongements. Sa contribution technique est un aveu :

> « [la méthode par vecteurs de paragraphe] performance is not good enough when we have **only attribute names, which consist of a few words and are typically not grammatically complete sentences**. […] Simpler approaches used in practice are to combine words using pairwise mean, min or max. Of those, **averaging word embeddings (AWE) is quite popular**. We found these approaches **did not yield good results in our case**. »

⚠️ **Un nom de colonne n'est pas une phrase, et les recettes d'agrégation de plongements conçues pour des phrases s'y comportent mal.** SemProp remplace l'agrégation par des *groupes cohérents* : au lieu de fabriquer un vecteur par nom, on raisonne sur les similarités **par paires** entre les jetons des deux noms, avec un facteur de cohérence `F(X)` = moyenne des similarités cosinus toutes-paires. Gain mesuré : la moyenne des plongements **réduit de 30 % la précision et le rappel** obtenus par les groupes cohérents ; doc2vec plafonne à 36 % de précision / 46 % de rappel, également en dessous.

Le prétraitement des noms y est décrit précisément — et c'est exactement `split_column_name` : suppression des symboles `-` et `_`, tokeniseur camelCase optionnel (`SocialSecurity` → `social security`), filtrage des mots vides, sortie en sac de mots. SemProp traite explicitement les jetons **hors vocabulaire** (`⟨unk⟩`), en les ignorant ou en pénalisant la similarité.

⚠️ **Mais les chiffres absolus de SemProp sont bas** — précision de l'ordre de 19 à 40 %, rappel de 12 à 69 % selon les tranches du pipeline — et le verdict de Valentine est sans appel : « **SemProp's effectiveness is unexpectedly low over all relatedness scenarios, worse than any other matching method** we tested », les plongements pré-entraînés ne tenant pas quand le domaine est spécifique. Son coût y est mesuré à **735 s par paire de tables** (§ 7.1).

**Magneto** ([Freire et al., PVLDB vol. 18, VLDB 2025](https://www.vldb.org/pvldb/vol18/p2681-freire.pdf)) apporte l'ablation la plus directe qui existe sur notre question — un petit modèle (MPNet) pour la présélection, un LLM (GPT-4o-mini) pour le reclassement, et une sérialisation paramétrable :

> « **Header only-ZS**, which uses only column headers without values, serving as a baseline. […] The Header only-ZS approach shows **the lowest performance in all datasets**, confirming that **column headers alone are insufficient in complex scenarios**. »

⚠️ **C'est le contre-argument le plus direct au pari de #122, et il faut le porter au dossier.** Deux nuances, qui ne l'annulent pas mais le bordent : (a) la tâche est l'**appariement** entre deux schémas, pas la classification dans une taxonomie fermée — apparier `Gender` à `Ge` est plus dur que reconnaître que `Ge` est un genre ; (b) la meilleure sérialisation zéro-tir de Magneto s'appelle `S_repeat` et consiste à **répéter le nom de colonne plusieurs fois** pour forcer le modèle à le prioriser, parce que sinon il « disproportionately focus[es] on later tokens or values rather than column names ». **Le nom porte donc le signal dominant ; les valeurs désambiguïsent à la marge.** Et sur le banc GDC, pour le schéma cible, les auteurs n'utilisent que « column names and domain information (i.e., we disregard column values) ».

Coût de Magneto : 11 à 33 s pour la voie plongements — **sur GPU A100** — et 545 à 589 s pour la voie LLM.

**ReMatch** ([arXiv 2403.01567](https://arxiv.org/abs/2403.01567), 2024) est, de tout ce dossier, le travail dont le cadre ressemble le plus à celui de #122 : appariement vers le modèle OMOP à partir des **métadonnées seules** — noms de tables, noms de colonnes, descriptions, types, clés — en « avoid[ing] any access to data in the source database ».

| Jeu | Exactitude@1 | Exactitude@5 |
| --- | --- | --- |
| MIMIC-III → OMOP | 0,424 | ~0,73–0,76 |
| MIMIC-III → OMOP (avec guidance) | **0,539** | **0,783** |
| Synthea → OMOP | **0,562** | **0,924** |

⚠️ **Le régime top-k est le bon régime**, et c'est le renseignement transposable : un moteur schéma-seul se trompe une fois sur deux en premier choix, mais place la bonne réponse dans ses cinq premières propositions quatre fois sur cinq. Pour une carte dont la posture est `Aide à la décision` et dont un humain arbitre chaque ligne, c'est un mode d'emploi. ⚠️ Ces chiffres proviennent de la version HTML v1 sur arXiv, extraits automatiquement : **confiance moyenne** (§ 10).

### 3.5 Classifieur supervisé sur le nom

**Recherche infructueuse en source primaire académique.** Aucun papier trouvé qui entraîne un classifieur supervisé prenant le **nom de colonne seul** en entrée pour prédire une catégorie de données personnelles.

Ce qui existe et qui en approche :

- **NameGuess/Columbo** (§ 3.3) — mais la tâche est la génération, pas la classification.
- Le brevet **US 11768916 B2 / US 11379601 B2**, « Detection of sensitive database information » (PayPal, priorité 2019-12-11, [Google Patents](https://patents.google.com/patent/US11768916)) : un classifieur neuronal multi-classe utilisant « features extracted from the **metadata** and the **data profile** », plus une « **character-based analysis of the set of data items** ». **Les métadonnées y sont un trait parmi d'autres, à côté d'un profil et d'une analyse caractère des valeurs** — ce n'est pas un moteur schéma-seul. ⚠️ Le texte exact des revendications n'a **pas** pu être vérifié (§ 10).
- Le brevet **US 12306855 B2**, « classifying columns of a data store based on character level labeling » : vérification faite, il classe les **valeurs cellulaires** (embeddings de caractères → CNN/TCN → CRF), et **ne mentionne ni nom de colonne ni métadonnée**.

**Enseignement.** Même l'industrie qui brevette classe à partir des valeurs. Un classifieur supervisé sur le nom seul est un objet que ce dépôt aurait à construire et à mesurer lui-même — il n'a pas d'antécédent publié à copier ni de chiffre auquel se comparer. Ce n'est pas une raison de l'exclure ; c'est une raison d'écrire son critère avant ses chiffres, comme la carte #42 l'a déjà fait.

### 3.6 Type SQL, longueur déclarée, contraintes de clé étrangère

**Trois sources primaires convergent, et le verdict est net : ce n'est pas un signal de sens, c'est un filtre de candidats.**

**Rahm & Bernstein, 2001** ([VLDB Journal 10(4)](https://link.springer.com/article/10.1007/s007780100057)) consacrent leur § 6.4 aux approches *constraint-based* : types de données et plages, unicité, optionalité, cardinalités, caractéristiques de clé (unique, primaire, étrangère), avec une table de synonymes de types (`string ≅ varchar`, `primary key ≅ unique`). Leur exemple est parlant : `Pno:int,unique` apparie aussi bien `EmpNo` que `DeptNo`. Et leur conclusion :

> « the use of constraint information alone **often leads to imperfect n:m matches**, as there may be several elements in a schema with comparable constraints. Still, the approach **helps to limit the number of match candidates** and may be combined with other matchers (e.g., name matchers). »

**Cupid, 2001** ([VLDB 2001](https://www.vldb.org/conf/2001/P049.pdf)) le borne numériquement : la compatibilité de types initialise la similarité structurelle des feuilles dans **[0 ; 0,5]** — types identiques valant 0,5. Le type est un contributeur plafonné et minoritaire, par conception.

**Valentine, 2021** ([ICDE 2021](https://arxiv.org/abs/2010.07386)) le mesure, et c'est la phrase la plus dure de tout ce dossier pour cette famille :

> « **in the absence of good attribute names, the rest of the schema information graph (e.g., types, transitive relationships) or contextual information such as the neighborhood of columns per dataset do not actually give any useful insights for any schema-based method.** »

**`datahub-classify` le confirme par la pratique** : le poids `Datatype` vaut **0 pour les treize infotypes** de sa configuration par défaut (§ 6.3). L'outil qui pondère explicitement quatre sources de signal donne au type SQL un poids nul.

⚠️ **Conséquence pour #122.** Le type, la longueur déclarée et les contraintes ne peuvent pas porter une catégorie de données, et donc pas non plus un degré de doute. Ce qu'ils peuvent faire, et que la littérature valide : **éliminer**. `DATE` exclut « courriel » ; `VARCHAR(2)` exclut « adresse postale » ; une colonne portant une contrainte de clé étrangère vers `REFERENTIEL_PAYS` n'est probablement pas un nom de personne. C'est une **règle de rejet**, exprimable en prose française (« écarté : le type `DATE` est incompatible avec la catégorie ») — donc compatible avec la décision de cadrage 6, et strictement plus honnête qu'un score.

Une remarque de Rahm & Bernstein mérite d'être remontée dans le cadrage : leur § 6.3 traite du ***description matching***, l'exploitation des **commentaires en langue naturelle** attachés aux éléments de schéma (leur exemple : `empn // employee name` apparié à `name // name of employee`). Pour une base relationnelle, cela correspond aux **commentaires de colonne SQL** — que le format d'entrée de #122 prévoit déjà de collecter. ⚠️ **C'est le seul endroit de tout le signal où de la prose française existe réellement**, donc le seul endroit où un traitement de langue serait sur son terrain. Aucune source lue ne mesure l'apport des commentaires isolément ; c'est une piste ouverte pour le banc, et une piste bon marché.

---

## 4. Le NER sur des identifiants de schéma : le cadrage tient, avec une nuance

**La décision de cadrage 1 de #122 dit : « ce n'est pas un NER — un NER est entraîné sur de la prose, et `dt_naiss` n'en est pas. » Le ticket #124 avait le droit de la contredire. Vérification faite, elle tient — mais pas exactement pour la raison écrite.**

### 4.1 Ce qui a été cherché, et ce qui a été trouvé

Aucune méthode sérieuse trouvée qui fasse tourner un modèle de reconnaissance d'entités nommées **sur des noms de colonnes**. Ce qui existe, systématiquement, c'est un NER **sur les valeurs échantillonnées**, le nom de colonne servant au mieux de contexte :

- **Microsoft Presidio** : le NER (spaCy/stanza) tourne sur le texte des valeurs. Le nom de colonne, lorsqu'il est fourni, entre par le paramètre `context` et **ne fait que modifier un score existant** (§ 6.1).
- **OpenMetadata** : le NER tourne sur les valeurs échantillonnées, modèle spaCy (`en_core_web_md`, `fr_core_news_md`, `xx_ent_wiki_sm`) — jamais sur les noms.
- **Le brevet US 12306855** : caractères des **valeurs**.

### 4.2 La nuance : ce n'est pas « pas un modèle de langue », c'est « pas un NER »

Le cadrage est juste sur le NER **au sens propre** — l'étiquetage de séquences sur de la prose. Mais il serait faux d'en déduire qu'aucun modèle de langue ne tourne sur des identifiants de schéma segmentés : **NameGuess et Columbo font exactement cela** (§ 3.3), et Columbo le fait **sans jamais lire une valeur**. Ce ne sont pas des NER — ce sont des modèles génératifs d'expansion — mais ce sont bien des modèles de langue appliqués à `cust_acq_dt`.

**Formulation qui survit à la vérification**, si le glossaire de #122 veut la durcir : *ce qui opère sur des noms d'identifiants relève du lexique, des règles et de la morphologie ; un modèle de langue peut y intervenir, mais pour **expanser** un identifiant, jamais pour **étiqueter des entités** dans une prose qui n'existe pas.* Le mot « NER » part bien avec la chose. La porte que #124 entrouvre est celle de l'**expansion**, et elle mène tout droit à une dépendance GPU (§ 3.3, § 7) — donc à la décision 13 de la carte.

### 4.3 Un cas particulier à ne pas confondre

Une famille de bibliothèques (par exemple `NERPII`) applique un NER « à des données structurées ». ⚠️ **Non vérifié en source primaire** — la page du papier était inaccessible (§ 10). D'après ce qui a pu être lu, ces bibliothèques appliquent le NER **aux valeurs des colonnes** d'un CSV, ce qui les range dans la case ci-dessus. À ne pas citer comme contre-exemple sans l'avoir vérifié.

---

## 5. L'appariement de schémas : 25 ans de travail sur exactement notre signal

**C'est le corpus que le ticket #124 ne nommait pas, et c'est le plus utile de tous.** La tâche diffère — apparier deux schémas, non classer une colonne dans une taxonomie — mais **le signal est identique** : décider du sens d'une colonne à partir de son nom, sans lire une valeur. Cette littérature a mesuré ce que la *semantic type detection* a refusé de mesurer.

### 5.1 Rahm & Bernstein 2001 : la taxonomie, et l'avertissement

[Le survey de référence](https://link.springer.com/article/10.1007/s007780100057) (VLDB Journal 10(4), 2001) énumère six formes de similarité de noms, dont la deuxième est littéralement notre problème :

> « equality of canonical name representations **after stemming and other preprocessing** […] `CName → customer name`, and `EmpNO → employee number` »

et pose la dépendance aux ressources, en anticipant explicitement le multilingue :

> « Exploiting synonyms and hypernyms requires the use of thesauri or dictionaries. General natural language dictionaries may be useful, perhaps even **multi-language dictionaries** […] In addition, name matching can use domain- or enterprise-specific dictionaries and is-a taxonomies containing common names, synonyms and descriptions of schema elements, **abbreviations**, etc. **These specific dictionaries require a substantial effort to be built up in a consistent way. The effort is well worth the investment, especially for schemas with relatively flat structure** where dictionaries provide the most valuable matching hints. »

⚠️ **« Relatively flat structure »**, c'est un schéma relationnel — le nôtre. Le survey dit donc, en 2001 : *pour votre cas, le dictionnaire est le levier principal, il coûte cher à construire, et ça vaut le coup.* C'est le meilleur argument publié en faveur d'un lexique français bâti à la main.

**Le risque numéro un y est nommé : l'homonymie.** « Homonyms are equal or similar names that refer to different elements. Clearly, homonyms can mislead a matching algorithm. » L'exemple donné est métier : **« line » = ligne de métier ou ligne de commande**. En français, `nom` (personne ou libellé d'article), `compte` (bancaire ou comptable), `civilite` (titre ou politesse), `cle`, `ref`. La parade proposée par le survey est le contexte — `Order.Line` contre `Business.Line`, donc **le nom de la table** — et, à défaut de trancher, une consigne qui devrait plaire à ce dépôt :

> « at least, the matcher can offer **a warning of the potential ambiguity**. »

C'est, mot pour mot, la posture `Aide à la décision` : ne pas trancher, signaler.

### 5.2 Cupid 2001 : « en l'absence d'instances, le nom est la meilleure source »

[Cupid](https://www.vldb.org/conf/2001/P049.pdf) ouvre son § 5 par la phrase que la carte #122 pourrait mettre en exergue :

> « The first phase of schema matching is based primarily on schema element names. **In the absence of data instances, such names are probably the most useful source of information for matching.** »

Son appariement linguistique se fait en trois étapes qui sont, à vingt-cinq ans de distance, l'architecture qu'un moteur de #122 aurait :

1. **Normalisation** — « Similar schema elements in different schemas often have names that differ due to the use of **abbreviations, acronyms, punctuations** » → tokenisation, **expansion** des abréviations et acronymes, élimination des mots vides. « In each of these steps we use a **thesaurus** that can have both common language and domain-specific references. »
2. **Catégorisation** — regroupement par type, hiérarchie, contenu linguistique, pour élaguer le produit cartésien.
3. **Comparaison** — similarité linguistique `lsim ∈ [0,1]` par comparaison de jetons via thésaurus (synonymie + hyperonymie) et sous-chaînes.

**Et Cupid dit aussi ce qui casse**, dans sa propre évaluation :

- « **The thesaurus plays a crucial role in linguistic matching.** » Le thésaurus fourni ne contenait que **4 abréviations et 2 entrées de synonymie**, et cela suffisait à faire basculer le résultat.
- « The sense of a word is often domain-specific ; e.g. the correct sense of `Header` **does not exist in WordNet**, and the synonym has to be manually added. » ⚠️ **WordNet est insuffisant même en anglais, même sur un mot courant.** Espérer qu'un lexique généraliste français suffise serait espérer mieux qu'eux.
- Et le résultat le plus directement transposable : en similarité linguistique **seule**, 2 paires manquées mais **7 faux positifs** ; et — « **In a relational schema, where the path-names include only the table and column names, the accuracy is much worse.** »

⚠️ **Cupid dit en 2001 que le nom seul sur un schéma relationnel plat est le cas le plus dur**, précisément parce qu'il n'y a plus d'arborescence pour désambiguïser. C'est notre cas exact.

### 5.3 Valentine 2021 : les chiffres modernes, et ils portent sur notre morphologie

[Valentine](https://arxiv.org/abs/2010.07386) réévalue six familles sur un banc commun. Les méthodes **schéma seul** sont Cupid, Similarity Flooding et COMA-Schema. Métrique : *recall@ground truth*.

**Résultat 1 — noms propres : le nom seul suffit parfaitement.**

> « with **verbatim schemata** all schema-based methods are able to place **all correct matches at top** »

**Résultat 2 — noms bruités : effondrement.** Et le bruit injecté par Valentine est *littéralement* notre pathologie :

> « i) we **prefix column names with their table name**, ii) [variantes de noms] and iii) **we drop vowels** »

`CLI_NOM_1` est le cas (i). `dt_naiss` est le cas (iii). Verdict :

> « when matching columns are represented by different attribute names, because of noise that we have introduced in the schemata, **there is no schema-based method that can provide satisfying and consistent results in any scenario**. […] **median recall at ground truth close to 0.6.** »

⚠️ **C'est le chiffre le plus contraignant de tout ce dossier.** Sur des noms préfixés par la table et dévoyellisés — c'est-à-dire sur des noms de colonnes de SI réel — les méthodes de nom plafonnent autour de 0,6 de rappel médian, avec une forte dispersion. Et rien ne rattrape : ni les types, ni les clés, ni le voisinage (§ 3.6).

**Résultat 3 — données humaines** (*recall@GT*, Table IV) :

| Méthode | Magellan | ING#1 | ING#2 |
| --- | --- | --- | --- |
| Cupid | **1,0** | 0,714 | 0,500 |
| Similarity Flooding | **1,0** | 0,357 | 0,439 |
| COMA schéma | **1,0** | 0,786 | **0,121** |
| Distribution des valeurs | 0,54 | 0,857 | **0,879** |

Magellan : noms identiques, méthodes de nom parfaites. **ING#2 : les noms de la seconde table portent des suffixes — COMA-Schema s'effondre de 0,786 à 0,121**, tandis que la méthode par distribution de valeurs monte à 0,879. Sur un vrai schéma bancaire aux conventions de nommage divergentes, le nom seul décroche brutalement.

**Résultat 4 — le coût, et il joue dans l'autre sens** (§ 7.1) : les méthodes schéma-seul coûtent 1,67 à 9,64 s par paire de tables contre 318 à 4 818 s pour tout ce qui touche aux valeurs. *« schema-based methods are by far the most efficient since they avoid looking into instance values. »* **Deux à trois ordres de grandeur.**

**Deux réserves de méthode que les auteurs posent eux-mêmes**, et qu'il faut reprendre : les résultats publiés sont obtenus en conditions quasi optimales (recherche d'hyperparamètres sur la vérité terrain) — *« In the wild, we expect to see lower performance for most algorithms »* — et une ligne de base Jaccard-Levenshtein d'environ 70 lignes de Python se défend étonnamment bien. Enfin, Magneto (§ 3.4) reproche aux jeux de Valentine de contenir surtout des correspondances **lexicales** (`Gender` → `Ge`) « uncommon in practical applications ». La lecture prudente : Valentine mesure la dégradation par la morphologie, ce qui est exactement ce qui nous intéresse, mais son banc n'est pas le nôtre.

### 5.4 Ce que #122 peut prendre à cette littérature

1. **Une formulation du problème comme recherche, pas comme optimisation.** Valentine conclut que l'appariement doit être traité « as a search problem, rather than an optimization problem », en présentant des candidats classés à un humain. ReMatch le chiffre (§ 3.4) : 0,42–0,56 en premier choix, 0,78–0,92 dans les cinq premiers. **Un moteur qui rendrait `k` catégories candidates ordonnées, plutôt qu'une seule, changerait la valeur du rapport pour l'`Operator` sans toucher au reste du contrat.** Ce n'est pas une décision de ce ticket, mais c'est une question que le banc devrait pouvoir instruire.
2. **La confirmation que le dictionnaire d'abréviations de domaine est le composant décisif**, et non l'algorithme : Cupid (« the thesaurus plays a crucial role »), TIDIER (+23 points, § 3.2), Rahm & Bernstein (« well worth the investment, especially for schemas with relatively flat structure »).
3. **L'avertissement d'ambiguïté comme sortie légitime** quand l'homonymie n'est pas tranchable — déjà dans le survey de 2001, et déjà la doctrine de ce dépôt.

---

## 6. Ce que font les outils réels

Sept outils ont été examinés **dans leur code source** ou dans leur documentation d'éditeur.

### 6.0 Tableau de synthèse

| Outil | Le nom est-il un signal ? | Suffisant seul ? | Mécanisme | Abréviations / segmentation | Français | Coût CPU publié |
| --- | --- | --- | --- | --- | --- | --- |
| **presidio-structured** | **Non** | — | valeurs uniquement | — | — | non |
| **presidio-analyzer** (`context=`) | Oui | **Non** — booster pur | lexique par recognizer, lemmes/sous-chaîne | non | **non** | non |
| **OpenMetadata** `ColumnNameScanner` (mort) | Oui | **Oui**, confiance 1,0 | 12 regex insensibles à la casse | non | **non** | négligeable |
| **OpenMetadata** `HeuristicPIIClassifier` | Oui, deux fois | **Non** | regex + `split_column_name` → contexte Presidio, **+0,5** au score | délimiteurs + camelCase | valeurs oui, noms **non** | non |
| **OpenMetadata** `TagAnalyzer` (actuel) | Oui, canal dédié | **Oui si** un recognizer `target: column_name` est configuré — **aucun livré** | recognizers déclaratifs sur le nom-texte | idem | idem | non |
| **datahub-classify** | Oui, pondéré 0,3–0,65 | **Non** — porte dure à 50 valeurs | regex à trois niveaux × poids | abréviations en dur, anglais | **non** | non |
| **piicatcher** | **Oui, signal unique** | **Oui** (`scan_type=metadata` par défaut) | 13 regex, binaire, premier match | abréviations anglaises en dur | **non** | ~nul |
| **Google Cloud DLP** (hotword) | Oui | **Non** — ajuste la vraisemblance | regex sur l'en-tête + fenêtre | à écrire soi-même | possible (regex libre) | facturé à l'octet |
| **AWS Macie** (keywords) | **Oui, documenté** | **Non** — condition `ET` avec la regex de valeur | ≤ 50 mots-clés littéraux | non | possible (mots libres) | non |
| **Microsoft Purview** (`Column Pattern`) | **Oui, champ dédié** | probable — **non confirmé** | regex sur nom + regex sur données, seuil 60 % | à écrire soi-même | **explicitement non** | non |

### 6.1 Microsoft Presidio : le nom est un booster, jamais un détecteur

**`presidio-structured` n'utilise à aucun moment le nom de colonne comme signal.** Le cœur, `PandasAnalysisBuilder._batch_analyze_df` dans [`analysis_builder.py`](https://github.com/microsoft/presidio/blob/main/presidio-structured/presidio_structured/analysis_builder.py), itère sur les colonnes et n'analyse que les valeurs ; le nom ne sert qu'à indexer le dictionnaire de sortie. La documentation le confirme ([`docs/structured/index.md`](https://github.com/microsoft/presidio/blob/main/docs/structured/index.md)) : « identify **columns or keys containing** PII […] and establishes a mapping between these column/keys names and the detected PII entities ».

Le vrai crochet est ailleurs, et il est explicitement documenté pour notre cas ([`docs/tutorial/06_context.md`](https://github.com/microsoft/presidio/blob/main/docs/tutorial/06_context.md)) :

> « additional context words could be passed on the **request level**. This is useful when there is context coming from **metadata such as column names** […] notice how the "zip" context word doesn't appear in the text but still enhances the confidence score **from 0.01 to 0.4** »

avec l'exemple littéral `record = {"column_name": "zip", "text": "My code is 90210"}`.

Mécanique, dans [`lemma_context_aware_enhancer.py`](https://github.com/microsoft/presidio/blob/main/presidio-analyzer/presidio_analyzer/context_aware_enhancers/lemma_context_aware_enhancer.py) : `context_similarity_factor = 0.35`, `min_score_with_context_similarity = 0.4`. Et le point décisif : `enhance_using_context` part des `raw_results` et **modifie des scores existants ; il n'en crée jamais**.

⚠️ **Chez Presidio, un nom de colonne `num_secu` sans valeur derrière ne produit rien.** C'est la position architecturale la plus nette de tout le paysage, et elle est l'inverse de celle que #122 doit tenir.

### 6.2 OpenMetadata : trois générations, et le meilleur retour d'expérience du domaine

Le module PII d'OpenMetadata porte **trois générations superposées**, ce qui en fait le meilleur observatoire de l'évolution du domaine.

**Génération 1 — [`column_name_scanner.py`](https://github.com/open-metadata/OpenMetadata/blob/main/ingestion/src/metadata/pii/scanners/column_name_scanner.py)**, nom seul, `confidence=1` :

```python
"PASSWORD":      "^.*password.*$",
"US_SSN":        "^.*(ssn|social).*$",
"EMAIL_ADDRESS": "^(email|e-mail|mail)(.*address)?$",
"PERSON":        "^.*(firstname|lastname|fullname|maidenname|nickname|name_suffix).*$",
"BIRTH_DATE":    "^.*(date_of_birth|dateofbirth|dob|birthday|date_of_death|dateofdeath).*$",
"ADDRESS":       "^.*(address|city|state|county|country|zipcode|zip|postal|zone|borough).*$",
```

⚠️ **Cette classe est aujourd'hui du code mort** : aucun processeur ne l'instancie. Le projet est passé du nom-seul au nom-comme-booster.

**Génération 2 — [`classifiers.py`](https://github.com/open-metadata/OpenMetadata/blob/main/ingestion/src/metadata/pii/algorithms/classifiers.py)** : le nom joue **deux fois**, découpé en jetons puis injecté comme `context` Presidio, et bonus additif `column_name_contribution = 0.5`. Mais la boucle itère sur `content_results` : **sans valeurs, rien à booster, résultat vide**. Deux garde-fous coupent en amont : `if not str_values: return {}` et un seuil de cardinalité relative à 1 %.

La segmentation, dans [`feature_extraction.py`](https://github.com/open-metadata/OpenMetadata/blob/main/ingestion/src/metadata/pii/algorithms/feature_extraction.py), est **l'état de l'art industriel** de la découpe de noms de colonnes — quinze lignes :

```python
def split_column_name(column_name: str) -> List[str]:
    delimiters = ["_", "-", " ", ".", "/"]
    parts = re.split("|".join(map(re.escape, delimiters)), column_name)
    result = []
    for part in parts:
        camel_parts = re.sub("([a-z])([A-Z])", r"\1 \2", part).split()
        result.extend([p.lower() for p in camel_parts])
    return result
```

Délimiteurs et camelCase, **rien d'autre** : ni désabréviation, ni découpe de mots collés. `dt_naiss` → `["dt", "naiss"]`, deux jetons qu'aucun lexique anglais ne reconnaît.

**Génération 3 — [`tag_scoring.py`](https://github.com/open-metadata/OpenMetadata/blob/main/ingestion/src/metadata/pii/algorithms/tag_scoring.py)** : les recognizers portent désormais un champ `target ∈ {content, column_name}` ([schéma JSON](https://github.com/open-metadata/OpenMetadata/blob/main/openmetadata-spec/src/main/resources/json/schema/type/recognizer.json)). Le nom est passé **comme texte** à un analyseur dédié, et en cas d'égalité **le nom l'emporte** (`column_score >= content_score`). Une classification sans aucune valeur est donc structurellement possible. ⚠️ **Mais les 45 recognizers livrés par défaut ont tous `"target": "content"`** — la capacité existe, la détection par le nom n'est pas fournie.

**Le retour d'expérience à retenir**, dans [`presidio_utils.py`](https://github.com/open-metadata/OpenMetadata/blob/main/ingestion/src/metadata/pii/algorithms/presidio_utils.py) — c'est le meilleur avertissement documenté sur le coût du filtrage par sous-chaîne, et il vient d'un correctif réel :

> « A plain `"cid" in "acid level"` test is true, which would boost the CVV recognizer on any column whose name merely contains the letters of a context word — `acid_level`, `incident_count`, `decoder_ring` — turning a 0.5 "3-4 digits" regex into a 1.0 match. »

⚠️ **À lire directement contre la décision de cadrage 5 de #122.** Le degré de doute y est « dérivé de la règle qui a déclenché ». Or une règle de sous-chaîne qui déclenche sur `acid_level` **déclenche exactement de la même façon** que sur un vrai `cvv` : le degré de doute serait identique pour un vrai positif et pour un faux. La forme de la règle — sous-chaîne, jeton entier, égalité après normalisation — est donc **une décision de conception du degré de doute**, pas un détail d'implémentation.

### 6.3 DataHub / `datahub-classify` : le nom pèse, mais pas assez pour décider seul

Le [framework `infotype`](https://github.com/acryldata/datahub-classify) combine quatre facteurs par somme pondérée. Poids par défaut, littéralement lus dans [`reference_input.py`](https://github.com/acryldata/datahub-classify/blob/main/datahub-classify/src/datahub_classify/reference_input.py) :

| Infotype | Name | Description | Datatype | Values |
| --- | --- | --- | --- | --- |
| Email_Address, Gender, Phone_Number, Credit_Debit_Card_Number, IBAN, US_SSN, IP_Address | **0,4** | 0 | 0 | 0,6 |
| Street_Address | **0,5** | 0 | 0 | 0,5 |
| Full_Name | **0,3** | 0 | 0 | 0,7 |
| **Age** | **0,65** | 0 | 0 | 0,35 |

Trois faits notables. **`Description` et `Datatype` sont à 0 partout** — les regex de description existent mais sont inertes, et **le type SQL ne pèse rien** (à rapprocher du résultat de Valentine, § 5). **`Age` est le seul infotype où le nom domine** — une colonne d'entiers entre 0 et 120 n'est identifiable que par son nom, ce qui est précisément l'argument de #122 pour les colonnes sans motif de valeur.

Le matching du nom, dans [`infotype_utils.py`](https://github.com/acryldata/datahub-classify/blob/main/datahub-classify/src/datahub_classify/infotype_utils.py), est à **trois niveaux** : `1,0` si égalité après suppression des caractères non alphabétiques (donc `first_name ≡ firstname`) ou `fullmatch` de la regex ; `0,65` si le motif est une simple sous-chaîne ; `0` sinon.

⚠️ **Conséquence chiffrée décisive.** Une colonne nommée exactement `email` obtient `1,0 × 0,4 = 0,40`, sous le seuil usuel de 0,6. **Le nom seul ne franchit jamais le seuil, sauf pour `Age`.** Et le nom seul est de toute façon impossible : `perform_basic_checks` rejette la colonne si `len(values) < minimum_values_threshold`, **50 par défaut**. `datahub-classify` est structurellement inutilisable en mode schéma-seul.

Les abréviations y sont traitées **à la main, infotype par infotype** : `.*(num|no).*`, `add[^a-z]+`, `.*[^a-z]+ph[^a-z]+.*`, `.*int.*bank.*acc.*`. Du travail d'orfèvre, en anglais, non généralisable.

### 6.4 `piicatcher` : le seul outil dont le mode par défaut est nom-seul

[`piicatcher`](https://github.com/tokern/piicatcher) (Tokern) est **le précédent le plus proche de #122**. Dans [`api.py`](https://github.com/tokern/piicatcher/blob/master/piicatcher/api.py), `ScanTypeEnum ∈ {metadata, data}` et **`metadata` est le défaut** : aucune donnée n'est lue.

[`scanner.py`](https://github.com/tokern/piicatcher/blob/master/piicatcher/scanner.py) porte 13 regex insensibles à la casse — le catalogue nom-seul le plus complet de l'open source :

```python
Person:     "^.*(firstname|fname|lastname|lname|fullname|maidenname|_name|nickname|name_suffix|name|person).*$"
BirthDate:  "^.*(date_of_birth|dateofbirth|dob|birthday|date_of_death|dateofdeath|birthdate).*$"
ZipCode:    "^.*(zipcode|zip_code|postal|postal_code|zip).*$"
SSN:        "^.*(ssn|social_number|social_security|social_security_number|social_security_no).*$"
Password:   "^.*pass.*$"
```

La détection est **binaire, premier match gagne, pas de score**, et l'ordre d'insertion du dictionnaire fait loi — `Person` est testé en premier.

⚠️ **Deux enseignements pour la décision de cadrage 5.** (a) `Password: "^.*pass.*$"` matche `passenger_count`, `bypass_flag`, `pass_rate` : c'est le prix payé pour une couverture large par sous-chaîne. (b) `piicatcher` **ne produit aucun score**, donc aucun degré de doute — ce qui est cohérent avec sa mécanique et incompatible avec le contrat de #122. Un moteur nom-seul qui veut un degré de doute **doit** structurer ses règles par forme (égalité exacte / jeton entier / sous-chaîne), puisque c'est la forme, et elle seule, qui porte l'information de doute.

### 6.5 Les outils commerciaux

**AWS Macie** est le plus explicite du paysage sur les en-têtes de colonne ([Configuration options for custom data identifiers](https://docs.aws.amazon.com/macie/latest/user/cdis-options.html)) :

> « **Structured columnar data** – Macie includes a result if the text matches the regex pattern **and a keyword is in the name of the field or column that stores the text** […] This is the case for Microsoft Excel workbooks, CSV files, and TSV files. »

Jusqu'à 50 mots-clés de 3 à 90 caractères, insensibles à la casse. Mais c'est une conjonction : **le mot-clé est une condition `ET` obligatoire, pas un détecteur autonome.**

**Google Cloud DLP** documente les *hotword rules* ([Customizing likelihood](https://docs.cloud.google.com/sensitive-data-protection/docs/creating-custom-infotypes-likelihood)), dont l'exemple canonique **est un en-tête de colonne** :

> « The hotword regular expression `(Fake Social Security Number)` **contains the name of the column** […] Setting `windowBefore` to 1 means the hotword is in a column header, and the findings must be in the column. »

Là encore : ajustement de vraisemblance sur un *finding* préexistant. Et l'[Architecture Center](https://docs.cloud.google.com/architecture/de-identification-re-identification-pii-using-cloud-dlp) va jusqu'à recommander de **court-circuiter l'inspection** — « you can expect a column labeled SSN to contain only SSN data and **don't need to inspect it** » — mais en confiant la décision à l'humain, pas à un moteur. C'est une validation éditeur de l'approche schéma-seul, et de sa limite.

**Microsoft Purview** offre le seul champ de premier ordre dédié au nom ([Custom classifications](https://learn.microsoft.com/en-us/purview/data-map-classification-custom)) : `Column Pattern`, « a regular expression that represents the column names that you want to match », **optionnel**, aux côtés d'un `Data Pattern` lui aussi optionnel. ⚠️ **La doc n'affirme pas qu'une règle Column-Pattern-seule classe effectivement une colonne** (§ 10). En revanche elle est catégorique sur la langue, deux fois : « **Custom classification rules are only supported in the English language.** »

---

## 7. Le coût machine sur CPU

**Avertissement méthodologique, dans la ligne de la recherche #44 de ce dépôt : le critère CPU n'est presque jamais publié.** Sur toutes les sources lues, un seul papier publie une latence par colonne, et c'est sur GPU. Ce qui suit sépare strictement ce qui est mesuré de ce qui ne l'est pas.

### 7.1 Ce qui est publié

| Famille | Chiffre publié | Source | Matériel |
| --- | --- | --- | --- |
| Appariement de schémas, schéma seul (COMA-Schema) | **1,67 s** / paire de tables | [Valentine, ICDE 2021](https://arxiv.org/pdf/2010.07386) | non précisé |
| Appariement de schémas, schéma seul (Similarity Flooding) | **7,09 s** / paire | idem | idem |
| Appariement de schémas, schéma seul (Cupid) | **9,64 s** / paire | idem | idem |
| Appariement, instances (COMA-Instance) | 318,07 s / paire | idem | idem |
| Appariement, instances (Distribution-based) | 71,16 s / paire | idem | idem |
| Appariement, hybride (SemProp) | 735,25 s / paire | idem | idem |
| Appariement, hybride (EmbDI) | **4 817,87 s** / paire | idem | idem |
| Dictionnaire sur valeurs | **0,01 s** / colonne, 0,5 Mo | [Sherlock, KDD 2019](https://arxiv.org/abs/1905.10688) | non précisé |
| Regex apprises sur valeurs | **0,01 s** / colonne, 0,01 Mo | idem | idem |
| Sherlock (réseau multi-entrées) | **0,42 s** (± 0,01) / colonne, 6,2 Mo | idem | idem |
| Forêt aléatoire sur les mêmes traits | 0,26 s / colonne, **760,4 Mo** | idem | idem |
| Magneto, voie plongements (MPNet) | 11–33 s / paire de schémas | [Magneto, VLDB 2025](https://www.vldb.org/pvldb/vol18/p2681-freire.pdf) | **GPU A100** |
| Magneto, voie LLM (GPT-4o-mini) | 545–589 s / paire de schémas | idem | API tierce |
| Qwen3 8B (détection contextuelle) | **0,46 s** / colonne | [Telkamp & Hulsebos, 2025](https://arxiv.org/html/2512.04120) | **8,0 Gio de GPU** |
| Qwen3 14B | 1,62 s / colonne | idem | 11,4 Gio de GPU |
| GPT-4o-mini | 1,20 s / colonne | idem | API tierce |
| spaCy `en_core_web_lg` (non-transformeur) | **10 014 mots/s CPU** | [spaCy, facts & figures](https://spacy.io/usage/facts-figures) | 10 000 commentaires Reddit |
| spaCy `en_core_web_trf` (transformeur) | **684 mots/s CPU**, 3 768 GPU | idem | idem |
| fastText (sac de mots + linéaire) | « half a million sentences among 312K classes in **less than a minute** » sur CPU multicœur | [Joulin et al., 2016](https://arxiv.org/abs/1607.01759) | « standard multicore CPU » |

### 7.2 Ce que ces chiffres disent, et ne disent pas

**L'écart entre familles est de deux à trois ordres de grandeur, et il est mesuré.** Valentine est le témoignage le plus net : sur la même tâche, un appariement schéma-seul coûte 1,67 à 9,64 s là où un appariement à plongements en coûte 4 818. **Ce n'est pas une différence de réglage, c'est une différence de nature.** Sherlock dit la même chose autrement : 0,01 s pour l'appariement de motifs contre 0,42 s pour le réseau — facteur 42 — pour un écart de qualité qui, lui, est massif dans l'autre sens (F1 0,16 contre 0,89, mais sur les valeurs).

**Ce que ces chiffres ne disent pas**, et qu'il faut refuser d'extrapoler :

- Les temps de Valentine sont **par paire de tables**, pas par colonne, et sur du matériel non précisé. Ils ordonnent les familles ; ils ne dimensionnent rien.
- Le 0,42 s de Sherlock **inclut l'extraction des traits sur 1 000 valeurs échantillonnées**. Un moteur qui ne lit pas les valeurs n'a pas ce coût. Le chiffre n'est **pas** transposable à un moteur schéma-seul.
- Les 0,46 s/colonne de Qwen3 8B sont **sur GPU**. Aucune mesure CPU du même montage n'existe. Sur ce dépôt, l'exécution CPU d'un modèle de cette taille n'est pas une hypothèse à évaluer : c'est hors de portée.
- **Aucun chiffre CPU n'existe pour NameGuess ni pour Columbo.** Columbo n'a même pas de version locale publiée.

### 7.3 La contention, qui est le vrai sujet de ce dépôt

Le § 5.8 de [`docs/spec/qualification.md`](../spec/qualification.md) a déjà tranché la forme du problème pour la `Qualification` : un appel LLM en vol de 120 s **affamerait** le point d'entrée du lexique, dont l'échéance de 5 s se déclencherait massivement « non parce que le lexique est lent mais parce qu'il attend derrière le LLM ». Le lexique y coûte **~0,5 ms**.

La carte #42 a mesuré la suite, sur ce dépôt et sur cette machine : **« 832 s par corps sur CPU, les huit cœurs saturés d'un bout à l'autre »** pour un entraînement SetFit, et à l'inférence **34,0 ms de médiane, 41,9 ms de p95, 267 Mio de RSS, 44 ms d'exposition du point d'entrée du lexique** pour un plongement dense gelé. C'est le seul jeu de chiffres CPU **mesuré sur le matériel réel** dont ce dépôt dispose, et il vaut mieux que toute la littérature lue ici.

**Trois lectures pour le protocole de mesure de #122 :**

1. **Le sac de règles est gratuit à l'échelle qui nous intéresse.** 13 regex par colonne (`piicatcher`) sur un schéma de 800 colonnes, c'est du bruit de fond. Aucune source ne publie de chiffre parce qu'il n'y a rien à publier. À mesurer quand même, une fois, pour que ce soit écrit.
2. **Un plongement dense est le premier palier où la contention devient réelle** — 34 ms par élément, 267 Mio, et huit cœurs saturés à l'entraînement. Sur un schéma de 800 colonnes cela reste tenable en latence (~27 s) mais **pas en contention** si le sidecar est partagé. C'est exactement ce que la décision 13 de #122 a exclu d'avance.
3. **Un LLM local est un moteur GPU, pas un moteur CPU.** 0,46 s/colonne × 800 colonnes = ~6 minutes de GPU exclusif, sur un GPU qui **sérialise déjà** la `Qualification`. Le coût n'est pas la latence du scan ; c'est la file d'attente qu'il crée pour un moteur en production.

---

## 8. Le français

**C'est le trou le plus net de tout le dossier, et il est total.**

### 8.1 Ce qui a été vérifié

- **Aucun des outils open-source examinés ne contient un seul terme français dans ses règles de noms de colonnes.** Ni `datahub-classify` (`reference_input.py` relu intégralement), ni `piicatcher`, ni les `_pii_column_name_regexes` d'OpenMetadata. Presidio n'a **aucun recognizer français** : son répertoire `predefined_recognizers/country_specific/` couvre l'Australie, le Canada, la Finlande, l'Allemagne, l'Inde, l'Italie, la Corée, le Nigeria, les Philippines, la Pologne, Singapour, l'Afrique du Sud, l'Espagne, la Suède, la Thaïlande, la Turquie, le Royaume-Uni et les États-Unis — **pas la France**.
- **Microsoft Purview l'exclut explicitement**, deux fois dans sa documentation : « Custom classification rules are only supported in the English language. »
- **Le français n'existe, dans tout ce paysage, que comme modèle NLP appliqué aux valeurs** : OpenMetadata sélectionne `fr_core_news_md` via `ClassificationLanguage.fr` ([`constants.py`](https://github.com/open-metadata/OpenMetadata/blob/main/ingestion/src/metadata/pii/constants.py)). Aucun apport pour les noms.
- **NameGuess** filtre ses noms d'entraînement par le **vocabulaire WordNet anglais** et ne mentionne pas le multilingue. **Columbo** non plus. **Sherlock, Sato, DoDuo, Telkamp & Hulsebos** : anglais exclusivement.

### 8.2 Le seul chiffre français trouvé, et ce qu'il vaut

[MultiSpider 2.0 / « Multilingual Text-to-SQL »](https://arxiv.org/html/2509.24405v1) est le seul travail lu qui **localise le schéma lui-même** — noms de tables, noms de colonnes, énumérations — dans huit langues dont le français, en préservant les identifiants canoniques.

| | Anglais | Français |
| --- | --- | --- |
| MultiSpider 1.0 (schémas académiques) | 94,95 % | **91,97 %** |
| MultiSpider 2.0 (schémas d'entreprise) | 15,92 % | **15,14 %** |

⚠️ **Lecture, et elle est à double tranchant.** La pénalité française est de **3 points** sur 1.0 et de **0,8 point** sur 2.0 — modeste. Un gros modèle multilingue comprend donc bien un schéma français. **Mais l'effondrement de 95 % à 16 % ne vient pas de la langue : il vient de la taille et de la complexité des schémas réels d'entreprise.** Le papier le dit lui-même — « a performance drop of over 75 percentage points ». Pour #122, dont l'entrée est précisément un recensement de schéma d'entreprise, c'est l'avertissement le plus dur du dossier : **le passage du schéma de laboratoire au schéma client coûte bien plus cher que le passage de l'anglais au français.**

### 8.3 Ce qui reste utilisable, et ce qui casse

**Ce qui casse :**

- **Tout lexique publié.** `firstname`, `dob`, `zipcode`, `ssn`, `cc_num` : rien de tout cela n'apparaît dans un schéma français. Le lexique est à écrire de zéro. C'est du travail borné, mais c'est du travail que personne n'a fait publiquement.
- **La segmentation appuyée sur un dictionnaire anglais.** `split_column_name` d'OpenMetadata est purement syntaxique et fonctionne (`dt_naiss` → `["dt","naiss"]`), mais tout ce qui vient après — reconnaître les jetons — s'effondre. Et la littérature de découpe d'identifiants (§ 3.2) se scinde exactement là :
  - **Famille lexicale** — greedy, TIDIER, TRIS, GenTest, LINSEN, **Ronin** — dépend d'un dictionnaire de langue. Ronin charge NLTK `words` + WordNet : **anglais en dur**. ⚠️ Ces algorithmes ne se contentent pas d'échouer sur du français, ils **hallucinent** : ils cherchent la découpe qui maximise le score contre un dictionnaire anglais, et `naiss` finira découpé en jetons anglais plausibles. Le taux de sur-segmentation de 10 % mesuré pour le greedy l'a été **en anglais avec le bon dictionnaire** — c'est un plancher, pas un plafond, hors langue.
  - **Famille fréquentielle pure** — **Samurai** — ne contient aucune connaissance lexicale et fonctionnerait en français, à condition de lui fournir des tables de fréquences minées sur un corpus **français** (idéalement : nos propres schémas). Mais c'est celle qui échoue le plus sur le mono-casse : **29 découpes correctes sur 249**.

  ⚠️ **Le choix est donc forcé et désagréable : l'algorithme agnostique à la langue est le plus faible sur notre morphologie, et le plus fort exige un dictionnaire qui n'existe pas.**
- **L'expansion d'abréviations à la NameGuess.** Le mécanisme de fabrication du corpus repose sur WordNet anglais. Reproduire NameGuess en français demanderait un vocabulaire français et une table d'abréviations françaises. Aucun des deux n'existe publiquement pour ce cas d'usage.
- **Les identifiants administratifs.** `nir`, `siret`, `siren`, `cp`, `civilite` n'ont d'équivalent dans **aucun** lexique lu. Le NIR (13 chiffres + clé de contrôle à 2 chiffres, [définition INSEE](https://www.insee.fr/fr/metadonnees/definition/c1409)) et le SIRET (9 chiffres de SIREN + 5 de NIC, [définition INSEE](https://www.insee.fr/fr/metadonnees/definition/c2047)) sont des objets nationaux dont un moteur anglophone n'a jamais entendu parler.

**Ce qui reste :**

- **Le mécanisme de Cupid**, qui exige un thésaurus mais ne présuppose aucune langue : tokenisation → **expansion des abréviations par thésaurus** → élimination → comparaison. Un thésaurus français d'abréviations métier (`dt`→date, `naiss`→naissance, `cli`→client, `adr`→adresse, `tel`→téléphone, `cp`→code postal, `civ`→civilité, `nir`, `siret`, `mnt`→montant, `lib`→libellé) est **le composant que toute cette littérature désigne comme décisif**, et c'est un travail borné et faisable. Rahm & Bernstein en donnent le devis honnête : « require a substantial effort to be built up in a consistent way. The effort is well worth the investment. »
- **La table de fréquences à la Samurai, dérivée de nos propres schémas** — le seul ingrédient qui ne coûte ni corpus ni langue, seulement de la donnée qu'on a déjà.
- **La forme des règles**, qui est indépendante de la langue : la hiérarchie *égalité exacte après normalisation* → *jeton entier* → *sous-chaîne* de `datahub-classify` (1,0 / 0,65 / 0) se réécrit en français sans rien perdre — et c'est elle, et non le vocabulaire, qui porte le **degré de doute** de la décision de cadrage 5.
- **La segmentation syntaxique** — délimiteurs, camelCase, chiffres terminaux — qui ne dépend d'aucune langue. `CLI_NOM_1` → `["cli","nom","1"]` marche exactement pareil en français.
- **Le retour d'expérience sur les faux positifs**, qui transpose directement et qu'il vaut mieux importer que redécouvrir : `"cid" in "acid level"` devient `"nom" in "nomenclature"`, `"adr" in "cadre"`, `"cp" in "cpt_general"`, `"civ" in "civil"`. La liste anglaise ne sert pas ; la **leçon** sert.
- **Le principe du `context` de Presidio** — un nom de colonne qui ne détecte pas mais qui hausse un score — est un mécanisme, pas un lexique. Il se transpose.
- **La CNIL** fournit la matière d'une taxonomie francophone plutôt qu'un lexique de noms : identification directe (« nom, prénom, pseudonyme, date de naissance »), identification indirecte par croisement, catégories particulières (santé, orientation sexuelle, origine, opinions, biométrie, génétique) — [Identifier les données personnelles](https://www.cnil.fr/fr/identifier-les-donnees-personnelles). C'est un point d'appui pour la **taxonomie fermée** que #122 doit écrire, pas pour le moteur.

**Conclusion de section.** Il n'y a pas d'état de l'art francophone à reprendre sur ce signal ; il y a un état de l'art anglophone dont **les formes** se transposent et dont **le vocabulaire** ne se transpose pas. Ce dépôt est déjà passé par là : le § 7.6 de la spec de `Qualification` note que « le lexique, bâti sur des mots-clés français, rendra `OutOfScope` » sur un texte anglais. La même asymétrie, en miroir, gouverne ici.

---

## 9. Ce qui départage les familles

⚠️ **Ce document ne choisit pas le moteur.** Ce qui suit n'est pas un classement mais une liste des **dimensions sur lesquelles les familles se séparent réellement**, avec l'état de la preuve pour chacune — pour que le banc d'essai de #122 mesure ce qui discrimine, et pas ce qui est facile à mesurer.

| Ce qui départage | Ce qui est **mesuré** en source primaire | Ce qui ne l'est **pas** |
| --- | --- | --- |
| **Exactitude sur noms propres** | Valentine : les méthodes de nom sont **parfaites** sur schémas verbatim | rien sur une taxonomie de données personnelles |
| **Exactitude sur noms abrégés / préfixés** | Valentine : **rappel médian ≈ 0,6**, COMA-Schema à 0,121 sur ING#2 ; Cupid : « much worse » sur schéma relationnel plat ; Magneto : « column headers alone are insufficient » | idem, et rien en français |
| **Découpe d'un identifiant mono-casse** | Samurai **29/249**, greedy à dictionnaire **125/249**, TIDIER **54 %** contre 31 % pour Samurai | aucun chiffre hors anglais |
| **Expansion d'abréviations, nom seul** | NameGuess sans contenu de table : GPT-4 **69,3 %**, GPT-Neo 2,7B **40,6 %**, GPT-2 124M **10,4 %** | aucun modèle local mesuré, aucun français |
| **Apport marginal du nom quand on a les valeurs** | DoDuo+metadata : **+0,34 point** | l'expérience symétrique (valeurs retirées) n'existe nulle part |
| **Apport du type SQL et des contraintes** | Valentine : **aucun** en présence de bruit ; Cupid : plafonné à 0,5 ; `datahub-classify` : poids **0** | l'usage en **rejet** (plutôt qu'en attribution) n'est mesuré nulle part |
| **Apport des commentaires de colonne** | rien | **tout** — c'est le seul signal de prose française disponible, et personne ne l'a isolé |
| **Coût, ordre de grandeur** | Valentine : **1,67–9,64 s** (schéma) contre **318–4 818 s** (valeurs/plongements) par paire de tables ; Sherlock : 0,01 s (motifs) contre 0,42 s (réseau) par colonne | **aucune mesure CPU** d'aucune famille sur des identifiants de schéma |
| **Contention avec un moteur en production** | ce dépôt, carte #42 : **832 s CPU huit cœurs saturés** à l'entraînement, **34 ms / 267 Mio / 44 ms d'exposition du lexique** à l'inférence ; § 5.8 de la spec : le lexique coûte **~0,5 ms** | rien dans la littérature — c'est une propriété de notre déploiement, pas des méthodes |
| **Dépendance GPU** | Telkamp & Hulsebos : Qwen3 8B = **0,46 s/colonne sur 8 Gio de GPU** ; Magneto = A100 ; Columbo = GPT-4o uniquement | aucune variante CPU publiée |
| **Aptitude à porter un degré de doute dérivé de la règle** (décision 5) | `datahub-classify` structure **1,0 / 0,65 / 0** selon la forme du match ; `piicatcher` ne produit **aucun** score | aucune évaluation de la calibration de ces niveaux |
| **Aptitude à produire un motif en prose** (décision 6) | trivial pour lexique/règles/morphologie ; **impossible sans génération** pour les plongements | — |
| **Auto-hébergement** (ADR-0001) | Columbo et Magneto reposent sur des API tierces ; Presidio, OpenMetadata, `piicatcher`, Samurai, Cupid sont locaux | — |

**Trois asymétries structurelles à porter au protocole de mesure**, parce qu'elles ne se lisent pas dans un tableau de F1 :

1. **Le coût et l'exactitude vont dans des sens opposés, et l'écart de coût est bien plus grand que l'écart d'exactitude.** Trois ordres de grandeur sur le coût, contre au mieux quelques dizaines de points sur l'exactitude. Sur une carte dont la contrainte dure est la contention CPU/GPU avec un moteur en production, ce n'est pas un arbitrage symétrique.
2. **La performance dépend plus du dictionnaire que de l'algorithme.** Trois sources indépendantes, sur trois décennies, le disent : Cupid 2001, TIDIER 2013, Columbo 2025 (« sans les règles : −28,6 points », contre « sans le raisonnement pas-à-pas : −6,8 »). ⚠️ **Un banc qui comparerait des familles à dictionnaire constant mesurerait surtout le dictionnaire.** Le protocole devra dire lequel il fournit, à qui, et pourquoi.
3. **Le régime de sortie n'est pas neutre.** ReMatch passe de 0,42 à 0,78 en passant du premier choix aux cinq premiers. Une carte en posture `Aide à la décision`, dont un humain arbitre ligne par ligne, n'a peut-être pas besoin d'un moteur qui a raison du premier coup.

---

## 10. Points non vérifiés en source primaire

Dans la tradition de la recherche #44 de ce dépôt, qui s'est close sur 27 points non vérifiés.

**Sur les papiers**

1. **TURL** (arXiv 2006.14806) — la composition exacte de la représentation d'entrée (légende, en-têtes, cellules-entités) et ce qui est fourni précisément à la tâche aval d'annotation de type. Deux tentatives de récupération n'ont rendu que le résumé. **C'est le seul papier de la famille dont l'entrée pourrait inclure les en-têtes**, et c'est donc le trou le plus gênant de la section 1.
2. **Korini & Bizer** — la lecture retenue (« valeurs seules dans le prompt ») vient d'une extraction automatique du PDF ; une autre extraction du même papier a répondu l'inverse (« we provide the column header, a few sample cell values »), et une troisième a désigné SemTab plutôt que SOTAB. **La contradiction n'est pas levée.** Ne pas s'appuyer sur ce point sans relire le PDF à la main.
3. **Sato** — aucun chiffre de latence, de mémoire ni de matériel n'a été trouvé dans le papier.
4. **DoDuo** — aucun chiffre de latence à l'inférence. Le tableau `Doduo+metadata` n'existe que pour WikiTable ; aucun résultat équivalent sur VizNet.
5. **Telkamp & Hulsebos** — le mode d'exécution exact des lignes de base Google DLP et Presidio (valeurs seules ? valeurs + en-têtes ?) n'est pas explicité dans ce qui a pu être lu. Les F1 de 0,58 et 0,57 sont donc **des chiffres dont on ne connaît pas exactement les conditions**.
6. **Columbo** — aucun chiffre de latence, de coût ni de matériel. Aucune évaluation sur modèle local.
7. **NameGuess** — aucun chiffre de latence à l'inférence ; seul le matériel d'affinage (8 A100) est donné.
8. **MultiSpider 2.0** — l'existence d'une condition expérimentale séparant « question localisée / schéma en anglais » de « question et schéma localisés » n'a pas été confirmée. Sans elle, on ne peut pas attribuer la pénalité de 3 points au seul schéma.
9. **`NERPII`** — page du papier inaccessible (serveur injoignable). Ce que fait exactement cette bibliothèque (NER sur les valeurs ou sur les noms) n'est **pas** établi. Ne pas la citer comme exemple ni comme contre-exemple.
10. **Brevet US 11768916 B2 / US 11379601 B2** (PayPal) — le PDF de l'office est une image sans couche texte, et la page Google Patents correspondante n'a rendu ni la revendication 1 ni le détail de l'extracteur de traits de métadonnées. L'affirmation « les métadonnées sont un trait parmi d'autres » repose sur un résumé de moteur de recherche, **pas sur le texte de la revendication**.
11. **Pythagoras, AdaTyper, ArcheType, Watchog, RECA, KGLink** et les autres successeurs croisés en recherche n'ont **pas** été lus. Rien ne dit qu'aucun d'eux ne prend le nom en entrée ; simplement, aucun ne s'est signalé comme tel.
12. **ReMatch** — chiffres extraits de la version **HTML v1** sur arXiv par un modèle d'extraction, non relus dans le PDF final. **Confiance moyenne.** Ce sont pourtant les chiffres nom-seul les plus encourageants du dossier ; à revalider avant de s'en servir.
13. **Matchmaker** (Seedat & van der Schaar, 2024) et **KcMF** ([arXiv 2410.12480](https://arxiv.org/abs/2410.12480)) — identifiés, non lus, aucun chiffre.
14. **COMA** (VLDB 2002) et **Similarity Flooding** (ICDE 2002) — papiers non lus directement. Leur description repose sur le survey Rahm & Bernstein et sur la réimplémentation qu'en fait Valentine.
15. **Magneto** — les chiffres par jeu de données Valentine sont dans des **figures**, pas des tables ; seules la Table 4 (ablation d'échantillonnage) et la section de passage à l'échelle ont été lues.

**Sur la littérature de découpe d'identifiants**

16. **AMAP** (Hill et al., MSR 2008, [DOI 10.1145/1370750.1370771](https://dl.acm.org/doi/10.1145/1370750.1370771)) — PDF ACM inaccessible (403). **Aucun chiffre.** Le mécanisme décrit (recherche d'expansion dans des portées croissantes du code lui-même, plutôt que dans un dictionnaire) vient de sources secondaires.
17. **Lawrie, Binkley, Morrell, « Normalizing Source Code Vocabulary », WCRE 2010** (algorithme **GenTest**) — PDF inaccessible. Exactitude non vérifiée ; sa dépendance à « an English dictionary, and a list of well-known abbreviations » vient d'un passage de la thèse Guerrouj.
18. **LINSEN** (Corazza et al., ICSM 2012) — aucun chiffre d'exactitude vérifié ; seule la revendication de linéarité en taille de dictionnaire est corroborée par le résumé.
19. **TRIS** — les valeurs exactes de précision/rappel/F-mesure (tables 6.9, 6.11, 6.13, 6.14 de la thèse) n'ont pas été lues. Seules les affirmations qualitatives de l'abstract sont reprises.
20. **« An empirical study of identifier splitting techniques », EMSE 2014** ([DOI 10.1007/s10664-013-9261-0](https://link.springer.com/article/10.1007/s10664-013-9261-0)) — paywall. C'est la comparaison croisée la plus complète camelCase / Samurai / GenTest / TIDIER, et elle manque au dossier.
21. **Coût CPU de Samurai et Ronin** — **aucun benchmark de vitesse publié**. L'affirmation « quasi gratuit » est une déduction de la structure de l'algorithme (deux consultations de table de hachage par position de coupure), pas un résultat.

**Sur les outils**

22. **Microsoft Purview** — qu'une règle avec `Column Pattern` **seul**, sans `Data Pattern`, classe effectivement une colonne. Les deux champs sont marqués « Optional », mais la documentation ne le confirme pas. À éprouver, pas à supposer.
23. **Google Cloud DLP** — la nature exacte des « additional clues » que la structure et les colonnes apportent en mode table. Moteur fermé, aucune documentation plus précise trouvée. Aucun infoType natif documenté comme fondé sur le nom.
24. **Purview, Macie, DLP** — aucune donnée de coût machine (services facturés au volume).
25. **Amundsen, Apache Atlas, Great Expectations** — non investigués dans le code. L'hypothèse (tags externes, classifications manuelles, expectations écrites à la main) n'est **pas** vérifiée.
26. **Les versions de code** ne sont pas épinglées : clones superficiels des branches `main` au 2026-08-08.

**Sur le coût**

27. **Aucune mesure CPU** n'existe, dans aucune source lue, pour aucune des familles décrites, à l'exception des chiffres généralistes de spaCy et de fastText — qui portent sur de la prose, pas sur des identifiants de schéma. **Le critère le plus dur de la carte #122 devra être mesuré par elle, comme la recherche #44 l'avait déjà conclu pour la carte #42.**
28. Les temps de Valentine sont par **paire de tables** et sur matériel non précisé : ils ordonnent les familles, ils ne dimensionnent rien.

**Sur le français**

29. L'absence de terme français a été vérifiée par lecture des fichiers de règles de `datahub-classify`, `piicatcher` et OpenMetadata, et par inventaire du répertoire de recognizers de Presidio. **Elle n'a pas été vérifiée exhaustivement sur l'ensemble des dépôts** — un terme français isolé ailleurs dans ces bases de code n'est pas exclu.
30. **Aucun corpus public de schémas SQL français annotés** n'a été trouvé. Si le banc de #122 en a besoin, il devra le fabriquer. Cette recherche n'a pas cherché systématiquement du côté des applications libres francophones ; c'est un travail distinct.
31. **Aucune évaluation d'aucune des méthodes de ce document sur des identifiants francophones n'a été trouvée.** Toute la section 8.3 (« ce qui reste, ce qui casse ») est une **déduction argumentée à partir des dépendances documentées**, pas un résultat mesuré. C'est, au terme de cette recherche, un trou dans la littérature — et l'endroit exact où le banc de #122 produirait un résultat que personne n'a.

---

### Note sur les bancs existants, pour le ticket de protocole de mesure

Trois jeux publics croisés pourraient servir de point de départ, sous réserve de vérification — **aucun n'est en français**, et ce document n'en a validé ni la licence ni le contenu :

- Le **banc PII de GitTables** de [Telkamp & Hulsebos](https://arxiv.org/html/2512.04120) — 66 tables, **2 061 colonnes annotées à la main** par type PII et niveau de sensibilité contextuelle. Implémentation annoncée comme ouverte. C'est le plus proche de la taxonomie de #122.
- Le banc humain de [**NameGuess**](https://arxiv.org/html/2310.13196v1) — 9 218 noms de colonnes abrégés issus de données ouvertes municipales, avec expansions annotées et stratification par difficulté.
- Les jeux de [**Valentine**](https://github.com/delftdata/valentine), pour la seule question de la dégradation morphologique — leur protocole de bruitage (préfixe de table, suppression des voyelles) est **réutilisable tel quel** sur un schéma français, et c'est peut-être ce qu'il a de plus précieux pour nous.
