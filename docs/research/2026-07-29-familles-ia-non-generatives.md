# État de l'art : familles d'approches non génératives pour une taxonomie fermée multi-label en français

**Ticket** : [#43](https://github.com/AmauryTISSOT/microservice_rgpd/issues/43) — enfant de la carte [#42](https://github.com/AmauryTISSOT/microservice_rgpd/issues/42)
**Date d'instruction** : 29 juillet 2026
**Branche** : `research/familles-ia-non-generatives`

> **Ce document ne choisit pas.** Il compare six familles selon les critères que la carte #42 a rendus décisifs, et applique les contraintes éliminatoires de l'[ADR-0001](../adr/0001-architecture-polyglotte-et-moteur-auto-heberge.md). Le choix est le ticket de *grilling* qui suit. Là où deux familles se départagent mal, ce document le dit plutôt que de trancher à la place de la session de décision.

---

## 1. Cadrage : ce que la tâche est réellement

### 1.1 Le problème, mesuré et non supposé

Les chiffres ci-dessous sont recalculés sur [`corpus/demandes-rgpd.fr.jsonl`](../../corpus/demandes-rgpd.fr.jsonl), et non repris du [`corpus/README.md`](../../corpus/README.md) — plusieurs d'entre eux ne s'y trouvent pas et sont pourtant structurants.

| Grandeur | Valeur mesurée |
|---|---|
| Exemples | **120** |
| Cardinalité d'étiquetage | **101** exemples à 1 droit, **18** à 2 droits, **1** à 3 droits — moyenne **1,17** |
| Combinaisons multi-droits distinctes observées | **12**, pour 19 exemples — la plus fréquente (`effacement` + `opposition`) n'apparaît que **4 fois** |
| Classes les plus rares | `portabilite` 13, `rectification` 14, `limitation` 14 |
| Longueur des textes | 1 à 193 mots, **médiane 18 mots** ; 1 à 1195 caractères, médiane 102 |
| Tokens au total (minusculisés, dé-accentués, alphanumériques) | **2 582** |
| Vocabulaire distinct | **755 types** |
| Types apparaissant dans un **seul** document | **491**, soit **65 %** du vocabulaire |
| Types apparaissant dans **au moins 5** documents | **77** |
| Registres | `courant` 74, `juridique` 25, `familier` 13, `maladroit` 8 |

Quatre conséquences traversent tout ce qui suit.

1. **La très faible cardinalité (1,17) fait du problème un mono-label à exceptions**, pas un vrai multi-label. Une famille qui ne saurait *jamais* rendre deux droits perdrait 19 exemples sur 120 ; une famille qui en rend trop en perdrait davantage.
2. **Les 12 combinaisons distinctes sur 19 exemples condamnent la transformation en « powerset d'étiquettes »** — traiter chaque combinaison observée comme une classe à part donnerait des classes à un seul exemple. Cette famille de transformation, classique en multi-label, est éliminée par les données elles-mêmes et n'est pas discutée plus bas.
3. **Le vocabulaire est un désert.** 755 types pour 2 582 tokens, dont 65 % de hapax : un sac de mots produirait une matrice de 120 lignes sur ~755 colonnes dont **77 seulement** sont vues assez souvent pour porter une régularité. C'est le fait le plus dur du dossier, et il pèse spécifiquement sur une famille (§ 3).
4. **La médiane à 18 mots** met la quasi-totalité du corpus sous la fenêtre de 128 sous-mots des encodeurs de phrases les plus légers — la longueur maximale n'est un problème que pour la queue de distribution (193 mots).

### 1.2 L'exclusivité de `hors-perimetre` n'est pas un détail d'étiquetage

`OutOfScope` « ne se combine jamais avec un droit » ([`CONTEXT.md`](../../CONTEXT.md)), et la spec précise que « l'exclusivité d'`OutOfScope` n'est pas exprimable » dans le schéma JSON et « doit être vérifiée par du code » ([`docs/spec/qualification.md`](../spec/qualification.md) § 5.4). Le sidecar traite déjà un avis qui violerait cet invariant comme **une panne du moteur, pas un avis faible**.

Cela impose une exigence dure à toute famille : **il ne suffit pas qu'elle sache produire un ensemble d'étiquettes plausible, il faut qu'elle ne puisse pas produire un ensemble interdit.** Aucune des six familles n'exprime nativement cette contrainte — c'est un résultat de la recherche, développé au § 9.1.

À titre de repère, le moteur témoin actuel la résout par une règle structurelle et non par un seuil : il évalue les six droits, et `hors-perimetre` n'est rendu que si aucun n'a été reconnu (`src/sidecar/qualification_sidecar/lexicon.py`). C'est la forme la moins coûteuse de la contrainte, et elle est reproductible dans toutes les familles ci-dessous.

### 1.3 Le budget de latence est confortable, mais partagé

La spec fixe des échéances explicites ([§ 5.8](../spec/qualification.md)) : **120 s côté sidecar vers Ollama, 150 s côté .NET**, et **5 s pour le lexique**, dont le coût réel est « ~0,5 ms ». `Core` appelle les moteurs **en parallèle**, donc l'échéance globale reste celle du seul appel LLM.

Un troisième moteur non génératif s'insère donc dans un budget très large — jusqu'à quelques secondes sans dégrader la latence perçue, puisqu'il court sous l'ombre du LLM. **La contrainte de latence est faible ; la contrainte de ressources ne l'est pas** (§ 1.4), et une contrainte d'architecture inattendue s'y ajoute (§ 8.3).

### 1.4 Les trois contraintes éliminatoires, appliquées sans indulgence

**Aucun tiers.** Toute famille dont la mise en œuvre passerait par un service d'inférence hébergé est disqualifiée, quel qu'en soit le mérite. Conséquence directe et non négociable : `text-embedding-3-large` d'OpenAI, qui domine [MTEB-French](https://arxiv.org/abs/2405.20468) avec un score moyen de **0,71** et occupe la **première place en classification (0,74)**, **est hors sujet**. Tous les chiffres retenus dans ce document sont ceux de modèles à poids ouverts téléchargeables. Cette élimination coûte moins qu'il n'y paraît : le meilleur modèle open-weights français mesuré par ce même benchmark, `Solon-embeddings-large-0.1`, atteint **0,67** de moyenne, et `sentence-camembert-large` **0,65**.

**Le GPU est déjà pris.** 8 Go de VRAM servent `qwen3:8b` et « sérialisent de fait les appels » (ADR-0001). Toute famille est donc évaluée **en supposant le CPU seul**, à l'entraînement comme à l'inférence. Cette contrainte n'élimine aucune famille frontalement, mais elle en dégrade deux sévèrement (§ 5 et § 7) et hiérarchise le reste (§ 8).

**Français.** Pas d'anglais avec traduction préalable. Cette contrainte pèse en deux temps : le modèle sous-jacent doit avoir vu du français en pré-entraînement, et — point moins évident — les artefacts textuels qu'une famille demande d'écrire (libellés d'étiquettes, gabarits d'hypothèse pour le NLI, listes de mots vides) doivent être écrits en français, sans équivalent officiel disponible dans plusieurs cas (§ 3.6, § 7.2).

---

## 2. Résumé comparatif

| | 1. TF-IDF + linéaire | 2. Plongements + classifieur | 3. Encodeur affiné | 4. Contrastif à peu d'exemples (SetFit) | 5. NLI détourné (zero-shot) | 6. Voisins / prototypes |
|---|---|---|---|---|---|---|
| **Entraînement requis** | oui, trivial | oui, trivial (tête seule) | oui, lourd | oui, moyen | **aucun** | oui, quasi nul |
| **Multi-label** | par méta-estimateur (`OneVsRestClassifier`) | idem | natif (`BCEWithLogitsLoss`) | natif (`multi_target_strategy`) | `multi_label=True` | natif (`KNeighborsClassifier`, ML-kNN) |
| **Exclusivité de `hors-perimetre`** | non native | non native | non native | non native | non native, et le mode exclusif du pipeline est global donc inutilisable seul | non native |
| **Tient à 120 exemples ?** | douteux — 755 features pour 120 lignes | oui | **non** — sous le seuil documenté | **oui**, régime nominal du papier | oui (0 exemple requis) | oui |
| **Déterminisme** | **total** (`lbfgs`) | total à artefact figé | **variance inter-graines documentée** | graine à figer, variance mesurée jusqu'à ±11,8 pts | **total** (pas d'entraînement) | **total** (une faille documentée, contrôlable) |
| **Sait douter ?** | oui — régression logistique calibrée par construction | oui — même tête | non nativement, sur-confiance | oui — tête logistique | scores non calibrés, seuils à apprendre | k-NN : grille à k+1 valeurs ; ML-kNN et centroïdes : score continu |
| **CPU** | trivial | 1 passe encodeur | le plus coûteux | 1 passe encodeur à l'inférence | **×7 passes** | 1 passe encodeur |
| **Français** | agnostique, prétraitement à écrire | Solon 0,67 / s-camembert-large 0,65 (MTEB-Fr) | natif (CamemBERT, MIT) | `paraphrase-multilingual-mpnet-base-v2`, mesuré en fr | mDeBERTa 0,823 XNLI-fr (MIT) | hérite de l'encodeur |

---

## 3. Sac de mots pondéré + modèle linéaire

### 3.1 Principe et outillage

`TfidfVectorizer` produit une matrice creuse document × terme ; un modèle linéaire (`LogisticRegression`, `LinearSVC`) apprend un hyperplan par étiquette. C'est la ligne de base historique de la classification de texte, et la seule famille du dossier sans aucun modèle pré-entraîné.

### 3.2 Multi-label et exclusivité

La documentation scikit-learn ([§ 1.12](https://scikit-learn.org/stable/modules/multiclass.html)) est explicite : ni `LogisticRegression` ni `LinearSVC` ne figurent dans la liste des estimateurs nativement multi-label. Un méta-estimateur est **obligatoire**, et trois sont disponibles :

- **`OneVsRestClassifier`** — un classifieur par classe, acceptant une matrice indicatrice ; la doc le présente comme « the most commonly used strategy » et « fair default choice ». Les étiquettes sont indépendantes : rien n'empêche `hors-perimetre` et `effacement` de sortir ensemble.
- **`ClassifierChain`** — N binaires ordonnés, chacun entraîné sur X **plus les vraies étiquettes des rangs inférieurs**, « capable of exploiting correlations among targets » ([doc](https://scikit-learn.org/stable/modules/multiclass.html)). C'est le seul mécanisme du dossier qui *apprend* la dépendance entre étiquettes, donc le seul candidat à apprendre l'exclusivité au lieu de se la faire imposer. Mais la doc précise que « many randomly ordered chains are typically fit and their predictions averaged » — moyenner des chaînes réintroduit un tirage, donc une graine, et surtout **une moyenne de sorties binaires n'a plus de raison de respecter l'exclusivité**. Placer `hors-perimetre` en tête d'une chaîne d'ordre fixé conserve le déterminisme mais renonce à la moyenne.
- **`MultiOutputClassifier`** — un classifieur par cible, sans exploitation des corrélations.

Avec 19 exemples multi-droits répartis en 12 combinaisons, **il est peu vraisemblable qu'une chaîne apprenne quoi que ce soit d'une corrélation**. L'exclusivité restera une règle écrite à la main.

### 3.3 Les seuils par classe

La documentation scikit-learn ([§ 3.3](https://scikit-learn.org/stable/modules/classification_threshold.html)) sépare le problème statistique (estimer une probabilité) du problème de décision (agir), et pose que le seuil par défaut — 0,5 pour `predict_proba` — est sous-optimal dans la plupart des cas. Deux outils : `FixedThresholdClassifier` pour un seuil imposé, `TunedThresholdClassifierCV` pour un seuil optimisé par validation croisée. Avertissement explicite de la doc : « You should never use the same data for training the classifier and tuning the decision threshold due to the risk of overfitting » — ce qui est exactement l'imbrication que la carte #42 a déjà exigée (décision 2).

Limite à connaître : cette section de la doc **ne traite que du cas binaire** et ne mentionne pas le multi-label. L'outillage s'applique donc classe par classe, à l'intérieur du `OneVsRestClassifier`, jamais globalement.

### 3.4 Le régime de 120 exemples — le point faible de cette famille

C'est ici que les mesures du § 1.1 mordent. **755 features pour 120 exemples, dont 491 hapax et seulement 77 termes vus dans au moins 5 documents.** Un poids appris sur un terme vu une seule fois est du bruit pur ; la régularisation le ramènera vers zéro, et il ne restera au modèle qu'un vocabulaire effectif de l'ordre de la centaine de termes.

Deux atténuations existent, et méritent d'être testées plutôt que supposées :

- **`class_weight='balanced'`** ([doc `LogisticRegression`](https://scikit-learn.org/stable/modules/generated/sklearn.linear_model.LogisticRegression.html)) pondère par `n_samples / (n_classes * bincount(y))` — pertinent pour `portabilite` à 13 exemples.
- **N-grammes de caractères** plutôt que de mots. La doc scikit-learn recommande explicitement `analyzer='char_wb'` : « One might alternatively consider a collection of character n-grams, a representation resilient against misspellings and derivations », et « The word boundaries-aware variant `char_wb` is especially interesting for languages that use white-spaces for word separation […] it can increase both the predictive accuracy and convergence speed of classifiers » ([doc](https://scikit-learn.org/stable/modules/feature_extraction.html)). C'est directement adressé aux **8 exemples de registre `maladroit`** du corpus, et cela densifie mécaniquement la matrice — mais au prix d'une explosion du nombre de colonnes, qui ne résout pas le déséquilibre features/exemples, elle le déplace.

Aucune source primaire ne publie de seuil de volume minimal pour cette famille. Le constat reste donc empirique et négatif : **la famille est structurellement handicapée par la taille du vocabulaire relativement au nombre d'exemples**, et la validation croisée de la carte #42 est le seul moyen honnête de savoir si elle s'effondre ou non.

### 3.5 Déterminisme — le meilleur du dossier

La doc de `LogisticRegression` est nette : `random_state` n'est « used when solver == 'sag', 'saga' or 'liblinear' to shuffle the data ». **Avec le solveur `lbfgs` (défaut), il n'existe aucune source d'aléa** et `random_state` est ignoré. Pour `LinearSVC`, la doc écrit : « When `dual=False` the underlying implementation of LinearSVC is not random and `random_state` has no effect on the results » — mais recommande `dual=True` quand `n_features > n_samples`, ce qui est précisément notre cas (755 > 120), et réintroduit alors une graine.

**`LogisticRegression` + `lbfgs` est donc la seule configuration du dossier dont le déterminisme ne repose sur aucune graine ni aucun artefact gelé** : il tient à l'algorithme.

### 3.6 Ce qu'elle sait dire de son doute

La doc de calibration scikit-learn note que la régression logistique est bien calibrée par construction, sa fonction de lien étant canonique, là où « maximum-margin methods produce extreme sigmoid curves » ([doc](https://scikit-learn.org/stable/modules/calibration.html)). Un `predict_proba` de régression logistique est donc directement lisible comme un degré de confiance — sans recalibrage, et sans amputer les 120 exemples d'un jeu de calibration.

C'est un avantage discriminant sur `LinearSVC`, qui n'expose pas `predict_proba` et dont la doc renvoie explicitement à `CalibratedClassifierCV`.

### 3.7 CPU et français

CPU trivial : l'entraînement d'un TF-IDF + régression logistique sur 120 documents est de l'ordre de la seconde, l'inférence sub-milliseconde. Aucune source primaire ne chiffre ce cas précis, mais l'ordre de grandeur est celui du lexique actuel (~0,5 ms) et la famille est la seule du dossier **sans aucun modèle à télécharger, à versionner ni à charger en mémoire**.

Français : la famille est agnostique, ce qui est un faux avantage. Toute la sensibilité linguistique migre dans le prétraitement, et **scikit-learn ne fournit aucune liste de mots vides française** — la doc précise que seule la valeur `'english'` est acceptée par `stop_words`, et l'assortit d'un avertissement : « There are several known issues in our provided 'english' stop word list. It does not aim to be a general, "one-size-fits-all" solution ». Une liste française serait donc à écrire et à justifier, avec un risque nommé par la même doc : « Popular stop word lists may include words that are highly informative to some tasks ». Sur un corpus RGPD, la négation et les modaux (« ne … pas », « sans », « ni ») sont précisément ce qui départage `edg-11` de `edg-12` — les supprimer serait une faute.

---

## 4. Plongements de phrases + classifieur

### 4.1 Principe

Un encodeur de phrases gelé projette chaque texte en un vecteur dense ; un classifieur léger — typiquement une régression logistique — apprend sur ces vecteurs. L'encodeur ne bouge jamais : tout le savoir linguistique vient du pré-entraînement, tout le savoir de la tâche vient de la tête.

### 4.2 Les modèles disponibles en français, et ce que le benchmark en dit

[MTEB-French](https://arxiv.org/abs/2405.20468) (Ciancone, Kerboua, Schaeffer, Siblini, 2024) est la source primaire sur cette question : 15 jeux existants plus 3 créés, 8 catégories de tâches, **51 modèles comparés** avec tests statistiques. Le classement pertinent après élimination des modèles hébergés (§ 1.4) :

| Modèle | Dim. | Tokens max | Licence | Score moyen MTEB-Fr |
|---|---|---|---|---|
| [`OrdalieTech/Solon-embeddings-large-0.1`](https://huggingface.co/OrdalieTech/Solon-embeddings-large-0.1) (0,6 Md param., base XLM-R, **français**) | — | — | MIT | **0,67** |
| [`dangvantuan/sentence-camembert-large`](https://huggingface.co/dangvantuan/sentence-camembert-large) | — | — | — | **0,65** |
| [`intfloat/multilingual-e5-large`](https://huggingface.co/intfloat/multilingual-e5-large) (base XLM-R-large, 100 langues) | 1024 | 512 | MIT | classification 0,66 |
| [`sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2`](https://huggingface.co/sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2) (~118 M, 50+ langues) | 384 | **128** | Apache-2.0 | — |
| [`sentence-transformers/distiluse-base-multilingual-cased-v1`](https://huggingface.co/sentence-transformers/distiluse-base-multilingual-cased-v1) | 512 | **128** | Apache-2.0 | — |

Le résultat le plus utile du papier n'est pas le classement mais les **corrélations de Spearman entre caractéristiques du modèle et performance** : « entraîné pour la similarité de phrases » **0,727**, *fine-tuning* 0,544, **nombre de paramètres 0,49**, **dimension d'embedding 0,452**, longueur de contexte maximale **0,336** seulement. Les auteurs expliquent ce dernier chiffre par le fait que 14 des jeux ont moins de 50 tokens.

Deux lectures directes pour notre cas :

1. **Un petit modèle CPU n'est pas une concession.** Ce qui prédit la performance en français, c'est d'avoir été entraîné à la similarité de phrases, pas la taille. Le papier le dit explicitement : « some small models perform very well on the benchmark ».
2. **La limite de 128 tokens de MiniLM et distiluse est un risque mesurable, pas théorique.** Notre médiane est à 18 mots, mais la queue va à 193 mots — quelques exemples seraient tronqués, et la troncature est silencieuse.

Point de vigilance à ne pas rater : **E5 et Solon exigent un préfixe**. La carte E5 précise d'utiliser `query: ` même pour les tâches symétriques ; l'omettre dégrade les performances sans erreur visible.

### 4.3 Multi-label, exclusivité, seuils

Identiques au § 3.2 et § 3.3 : la tête est un classifieur scikit-learn, donc `OneVsRestClassifier` sur matrice indicatrice, seuils par classe réglables, exclusivité en post-traitement. **Cette famille et la précédente partagent exactement le même appareillage de décision** ; elles ne diffèrent que par la représentation d'entrée.

C'est ce qui rend leur comparaison propre : à tête identique, l'écart mesuré est imputable à la représentation seule.

### 4.4 Le régime de 120 exemples

C'est le point où cette famille se sépare nettement de la précédente. La représentation est **dense et de dimension fixe** — 384 à 1024 — et non creuse de dimension 755 croissant avec le corpus. La tête apprend donc 384 poids par étiquette sur 120 exemples, avec des features toutes informatives, au lieu de 755 dont 65 % de hapax.

Surtout, **le savoir linguistique n'est pas appris sur les 120 exemples** : il vient d'un pré-entraînement sur des corpus de plusieurs dizaines de milliards de tokens. Les 120 exemples ne servent qu'à placer six hyperplans dans un espace déjà structuré.

Aucune source primaire ne publie de volume minimal pour cette famille précise. Le repère le plus proche est celui de SetFit (§ 6.3), qui utilise la même représentation et fonctionne à **8 exemples par classe** — nous en avons 13 au minimum.

### 4.5 Déterminisme

L'encodage est un simple passage avant en mode `eval()` : ni *dropout*, ni échantillonnage, aucune graine. Le non-déterminisme résiduel est celui de PyTorch, dont la documentation est franche : « Completely reproducible results are not guaranteed across PyTorch releases, individual commits, or different platforms », et « results may not be reproducible between CPU and GPU executions, even when using identical seeds » ([PyTorch, *Reproducibility*](https://docs.pytorch.org/docs/stable/notes/randomness.html)).

Traduction opérationnelle : **le déterminisme est atteignable, mais il devient une propriété de l'artefact et de son environnement, plus une propriété de l'algorithme.** Il faut épingler la version de la bibliothèque, la révision exacte du modèle sur le Hub, et le *backend* d'inférence. C'est un déterminisme de second ordre par rapport à celui du § 3.5 — plus fort que celui d'une famille entraînée avec graine (§ 5, § 6), plus fragile que celui d'un solveur convexe.

### 4.6 Ce qu'elle sait dire de son doute

Rigoureusement identique au § 3.6 : la tête est une régression logistique, donc calibrée par construction, et `predict_proba` est directement lisible. **C'est le seul point du dossier où deux familles sont strictement à égalité**, et ce n'est pas un hasard : elles partagent la tête.

### 4.7 CPU

Le seul coût est le passage avant de l'encodeur. La documentation officielle de sentence-transformers ([*Speeding up Inference*](https://sbert.net/docs/sentence_transformer/usage/efficiency.html)) publie des facteurs d'accélération sur CPU par rapport à PyTorch fp32 : **ONNX ×1,39**, **ONNX int8 ×3,08**, **OpenVINO ×1,29**, avec une perte d'exactitude annoncée autour de **0,4 %** pour les variantes quantifiées. La recommandation officielle est OpenVINO int8 sur CPU Intel, ONNX int8 ailleurs. La doc avertit cependant que « for longer texts, ONNX and OpenVINO can even perform slightly worse than PyTorch » — nos textes sont courts, ce qui joue en notre faveur, puisque les gains les plus élevés sont mesurés sur le jeu aux textes les plus courts.

La doc ne publie pas de latence absolue. Un repère chiffré existe cependant côté SetFit, sur la même mécanique (§ 6.7) : **15,7 ms par échantillon en fp32, 4,6 ms après quantification INT8**, pour un encodeur de taille comparable. À comparer aux 5 s d'échéance du lexique : trois ordres de grandeur de marge.

---

## 5. Encodeur affiné pour la classification de séquence

### 5.1 Principe et modèles français

On reprend un encodeur pré-entraîné et on **met à jour tous ses poids** sur la tâche, avec une tête de classification. C'est la famille la plus puissante en régime de données abondantes, et la seule qui adapte la représentation elle-même à la tâche.

Le français y est particulièrement bien servi :

- **CamemBERT** (Martin *et al.*, [ACL 2020](https://aclanthology.org/2020.acl-main.645/), [arXiv:1911.03894](https://arxiv.org/abs/1911.03894)) — **110 M de paramètres, licence MIT**, architecture RoBERTa, 12 couches, dimension 768, entraîné sur OSCAR ([carte HF](https://huggingface.co/almanach/camembert-base)).
- **CamemBERT 2.0** (Antoun *et al.*, [arXiv:2411.08868](https://arxiv.org/abs/2411.08868)) — **275 milliards de tokens** de pré-entraînement contre ~32 pour la v1, tokeniseur mis à jour, contexte plus long. La motivation annoncée est directement pertinente ici : « outdated training data leads to a decline in performance, especially when encountering new topics and terminology ».
- **FlauBERT** (Le *et al.*, [LREC 2020](https://arxiv.org/abs/1912.05372)), avec son protocole d'évaluation FLUE incluant une tâche de classification de texte.

⚠️ Une confusion à éviter, parce qu'elle est tentante : CamemBERT annonce qu'« a relatively small web-crawled dataset (4 GB) leads to results that are as good as those obtained using larger datasets (130+ GB) ». **C'est une affirmation sur le volume de pré-entraînement, pas sur le volume d'annotation en aval.** Elle ne dit rien de notre régime à 120 exemples et ne doit pas être citée à ce titre.

### 5.2 Multi-label et exclusivité

HuggingFace expose le multi-label par un attribut de configuration. La documentation de `PreTrainedConfig` définit `problem_type` comme pouvant valoir « `"regression"`, `"single_label_classification"` or `"multi_label_classification"` » ([doc](https://huggingface.co/docs/transformers/main_classes/configuration)). La dernière valeur bascule la perte sur `BCEWithLogitsLoss`, soit **sept têtes sigmoïdes indépendantes** — l'expression la plus native du multi-label de tout le dossier, et les sept seuils sont libres.

Piège documenté : les étiquettes doivent être des **flottants**, `BCEWithLogitsLoss` ne faisant aucune conversion automatique ([issue transformers #20046](https://github.com/huggingface/transformers/issues/20046)).

Pour l'exclusivité, aucun mécanisme natif. La littérature offre deux voies :
- **Post-traitement cohérent** : Giunchiglia & Lukasiewicz, *Coherent Hierarchical Multi-Label Classification Networks*, [NeurIPS 2020](https://arxiv.org/abs/2010.10151), imposent la structure **sur les sorties du réseau**, sans le modifier, et montrent que cela « produce predictions coherent with the constraint and improve performance ». C'est exactement la règle du lexique, formalisée.
- **Tête hybride** : softmax sur le groupe mutuellement exclusif, sigmoïdes sur le reste. Applicable directement à notre cas — un groupe exclusif, six étiquettes indépendantes — mais sans outillage HuggingFace : c'est du code à écrire.

### 5.3 Le régime de 120 exemples — l'élimination

C'est ici que la famille tombe, et sur une source primaire explicite.

Szép *et al.*, *A Practical Guide to Fine-tuning Language Models with Limited Data* ([arXiv:2411.09539](https://arxiv.org/html/2411.09539v1)), donnent en table 2, pour la classification de texte : **sous 250 exemples, recommander PET avec adaptateurs, pas le fine-tuning complet** ; le *prefix-tuning* ne devient viable qu'à partir de ~1 000 exemples ; le fine-tuning complet en *sequence labeling* n'est recommandé qu'à partir de 5 000.

**120 exemples, c'est moins de la moitié du seuil de 250 en dessous duquel la littérature déconseille explicitement cette famille.** Et 13 exemples pour `portabilite` mettent la classe la plus rare deux ordres de grandeur sous le régime où l'affinage complet a du sens.

Dodge *et al.* ajoutent le mécanisme : l'affinage complet est « sample-inefficient and can be unstable in low-resource settings » ([arXiv:2002.06305](https://arxiv.org/abs/2002.06305)).

### 5.4 Déterminisme — la seconde élimination, indépendante de la première

Dodge, Ilharco, Schwartz, Farhadi, Hajishirzi et Smith ([arXiv:2002.06305](https://arxiv.org/abs/2002.06305)) ont affiné BERT **des centaines de fois** sur quatre jeux GLUE **en ne faisant varier que la graine**, et publié 2 100 essais. Résultat : l'affinage « is often brittle — even with the same hyperparameter values, distinct random seeds can lead to substantially different results ». Les deux facteurs pilotés par la graine — initialisation des poids et ordre des données — « contribute comparably to the variance of out-of-sample performance ». Sur les petits jeux, « many fine-tuning trials diverge part of the way through training ».

Mosbach, Andriushchenko et Klakow ([arXiv:2006.04884](https://arxiv.org/abs/2006.04884), ICLR 2021) précisent la cause — des « optimization difficulties that lead to vanishing gradients », et non l'oubli catastrophique ni la petite taille du jeu — et proposent une ligne de base plus stable. **Le remède existe donc, mais il confirme le diagnostic.**

Le point important pour la carte #42 : **« reproductible » et « stable » ne sont pas la même propriété.** À graine figée et environnement épinglé, un encodeur affiné est parfaitement reproductible — et peut néanmoins être arbitrairement mauvais, parce que la graine tirée l'a envoyé dans un mauvais minimum. L'ADR-0001 fonde l'absence de reprise sur le déterminisme des moteurs ; cette famille satisferait la lettre de cette exigence tout en la vidant de son sens.

### 5.5 Doute et CPU

**Aucune calibration native.** Les sorties de `BCEWithLogitsLoss` ne sont pas des probabilités calibrées, et les réseaux profonds sont réputés sur-confiants. Le remède — jeu de calibration séparé, *temperature scaling* sur les logits — **amputerait encore les 120 exemples**, ce qui referme la boucle avec le § 5.3.

CPU : c'est la famille la plus coûteuse du dossier, à l'entraînement comme à l'inférence, un encodeur de 110 M de paramètres à dimension 768 étant environ un ordre de grandeur plus cher qu'un MiniLM à 384. Aucune source primaire ne publie de latence CPU officielle pour CamemBERT. La contrainte de latence n'est cependant pas ce qui élimine cette famille (§ 1.3) — les § 5.3 et § 5.4 s'en chargent, et chacun suffirait.

---

## 6. Apprentissage contrastif à peu d'exemples (SetFit)

### 6.1 Principe

Tunstall, Reimers, Jo, Bates, Korat, Wasserblat et Pereg, *Efficient Few-Shot Learning Without Prompts*, [arXiv:2209.11055](https://arxiv.org/abs/2209.11055) (2022). Deux étapes : on affine d'abord un encodeur de phrases **par apprentissage contrastif sur des paires de textes** tirées du corpus annoté — même étiquette, paire positive ; étiquettes différentes, paire négative — puis on entraîne une tête de classification sur les embeddings ainsi spécialisés. **Sans prompt ni verbaliseur.**

C'est la famille intermédiaire entre le § 4 (encodeur gelé) et le § 5 (encodeur entièrement réappris) : l'encodeur bouge, mais l'objectif contrastif est bien plus économe en données qu'une perte de classification.

### 6.2 Multi-label et exclusivité

La documentation officielle ([SetFit, *Multilabel*](https://huggingface.co/docs/setfit/en/how_to/multilabel)) expose `multi_target_strategy` avec exactement les trois stratégies scikit-learn : `"one-vs-rest"` → `OneVsRestClassifier`, `"multi-output"` → `MultiOutputClassifier`, `"classifier-chain"` → `ClassifierChain`. Utilisable avec la tête logistique par défaut ou avec `use_differentiable_head=True`.

**Aucune des trois n'exprime l'exclusivité** — la remarque du § 3.2 s'applique intégralement, y compris la réserve sur la moyenne de chaînes ordonnées aléatoirement.

### 6.3 Le régime de 120 exemples — le meilleur dossier chiffré

Le papier est calibré sur **N = 8 exemples par classe**, soit près de deux fois moins que notre classe la plus rare. Le résultat le plus cité : avec 8 exemples annotés par classe sur Customer Reviews, SetFit est compétitif avec un affinage de RoBERTa-Large sur les 3 000 exemples complets.

Moyennes sur les jeux du papier (accuracy, moyenne ± écart-type sur 10 tirages) :

| Régime | FineTune | PERFECT | ADAPET | T-Few 3B | **SetFit-MPNet** |
|---|---|---|---|---|---|
| N = 8 | — | 48,7 ± 6,0 | 58,3 ± 3,6 | 63,4 ± 1,9 | **62,3 ± 4,9** |
| N = 64 | — | — | 73,8 ± 2,2 | 70,3 ± 1,5 | **75,3 ± 1,3** |

Sur RAFT (11 tâches, 50 exemples d'entraînement chacune), SetFit-RoBERTa (355 M) obtient 71,3 contre 62,7 pour GPT-3 à 175 milliards de paramètres.

**Avec 13 à 30 exemples par classe, nous sommes entre les deux régimes mesurés**, plus près de N = 8 que de N = 64 pour les classes rares. C'est la seule famille du dossier dont le régime nominal publié correspond à notre volume.

### 6.4 Déterminisme — la contrepartie, chiffrée

Les écarts-types du tableau ci-dessus ne sont pas décoratifs : le protocole du papier retient **10 tirages d'entraînement aléatoires par jeu et par taille**, motivé explicitement par le fait que l'affinage sur petits jeux « may incur instability ». La dispersion mesurée à N = 8 va de **±1,9 à ±11,8 points d'exactitude** selon le jeu, le pire cas étant AmazonCF — un jeu à classes rares et déséquilibrées, c'est-à-dire notre configuration.

Côté API, `TrainingArguments.seed` vaut 42 par défaut, et la doc note : « To ensure reproducibility across runs, use the `model_init` argument to Trainer » ([doc](https://huggingface.co/docs/setfit/en/reference/trainer)). D'autres paramètres influencent le tirage des paires contrastives : `num_iterations`, `sampling_strategy` (`"oversampling"` par défaut).

La lecture honnête est la même qu'au § 5.4, à un degré moindre : **à graine fixée l'entraînement est reproductible, mais le modèle produit dépend de la graine.** Le déterminisme du service exige donc de **geler et versionner l'artefact entraîné**, pas seulement le code — ce qui a une conséquence concrète sur la `QualificationEngineIdentity`, dont la spec dit qu'elle porte « celle de ses règles pour un lexique, celle du modèle servi pour un LLM » : il faudrait ici une troisième forme, la version de l'artefact entraîné.

Et la variance de ±11,8 points n'est pas neutralisée par le gel : elle signifie que **le chiffre qu'on lira dans le notebook dépendra du tirage**, ce qui valide rétrospectivement la décision 2 de la carte #42 (rendre un intervalle, jamais un chiffre unique).

### 6.5 Ce qu'il sait dire de son doute

Le papier ne dit rien de la calibration. La tête étant une régression logistique scikit-learn, on retrouve `predict_proba` par étiquette et les seuils réglables du § 3.3 — mais **sur des embeddings que l'apprentissage contrastif a délibérément rendus séparables**, ce qui pousse mécaniquement les probabilités vers les extrêmes. Il est plausible que la calibration soit moins bonne qu'au § 4, où l'encodeur reste gelé ; **aucune source primaire ne le mesure**, et c'est un point à instrumenter dans le notebook plutôt qu'à supposer.

En stratégie *one-vs-rest*, « aucune étiquette » est directement exprimable (toutes les probabilités sous le seuil) — ce qui, pour notre taxonomie, est le signal naturel de `hors-perimetre`.

### 6.6 Français

Le papier a une section multilingue : le corps est remplacé par [`paraphrase-multilingual-mpnet-base-v2`](https://huggingface.co/sentence-transformers/paraphrase-multilingual-mpnet-base-v2), et l'évaluation porte sur le Multilingual Amazon Reviews Corpus en 6 langues dont le français. À N = 8, l'erreur absolue moyenne (×100, plus bas = mieux) en français : **SetFit 82,2 à 85,3** selon le régime, contre **117,3 à 123,2 pour l'affinage complet** — soit une réduction d'environ 30 %.

Résultat annexe utile : le transfert *anglais → français* est aussi bon que l'entraînement monolingue français. Il n'a pas d'usage direct ici (notre corpus est français), mais il indique que la représentation multilingue ne pénalise pas le français.

### 6.7 CPU

Le papier chiffre l'entraînement à **30 secondes sur un V100 pour 8 exemples annotés**, contre ~700 s pour T-Few 3B — et le blog officiel précise « you can even train SetFit on CPU in just a few minutes ». À l'inférence, le coût est celui d'**un seul passage avant** d'encodeur, comme au § 4.

Chiffres CPU publiés par HuggingFace sur un encodeur de taille comparable, lot de 1 : **15,69 ± 0,57 ms par échantillon en PyTorch fp32, 4,55 ± 0,25 ms après quantification INT8** (×3,45), l'exactitude passant de 88,4 % à 88,1 % et la taille du modèle de 127 Mo à 45 Mo.

Face aux 5 s d'échéance du lexique, la marge est de trois ordres de grandeur.

---

## 7. Inférence en langue naturelle détournée en classification sans exemple

### 7.1 Principe

Yin, Hay et Roth, *Benchmarking Zero-shot Text Classification*, [EMNLP-IJCNLP 2019](https://aclanthology.org/D19-1404/) ([arXiv:1909.00161](https://arxiv.org/abs/1909.00161)). Le texte devient la prémisse, chaque étiquette devient une hypothèse en langue naturelle, et un modèle d'inférence en langue naturelle décide si la prémisse implique l'hypothèse. Gabarits du papier : « the text is about ___ », « this text expresses ___ ». Gabarit par défaut du pipeline HuggingFace : `"This example is {}."`

**Cette famille ne s'entraîne pas.** C'est sa singularité dans le dossier, et la source de tous ses avantages comme de tous ses défauts.

### 7.2 Multi-label et exclusivité — le point le plus subtil du dossier

La documentation du pipeline HuggingFace est explicite sur `multi_label` : « Whether or not multiple candidate labels can be true. If `False`, the scores are normalized such that the sum of the label likelihoods for each sequence is 1. If `True`, the labels are considered independent and probabilities are normalized for each candidate by doing a softmax of the entailment score vs. the contradiction score » ([doc](https://huggingface.co/docs/transformers/en/main_classes/pipelines)).

Précision qui compte et que la formulation courante rate : en mode multi-label, **ce n'est pas une sigmoïde sur le logit d'implication**, mais un **softmax à deux termes (contradiction, implication) par hypothèse**, le logit *neutre* étant purement ignoré.

Conséquence directe sur notre invariant :

- `multi_label=False` impose un softmax sur les **sept** étiquettes, donc une exclusivité **totale** : on perdrait les 19 exemples multi-droits. Inutilisable seul.
- `multi_label=True` rend sept scores indépendants, donc **aucune** exclusivité : `hors-perimetre` peut sortir avec `effacement`.
- **Il n'existe pas de mode mixte.** La règle du § 1.2 est à écrire à la main, comme partout ailleurs — mais ici la bibliothèque ne fournit même pas une matrice indicatrice sur laquelle l'appliquer proprement.

Corollaire pour l'abstention : « je ne sais pas » n'est exprimable qu'en mode multi-label. En mode exclusif, la somme vaut 1 et **l'abstention est structurellement impossible**.

Le papier original traite d'ailleurs le cas multi-label exactement ainsi : sur la tâche *situation*, « on retient toutes les étiquettes prédites positives », avec un seuil α = 0,05 comme marge de décision, et une classe « none » explicitement présente dans la taxonomie — soit la même structure que notre `hors-perimetre`.

### 7.3 Le régime de 120 exemples — un avantage réel et une performance basse

Aucune donnée annotée n'est requise. **C'est la seule famille pour laquelle les 120 exemples ne servent pas à entraîner mais uniquement à calibrer sept seuils et à valider** — ce qui, en régime de très peu de données, est un avantage structurel : elle est la seule à n'être pas contrainte par le volume.

La contrepartie est chiffrée et sévère. Table 6 du papier, régime *label-fully-unseen* :

| Modèle | Topic (10 étiquettes) | Emotion (10) | Situation (12, multi-label) |
|---|---|---|---|
| Majorité | 10,0 | 5,9 | 11,0 |
| Wikipedia-based | 52,1 | 21,2 | 27,7 |
| BERT-MNLI | 37,9 | 22,3 | 15,4 |
| BERT-RTE | 43,8 | 12,6 | 37,2 |
| Ensemble | 45,7 | 25,2 | 38,0 |

Soit **25 à 46 % sur des taxonomies de 10 à 12 étiquettes** — au-dessus du hasard, très loin d'un supervisé. Les auteurs concluent que l'ordre d'efficacité des modèles pré-entraînés en *label-fully-unseen* est « RTE >> FEVER >> MNLI », ce qui est contre-intuitif et mérite d'être noté : le plus gros corpus d'inférence n'est pas le meilleur point de départ.

### 7.4 Déterminisme — le meilleur du dossier, avec une nuance

Pas d'entraînement, donc **pas de graine, pas de variance inter-graines, pas d'artefact entraîné à versionner**. En `eval()` et `no_grad()` sur CPU, un passage avant est déterministe aux réserves générales de PyTorch près (§ 4.5).

La nuance est ailleurs, et elle est importante : **le gabarit d'hypothèse et le libellé français des étiquettes déplacent fortement les scores**, et ce ne sont pas des hyperparamètres appris. Ce sont des choix de conception qui doivent être versionnés comme du code — au même titre que les règles du lexique, dont la spec dit que leur version « est à incrémenter dès que le comportement du moteur change ».

C'est un déplacement du problème plutôt qu'une solution : la famille est déterministe, mais son comportement dépend d'une chaîne de caractères en français qu'un humain a choisie, sans procédure de réglage.

### 7.5 Calibration

Le softmax à deux termes n'offre **aucune garantie de calibration** : c'est une renormalisation post-hoc des logits d'un modèle entraîné sur MNLI/XNLI, pas sur notre taxonomie. Les scores sont fortement dépendants du libellé et **non comparables entre étiquettes** — ce qui interdit un seuil unique.

Retournement notable : nos 120 exemples sont exactement le bon budget pour **apprendre sept seuils par étiquette** sur les scores d'implication. Sept paramètres appris sur 120 exemples, c'est un régime statistique sain, alors que 384 poids par étiquette ne l'est pas. **C'est le levier principal de cette famille**, et il est peu coûteux.

### 7.6 Le CPU — l'obstacle structurel

Le pipeline construit **une paire (prémisse, hypothèse) par étiquette candidate**. Pour notre taxonomie de sept valeurs, cela fait **sept passages avant par texte**. Ils se regroupent en un lot de 7, mais le total des opérations reste multiplié par 7. **C'est structurel et non contournable** : c'est la définition même de la méthode.

Modèles NLI utilisables en français, avec ce qu'ils coûtent :

| Modèle | Paramètres | Licence | XNLI français | Latence CPU / passe | ×7 étiquettes |
|---|---|---|---|---|---|
| [`cmarkea/distilcamembert-base-nli`](https://huggingface.co/cmarkea/distilcamembert-base-nli) | 68,1 M | **MIT** | 77,45 % | **51,4 ms** (Ryzen 5 4500U) | ≈ **360 ms** |
| [`MoritzLaurer/mDeBERTa-v3-base-xnli-multilingual-nli-2mil7`](https://huggingface.co/MoritzLaurer/mDeBERTa-v3-base-xnli-multilingual-nli-2mil7) | 0,3 Md | **MIT** | **0,823** | non publiée | ≈ 2–3 s (estimé) |
| [`morit/french_xlm_xnli`](https://huggingface.co/morit/french_xlm_xnli) | ~278 M | **MIT** | 78,02 % | non publiée | — |
| `BaptisteDoyen/camembert-base-xnli` | ~110 M | ⚠️ voir ci-dessous | 81,7 % (source secondaire) | ~105 ms | ≈ 735 ms |

⚠️ **`BaptisteDoyen/camembert-base-xnli` est à écarter en l'état.** Au moment de cette recherche, sa carte de modèle sur le Hub ne contient plus qu'un avis de restriction — usage réservé à une entreprise, téléchargement et accès par des tiers explicitement interdits — et les README bruts renvoient HTTP 401. Les chiffres de 81,4/81,7 % circulant à son sujet proviennent de reprises secondaires de l'ancienne carte, pas de la source primaire actuelle. Sur le critère de l'auto-hébergement strict, seuls les modèles sous MIT ci-dessus sont sûrs.

Le socle d'évaluation commun est **XNLI** (Conneau *et al.*, [EMNLP 2018](https://arxiv.org/abs/1809.05053)), extension des jeux de développement et de test de MultiNLI à 15 langues dont le français.

Enfin, la page de tâche HuggingFace note que la capacité *zero-shot* émerge au-delà d'environ 100 M de paramètres et croît avec la taille — ce qui pousse vers mDeBERTa (0,3 Md), donc vers la latence haute du tableau. **L'arbitrage interne à cette famille est frontal : qualité française contre budget CPU multiplié par sept.**

Il faut être exact sur la portée de cet obstacle : **360 ms à 3 s reste très en deçà de l'échéance de 5 s du lexique** et invisible sous l'ombre du LLM. Le facteur 7 n'est donc pas éliminatoire au sens du § 1.4 — il place simplement cette famille deux à trois ordres de grandeur au-dessus de toutes les autres en coût unitaire, ce qui devient significatif dès qu'on parle de rejouer les 120 exemples en validation croisée, et davantage encore avec le point du § 8.3.

---

## 8. Plus proches voisins et prototypes

### 8.1 Trois variantes qui ne se valent pas

**k-NN sur plongements** — [`KNeighborsClassifier`](https://scikit-learn.org/stable/modules/generated/sklearn.neighbors.KNeighborsClassifier.html) accepte nativement un `y` de forme `(n_samples, n_outputs)` et figure dans la liste officielle des estimateurs multi-label natifs. `predict_proba` renvoie une liste de tableaux, un par sortie.

**Prototypes / centroïdes** — [`NearestCentroid`](https://scikit-learn.org/stable/modules/generated/sklearn.neighbors.NearestCentroid.html) : « Each class is represented by its centroid, with test samples classified to the class with the nearest centroid », et la doc note que « when used for text classification with tf-idf vectors, this classifier is also known as the Rocchio classifier ». **Limite bloquante : il ne supporte pas le multi-label.** Il faut soit l'envelopper dans un `OneVsRestClassifier`, soit — plus propre — calculer les sept centroïdes soi-même et seuiller les similarités cosinus.

**ML-kNN** — Zhang & Zhou, *ML-KNN: A lazy learning approach to multi-label learning*, [Pattern Recognition 40(7):2038–2048, 2007](https://dl.acm.org/doi/10.1016/j.patcog.2006.12.019). Pour chaque instance, on identifie les k voisins puis on décide **étiquette par étiquette par maximum a posteriori** sur le nombre de voisins portant l'étiquette — une adaptation bayésienne, pas une simple binarisation. L'implémentation `skmultilearn.adapt.MLkNN` expose un paramètre de lissage `s` (défaut 1,0) et un `predict_proba` calculé comme `p_true / (p_true + p_false)`. Réserve pratique : **scikit-multilearn est peu maintenu**, et sa compatibilité avec les versions courantes de scikit-learn et NumPy est à vérifier avant tout engagement.

### 8.2 Calibration : l'écart entre les trois variantes est décisif

Avec `weights='uniform'`, le `predict_proba` de k-NN est la **fréquence des voisins**, donc une valeur sur la grille `{0, 1/k, …, 1}`. À k = 5, cela fait **six valeurs possibles** : résolution grossière, seuils fins impossibles, scores saturés à 0 ou 1. Et k ne peut pas monter beaucoup sans noyer une classe à 13 exemples. `weights='distance'` lisse la grille et est le réglage indiqué ici.

Les deux autres variantes n'ont pas ce défaut : **le lissage `s` de ML-kNN empêche exactement la probabilité dégénérée à 0** pour une étiquette rare, et une similarité cosinus à un centroïde est un score continu, seuillable finement.

Un argument statistique supplémentaire favorise nettement les prototypes dans notre régime : **le centroïde d'une classe à 13 exemples est bien plus stable que ses 3 à 5 plus proches voisins.** Il moyenne 13 observations ; k-NN en consulte 5 dont aucune n'est garantie d'appartenir à la classe.

### 8.3 Déterminisme — et un avertissement à lire au mot près

La doc de `KNeighborsClassifier` publie la seule source de non-déterminisme de la famille, et elle la publie franchement :

> « Regarding the Nearest Neighbors algorithms, if it is found that two neighbors, neighbor `k+1` and `k`, have identical distances but different labels, the results will depend on the ordering of the training data. »

C'est **contrôlable** : ordre du jeu d'entraînement figé, résultat parfaitement reproductible, sans aucune graine. Et sur des plongements denses en flottants, les égalités exactes de distance sont quasi impossibles. Les centroïdes et ML-kNN n'ont aucun tirage aléatoire du tout.

**Cette famille est, avec le § 3 et le § 7, l'un des trois seuls déterminismes du dossier qui ne repose ni sur une graine ni sur le gel d'un artefact entraîné.**

### 8.4 CPU et français

Le coût d'ajustement est nul — c'est de la mémorisation. La recherche de voisins sur 120 points en force brute est négligeable. **Le seul coût réel est l'encodage : un passage avant, contre sept pour le NLI.** La famille est agnostique au français ; toute la qualité vient de l'encodeur, et `paraphrase-multilingual-mpnet-base-v2` — validé en français par le papier SetFit (§ 6.6) — est directement réutilisable gelé.

Cela fait de cette famille **la ligne de base naturelle de SetFit** : encodeur identique, aucun entraînement contrastif. L'écart mesuré entre les deux isole exactement ce qu'apporte l'étape contrastive, ce qui est précisément la forme de comparaison que la carte #42 a instrumentée (décision 3).

---

## 9. Ce qui traverse toutes les familles

### 9.1 Aucune famille n'exprime nativement l'exclusivité de `hors-perimetre`

C'est le résultat transverse le plus net de cette recherche, et il n'était pas acquis d'avance.

| Famille | Mécanisme multi-label | Exclusivité |
|---|---|---|
| TF-IDF + linéaire | `OneVsRestClassifier` | non |
| Plongements + classifieur | `OneVsRestClassifier` | non |
| Encodeur affiné | `BCEWithLogitsLoss` | non |
| SetFit | `multi_target_strategy` | non |
| NLI zero-shot | `multi_label=True` | non — et le mode exclusif est global, donc inutilisable |
| Voisins / prototypes | natif | non |

Trois voies existent, et elles se classent proprement :

1. **Post-traitement déterministe** — la voie du lexique actuel : évaluer les six droits, ne rendre `hors-perimetre` que si aucun n'a franchi son seuil. Formalisée par C-HMCNN (Giunchiglia & Lukasiewicz, [NeurIPS 2020](https://arxiv.org/abs/2010.10151)), qui impose la cohérence sur les sorties sans toucher au modèle. **Universelle : applicable aux six familles, à coût nul.**
2. **Tête hybride** — softmax sur le groupe exclusif, sigmoïdes sur le reste. Applicable aux familles à réseau (§ 5), sans outillage disponible : c'est du code à écrire.
3. **`ClassifierChain` avec la classe exclusive en tête** — la seule voie qui *apprend* la contrainte. Disponible dans les familles à tête scikit-learn (§ 3, § 4, § 6). Mais avec 12 combinaisons observées sur 19 exemples, **il n'y a probablement pas de corrélation apprenable**, et la recommandation officielle de moyenner plusieurs chaînes ordonnées aléatoirement contredit à la fois le déterminisme et l'exclusivité.

**Conséquence de cadrage** : le choix du § 10 porte sur la représentation et la tête, pas sur l'exclusivité — qui sera une règle écrite quelle que soit la famille retenue, et qui devra vivre là où vit déjà la vérification d'invariant, c'est-à-dire dans le sidecar (spec § 5.4).

### 9.2 Le déterminisme se lit sur trois niveaux, pas deux

La contrainte de l'ADR-0001 est binaire — « les moteurs sont déterministes » — mais la recherche fait apparaître **trois régimes distincts**, dont la confusion serait coûteuse.

- **Déterminisme algorithmique.** Aucune source d'aléa dans la méthode. `LogisticRegression` avec `lbfgs` (§ 3.5), k-NN et centroïdes (§ 8.3), NLI zero-shot (§ 7.4). Le plus fort : il survit à un changement de machine et de version.
- **Déterminisme d'artefact.** L'inférence est un passage avant sans aléa, mais dépend d'un modèle téléchargé dont la version doit être épinglée, ainsi que la version de PyTorch et le *backend*. Familles à plongements gelés (§ 4). La doc PyTorch est explicite sur la fragilité : « Completely reproducible results are not guaranteed across PyTorch releases, individual commits, or different platforms ».
- **Déterminisme de graine.** Le modèle lui-même est le produit d'un tirage. SetFit (§ 6.4), encodeur affiné (§ 5.4). Reproductible à graine fixée, mais **« reproductible » n'y implique pas « stable »** : Dodge *et al.* mesurent une variance substantielle inter-graines, SetFit publie jusqu'à ±11,8 points d'écart-type à N = 8.

Le troisième niveau satisfait la lettre de l'ADR-0001 sans en satisfaire l'esprit — l'ADR fonde l'absence de reprise sur le fait que rejouer donne le même résultat, ce qui reste vrai, mais le résultat lui-même a été tiré au sort une fois pour toutes.

**Une tension méthodologique en découle, et elle est réelle.** La documentation scikit-learn sur les pièges courants avertit que fixer une graine entière pour la validation croisée fait qu'« if the estimator performs well (or bad), as evaluated by CV, it might just be because we got lucky (or unlucky) with that specific seed », et recommande de laisser varier le générateur d'un pli à l'autre pour obtenir des résultats robustes ([*Common pitfalls*](https://scikit-learn.org/stable/common_pitfalls.html)). Autrement dit : **le service doit être déterministe, l'évaluation doit délibérément ne pas l'être.** Ce ne sont pas deux exigences contradictoires mais deux exigences portant sur deux objets différents — et c'est exactement ce que la décision 2 de la carte #42 avait anticipé en exigeant un intervalle plutôt qu'un chiffre.

### 9.3 Ce que chaque famille sait dire de son doute — le critère que l'ADR-0001 rend décisif

L'ADR-0001 fait du silence du lexique sa limite : « ses erreurs sont **silencieuses** ; pour une aide à la décision, ne pas savoir dire qu'on doute est plus grave qu'un taux d'erreur ». Une famille qui rend un score calibré a donc une valeur qu'aucun score d'exactitude ne capture.

**Ce qu'un score calibré serait, et ne serait pas.** La spec refuse explicitement un score numérique du LLM : « `0,73` rendu par un modèle de 8 milliards de paramètres n'est pas une probabilité, c'est un mot choisi qui ressemble à un nombre » (§ 5.3). **Cet argument ne se transpose pas à un classifieur supervisé.** Le `predict_proba` d'une régression logistique n'est pas un mot produit par un décodeur : c'est une estimation ajustée sur des données étiquetées, dont la doc scikit-learn donne le critère de validité — « a well calibrated (binary) classifier should classify the samples such that among the samples to which it gave a `predict_proba` value close to, say, 0.8, approximately 80 % actually belong to the positive class » ([doc](https://scikit-learn.org/stable/modules/calibration.html)) — et qui est **mesurable** par la perte logarithmique, le score de Brier, ou une courbe de fiabilité (`CalibrationDisplay`).

C'est la différence de nature qui rend cette question intéressante pour la carte : **un troisième moteur pourrait, contrairement au lexique, remplir une `DeclaredConfidence`** — la spec la déclare optionnelle et note que « le lexique ne rend pas ce champ ». Mais la spec ajoute qu'une confiance constante serait un « mensonge typé », et un score mal calibré n'en serait qu'une variante plus insidieuse. **Une famille qui rendrait une confiance non mesurée serait pire que le lexique qui n'en rend aucune.**

Classement des six familles sur ce critère :

1. **TF-IDF + linéaire** et **plongements + tête logistique** — à égalité, et en tête. Régression logistique calibrée par construction, `predict_proba` directement lisible, **aucun jeu de calibration à prélever sur les 120 exemples**.
2. **SetFit** — même tête, mais sur des embeddings délibérément rendus séparables par l'objectif contrastif, ce qui pousse les probabilités vers les extrêmes. Plausiblement moins bien calibré ; non mesuré par la littérature.
3. **ML-kNN et centroïdes** — score continu, lissé pour ML-kNN. **k-NN brut** est distinctement moins bon : grille à k+1 valeurs.
4. **NLI zero-shot** — non calibré, scores non comparables entre étiquettes ; mais sept seuils appris sur 120 exemples sont un régime statistique sain, et c'est le levier principal de la famille.
5. **Encodeur affiné** — aucune calibration native, sur-confiance documentée, et le remède ampute le corpus.

**Les recalibrages a posteriori sont largement fermés par le volume.** La doc scikit-learn est chiffrée : « 'isotonic' will perform as well as or better than 'sigmoid' when there is enough data **(greater than ~1000 samples)** to avoid overfitting ». Avec 120 exemples, **la régression isotone est disqualifiée par la documentation officielle elle-même**. Le *sigmoid* (Platt) est présenté comme « most effective for small sample sizes », mais suppose une erreur de calibration symétrique — « This can be a problem for highly imbalanced classification problems », ce qui est notre cas. Et la règle absolue s'applique : le calibrateur doit être ajusté sur des données distinctes de l'entraînement.

Un point de rupture technique concret mérite d'être nommé : `CalibratedClassifierCV(ensemble=True)` exige que **toutes les classes soient présentes dans le jeu d'entraînement et dans le jeu de test de chaque découpage**. Avec 13 exemples pour `portabilite` et cv = 5, cela laisse 2 à 3 exemples par pli — c'est la première chose qui cassera.

**Une garantie formelle existe, et il faut être franc sur son coût.** La prédiction conforme (Angelopoulos & Bates, [arXiv:2107.07511](https://arxiv.org/abs/2107.07511)) produit des ensembles de prédiction « guaranteed to contain the ground truth with a user-specified probability », **sans hypothèse de distribution ni de modèle**, sous la seule condition d'échangeabilité. Des variantes multi-label sont implémentées dans [MAPIE](https://github.com/scikit-learn-contrib/MAPIE) (`scikit-learn-contrib`) : RCPS et CRC contrôlent le rappel, LTT la précision. C'est la seule voie du dossier qui offre un « je ne sais pas » avec une garantie plutôt qu'une heuristique. **Mais elle exige un jeu de calibration séparé** : sur 120 exemples, ce jeu serait minuscule et les ensembles de prédiction en sortiraient très larges — donc peu informatifs. Elle est mentionnée pour complétude, pas comme une option prête.

### 9.4 Une contrainte d'architecture que la recherche fait apparaître

La spec impose que le sidecar « serve ses deux points d'entrée concurremment », faute de quoi un appel LLM en cours — jusqu'à 120 s — « affamerait » le point d'entrée du lexique, dont l'échéance de 5 s se déclencherait massivement : « l'entrecontrôle s'éteindrait précisément quand le service travaille » (§ 5.8).

Or **toutes les familles de ce dossier sauf la première sont bornées par le calcul, pas par les entrées-sorties.** Un passage avant de 15 ms — et *a fortiori* les 360 ms à 3 s du NLI — exécuté dans la boucle d'événements bloquerait le point d'entrée du lexique tout du long. La documentation FastAPI donne la parade : « When you declare a *path operation function* with normal `def` instead of `async def`, it is run in an external threadpool that is then awaited, instead of being called directly (as it would block the server) » ([doc](https://fastapi.tiangolo.com/async/)).

Ce n'est pas une objection à une famille en particulier — c'est **une contrainte d'implémentation qui s'appliquera au troisième moteur quel qu'il soit**, et qui pèse d'autant plus lourd que le coût unitaire est élevé. Elle mérite d'être nommée maintenant : c'est exactement le type de détail que la spec a déjà eu à traiter une fois, et qui coûte cher découvert tard.

### 9.5 Un obstacle méthodologique à la décision 2 de la carte

La carte #42 exige une « validation croisée **stratifiée** sur les 120 exemples ». La documentation de `StratifiedKFold` décrit un découpage qui préserve les proportions « for each class in `y` in a **binary or multiclass** classification setting » — **le multi-label n'y figure pas**, et il n'est pas supporté.

La stratification multi-label est un problème traité en soi : Sechidis, Tsoumakas et Vlahavas, *On the Stratification of Multi-Label Data*, [ECML PKDD 2011](https://link.springer.com/chapter/10.1007/978-3-642-23808-6_10), en proposent l'algorithme itératif de référence, implémenté hors de scikit-learn ([`iterative-stratification`](https://github.com/trent-b/iterative-stratification), `IterativeStratification`).

**Cet obstacle est indépendant de la famille retenue.** Il concerne le harnais de mesure et devra être réglé quel qu'en soit le résultat — soit par stratification itérative, soit par une stratification sur une projection mono-label du corpus, assumée comme telle. Avec 13 exemples pour `portabilite` et un découpage en 5 plis, une stratification approximative laisserait 2 à 3 exemples par pli et pourrait produire des plis sans aucun positif — auquel cas la métrique n'est pas seulement bruitée, elle est indéfinie.

---

## 10. Où les familles se départagent, et où elles ne se départagent pas

**Deux familles ne survivent pas aux contraintes appliquées sans indulgence.**

L'**encodeur affiné** (§ 5) tombe deux fois, et chaque chute suffirait : 120 exemples est moins de la moitié du seuil de 250 sous lequel la littérature déconseille explicitement l'affinage complet en classification de texte ; et la variance inter-graines documentée par Dodge *et al.* rend son déterminisme formel sans le rendre stable. S'y ajoute, en troisième position seulement, qu'elle est la plus coûteuse sur un CPU déjà contraint et la seule sans calibration native.

Le **NLI zero-shot** (§ 7) ne tombe pas sur les contraintes éliminatoires — 360 ms à 3 s tiennent dans le budget — mais sur la performance : 25 à 46 % sur des taxonomies comparables en régime *label-fully-unseen*, et un mode multi-label qui ne peut pas exprimer l'exclusivité. Son cas est cependant le plus asymétrique du dossier : c'est la seule famille qui **ne consomme aucun exemple d'entraînement**, donc la seule dont les 120 exemples serviraient intégralement à valider. Sa place naturelle n'est peut-être pas celle de candidat mais celle de **borne inférieure sans annotation** dans le notebook — ce que la session de décision est mieux placée que ce document pour trancher.

**Quatre familles restent en lice, et la recherche ne les départage pas.**

Le **sac de mots** (§ 3) a le meilleur déterminisme du dossier — algorithmique, sans graine ni artefact —, la meilleure calibration à égalité, un coût nul, et aucun modèle à télécharger. Il a contre lui le fait le plus dur du corpus : 755 features pour 120 exemples, dont 65 % de hapax. Aucune source ne dit s'il s'effondre ou tient ; seule la mesure le dira, et c'est précisément ce que le notebook est fait pour établir.

Les **plongements + tête logistique** (§ 4) résolvent le problème de représentation du précédent sans rien perdre de sa tête ni de sa calibration, au prix d'un déterminisme d'artefact plutôt qu'algorithmique et d'un modèle à versionner. MTEB-French donne le fait qui compte : ce qui prédit la performance en français est d'avoir été entraîné à la similarité de phrases (ρ = 0,727), pas la taille (ρ = 0,49) — donc un petit modèle CPU n'est pas une concession.

**SetFit** (§ 6) est la seule famille dont le régime nominal publié corresponde à notre volume, avec des chiffres à N = 8 là où nous en avons 13 au minimum. Elle a contre elle un déterminisme de graine et une variance mesurée jusqu'à ±11,8 points sur le jeu à classes déséquilibrées — c'est-à-dire notre configuration.

Les **prototypes** (§ 8) sont le plus économe et le plus déterministe des trois précédents à représentation dense, avec un argument statistique propre à notre régime : un centroïde de 13 exemples est plus stable que 5 voisins. Ils ont contre eux une calibration moins fine.

**Ce que ce document affirme sur ces quatre-là**, et qui devrait cadrer le grilling :

- Les § 4, § 6 et § 8 **partagent le même encodeur**. À encodeur fixé, elles forment une échelle croissante d'engagement — prototypes (rien n'est appris), tête logistique (six hyperplans), SetFit (l'encodeur bouge) — dont chaque échelon coûte en déterminisme ce qu'il promet en performance. **Elles se comparent à matériau constant**, ce qui est exactement la forme instrumentée par la décision 3 de la carte #42.
- Le § 3 n'est comparable à aucune des trois autrement que par le résultat, puisqu'il ne partage ni la représentation ni le modèle pré-entraîné. Il est en revanche le seul à ne dépendre d'aucun téléchargement, ce qui a une valeur d'exploitation que la carte n'a pas encore chiffrée.
- **L'exclusivité de `hors-perimetre` ne départage rien** : elle sera une règle écrite dans les six cas (§ 9.1).
- **Le multi-label ne départage presque rien non plus** : à une cardinalité de 1,17 et 12 combinaisons sur 19 exemples, aucune famille n'apprendra de corrélation entre étiquettes. Ce qui départage, c'est la représentation, la calibration et le niveau de déterminisme.

---

## 11. Ce que cette recherche n'a pas établi

Résultats négatifs, à ne pas confondre avec des absences de résultat.

- **Aucune latence CPU officielle n'est publiée pour CamemBERT**, ni pour un TF-IDF + régression logistique sur 120 documents. Les ordres de grandeur donnés aux § 3.7 et § 5.5 sont dérivés, pas sourcés.
- **Aucune source primaire ne mesure la calibration de SetFit.** L'hypothèse du § 6.5 — que l'objectif contrastif dégrade la calibration en poussant les probabilités vers les extrêmes — est un raisonnement, pas un fait. C'est à instrumenter dans le notebook.
- **Aucun volume minimal publié** pour le sac de mots ni pour les plongements + tête. Le seuil de 250 exemples du § 5.3 ne vaut que pour l'affinage complet.
- **La latence CPU de mDeBERTa-v3-base n'est pas publiée** ; l'estimation de 2 à 3 s pour sept étiquettes est extrapolée du rapport de tailles.
- **Le statut de licence de `BaptisteDoyen/camembert-base-xnli` est à vérifier de première main** avant tout usage ; les chiffres qui circulent à son sujet ne proviennent plus d'une source primaire accessible.
- **Aucun benchmark français de classification multi-label en régime de très peu de données** n'a été trouvé. MTEB-French évalue la classification, mais mono-label et sur des jeux de taille normale. Le corpus de 120 exemples reste, pour cette question, sa propre référence — ce qui est précisément la raison d'être du notebook de la carte #42.

---

## Sources

**Articles**

- Ciancone, Kerboua, Schaeffer, Siblini — *MTEB-French: Resources for French Sentence Embedding Evaluation and Analysis*, [arXiv:2405.20468](https://arxiv.org/abs/2405.20468) · [HTML](https://arxiv.org/html/2405.20468v2) · [code](https://github.com/Lyon-NLP/mteb-french)
- Tunstall, Reimers, Jo, Bates, Korat, Wasserblat, Pereg — *Efficient Few-Shot Learning Without Prompts*, [arXiv:2209.11055](https://arxiv.org/abs/2209.11055)
- Yin, Hay, Roth — *Benchmarking Zero-shot Text Classification*, [EMNLP-IJCNLP 2019](https://aclanthology.org/D19-1404/) · [arXiv:1909.00161](https://arxiv.org/abs/1909.00161)
- Martin *et al.* — *CamemBERT: a Tasty French Language Model*, [ACL 2020](https://aclanthology.org/2020.acl-main.645/) · [arXiv:1911.03894](https://arxiv.org/abs/1911.03894)
- Antoun *et al.* — *CamemBERT 2.0: A Smarter French Language Model Aged to Perfection*, [arXiv:2411.08868](https://arxiv.org/abs/2411.08868)
- Le *et al.* — *FlauBERT: Unsupervised Language Model Pre-training for French*, [LREC 2020](https://arxiv.org/abs/1912.05372)
- Conneau *et al.* — *XNLI: Evaluating Cross-lingual Sentence Representations*, [EMNLP 2018](https://arxiv.org/abs/1809.05053)
- Dodge, Ilharco, Schwartz, Farhadi, Hajishirzi, Smith — *Fine-Tuning Pretrained Language Models: Weight Initializations, Data Orders, and Early Stopping*, [arXiv:2002.06305](https://arxiv.org/abs/2002.06305)
- Mosbach, Andriushchenko, Klakow — *On the Stability of Fine-tuning BERT*, [arXiv:2006.04884](https://arxiv.org/abs/2006.04884) (ICLR 2021)
- Szép *et al.* — *A Practical Guide to Fine-tuning Language Models with Limited Data*, [arXiv:2411.09539](https://arxiv.org/html/2411.09539v1)
- Giunchiglia, Lukasiewicz — *Coherent Hierarchical Multi-Label Classification Networks*, [NeurIPS 2020](https://arxiv.org/abs/2010.10151)
- Zhang, Zhou — *ML-KNN: A lazy learning approach to multi-label learning*, [Pattern Recognition 40(7), 2007](https://dl.acm.org/doi/10.1016/j.patcog.2006.12.019)
- Sechidis, Tsoumakas, Vlahavas — *On the Stratification of Multi-Label Data*, [ECML PKDD 2011](https://link.springer.com/chapter/10.1007/978-3-642-23808-6_10)
- Angelopoulos, Bates — *A Gentle Introduction to Conformal Prediction and Distribution-Free Uncertainty Quantification*, [arXiv:2107.07511](https://arxiv.org/abs/2107.07511)

**Documentation officielle**

- scikit-learn — [§ 1.12 Multiclass and multioutput](https://scikit-learn.org/stable/modules/multiclass.html) · [§ 3.3 Tuning the decision threshold](https://scikit-learn.org/stable/modules/classification_threshold.html) · [Probability calibration](https://scikit-learn.org/stable/modules/calibration.html) · [Feature extraction](https://scikit-learn.org/stable/modules/feature_extraction.html) · [Common pitfalls](https://scikit-learn.org/stable/common_pitfalls.html) · [`LogisticRegression`](https://scikit-learn.org/stable/modules/generated/sklearn.linear_model.LogisticRegression.html) · [`LinearSVC`](https://scikit-learn.org/stable/modules/generated/sklearn.svm.LinearSVC.html) · [`KNeighborsClassifier`](https://scikit-learn.org/stable/modules/generated/sklearn.neighbors.KNeighborsClassifier.html) · [`NearestCentroid`](https://scikit-learn.org/stable/modules/generated/sklearn.neighbors.NearestCentroid.html) · [`StratifiedKFold`](https://scikit-learn.org/stable/modules/generated/sklearn.model_selection.StratifiedKFold.html) · [`TunedThresholdClassifierCV`](https://scikit-learn.org/stable/modules/generated/sklearn.model_selection.TunedThresholdClassifierCV.html)
- HuggingFace — [`PreTrainedConfig` / `problem_type`](https://huggingface.co/docs/transformers/main_classes/configuration) · [pipeline `zero-shot-classification`](https://huggingface.co/docs/transformers/en/main_classes/pipelines) · [SetFit](https://huggingface.co/docs/setfit/en/index) · [SetFit multilabel](https://huggingface.co/docs/setfit/en/how_to/multilabel) · [SetFit `TrainingArguments`](https://huggingface.co/docs/setfit/en/reference/trainer)
- sentence-transformers — [Speeding up Inference](https://sbert.net/docs/sentence_transformer/usage/efficiency.html)
- PyTorch — [Reproducibility](https://docs.pytorch.org/docs/stable/notes/randomness.html)
- FastAPI — [Concurrency and async/await](https://fastapi.tiangolo.com/async/)
- MAPIE — [dépôt](https://github.com/scikit-learn-contrib/MAPIE) · [classification multi-label](https://mapie.readthedocs.io/en/v0.9.2/examples_multilabel_classification/1-quickstart/plot_tutorial_multilabel_classification.html)
- `iterative-stratification` — [dépôt](https://github.com/trent-b/iterative-stratification)

**Cartes de modèles**

[`almanach/camembert-base`](https://huggingface.co/almanach/camembert-base) · [`almanach/camembertav2-base`](https://huggingface.co/almanach/camembertav2-base) · [`OrdalieTech/Solon-embeddings-large-0.1`](https://huggingface.co/OrdalieTech/Solon-embeddings-large-0.1) · [`dangvantuan/sentence-camembert-large`](https://huggingface.co/dangvantuan/sentence-camembert-large) · [`intfloat/multilingual-e5-large`](https://huggingface.co/intfloat/multilingual-e5-large) · [`sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2`](https://huggingface.co/sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2) · [`sentence-transformers/paraphrase-multilingual-mpnet-base-v2`](https://huggingface.co/sentence-transformers/paraphrase-multilingual-mpnet-base-v2) · [`sentence-transformers/distiluse-base-multilingual-cased-v1`](https://huggingface.co/sentence-transformers/distiluse-base-multilingual-cased-v1) · [`cmarkea/distilcamembert-base-nli`](https://huggingface.co/cmarkea/distilcamembert-base-nli) · [`MoritzLaurer/mDeBERTa-v3-base-xnli-multilingual-nli-2mil7`](https://huggingface.co/MoritzLaurer/mDeBERTa-v3-base-xnli-multilingual-nli-2mil7) · [`morit/french_xlm_xnli`](https://huggingface.co/morit/french_xlm_xnli)

**Sources internes au dépôt**

[`CONTEXT.md`](../../CONTEXT.md) · [`docs/adr/0001`](../adr/0001-architecture-polyglotte-et-moteur-auto-heberge.md) · [`docs/spec/qualification.md`](../spec/qualification.md) § 5.3, 5.4, 5.8, 6 · [`corpus/README.md`](../../corpus/README.md) · [`corpus/demandes-rgpd.fr.jsonl`](../../corpus/demandes-rgpd.fr.jsonl) · [`src/sidecar/qualification_sidecar/lexicon.py`](../../src/sidecar/qualification_sidecar/lexicon.py) · [`src/sidecar/pyproject.toml`](../../src/sidecar/pyproject.toml)
