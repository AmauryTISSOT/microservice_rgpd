# ADR-0025 — La détection passe à un modèle à plongements servi par Ollama, le lexique en repli de déploiement

- **Statut** : accepté
- **Date** : 2026-09-14
- **Décidé par** : le PRD [#460](https://github.com/AmauryTISSOT/microservice_rgpd/issues/460),
  tracé par [#461](https://github.com/AmauryTISSOT/microservice_rgpd/issues/461)
- **Glossaires** : [`CONTEXT-MAP.md`](../../CONTEXT-MAP.md), [Screening](../contexts/screening/CONTEXT.md)
- **Supplante, sur deux points nommés** :
  [ADR-0004](./0004-moteur-de-depistage-en-csharp-sans-second-sidecar.md) —
  - la clause 1, « le montage retenu est **règles + lexique FR+EN** » : le lexique n'est plus le
    moteur retenu, il devient le moteur d'un déploiement qui n'allume pas A2 ;
  - la clause 3, « le cas mixte (« règles plus un petit modèle de similarité ») est forclos par le
    coût mesuré de son composant » : un moteur à modèle de similarité entre dans le service. Ce
    n'est pas le cas mixte que la clause visait — les deux moteurs ne détectent jamais ensemble —,
    mais la clause forclosait tout moteur à modèle, et c'est ce qui tombe.
- **Garde** de l'ADR-0004 : **pas de second sidecar Python**, et la clause 2 — `IScreeningEngine`
  comme couture de réversibilité, dont cet ADR est le premier usage réel.
- **Tient** l'[ADR-0012](./0012-la-connexion-le-scan-et-les-echantillons-entrent-dans-screening.md) :
  le scan continue de prélever cinq valeurs par colonne pour l'aperçu, qu'A2 ne lit jamais.

## Contexte

La détection des données personnelles de `Screening` repose sur un seul moteur : des règles et des
lexiques FR+EN gelés, portés en C# dans `Infrastructure` par l'ADR-0004. Le banc qui l'a retenu lui
mesurait un F2 macro de 0,317, contre un plafond d'annotation de 0,839. Sur un rapport de détection
réel, l'`Operator` reçoit beaucoup d'omissions, qu'il ne rattrape qu'en relisant chaque ligne
`Unflagged` : l'`Omission relue` tient, mais elle tient par épuisement.

Un second banc, mené dans le cadre du mémoire (dépôt `recherche_schema_v2`), a produit un modèle
figé, **A2** — artefact `a2_c1_logreg`, commit de provenance `f0a2654` : plongements `bge-m3` servis
par Ollama, régression logistique, seuil publié. Il est mesuré à **F2 0,697** sur un hold-out de
douze schémas (rappel 0,828, précision 0,428).

Le service ne sait pas s'en servir. Aucun moteur ne parle à un encodeur, la taxonomie
`PersonalDataCategory` ne correspond pas aux prototypes du modèle, et l'ADR-0004 forclot tout moteur
à modèle. D'où cet ADR.

⚠️ **Les deux chiffres ne se comparent pas.** Le 0,317 de l'ADR-0004 est un F2 **macro** sur les six
plis de [#130](https://github.com/AmauryTISSOT/microservice_rgpd/issues/130), dans l'ancienne
taxonomie ; le 0,697 d'A2 est un F2 sur un **hold-out de douze schémas**, sur la décision « signalée
ou non ». Ni le corpus, ni la métrique, ni la taxonomie ne sont les mêmes. Cet ADR ne présente pas
l'écart comme un gain mesuré : il dit que le modèle a été mesuré nettement meilleur dans son propre
banc, et que le lexique, dans le sien, plafonnait bas. Un banc qui les départagerait sur le même
corpus reste à faire.

Le motif qui forclosait le modèle ne tient plus pour A2. L'ADR-0004 écartait le modèle **CPU** de son
banc — p95 29,1 ms/colonne sous contention, 131,7 s sur les 5 382 colonnes de Dolibarr, RSS 1,66 Gio
— en un processus Python à héberger. A2 n'est pas ce modèle : l'encodeur est servi par Ollama, que la
pile sait déjà démarrer pour `Qualification`, et ce qui reste au service — un produit scalaire et une
sigmoïde par colonne — est du C# sans dépendance. Son coût réel sur Dolibarr n'est pas encore mesuré :
voir plus bas.

## Décision

### 1. Deux moteurs derrière `IScreeningEngine`, un seul actif par déploiement

`Screening` porte **deux implémentations** du port `IScreeningEngine`, dont la signature ne change pas :

- le moteur **lexique** actuel — règles de nom, règles de forme, lexiques gelés —, intact dans son
  code, identité `regles-lexique-fr-en` ;
- le moteur **A2**, en C# dans `Infrastructure`, qui appelle Ollama par HTTP.

Le câblage lit **au démarrage** le drapeau `Screening:Embeddings:Enabled` et enregistre **l'un ou
l'autre**. Même règle que `Llm:Enabled` : absent vaut éteint, toute valeur autre que « true » ou
« false » arrête le démarrage. **Éteint est le défaut** : la pile démarre sans GPU ni téléchargement,
et c'est le lexique qui détecte, exactement comme avant.

⚠️ **Il n'y a aucun repli à l'exécution.** Aucun composant ne choisit entre les deux moteurs pendant
que le service tourne. A2 allumé et Ollama injoignable, lent au-delà de l'échéance, ou servant un
autre encodeur : **aucun `Screening` n'est produit**, l'écran le dit — refus nommé au dépôt collé,
fin d'échec nommée sur l'écran d'attente du scan, famille « moteur de détection indisponible » —, et
le rapport courant ne recule pas. Le lexique ne détecte jamais à la place d'A2.

Le motif est celui de la `ScreeningEngineIdentity` : deux rapports d'une même pile doivent venir du
même moteur, faute de quoi l'`Operator` qui en compare deux compare deux moteurs sans le savoir. Un
repli silencieux ferait changer de moteur un rapport dans son dos, et un repli affiché ferait de
chaque panne d'Ollama un rapport de moindre rappel que l'`Operator` arbitrerait comme l'autre.
« Repli » dans le titre de cet ADR s'entend donc **au déploiement** : l'exploitant qui ne peut pas
servir Ollama éteint le drapeau, et sa pile entière détecte au lexique.

### 2. A2 ne lit que le nom de la table et le nom de la colonne

Pour chaque colonne du `ColumnListing`, A2 encode le texte `table: {table} | column: {colonne}` —
gabarit lu dans le manifest de l'artefact. **Ni type, ni commentaire, ni contrainte, ni aperçu.** Les
aperçus du chemin scanné restent affichés à côté des lignes pour que l'`Operator` juge de ses yeux,
mais ils n'influencent jamais la détection.

Conséquence voulue : **un relevé collé et un relevé scanné donnent le même rapport A2** pour les mêmes
noms. Le moteur lexique, lui, garde la dissymétrie que sa `ScreeningEngineIdentity` déclare — règles
de forme inactives sans aperçu.

### 3. Le score est calculé, jamais exposé

Une colonne est signalée si `sigmoid(v · coef + intercept) ≥ seuil`, le seuil lu dans le manifest.
Elle porte alors la catégorie du **prototype le plus proche** (similarité cosinus maximale parmi les
quatorze de l'artefact), le degré `PrototypeProximity`, et un motif français qui cite `table.colonne`
et le texte du prototype. Sous le seuil : `Unflagged`, sans catégorie, sans motif, sans degré.

⚠️ **Le score ne sort pas du moteur** : ni à l'écran, ni dans le motif, ni dans l'export, ni en base.
Il n'est **pas calibré** (ECE 0,159) : un 0,72 affiché se lirait comme une probabilité qu'il n'est
pas. Ce motif suffit à lui seul. Il rejoint celui de `RuleStrength`, qui refuse tout score à la ligne :
`PrototypeProximity` dit **quel mécanisme** a parlé, jamais combien.

### 4. Les préconditions d'A2 sont vérifiées, pas supposées

Le modèle ne vaut que dans les conditions où il a été mesuré. Le service les tient toutes, et ne les
lit que dans le **manifest** de l'artefact, embarqué bit pour bit et vérifié par empreinte :

- **le même encodeur** : le digest `bge-m3` du manifest est comparé, **avant chaque détection**, à
  celui qu'Ollama déclare servir (`/api/tags`). Différent, le moteur échoue sans rien encoder : un
  autre `bge-m3` produirait un rapport sans valeur, que rien ne distinguerait du bon ;
- **le même gabarit de mise en texte** : lu dans le manifest, jamais en configuration ;
- **des vecteurs normalisés L2**, côté service, avant tout produit scalaire ;
- **une version d'Ollama compatible** avec la 0.34.0, avec laquelle l'artefact a été construit.
  L'image d'Ollama de l'AppHost doit l'être ; la mesure Dolibarr consigne celle qui a servi.

Le seuil, le digest, le tag, le gabarit, la dimension et les prototypes n'ont **aucune autre source**
que le manifest : aucune configuration ne peut les désaccorder.

### 5. La taxonomie `PersonalDataCategory` est remplacée

Elle devient les **quatorze prototypes d'A2 plus `Unflagged`** — quinze valeurs. Sont retirées
`CriminalOffenceData`, `SpecialCategoryData`, `ConnectionData` et `PersonalDataUncategorised`. Le
lexique y est ramené par une correspondance appliquée au chargement, sans réécrire ses lexiques
gelés : `ConnectionData` → `OnlineIdentifier`, `SpecialCategoryData` et `CriminalOffenceData` →
`DemographicData`, `PersonalDataUncategorised` (dont la règle du conteneur libre) →
`FreeTextAboutPerson`.

L'**ordre d'arbitrage** cesse d'être une propriété de la taxonomie. A2 n'en a pas besoin — il rend un
prototype, pas plusieurs déclenchements —, et l'imposer à un moteur qui ne le lit pas en ferait une
doctrine décorative. Le lexique garde un ordre **interne** pour départager ses propres déclenchements.

Le glossaire exigeait un ADR pour retirer ou renommer une valeur : c'est celui-ci.

### 6. Les `Screening` existants sont supprimés

La migration supprime toutes les lignes de `screenings` et de `screened_columns`. Aucune n'est
convertie : une catégorie retirée n'a pas de traduction honnête, et une archive qui porterait des
valeurs qui n'existent plus serait illisible.

## Ce que la bascule coûte

⚠️ **Ollama entre dans la pile de `Screening`.** Pour un déploiement qui allume A2, la détection
dépend d'un processus servi, d'un modèle tiré (`bge-m3`) et d'une échéance réseau, là où le lexique
démarrait avec le service. Ollama naît si `Llm:Enabled` **ou** `Screening:Embeddings:Enabled` est
allumé ; `bge-m3` seul ne réclame pas de GPU. Ce n'est pas un second sidecar — aucun code Python
n'est écrit pour `Screening`, et Ollama est déjà une ressource de la pile —, mais c'est une dépendance
d'exploitation nouvelle pour ce contexte, et une panne d'Ollama est désormais une panne de la
détection.

⚠️ **La taxonomie est remplacée, et le travail humain passé avec elle.** Les arbitrages signés et
datés des rapports existants sont perdus : l'historique est vide après la migration, et l'`Operator`
relance un rapport de détection en connaissance de cause.

⚠️ **Les art. 9 (hors santé) et 10 cessent d'être signalés comme tels — c'est la régression
principale.** `HealthData` reste. Mais `DemographicData` couvre religion et nationalité **sans
mention d'art. 9** : une colonne `confession`, que l'ancienne taxonomie rendait « catégorie
particulière (art. 9), motif : convictions religieuses », est désormais signalée comme donnée
démographique. Elle reste signalée — l'`Omission relue` n'est pas touchée —, mais le régime juridique
que la valeur disait n'est plus dit, et c'est à l'`Operator` de le reconnaître. De même, le lexique
ramène `CriminalOffenceData` à `DemographicData` : l'art. 10 n'a plus de valeur. Le glossaire tenait
que fusionner les deux articles serait « une erreur de droit dans l'outil dont c'est le métier de ne
pas en commettre » ; cet ADR ne les fusionne pas entre eux, il les **perd** tous deux dans une valeur
ordinaire, parce que le modèle mesuré n'a pas de prototype pour eux et qu'en recalculer sortirait de
l'artefact mesuré. C'est assumé et écrit ici pour ne pas être redécouvert.

⚠️ **`PersonalDataUncategorised` disparaît.** Le repli « vu, personnel, mais aucune valeur ne va »
n'a pas d'équivalent chez A2, qui rend toujours le prototype le plus proche. Son seul chemin chez le
lexique — le conteneur libre — trouve place dans `FreeTextAboutPerson`. Le taux de repli, que le
glossaire tenait pour l'instrument de mesure de la taxonomie, n'est plus mesurable.

⚠️ **Le temps de détection d'A2 sur un vrai schéma n'est pas encore mesuré.** Le dépôt collé est
synchrone, et A2 encode par lots via HTTP. La mesure sur Dolibarr (5 382 colonnes), **sur GPU et
sur CPU**, avec la version d'Ollama utilisée, est à consigner dans la section ci-dessous ; elle est
l'objet d'un ticket distinct. Si elle dépasse le budget du geste, rendre le dépôt collé asynchrone se
décide par une issue à part, pas ici.

## Mesure du temps de détection sur Dolibarr

_À consigner._ Colonnes : 5 382. GPU : —. CPU : —. Version d'Ollama : —.

## Alternatives écartées

| Alternative | Motif du rejet |
| --- | --- |
| **Remplacer le lexique par A2** | une pile sans Ollama — pas de GPU, pas de téléchargement, pas de réseau — ne détecterait plus rien. Le lexique reste le défaut, et le moteur de ces déploiements |
| **Repli automatique d'A2 vers le lexique à l'exécution** | un rapport changerait de moteur dans le dos de l'`Operator`, et deux rapports d'une même pile cesseraient d'être comparables ; une panne d'Ollama se lirait comme un rapport ordinaire de moindre rappel |
| **Faire détecter les deux moteurs ensemble** | deux rapports à fusionner, donc un étage qui arbitre l'un contre l'autre — l'étage que le glossaire refuse entre « le nom » et « les valeurs », déplacé entre deux moteurs |
| **Exposer le score** — à l'écran, dans le motif ou dans l'export | non calibré (ECE 0,159) : il se lirait comme une probabilité. Et un score à la ligne est ce que `RuleStrength` existe pour empêcher |
| **Faire lire à A2 le type, le commentaire ou les aperçus** | hors de l'artefact mesuré : le modèle n'a été ni entraîné ni évalué sur ces champs, et ses chiffres ne vaudraient plus |
| **Garder l'ancienne taxonomie et y ramener les prototypes** | il faudrait recalculer des prototypes alignés sur elle, donc sortir du modèle mesuré ; et `SpecialCategoryData` comme `CriminalOffenceData` n'ont aucun prototype d'où venir |
| **Convertir les `Screening` existants** | les catégories retirées n'ont pas de traduction honnête ; une conversion inventerait des arbitrages sur des valeurs que l'humain n'a pas vues |
| **Servir A2 dans un second sidecar Python** | l'ADR-0004 le refuse et cet ADR le garde : ce qui reste au service après l'encodeur est un produit scalaire, et l'encodeur est servi par Ollama, déjà dans la pile |

## Ce que cet ADR n'ouvre pas

- **Le réentraînement, le recalibrage ou un nouveau seuil** du modèle, ni des prototypes alignés
  sur l'ancienne taxonomie.
- **Une valeur pour l'art. 10 ou pour les catégories particulières de l'art. 9 hors santé.** La
  rétablir demanderait son propre ADR — et, pour A2, un artefact nouveau.
- **Le retrait du moteur lexique**, de ses lexiques gelés ou de `exploration/banc-screening`.
- **Le dépôt collé asynchrone**, suspendu à la mesure Dolibarr.
- **Une modification de `Qualification`** ou de `Llm:Enabled`, au-delà du partage de la ressource
  Ollama.
- **L'ADR-0012**, dont le prélèvement de cinq valeurs par colonne pour l'aperçu est inchangé.
- **Les ADR 0001 à 0024**, qui ne sont pas édités.
