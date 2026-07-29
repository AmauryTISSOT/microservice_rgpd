# Recensement — modèles et outillage francophones auto-hébergeables pour un moteur non génératif

- **Date** : 2026-07-29
- **Ticket** : [#44 — Modèles et outillage concrets, auto-hébergeables et francophones](https://github.com/AmauryTISSOT/microservice_rgpd/issues/44)
- **Carte** : [#42 — Un troisième moyen de détection, non génératif, pour affiner le diagnostic](https://github.com/AmauryTISSOT/microservice_rgpd/issues/42)
- **Contraintes** : [ADR-0001](../adr/0001-architecture-polyglotte-et-moteur-auto-heberge.md)

> **Ce document ne tranche pas.** Il recense la matière du choix ; le choix est le ticket #47.
> Là où une contrainte de la carte élimine mécaniquement un candidat — licence non commerciale,
> exigence de GPU, anglais seulement — l'élimination est notée comme telle. Ce n'est pas un
> arbitrage, c'est l'application du filtre que la carte a posé avant d'ouvrir ce ticket.

---

## 1. Le cadre, rappelé

Le troisième moteur qualifie un texte libre **français** vers 7 étiquettes, en **multi-étiquettes**,
et sert de **second témoin** : il n'entre jamais dans la `Qualification` rendue, il enrichit le
`ReviewSignal`. Quatre contraintes filtrent tout ce qui suit.

| Contrainte | Origine | Effet sur le recensement |
| --- | --- | --- |
| **Auto-hébergement strict** | ADR-0001, art. 28 RGPD | tout modèle disponible seulement derrière une API est hors sujet, quelle que soit sa qualité |
| **Pas de GPU** | ADR-0001 — 8 Go de VRAM déjà pris par `qwen3:8b` | **critère éliminatoire**, pas une note de bas de page |
| **Déterminisme** | ADR-0001 — le service ne reprend jamais | une bibliothèque non reproductible casse une propriété acquise |
| **Sobriété des dépendances** | `src/sidecar/pyproject.toml` | 4 dépendances de production aujourd'hui : `fastapi`, `openai`, `pydantic`, `uvicorn` |

**Le corpus, mesuré.** 120 exemples, 7 étiquettes, distribution constatée : `hors-perimetre` 30,
`effacement` 26, `acces` 23, `opposition` 20, `rectification` 14, `limitation` 14, `portabilite` 13.
Longueur des textes : minimum 1 caractère, médiane 102, maximum 1195.

Deux conséquences que le recensement doit garder en tête :

1. **La médiane à 102 caractères est trompeuse.** Le maximum à 1195 caractères dépasse largement les
   **128 jetons** auxquels plafonnent plusieurs modèles ci-dessous — et la troncature y est
   silencieuse. C'est un critère de tri à part entière, pas un détail.
2. **Le coût d'encodage du corpus d'entraînement est négligeable** (120 textes, une fois, mis en
   cache). Le coût CPU qui compte est la **latence par requête en production**.

---

## 2. Méthode et statut des sources

Chaque fait a été remonté à la source qui le détient : l'API Hugging Face du dépôt lui-même
(`/api/models/<id>?blobs=true`, qui rend le champ `license` du front-matter et la taille en octets de
chaque fichier de poids), les JSON PyPI, les documentations officielles, le code source, les articles.

**Ce qui a été revérifié directement**, parce que porteur de conséquence :

- la Table 9 de l'article MTEB-French, recopiée depuis le HTML arXiv de la v2 — les chiffres du § 4 sont **confirmés**, la réserve initiale est levée ;
- la licence `cc-by-nc-4.0` de `jinaai/jina-embeddings-v3` (API HF) ;
- la licence CC-BY-SA-3.0 des vecteurs fastText (page officielle, verbatim) ;
- la licence MIT de `intfloat/multilingual-e5-small` (API HF) ;
- la licence Apache-2.0 et la liste de langues incluant `fr` de `ibm-granite/granite-embedding-107m-multilingual` (API HF) ;
- les chiffres du billet *Static Embeddings* de Hugging Face ;
- l'issue [ollama#15609](https://github.com/ollama/ollama/issues/15609) et ses mesures ;
- les métadonnées PyPI d'`onnxruntime` 1.28.0.

Le § 12 récapitule ce qui **n'a pas pu être vérifié en source primaire**. Rien de ce qui figure dans
ce document ne doit être cité sans avoir consulté ce paragraphe.

---

## 3. Les modèles de plongement — tableau de recensement

Tailles = fichier de poids réel du dépôt, précision indiquée. La colonne « Class. fr » est le score
**Classification** de MTEB-French (§ 4) — c'est la métrique pertinente ici, pas *Retrieval*.

### 3.1 Candidats compatibles avec toutes les contraintes

| Identifiant | Params | Dim | Ctx | Poids | Licence | Class. fr |
| --- | --- | --- | --- | --- | --- | --- |
| `intfloat/multilingual-e5-small` | 117,7 M | 384 | 512 | 470,6 Mo | MIT | 0,60 |
| `ibm-granite/granite-embedding-107m-multilingual` | 107,0 M | 384 | 512 | 214,0 Mo (bf16) | Apache-2.0 | non rapporté |
| `sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2` | 117,7 M | 384 | **128** | 470,6 Mo fp32 / **119,0 Mo** int8 OpenVINO | Apache-2.0 | 0,60 |
| `Lajavaness/bilingual-embedding-small` | 117,7 M | 384 | 512 | 470,6 Mo | Apache-2.0 | non rapporté |
| `sentence-transformers/distiluse-base-multilingual-cased-v2` | 134,7 M | **512** | **128** | 538,9 Mo + 1,6 Mo | Apache-2.0 | 0,64 |
| `sentence-transformers/distiluse-base-multilingual-cased-v1` | 134,7 M | **512** | **128** | 538,9 Mo + 1,6 Mo | Apache-2.0 | non rapporté |
| `Lajavaness/sentence-camembert-base` | 110,6 M | 768 | n. v. | 442,5 Mo | Apache-2.0 | non rapporté |
| `Lajavaness/sentence-flaubert-base` | 137,3 M | 768 | n. v. | 549,3 Mo | Apache-2.0 | non rapporté |
| `dangvantuan/sentence-camembert-base` | 110,6 M | 768 | **128** | 442,5 Mo | Apache-2.0 | 0,57 |
| `sentence-transformers/static-similarity-mrl-multilingual-v1` | table statique | 1024 (MRL) | illimité | 433,7 Mo | Apache-2.0 | non rapporté |

### 3.2 Candidats au coût CPU à mesurer avant de conclure

| Identifiant | Params | Dim | Ctx | Poids | Licence | Class. fr |
| --- | --- | --- | --- | --- | --- | --- |
| `OrdalieTech/Solon-embeddings-base-0.1` | 278,0 M | 768 | 512 | 1 112,2 Mo | MIT | **0,67** |
| `intfloat/multilingual-e5-base` | 278,0 M | 768 | 512 | 1 112,2 Mo | MIT | 0,65 |
| `sentence-transformers/paraphrase-multilingual-mpnet-base-v2` | 278,0 M | 768 | **128** | 1 112,2 Mo fp32 / 279,4 Mo int8 | Apache-2.0 | 0,63 |
| `ibm-granite/granite-embedding-278m-multilingual` | 278,0 M | 768 | 512 | 556,1 Mo (bf16) | Apache-2.0 | non rapporté |
| `Alibaba-NLP/gte-multilingual-base` | 305,4 M | 768 | 8192 | 610,8 Mo (fp16) | Apache-2.0 | non rapporté |
| `dangvantuan/french-document-embedding` | 305,4 M | 768 | 8192 | 1 221,5 Mo | Apache-2.0 | non rapporté |
| `Lajavaness/bilingual-embedding-base` | 278,0 M | 768 | 512 | 1 112,2 Mo | Apache-2.0 | non rapporté |
| `Snowflake/snowflake-arctic-embed-m-v2.0` | 305,4 M | 768 | 8192 | 1 221,5 Mo | Apache-2.0 | non rapporté |

### 3.3 Écartés par une contrainte de la carte

| Identifiant | Poids | Licence | Motif de l'élimination |
| --- | --- | --- | --- |
| `jinaai/jina-embeddings-v3` | 1 144,7 Mo | **cc-by-nc-4.0** | **licence non commerciale** — voir § 5 |
| fastText `cc.fr.300` | 4,50 Go compressé, ~7 Go en RAM | **CC-BY-SA-3.0** | licence à partage à l'identique **et** empreinte mémoire — voir § 5 |
| `manu/sentence_croissant_alpha_v0.4` | 2 559,8 Mo, 1,28 G params | MIT | coût CPU éliminatoire |
| `antoinelouis/colbert-xm` | 3 410,8 Mo | MIT | coût CPU **et** représentation multi-vecteurs, incompatible avec « plongement + classifieur » |
| `BAAI/bge-m3` | 2 271,1 Mo | MIT | 560 M paramètres sur CPU — meilleur score français ouvert (0,69), coût à peser |
| `OrdalieTech/Solon-embeddings-large-0.1` | 2 239,6 Mo | MIT | idem, 0,69 |
| `intfloat/multilingual-e5-large` | 2 239,6 Mo | MIT | idem, 0,66 |
| `sentence-transformers/LaBSE` | 1 883,7 Mo | Apache-2.0 | idem, 0,65 |
| `nomic-ai/nomic-embed-text-v2-moe` | 1 901,2 Mo | Apache-2.0 | idem |
| `EuroBERT/EuroBERT-210m` et `-610m` | 1 241,1 / 3 022,5 Mo | Apache-2.0 (⚠️ § 5) | MLM brut, pas un encodeur de phrases |
| `mixedbread-ai/mxbai-embed-large-v1` | 670,3 Mo | Apache-2.0 | **anglais seulement** |
| `nomic-ai/nomic-embed-text-v1.5` | 546,9 Mo | Apache-2.0 | **anglais seulement** |
| `answerdotai/ModernBERT-base` | 598,6 Mo | Apache-2.0 | anglais, et MLM brut |
| `antoinelouis/crossencoder-camembert-base-mmarcoFR` | 442,5 Mo | MIT | c'est un *reranker* : il ne produit aucun vecteur, il ne peut pas alimenter un classifieur |
| `almanach/camembert-base`, `camembertv2-base`, `camembertav2-base`, `moderncamembert-base` | 442–545 Mo | MIT | **MLM bruts** — MTEB-fr Classification 0,42, Retrieval 0,02 |
| `almanach/camembert-bio-base` | 442,7 Mo | MIT | domaine biomédical |
| `FacebookAI/xlm-roberta-base` | 1 115,6 Mo | MIT | MLM brut — Classification 0,31 |
| `flaubert/flaubert_base_cased` | 553,2 Mo | MIT | MLM brut — Classification 0,25 |
| `dbmdz/bert-base-french-europeana-cased` | 445,0 Mo | MIT | corpus OCR historique |

`n. v.` = non vérifié à la source.

**Un point à ne pas manquer sur les MLM bruts.** `camembert-base` est le modèle français le plus
connu, et c'est précisément le piège : utilisé directement comme encodeur de phrases, il obtient 0,42
en Classification et 0,02 en Retrieval. Un modèle de langue masquée n'est pas un encodeur de phrases.
L'affiner sur 120 exemples n'est pas une réponse raisonnable — c'est ce que la ligne « Supervisé
classique » du tableau des alternatives écartées de l'ADR-0001 avait déjà pressenti.

---

## 4. Les performances françaises — MTEB-French

**Source primaire.** Ciancone, Kerboua, Schaeffer, Siblini, *MTEB-French: Resources for French
Sentence Embedding Evaluation and Analysis*, [arXiv:2405.20468](https://arxiv.org/abs/2405.20468)
(30 mai 2024, révisé le 17 juin 2024). 51 modèles évalués sur 8 catégories de tâches, 15 jeux de
données existants plus 3 créés pour le français. Code : [Lyon-NLP/mteb-french](https://github.com/Lyon-NLP/mteb-french).

**Table 9, annexe D — « Average performance of models per task type »**, recopiée depuis
[le HTML de la v2](https://arxiv.org/html/2405.20468v2). Métriques brutes : Classification =
exactitude, Clustering = V-measure, PairClassification = AP, Reranking = MAP, Retrieval = nDCG@10,
STS/Summarization = Spearman, BitextMining = F1.

| Modèle | Moy. | Bitext | **Class.** | Clust. | PairClass. | Rerank | Retr. | STS | Summ. |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| bge-m3 | 0,68 | 0,95 | **0,69** | 0,43 | 0,77 | 0,81 | 0,65 | 0,81 | 0,31 |
| Solon-embeddings-large-0.1 | 0,67 | 0,96 | **0,69** | 0,42 | 0,77 | 0,79 | 0,63 | 0,80 | 0,30 |
| sentence_croissant_alpha_v0.3 | 0,67 | 0,92 | 0,66 | 0,46 | 0,79 | 0,78 | 0,65 | 0,77 | 0,31 |
| multilingual-e5-large | 0,66 | 0,95 | 0,66 | 0,40 | 0,76 | 0,76 | 0,59 | 0,81 | 0,31 |
| sentence-camembert-large | 0,65 | 0,90 | 0,66 | 0,43 | 0,77 | 0,72 | 0,56 | 0,82 | 0,31 |
| multilingual-e5-base | 0,65 | 0,95 | 0,65 | 0,43 | 0,75 | 0,75 | 0,56 | 0,78 | 0,31 |
| Solon-embeddings-base-0.1 | 0,64 | 0,95 | **0,67** | 0,43 | 0,76 | 0,78 | 0,41 | 0,78 | 0,31 |
| multilingual-e5-small | 0,63 | 0,94 | 0,60 | 0,39 | 0,75 | 0,73 | 0,52 | 0,78 | 0,32 |
| paraphrase-multilingual-mpnet-base-v2 | 0,63 | 0,94 | 0,63 | 0,40 | 0,76 | 0,74 | 0,50 | 0,78 | 0,30 |
| paraphrase-multilingual-MiniLM-L12-v2 | 0,60 | 0,93 | 0,60 | 0,39 | 0,74 | 0,68 | 0,44 | 0,75 | 0,29 |
| distiluse-base-multilingual-cased-v2 | 0,60 | 0,94 | 0,64 | 0,39 | 0,72 | 0,69 | 0,40 | 0,75 | 0,28 |
| LaBSE | 0,59 | 0,96 | 0,65 | 0,39 | 0,74 | 0,61 | 0,33 | 0,74 | 0,30 |
| sentence-camembert-base | 0,57 | 0,72 | 0,57 | 0,36 | 0,74 | 0,66 | 0,43 | 0,78 | 0,29 |
| xlm-roberta-base | 0,36 | 0,48 | 0,31 | 0,28 | 0,68 | 0,30 | 0,01 | 0,51 | 0,29 |
| camembert-base | 0,35 | 0,18 | 0,42 | 0,34 | 0,68 | 0,31 | 0,02 | 0,57 | 0,30 |
| flaubert_base_cased | 0,34 | 0,23 | 0,25 | 0,27 | 0,67 | 0,36 | 0,08 | 0,52 | 0,31 |
| *text-embedding-ada-002 (API — hors sujet ici, donné en repère)* | 0,69 | 0,95 | 0,69 | 0,51 | 0,77 | 0,82 | 0,67 | 0,78 | 0,30 |

### Le fait le plus utile de ce tableau

**La colonne Classification est nettement plus plate que les autres.** Entre le plus petit modèle
utile (`multilingual-e5-small`, 118 M) et le meilleur modèle ouvert (`bge-m3`, 560 M), l'écart est de
**9 points en Classification** — mais de **21 points en Retrieval**. Autrement dit : la tâche qui
ressemble à la nôtre est celle où la taille du modèle rapporte le moins.

Deux corollaires pour le choix à venir :

- Les classements généralistes, dominés par les tâches de recherche, **surestiment** l'intérêt des
  gros modèles pour ce cas d'usage précis.
- `Solon-embeddings-base-0.1` mérite d'être noté : **0,67 en Classification pour 278 M paramètres**,
  soit mieux que `multilingual-e5-base` (0,65) à taille égale, et à deux points seulement des modèles
  deux fois plus gros.

**Aucun score MTEB-fr publié n'a été trouvé** pour `Lajavaness/bilingual-embedding-*` (la carte du
modèle affiche littéralement « TODO » dans sa section évaluation), `Alibaba-NLP/gte-multilingual-base`,
`jinaai/jina-embeddings-v3`, `nomic-embed-text-v2-moe`, `granite-embedding-*`,
`snowflake-arctic-embed2` et `static-similarity-mrl-multilingual-v1`. Ces modèles sont **postérieurs à
l'article**. Leurs scores figurent sur le [classement MTEB(fra) en ligne](https://huggingface.co/spaces/mteb/leaderboard),
qui est un Space Gradio **non interrogeable programmatiquement** — donc non vérifié ici.

⚠️ **Une réserve de méthode qui vaut pour tout le tableau.** MTEB-French mesure des tâches françaises
génériques, pas la qualification de demandes RGPD en multi-étiquettes sur 7 classes. Ces chiffres
ordonnent des candidats ; ils ne prédisent pas le résultat sur les 120 exemples. La carte a d'ailleurs
prévu l'instrument qui, lui, le mesurera : la validation croisée stratifiée à hyperparamètres imbriqués.

---

## 5. Les licences — vérifiées à la source, pas supposées

Pour un service dont l'unique métier est la conformité, la licence n'est pas une formalité. Elle est
vérifiée ici au front-matter de la carte de modèle, via l'API Hugging Face.

### Les deux problèmes réels

**`jinaai/jina-embeddings-v3` — `cc-by-nc-4.0`.** Confirmé directement sur
`https://huggingface.co/api/models/jinaai/jina-embeddings-v3` : le tag est littéralement
`license:cc-by-nc-4.0`. **NC = non commercial.** Jina vend une licence commerciale séparée. Un
microservice de conformité RGPD exploité en entreprise relève de l'usage commercial. **Le modèle est
hors jeu**, indépendamment de sa qualité technique — et c'est exactement le genre de supposition que
le ticket demandait de ne pas faire.

**Vecteurs fastText `cc.fr.300` — CC-BY-SA-3.0.** Texte verbatim de la
[page officielle](https://fasttext.cc/docs/en/crawl-vectors.html) : « *The word vectors are
distributed under the Creative Commons Attribution-Share-Alike License 3.0* ». La clause de **partage
à l'identique** est le problème : selon l'interprétation retenue, un artefact dérivé — index
vectoriel, classifieur entraîné sur ces vecteurs — peut être considéré comme œuvre dérivée à
repartager sous la même licence. Pour un produit fermé, c'est un risque juridique à faire trancher,
pas à contourner par optimisme.

### Le reste du paysage

| Licence | Modèles | Statut |
| --- | --- | --- |
| **MIT** | `almanach/*`, `intfloat/multilingual-e5-*`, `BAAI/bge-m3`, `OrdalieTech/Solon-*`, `antoinelouis/*`, `manu/sentence_croissant_alpha_v0.4`, `FacebookAI/xlm-roberta-base`, `flaubert/*`, `dbmdz/*` | aucun problème |
| **Apache-2.0** | tous les `sentence-transformers/*`, `Lajavaness/*`, `dangvantuan/*`, `Alibaba-NLP/gte-multilingual-base`, `nomic-ai/*`, `ibm-granite/*`, `Snowflake/*`, `EuroBERT/*`, `mixedbread-ai/*` | aucun problème |
| **Gemma Terms** (non-OSI) | `embeddinggemma:300m` (servi par Ollama) | licence propriétaire avec politique d'usage — à faire valider avant tout usage |
| **LGPL-LR** | modèles spaCy français (`fr_core_news_*`, `fr_dep_news_trf`) | *Lesser GPL For Linguistic Resources* — utilisable, mais ce n'est ni MIT ni Apache |
| **LGPL-2.1-only** | la bibliothèque `gensim` | seule copyleft du lot ; sans effet pour un service auto-hébergé non redistribué, à noter tout de même |

**Aucune clause d'usage acceptable** n'a été trouvée sur les cartes des modèles retenus. Aucun n'est
sous Llama-Community.

⚠️ **Une réserve à porter au dossier.** `EuroBERT/EuroBERT-210m` et `-610m` déclarent `apache-2.0` en
front-matter, et le corps de la carte écrit « Apache 2.0 license » — mais **le dépôt ne contient aucun
fichier `LICENSE`** (liste de fichiers vérifiée). La déclaration ne repose que sur la carte. Sans
conséquence ici puisque ces modèles sont écartés pour un autre motif, mais c'est le type de faille
qu'il faut savoir repérer.

---

## 6. Le coût CPU — critère éliminatoire

**Le constat qui structure tout ce paragraphe : aucune carte de modèle des candidats principaux ne
publie de chiffre d'inférence CPU.** Ni `multilingual-e5-*`, ni `bge-m3`, ni `Solon-*`, ni
`paraphrase-multilingual-*`. Le critère le plus dur de la carte est celui sur lequel l'écosystème est
le plus silencieux. Il devra donc être **mesuré**, pas lu.

### 6.1 Les rares chiffres réellement publiés

**Plongements statiques** — [billet officiel Hugging Face « Static Embeddings »](https://huggingface.co/blog/static-embeddings),
pour `sentence-transformers/static-similarity-mrl-multilingual-v1` :

- « *approximately ~125x faster on CPU and ~10x faster on GPU* » **par rapport à `multilingual-e5-small`** ;
- performance relative conservée : **92,3 % en STS**, **95,52 % en Pair Classification**, **86,52 % en Classification** ;
- évalué sur « *5 languages which have a lot of benchmarks* » ;
- le modèle jumeau anglais `static-retrieval-mrl-en-v1` atteint **107 419,51 textes/s sur CPU**.

C'est une **table de correspondance** : pas de multiplication matricielle, pas de couche d'attention.
Le coût CPU est essentiellement une lecture mémoire. C'est la seule famille où le critère éliminatoire
de la carte devient structurellement non contraignant.

⚠️ **Mais lisez bien la bonne colonne.** Le chiffre le plus cité est le 92,3 % en STS ; celui qui
compte ici est le **86,52 % en Classification** — la tâche la plus proche de la nôtre est aussi celle
où ces modèles perdent le plus. Un facteur 125 en vitesse contre ~13,5 % de performance relative en
Classification : c'est un arbitrage réel, à instruire, pas une évidence.

**Accélération des backends** — [doc officielle sentence-transformers](https://sbert.net/docs/sentence_transformer/usage/efficiency.html),
CPU i7, 1000 échantillons, jeu STSb (textes courts, donc proches de notre médiane) :

| Backend | Accélération vs PyTorch |
| --- | --- |
| ONNX | **1,39×** |
| OpenVINO | 1,29× |
| **ONNX + quantification int8 dynamique** | **3,08×** |

Perte de qualité annoncée de l'ordre de **0,4 %** d'exactitude. Mise en garde officielle explicite :
« *for longer texts, ONNX and OpenVINO can even perform slightly worse than PyTorch* ». Ces chiffres
sont des moyennes sur plusieurs modèles et jeux de données — à revalider sur un corpus français.

La documentation d'ONNX Runtime, elle, **ne publie aucun chiffre** de gain de quantification :
« *The performance improvement depends on your model and hardware.* » Elle précise aussi que
« *it is recommended to use dynamic quantization for RNNs and transformer-based models* » et que le
gain int8 dépend du jeu d'instructions (« *x86-64 with VNNI […] Old hardware has none or few of the
instructions needed to perform efficient inference in int8* »). Le facteur ~4 de réduction de taille
fp32 → int8 est **arithmétique**, pas mesuré.

### 6.2 Ordres de grandeur — déduits, non publiés

⚠️ **Ce qui suit n'est pas sourcé.** Ce sont des estimations pour un texte court (< 128 jetons), un
cœur CPU moderne, fp32. À traiter comme des repères de tri, jamais comme des mesures.

| Classe | Exemples | Latence estimée | RAM estimée |
| --- | --- | --- | --- |
| ~110 M params, 384 d | e5-small, MiniLM-L12, granite-107m | ~5–15 ms/texte | 0,6–0,8 Go |
| ~280–305 M params, 768 d | e5-base, Solon-base, gte-multilingual-base | ~20–50 ms/texte | 1,3–1,6 Go |
| ~560 M params, 1024 d | bge-m3, Solon-large, e5-large | ~80–200 ms/texte | 2,5–3,2 Go |
| 1,28 G params | sentence_croissant_alpha_v0.4 | plusieurs centaines de ms | ≥ 3 Go |

Règle d'empreinte déduite : ≈ 1,2–1,5 × la taille des poids sur disque, plus les activations.

**La lecture qui compte.** Entre `multilingual-e5-small` et `bge-m3`, l'écart est d'environ **un
facteur 10 en latence** pour **9 points de Classification française**. C'est le seul arbitrage
quantifié que ce recensement permet de poser — et c'est au ticket #47 de le trancher.

---

## 7. Les bibliothèques Python — ce que chaque famille coûterait

Le sidecar a 4 dépendances de production. Voici ce que chaque piste ajouterait. Toutes les
métadonnées viennent des JSON PyPI, vérifiés le 2026-07-29.

### 7.1 Vue d'ensemble

| Piste | Nouvelles dépendances | Poids installé (Linux x86_64) | Déterminisme |
| --- | --- | --- | --- |
| **Ollama `/v1/embeddings`** via le client `openai` **déjà présent** | **zéro** | 0 octet côté Python | bon sur CPU, **non contractuel** |
| `onnxruntime` seul (modèle converti hors production) | 1 + numpy/protobuf | **~18,3 Mio** de roue | option existante, non documentée |
| `fastembed` | ~8, **sans torch** | ~40–60 Mio (non vérifié) | hérité d'onnxruntime |
| `sentence-transformers` | transformers + **torch** + scipy + scikit-learn + hf-hub | **≥ 502 Mio** en CPU, **plusieurs Gio** par défaut | doc PyTorch : **pas garanti** |
| `scikit-learn` (le classifieur — requis dans tous les cas) | 5 | ~8,7 Mio + numpy/scipy | **oui, explicitement** |
| `setfit` | tout sentence-transformers **+ datasets + evaluate** | très lourd | hérité de torch |

### 7.2 Le fait saillant : `onnxruntime` contre `torch`

| | `onnxruntime` 1.28.0 | `torch` 2.13.0 |
| --- | --- | --- |
| Roue `cp312` Linux x86_64 | **19 214 257 o ≈ 18,3 Mio** | **526 605 292 o ≈ 502 Mio** |
| Licence | MIT | Apache-2.0 et al. |
| `requires_python` | `>=3.11` | `>=3.10` |
| Classifiers | 3.11 → 3.14 | 3.10 → 3.14 |
| Dépendances | `flatbuffers`, `numpy`, `packaging`, `protobuf` — **aucune trace de torch** | `filelock`, `typing-extensions`, `setuptools`, `sympy`, `networkx`, `jinja2`, `fsspec` |

**Un facteur ~27 sur la seule roue du moteur d'inférence.** Et ce n'est pas le pire.

⚠️ **Le piège CUDA.** Sur Linux x86_64, `pip install torch` tire **par défaut**, via marqueurs
d'environnement : `cuda-toolkit[...]==13.0.3`, `cuda-bindings`, `nvidia-cudnn-cu13==9.20.0.48`,
`nvidia-cusparselt-cu13`, `nvidia-nccl-cu13`, `nvidia-nvshmem-cu13`, `triton==3.7.1`. Trois d'entre
eux ont été mesurés : cuDNN **349 Mio**, Triton **189 Mio**, NCCL **206 Mio**. Avec torch lui-même :
**≈ 1,22 Gio de téléchargement vérifiés**, avant même `cuda-toolkit`. Sur une machine où **le GPU est
interdit au troisième moteur**, c'est du poids mort intégral.

L'index CPU-only existe (`https://download.pytorch.org/whl/cpu`, roues suffixées `+cpu`, sans aucune
dépendance `nvidia-*` ni `triton`). Piège opérationnel : `--index-url` **remplace** PyPI. Avec `uv`,
qui est déjà l'outil du sidecar, la forme correcte est une source explicite :

```toml
[tool.uv.sources]
torch = { index = "pytorch-cpu" }
[[tool.uv.index]]
name = "pytorch-cpu"
url = "https://download.pytorch.org/whl/cpu"
explicit = true
```

### 7.3 `sentence-transformers` 5.6.1

Publiée le **2026-07-23**, Apache-2.0, `requires_python >=3.10`, classifiers jusqu'à 3.13.
**Maintenance active.** Dépendances obligatoires : `transformers<6.0.0,>=4.41.0`,
`huggingface-hub>=0.23.0`, **`torch>=1.11.0`**, `numpy`, `scikit-learn>=0.22.0`, `scipy`,
`typing_extensions`, `tqdm`.

Deux précisions utiles : **`Pillow` n'est plus obligatoire** (passé dans l'extra `[image]`), et
`scikit-learn` **l'est** — si cette piste est retenue, le classifieur vient gratuitement.

⚠️ **Une incompatibilité à trois branches, à connaître avant de bâtir dessus.**
`sentence-transformers` 5.6.1 accepte `transformers<6.0.0` donc la **v5** ; mais `optimum-onnx` 0.1.0
exige `transformers<4.58.0` et `spacy-transformers` 1.4.0 exige `transformers<4.53.3`. **Ces trois
contraintes sont mutuellement inconciliables** dans un même environnement.

### 7.4 La chaîne ONNX — un outil de conversion, pas une dépendance de production

`optimum` 2.2.0 (Apache-2.0) **tire `torch` obligatoirement**, et son extra `[onnxruntime]` ne
contient plus le code ONNX : il redirige vers `optimum-onnx`, paquet **extrait récemment**
(première publication 2025-10-09). `optimum-onnx` 0.1.0 date du **2025-12-23** — sept mois sans
publication — et épingle `optimum~=2.1.0`, ce qui **exclut `optimum` 2.2.0**. L'écosystème est
désaligné, et la version 0.1.0 signale une API non stabilisée.

**La conséquence pratique est nette** : convertir un modèle en ONNX est une opération à mener **hors
production**, dans un environnement jetable. Le sidecar n'a jamais besoin d'`optimum` — seulement
d'`onnxruntime` (18,3 Mio) et du fichier `.onnx`.

### 7.5 `fastembed` 0.8.0 (Qdrant)

Apache-2.0, publiée le **2026-03-23**, `requires_python >=3.10`, classifiers jusqu'à 3.13,
maintenance active. Huit dépendances directes (`huggingface-hub`, `loguru`, `mmh3`, `numpy`,
`onnxruntime`, `pillow`, `py-rust-stemmers`, `requests`, `tokenizers`, `tqdm`) — **aucune trace de
`torch` ni de `transformers`**. README verbatim : « *We don't require a GPU and don't download GBs of
PyTorch dependencies, and instead use the ONNX Runtime.* »

Son catalogue dense **multilingue** se réduit à trois modèles :
`paraphrase-multilingual-MiniLM-L6-v2` (0,22 Go), `paraphrase-multilingual-mpnet-base-v2` (1,00 Go),
`multilingual-e5-large` (2,24 Go). Tout le reste est anglophone. Deux des trois datent de 2021, le
troisième pèse 2,24 Go. Ni `bge-m3`, ni `snowflake-arctic-embed2`, ni `Solon`, ni `granite`.

### 7.6 `scikit-learn` 1.9.0 — la brique commune à toutes les pistes

BSD-3-Clause, publiée le **2026-06-02**, `requires_python >=3.11`, classifiers jusqu'à 3.14,
maintenance très active. Roue `cp312` Linux : **9 132 097 o ≈ 8,7 Mio**. Dépendances : `numpy>=1.24.1`,
`scipy>=1.10.0`, `joblib>=1.4.0`, `narwhals>=2.0.1`, `threadpoolctl>=3.5.0`. Total avec numpy et scipy :
**~60–70 Mio** (non mesuré précisément).

Quel que soit l'encodeur retenu, c'est ici que vit le classifieur. Options pour 7 étiquettes en
multi-étiquettes sur 120 exemples :

- **`OneVsRestClassifier(LogisticRegression)`** — la référence sur plongements denses ; 7 classifieurs
  binaires indépendants ; `lbfgs` est déterministe et n'a même pas de tirage aléatoire.
- **`MultiOutputClassifier`** — équivalent fonctionnel pour un `y` de forme `(n, 7)`.
  `OneVsRestClassifier` expose en plus `decision_function`.
- **`LinearSVC`** — souvent meilleur en très petit régime, mais **pas de `predict_proba`** : seuillage
  sur `decision_function` uniquement.
- **`ComplementNB`** — conçu pour les corpus déséquilibrés, mais **exige des features non négatives** :
  compatible TF-IDF, **incompatible avec des plongements denses**.
- **`TfidfVectorizer`** — entièrement déterministe, aucun `random_state`. C'est la branche « sac de
  mots » qui n'a besoin d'aucun modèle de plongement, donc d'aucun poids à stocker. Elle mérite
  d'être au recensement ne serait-ce que comme **ligne de base** face au lexique.

⚠️ **`MultilabelStratifiedKFold` n'est pas dans scikit-learn** — confirmé, `sklearn.model_selection`
n'expose que `StratifiedKFold`, mono-étiquette. Or la carte impose une validation croisée stratifiée
multi-étiquettes. L'implémentation de référence est **`iterative-stratification` 0.1.9**
(BSD-3-Clause, dernière publication **2024-10-12**, dépôt à 896 étoiles **non archivé**), qui fournit
`MultilabelStratifiedKFold`, `RepeatedMultilabelStratifiedKFold` et `MultilabelStratifiedShuffleSplit`,
d'après Sechidis, Tsoumakas & Vlahavas (2011), *On the Stratification of Multi-Label Data*, ECML PKDD.
Le paquet est **dormant mais pas abandonné** ; il ne déclare **aucun classifier de version Python**.
Étant du Python pur sans extension compilée, ce n'est pas rédhibitoire, mais le fonctionnement sous
Python 3.12 avec scikit-learn 1.9 **n'a pas été vérifié**. L'algorithme fait ~150 lignes : le
vendoriser est une option qui évite une dépendance dormante.

**`scikit-multilearn` 0.2.0 est à écarter** : dernière publication **2018-12-10**, plus de sept ans.

### 7.7 `setfit` 1.1.3 — le candidat conceptuellement juste

Apache-2.0, publiée le **2025-08-05** (~12 mois), `requires_python` **non déclaré**, classifiers
jusqu'à **3.12 seulement**. Dépendances : `datasets`, **`sentence-transformers[train]>=3`**,
`transformers`, `evaluate`, `huggingface_hub`, `scikit-learn`, `packaging`.

**Il faut lui rendre justice** : conceptuellement, SetFit est *exactement* l'outil pour 120 exemples
— affinage contrastif d'un encodeur de phrases puis tête logistique, sans prompt, sans génération,
avec un support natif du multi-étiquettes (`multi_target_strategy="one-vs-rest" | "multi-output" |
"classifier-chain"`). C'est le seul candidat de ce recensement conçu pour le régime *few-shot*.

Le coût, en revanche, est massif : il tire `sentence-transformers[train]`, donc `torch` + `accelerate`
+ `datasets` + `evaluate` — des dépendances **d'entraînement** dont un service d'inférence n'a aucun
besoin. Maintenance ralentie, pas de classifier 3.13.

**La voie qui réconcilie les deux** — et elle vaut aussi pour `optimum-onnx` : entraîner hors ligne
dans un environnement jetable, puis n'exporter que (a) l'encodeur affiné en ONNX et (b) la tête
logistique en `joblib` ou en simple matrice de poids. **L'outillage d'entraînement n'entre jamais dans
l'image de production.**

### 7.8 Les autres — verdicts

| Bibliothèque | Version, date | Licence | Python 3.12 | Verdict |
| --- | --- | --- | --- | --- |
| `spaCy` 3.8.14 | 2026-03-29 | MIT | oui (3.9 → 3.13) | **~19 dépendances obligatoires** pour un service qui en a 4. Outil de pipeline linguistique (tokenisation, POS, NER) ; n'apporte rien que scikit-learn ne fasse plus légèrement pour de la classification. Point positif : `pydantic>=2.0` est déjà là. Note : la NER serait un *autre* besoin, pas celui-ci. |
| `spacy-transformers` 1.4.0 | — | MIT | 3.10 → 3.14 | exige `transformers<4.53.3` **et** `torch`. À écarter. |
| `flair` 0.15.1 | **2025-02-05** (~18 mois) | MIT | **aucun classifier déclaré** | **À écarter catégoriquement.** 25 dépendances obligatoires dont `boto3` (SDK AWS), `gdown` (Google Drive), `wikipedia-api`, `matplotlib`. Dans un service dont la propriété cardinale est que **rien ne sort**, embarquer un SDK cloud et deux clients d'API distantes est un anti-motif de conformité en soi. |
| `gensim` 4.4.0 | 2025-10-18 | **LGPL-2.1-only** | oui (roues cp39 → cp313) | Très sobre (3 dépendances), maintenue. Mais c'est Word2Vec/Doc2Vec/LDA : de l'entraînement de plongements sur corpus, pas des plongements de phrases pré-entraînés. Avec 120 exemples, hors sujet. |
| `fasttext` 0.9.3 | 2024-06-12 | MIT | **classifiers : 2.7 → 3.6** | **Amont archivé** — « *This repository was archived by the owner on Mar 19, 2024* ». Sous Linux/Python 3.12, compilation obligatoire, qui échoue (retrait de `distutils` du stdlib en 3.12). `fasttext-wheel` 0.9.2 est le contournement communautaire, mais ses métadonnées s'arrêtent aussi à 3.6 et **aucune roue cp313**. À écarter, indépendamment du problème de licence des vecteurs (§ 5). |
| `llama-cpp-python` 0.3.34 | 2026-07-12 | MIT | oui (3.8 → 3.14) | **Très active** (publication ~hebdomadaire). ⚠️ **PyPI ne publie que le sdist** — vérifié sur les 10 dernières versions : `pip install` nu **compile llama.cpp depuis les sources**. L'index de roues CPU (`https://abetlen.github.io/llama-cpp-python/whl/cpu`) est à jour, tag `py3-none` donc 3.12/3.13/3.14 sans roue spécifique, **22,1 Mio vérifiés**, aucune dépendance CUDA. Réserve : cet index vit sur les GitHub Pages personnelles d'un mainteneur — un point unique de défaillance hors PyPI. |

---

## 8. L'option « zéro nouvelle dépendance » — Ollama sert déjà des plongements

Elle mérite un paragraphe à elle seule, parce qu'elle est la seule qui ne coûte **rien** en
dépendances : le client `openai` est **déjà** dans `pyproject.toml`, et Ollama est **déjà** dans la
pile Aspire, avec son volume de modèles nommé (`microservice_rgpd_ollama_models`).

**Ce qui est documenté.** `POST /api/embed` accepte `model`, `input` (chaîne ou liste), `truncate`
(**défaut `true`**), `options`, `keep_alive`, `dimensions`. Ollama **L2-normalise systématiquement**
les vecteurs, ce n'est pas configurable. Côté compatibilité OpenAI, `/v1/embeddings` supporte `model`,
`input`, `encoding_format` et `dimensions` — pas `user`.

**Une propriété favorable au déterminisme** : côté serveur, chaque texte est encodé par un appel
séparé. **Il n'y a pas de traitement par lots multi-textes**, donc le vecteur d'un texte ne dépend pas
de ses voisins. C'est précisément le piège dont souffrent les autres backends (§ 9).

**Le chargement concurrent est acquis** : disponible en option depuis v0.1.33 (2024-04-28), **activé
par défaut depuis v0.2.0** (2024-07-02), dont l'annonce cite explicitement le cas d'un modèle de
plongement et d'un modèle de complétion chargés simultanément. Variables : `OLLAMA_MAX_LOADED_MODELS`,
`OLLAMA_NUM_PARALLEL`, `OLLAMA_KEEP_ALIVE`, `OLLAMA_SCHED_SPREAD`, `OLLAMA_MAX_QUEUE`.

### Trois limites qu'il faut connaître avant de s'y engager

1. **`/v1/embeddings` ne transmet pas `options`.** Le middleware de compatibilité OpenAI ne fait
   passer que `{Model, Input, Dimensions}` : `options` et `keep_alive` sont perdus. **On ne peut donc
   pas forcer `num_gpu: 0` par requête via l'endpoint OpenAI** — celui-là même qu'utilise le client
   `openai` déjà présent. Le contournement documenté est un Modelfile dédié (`PARAMETER num_gpu 0`)
   figeant le placement CPU au niveau du modèle, ou un second `ollama serve` avec
   `CUDA_VISIBLE_DEVICES=-1` (valeur documentée dans `docs/gpu.mdx`).

2. **Certaines variables Ollama sont globales.** La FAQ officielle précise que la quantification du
   cache K/V est « *a global option — meaning all models will run with the specified quantization
   type* ». Ce qui est réglé pour `qwen3:8b` s'appliquerait au modèle de plongement, et
   réciproquement. **Le troisième moteur entrerait en couplage avec le moteur de verdict** — ce qui
   contredit frontalement l'argument des « deux endpoints plutôt qu'un » de l'ADR-0001, où
   l'indépendance des avis est voulue *structurelle*. C'est l'objection la plus sérieuse à cette
   piste, et elle est architecturale, pas technique.

3. **Aucune garantie de déterminisme documentée.** Le sujet n'est pas abordé par Ollama. Voir § 9.

### Les modèles de plongement multilingues servis par Ollama

| Tag | Taille | Dim | Ctx | Licence | Français |
| --- | --- | --- | --- | --- | --- |
| `bge-m3:567m` | 1,2 Go | 1024 | 8K | MIT | 100+ langues |
| `snowflake-arctic-embed2:568m` | 1,2 Go | 1024 | 8K | Apache-2.0 | 101 langues, **français explicitement évalué** |
| `granite-embedding:278m` | 563 Mo | 768 | 512 | Apache-2.0 | 12 langues, **`fr` explicitement listé** |
| `qwen3-embedding:0.6b` | 639 Mo | 1024 (MRL) | 32K | Apache-2.0 | 100+ langues |
| `embeddinggemma:300m` | 622 Mo | 768 (MRL) | 2K | **Gemma (non-OSI)** | 100+ langues |
| `paraphrase-multilingual:278m` | 563 Mo | 768 | **128 réel** | Apache-2.0 | 50 langues, modèle de 2021 |

**À écarter pour le français** malgré leur popularité : `nomic-embed-text` (v1.5),
`mxbai-embed-large`, `all-minilm`, `bge-large`, `granite-embedding:30m` — **anglais uniquement**.
Le tag `nomic-embed-text` sert la v1.5 anglophone ; le modèle multilingue est
`nomic-embed-text-v2-moe`, qui n'est pas servi sous ce tag.

### ⚠️ Le piège des accents — le fait le plus important de ce recensement

[**ollama#15609**](https://github.com/ollama/ollama/issues/15609), ouverte le **15 avril 2026**,
**toujours ouverte** (PR associée #15627) :

> « BERT-derived embedding models produce incorrect embeddings for non-ASCII text (strip_accents
> preprocessing dropped in gguf conversion) »

Le prétraitement `strip_accents` est perdu à la conversion GGUF. Les mots accentués tombent sur
`[UNK]`, et **des mots sans aucun rapport convergent** :

| Modèle | Même mot, accentué ↔ ASCII (attendu ≈ 1) | Mots **sans rapport**, tous deux accentués (attendu bas) |
| --- | --- | --- |
| `mxbai-embed-large` | 0,511 | **0,904** |
| `nomic-embed-text` | 0,888 | **0,992** |
| `all-minilm` | 0,240 | **0,875** |

« Hokkaidō » et « Éire » obtiennent une similarité de 0,9+ parce qu'ils se tokenisent tous deux en
`[CLS] [UNK] [SEP]`.

**Pour un moteur dont l'entrée est du français, c'est potentiellement fatal.** Les trois modèles
testés sont anglophones et déjà écartés ici pour cette raison — mais le mécanisme en cause est celui
des tokeniseurs **WordPiece** des modèles dérivés de BERT. Les candidats sérieux du § 8
(`bge-m3`, `snowflake-arctic-embed2`, `paraphrase-multilingual`) sont des **XLM-RoBERTa /
SentencePiece** (`model_type: "xlm-roberta"` vérifié dans leur `config.json`), donc *a priori* hors
du mécanisme. **Mais l'issue ne les teste pas, et cela n'a pas été vérifié.**

**C'est la première mesure à faire, avant tout benchmark.** Rejouer le script de l'issue sur le
modèle candidat avec des paires françaises — « côté » / « cote », « pêche » / « peche », « où » /
« ou » — prend quelques minutes et peut invalider un modèle entier. Aucun score MTEB ne rattrape un
tokeniseur qui écrase les diacritiques.

**Deux autres issues ouvertes à connaître** : [ollama#15582](https://github.com/ollama/ollama/issues/15582)
(`bge-m3` rend un HTTP 500 `unsupported value: NaN` sur certains contenus markdown) et
[llama.cpp#25293](https://github.com/ggml-org/llama.cpp/issues/25293) (en mode plongement, l'entrée
doit tenir dans un seul ubatch).

---

## 9. Le déterminisme en pratique, et non en théorie

Le ticket demandait explicitement de ne pas se payer de mots. Voici ce que les documentations
officielles disent réellement.

| Couche | Reproductible bit-à-bit ? | Prix à payer | Source |
| --- | --- | --- | --- |
| **`torch` CPU** | **Non garanti** | graines, `use_deterministic_algorithms(True)`, `set_num_threads(1)`, `OMP_NUM_THREADS=1`, un texte par lot | [randomness.html](https://docs.pytorch.org/docs/2.13/notes/randomness.html) |
| **`onnxruntime` CPU** | Non documenté ; une option existe | `use_deterministic_compute=True` (**défaut `false`**), `intra_op_num_threads=1`, `ORT_SEQUENTIAL` | [api_summary](https://onnxruntime.ai/docs/api/python/api_summary.html) |
| **llama.cpp / Ollama CPU** | « CPU is already deterministic » (PR **non fusionnée**) — **contredit** par OpenBLAS multi-thread | `num_gpu 0`, `num_thread 1`, `OMP_NUM_THREADS=1`, pas d'OpenBLAS, version figée | [PR#16016](https://github.com/ggml-org/llama.cpp/pull/16016), [#22956](https://github.com/ggml-org/llama.cpp/issues/22956) |
| **`scikit-learn`** | **Oui, explicitement** | `random_state=<int>`, `n_jobs=1` | [common_pitfalls](https://scikit-learn.org/stable/common_pitfalls.html) |

**PyTorch est le plus franc, et le plus décourageant.** Verbatim :

> « **Completely reproducible results are not guaranteed across PyTorch releases, individual commits,
> or different platforms.** »
> « Furthermore, results may not be reproducible between CPU and GPU executions, even when using
> identical seeds. »

`torch.use_deterministic_algorithms(True)` « *configures PyTorch to use deterministic algorithms […]
and to throw an error if an operation is known to be nondeterministic* ». `sentence-transformers`
n'énonce **aucune** garantie propre : elle hérite intégralement de celle de `torch`.

**scikit-learn est le seul à donner une garantie explicite** :

> « If an integer is passed, calling `fit` or `split` multiple times always yields the same results. »
> « For reproducible results across executions, remove any use of `random_state=None`. »

Nuance méthodologique utile pour la carte : la même page recommande, **pour évaluer** un modèle en
validation croisée, de laisser l'estimateur utiliser un générateur différent à chaque pli — sous peine
de mesurer la chance d'une graine particulière. Mais **pour le modèle de production livré**,
`random_state=<int>` fixe est la bonne réponse. Les deux régimes coexistent sans se contredire.

### Les deux risques réels — et ce ne sont pas les bits de poids faible

**Le remplissage de lot (*padding*).** Deux textes encodés ensemble sont complétés à la longueur du
plus long ; encodés séparément, ils ne le sont pas. **Les vecteurs diffèrent.** Ce n'est pas un bug,
c'est de l'arithmétique flottante. Pour un déterminisme strict, il faut encoder **un texte à la fois**
— ou figer la taille de lot et l'ordre des entrées. Sur ce point précis, Ollama est structurellement
avantagé (§ 8) et `llama-cpp-python` structurellement désavantagé : son `embed()` accumule les
séquences jusqu'à `n_batch` jetons, donc le vecteur d'un texte **peut dépendre de ses voisins dans la
liste**. Autre différence à ne pas manquer entre ces deux backends : `llama-cpp-python` ne normalise
**pas** en L2 par défaut, là où Ollama le fait toujours — **les vecteurs des deux ne sont pas
comparables tels quels**.

**La montée de version silencieuse.** Un changement de version de bibliothèque, ou de révision du
modèle, invalide tout ce qui a été calibré, **sans erreur**. Un vecteur produit par un encodeur ne
signifie rien pour un classifieur entraîné sur un autre. La parade est de figer les versions **et** la
révision du modèle, et de **stocker avec le classifieur entraîné une empreinte de l'encodeur qui l'a
produit**.

**Variables à figer, quelle que soit la piste :**

```
OMP_NUM_THREADS=1
MKL_NUM_THREADS=1
OPENBLAS_NUM_THREADS=1
PYTHONHASHSEED=0
```

`CUBLAS_WORKSPACE_CONFIG` est **sans objet** — il n'y a pas de GPU. L'ajouter serait du bruit.

⚠️ **Une déduction, pas une garantie** : l'inférence CPU d'un encodeur en `eval()` sans dropout est un
calcul déterministe *pour un même nombre de threads BLAS et une même version de bibliothèque*.
Changer le nombre de threads change l'ordre des réductions flottantes, donc les derniers bits.
PyTorch ne documente pas explicitement cette invariance. **C'est à mesurer, pas à supposer** :
100 appels sur un même texte français long, sur la machine cible.

---

## 10. Le stockage des poids

Le dépôt versionne du texte, pas des binaires. Quatre voies existent ; trois seulement tiennent.

### 10.1 Téléchargement via `huggingface_hub`

`huggingface-hub` 1.25.1 (2026-07-27, Apache-2.0, `requires_python >=3.10`, classifiers jusqu'à 3.14).

⚠️ **La bibliothèque est passée en v1.x** : **`huggingface-cli` n'existe plus**, la CLI est **`hf`**.
Tout script appelant `huggingface-cli download` casse. Nouveauté v1.x utile en production isolée :
`IncompleteSnapshotError` est levée si le Hub est injoignable et que des fichiers manquent, au lieu de
rendre silencieusement un dossier partiel.

**Variables d'environnement, noms exacts** (source :
[référence officielle](https://huggingface.co/docs/huggingface_hub/package_reference/environment_variables)) —
avec cet avertissement en tête de page : « *All environment variables are read at import time of
`huggingface_hub`. Any modification made afterwards will not be taken into account.* »

| Variable | Rôle | Défaut |
| --- | --- | --- |
| `HF_HOME` | racine des données locales | `~/.cache/huggingface` |
| `HF_HUB_CACHE` | cache des dépôts | `$HF_HOME/hub` |
| `HF_HUB_OFFLINE` | « *If set, no HTTP calls will be made to the Hugging Face Hub […] If no cache file is detected, an error is raised.* » | non défini |
| `HF_HUB_DISABLE_TELEMETRY` | désactive la télémétrie (`DO_NOT_TRACK=1` est équivalent) | non défini |
| `HF_HUB_DISABLE_UPDATE_CHECK` | la CLI `hf` interroge PyPI au démarrage — « *Useful in offline CI environments* » | non défini |
| `HF_HUB_ETAG_TIMEOUT` | au dépassement : **retombe sur le cache local** | 10 s |
| `HF_HUB_DOWNLOAD_TIMEOUT` | au dépassement : **lève `TimeoutError`** | 10 s |

**Dépréciées** (fonctionnelles mais sans priorité sur leur remplaçante) : `HUGGINGFACE_HUB_CACHE` →
`HF_HUB_CACHE`, `HUGGINGFACE_ASSETS_CACHE` → `HF_ASSETS_CACHE`, `HUGGING_FACE_HUB_TOKEN` → `HF_TOKEN`.

**`HF_HUB_OFFLINE=1` contre `local_files_only=True`** — la distinction est décisive pour un service
qui ne doit rien laisser sortir :

| | `local_files_only=True` | `HF_HUB_OFFLINE=1` |
| --- | --- | --- |
| Portée | **par appel** | **processus entier**, lu à l'import |
| Effet sur `HfApi` | aucun, le réseau reste appelé | lève `OfflineModeIsEnabled` |
| Couvre le code tiers | non | **oui** |

`HF_HUB_OFFLINE=1` est la **seule garantie globale** qu'aucun appel sortant ne partira, y compris
depuis du code tiers. `local_files_only=True` est un complément, pas un substitut.

**Épinglage.** `revision` accepte « *a branch name, a tag, or a commit hash* ». **Épingler sur un SHA
de commit à 40 caractères** est la seule forme immuable : une branche bouge, un tag Git est mutable.
Pour une trace d'audit RGPD, pouvoir justifier *quelle* version du modèle a produit une décision n'est
pas un luxe. Piège : `allow_patterns=["*.safetensors"]` **casse le chargement** d'un modèle
`sentence-transformers`, qui a besoin de `modules.json`, `config_sentence_transformers.json`,
`1_Pooling/config.json`, `sentence_bert_config.json`, `tokenizer.json`. Préférer la liste noire.

⚠️ **`hf-transfer` est déprécié** — la doc est formelle : « *This is a deprecated environment
variable. Now that the Hugging Face Hub is fully powered by the Xet storage backend […] This means
`hf_transfer` can't be used anymore.* » Sa remplaçante `hf-xet` est installée automatiquement avec
`huggingface_hub` depuis la 0.32.0. C'est un binaire Rust : les roues existent pour Debian/glibc,
**elles peuvent manquer sur Alpine (musl)**.

### 10.2 Poids dans l'image du container

Documentation Docker, verbatim : « *Once a layer changes, then all downstream layers need to be
rebuilt as well.* » Deux pièges qui doublent la taille :

1. **Un fichier supprimé dans un layer ultérieur ne réduit pas la taille.** Téléchargement et
   nettoyage doivent tenir dans le **même `RUN`**, ou passer par un build multi-étapes.
2. **`chown -R` duplique les fichiers.** Avertissement Hugging Face verbatim : « *Updating metadata
   for a file creates a new copy stored in the new layer. Therefore, a recursive `chown` can result
   in a very large image due to the duplication of all affected files.* » Sur 400 Mo de poids, ce seul
   détail double l'image. `COPY --chown=user`, **jamais** `COPY` puis `RUN chown -R`.

**La propriété que cette voie apporte** : le couple (code, poids) devient **une seule unité immuable
identifiée par un digest d'image**. Pouvoir affirmer « la version X de l'image contient exactement le
modèle au SHA Y » est une propriété de traçabilité qui parle directement au § 8 de la spec de
qualification.

### 10.3 Volume nommé — le précédent Ollama ne se transpose pas

Documentation Docker, verbatim : « *if you mount an empty volume into a directory in the container in
which files or directories exist, these files or directories are propagated (copied) into the volume
by default.* » Trois limites :

1. **Volumes seulement, pas les bind mounts** — un bind mount d'un dossier hôte vide **masque** le
   contenu de l'image.
2. **Uniquement si le volume est vide.** Dès le deuxième démarrage, plus aucune copie. **Corollaire :
   publier une image avec de nouveaux poids ne met pas à jour le volume existant.** On obtient
   silencieusement l'ancien modèle avec le nouveau code.
3. Le montage masque le contenu préexistant.

⚠️ **Le point 2 est un mode de défaillance silencieux**, et c'est précisément le genre de défaut que
l'ADR-0001 reproche au lexique seul : « ses erreurs sont silencieuses ». Pour un service où la version
du modèle doit être justifiable, c'est un coût à porter au dossier plutôt qu'un détail d'exploitation.

**Et le précédent Ollama ne valide pas cette voie.** Ollama **peuple son volume au runtime** via
`ollama pull` (`docker run -v ollama:/root/.ollama`, redirigeable par `OLLAMA_MODELS`) : **l'image
Ollama ne contient aucun poids**, le pré-remplissage automatique depuis l'image ne joue donc aucun
rôle chez eux. Le sidecar n'a pas d'équivalent : reprendre le patron exigerait de fournir soi-même le
mécanisme de peuplement **et de mise à jour**.

### 10.4 Git LFS — à écarter

| Plan GitHub | Bande passante | Stockage |
| --- | --- | --- |
| Free / Pro / Free for organizations | **10 GiB** | 10 GiB |
| Team / Enterprise Cloud | 250 GiB | 250 GiB |

Verbatim : « *When you download a Git LFS file, the bandwidth you use is included in the repository
owner's bandwidth usage.* » — cela inclut les `pull` des collaborateurs **et les workflows GitHub
Actions**. Quatre raisons :

1. **Contradiction frontale avec la règle du dépôt.** Git LFS *versionne bien des binaires* : il ne
   contourne pas la décision, il la viole avec une indirection.
2. **La bande passante est facturée à chaque clone, CI comprise.** 400 Mo × chaque build épuise les
   10 GiB mensuels en environ 25 clones.
3. **Immutabilité illusoire** : l'historique LFS reste réécrivable ; un SHA de révision HF ne l'est pas.
4. **Redondance pure** : le Hub Hugging Face **est déjà** un serveur LFS/Xet, gratuit en
   téléchargement, adressable par contenu, avec épinglage par SHA.

Hugging Face déconseille d'ailleurs le clone Git : « *An alternative would be to clone the repo, but
this requires git and git-lfs […] It is also not possible to filter which files to download when
cloning a repository using git.* »

---

## 11. Ce que ce recensement laisse ouvert pour le ticket #47

Le choix ne se fera pas sur ce document seul. Il manque **trois mesures** qui ne peuvent être ni lues
ni déduites, seulement faites — et elles sont peu coûteuses :

1. **Le test des accents.** Rejouer le script de [ollama#15609](https://github.com/ollama/ollama/issues/15609)
   sur chaque candidat sérieux, avec des paires françaises. Quelques minutes ; peut invalider un
   modèle entier avant tout autre travail. **C'est la mesure à faire en premier.**
2. **La latence CPU réelle sur la machine cible.** Aucune carte de modèle ne la publie. Le critère
   éliminatoire de la carte ne peut donc être appliqué qu'après mesure — encodage d'un texte français
   de longueur médiane (102 caractères) puis maximale (1195), sur un cœur, threads figés à 1.
3. **L'égalité bit-à-bit sur 100 appels** d'un même texte, pour la piste retenue. Le déterminisme est
   une propriété acquise du service ; aucune documentation ne la garantit pour les couches
   d'inférence, et une seule d'entre elles la garantit explicitement (`scikit-learn`).

Il manque aussi une décision qui n'appartient pas à ce ticket : **la place du modèle de plongement
dans la topologie**. L'option « zéro dépendance » via Ollama est la plus sobre en paquets Python, mais
elle place le troisième moteur **dans le même processus que le moteur de verdict**, avec des variables
de configuration globales partagées (§ 8). L'ADR-0001 a voulu l'indépendance des avis *structurelle*.
Ce que coûte, ou non, ce couplage est un arbitrage d'architecture — pas un fait à recenser.

Enfin, une branche que ce recensement a croisée sans la creuser : **`TfidfVectorizer` + un classifieur
linéaire scikit-learn**, sans aucun modèle de plongement. Zéro poids à stocker, ~9 Mio de dépendances,
déterminisme garanti par la documentation, et le même matériau que le lexique agrégé différemment —
ce qui correspond exactement à la décision de cadrage n° 3 de la carte. Elle mérite au minimum de
servir de **ligne de base** face à laquelle mesurer ce que les plongements apportent réellement.

---

## 12. Points non vérifiés en source primaire

À lire avant toute citation de ce document.

**Modèles**

1. Scores MTEB(fra) des modèles postérieurs à l'article (`Lajavaness/bilingual-*`,
   `gte-multilingual-base`, `granite-embedding-*`, `snowflake-arctic-embed2`,
   `static-similarity-mrl-multilingual-v1`, `jina-embeddings-v3`, `nomic-embed-text-v2-moe`) : le
   classement en ligne est un Space Gradio non interrogeable programmatiquement.
2. Longueur de contexte de `Lajavaness/sentence-camembert-*`, `sentence-flaubert-base` et
   `antoinelouis/biencoder-camembert-base-mmarcoFR` — notée `n. v.`.
3. Score de `manu/sentence_croissant_alpha_v0.4` : la valeur de la Table 9 est celle de la **v0.3**.
4. `EuroBERT` : licence Apache-2.0 déclarée en carte, **aucun fichier `LICENSE` dans le dépôt**.
5. Existence d'un fichier fastText `cc.fr.300` quantifié officiel distribué par Meta : non trouvé.
6. Licences et tailles exactes des modèles spaCy français : `spacy.io/models/fr` affichait « Unable to
   load model details from GitHub » lors de la consultation.
7. **Tous les chiffres de latence et d'empreinte mémoire du § 6.2 sont des ordres de grandeur
   déduits**, non publiés.

**Bibliothèques**

8. Taille exacte de la roue `torch` CPU-only 2.13.0 : l'index existe et le suffixe `+cpu` est
   confirmé, mais la page récupérée était tronquée. Le chiffre souvent cité de ~180–200 Mio **n'est
   pas sourcé ici**. Ce qui est certain, c'est l'absence des ~1,22 Gio de paquets NVIDIA mesurés.
9. Taille de `cuda-toolkit==13.0.3`, donc **total réel** de l'installation torch par défaut.
10. Tailles d'installation totales de `fastembed`, `numpy`, `scipy`.
11. `use_deterministic_compute` d'ONNX Runtime : **aucune page de documentation n'en définit la
    garantie précise**. Ne pas présenter le déterminisme d'ORT comme contractuel.
12. Invariance de PyTorch CPU au nombre de threads : **déduction, non documentée**.
13. Absence d'une page « reproducibility » dans la documentation `sentence-transformers`.
14. `fastembed.add_custom_model` : API non validée en documentation primaire.
15. Fonctionnement d'`iterative-stratification` 0.1.9 sous Python 3.12 avec scikit-learn 1.9.
16. `scikit-multilearn-ng` (fork communautaire) : non examiné.
17. `fasttext-wheel` : roues cp312 rapportées par les métadonnées PyPI, classifiers arrêtés à 3.6,
    **aucune cp313**.

**Stockage et exploitation**

18. `HF_ENDPOINT` : **absente** de la page de référence des variables d'environnement ; confirmée
    seulement indirectement via le paramètre `endpoint` de `hf_hub_download`.
19. `TRANSFORMERS_CACHE` : retrait en `transformers` v5 **déduit d'un diff de documentation**, sans
    note de dépréciation formelle. `TRANSFORMERS_OFFLINE` : **introuvable** en v4 comme en v5.
20. `SENTENCE_TRANSFORMERS_HOME` : précédence vis-à-vis de `HF_HOME` non vérifiée dans le code source.
21. Licence de `hf-transfer` : champ PyPI nul, aucune mention au README — **non déterminée**. Licence
    de `hf-xet` : champ nul, « Apache-2.0 » lu sur la page HTML seulement.
22. `hf-xet` : dépendance obligatoire ou extra de `huggingface_hub` 1.x — non tranché.
23. Aucun outil Hugging Face officiel pour une vérification d'intégrité **100 % hors ligne** contre un
    manifeste local (`hf cache verify` exige le réseau).
24. Aucune page Hugging Face officielle ne recommande le pré-téléchargement au build Docker : le
    patron du § 10.2 est une synthèse à partir des primitives documentées.
25. Syntaxe Compose exacte de l'option `volume-nocopy`.
26. Tarifs Git LFS au GiB : agrégat de recherche, non lus sur page primaire.
27. **Déterminisme bit-à-bit des plongements Ollama : non testé empiriquement.**
28. **Bug `strip_accents` ([ollama#15609](https://github.com/ollama/ollama/issues/15609)) sur
    `bge-m3` / `snowflake-arctic-embed2` / `paraphrase-multilingual` : NON TESTÉ.** L'issue ne couvre
    pas ces modèles. Leur immunité présumée repose sur leur `model_type: "xlm-roberta"` — c'est un
    raisonnement, pas une mesure.

---

## Sources

**Modèles et évaluation**

- API Hugging Face `/api/models/<id>?blobs=true` — licences, paramètres, tailles de fichiers (le dépôt lui-même)
- Ciancone, Kerboua, Schaeffer, Siblini — [*MTEB-French*, arXiv:2405.20468](https://arxiv.org/abs/2405.20468) · [HTML v2](https://arxiv.org/html/2405.20468v2) · [Lyon-NLP/mteb-french](https://github.com/Lyon-NLP/mteb-french)
- [MTEB Leaderboard](https://huggingface.co/spaces/mteb/leaderboard)
- [Hugging Face — Static Embeddings](https://huggingface.co/blog/static-embeddings)
- [fastText — Word vectors for 157 languages](https://fasttext.cc/docs/en/crawl-vectors.html)
- [spacy-models releases](https://github.com/explosion/spacy-models/releases)
- [Ollama library](https://ollama.com/library)
- Sechidis, Tsoumakas, Vlahavas — *On the Stratification of Multi-Label Data*, ECML PKDD 2011

**Bibliothèques** — JSON PyPI de `sentence-transformers` 5.6.1, `transformers` 5.14.1, `torch` 2.13.0,
`onnxruntime` 1.28.0, `optimum` 2.2.0, `optimum-onnx` 0.1.0, `fastembed` 0.8.0, `setfit` 1.1.3,
`scikit-learn` 1.9.0, `iterative-stratification` 0.1.9, `scikit-multilearn` 0.2.0, `spacy` 3.8.14,
`spacy-transformers` 1.4.0, `flair` 0.15.1, `gensim` 4.4.0, `fasttext` 0.9.3, `fasttext-wheel` 0.9.2,
`llama-cpp-python` 0.3.34, `huggingface-hub` 1.25.1, `hf-transfer` 0.1.9, `hf-xet` 1.5.2

**Déterminisme**

- [PyTorch — Reproducibility](https://docs.pytorch.org/docs/2.13/notes/randomness.html)
- [scikit-learn — Common pitfalls / Controlling randomness](https://scikit-learn.org/stable/common_pitfalls.html)
- [ONNX Runtime — Python API summary](https://onnxruntime.ai/docs/api/python/api_summary.html) · [Quantization](https://onnxruntime.ai/docs/performance/model-optimizations/quantization.html) · [issue #4611](https://github.com/microsoft/onnxruntime/issues/4611) · [#12086](https://github.com/microsoft/onnxruntime/issues/12086)
- [sentence-transformers — Speeding up Inference](https://sbert.net/docs/sentence_transformer/usage/efficiency.html)
- [llama.cpp — PR #16016](https://github.com/ggml-org/llama.cpp/pull/16016) · [issue #22956](https://github.com/ggml-org/llama.cpp/issues/22956) · [#25293](https://github.com/ggml-org/llama.cpp/issues/25293)

**Ollama**

- [API](https://github.com/ollama/ollama/blob/main/docs/api.md) · [Compatibilité OpenAI](https://github.com/ollama/ollama/blob/main/docs/api/openai-compatibility.mdx) · [FAQ](https://docs.ollama.com/faq) · [GPU](https://github.com/ollama/ollama/blob/main/docs/gpu.md)
- [issue #15609 — strip_accents](https://github.com/ollama/ollama/issues/15609) · [#15582](https://github.com/ollama/ollama/issues/15582)
- [Docker Hub — ollama/ollama](https://hub.docker.com/r/ollama/ollama)

**Stockage**

- [huggingface_hub — Environment variables](https://huggingface.co/docs/huggingface_hub/package_reference/environment_variables) · [Manage cache](https://huggingface.co/docs/huggingface_hub/guides/manage-cache) · [File download](https://huggingface.co/docs/huggingface_hub/package_reference/file_download) · [CLI](https://huggingface.co/docs/huggingface_hub/guides/cli) · [Migration v1.0](https://huggingface.co/docs/huggingface_hub/en/concepts/migration)
- [Docker — Volumes](https://docs.docker.com/engine/storage/volumes/) · [Build cache](https://docs.docker.com/build/cache/) · [Best practices](https://docs.docker.com/build/building/best-practices/)
- [Hugging Face — Docker Spaces](https://huggingface.co/docs/hub/spaces-sdks-docker)
- [GitHub Docs — Storage and bandwidth usage (Git LFS)](https://docs.github.com/en/repositories/working-with-files/managing-large-files/about-storage-and-bandwidth-usage)
