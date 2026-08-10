# Screening

Ce contexte ne connaît que **le temps d'avant** : la configuration, quand aucune demande d'exercice
de droits n'existe encore et que personne n'attend de réponse. Un `Operator` lui colle le relevé des
colonnes d'une base du client ; il lui rend, **colonne par colonne**, une catégorie de données
présumée, le degré de la règle qui l'a produite et un motif en prose française — que l'`Operator`
**retient ou écarte**, nommé et daté.

Il **dépiste, il ne recense pas**. Le mot est pris au sens médical, et il porte l'économie entière de
ce contexte : un dépistage est calibré pour la sensibilité, il ne rend jamais un diagnostic mais des
suspicions, un humain confirme, et il ne couvre **que ce qu'il a dépisté**. C'est l'`Aide à la
décision`, définie une fois pour tout le dépôt dans [`CONTEXT-MAP.md`](../../../CONTEXT-MAP.md).

Il ne partage **rien** avec les deux autres contextes — pas même `DataSubjectRight`. Il ne touche
jamais au `Manifest` de [Casework](../casework/CONTEXT.md), et c'est une clause de ce glossaire, pas
une conséquence de l'architecture. Voir [`CONTEXT-MAP.md`](../../../CONTEXT-MAP.md).

Les identifiants du code sont en anglais ; les textes destinés à l'humain — libellés, messages,
documentation d'API — sont en français.

**On lance un `Screening`.** Le geste central de ce contexte n'a qu'un seul mot, et « scan » n'en est
pas un : ni endpoint, ni service, ni libellé. Un contexte qui a deux mots pour son geste central en
aura trois dans un an.

## Language

### Le relevé collé, et ce qu'il devient

**ColumnListing** :
Le relevé que l'`Operator` colle : une ligne par colonne — table, colonne, type, commentaire,
contraintes — produit par la requête que le service lui fournit. Le service ne se connecte à rien ;
l'humain colle, le service lit ce qu'on lui a mis dans la main.
⚠️ **Recopié tel quel, jamais vérifié ni complété.** `Greffier, pas témoin` vaut ici aussi : le
service ne sait pas d'où vient ce relevé. Il en enregistre le nom de base que le SGBD lui a donné
sans jamais le vérifier ni s'en servir pour identifier quoi que ce soit — c'est un repère pour
l'humain qui relit un `Screening` trois jours plus tard, jamais une identité sur laquelle bâtir une
comparaison.

⚠️ **Il est entier ou il n'existe pas.** La requête fournie **produit elle-même** le relevé plutôt
que de laisser un client SQL le mettre en forme : il déclare donc le SGBD dont il vient et le nombre
de colonnes qu'il porte, et une troncature au collage devient **détectable**. Un relevé dont il
manque un morceau est **refusé en bloc** — jamais ingéré en partie, si petite que soit la part
perdue. Motif : un `Screening` bâti sur 99 % d'un relevé **se lirait comme complet**, et l'`Omission
relue` repose entièrement sur le fait que le rapport rend **toutes** les colonnes du relevé. Trois
colonnes que personne ne relira jamais, dans un artefact qui promet qu'on relit tout, est la faille
exacte que ce contexte existe pour ne pas avoir. Le coût est faible et réversible : l'`Operator`
relance sa requête, il ne perd aucun arbitrage.

⚠️ **Ce qui reste hors de portée, en revanche, c'est la provenance.** Un relevé sincère, entier et
bien formé, mais tiré de la base de recette ou de celle d'hier, est indiscernable du bon. Aucun
mécanisme n'attrape ce cas, et aucun ne doit prétendre l'attraper : demander à l'`Operator` de
redéclarer d'où vient son relevé créerait une seconde source de vérité sur le même fait sans rien
vérifier, et donnerait l'illusion d'un contrôle qui n'a pas lieu.

⚠️ **Ce que le relevé ne porte pas dépend du SGBD, et cette absence-là est structurelle.** Un
`ColumnListing` a la même forme quel qu'en soit le SGBD, et un champ qu'un SGBD ne sait pas produire
y arrive vide — SQLite, par exemple, ne rend aucun commentaire. Sans le dialecte déclaré, « cette
colonne n'a pas de commentaire » et « ce SGBD n'en rend jamais » se liraient pareil, ce qui est
l'`Omission silencieuse` déplacée d'un cran ; avec lui, l'absence est **nommée**, et le `Screening`
peut dire qu'il n'a pas regardé un signal qui n'existait pas plutôt que de laisser croire qu'il l'a
regardé en vain.
Sa forme exacte — les neuf champs, l'en-tête, la ligne de fin et les neuf cas de refus — vit dans
[`pivot-format.md`](./pivot-format.md), parce qu'elle a deux producteurs (les requêtes par dialecte)
et un consommateur, et que des clés qui ne sont écrites nulle part en toutes lettres divergent.
_Avoid_ : Schema, Catalog, Inventory, Dump, Export, Snapshot, cartographie ⚠️ les cinq premiers sont
sur la liste _Avoid_ de `Manifest`, qui garde la clause « déclaré, non découvert » : les reprendre
ici ferait lire ce relevé comme un recensement du paysage du client, ce qu'il n'est pas.

**Screening** :
Ce qu'un dépistage a rendu sur un `ColumnListing` : une `ScreenedColumn` par colonne du relevé, et
l'agrégat de ce contexte. C'est **l'acte et son résultat**, comme une `Qualification` — il n'existe
pas d'objet « lancement » distinct de l'objet rendu.
Il est **détenu** et vit plusieurs jours : un recensement s'arbitre en plusieurs fois, colonne par
colonne. Son grain est le **déploiement**, jamais le dossier ; il n'écrit rien au `Ledger`, n'a
aucune échéance et vit jusqu'à ce qu'un `Operator` le supprime.
⚠️ **Un re-scan ne fusionne pas.** Relancer un `Screening` en produit un neuf ; les arbitrages du
précédent ne sont pas repris. C'est un écart assumé au précédent de `Reservation`, dont les réserves
fusionnent précisément pour ne jamais détruire un arbitrage humain, et il coûte du travail humain
réel — d'où la `ScreeningEngineIdentity`, qui dit au moins **pourquoi** le nouveau diffère.
_Avoid_ : Report, Scan, Audit, Assessment, Inventory, Analysis, cartographie ⚠️ `Report` et `Audit`
promettent un document figé là où l'objet est vivant et s'arbitre ; `Scan` est le doublon du geste,
banni pour cette seule raison.

**ScreeningEngineIdentity** :
Le nom et la version que le moteur joint au `Screening` qu'il a produit — celle de ses règles, celle
du modèle servi le cas échéant. Elle ne sert qu'à l'humain qui relit un rapport plusieurs jours après
l'avoir lancé, ou qui en compare deux : le domaine ne l'interprète **jamais** et aucune réponse
publique ne la porte. Décalque exact de `QualificationEngineIdentity`, retenue comprise.
_Avoid_ : modèle, moteur, provenance, signature, version ⚠️ `signature` est prise par le geste d'un
humain, qui est la seule signature de ce dépôt.

**IScreeningEngine** :
Le port par lequel le domaine fait dépister un `ColumnListing`. Il ne nomme aucun moteur : le
domaine ignore s'il parle à des règles locales, à un modèle servi, ou à un troisième moteur pas
encore écrit.
⚠️ **C'est la couture de réversibilité d'[ADR-0004](../../adr/0004-moteur-de-depistage-en-csharp-sans-second-sidecar.md),
et non une couture de test.** Le banc a désigné un dictionnaire, l'ADR en a tiré que le moteur vit
en C# dans `Infrastructure` ; ce port est ce qui rend cette décision réversible — un moteur qui
reviendrait en Python serait une implémentation de plus, et `Core` ne bougerait pas. C'est aussi
pourquoi il est **asynchrone** alors que le moteur retenu est local et déterministe : une signature
synchrone obligerait un futur moteur servi à bloquer sur son propre transport, et cette dette-là se
paierait dans `Core`.
⚠️ **Il rend une ligne par colonne, ou il échoue.** Un dépistage partiel n'existe pas : c'est la
même clause que « il est entier ou il n'existe pas », vue du moteur.
_Avoid_ : Scanner, Detector, Classifier, Analyzer, ArbitrationEngine ⚠️ `ArbitrationEngine` donnerait
à une machine le mot réservé au geste de l'`Operator`, qui est seul à produire une issue.

**ScreenedListing** :
Ce qu'un `IScreeningEngine` rend : une `ScreenedColumn` par colonne du relevé, dans l'ordre du
relevé, **et l'identité du moteur qui les a produites**.
⚠️ **L'identité voyage avec les lignes, elle ne se lit pas à côté.** Le moteur la *joint* à ce qu'il
rend ; un moteur servi apprend la version qu'on lui sert au moment où il répond, et une propriété
posée à côté de l'appel dirait la version configurée plutôt que celle qui a répondu.
⚠️ **Ce n'est pas encore un `Screening`.** Il y manque ce que le moteur n'a pas à décider :
l'identité du rapport, l'instant du lancement, le nom de base et le dialecte. C'est le geste qui
assemble, jamais le moteur.
_Avoid_ : ScreeningResult, ScreeningOutcome, Predictions, Findings ⚠️ `Outcome` est le mot de l'issue,
qui n'appartient qu'à l'humain ; `Predictions` promet un modèle et un score.

### La ligne, et pourquoi elles y sont toutes

**ScreenedColumn** :
Ce qu'un `Screening` dit d'**une** colonne du `ColumnListing` : sa `PersonalDataCategory`, la
`RuleStrength` de la règle qui l'a produite, un motif en prose française, et son état d'arbitrage.
⚠️ **Il y en a une par colonne du relevé, sans exception** — y compris là où le service n'a rien vu.
Ce n'est pas un détail de présentation : c'est le mécanisme entier de l'`Omission relue`. Une colonne
absente du `Screening` serait une colonne que personne ne relit jamais.
⚠️ **Le motif est obligatoire dès que la ligne est signalée**, sur le modèle exact de `Reservation`,
dont le glossaire dit qu'« une réserve **sans** motif est une panne du contrat, pas une réserve ».
« `adr_l1` → `ContactDetails`, degré bas, motif : préfixe `adr` reconnu » s'arbitre ;
« `ContactDetails`, 0,72 » ne s'arbitre pas. C'est de la prose de travail, lue telle quelle et jamais
analysée.
Symétriquement, une ligne `Unflagged` n'a **pas** de motif : il n'y a rien à motiver, et c'est ce qui
distingue « rien vu » de « vu et écarté ».
_Avoid_ : Finding, Hit, Detection, Match, Candidate, Suspect, alerte ⚠️ `Match` et `Candidate` sont
déjà refusés sur `Reservation` pour la raison qui vaut ici — ils promettent un rapprochement que le
service ne fait pas et un score qu'il n'a pas ; `Finding` et `Hit` supposeraient qu'une ligne non
signalée n'en est pas une, c'est-à-dire ré-introduiraient par le vocabulaire le filtre que
l'`Omission relue` interdit.

**PersonalDataCategory** :
La taxonomie fermée dans laquelle une `ScreenedColumn` puise. Elle reconnaît toujours une valeur —
l'absence de signalement est elle-même une valeur nommée, `Unflagged`, jamais une ligne absente.
⚠️ **Elle n'est pas `DataSubjectRight` et ne s'y raccroche jamais.** Une colonne « courriel » ne
relève pas d'un droit plutôt qu'un autre, elle relève de tous ; l'y rattacher ferait peser une
troisième dépendance sur le noyau partagé sans rien apporter. Elle est **propre à ce contexte**, et
son auteur n'est pas le RGPD.
⚠️ Le mot **catégorie** est sur la liste _Avoid_ de `DataSubjectRight` et le reste : le nom complet
est porté ici précisément pour que cette interdiction n'ait pas d'exception à gérer.

**Treize valeurs**, chacune portant son **nom canonique anglais** et son **libellé français attaché**
— décalque exact de `DataSubjectRight`, une seule source de vérité et aucune table de correspondance
parallèle. Le motif, lui, reste en prose française : il est écrit pour l'humain qui arbitre.

| Valeur | Libellé | Origine |
|---|---|---|
| `CriminalOffenceData` | données relatives aux infractions | RGPD art. 10 |
| `HealthData` | données concernant la santé | RGPD art. 9 |
| `SpecialCategoryData` | autre catégorie particulière | RGPD art. 9 |
| `AuthenticationSecret` | secret d'authentification | doctrinal |
| `NationalIdentifier` | identifiant national | CNIL, registre simplifié |
| `FinancialData` | données économiques et financières | CNIL, registre simplifié |
| `LocationData` | données de localisation | CNIL, registre simplifié |
| `ConnectionData` | données de connexion | CNIL, registre simplifié |
| `Identity` | état civil et identité | CNIL, registre simplifié |
| `ContactDetails` | coordonnées | CNIL, registre simplifié |
| `ProfessionalLife` | vie professionnelle | CNIL, ancien modèle |
| `PersonalDataUncategorised` | donnée personnelle sans catégorie | repli |
| `Unflagged` | rien signalé | repli |

⚠️ **La sensibilité est une valeur, pas une seconde dimension.** La CNIL coche ses neuf items
sensibles dans un **bloc parallèle** à ses six catégories ordinaires, et on pourrait croire qu'il
faut l'imiter — une colonne serait alors une catégorie **plus** un drapeau. C'est un artefact de son
**grain** : une fiche de registre décrit un traitement entier, où « identité **et** santé » coexistent
forcément. Notre grain est **la colonne**, et à ce grain la coexistence s'effondre : `confession` est
une conviction religieuse, elle n'est pas *aussi* de l'état civil. Un drapeau qui vaudrait vrai
exactement quand la catégorie est déjà l'une des trois valeurs de droit est un champ redondant — et
deux champs qu'un chemin d'écriture peut dissocier finissent par se dissocier. « Montre-moi les
colonnes sensibles » est donc un **calcul** sur la catégorie, comme « courant » est un calcul sur le
`Screening`.

⚠️ **L'art. 9 tient en deux valeurs, et l'énumération n'y porte pas l'item : le motif le porte.**
Ni une valeur unique, ni les huit du règlement. La santé se détache parce qu'elle cumule trois
raisons — c'est le seul des huit items fréquent dans un schéma réel, le seul dont le texte singularise
le régime (art. 9 § 2 h et i, § 3), et la première question d'un DPO. Les sept autres tiennent dans
`SpecialCategoryData`, et le motif nomme lequel : « `confession` → catégorie particulière (art. 9),
motif : convictions religieuses » dit à l'humain exactement ce que sept valeurs lui auraient dit,
sans que la taxonomie porte six engagements qui ne se déclenchent jamais. Le cas qui tranche est la
**biométrie** : le [considérant 51](https://eur-lex.europa.eu/legal-content/FR/TXT/HTML/?uri=CELEX:32016R0679)
ne la range à l'art. 9 que « aux fins d'identifier une personne de manière unique » — une **finalité**
qu'aucun lecteur de schéma ne connaît. Une valeur `Biometrics` affirmerait donc toujours plus que le
service ne peut savoir ; un motif peut dire que le régime dépend d'une finalité illisible ici.

⚠️ **L'art. 10 ne se replie pas dans l'art. 9**, alors même qu'il ne se déclenchera presque jamais.
Articles distincts, régimes distincts ; la CNIL maintient la distinction jusque dans un commentaire de
cellule de tableur — « font **également** l'objet de règles particulières », donc à côté et non
dedans. Les fusionner pour économiser une valeur serait une **erreur de droit** dans l'outil dont
c'est le métier de ne pas en commettre. Sa rareté n'est pas un argument contre son existence : c'est
un fait que la clause d'incomplétude a charge de dire.

**L'exclusivité est un invariant, et l'arbitrage est l'ordre de ce tableau, figé.** Une
`ScreenedColumn` porte **une** valeur. Quand plusieurs règles déclenchent — `arret_maladie` est santé
*et* vie professionnelle, `email_pro` est coordonnées *et* vie professionnelle — c'est l'ordre
ci-dessus qui tranche, du plus au moins coûteux à omettre. ⚠️ **Jamais la `RuleStrength`** :
comparer deux degrés pour désigner un gagnant serait un score qui produit une issue, ce que l'`Aide
à la décision` interdit. Le motif, lui, peut dire ce qui a été écarté — « la règle *vie
professionnelle* a aussi déclenché ». Les moteurs **héritent** cet ordre ; aucun ne le redécide.

**Sa gouvernance a deux étages, parce que ses valeurs n'ont pas toutes le même auteur.**
`DataSubjectRight` peut écrire qu'ajouter une valeur est une rupture de niveau ADR : il y a six droits
parce que le RGPD en ouvre six, et la clause emprunte sa solennité au règlement. **Cette taxonomie-ci
n'a pas cet appui** — le RGPD n'énumère nulle part les catégories *ordinaires*, l'art. 30 impose
l'exercice sans fournir le vocabulaire, et **toute** nomenclature ordinaire est donc doctrinale, celle
de la CNIL comprise. Se donner la même gravité sans le même fondement serait une posture.
- Les **trois valeurs de droit** — `CriminalOffenceData`, `HealthData`, `SpecialCategoryData` — sont
  fermées par le texte et ne bougent que s'il bouge.
- Les **valeurs ordinaires** sont un découpage de travail, révisable et sans autorité empruntée. En
  ajouter une n'est pas un ADR : c'est une PR dont le corps répond à trois questions — quelles
  colonnes réelles, dans quel schéma réel, ne trouvaient pas de valeur ; pourquoi
  `PersonalDataUncategorised` ne suffisait pas ; et où la valeur entre dans l'ordre d'arbitrage.
- ⚠️ **Retirer ou renommer une valeur reste un ADR**, et pour une raison qui n'est pas la même que
  chez `DataSubjectRight` : il n'y a pas d'appelant à casser ici, mais il y a des **arbitrages humains
  signés et datés** qui vivent plusieurs jours, et qu'un re-scan ne reprend pas. Ce geste-là ne périme
  pas un contrat, il périme du travail humain.

_Avoid_ : DataCategory, catégorie, Label, Class, Tag, Type ⚠️ le raccourci `DataCategory` viendrait
frotter contre la liste de `DataSubjectRight` pour économiser huit caractères.

**Unflagged** :
La valeur rendue quand le dépistage n'a rien signalé sur une colonne. Exclusive : elle ne se combine
avec aucune autre.
⚠️ **Elle dit ce que le service n'a pas fait, jamais ce que la colonne est.** Une colonne `Unflagged`
n'est pas une colonne sans données personnelles — c'est une colonne où **rien n'a été vu**, ce qui
est un constat sur le dépistage et non sur la donnée. Le service n'a jamais vu la donnée. C'est
`Greffier, pas témoin` appliqué au seul endroit de ce contexte où il serait tentant de l'oublier,
parce qu'une machine qui déclare une colonne inoffensive est très exactement le témoignage qu'elle
n'a pas les moyens de porter.
⚠️ **Elle n'est pas le repli `PersonalDataUncategorised`**, et les confondre coûterait cher : celle-ci
dit « rien vu », celui-là dit « vu, personnel, mais aucune valeur ne va ». La règle qui les départage
est mécanique et se teste : **motif présent ⇔ ce n'est pas `Unflagged`.**
_Avoid_ : None, Unknown, Safe, Clean, NonPersonal, Negative, RAS ⚠️ tous affirment l'innocuité de la
colonne ; `None` et `Unknown` la feraient de surcroît lire comme une absence de valeur, alors qu'elle
en est une.

**PersonalDataUncategorised** :
Le repli : la valeur rendue quand le dépistage a reconnu une colonne comme **personnelle** sans
qu'aucune autre valeur ne lui aille. Elle est signalée, donc elle porte un **motif**, et c'est ce
motif qui la sépare d'`Unflagged`.
⚠️ **C'est un verdict, pas un aveu d'ignorance** — même geste que `DataSubjectRight.OutOfScope`, dont
le glossaire dit « ce n'est ni "inconnu", ni "non classé" : c'est un verdict », et même formulation
que le seul repli formel de tout le corpus des outils, le `GENERIC_ID` de Google : « *may be*
personally identifying but do not belong to a well-defined category ».
⚠️ **Sans elle, le moteur n'a que deux issues et les deux mentent** : déguiser un doute en catégorie,
ou retomber sur `Unflagged` et affirmer « rien vu » alors que quelque chose a été vu. Le premier
mensonge est bruyant, le second est silencieux, et c'est le second qui coûte ici.
⚠️ **Le taux de repli est l'instrument de mesure de la taxonomie**, et il n'est pas un défaut à
minimiser : un taux qui monte est le signal qu'il manque une valeur. C'est ce que le banc compte, au
même titre que ce qu'il détecte.
⚠️ **Il n'y a pas de second repli.** Une valeur « non personnelle » a été explicitement écartée : elle
porterait sur la donnée un verdict d'innocuité que le service n'a pas les moyens de rendre — il n'a
jamais vu la donnée. `Unflagged` occupe cette place et dit la bonne chose, un constat sur le dépistage
et non sur la donnée. Voir la liste _Avoid_ d'`Unflagged`, où `NonPersonal` figure nommément.
_Avoid_ : Other, Misc, Unclassified, Unknown, Generic, NonPersonal, divers, fourre-tout ⚠️ `Other` et
`Misc` en feraient une poubelle qu'on cesse de lire, alors que c'est la valeur qu'il faut lire en
premier ; `Unknown` en referait l'aveu d'ignorance qu'elle n'est pas.

**RuleStrength** :
Le degré de doute d'une `ScreenedColumn`, **dérivé de la règle qui a déclenché** — correspondance
exacte, rapprochement morphologique, heuristique de type. Externe et déterministe : il ne doit rien à
l'auto-évaluation d'un moteur.
⚠️ **Il ne se confond pas avec la `DeclaredConfidence` de `Qualification`**, et c'est l'homonyme le
plus dangereux du dépôt : celle-là est une auto-évaluation qu'un moteur produit sur lui-même, et le
ticket #46 l'a mesurée **dégénérée** sur `qwen3:8b` — 118 `Low`, 2 `High`, aucun `Medium` sur 120.
La leçon est payée ; le vocabulaire la garde.
_Avoid_ : Confidence, DeclaredConfidence, Score, Probability, certitude, fiabilité ⚠️
`DeclaredConfidence` est listé nommément : il vit dans un autre contexte, où il veut à peu près
l'inverse.

### L'arbitrage, et sa signature

**États d'une `ScreenedColumn`** — `Awaiting`, `Retained`, `SetAside`.
`Awaiting` et `SetAside` sont repris mot pour mot de `ReservationState` : même geste, même sens, et
un synonyme inventé ferait croire à une nuance qui n'existe pas. `Attached` ne transporte pas — rien
n'est rattaché ici — et devient `Retained`.
⚠️ **Une colonne `Unflagged` est arbitrable comme les autres.** L'`Operator` peut la passer en
`Retained` de sa propre main, et c'est ce qui paye le fait de toutes les rendre : sans ce geste, les
lignes non signalées seraient neuf cents lignes grises qu'on survole, et l'`Omission relue` serait
décorative. Corollaire : un `Retained` posé sur une colonne `Unflagged` prouve qu'un **humain** l'a
retenue, jamais que le service l'avait vue.
_Avoid_ : Confirmed, Validated, Accepted, Rejected, Dismissed, Ignored ⚠️ `Confirmed` et `Validated`
feraient de l'`Operator` le validateur d'un avis de la machine, alors qu'il est le seul à produire
une issue ; `Rejected` et `Ignored` diraient qu'on a jeté la ligne, alors qu'elle reste au rapport.

**La signature vit à côté de l'état, et aucun chemin d'écriture ne peut poser l'un sans l'autre.**
Qui a arbitré et quand vivent sur la `ScreenedColumn` elle-même — il n'y a pas de `Ledger` ici, le
`Screening` n'en écrit aucune ligne et son grain est le déploiement. Il n'existe donc **ni `Retained`
ni `SetAside` non signé** : `Awaiting` est par construction le seul état sans signature, et une
signature manquante n'est pas un champ vide, c'est un état qui n'a pas eu lieu. Même mécanique que le
régime de `ReceptionDate`, et pour la même raison — deux champs qu'un chemin d'écriture peut dissocier
finissent par se dissocier.

**Un `Screening` n'a aucun état.** « Courant » est un **calcul** : le `Screening` le plus récent du
déploiement est le courant, tous les autres sont archivés par le seul fait qu'un plus récent existe.
Si `Archived` était un état, une transition ratée laisserait deux rapports courants et l'`Operator`
arbitrerait le mauvais — même mécanique que le refus d'un état « en retard » dans `Casework`, où le
dépassement est un calcul pour que jamais un retard non détecté ne devienne un retard inexistant.
L'avancement non plus n'est pas un état : « douze colonnes en attente » est un **compte** sur les
`ScreenedColumn`, jamais un état de haut niveau rassurant.

### L'acteur

**Operator** :
L'humain, côté client, qui colle un `ColumnListing`, lance un `Screening` et arbitre ses
`ScreenedColumn`. **Seul** à produire une issue : le service signale, il ne retient ni n'écarte
jamais. Rien ne se déclenche sans lui — aucun processus périodique, aucune API publique de dépistage.
⚠️ **Homonyme assumé de l'`Operator` de `Casework`**, et rien de plus : même personne au bureau, même
mot au glossaire, pouvoirs différents et **aucun type partagé**. Il n'entre pas au noyau partagé, qui
vaut par sa petitesse et ne contient que `DataSubjectRight`. Factoriser un `Operator` commun serait la
première fissure dans cette clause, pour une économie nulle.
_Avoid_ : User, Agent, Admin, DPO, gestionnaire

### Ce que le contexte ne fait pas

**Omission relue** :
Le régime d'erreur de ce contexte, et il n'est ni celui de `Qualification` ni celui de `Casework`.
L'erreur qui coûte ici est l'**omission**, comme dans `Casework` : une colonne portant des données
personnelles que le dépistage n'a pas signalée ne produit pas une ligne fausse, elle produit une
absence. Une ligne signalée à tort, elle, coûte peu — l'`Operator` l'écarte d'un geste, et c'est
l'`Erreur relue`.
Mais contrairement à `Casework`, cette omission est **relisible**, et elle ne l'est que par un
mécanisme précis : le `Screening` porte une `ScreenedColumn` par colonne du `ColumnListing`, y
compris `Unflagged`, et chacune est arbitrable. Le manque redevient alors une ligne **visible** —
exactement le geste que `Qualification` a fait en nommant `OutOfScope` plutôt qu'en rendant un
ensemble vide.
⚠️ **Corollaire non négociable : un `Screening` qui n'affiche que les colonnes signalées cesse d'être
ce contexte.** Filtrer les `Unflagged` — dans l'API, dans l'écran, dans une pagination par défaut —
rétablit l'`Omission silencieuse` sans qu'aucune ligne de doctrine n'ait été modifiée.
⚠️ Ce que ce mécanisme **ne rattrape pas**, c'est ce qui n'était pas dans le `ColumnListing` : le CRM
en SaaS, les tableurs partagés, les journaux, les exports du service commercial. Le `Screening` le dit
à chaque rendu, et comme une propriété de la réponse, jamais comme une mention en pied de page.
_Avoid_ : faux négatif, angle mort, oubli, erreur bénigne, erreur rattrapable

**Suggéré, jamais déclaré** :
Le `Screening` ne touche **jamais** au `Manifest`. Il produit une suggestion qu'un humain lit
**pendant** qu'il déclare ses `DeclaredSystem` à la main : aucun pré-remplissage, aucun export, aucune
confrontation, aucun bouton. C'est le décalque inverse et explicite du « déclaré, non découvert » que
`Manifest` protège — ce qui est ici découvert le reste, et n'accède jamais au statut de déclaration.
Le motif est écrit dans le glossaire de `Casework` : un `Manifest` pré-rempli par une machine **se
lirait comme complet**, ce qui est l'`Omission silencieuse` sous sa forme la plus dangereuse. Et la
tentation est réelle, parce que le pont a l'air utile : un `Operator` qui vient d'arbitrer quarante
colonnes `Retained` va les ressaisir à la main.
_Avoid_ : export, pré-remplissage, prefill, import, synchronisation, rapprochement, réconciliation,
alimentation ⚠️ cette liste n'est pas du style : elle est le seul garde-fou qui accroche une revue de
code sur un `ScreeningExportService` ou un `POST /manifest/prefill-from-screening`, qui autrement
passeraient pour d'aimables raccourcis.

**Le nom, jamais la valeur** :
Le service lit des noms de tables et de colonnes, des types, des contraintes et des commentaires.
**Aucune donnée personnelle réelle n'entre**, et il n'existe aucun chemin par lequel elle entrerait :
pas de chaîne de connexion, pas de socket vers la production du client, pas d'échantillon de valeurs,
pas de sondage. Le service ne détient aucun secret d'accès à la base du client.
⚠️ **Corollaire contre-intuitif, et il faut le dire : ce n'est donc pas un NER.** Un NER s'entraîne
sur de la prose, et `dt_naiss` n'est pas de la prose. Ce qui opère sur des noms d'identifiants relève
du lexique, des règles et de la morphologie ; appeler la chose un NER ferait chercher des outils
calibrés pour un problème qu'on n'a pas.
_Avoid_ : NER, échantillon, sondage de valeurs, aperçu, connexion, chaîne de connexion, scan de
contenu, profilage
