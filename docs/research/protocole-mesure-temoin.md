# Protocole de mesure d'un témoin

> Recherche du ticket [#45](https://github.com/AmauryTISSOT/microservice_rgpd/issues/45), carte [#42](https://github.com/AmauryTISSOT/microservice_rgpd/issues/42).
> **Ce document donne la matière d'un choix ; il ne le fait pas.** Le critère de pertinence — « à partir de quel chiffre le troisième témoin mérite-t-il d'entrer dans `/qualifications` ? » — appartient au ticket #48 et n'est pas fixé ici. Aucune valeur seuil n'est prescrite ci-dessous.

## Ce que la question demande

La carte #42 a acté trois choses que cette recherche prend pour données : le nouveau moteur est un **second témoin** qui ne contribue jamais à la `Qualification` ; la mesure est une **validation croisée stratifiée** sur les 120 exemples, tout réglage d'hyperparamètre **imbriqué** dans les plis ; on rend **un intervalle, jamais un chiffre unique**.

Reste ouvert ce que ce document instruit, en deux moitiés :

1. **Comment on écrit correctement le protocole d'échantillonnage** — stratification multi-label, nombre de plis, imbrication, intervalles, traitement des paires minimales.
2. **Ce qu'on mesure d'un témoin** — c'est-à-dire une **valeur d'alarme**, pas une exactitude. L'ADR-0001 le dit déjà sans le formaliser : le lexique a été retenu non pour son score mais parce que *« les erreurs du LLM portent toutes une confiance non haute, donc une relecture ciblée les capte, là où les erreurs du lexique sont silencieuses »* ([ADR-0001](../adr/0001-architecture-polyglotte-et-moteur-auto-heberge.md)). Mesurer un témoin, c'est mesurer sa capacité à rendre bruyantes les erreurs du verdict.

---

# Partie 0 — Ce que le corpus impose

Chiffres relevés directement sur [`corpus/demandes-rgpd.fr.jsonl`](../../corpus/demandes-rgpd.fr.jsonl), parce que plusieurs choix de protocole en découlent mécaniquement.

| Fait | Valeur | Conséquence pour le protocole |
| --- | --- | --- |
| Exemples | 120 | Cf. Partie 1 § 4 : ±7 points d'intervalle sur une métrique globale. |
| Étiquettes | 7 | `hors-perimetre` 30 · `effacement` 26 · `acces` 23 · `opposition` 20 · `rectification` 14 · `limitation` 14 · `portabilite` 13 |
| Rapport fréquence max/min | 30/13 ≈ **2,3** | Déséquilibre **modéré**. Micro et macro ne divergeront pas dramatiquement, mais les trois étiquettes rares seront très bruitées. |
| Cardinalité moyenne | **1,17** étiquette/exemple | 101 exemples mono-droit, 18 bi-droits, 1 tri-droit. Le corpus est **presque** multi-classe ; le multi-label ne joue que sur 19 exemples. |
| Combinaisons distinctes (*labelsets*) | **19**, dont **9 vues une seule fois** | Décisif : cf. Partie 1 § 1. |
| Ratio *labelsets* / exemples | 19/120 ≈ **0,16** | Régime intermédiaire où les deux sources primaires se contredisent (Partie 1 § 1). |
| Matrice d'étiquettes | 141 positifs sur 120 × 7 = 840 cellules ≈ **17 %** | Un classifieur qui ne prédit jamais rien obtient une *Hamming loss* de 0,17. |
| Paires minimales | 8 paires = **16 exemples**, soit 13,3 % du corpus | Cf. Partie 1 § 6. |
| Registres | `courant` 74 · `juridique` 25 · `familier` 13 · `maladroit` 8 | `maladroit` à 8 exemples : moins d'un par pli à k = 10. |

Contraintes héritées, non renégociables (ADR-0001) : auto-hébergement strict, GPU déjà pris par `qwen3:8b`, déterminisme. Elles pèsent sur le **coût en nombre d'ajustements** de la Partie 1 § 3 : tout protocole qui demanderait de rejouer le LLM à chaque pli est hors budget.

---

# Partie 1 — Le protocole d'échantillonnage

## 1. La stratification multi-label

### Pourquoi `StratifiedKFold` ne s'applique pas

Ce n'est pas une préférence, c'est un refus d'exécution. Le contrôle de type de scikit-learn n'autorise que `("binary", "multiclass")` et lève une exception sur `multilabel-indicator` ([`_split.py`](https://raw.githubusercontent.com/scikit-learn/scikit-learn/main/sklearn/model_selection/_split.py)) ; le paramètre `y` de [`StratifiedKFold`](https://scikit-learn.org/stable/modules/generated/sklearn.model_selection.StratifiedKFold.html) est déclaré `array-like of shape (n_samples,)`, donc unidimensionnel. La même classe émet par ailleurs l'avertissement `"The least populated class in y has only %d members, which is less than n_splits=%d."` et l'erreur dure `"n_splits=%d cannot be greater than the number of members in each class."` — deux garde-fous qu'aucune implémentation multi-label ne reproduit (voir plus bas).

### Les deux interprétations de « stratifier », et pourquoi la première est impossible ici

Sechidis, Tsoumakas & Vlahavas, *On the Stratification of Multi-Label Data*, ECML/PKDD 2011 ([PDF LPIS](http://lpis.csd.auth.gr/publications/sechidis-ecmlpkdd-2011.pdf)) posent l'alternative :

- **Stratification par *labelsets*** (*label powerset*) : les groupes sont les combinaisons distinctes d'étiquettes. Le papier note que leur nombre est borné par `min(m, 2^q)` et que la méthode devient impraticable dès que le ratio *labelsets*/exemples est élevé, « as most groups would consist of just a single example ».
- **Interprétation relâchée**, celle de leur algorithme : « the maintenance of the distribution of positive and negative examples of each label » — du **premier ordre**, chaque étiquette prise indépendamment.

**Sur notre corpus, la première est structurellement exclue** : 9 des 19 combinaisons n'ont qu'un seul exemple. Une combinaison à un exemple ne peut pas être répartie sur 5 plis ; elle sera absente de 4 plis quoi qu'on fasse. C'est exactement le cas de dégénérescence que Sechidis décrit.

### Stratification itérative (IS) — ce que fait l'algorithme

L'algorithme 1 du papier fonctionne ainsi :

- on calcule `c_j = |D|·r_j`, le nombre d'exemples désiré dans le pli *j*, et `c^i_j = |D^i|·r_j`, le nombre de **positifs de l'étiquette λ_i** désiré dans le pli *j* ;
- à chaque tour on choisit `l = argmin_i |D^i|` — **l'étiquette la plus rare encore à placer**, ex æquo tranchés au hasard ;
- on affecte un de ses exemples au pli `M = argmax_j c^l_j`, départage par `argmax_j c_j`, puis tirage aléatoire ;
- on décrémente les compteurs de toutes les étiquettes portées par l'exemple.

La priorité aux étiquettes rares est justifiée explicitement : *« if rare labels are not examined in priority, then they may be distributed in an undesired way, and this cannot be repaired subsequently. On the other hand with frequent labels, we have the chance later on to modify the current distribution towards the desired. »*

**Ce qu'IS optimise, et ce qu'il sacrifie.** Il optimise **LD** (*Labels Distribution*, l'écart moyen entre le ratio positifs/négatifs par pli et celui du corpus) au détriment d'**ED** (*Examples Distribution*, l'écart entre la taille effective d'un pli et sa taille désirée) : *« In terms of ED, the labelsets-based and the random sampling methods achieve the best performance in all datasets, while iterative stratification is much worse. »* **Les plis d'IS n'ont pas exactement la même taille** — c'est un compromis assumé, et il faut le savoir avant de s'étonner d'un pli de 22 et d'un pli de 26.

### Ce qu'il fait des étiquettes à 13-14 exemples

Le papier introduit deux mesures dédiées : **FZ** (nombre de plis contenant au moins une étiquette sans aucun positif) et **FLZ** (nombre de couples pli-étiquette sans positif). Résultat : *« iterative stratification produces the smallest value for FZ and FLZ in all datasets »*. La motivation est directe : sans positif dans un pli, rappel et précision sont `0/0`, donc F1, AUC et *average precision* sont **indéfinis**, et leurs versions macro le deviennent aussi.

Limite dure reconnue : *« All methods fail to produce subsets with positive examples for all labels in the datasets Corel5k, Enron, Medical and Genbase, which contain labels characterized by absolute rarity […] the minimum number of examples per label is just one. »*

**Notre corpus n'est pas dans ce régime.** Le minimum par étiquette est 13 (`portabilite`) ≥ k = 5 : `FZ = FLZ = 0` est atteignable, avec 2 à 3 positifs par pli pour les trois étiquettes rares (13 = 3+3+3+2+2 ; 14 = 3+3+3+3+2). En revanche, ce n'est **pas** vrai des *paires* d'étiquettes : 10 des 19 combinaisons vues 1 à 4 fois seront absentes de plusieurs plis, quoi qu'on fasse.

### Stratification itérative de second ordre (SOIS)

Szymański & Kajdanowicz, *A Network Perspective on Stratification of Multi-Label Data*, 2017 ([arXiv:1704.08756](https://arxiv.org/abs/1704.08756) · [PDF](https://arxiv.org/pdf/1704.08756)) généralisent : au lieu de stratifier sur l'ensemble `L` des étiquettes, on stratifie sur `Λ = {{λ_i, λ_j} : ∃(x,Y)∈D, {λ_i,λ_j} ⊂ Y}`, l'ensemble des **paires d'étiquettes co-occurrentes** (i = j inclus). L'algorithme est le même, appliqué aux paires ; une fois toutes les paires distribuées, *« the same algorithm is employed to distribute labels from L in a similar manner - which is the graceful fallback to the IS algorithm »*.

Nouvelles mesures : **LPD** (*Label Pair Distribution*) et **FLPZ** (couples pli-paire sans positif), cette dernière avec une correction honnête — on ne compte que les cas dépassant *« the inevitable minimum value corresponding to the number of folds minus the number of available samples with a label pair »*.

Verdict des auteurs : *« The Iterative Stratification approach ranks best in terms of FLZ and LD […] but underperforms when it comes to positive evidence for label pairs »* ; *« SOIS is clearly a better choice as the gain in stability of label pair measures is larger than the loss in FLZ. »* Ils condamnent frontalement la stratification par *labelset* : elle *« should only be used in the case when there is little to no imbalance of positive evidence distribution among labelsets, in practice - never »*.

### ⚠️ Le point où les deux sources primaires se contredisent

Sechidis observe que *« the difference in LD between iterative stratification and the labelsets-based method grows with the ratio of labelsets over examples […] when this ratio is small (e.g. ≤ 0.1), the LD of the labelset-based method is close to that of iterative stratification »* — et sur Scene, Yeast et TMC2007 (ratios 0,01–0,05), la méthode par *labelsets* **bat** IS sur LD. Notre ratio est 19/120 ≈ 0,16 : juste au-dessus du seuil que Sechidis donne comme favorable au *labelset*, et dans un régime que Szymański qualifie de « jamais en pratique ».

Les deux ne se recoupent pas. Leur **point commun**, en revanche, est qu'IS et SOIS sont sûrs dans les deux régimes. Et le fait local tranche par ailleurs : 9 combinaisons à un exemple rendent le *labelset* inapplicable ici, quel que soit le ratio.

Élément à ne pas perdre de vue : avec une cardinalité moyenne de **1,17**, le gain de SOIS sur IS porte sur les **19 exemples multi-droits seulement**. L'écart entre les deux sera vraisemblablement faible. Ce qui rend l'arbitrage instrumentable plutôt que doctrinal : **rapporter LD, ED, FZ/FLZ et FLPZ des plis effectivement obtenus** est un diagnostic reproductible, pas une opinion — et le notebook peut le faire pour les deux réglages.

### Implémentations de référence

| Paquet | Classe | Ordre | Notes |
| --- | --- | --- | --- |
| **scikit-multilearn** | `skmultilearn.model_selection.IterativeStratification(n_splits, order, sample_distribution_per_fold, random_state)` | `order=1` → Sechidis ; `order≥2` → SOIS | La **seule** implémentation de SOIS disponible. Fournit aussi `iterative_train_test_split(X, y, test_size)` = `IterativeStratification(n_splits=2, order=2)`. |
| **iterative-stratification** (trent-b) | `MultilabelStratifiedKFold`, `RepeatedMultilabelStratifiedKFold`, `MultilabelStratifiedShuffleSplit` | **premier ordre uniquement** | API scikit-learn complète, compatible `cross_val_score` / `cross_val_predict`. |

Lecture du [code source de scikit-multilearn](https://raw.githubusercontent.com/scikit-multilearn/scikit-multilearn/master/skmultilearn/model_selection/iterative_stratification.py) : `_get_most_desired_combination` implémente la priorité aux combinaisons rares, `_fold_tie_break` implémente exactement la règle de départage de Sechidis (`argmax` sur les exemples désirés, puis `np.random.choice`). Les deux papiers sont cités en BibTeX dans le *docstring*.

Deux avertissements pratiques :

- **`MultilabelStratifiedKFold` n'existe pas dans scikit-multilearn** — c'est `IterativeStratification`. Le nom `MultilabelStratifiedKFold` appartient à l'autre paquet. La confusion est fréquente.
- Le [*docstring* de `iterative-stratification`](https://raw.githubusercontent.com/trent-b/iterative-stratification/master/iterstrat/ml_stratifiers.py) précise : *« Unlike StratifiedKFold that only uses random_state when shuffle == True, this multilabel implementation always uses the random_state since the iterative stratification algorithm breaks ties randomly »* — **conséquence directe sur le déterminisme exigé par l'ADR-0001 : `random_state` doit être fixé et consigné, sans quoi le notebook n'est pas reproductible.** Le même *docstring* confirme le sacrifice sur ED : *« Train and test sizes may be slightly different in each fold. »*
- **Aucune des deux implémentations n'émet d'avertissement de sous-peuplement** analogue à celui de `StratifiedKFold`. La surveillance de FZ/FLZ est entièrement à la charge de l'appelant.

## 2. Combien de plis

### Ce que dit la littérature primaire

**Kohavi, IJCAI 1995** ([PDF des actes](https://www.ijcai.org/Proceedings/95-2/Papers/016.pdf)), à partir de plus d'un demi-million d'exécutions de C4.5 et de bayésien naïf :

- Biais : *« k-fold cross-validation is pessimistically biased, especially for two and five folds […] Most of the estimates are reasonably good at 10 folds and at 20 folds they are almost unbiased. »*
- *Leave-one-out* : *« leave-one-out is almost unbiased, but it has high variance leading to unreliable estimates. »*
- Stratification : *« Stratification reduces the variance slightly, and thus seems to be uniformly better than cross-validation, both for bias and variance. »*
- Synthèse : *« As k decreases (2-5) and the sample sizes get smaller, there is variance due to the instability of the training sets themselves leading to an increase in variance […] In these situations, stratification seems to help, but **repeated runs may be a better approach**. »*
- Recommandation : *« We recommend using stratified ten fold cross-validation for model selection »*, y compris quand la puissance de calcul permettrait davantage.

**scikit-learn**, [*Cross-validation*](https://scikit-learn.org/stable/modules/cross_validation.html), est plus direct : *« As a general rule, most authors and empirical evidence suggest that 5 or 10-fold cross validation should be preferred to LOO. »* Sur le LOO : *« since n − 1 of the n samples are used to build each model, models constructed from folds are virtually identical to each other and to the model built from the entire training set. »* La doc pointe aussi `RepeatedKFold` / `RepeatedStratifiedKFold`.

**Bengio & Grandvalet, JMLR 5 (2004)** ([PDF](https://www.jmlr.org/papers/volume5/grandvalet04a/grandvalet04a.pdf)) sont explicitement **non prescriptifs** : *« there is no general trend either in variance or decomposition of the variance in its σ², ω and γ components. The minimum variance can be reached for K = n or for an intermediate value of K. »* Coïncidence utile : leur **figure 5** trace précisément ces contributions en fonction de K **pour n = 120** — notre taille exacte. Le message à cette taille est qu'il n'existe pas de K universellement optimal.

### ⚠️ Ce que la littérature ne tranche pas, et ce que le corpus tranche

La littérature primaire donne : 10 plis comme défaut recommandé (Kohavi, scikit-learn), 5 explicitement acceptable (scikit-learn), aucun argument théorique décisif (Bengio & Grandvalet). **À n = 120, le choix se décide sur un fait local plutôt que sur la théorie :**

| k | Taille du pli de test | Positifs de `portabilite` (13) par pli de test |
| --- | --- | --- |
| 5 | 24 | 2 à 3 |
| 10 | 12 | 1 à 2 |
| LOO (120) | 1 | 0 ou 1 |

À k = 10, un F1 par étiquette calculé sur 1 positif est du bruit pur, et la garantie `FLZ = 0` de Sechidis devient fragile. À k = 5, le corpus reste au-dessus du seuil de dégénérescence. **La répétition** — 5 plis répétés R fois avec des germes différents — est le levier que Kohavi désigne (*« repeated runs may be a better approach »*) et que les deux bibliothèques offrent (`RepeatedStratifiedKFold`, `RepeatedMultilabelStratifiedKFold`). Elle est corroborée par le banc d'essai cité au § 4, qui recommande explicitement K = 5 avec au moins 25 répétitions pour n ≤ 100.

## 3. La validation croisée imbriquée

### Sa forme exacte

Deux boucles, deux rôles disjoints ([scikit-learn, *Nested versus non-nested cross-validation*](https://scikit-learn.org/stable/auto_examples/model_selection/plot_nested_cross_validation_iris.html)) :

- boucle **interne** — `GridSearchCV(estimator, p_grid, cv=inner_cv)` : **sélectionne** les hyperparamètres, ne rapporte rien ;
- boucle **externe** — `cross_val_score(clf, X, y, cv=outer_cv)` : **estime** la performance, ne sélectionne rien.

*« Nested CV effectively uses a series of train/validation/test set splits. »* Sans imbrication : *« Choosing the parameters that maximize non-nested CV biases the model to the dataset, yielding an overly-optimistic score. »*

### L'ampleur du biais évité

**Cawley & Talbot, JMLR 11 (2010)** ([PDF](https://www.jmlr.org/papers/volume11/cawley10a/cawley10a.pdf)) — le mécanisme : *« a non-negligible variance introduces the potential for over-fitting in model selection as well as in training the model »*. L'ampleur : *« The scale of the bias observed on some data sets is much larger than the difference in performance between learning algorithms, and so one could easily draw incorrect inferences based on the results obtained. »* Prescription : *« model selection must be treated as an integral part of the model fitting process and performed afresh every time a model is fitted to a new sample of data »*, ce qui appelle *« more rigorous and computationally intensive protocols, such as nested cross-validation or "double cross" (Stone, 1974) »*.

**Portée à retenir pour la carte #42** : la conclusion *« applies to any model selection practice involving the optimisation of a model selection criterion evaluated over a finite sample of data »*. Ce n'est donc pas réservé aux hyperparamètres numériques. Le choix d'un **seuil de décision**, d'une **famille de modèle**, d'un **vectoriseur**, ou même le choix « lexique + nouveau contre lexique seul » s'il est fait en regardant les scores, tombe sous la même règle. C'est le piège le plus facile à tendre soi-même dans un notebook.

**Varma & Simon, BMC Bioinformatics 7:91 (2006)** ([texte intégral](https://pmc.ncbi.nlm.nih.gov/articles/PMC1397873/)) donnent le chiffre qui marque : sur des données **simulées sans aucun signal** (erreur vraie = 50 %), à n = 40, la validation croisée « optimisée » rapporte une erreur moyenne de **37,8 %** (*Shrunken Centroids*) et **41,7 %** (SVM) ; 38 % des jeux nuls donnaient au SVM une erreur estimée nettement sous 50 %. Conclusion : *« Using CV to compute an error estimate for a classifier that has itself been tuned using CV gives a significantly biased estimate of the true error »*, là où la CV imbriquée *« provides an almost unbiased estimate »*. Le biais **croît quand n décroît** ; à n = 120 il est plus faible qu'à n = 40, mais reste du même ordre de grandeur que les écarts qu'on cherche à mesurer.

### Le coût, en nombre d'ajustements

Avec `K_ext` plis externes, `K_int` plis internes, `|Λ|` configurations candidates, `R` répétitions :

```
N_fits = R · K_ext · ( K_int · |Λ| + 1 )   [ + 1 si réajustement final sur tout D ]
```

Le `+1` interne est le réajustement de `GridSearchCV(refit=True)` sur le pli d'entraînement externe. Une CV **non** imbriquée coûte `K·|Λ| + 1` : le **surcoût de l'imbrication est un facteur ≈ `K_ext`**, pas `K_ext · K_int`. C'est moins cher que la réputation ne le dit.

| Protocole | Ajustements |
| --- | --- |
| 5 × 5, \|Λ\| = 6, R = 1 | 156 |
| 5 × 5, \|Λ\| = 6, R = 10 | 1 560 |
| 5 × 5, \|Λ\| = 6, R = 25 | 3 900 |
| 10 × 5, \|Λ\| = 6, R = 1 | 310 |
| 5 plis simple (biaisé), \|Λ\| = 6 | 31 |

Rapporté aux contraintes de l'ADR-0001 : ces ajustements portent sur le **classifieur non génératif**, sur CPU, et ne touchent pas le GPU. En revanche, un protocole qui exigerait de rejouer `qwen3:8b` à chaque pli serait hors budget — les avis du LLM doivent être **calculés une fois** et mis en cache, puis réutilisés dans toutes les répétitions. C'est licite précisément parce que le LLM n'est pas la chose entraînée.

### La contrainte de taille qui en résulte

En 5 × 5, l'entraînement interne porte sur 120 × (4/5) × (4/5) ≈ **77 exemples**, la validation interne sur ≈ 19, le test externe sur 24. À ce régime, `portabilite` (13) fournit environ **2 positifs dans le pli de validation interne**. Fait mécanique, pas opinion : **la boucle interne ne peut pas départager des configurations sur le macro-F1**, qui est dominé par les étiquettes rares. La métrique de sélection interne doit être agrégée (micro-F1, *Hamming*) ; la métrique de rapport externe peut, elle, être macro.

## 4. Rendre un intervalle plutôt qu'un chiffre

### Pourquoi l'écart-type entre plis n'est pas un intervalle de confiance

C'est le point le plus contre-intuitif du protocole, et il a un théorème.

**Bengio & Grandvalet, théorème 6** : *« There exists no universally unbiased estimator of Var[μ̂]. »* La mécanique : la matrice de covariance des erreurs de test n'a que **trois** valeurs distinctes — `σ²` (variance des erreurs de test), `ω` (covariance intra-pli), `γ` (covariance inter-plis due au chevauchement des ensembles d'entraînement). *« The variance of the cross-validation estimator is a linear combination of three moments. »* L'estimateur naïf ignore `ω` et `γ` ; il *« grossly underestimate[s] variance »*. Les auteurs concluent : *« in very simple cases, the bias incurred by ignoring the dependencies between test errors will be of the order of the variance itself. […] the assessment of the significance of observed differences in cross-validation scores should be treated with much caution. »* Le LOO n'y échappe pas.

**Quantification empirique moderne** — Bates, Hastie & Tibshirani, *Cross-validation: what does it estimate and how well does it do it?* ([arXiv:2104.00673](https://arxiv.org/pdf/2104.00673)) : *« the standard confidence intervals for prediction error derived from cross-validation may have coverage far below the desired level. Because each data point is used for both training and testing, there are correlations among the measured accuracies for each fold, and so the usual estimate of variance is too small. »* Chiffre : *« intervals with desired miscoverage of 10% give **31 % miscoverage** in our simulation. The intervals need to be made larger by a factor of about **1,6** to obtain coverage at the desired level. »*

Le même papier soulève un second point rarement retenu, et qui concerne directement la lecture du notebook : *« the estimand of CV is not the accuracy of the model fit on the data at hand, but is instead the average accuracy over many hypothetical data sets. »* La validation croisée n'estime pas la performance du modèle qu'on déploiera ; elle estime la performance moyenne de la **procédure**.

### Les candidats, et ce qu'ils valent à n = 120

**Intervalles binomiaux sur les 120 prédictions hors-pli.** Disponibles en source primaire :

- [`scipy.stats.binomtest(...).proportion_ci(confidence_level, method)`](https://docs.scipy.org/doc/scipy/reference/generated/scipy.stats._result_classes.BinomTestResult.proportion_ci.html) — `'exact'` = Clopper-Pearson (Clopper & Pearson, *Biometrika* 26(4), 1934), `'wilson'` (Wilson 1927 ; Newcombe, *Stat. Med.* 17, 1998), `'wilsoncc'` avec correction de continuité.
- [`statsmodels.stats.proportion.proportion_confint`](https://www.statsmodels.org/stable/generated/statsmodels.stats.proportion.proportion_confint.html) — `normal`, `agresti_coull`, `beta` (= Clopper-Pearson), `wilson`, `jeffreys`, `binom_test`. La doc avertit : *« Beta, the Clopper-Pearson exact interval has coverage at least 1-alpha, but is in general conservative »*. Référence citée : Brown, Cai & DasGupta, *Interval Estimation for a Binomial Proportion*, Statistical Science 16(2), 2001.

Largeurs effectives à nos tailles (calculs par les formules de Wilson et Clopper-Pearson, à titre illustratif) :

| Succès / n | Proportion | Wilson 95 % | Clopper-Pearson 95 % |
| --- | --- | --- | --- |
| 100/120 | 0,833 | [0,757 ; 0,889] | [0,754 ; 0,895] |
| 96/120 | 0,800 | [0,720 ; 0,862] | [0,717 ; 0,867] |
| 90/120 | 0,750 | [0,666 ; 0,819] | [0,663 ; 0,825] |
| 19/23 (`acces`) | 0,826 | [0,629 ; 0,930] | [0,612 ; 0,950] |
| 12/14 (`rectification`) | 0,857 | [0,601 ; 0,960] | [0,572 ; 0,982] |
| 11/13 (`portabilite`) | 0,846 | [0,578 ; 0,957] | [0,546 ; 0,981] |

**Lecture directe, et c'est le chiffre le plus utile de cette partie : ±7 points sur une métrique globale à n = 120, et ±20 à 25 points sur une étiquette à 13-14 exemples.** Aucune différence inférieure à ~13 points sur une métrique globale, ni à ~40 points sur une étiquette rare, n'est distinguable du bruit d'échantillonnage sur ce corpus. La carte #42 avait raison de refuser le chiffre unique ; l'ordre de grandeur du refus est celui-là.

**Amorçage (*bootstrap*) .632 / .632+.** Kohavi documente son comportement : *« Bootstrap has low variance but extremely large bias on some problems »* — jusqu'à 9,8 points de biais mesurés sur `vehicle` avec C4.5. ⚠️ Le papier original d'Efron & Tibshirani (JASA 92(438), 1997) est derrière un mur payant qui a refusé l'accès ; aucune de ses formules n'est citée ici de première main.

**Tests de permutation.** [scikit-learn, `permutation_test_score`](https://scikit-learn.org/stable/modules/cross_validation.html) : *« generates a null distribution by calculating n_permutations different permutations of the data. In each permutation the target values are randomly shuffled, thereby removing any dependency between the features and the targets. »* L'hypothèse nulle testée est *« The estimator fails to leverage any statistical dependency between the features and the targets to make correct predictions on left-out data »*. La formule réelle du [code](https://raw.githubusercontent.com/scikit-learn/scikit-learn/main/sklearn/model_selection/_validation.py) porte la correction `+1` que la prose n'explicite pas toujours :

```python
pvalue = (np.sum(permutation_scores >= score) + 1.0) / (n_permutations + 1)
```

Conséquence : le *p* minimal atteignable est `1/(n_permutations+1)` — 1 000 permutations plafonnent à *p* ≈ 0,001.

**Le banc d'essai le plus directement applicable.** *Constructing Confidence Intervals for "the" Generalization Error — a Comprehensive Benchmark Study* ([arXiv:2409.18836](https://arxiv.org/pdf/2409.18836)) compare treize méthodes d'intervalle empiriquement, et recommande verbatim :

> *« For small data (up to n = 100): Nested CV with at least 25 outer repetitions and K = 5, or Conservative-Z with 25 outer repetitions and at least K = 10. »*

Ils notent au passage que *« The CV Wald method provided consistently small CIs »* — c'est-à-dire que l'intervalle naïf (moyenne ± t·sd/√k sur les plis) est systématiquement trop étroit, exactement le point de Bengio & Grandvalet.

### Comparer deux systèmes : les tests appariés, et celui qu'il ne faut pas utiliser

**Dietterich, Neural Computation 10(7), 1998** ([PDF, miroir SCI2S](https://sci2s.ugr.es/keel/pdf/algorithm/articulo/dietterich1998.pdf)) — résumé verbatim :

> *« Two widely-used statistical tests are shown to have high probability of Type I error in certain situations and **should never be used**. These tests are (a) a test for the difference of two proportions and (b) a paired-differences t test based on taking several random train/test splits. A third test, a paired-differences t test based on 10-fold cross-validation, exhibits somewhat elevated probability of Type I error. A fourth test, **McNemar's test**, is shown to have low Type I error. […] **For algorithms that can be executed only once, McNemar's test is the only test with acceptable Type I error. For algorithms that can be executed ten times, the 5x2cv test is recommended.** »*

Le mécanisme, § 3.4 : *« In a 10-fold cross-validation, each pair of training sets shares 80 % of the examples. This overlap may prevent this statistical test from obtaining a good estimate of the amount of variation that would be observed if each training set were completely independent. »*

**Conséquence pour la carte #42** : les comparaisons que la décision 3 exige — « lexique + nouveau contre lexique seul » et « nouveau seul contre lexique seul » — portent sur les mêmes 120 exemples avec des prédictions appariées. **McNemar est le test que Dietterich désigne** pour ce cas, d'autant que le verdict LLM est coûteux et n'est joué qu'une fois. Un t apparié sur les plis est explicitement déconseillé.

### Synthèse des trois nombres à ne pas confondre

1. **Intervalle binomial (Wilson ou Clopper-Pearson) sur les 120 prédictions hors-pli** — dit l'incertitude d'échantillonnage **du corpus**. Exact, calculable, honnête sur ce qu'il couvre. Ne capture pas la variabilité due au choix de l'ensemble d'entraînement.
2. **CV imbriquée répétée (K = 5, ≥ 25 répétitions)** — la seule recommandation explicite trouvée pour un intervalle sur l'erreur de généralisation à cette taille d'échantillon.
3. **« Moyenne ± écart-type entre plis » — jamais présenté comme un intervalle de confiance.** Théorème 6 de Bengio & Grandvalet ; 31 % de non-couverture pour un nominal de 10 % chez Bates et al. Si on le rapporte, l'appeler **« dispersion inter-plis »**.

## 5. Les métriques multi-label : micro, macro, et les pièges du corpus

[Documentation scikit-learn, *Metrics and scoring*](https://scikit-learn.org/stable/modules/model_evaluation.html) :

| Moyenne | Formule | Ce qu'elle fait |
| --- | --- | --- |
| `macro` | `(1/|L|) Σ_l P(y_l, ŷ_l)` | poids égal à chaque étiquette |
| `micro` | `P(y, ŷ)` sur l'union aplatie | *« gives each sample-class pair an equal contribution »* ; somme les numérateurs et dénominateurs avant de diviser |
| `weighted` | `(1/Σ_l |y_l|) Σ_l |y_l| · P(y_l, ŷ_l)` | pondéré par le support |
| `samples` | `(1/|S|) Σ_s P(y_s, ŷ_s)` | *« applies only to multilabel problems »* — moyenne **par exemple** |

La doc ajoute : *« Micro-averaging may be preferred in multilabel settings, including multiclass classification where a majority class is to be ignored »*. ⚠️ **La réserve ne s'applique pas à nous** : `hors-perimetre` (30) est bien une classe majoritaire, mais c'est aussi celle où *« un faux positif coûte le plus cher à l'opérateur humain »* ([`corpus/README.md`](../../corpus/README.md)). On ne veut pas l'ignorer.

Autres définitions officielles :

- **Exactitude par sous-ensemble** (*subset accuracy*, `accuracy_score` en multi-label) : *« If the entire set of predicted labels for a sample strictly match with the true set of labels, then the subset accuracy is 1.0; otherwise it is 0.0. »*
- ***Hamming loss*** : `1/(n_samples · n_labels) · Σ_i Σ_j 1(ŷ_ij ≠ y_ij)`. La doc note que *« predicting a proper subset or superset of the true labels will give a Hamming loss between zero and one, exclusive »* — elle crédite les prédictions partiellement correctes, là où l'exactitude par sous-ensemble les compte à zéro.

### Le piège de nomenclature du macro-F1

Opitz & Burst, *Macro F1 and Macro F1* ([arXiv:1911.03347](https://arxiv.org/pdf/1911.03347)) montrent que **deux formules incompatibles circulent sous le même nom** :

- ***Averaged F1*** : `F1 = (1/n) Σ_x 2P_x R_x / (P_x + R_x)` — moyenne arithmétique de moyennes harmoniques ;
- ***F1 of averages*** : `F1 = 2 P̄ R̄ / (P̄ + R̄)` — moyenne harmonique de moyennes arithmétiques.

*« The difference in outcome of the two computations can be as high as 0.5 »* ; RMSD mesurée de 0,13 sur 1 000 tâches binaires simulées, corrélation de Pearson ρ = 0,72 seulement, et *« allows for different classifier rankings »*. Verdict : *« one metric (F1 of averages) is overly "benevolent" towards heavily biased classifiers and can yield misleadingly high evaluation scores. This is likely to happen when the data set is imbalanced. […] we recommend evaluating classifiers with F1 (the arithmetic mean over individual F1 scores). At the very least, researchers should indicate which formula they are using. »*

`sklearn.metrics.f1_score(average='macro')` implémente bien la variante recommandée.

### Ce que le corpus impose

- Le rapport 30/13 ≈ 2,3 est un déséquilibre **modéré** : micro et macro ne divergeront pas dramatiquement. Mais macro reste ~4× plus bruité sur `portabilite`, `rectification` et `limitation` (±20-25 points, § 4).
- ***Hamming loss* à ne jamais rapporter seule** : avec 141 positifs sur 840 cellules, un classifieur qui ne prédit **jamais rien** obtient 0,17.
- **Exactitude par sous-ensemble** : la seule qui pénalise correctement une erreur sur les 19 exemples multi-droits, mais brutale et à variance maximale.
- ⚠️ **Une contrainte propre à ce schéma qu'aucune métrique de la littérature ne détecte** : `hors-perimetre` est **exclusif**. Une prédiction `{hors-perimetre, acces}` est structurellement invalide, pas « à moitié fausse » — micro-F1 et *Hamming* la comptent pourtant comme un demi-succès. Un **taux de violation de contrainte** (proportion de prédictions mêlant `hors-perimetre` à un droit) est un diagnostic à part entière, à ajouter au tableau de bord. C'est le même invariant que celui qui rend l'union des deux avis indéfinissable (§ 6.1 de la spec).

**Matière du choix** : rapporter le quatuor **macro-F1 (formule nommée), micro-F1, exactitude par sous-ensemble, *Hamming loss***, plus le **F1 par étiquette avec son n et son intervalle de Wilson**, et **déclarer laquelle sert à la sélection en boucle interne** (§ 3 : agrégée, pas macro).

## 6. Les huit paires minimales

Les 8 paires du corpus ([`corpus/README.md`](../../corpus/README.md)) sont exactement ce que la littérature appelle des ***contrast sets*** ou des ***counterfactually-augmented data***.

### Ce qu'elles sont, dans la littérature

Gardner et al., *Evaluating Models' Local Decision Boundaries via Contrast Sets*, Findings of EMNLP 2020 ([PDF](https://arxiv.org/pdf/2004.02709)) : *« A contrast set C(x) is any sample of points from a local decision boundary around x […] consists of inputs x′ that are similar to x according to some distance function d. Typically these points are sampled such that y′ ≠ y. »*

Kaushik, Hovy & Lipton, ICLR 2020 ([PDF](https://arxiv.org/pdf/1909.12434)) posent le cadre causal — *« spurious associations owe to confounding »* — et le protocole d'annotation : éditer le document pour que *« (a) the counterfactual label applies; (b) the document remains coherent; and (c) **no unnecessary modifications are made** »*. C'est mot pour mot ce que fait `edg-13` / `edg-14` : le même texte, à la valeur de remplacement près.

### La métrique dédiée

Gardner et al. définissent la ***contrast consistency*** : *« whether it makes correct predictions ŷ on every element in the set: all({ŷ = y′ ∀(x′,y′) ∈ C(x)}) »*. **Une paire compte pour 1 si et seulement si ses deux membres sont correctement classés** — pas 0,5 si un seul l'est. C'est précisément ce qui distingue « le modèle connaît la distinction » de « le modèle a un biais qui tombe juste une fois sur deux ».

Ils constatent que *« models struggle […] worse on our contrast sets than on the original test sets, especially when evaluating consistency »*.

### Grouper dans le même pli ? — oui, et il y a une raison mécanique

La [documentation scikit-learn](https://scikit-learn.org/stable/modules/cross_validation.html) pose le principe général : *« The i.i.d. assumption is broken if the underlying generative process yields groups of dependent samples […] we need to ensure that all the samples in the validation fold come from groups that are not represented at all in the paired training fold. »*

Kaushik et al. appliquent effectivement cette règle : *« Following revision by the crowd workers, we partition this dataset into train/validation/test splits »* — l'original **et** sa révision restent du même côté. Et ils montrent pourquoi ça compte : *« classifiers trained on original IMDb reviews fail on counterfactually-revised data and vice versa »*, avec un Bi-LSTM qui tombe de 79,3 % à 55,7 %.

Si un membre d'une paire est en entraînement et l'autre en test, le modèle a vu un exemple quasi identique portant l'étiquette **opposée**. Le score qui en sort n'est pas interprétable : soit surestimé par mémorisation de surface, soit sous-estimé par contradiction d'étiquettes. Dans les deux cas, il ne mesure plus rien.

### ⚠️ Un trou d'outillage réel

**Aucune bibliothèque n'offre à la fois la stratification multi-label et la contrainte de groupes :**

- [`StratifiedGroupKFold`](https://scikit-learn.org/stable/modules/generated/sklearn.model_selection.StratifiedGroupKFold.html) préserve la stratification *« given the constraint of non-overlapping groups between splits »* — mais son `y` est *« binary or multiclass »* : **pas de multi-label** ;
- `IterativeStratification._iter_test_indices(self, X, y, groups)` accepte `groups` par conformité d'API, mais l'algorithme ne l'utilise nulle part ;
- `iterative-stratification` n'a aucune notion de groupe.

**La contrainte doit être codée à la main.** Recette mécaniquement correcte compte tenu des chiffres : traiter chaque paire comme une unité lors de l'appel au stratificateur — 120 − 8 = **112 unités** — puis ré-injecter le second membre dans le pli de son jumeau. [`GroupKFold`](https://scikit-learn.org/stable/modules/generated/sklearn.model_selection.GroupKFold.html) exige que *« the number of distinct groups has to be at least equal to the number of folds »* : 8 paires ≥ 5 plis, satisfait à **k = 5**, **pas à k = 10**. C'est un second argument, indépendant du § 2, en faveur de k = 5.

### Rapporter à part ? — oui, mais en brut

Gardner et al. traitent la *contrast consistency* comme une métrique **séparée** du score i.i.d. et ne mélangent jamais les deux : *« we do not provide i.i.d. contrast sets at training time, which could provide additional artificially simple decision boundaries to a model »*. Diluer les 16 exemples dans le micro-F1 global efface exactement le signal qu'ils ont été construits pour produire.

⚠️ **Et il ne faut pas les transformer en pourcentage.** 8 paires, c'est n = 8. Un intervalle de Wilson à 95 % sur 6/8 s'étend d'environ 0,37 à 0,94. **Aucune comparaison entre systèmes n'est concluante sur 8 paires.** Rapporter « k/8 paires résolues » en brut, et lire la liste : ces paires sont un **diagnostic qualitatif** — *quelles distinctions le moteur rate* — bien plus qu'une mesure. C'est d'ailleurs leur usage naturel dans une carte qui cherche à savoir *où* un témoin ajoute de la vue, et pas seulement *combien*.

---

# Partie 2 — Ce qu'on mesure d'un témoin

C'est la moitié la moins évidente, et la plus importante. Un témoin ne rend pas le verdict ; le juger à son exactitude serait le juger sur ce qu'on ne lui demande pas. L'ADR-0001 a déjà tranché ce point en creux quand il a écarté le lexique seul : *« ses erreurs sont **silencieuses** ; pour une aide à la décision, ne pas savoir dire qu'on doute est plus grave qu'un taux d'erreur. »* Ce qu'on mesure d'un témoin, c'est donc **sa capacité à rendre bruyantes les erreurs du verdict**.

## 7. La détection d'erreur par désaccord

### 7.1 Le résultat qui fonde l'architecture

Le désaccord entre deux moteurs majore le taux d'erreur. Ce n'est pas une intuition, c'est un théorème.

**Dasgupta, Littman & McAllester, *PAC Generalization Bounds for Co-training*, NIPS 2001** ([PDF](https://cseweb.ucsd.edu/~dasgupta/papers/cotrain.pdf)). Le cadre est celui de règles **partielles** `h_i : X_i → {1,…,k,⊥}`, où `⊥` signifie « pas d'opinion » ; *« the error of a partial rule is the probability that the rule is incorrect **given that it has an opinion** »*. L'intuition est donnée verbatim :

> *« the data-processing inequality of information theory: I(h₁;y) ≥ I(h₁;h₂). Any mutual information between h₁ and h₂ must be mediated through y. In particular, if h₁ and h₂ agree to a large extent, then they must reveal a lot about y. »*

Le **théorème 1** borne l'erreur vraie par le désaccord **observé sur des données non étiquetées**, divisé par une marge d'accord `γ_i` :

```
P(h₁ ≠ i | f(y) = i, h₁ ≠ ⊥)  ≤  [ P̂(h₁ ≠ i | h₂ = i, h₁ ≠ ⊥) + ε_i ] / γ_i
```

**Erreur vraie ≤ désaccord observé / marge d'accord.** La borne **devient vide dès que `γ_i ≤ 0`**.

Deux conséquences directes pour la carte #42 :

- **Le désaccord se mesure sans étiquettes.** C'est la propriété qui rendrait le `ReviewSignal` mesurable en production, au-delà des 120 exemples annotés. Cette recherche ne le recommande pas — c'est une question de carte, pas de protocole — mais le fait mérite d'être su.
- **Un témoin qui s'abstient massivement est un bon design, et la théorie le dit.** Les auteurs montrent que même si le taux d'erreur global d'une règle est statistiquement indiscernable de 1/2, *« theorem 1 can still establish that the false positive and false negative rate of the partial rule h₁ is near zero. »* Un lexique qui ne se prononce que quand il est sûr, et `⊥` sinon, n'est pas un lexique faible : c'est un témoin bien conçu. Notre lexique n'a pas ce comportement aujourd'hui — il rend toujours un avis — mais un troisième moteur pourrait l'avoir, et ce serait un **choix de conception à évaluer**, pas un défaut.

Le cadre repose sur l'**indépendance conditionnelle des vues** de **Blum & Mitchell, COLT 1998** ([PDF](https://www.cs.cmu.edu/~avrim/Papers/cotrain.pdf)) : `P[x₁ = x̂₁ | x₂ = x̂₂] = P[x₁ = x̂₁ | f₂(x₂) = f₂(x̂₂)]`. Détail rarement cité : **les auteurs eux-mêmes doutent de leur hypothèse** — *« Theorem 1 below can be viewed as showing why this is perhaps not really so plausible after all. »*

### 7.2 L'indépendance des erreurs — le point à creuser

C'est le cœur de la question du ticket. Deux moteurs qui se trompent ensemble ne s'alarment jamais.

#### La démonstration qu'en pratique l'indépendance est fausse

**Abney, *Bootstrapping*, ACL 2002** ([PDF](https://aclanthology.org/P02-1046.pdf)) fait le test empirique. Sous indépendance, connaître la précision d'une règle détermine celle de toutes, par `P(Y|F) = [P(G|F) − P(G|Ȳ)] / [P(G|Y) − P(G|Ȳ)]`. Appliquée aux données de Collins & Singer (89 305 instances), la formule donne des « précisions » de **−12,7**, **75,7** et **−9,94**, contre des vérités de 0,030, 0,986 et 0,949. Verbatim : *« the numbers do not even look like probabilities. The cause is the failure of view independence to hold in the data. »*

#### L'affaiblissement, et le budget de dépendance tolérable

Abney introduit l'**indépendance de règles**, plus faible que l'indépendance de vues, et un écart à l'indépendance `d_y`. Son **théorème 3** rétablit la conclusion sous **dépendance faible de règles** :

```
d_y  ≤  p₂ · (q₁ − p₁) / (2 p₁ q₁)
```

où `p₁` est le taux d'erreur conditionnel du moteur principal et `q₁ = 1 − p₁`. Ce qui en découle :

- **Corrélation négative : gratuite.** *« If the rules are negatively correlated, then their disagreement is larger than if they are conditionally independent, and the conclusion of theorem 2 is maintained a fortiori. »*
- **Corrélation positive : danger.** *« Unfortunately, in the data, they are positively correlated, so the theorem does not apply. »*
- **Le budget dépend de la qualité du moteur principal.** *« If p₁ = 0.5, then weak rule dependence reduces to independence […] However, **as p₁ decreases, the permissible amount of conditional dependence increases**. »*

⚠️ **Conséquence contre-intuitive, et directement applicable au risque nommé de l'ADR-0001.** Si `qwen3:8b` est bon (`p₁ → 0`), le budget de dépendance tolérable explose et le désaccord reste un majorant valide même avec des moteurs corrélés. Si `qwen3:8b` est médiocre (`p₁ → 0,5`), le budget tend vers zéro et **la moindre corrélation positive casse la garantie**. Autrement dit : **le témoignage par désaccord est le plus fiable quand on en a le moins besoin.** L'ADR-0001 avait déjà nommé le risque — *« la décision a été prise sans mesure de `qwen3:8b` »* — ; ce théorème en donne la forme exacte. Mesurer `p₁` n'est donc pas un préalable optionnel : c'est ce qui détermine si les mesures du témoin veulent dire quelque chose.

#### La dégradation, quantifiée

**Kuncheva, Whitaker, Shipp & Duin, *Pattern Analysis and Applications* 6, 2003** ([PDF](https://lucykuncheva.co.uk/papers/lkpaa.pdf)) bornent le vote majoritaire de `L` classifieurs de précision individuelle `p`, selon la dépendance mesurée par le `Q` de Yule.

| `p` | `L=3`, indépendants (Condorcet) | `L=3`, pire dépendance | `L=3`, meilleure dépendance | `L→∞`, pire |
| --- | --- | --- | --- | --- |
| 0,6 | 0,648 | **0,400** | 0,900 | 0,200 |
| 0,7 | 0,784 | 0,550 | — | 0,400 |
| 0,8 | 0,896 | 0,700 | — | 0,600 |

**Le chiffre qui doit rester en tête** : à `p = 0,6` et `L = 3`, l'indépendance donne 0,648 — un gain Condorcet de **+4,8 points**. La pire dépendance positive (*pattern of failure*) donne **0,400, soit 20 points en dessous de la précision individuelle**. L'écart entre meilleure et pire structure de dépendance est de **50 points**, contre 4,8 points pour le gain d'indépendance. **La structure de dépendance domine complètement le nombre de votants.** Sous le *pattern of failure*, `P_maj` est même **monotone décroissante en `L`** : ajouter un troisième témoin corrélé positivement **empire** les choses. Limite asymptotique : `2p − 1`.

**Kaniovski**, *Theory and Decision* ([PDF](https://serguei.kaniovski.wifo.ac.at/fileadmin/pdf/condorcet.pdf)), donne la forme explicite de la soustraction. Son **théorème 1** (solution de Bahadur tronquée à l'ordre 2) fait apparaître un facteur `(0,5 − p)` : pour `p > 0,5`, il est **négatif**, donc toute corrélation positive **soustrait directement** au terme de Condorcet, proportionnellement à `c` et en `n(n−1)`. Cas limite : *« The collective competence of a perfectly correlated jury equals that of a single juror. »*

⚠️ **Mais son théorème 2 dit l'inverse, et c'est un vrai trou.** Sous la *Q-solution*, *« the probability of a homogeneous jury collectively making the correct decision under simple majority rule is **independent of the correlation coefficient** »* — parce que *« the marginal probabilities and correlation coefficients **do not uniquely define a distribution** »*. Deux solutions valides du même problème donnent deux réponses opposées. **On ne peut donc pas prédire l'effet d'un troisième témoin à partir des seules corrélations par paires** ; il faudrait les corrélations d'ordre supérieur (Bahadur 1961), dont l'estimation *« requires an excessive number of parameters »*. Son corollaire 1.4 est explicite : *« For positive correlation, the effect of larger size on competence is **ambiguous**. »*

#### La formule qui répond directement à la question du troisième témoin

**Brown, Wyatt & Tiňo, *JMLR* 6, 2005** ([PDF](https://www.jmlr.org/papers/volume6/brown05a/brown05a.pdf)) — décomposition biais-variance-**covariance** :

```
E[(f̄ − t)²] = biais²  +  (1/M)·var  +  (1 − 1/M)·covar
```

**C'est la formule à retenir.** La variance est divisée par `M` ; la covariance est multipliée par `(1 − 1/M)`, qui **croît vers 1**. Passer de `M = 2` à `M = 3` : la contribution de la variance descend de 1/2 à 1/3, mais celle de la **covariance monte de 1/2 à 2/3**. Ajouter des membres ne peut **jamais** éliminer la covariance — c'est un plancher irréductible.

Le même papier prévient qu'on ne peut pas maximiser la diversité impunément : *« The fact that the Ω term exists illustrates again that **we cannot simply maximise diversity without affecting the other parts of the error**. »*

**Dietterich, MCS 2000** ([PDF](https://web.engr.oregonstate.edu/~tgd/publications/mcs-ensembles.pdf)) rappelle la condition **nécessaire et suffisante** de Hansen & Salamon : les classifieurs doivent être *« accurate and diverse »*, et *« two classifiers are diverse if they make **different errors** »*. Avec l'avertissement : si les erreurs individuelles dépassent 0,5, même décorrélées, *« the error rate of the voted ensemble will **increase** »*.

### 7.3 Le régime « deep ensembles » ne s'applique pas ici

Il faut écarter explicitement une littérature séduisante mais hors sujet.

**Jiang, Nagarajan, Baek & Kolter, ICLR 2022** ([arXiv:2106.13799](https://arxiv.org/abs/2106.13799)) établissent le *Generalization Disagreement Equality* : `E[Dis] = E[TestErr]` — non plus une borne mais une **égalité**, sous calibration *class-wise* de l'ensemble. Empiriquement sur CIFAR-10, erreur 0,336 contre désaccord 0,348. Réserve des auteurs eux-mêmes : *« we would need labeled test data to verify if CACE is small. **This would defeat the purpose.** »*

**Kirsch & Gal, TMLR 2022** ([PDF](https://arxiv.org/pdf/2202.01851)) le dégonflent avec le résultat qui compte ici : *« **all calibration metrics, ECE, CACE and CWCE, deteriorate under increasing disagreement**, both in distribution and under distribution shift. »* ⚠️ **La borne se relâche exactement sur les cas à fort désaccord — c'est-à-dire exactement ceux qu'on veut détecter.** Et la circularité est nommée : *« Given that all these calibration metrics require access to the labels […] we might just as well use the labels directly. »*

**Pourquoi ce cadre n'est pas le nôtre.** Le GDE suppose deux tirages du **même** algorithme stochastique. **Lakshminarayanan, Pritzel & Blundell, NeurIPS 2017** ([PDF](https://papers.nips.cc/paper_files/paper/2017/file/9ef2ed4b7fd2c810847ffa5fa85bce38-Paper.pdf)) identifient la source de diversité qui marche dans ce régime : *« random initialization of the NN parameters, along with random shuffling of the data points, was sufficient »*, et notent que *« bagging deteriorated performance »*. Un LLM et un lexique de règles ont une diversité **architecturale**, pas stochastique. **On est dans le régime Blum-Mitchell (§ 7.1-7.2), pas dans le régime des *deep ensembles*.** C'est d'ailleurs une bonne nouvelle : la diversité architecturale est ce qui rend l'indépendance des erreurs plausible, et c'est exactement l'argument que l'ADR-0001 fait quand il dit que le lexique déclenche l'alarme *« de l'extérieur, sans rien demander au LLM »*.

⚠️ **Un troisième moteur appris déplace ce curseur.** Un classifieur entraîné sur le même corpus, avec les mêmes traits lexicaux, serait bien plus proche du lexique que le lexique ne l'est du LLM. **La question « ce témoin est-il indépendant ? » n'est pas rhétorique : elle se mesure, et elle est mesurable** (§ 7.4).

### 7.4 Ce qui se mesure : le catalogue de diversité, et son résultat négatif

**Kuncheva & Whitaker, *Machine Learning* 51(2), 2003** ([PDF](https://lucykuncheva.co.uk/papers/lkml.pdf)) cataloguent les mesures à partir de la table 2×2 des couples corrects/faux — `N¹¹` (les deux corrects), `N⁰⁰` (les deux faux), `N¹⁰`, `N⁰¹` :

| Mesure | Formule | Ce qu'elle dit |
| --- | --- | --- |
| `Q` de Yule | `(N¹¹N⁰⁰ − N⁰¹N¹⁰) / (N¹¹N⁰⁰ + N⁰¹N¹⁰)` | ∈ [−1, 1], **espérance 0 sous indépendance statistique**. C'est le test direct de la condition du § 7.2. |
| corrélation `ρ` | numérateur identique, dénominateur en racine des marges | même signe que `Q`, avec `\|ρ\| ≤ \|Q\|` |
| désaccord `Dis` | `(N⁰¹ + N¹⁰) / N` | le signal d'alarme lui-même |
| **double faute `DF`** | **`N⁰⁰ / N`** | **l'angle mort : les cas où les deux se trompent ensemble** |

**Le *double-fault* est la mesure centrale de cette carte.** C'est, par construction, ce que le désaccord ne peut jamais voir : la proportion de demandes où le verdict est faux **et** le témoin ne dit rien. Un troisième moteur dont le `DF` avec le LLM est proche du taux d'erreur du LLM seul n'ajoute **aucune capacité de rattrapage** — il échoue précisément là où le LLM échoue déjà.

Relations exactes établies par les auteurs : `KW = ((L−1)/2L)·Dis_av` et `κ = 1 − Dis_av / (2 p̄(1−p̄))`.

⚠️ **Le résultat négatif, à ne pas édulcorer.** La conclusion de l'article est décourageante :

> *« The low absolute values of the correlation coefficients […] is **discouraging**. It **raises doubts about the abilities of diversity measures to pick up small improvements and deteriorations. Practical problems are likely to be exactly in this class.** »*

Et : *« there is no clear relationship between diversity and the averaged individual accuracy »* ; *« the notion of diversity is not clear-cut »*.

Deux nuances qui sauvent quelque chose. D'abord, en simulation à dépendance **symétrique**, toutes les mesures corrèlent correctement avec l'amélioration ; c'est quand la dépendance par paires devient **déséquilibrée** que la relation casse. Ensuite, sur données réelles les auteurs trouvent **trois groupes de mesures** : le *double-fault* seul, le CFD seul, et tout le reste. **`Dis` et `DF` ne mesurent donc pas la même chose : il faut suivre les deux.**

**Lecture pour la carte** : les mesures de diversité sont un **diagnostic**, pas un prédicteur de gain. Elles répondent à « ce témoin est-il redondant avec l'existant ? » — question réelle et mesurable — mais pas à « de combien améliorera-t-il l'alarme ? », qui se mesure directement (§ 8, § 11).

### 7.5 Le diagramme kappa-erreur

**Margineantu & Dietterich, ICML 1997** ([PDF](https://web.engr.oregonstate.edu/~tgd/publications/ml97-pruning-adaboost.pdf)) proposent la visualisation qui rend tout cela lisible d'un coup d'œil. Kappa corrige l'accord attendu par hasard — *« all reasonable classifiers will tend to agree with one another, simply by chance […] We would like our measure of agreement to be high only for classifiers that agree with each other much more than we would expect from random agreements »* :

```
κ = (Θ₁ − Θ₂) / (1 − Θ₂)
```

La construction : *« a **scatterplot where each point corresponds to a pair of classifiers**. The x coordinate is the value of κ. The y coordinate is the **average of their error rates**. »* Et la lecture : *« The classifiers at the **lower right are very accurate but also very similar**. The classifiers at the **upper left have higher error rates, but they are also very different**. »*

**Avec trois moteurs, il y a exactement trois points** : (LLM, lexique), (LLM, nouveau), (lexique, nouveau). C'est un graphe minuscule, immédiatement lisible, et qui répond à la question de redondance mieux qu'un tableau de chiffres : **un nuage serré en bas-à-droite dit que les témoins sont redondants et sans pouvoir de détection**. Avantage pratique noté par les auteurs : la méthode *« does not require any holdout set »*.

### 7.6 La prémisse « la divergence prime sur la confiance auto-déclarée » est-elle étayée ?

La spec (§ 6.2) fait primer `Contested` sur `NeedsReview` parce que la divergence *« ne dépend pas de l'auto-évaluation d'un petit modèle réputé sur-confiant »*. Cette prémisse est **étayée, avec une nuance qui mérite d'être connue**.

**Ce qui l'étaye :**

- **Guo et al., ICML 2017** ([arXiv:1706.04599](https://arxiv.org/abs/1706.04599)) : les réseaux modernes sont **sur-confiants** ; profondeur, largeur, *BatchNorm* et *weight decay* réduit y contribuent.
- **Xiong et al., ICLR 2024** ([texte](https://ar5iv.labs.arxiv.org/html/2306.13063)) : *« LLMs often exhibit a high degree of overconfidence when verbalizing their confidence »*, avec des confiances massivement agglutinées dans la tranche 80-100 %. **C'est structurellement le même problème qu'une échelle `Low`/`Medium`/`High` saturée sur `High`.** ECE/AUROC de la confiance verbalisée : GPT-3 52,0 / 51,3 ; GPT-3,5 37,7 / 55,1 ; GPT-4 18,0 / 62,7. Comparaison frappante sur GSM8K : cohérence entre échantillons **AUROC 92,7 %** contre verbalisé **54,8 %**.
- **GPT-4 Technical Report** ([texte](https://ar5iv.labs.arxiv.org/html/2303.08774)), légende de la figure 8 : *« The post-training hurts calibration significantly. »* Cohérent avec Kadavath et al. ([arXiv:2207.05221](https://arxiv.org/abs/2207.05221)) : *« RL finetuning tends to collapse language model predictions. »*
- **Huang et al., ICLR 2024** ([arXiv:2310.01798](https://arxiv.org/abs/2310.01798)) : l'auto-correction **intrinsèque** dégrade la performance (GPT-3,5 sur CommonSenseQA : 75,8 → **38,1**), alors qu'avec des étiquettes oracle elle monte à 89,7. **Tout le gain vient de l'information externe, pas de l'introspection.** Confirmé par Stechly et al. ([arXiv:2402.08115](https://arxiv.org/abs/2402.08115)) : *« significant performance collapse with self-critique and significant performance gains with sound external verification. »*

**La nuance qui empêche de conclure trop vite** : Tian et al., EMNLP 2023 ([ACL Anthology](https://aclanthology.org/2023.emnlp-main.330/)) trouvent que pour les modèles alignés par RLHF, *« verbalized confidences […] are typically **better-calibrated than the model's conditional probabilities** […] often reducing the expected calibration error by a relative 50 % »*. Et l'AUROC de la confiance verbalisée mesurée par Xiong et al. reste **partout supérieure à 0,5** : ce n'est pas du bruit pur. Leur méthode hybride (verbalisé + cohérence) l'emporte dans 13 cas sur 20.

**Lecture** : la littérature soutient de **ne pas laisser la confiance auto-déclarée dominer un signal externe** — ce qui est exactement la règle de la spec — sans soutenir de la **jeter**. La spec ne la jette pas non plus : elle en fait la condition de `Corroborated`. La conception tient.

⚠️ **Une réserve propre au multi-label, non couverte par la spec.** Kull et al., NeurIPS 2019 ([arXiv:1910.12656](https://arxiv.org/abs/1910.12656)) distinguent trois notions emboîtées : calibration de **confiance** (top-1 seulement), **par classe** (`P(Y_k=1 | p̂_k = p) = p` pour chaque `k`, strictement plus exigeante), et **canonique** (vecteur joint). **Une `DeclaredConfidence` unique et globale sur un ensemble de 7 droits ne correspond à aucune de ces trois notions.** Ce n'est pas un défaut de la spec — le champ n'a jamais prétendu être une probabilité — mais cela signifie qu'on ne peut pas lui appliquer la boîte à outils de la calibration sans la redéfinir d'abord. Toute mesure d'ECE sur `DeclaredConfidence` devrait donc dire **par rapport à quel événement** elle est calculée : « l'ensemble complet est exact », ou « le droit dominant est exact ». Les deux réponses diffèrent, et aucune n'est celle de la spec par défaut.

## 8. Les métriques d'une valeur d'alarme

### 8.1 Le cadre formel de la prédiction sélective

**El-Yaniv & Wiener, *JMLR* 11, 2010** ([PDF](https://www.jmlr.org/papers/volume11/el-yaniv10a/el-yaniv10a.pdf)) posent le modèle sélectif : un couple `(f, g)` où `f` prédit et `g : X → [0,1]` sélectionne.

```
couverture      Φ(f,g) = E[g(X)]
risque sélectif R(f,g) = E[ℓ(f(X),Y) · g(X)] / Φ(f,g)
```

La **courbe risque-couverture** est le risque sélectif en fonction de la couverture, quand on fait varier le seuil. Les auteurs insistent : *« both the coverage and risk are **unknown quantities** »* — d'où la nécessité de bornes probabilistes.

**Geifman & El-Yaniv, NeurIPS 2017** ([PDF](https://arxiv.org/pdf/1705.08500)) donnent la version pratique (algorithme SGR), avec une borne (lemme 3.1, Gascuel & Caraux) qui est **exactement une borne binomiale de type Clopper-Pearson** — le même outil qu'au § 4 — et une **correction d'union `δ/k`** sur les `k` seuils testés (théorème 3.2). Ce dernier point est le traitement primaire du problème « je teste plusieurs seuils sur le même jeu de test », et il s'applique tel quel ici.

**Chow, 1970** — l'origine du compromis rejet/erreur. ⚠️ Article non ouvert (IEEE Xplore / ACM DL inaccessibles) ; sa formalisation est restituée par [ce survey](https://arxiv.org/html/2107.11277v3) : rejeter si `max_i P(ω_i|x) < t`, avec `t = (C_r − C_c)/(C_e − C_c)`. Limite déjà notée à l'époque : la règle suppose la connaissance complète des distributions.

### 8.2 AURC, E-AURC, AUGRC

**Geifman, Uziel & El-Yaniv, ICLR 2019** ([texte](https://ar5iv.labs.arxiv.org/html/1805.08206)) définissent :

```
AURC(κ, f | V_n) = (1/n) · Σ_{θ ∈ Θ} r̂(f, g_θ | V_n)
```

**`Θ` est l'ensemble des valeurs distinctes du score.** Retenir cette phrase : c'est elle qui décide de tout ce qui suit pour un signal à trois valeurs.

L'**E-AURC** soustrait l'AURC du meilleur ordonnancement possible **à même taux d'erreur** (`AURC(κ*) ≈ r̂ + (1−r̂)·ln(1−r̂)`), ce qui isole la **qualité de tri** de la performance du classifieur sous-jacent — un classifieur très précis a mécaniquement une AURC basse même avec un score de confiance médiocre.

**Hendrycks & Gimpel, ICLR 2017** ([texte](https://ar5iv.labs.arxiv.org/html/1610.02136)) fixent la référence : AUROC comme *« the probability that a positive example has a greater detector score than a negative example »*, et **AUPR rapportée deux fois** (Success et Error) *« because the base rate of the positive class greatly influences the AUPR »*. Leur mise en garde vaut pour toute confiance auto-déclarée : les probabilités softmax ne sont pas fiables **dans l'absolu** (un bruit aléatoire obtient 91 % de confiance sur MNIST), seulement en **ordonnancement**.

**Corbière et al., NeurIPS 2019** ([texte](https://ar5iv.labs.arxiv.org/html/1910.04851)) définissent la *True Class Probability*, avec des garanties intéressantes par leur asymétrie : `TCP > 1/2 ⇒` prédiction correcte ; `TCP < 1/K ⇒` prédiction incorrecte ; **aucune garantie dans `[1/K, 1/2]`**. Métriques rapportées : AUPR-Error (principale), AUPR-Success, FPR à 95 % TPR, AUROC.

**Jaeger et al., ICLR 2023** ([texte](https://ar5iv.labs.arxiv.org/html/2211.15259)) apportent la critique méthodologique la plus utile ici. Leur thèse : une fonction de score de confiance *« has no self-purpose »*, elle existe pour empêcher les ***silent failures*** — le mot exact de l'ADR-0001. **Détecteur et classifieur doivent être évalués comme un système symbiotique.** Ils montrent que :

- l'**AUROC de détection d'erreur est trompeuse** parce qu'elle ignore l'effet du détecteur sur le classifieur lui-même : quand le détecteur modifie le modèle, il change **l'ensemble des cas d'échec qui servent d'étiquettes de vérité terrain**, si bien que chaque méthode est évaluée sur son propre jeu de test ; s'ajoute un biais vers les vrais négatifs ;
- l'**AUPR-Error est trompeuse** parce qu'elle est *« hacked by biases towards easy-to-detect true positives »*.

**Traub, Jaeger et al., NeurIPS 2024** ([texte](https://arxiv.org/html/2407.01032v2)) introduisent la distinction qui, à notre avis, colle le mieux au `ReviewSignal` :

```
risque sélectif  (conditionnel) : P(Y_f = 1 | g(x) ≥ τ)
risque généralisé      (joint)  : P(Y_f = 1,  g(x) ≥ τ)
```

L'AUGRC est l'aire sous le second, et son interprétation est **« le risque moyen des défaillances non détectées »**. Ils reprochent à l'AURC de *« fail to adequately translate the risk from specific working points into a meaningful aggregated evaluation score »*, en surpondérant le régime de très basse couverture où le dénominateur est minuscule ; le classement des méthodes change sur 5 jeux de données sur 6.

⚠️ **Pourquoi cette distinction compte particulièrement ici.** Dans notre service, **un humain relit tout** : la couverture n'est pas un rejet, c'est un **ordre de priorité**. Le risque *conditionnel* à l'acceptation modélise mal cela. Le risque **généralisé** — probabilité **jointe** d'être fautif ET non signalé — est la traduction exacte de « ce que la relecture verrait tard ». C'est le concept que l'ADR-0001 appelle « erreur silencieuse ».

### 8.3 Le problème central : le `ReviewSignal` n'a que trois valeurs

**Le fait structurel.** Toutes les métriques du § 8.2 sont des intégrales sur `Θ` = valeurs distinctes du score. Avec `|Θ| = 3`, la courbe risque-couverture a **trois points** et la courbe ROC est une polyligne à **deux sommets internes**. Tout le reste est de l'**interpolation, pas de la mesure**.

**Fawcett, *Pattern Recognition Letters* 27, 2006** ([PDF](https://people.inf.elte.hu/kiss/13dwhdm/roc.pdf)) est explicite :

- *« A discrete classifier is one that outputs only a class label. Each discrete classifier produces an (fp rate, tp rate) pair corresponding to a **single point** in ROC space. »*
- L'interpolation vers `(0,0)` et `(1,1)` *« is misleading because it suggests performance at operating points the classifier was never designed for »*.
- Sa figure 8b montre deux systèmes égaux en un point, dont l'un se dégrade en s'en éloignant : **une AUC calculée sur deux points est une aire trapézoïdale, pas une mesure de pouvoir discriminant comparable**.
- Et : *« Without a measure of variance we cannot compare the classifiers. »*

**Hand, *Machine Learning* 77, 2009** ([PDF](https://spiral.imperial.ac.uk/server/api/core/bitstreams/4daffcda-4e19-4e5f-bc5d-dee73f618939/content)) aggrave le diagnostic : l'AUC est **incohérente**, car elle équivaut à une perte espérée sous une distribution de coûts **qui dépend du classifieur évalué**. Comparer deux classifieurs par leur AUC revient à les évaluer avec deux métriques différentes. **Avec deux seuils internes seulement, l'objection est encore plus forte** : agréger deux points en une aire leur donne des poids fixés par la géométrie de l'interpolation, pas par les coûts métier.

Nuance historique utile : **Hanley & McNeil, *Radiology* 143, 1982** ([notice](https://pubmed.ncbi.nlm.nih.gov/7063747/)) ont établi l'AUC par la *rating method*, c'est-à-dire sur des échelles **ordinales à peu de niveaux** (5 catégories radiologiques). Ce n'est donc pas l'interprétation par rangs qui casse à 3 niveaux, mais le fait que les **ex æquo massifs** rendent l'estimation dominée par la convention d'interpolation.

### 8.4 Le cadre natif d'un signal ordinal grossier : les rapports de vraisemblance par niveau

C'est la trouvaille la moins attendue de cette recherche, et probablement la plus directement utilisable.

**Peirce & Cornell, *Medical Decision Making* 13(2), 1993** ([notice](https://pubmed.ncbi.nlm.nih.gov/8483399/) ; [application ouverte](https://pmc.ncbi.nlm.nih.gov/articles/PMC5602451/)) formalisent le ***stratum-specific likelihood ratio*** (SSLR) — conçu exactement pour un test diagnostique dont le résultat est ordinal à peu de niveaux :

```
LR(s) = P(résultat ∈ s | positif) / P(résultat ∈ s | négatif)
cote post-test = cote pré-test × LR(s)
```

Un SSLR *« indicates how much more likely or less likely a specific test result is for individuals with a disease than for individuals without »*. **Chaque niveau du signal produit sa propre probabilité post-test.** Appliqué au `ReviewSignal`, cela donne trois nombres directement interprétables : *sachant `Contested`, de combien la cote « le verdict est faux » est-elle multipliée ?* — et de même pour `NeedsReview` et `Corroborated`.

Propriétés décisives ici :

- **Indépendants de la prévalence**, donc transférables du corpus vers le flux réel — contrairement à la précision ou à l'AUPR. C'est une propriété rare et précieuse à 120 exemples.
- Les auteurs mesurent la supériorité empirique de la stratification sur un *cutoff* dichotomique unique (`p = 0,004`).
- Règles de construction explicites : assez de sujets par strate pour que les SSLR soient **monotones** ; **fusionner** les strates dont les intervalles de confiance à 95 % se recouvrent.
- **Résultat empirique directement transposable** : la même mesure produit **4 strates discriminantes** en unité de soins coronariens mais **seulement 2** en service d'urgence. **Le nombre de niveaux réellement informatifs est une propriété des données, pas du barème.** Traduction pour la carte #42 : il se peut très bien que sur 120 exemples, `NeedsReview` et `Corroborated` aient des SSLR dont les intervalles se recouvrent — auquel cas le signal à trois valeurs en vaut opérationnellement deux. C'est un résultat mesurable, et c'est une information que la carte a intérêt à connaître avant d'ajouter un troisième moteur.

Une réserve de vocabulaire : le `ReviewSignal` n'est pas une fonction de score de confiance au sens de Jaeger et al., c'est un **accord/désaccord entre deux systèmes** — donc plus proche d'un test diagnostique séparé, ce qui est précisément le cadre SSLR. L'avertissement de Jaeger reste néanmoins valide : témoin et verdict s'évaluent **conjointement**, puisque c'est le témoin qui définit quels cas comptent comme signalés.

### 8.5 Tableau de transposition

| Métrique | Statut pour un signal ordinal à 3 valeurs | Justification |
| --- | --- | --- |
| Cadre `(f, g)`, `R(f,g)`, `Φ(g)` | **S'applique pleinement** | `g` est déjà binaire ; rien n'exige un score continu ([El-Yaniv & Wiener](https://www.jmlr.org/papers/volume11/el-yaniv10a/el-yaniv10a.pdf)) |
| Risque sélectif + couverture **en un point** | **S'applique — mesure naturelle** | 2-3 points bien définis, chacun avec `r̂` et `Φ̂` mesurables |
| **Risque généralisé en un point** | **S'applique — probablement le mieux adapté** | Pas de dénominateur minuscule ; exprime « fraction du flux fautive ET non signalée » ([AUGRC](https://arxiv.org/html/2407.01032v2)) |
| Courbe risque-couverture continue | **Ne s'applique pas** — polyligne à 2 sommets | `Θ` = valeurs distinctes |
| Règle de Chow comme **règle** | **Ne s'applique pas** | Suppose un `max_i P(ω_i\|x)` continu et calibré |
| SGR | **Ne s'applique pas** | La dichotomie sur `θ` n'a pas de granularité |
| AURC | Applicable mécaniquement, **quasi vide de contenu** | Somme sur `\|Θ\| = 3` : dominée par la convention d'interpolation |
| **E-AURC** | **Ne s'applique pas de façon informative** | `κ*` est un tri parfait au niveau individuel, inatteignable avec 3 niveaux : l'écart mesurerait la **granularité**, pas la qualité. **E-AURC surestimerait systématiquement le défaut du témoin.** |
| MSP / TCP | **Ne s'appliquent pas** | Le signal n'est ni un softmax ni une probabilité de vraie classe |
| AUROC de détection d'erreur | Applicable formellement, **fortement déconseillée** | Fawcett (aire sur classifieur discret) + Jaeger (ignore l'effet du détecteur, biais vers les VN) + Hand (incohérence des coûts) |
| AUPR-Error / Success | Applicable mais **dépendante du taux de base** ; tri intra-niveau indéfini | Hendrycks & Gimpel ; Jaeger et al. |
| **FPR à 95 % TPR** | **Ne s'applique pas** | Un TPR de 95 % n'est en général **pas atteignable** avec 2 seuils ; il faudrait randomiser |
| AUGRC | La plus défendable des agrégées, mais bornée par `\|Θ\| = 3` | Le risque joint n'explose pas en basse couverture |
| **Enveloppe convexe ROC + droites d'iso-performance** | **S'applique — le bon outil** | Conçue pour comparer un petit nombre de points discrets (§ 10.5) |
| **SSLR / rapport de vraisemblance par niveau** | **S'applique — le cadre natif** | Conçu pour les tests ordinaux à peu de niveaux ; indépendant de la prévalence |
| H-measure | Applicable, exige d'expliciter une distribution de coûts | Hand 2009 |

### 8.6 ⚠️ Ce que la littérature ne tranche pas

1. **Aucune source primaire ne prescrit de métrique pour un score à 2-3 niveaux dans le cadre moderne de la prédiction sélective.** La littérature « prédiction sélective » suppose partout une granularité fine ; la littérature ROC discret et la littérature médicale SSLR traitent des scores grossiers mais **sans lien avec ce vocabulaire. Le pont entre les deux n'est pas fait — le `ReviewSignal` tombe dans l'interstice.**
2. **AURC contre AUGRC : débat récent et non clos**, entre deux équipes largement recouvrantes (ICLR 2023 contre NeurIPS 2024).
3. **Ex æquo massifs** : aucune recommandation faisant autorité pour le régime où presque toutes les paires sont ex æquo.
4. **Nombre de niveaux nécessaire** : il existe des règles de fusion (recouvrement des intervalles de confiance des SSLR), aucune règle prescriptive.
5. **Randomisation** : Provost & Fawcett démontrent que tout point d'un segment est atteignable par randomisation entre deux classifieurs, mais **rien n'établit que ce soit acceptable dans un contexte de conformité réglementaire** — et cela casserait le déterminisme exigé par l'ADR-0001.

### 8.7 La contrainte de taille, encore

Un chiffre à ne pas perdre de vue en lisant la Partie 1 § 4. **La précision de l'alarme s'estime sur le nombre de cas signalés ; le rappel de l'alarme s'estime sur le nombre d'erreurs du verdict.** Si `qwen3:8b` se trompe sur, disons, 20 des 120 exemples, le rappel de l'alarme est une proportion à **n = 20** : intervalle de Wilson d'environ ±20 points. Les deux moitiés du couple **n'ont pas la même précision**, et le dénominateur du rappel est le petit. C'est une raison de plus de rapporter les deux séparément, chacun avec son `n` et son intervalle (§ 11.5).

## 9. L'apport marginal d'un troisième témoin

### 9.1 Comparer deux détecteurs sur le même échantillon

Le cadre est celui de la Partie 1 § 4 : prédictions **appariées** sur les mêmes 120 exemples. **McNemar** est le test que Dietterich désigne pour ce cas. Formule avec correction d'Edwards, où seules les cellules discordantes portent de l'information :

```
χ² = (|n₀₁ − n₁₀| − 1)² / (n₀₁ + n₁₀)        (1 ddl)
```

⚠️ **Condition de validité : `n₀₁` et `n₁₀` d'au moins ~25 chacun.** À 120 exemples et deux détecteurs relativement proches, ce seuil n'est pas garanti. Il faut alors la **version exacte binomiale** `p = 2·Σ_{i=B}^{n} C(n,i)·0,5ⁿ` avec `n = n₀₁ + n₁₀`. (Formules restituées via [Raschka, arXiv:1811.12808](https://ar5iv.labs.arxiv.org/html/1811.12808), le texte intégral de Dietterich 1998 n'étant accessible que par le miroir cité en Partie 1.)

**Contre-indication à connaître** : McNemar teste **deux artefacts figés sur un échantillon figé**. Il ne capture ni la variabilité d'entraînement ni celle du tirage de test — c'est précisément pour cela que Dietterich introduit le 5×2cv. Appliqué à un verdict LLM non ré-entraînable, cette lacune **reste sans réponse propre dans la littérature**.

**Dror, Baumer, Shlomov & Reichart, ACL 2018** ([ACL Anthology](https://aclanthology.org/P18-1128/)) donnent l'arbre de décision complet pour le TAL (t apparié, test des signes, McNemar, Wilcoxon signé, Cochran Q — la généralisation multi-classes de McNemar —, permutation de Pitman, *bootstrap* apparié). Deux mises en garde qui portent directement sur notre corpus :

- **Observations dépendantes.** Les jeux TAL violent typiquement l'indépendance (phrases d'une même source, d'un même auteur), et ces dépendances sont *« **hard to characterize, let alone to quantify** »*. ⚠️ **Notre corpus est exactement dans ce cas** : 21 exemples `inspire-cnil`, 4 `inspire-edpb`, et surtout les 16 exemples appariés (Partie 1 § 6). Tous les tests ci-dessus **surestiment la précision de leurs valeurs *p***.
- **Comparaisons multiples.** Seuls 3 papiers ACL 2017 sur 110 corrigeaient la multiplicité ; 65 % ne rapportaient aucun test. Or la carte #42 prévoit **au moins deux comparaisons** (lexique+nouveau contre lexique, nouveau seul contre lexique) — plus toutes celles qu'on sera tenté de faire par étiquette.

### 9.2 ⚠️ DeLong ne teste pas ce qu'on croit dans le cas emboîté

Le test de DeLong (1988) est le réflexe pour comparer deux AUC appariées : l'AUC empirique est une U-statistique de Mann-Whitney, dont on estime non paramétriquement la covariance par les composantes structurelles.

**Mais « ajouter un troisième moteur » est le cas *emboîté* par excellence, et DeLong y est invalide.** Démonstration par **Demler, Pencina & D'Agostino, *Statistics in Medicine* 2012** ([texte intégral](https://pmc.ncbi.nlm.nih.gov/articles/PMC3684152/)) : sous l'hypothèse nulle (le nouveau prédicteur n'apporte rien), *« if the models are nested, parameters known and the new predictor is noninformative, then the full and reduced models are the same »* — la différence d'AUC devient une **U-statistique dégénérée**, dont la loi asymptotique n'est plus normale.

Conséquences mesurées :

- les valeurs *p* de DeLong contre celles d'un test exact *« do not form a 45 degree line; they are very scattered »*, avec un **biais vers le nul** ;
- **perte de puissance massive** : sur 621 événements / 8 261 observations avec un effet de 0,2, puissance **0,473** (DeLong) contre **0,941** (Wald).

**Recommandation des auteurs** : procédure en deux temps — (1) tester la significativité par un test de Wald ou de rapport de vraisemblance ; (2) **si** significatif, rapporter la différence d'AUC **avec son intervalle** DeLong. **DeLong pour estimer, pas pour tester un ajout.**

Seconde critique, indépendante (**Gur, Bandos & Rockette**, [texte](https://pmc.ncbi.nlm.nih.gov/articles/PMC2637106/)) : l'approche non paramétrique **extrapole linéairement** du dernier point expérimental jusqu'à `(1,1)` ; quand les systèmes n'ont pas les mêmes points extrêmes, cela peut **inverser les conclusions** — dans leur exemple, paramétrique `p = 0,75` contre DeLong `p = 0,003` **sur les mêmes données**.

Contre-indication pratique, enfin : l'AUC exige un score continu. Un lexique à règles et un verdict LLM n'en produisent pas nativement, et **fabriquer un score change l'objet évalué** (§ 8.3).

### 9.3 Les mesures d'accord, et ce qu'elles disent vraiment

Le kappa de Cohen s'écrit `κ = (p̂_a − p̂_e) / (1 − p̂_e)`. Sa généralisation à plus de deux juges est le **kappa de Fleiss**.

⚠️ **Limite structurelle qui nous concerne directement** ([Moons & Vandervieren, arXiv:2303.12502](https://arxiv.org/pdf/2303.12502)) : le kappa de Fleiss exige que les juges classent chaque item dans **exactement une** catégorie, contrainte que les auteurs qualifient de *« consequential »* dès qu'un item relève de plusieurs. **C'est notre cas : 7 droits, multi-label.** Le kappa de Fleiss standard ne s'applique donc pas tel quel à une qualification RGPD. Il reste applicable **par étiquette** (7 kappas binaires) ou sur l'égalité d'ensembles (accord/désaccord binaire) — deux objets différents, à ne pas confondre.

**Les paradoxes du kappa.** Feinstein & Cicchetti 1990 ([notice](https://pubmed.ncbi.nlm.nih.gov/2348207/)) : *« a high value of p₀ can be drastically lowered by a substantial imbalance in the table's marginal totals »*, et l'ajustement par `κ_max` *« does not repair either problem, and seems to make the second one worse »*. Effet de prévalence quantifié par [Zec et al.](https://pmc.ncbi.nlm.nih.gov/articles/PMC5712640/) : au-delà d'environ 60 % de prévalence, kappa *« yields low values, often leading to conclude that no agreement is present »* alors que la concordance observée est élevée. ⚠️ **Directement critique ici** : `hors-perimetre` pèse 25 % du corpus, et un accord LLM/lexique attendu élevé place le calcul exactement dans le régime paradoxal. Correctifs documentés : PABAK (symétrisation de la table) et l'AC1 de Gwet.

**Contre-indication de fond, et elle est décisive** : **le kappa mesure l'accord, pas la valeur ajoutée.** Deux détecteurs peuvent avoir un kappa faible sans rien améliorer. C'est le résultat négatif de Kuncheva & Whitaker (§ 7.4) sous une autre forme.

**La mesure qui, elle, dit quelque chose sur la redondance utile : le *double-fault*** (§ 7.4). `DF = N⁰⁰/N` est *« the proportion of the cases that have been misclassified by both classifiers »*. Un troisième moteur dont le `DF` avec le LLM approche le taux d'erreur du LLM seul **n'ajoute aucune capacité de rattrapage**.

### 9.4 ⚠️ Les indices dédiés à l'apport marginal sont réfutés

C'est le résultat le plus tranchant de cette partie, et il évite une erreur qu'on aurait de bonnes chances de commettre.

Le **Net Reclassification Improvement (NRI)** et l'**Integrated Discrimination Improvement (IDI)** de Pencina et al. (2008) ont été conçus exactement pour mesurer l'apport d'un marqueur ajouté à un modèle existant. Ils sont aujourd'hui déconseillés par la littérature qui les a suivis.

**Kerr, Wang, Janes, McClelland, Psaty & Pepe, *Epidemiology* 2014** ([texte intégral](https://pmc.ncbi.nlm.nih.gov/articles/PMC3918180/)) :

- *« It is incorrect to interpret the index as "the proportion of patients reclassified" »* — il *« combines four proportions but is not itself a proportion »*.
- *« **NRI>0 tends to be positive for uninformative Y even when computed on a large, independent validation dataset.** »*
- *« NRI>0 has no interpretation that translates to the clinical benefit of the new marker. »*
- *« Infinitesimally small changes "count" even though they are clinically irrelevant. »*
- Pour trois catégories ou plus : *« **recommend against** net reclassification indices »*. (Notre `ReviewSignal` en a trois.)
- **Résultat structurant et directement utile** : à deux catégories, `NRI_e = Δ TPR` et `NRI_ne = −Δ FPR`. **Le NRI n'apporte alors rien de neuf par rapport à Δsensibilité et Δ(1−spécificité).** Les auteurs recommandent de garder le vocabulaire descriptif existant.

**Pepe, Janes & Li, *JNCI* 2014** ([article](https://academic.oup.com/jnci/article/106/4/dju041/2607255)) donnent les chiffres de simulation qui closent le débat :

> 10 000 individus, taux d'événement 10,2 %, **quatre biomarqueurs sans aucun pouvoir prédictif**, entraînement n = 420, test n = 420 ou 840.
> **Taux de faux positifs du NRI : 63,0 % sur données d'entraînement ; 18,8 % à 34,4 % sur données de test INDÉPENDANTES.**
> Par contraste : rare avec le changement d'AUC ; **~5,0 %** (nominal) avec le rapport de vraisemblance.
> *« Use of NRI P values in scientific reporting should be halted. »*

**Pepe et al., *Statistics in Biosciences* 2015** ([article](https://link.springer.com/article/10.1007/s12561-014-9118-0)) confirment : le NRI *« is likely to be positive even when the new marker has no predictive information »* et *« can provide a biased assessment even with independent validation data »*.

**Le mécanisme, en une phrase** : NRI et IDI récompensent le **déplacement des risques prédits**, pas l'amélioration de la calibration ni de la discrimination. Un prédicteur de bruit ajouté à un modèle réestimé déplace mécaniquement les risques. ⚠️ **Et cela survit à un jeu de test indépendant** — la validation externe ne protège pas.

**Traduction pour la carte #42** : mesurer « combien d'exemples changent de `ReviewSignal` quand on ajoute le troisième moteur » est un **indice de reclassification**, et cette famille de mesures est **positive même sous témoin purement bruité**. C'est le piège exact que la lecture du notebook devra éviter. La forme honnête de la même question est descriptive et déjà connue : **Δ rappel de l'alarme et Δ taux de fausse alarme**, chacun avec son intervalle.

### 9.5 L'alternative que les critiques recommandent : le bénéfice net

**Vickers & Elkin, *Medical Decision Making* 2006** ([texte intégral](https://pmc.ncbi.nlm.nih.gov/articles/PMC2577036/)) :

```
NB = TP/n − (FP/n) · [p_t / (1 − p_t)]
```

Pour comparer avec et sans le nouveau moteur, on **superpose les courbes** ([méthodologie](https://pmc.ncbi.nlm.nih.gov/articles/PMC6777022/)) : *« If the model incorporating the additional marker produces a higher curve, it demonstrates superior predictive value for decision-making — **accounting for both discrimination and calibration**. »*

Kerr et al. tranchent : *« **The preferred single-number summary of the prediction increment is the improvement in net benefit.** »* Et Vickers et al., *BMJ* 2016 ([texte](https://pmc.ncbi.nlm.nih.gov/articles/PMC4724785/)) : sensibilité, spécificité et AUC *« **do not tell us whether the model, test, or marker would do more good than harm** »*.

⚠️ **Ce n'est pas un chiffre unique** : les courbes peuvent se croiser, auquel cas la conclusion dépend du seuil. C'est précisément le sujet du § 10.

### 9.6 ⚠️ Ce que la littérature ne tranche pas

1. **Aucune métrique unique ne mesure l'apport marginal.** Les indices conçus pour ça sont réfutés ; le substitut recommandé (bénéfice net) est une **courbe** dépendant d'un jugement de valeur explicite.
2. **Test et estimation ne se recouvrent pas.** Le test naturel (DeLong) est invalide dans le cas emboîté ; il n'existe **pas de procédure consensuelle valide en une étape**.
3. **Le kappa n'a pas de correction consensuelle** — brut, PABAK et AC1 diffèrent par leur définition du hasard.
4. **La dépendance intra-corpus** est *« hard to characterize, let alone to quantify »* ; aucune méthode standard.
5. **La littérature primaire sur le protocole d'ablation en apprentissage automatique est quasi inexistante.** Le substitut rigoureux est le cadre de Dror et al. appliqué à la paire « système complet » contre « système sans le composant ».

## 10. Le coût asymétrique des fausses alarmes

Le service est une aide à la décision : un humain valide chaque qualification. Une **fausse alarme** coûte une relecture inutile ; une **alarme manquée** laisse passer une erreur. La spec l'a déjà noté — *« un faux positif y coûte donc peu, ce qui autorise le taux de déclenchement de ~25 % extrapolé au prototypage »* (§ 6.3) — mais sans chiffrer le ratio, et le taux n'a jamais été mesuré.

### 10.1 Le seuil optimal, et sa condition de validité

**Elkan, *The Foundations of Cost-Sensitive Learning*, IJCAI 2001** ([PDF](https://cseweb.ucsd.edu/~elkan/rescale.pdf)). Convention **ligne = prédiction, colonne = vérité** : `c₀₁` est le coût du **faux négatif**, `c₁₀` celui du **faux positif** (piège de notation : c'est l'inverse de la lecture naïve).

```
p* = (c₁₀ − c₀₀) / (c₁₀ − c₀₀ + c₀₁ − c₁₁)
```

Conditions de raisonnabilité : `c₁₀ > c₀₀` et `c₀₁ > c₁₁`, faute de quoi la politique optimale dégénère en constante. **C'est un test de cohérence à faire avant toute mesure.**

**Le point libérateur** : toute matrice 2×2 n'a *« essentiellement qu'un seul degré de liberté du point de vue de la décision »*. Si `c₀₀ = c₁₁ = 0`, alors `p*/(1−p*) = c₁₀/c₀₁`. **Il n'y a pas besoin de chiffrer quatre coûts — un seul ratio suffit.**

Sur le rééchantillonnage, la conclusion d'Elkan est un désaveu partiel de la pratique courante : changer l'équilibre des classes *« a peu d'effet […] la manière recommandée est d'apprendre un classifieur sur l'ensemble tel quel, puis de **calculer explicitement les décisions optimales** en utilisant les estimations de probabilité »*, après lissage et ajustement empirique du seuil.

⚠️ **Contre-indication centrale : tout le théorème suppose des probabilités calibrées.** Appliquer `p*` à un score non calibré est un non-sens silencieux. **Et un signal ordinal à trois valeurs n'est pas une probabilité.** Le théorème ne s'y applique qu'après remontée vers une probabilité calibrée, ou en traitant chaque frontière (`Corroborated`|`NeedsReview`, `NeedsReview`|`Contested`) comme un point d'opération distinct.

### 10.2 Les courbes de coût — la lecture directe du compromis

**Drummond & Holte, *Cost curves*, *Machine Learning* 65, 2006** ([PDF](https://webdocs.cs.ualberta.ca/~holte/Publications/mlj2006.pdf)). *(Correction bibliographique : le papier fondateur « Explicitly Representing Expected Cost » est **KDD 2000**, pas ICML 2000 — [page des auteurs](https://webdocs.cs.ualberta.ca/~holte/CostCurves/).)*

```
PC(+) = p(+)·C(−|+) / [ p(+)·C(−|+) + p(−)·C(+|−) ]
Norm(E[Cost]) = (FN − FP)·PC(+) + FP
```

Dualité point ↔ droite : un point ROC `(FP, TP)` devient la droite `Y = (FN−FP)x + FP`. Les segments de l'enveloppe inférieure correspondent **précisément** aux points de l'enveloppe convexe ROC, et les sommets aux **segments**.

Cinq raisons de préférer cette représentation à la ROC pour lire un coût :

1. **La différence verticale EST le delta de coût** au point d'opération. En ROC, le coût est une pente d'iso-performance — non lisible à l'œil.
2. **Le point de croisement EST le point d'indifférence.**
3. **La plage d'exploitation se lit exactement.** En ROC, *« la plage d'exploitation ne peut en général pas être lue précisément »* : sur leur exemple, estimation à l'œil 0,05-0,94 contre valeur exacte 0,14-0,85. Corollaire de sécurité : *« des performances pires que les classifieurs triviaux **ne peuvent pas passer inaperçues** »* — les auteurs montrent des travaux publiés qui évaluaient des classifieurs **hors** de leur plage d'exploitation.
4. **Intervalles de confiance et significativité visualisables** — *« not easily done with ROC »*.
5. **Moyennage correct** : le moyennage vertical en espace des coûts apparie les points au **même point d'opération**, et seul lui a la propriété que le coût attendu de la moyenne est la moyenne des coûts attendus. (Pertinent pour agréger sur les plis, Partie 1 § 2.)

**Positionnement clé pour notre cas** : les *regret graphs* de Hilden & Glasziou supposent les coûts **connus exactement** ; *« si ces valeurs ne sont pas connues exactement, les regret graphs ne peuvent pas être utilisés, mais les courbes de coût le peuvent »*. Et si une courbe domine l'autre, la distance verticale minimale est une **borne inférieure sur l'avantage relatif**.

Limites déclarées : `PC(+)` **mélange** prévalence et coût (une force et une faiblesse) ; coûts uniformes à l'intérieur de chaque type d'erreur ; c'est la prévalence de **déploiement** qui compte, pas celle du corpus.

### 10.3 La courbe de décision — comment obtenir le ratio sans chiffrer les coûts

C'est la seule des quatre familles qui fournit un **protocole d'élicitation concret**.

**Vickers & Elkin 2006** posent l'équivalence fondatrice : `(a−c)/(d−b) = (1−p_t)/p_t`, où `a−c` est le préjudice d'un faux négatif et `d−b` celui d'un faux positif — *« le seuil de probabilité auquel un patient optera pour le traitement **est informatif de la façon dont il pondère les préjudices relatifs** »*.

Le *BMJ* 2016 ([texte](https://pmc.ncbi.nlm.nih.gov/articles/PMC4724785/)) formalise `p_t` comme un **taux de change** (*« tout comme les euros se convertissent en dollars »*), obtenu par une question opérationnelle : *« Combien de biopsies feriez-vous pour trouver un cancer agressif ? »* — réponse « 10 » ⟹ seuil de 10 %. Formulation duale : un seuil de 10 % signifie « manquer la maladie est **9 fois pire** qu'une intervention inutile ».

**Transposition directe** : *« combien de relectures prioritaires inutiles acceptez-vous pour rattraper une qualification erronée ? »* Une réponse `k` fixe `p_t = 1/(1+k)` **sans jamais chiffrer le coût d'une non-conformité RGPD**. C'est exactement ce dont on a besoin : un service dont le métier est la conformité ne peut pas mettre un montant sur une violation, mais il peut dire combien de relectures il est prêt à payer.

**On rapporte une courbe, pas un point.** *« L'investigateur n'a pas à trouver le "bon" seuil, juste à avoir une idée du type de seuils qui auraient et n'auraient pas de sens. »* Lignes de référence obligatoires : « tout signaler » et « ne rien signaler » — analogues exacts de `(1,1)` et `(0,0)` en ROC. Sur notre corpus, « tout signaler » est le comportement dégradé maximal ; c'est une référence utile et honnête.

Limites déclarées : suppose probabilité prédite et seuil **indépendants** ; *« ne prend pas en compte l'incertitude associée aux prédictions »* ; suppose un comportement rationnel, la pratique étant *« plus désordonnée »*.

⚠️ **Contre-indication cruciale** ([analyse comparative](https://arxiv.org/abs/2509.24608)) : bénéfice net et perte de Brier *« choisiront toujours le même modèle comme optimal à un seuil donné »*, mais **à travers les seuils**, *« les différences de perte de Brier sont comparables, alors que les différences de bénéfice net **ne peuvent pas être comparées** »*. **On ne peut donc pas intégrer un Δ bénéfice net le long de l'axe `p_t` pour en faire un chiffre unique.** C'est précisément la tentation à laquelle il ne faut pas céder.

### 10.4 Les règles de score propres, et pourquoi elles ne s'appliquent pas ici

**Gneiting & Raftery, *JASA* 102(477), 2007** ([PDF](https://sites.stat.washington.edu/raftery/Research/PDF/Gneiting2007jasa.pdf)) : une règle est **strictement propre** si *« le prévisionniste n'a aucune incitation à prédire un P ≠ Q […] si S(Q,Q) ≥ S(P,Q) avec égalité si et seulement si P = Q »*. C'est une contrainte d'incitation-compatibilité : aucune stratégie de couverture ne peut améliorer le score espéré.

La **décomposition de Murphy (1973)** du score de Brier ([restitution](https://arxiv.org/pdf/1303.6182)) est l'outil intéressant :

```
Br = REL − RES + UNC
```

où **REL** (fiabilité) est l'écart entre probabilités annoncées et réelles — nul si calibré, donc *c'est le terme de calibration exigé par Elkan au § 10.1* ; **RES** (résolution) récompense les variations cohérentes et est nulle si le système annonce toujours la même valeur ; **UNC** ne dépend que de la prévalence. Critère d'utilité : *« une prévision "utile" doit avoir un score de Brier inférieur à sa composante d'incertitude, autrement dit la résolution doit être supérieure à la fiabilité. »*

**Le pont théorique, remarquable** — représentation de Schervish, restituée par Gneiting & Raftery :

> *« Un problème à deux décisions peut être caractérisé par un **ratio coût-perte** c ∈ (0,1) […] toute règle de score bornée continue à gauche est équivalente à un **mélange de scores zéro-un asymétriques pondérés par le coût**, avec une mesure de mélange non négative ν(dc). »*

**Une règle de score propre EST donc une moyenne pondérée de performances de décision sur tous les ratios de coût.** Choisir le score de Brier, c'est choisir implicitement une pondération `ν` — donc **diluer le signal en intégrant sur des régimes de coût qui ne nous concernent pas**. C'est le même reproche que celui de Hand à l'AUC (§ 8.3), sous une forme constructive.

⚠️ **Les *skill scores* sont généralement IMPROPRES.** Gneiting & Raftery : *« les skill scores de la forme (8) sont généralement impropres, même si la règle sous-jacente S est propre »* ; le *Brier skill score* n'est qu'**asymptotiquement** propre, et *« l'affirmation de Mason (2004) sur la propriété du Brier skill score repose sur des approximations injustifiées et est généralement incorrecte »*. **Normaliser un Brier par une baseline ne conserve pas la propriété.**

⚠️⚠️ **Et la limite qui tranche pour nous.** Toute cette théorie est définie sur une distribution prédictive et `p ∈ [0,1]`. **Un signal ordinal à trois valeurs non probabiliste n'est pas un objet auquel `S(P,x)` s'applique.** Deux options seulement : remonter à des probabilités calibrées — et alors les scores propres redeviennent applicables et servent de test de la calibration exigée par Elkan —, ou accepter qu'ils ne mesurent pas cet artefact.

**La chose à ne surtout pas faire : projeter `{NeedsReview, Corroborated, Contested}` sur `{0,1 ; 0,5 ; 0,9}` et calculer un score de Brier.** Le score mesurerait le choix arbitraire de projection autant que le modèle, et la propriété d'incitation à la sincérité ne survit pas à une projection arbitraire.

### 10.5 ⚠️ Le cas central : rendre une famille paramétrée par le ratio

**Provost & Fawcett, *Robust Classification for Imprecise Environments*, *Machine Learning* 42, 2001** ([PDF](https://arxiv.org/pdf/cs/0009007)) est la source la plus directement pertinente du ticket, parce qu'elle est **explicitement conçue pour le cas où coûts et distributions sont inconnus ou changeants** — c'est-à-dire le nôtre.

```
ec(FP,TP) = p(p)·(1−TP)·c(N,p) + p(n)·FP·c(Y,n)
pente d'iso-performance :  m_ec = [ c(Y,n)·p(n) ] / [ c(N,p)·p(p) ]
```

**Enveloppe convexe ROC (ROCCH)** : *« un classifieur est optimal pour certaines conditions **si et seulement si** il se situe sur la frontière nord-ouest de l'enveloppe convexe »*. L'enveloppe est linéaire par morceaux, concave, à pente monotone non croissante.

**Le mécanisme exact pour des coûts inconnus :**

> *« La méthode s'**adapte gracieusement à tout degré de précision** dans la spécification des distributions de coût et de classe. **Si l'on ne sait rien**, l'enveloppe convexe ROC montre **tous les classifieurs qui peuvent être optimaux sous n'importe quelles conditions.** »*
>
> *« Une information de distribution imprécise définit une **plage de pentes**. Cette plage **intersecte un segment** de l'enveloppe convexe, ce qui **facilite l'analyse de sensibilité**. »*

Leur exemple travaillé montre les deux issues possibles : avec `1/5 ≤ m ≤ 1/2`, *« le choix du classifieur est **insensible** aux variations dans cette plage »* ; avec `1/2 ≤ m ≤ 3`, *« le choix est **très sensible** […] Les classifieurs A, C et E sont **chacun optimaux** pour un sous-intervalle. »*

**C'est exactement le livrable que le ticket décrit.** Au lieu d'un chiffre, on produit (i) **l'ensemble des montages possiblement optimaux**, et (ii) la réponse à « à partir de quelle imprécision sur le ratio le choix bascule-t-il ? ». Un résultat de la forme « pour tout ratio entre `r₁` et `r₂`, le même montage gagne » est robuste **sans jamais chiffrer le ratio**. Avantage annexe : *« comparer sous une nouvelle distribution implique seulement de calculer la ou les pentes correspondantes et de les intersecter »* — recalcul trivial quand la prévalence des erreurs dérive en production.

⚠️ **Mais le *rocch-hybrid* est RANDOMISÉ** (leur théorème 7 : choisir `C_r` avec probabilité `p/d`). Deux demandes identiques recevraient des priorités différentes. **C'est incompatible avec le déterminisme posé par l'ADR-0001, et probablement inacceptable en gouvernance RGPD.** La ROCCH reste néanmoins un excellent outil d'**analyse** — on ne déploie pas le *hybrid*.

Limites déclarées : deux classes seulement ; coûts uniformes à l'intérieur de chaque type d'erreur ; nécessite des estimations fiables de TP et FP.

### 10.6 Les métriques à écarter, et pourquoi

**`F_β`.** Van Rijsbergen 1979 ([chapitre 7](http://www.dcs.gla.ac.uk/Keith/Chapter.7/Ch.7.html)) définit formellement `β` comme le point d'**indifférence marginale** : *« l'importance relative qu'un utilisateur attache à la précision et au rappel est le ratio P/R pour lequel ∂E/∂R = ∂E/∂P »*. Chinchor, MUC-4 1992 ([PDF](https://aclanthology.org/M92-1002.pdf)) : *« β l'importance relative donnée au rappel par rapport à la précision […] Pour un rappel deux fois plus important que la précision, β = 2,0. »*

⚠️ **Le problème dirimant pour une file de relecture** ([Powers](https://arxiv.org/pdf/2010.16061)) : *« Recall ne concerne que la colonne +R et Precision que la ligne +P. **Ni l'une ni l'autre ne prend en compte le nombre de vrais négatifs** […] F₁ **ignore complètement TN, qui peut varier librement sans affecter la statistique**. »* Or dans notre cas, **les vrais négatifs sont les demandes correctement non signalées — c'est-à-dire la capacité de relecture économisée. C'est précisément ce qui compte.** Une métrique qui les ignore par construction ne mesure pas la bonne chose. Alternatives citées : *Informedness* (= `J` de Youden, `TPR + TNR − 1`), *Markedness*, coefficient de Matthews.

Hand, Christen & Kirielle, *Machine Learning* 2021 ([PDF](https://arxiv.org/pdf/2008.00103)) ajoutent que la moyenne harmonique *« n'a aucune interprétation comme probabilité »*, est plus proche de la plus petite des deux valeurs, et vaut zéro si l'une est nulle, *« ignorant la valeur de l'autre »*. Ils proposent `F* = F/(2−F) = TP/(FN+FP+TP)` — le coefficient de Jaccard, *« la proportion des classifications pertinentes qui sont correctes »*.

⚠️ **Lacune de source assumée.** L'argument selon lequel `F = pR + (1−p)P` avec `p = P/(P+R)`, donc **un poids qui dépend de la sortie du classifieur évalué** — ce qui rendrait la comparaison par `F_β` incohérente au même titre que l'AUC chez Hand 2009 — est attribué à Hand & Christen 2018, **qui n'a pas pu être ouvert** (Springer ; Semantic Scholar confirme `status: CLOSED`). Le papier F\* 2021 cite la source pour le « malaise sur la moyenne harmonique » mais **ne restitue pas cet argument**. **S'il devait fonder une décision, il faut obtenir le PDF Springer.** S'il tient, `F_β` n'est pas un moyen légitime d'exprimer un ratio de coûts inconnu.

**PR-AUC contre ROC-AUC : la littérature ne tranche pas.** Les **faits** sont incontestés :

- *Invariance de ROC* — Saito & Rehmsmeier, *PLOS ONE* 2015 ([article](https://journals.plos.org/plosone/article?id=10.1371%2Fjournal.pone.0118432)) : les tracés ROC sont *« inchangés »* entre jeux équilibrés et déséquilibrés, alors que dans leur exemple les faux positifs absolus passent de 160 à 1 600. **Une invariance mathématiquement réelle qui devient un piège quand ce qui coûte, ce sont les FP en valeur absolue** — le cas d'une file de relecture.
- *Baseline PR* : l'AUC-PR d'un classifieur aléatoire vaut `P/(P+N)`, pas 0,5. **Un PR-AUC ne s'interprète jamais sans sa baseline de prévalence.**
- *Interpolation* — Davis & Goadrich, ICML 2006 ([PDF](https://mark.goadrich.com/articles/davisgoadrichcamera2.pdf)) : correspondance bijective ROC↔PR (théorème 3.1) et équivalence des dominances (théorème 3.2), mais *« **l'interpolation linéaire dans l'espace PR n'est typiquement pas atteignable** »* et *« est une erreur qui produit une estimation exagérément optimiste »*. **Ampleur du dégât mesurée : AUC-PR de 0,031 avec l'interpolation correcte contre 0,50 avec la connexion linéaire. Facteur 16.**

La question **normative** (« faut-il préférer PR-AUC sous déséquilibre ? ») n'est pas résolue. **McDermott et al., NeurIPS 2024** ([PDF](https://proceedings.neurips.cc/paper_files/paper/2024/file/4df3510ad02a86d69dc32388d91606f8-Paper-Conference.pdf)) démontrent que la seule différence dépendant du modèle entre AUROC et AUPRC est une pondération par `1/P(f(x)>t)` — **pas le déséquilibre de classe**. Leur théorème 3 est directement pertinent : *« L'AUPRC favorise **prouvablement** les sous-populations à prévalence plus élevée […] dans les contextes où l'équité entre sous-populations importe, **l'AUPRC ne devrait pas être utilisée**. »* ⚠️ **Nos 7 droits ont des prévalences de 13 à 30 : l'AUPRC macro favoriserait mécaniquement `hors-perimetre` et `effacement`.** Revue de plus d'1,5 million d'articles : l'affirmation contraire est *« souvent faite sans citation, mal attribuée et agressivement surgénéralisée »*.

**Ce que personne ne conteste** : ni l'AUROC ni l'AUPRC ne sont des métriques de coût. Ce sont des agrégats à pondération implicite, et cette pondération n'est pas notre ratio.

### 10.7 Synthèse comparative des quatre familles

| Famille | Paramètre de coût | Sortie | Exige des probabilités calibrées ? | Applicable à 3 valeurs ordinales ? |
| --- | --- | --- | --- | --- |
| Seuil d'Elkan | `p*` (scalaire) | un seuil | **Oui, impératif** | Non directement |
| **ROCCH / Provost & Fawcett** | pente `m_ec` (**plage**) | **ensemble des montages possiblement optimaux + analyse de sensibilité** | Non — travaille sur des points d'opération | **Oui** (2 points internes) |
| **Courbes de coût** | `PC(+) ∈ [0,1]` | courbe, enveloppe inférieure, plage d'exploitation | Non | **Oui** |
| **Courbe de décision / bénéfice net** | `p_t ∈ (0,1)`, **élicitable** | courbe + lignes triviales | Non | **Oui** |
| Règles de score propres | `ν(dc)` implicite | scalaire + décomposition REL/RES/UNC | **Oui, par construction** | **Non** |
| `F_β` | `β²` nominal ; ratio implicite (**non vérifié**, § 10.6) | scalaire | Non | Oui, mais **ignore les TN** |
| ROC-AUC / PR-AUC | **aucun** — pondération implicite fixée par la métrique | scalaire | Non | Partiellement |

**Point structurel à ne pas manquer** : les trois familles « courbes » ne produisent, à trois niveaux, que deux ou trois points d'opération. **Cela ne casse rien** — la ROCCH et les courbes de coût sont définies pour des classifieurs binaires discrets — mais cela réduit la granularité de l'analyse de sensibilité. Conséquence à double tranchant : **la plage de ratios sur laquelle un même arbitrage reste optimal sera large. Bonne nouvelle pour la robustesse, mauvaise pour la finesse.**

## 11. Charge de travail et taux de déclenchement

Le `ReviewSignal` **priorise** la relecture, il ne la déclenche pas : un humain valide de toute façon. La question honnête est donc : *« ce témoin attrape X % des erreurs en faisant relire en priorité Y % des cas. »*

### 11.1 La formule qui relie les deux axes

**Ling & Li, KDD-98** ([PDF](https://cdn.aaai.org/KDD/1998/KDD98-011.pdf)). Pour un point ROC `(fpr, tpr)` et une prévalence d'erreurs `π` :

```
Yrate = tpr · π  +  fpr · (1 − π)
  ↑                    ↑
  % de la file       % d'erreurs
  relue en           attrapées
  priorité
```

⚠️ **Corollaire décisif : l'axe « % signalé » dépend de la prévalence des erreurs ; l'axe « % d'erreurs attrapées » n'en dépend pas.** Si le taux d'erreur du verdict change — par exemple parce qu'on remplace `qwen3:8b` — le couple (X %, Y %) se déplace **à performance intrinsèque constante**. Un énoncé « X % des erreurs pour Y % de relecture prioritaire » n'est **valide que pour la prévalence d'erreurs du jeu où il a été mesuré**. C'est la forme exacte de l'avertissement de Fawcett : précision, *lift* et F changent avec la distribution des classes *« even if the fundamental classifier performance does not »*, alors que `tpr` et `fpr` non.

En effectifs, Fawcett 2006 § 10 donne la formalisation primaire de « relire en priorité `k` dossiers » : `fpr · N_neg + tpr · P = k`.

⚠️ **scikit-learn n'expose ni *lift chart* ni *cumulative gain chart*** dans `sklearn.metrics` ([doc](https://scikit-learn.org/stable/modules/model_evaluation.html)) : il n'existe pas d'implémentation de référence du *lift*, il faut l'écrire.

Note complémentaire sur la stabilité : la précision@k est, selon [Manning, Raghavan & Schütze](https://nlp.stanford.edu/IR-book/html/htmledition/evaluation-of-ranked-retrieval-results-1.html), *« the least stable of the commonly used evaluation measures »*.

### 11.2 La courbe DET, et la question « le signal est-il calé au bon endroit ? »

**Martin, Doddington, Kamm, Ordowski & Przybocki, Eurospeech 1997** ([PDF](https://www.isca-archive.org/eurospeech_1997/martin97b_eurospeech.pdf)) ouvrent sur la phrase qui résume la Partie 2 :

> *« When there is a tradeoff of error types, **a single performance number is inadequate** to represent the capabilities of a system. Such a system has many operating points, and is best represented by a performance curve. »*

Axes : `P_miss` contre `P_fa` — **deux taux d'erreur**, *« giving uniform treatment to both types of error »* — sur une échelle de **déviation normale**. Propriétés utiles : la linéarité du tracé *« is a result of the assumed normality of the likelihood distributions »*, donc *« if the resulting curves are straight lines, then this provides a **visual confirmation** that the underlying likelihood distributions are normal »* ; et l'échelle *« moves the curves away from the lower left when performance is high, making comparisons easier »*.

**La recommandation qui vise exactement notre situation** : *« Confidence intervals, or a **confidence box**, around such points may also be included. »* Et surtout : la décision binaire dure du système est reportée sur la courbe, et *« the proximity of these points to the weighted average points […] is an indication of **how appropriately the system implementers chose the hard decision operating points** »*. **C'est mot pour mot la question « les trois niveaux du `ReviewSignal` sont-ils calés au bon endroit ? ».**

Le **DCF** du NIST ([plan d'évaluation SRE18](https://www.nist.gov/system/files/documents/2018/08/17/sre18_eval_plan_2018-05-31_v6.pdf)) formalise :

```
C_Norm(θ) = P_Miss(θ) + β · P_FA(θ),   β = (C_FA / C_Miss) · (1 − P_Target)/P_Target
C_Default = min{ C_Miss·P_Target , C_FA·(1−P_Target) }   ← le meilleur coût atteignable SANS traiter l'entrée
```

⚠️ **Et la distinction à retenir : `min-DCF` contre `actual-DCF`** — qualité de l'**ordre** contre qualité du **calage** des seuils. Ce sont deux défauts distincts avec deux remèdes distincts. Un témoin peut trier correctement et être mal calé sur ses trois niveaux ; les confondre conduirait à rejeter un bon témoin mal réglé.

### 11.3 Les courbes de gain — la littérature de la file relue par un humain

**Grossman, Cormack & Roegiest, *TREC 2016 Total Recall Track Overview*** ([PDF](https://trec.nist.gov/pubs/trec25/papers/Overview-TR.pdf)) est la source primaire la plus proche de notre situation : un humain relit une file, un système la trie.

> *« The principal tool for comparing runs was a **gain curve**. A gain curve plots **recall** […] **as a function of effort** […]. A run that achieves higher recall with less effort demonstrates superior effectiveness, especially at high recall levels. »*

> *« While gain curves and recall-precision curves convey similar information, they are **influenced differently by prevalence or richness** […] Total-Recall applications tolerate a fair amount of **fixed overhead** in exchange for high recall; **this tradeoff is more readily apparent in a gain curve**. »*

Leur métrique paramétrique — **rappel @ `aR+b`** — est un modèle de rendu honnête : *« The parameter a admits that it may be reasonable to review **more than one document for every relevant one** identified; the parameter b admits that it may be reasonable to review a **fixed number of additional documents**. »* Ils rapportent **toutes** les combinaisons `a ∈ {1,2,4} × b ∈ {0,100,1000}` — **une grille de points, pas un chiffre**.

⚠️ **Limite reconnue** : *« A gain curve or recall-precision curve is **blind to the important consideration of when to stop** a retrieval effort. »* D'où leur expédient du ***call your shot*** : le participant déclare **contemporainement** le point d'arrêt qu'il recommande, et l'on rapporte à ce point rappel, précision et F1. Transposé : le notebook devrait déclarer **avant de lire les chiffres** quel point d'opération il défend, puis rapporter la performance à ce point — c'est la protection minimale contre la sélection *a posteriori* que Cawley & Talbot décrivent (Partie 1 § 3).

Cormack & Grossman ([PDF](https://arxiv.org/pdf/1504.06868)) ajoutent : *« **Averages […] are generally inadequate to assess the reliability** »*, et proposent un ***differential plot*** comparant *« the recall achieved by one method with the recall achieved by another, **on the same topic, for the same review effort** »*. C'est l'analogue, en espace d'effort, du test apparié du § 9.1.

### 11.4 ⚠️ Le piège de vocabulaire : « couverture »

Ce qui se transpose du cadre sélectif (§ 8.1) : `g(x) = 1` ↔ « mis en tête de file » ; `Φ = E[g]` ↔ proportion signalée ; la borne empirique et la correction d'union `δ/k`.

**Ce qui NE se transpose PAS :**

- Le **risque sélectif** est le risque *sur la région acceptée*, sous la convention que la région rejetée n'est pas traitée. **Ici, un humain relit tout.** Il n'y a pas de région non couverte portant un risque non mesuré : le rejet ne supprime pas l'erreur, **il la retarde**. La quantité qui compte est le **rappel cumulé à l'effort**, pas `R(f,g)`. (C'est le même argument qu'au § 8.2 en faveur du risque *généralisé*.)
- Les résultats d'optimalité de la classification sélective portent sur un objectif — minimiser le risque des décisions rendues — **qui n'est pas le nôtre**.
- ⚠️ **Le mot est inversé et piégeux.** Chez El-Yaniv et Geifman, *coverage* = fraction traitée **par la machine**. Chez nous, la fraction signalée est celle envoyée **à l'humain en priorité**. **Employer « couverture » sans le redéfinir est une source d'erreur garantie** dans un notebook que d'autres liront. Autant lui donner un nom du domaine — *taux de déclenchement*, que la spec emploie déjà.

**Un cadre voisin, et pourquoi il ne s'applique pas non plus.** Mozannar & Sontag, ICML 2020 ([PDF](https://proceedings.mlr.press/v119/mozannar20b/mozannar20b.pdf)) formalisent l'apprentissage à déléguer : `L(h,r) = E[ 1{h(x)≠y}·1{r(x)=0} + 1{m≠y}·1{r(x)=1} ]`. **Déléguer signifie que la machine ne décide pas. Chez nous, l'humain décide dans 100 % des cas : le cadre dégénère si `r(x) ≡ 1`.** On priorise l'**ordre**, pas l'**allocation**.

En revanche leur **pratique de rendu est directement imitable** : ils rapportent **trois colonnes conjointement** — exactitude du système, couverture, exactitude du classifieur sur le non-délégué — chacune **avec son écart-type** (par exemple `96,29 ± 0,25 / 51,67 ± 1,46 / 99,2 ± 0,08`). **Jamais un seul chiffre.** Sur CheXpert, ils **contraignent** la couverture à `c` % pour chaque `c`, et tracent la performance système **en fonction de la couverture**, avec barres d'erreur sur 10 exécutions.

### 11.5 Rapporter honnêtement : points discrets, enveloppe, intervalles

**Trois valeurs = deux points non triviaux.** Fawcett : *« A discrete classifier produces only a single point in ROC space »* ; les points triviaux `(0,0)` (« ne rien signaler ») et `(1,1)` (« tout signaler ») sont nommés dans le texte, et *« one point in ROC space is better than another if it is to the **northwest** »*.

**Le test le plus économique que le notebook puisse faire.** Provost & Fawcett : *« E may be optimal because it extends the convex hull. Classifiers F and G never will be optimal because they do not extend the hull. »* ⚠️ **Traduction : si le niveau intermédiaire n'est pas sur l'enveloppe convexe, il est dominé — et le signal à trois valeurs se réduit opérationnellement à deux.** C'est le pendant, côté ROC, du test de recouvrement des SSLR (§ 8.4). Les deux répondent à la même question, et ils devraient être d'accord.

⚠️ **Le segment entre deux points n'est pas une mesure.** Fawcett § 10 donne la construction : `k = (FP_C − FP_A)/(FP_B − FP_A)` puis *« for each instance, generate a random number between zero and one. If the random number is greater than k, apply classifier A […] else pass the instance to B. »* **C'est randomisé** — donc incompatible avec le déterminisme de l'ADR-0001. **Le segment reliant deux points est une performance construite, pas mesurée ; le présenter comme une courbe lissée serait trompeur.**

**La variance est obligatoire.** Fawcett, sans détour :

> *« Some researchers have assumed that an ROC graph may be used to select the best classifiers simply by graphing them […] **This is misleading; it is analogous to taking the maximum of a set of accuracy figures from a single test set. Without a measure of variance we cannot compare the classifiers.** »*

**Quel intervalle binomial.** **Brown, Cai & DasGupta, *Statistical Science* 16(2), 2001** ([PDF](https://projecteuclid.org/journals/statistical-science/volume-16/issue-2/Interval-Estimation-for-a-Binomial-Proportion/10.1214/ss/1009213286.pdf)) — la référence que citait déjà `statsmodels` (Partie 1 § 4) :

- Recommandation : *« for small n (40 or less) […] **either the Wilson or the Jeffreys prior interval** […] For larger n (n > 40) […] the **Agresti-Coull interval** should be recommended. »*
- Clopper-Pearson : *« **very conservative; undesirably so for most practical purposes** »*.
- Démolition de Wald : *« the chaotic coverage properties of the Wald interval are **far more persistent than is appreciated** […] common textbook prescriptions regarding its safety are misleading and defective […] and **cannot be trusted**. »* Contre-exemples : à `n = 100`, couverture réelle 0,952 si `p = 0,106` mais **0,911 si `p = 0,107`** ; à `p = 0,5`, 0,953 pour `n = 17` mais **0,919 pour `n = 40`**.

⚠️ **Le piège de rendu propre à notre couple de chiffres.** Le rappel de l'alarme a pour dénominateur le **nombre d'erreurs du verdict** — petit, et c'est exactement là que Wald casse. Le taux de déclenchement a pour dénominateur 120. **Les deux moitiés du couple n'ont pas la même précision, et un rapport honnête doit le montrer** : intervalles séparés, Wilson ou Jeffreys sur le rappel (petit `n`), Agresti-Coull sur le taux de déclenchement (`n = 120 > 40`).

Enfin, **correction d'union** sur les points multiples : `δ/k` (Geifman & El-Yaniv, théorème 3.2). Avec deux ou trois niveaux, `k` est petit et la correction bénigne — **mais elle doit être faite, et dite**.

### 11.6 Ce sur quoi trois traditions indépendantes convergent

1. **Un couple de chiffres, jamais un seul.** Martin et al. : *« a single performance number is inadequate »*. Mozannar & Sontag : trois colonnes ± écart-type.
2. **Une courbe quand elle existe, une grille de points quand elle n'existe pas.** TREC rapporte 9 points de grille **plus** le point auto-déclaré.
3. **Toujours une mesure de dispersion.** Fawcett ; Martin et al. (« *confidence box* »).
4. **Nommer les hypothèses de coût et de prévalence** plutôt que les enterrer dans un score unique — c'est l'objet même du DCF et des droites d'iso-performance.
5. **Signaler l'instabilité** des mesures qui en souffrent (précision@k).

### 11.7 ⚠️ Ce que la littérature ne tranche pas

1. **Quand s'arrêter.** Explicitement non résolu par TREC (*« blind to the important consideration of when to stop »*), d'où l'expédient du « *call your shot* ».
2. **Bandes de confiance autour d'une courbe ROC.** Provost & Fawcett l'écrivent noir sur blanc : *« **To our knowledge, the issue is open** of how best to produce confidence bands appropriate to a particular problem. »*
3. **Savoir si un point est significativement meilleur que le hasard.** Fawcett : *« **There is no conclusive test for this.** »*
4. **Comment moyenner** des courbes de gain sur des tâches de prévalences différentes : TREC ne tranche pas ; Cormack & Grossman jugent les moyennes *« generally inadequate »* et proposent un *differential plot* — méthode ad hoc, pas un standard.
5. **Aucune source ouverte ne fixe de seuil**, de rappel acceptable ni de budget de relecture. Le DCF du NIST exige de **déclarer** `C_Miss`, `C_FA` et `P_Target` ; il ne les dérive pas. **C'est cohérent avec le périmètre de ce document : le taux de change est un jugement de valeur à expliciter, pas une constante statistique.**

---

# Ce qui reste ouvert

Recensement des points où la littérature primaire ne tranche pas, et où le choix devra donc être argumenté plutôt que cité.

**Sur le protocole (Partie 1)**

1. **IS contre SOIS contre *label powerset*** — Sechidis et Szymański se contredisent sur le régime de ratio 0,1-0,2, qui est le nôtre. Le fait local (9 combinaisons à un exemple) exclut le *label powerset*, mais l'arbitrage IS/SOIS reste ouvert et se mesure (LD, ED, FZ/FLZ, FLPZ).
2. **Le nombre de plis** n'a pas de réponse théorique à n = 120 (Bengio & Grandvalet). Deux faits locaux convergent vers 5 : la dégénérescence des étiquettes rares à k = 10, et la contrainte « ≥ k groupes » des 8 paires minimales.
3. **Aucune bibliothèque ne combine stratification multi-label et contrainte de groupes.** Il faut coder la contrainte à la main.
4. **La variance de la validation croisée n'a pas d'estimateur non biaisé** (théorème 6). Les trois nombres du § 4 ne se substituent pas les uns aux autres.

**Sur la mesure du témoin (Partie 2)**

5. **Le pont entre la littérature « prédiction sélective » (score fin) et la littérature « test diagnostique ordinal » (SSLR) n'est pas fait.** Le `ReviewSignal` tombe dans l'interstice ; c'est le trou le plus net rencontré.
6. **On ne peut pas prédire l'effet d'un troisième témoin à partir des seules corrélations par paires** — les théorèmes 1 et 2 de Kaniovski donnent deux réponses opposées, et les corrélations d'ordre supérieur sont inestimables en pratique. **L'apport se mesure, il ne se déduit pas.**
7. **La diversité ne prédit pas le gain** (Kuncheva & Whitaker, résultat négatif net). Les mesures de diversité sont un diagnostic de redondance, pas un prédicteur.
8. **Aucune métrique unique ne mesure l'apport marginal.** Les indices dédiés (NRI, IDI) sont réfutés — 18,8 % à 34,4 % de faux positifs **sur données de test indépendantes**, sous marqueur purement bruité. L'alternative recommandée (bénéfice net) est une courbe, dont les différences **ne se comparent d'ailleurs pas entre seuils**.
9. **Le désaccord marche empiriquement mieux que la théorie ne le justifie**, et personne ne sait pourquoi (Kirsch & Gal : *« they are not explained by the proposed calibration metrics »*).
10. **AURC contre AUGRC** : débat ouvert entre deux publications de 2023 et 2024.
11. **`DeclaredConfidence` ne correspond à aucune des trois notions de calibration** de Kull et al. dans un cadre multi-label. Toute mesure de calibration devra d'abord dire par rapport à quel événement elle est calculée.

**Lacunes de source à combler si elles doivent fonder une décision**

- **Hand & Christen 2018** — l'argument du ratio de coûts dépendant du classifieur dans `F_β` (Springer, accès fermé ; non vérifié en source primaire).
- **Efron & Tibshirani 1997** — le .632+ (JASA, mur payant).
- **Chow 1970**, **Dietterich 1998**, **DeLong et al. 1988**, **Pencina et al. 2008**, **Hilden & Gerds 2014** — restitués via des sources primaires ouvertes qui les exposent formellement, mais textes intégraux non ouverts.

---

## Notes de méthode

- Toutes les URL citées ont été ouvertes et lues. Les sources restées inaccessibles sont signalées comme telles et **ne sont jamais citées comme preuve** : leur contenu est restitué via une source ouverte qui l'expose formellement, et la restitution est identifiée à chaque fois.
- Les chiffres de la Partie 0 sont relevés directement sur `corpus/demandes-rgpd.fr.jsonl`.
- Les largeurs d'intervalles binomiaux du § 4 sont des applications des formules citées, données à titre illustratif.
- **Aucun seuil, aucune plage de coût, aucune valeur de β, aucun critère de pertinence n'est proposé ici.** C'est délibéré, et cohérent avec Vickers et al. pour qui le taux de change est un jugement de valeur à expliciter, pas une constante statistique. Ce document donne la matière ; #48 fait le choix.
