# Screening

Ce contexte ne connaît que **le temps d'avant** : la configuration, quand aucune demande d'exercice
de droits n'existe encore et que personne n'attend de réponse. Un `Operator` lui donne le relevé des
colonnes d'une base du client — il le **colle**, ou il laisse le service **scanner** la base et le
relever lui-même. Il lui rend, **colonne par colonne**, une catégorie de données présumée, le degré
de la règle qui l'a produite et un motif en prose française — que l'`Operator` **retient ou
écarte**, nommé et daté.

Il **détecte, il ne recense pas**, et cette distinction porte l'économie entière de ce contexte : la
détection des données personnelles est **réglée pour signaler large, quitte à se tromper souvent —
c'est à l'humain de trancher**. Elle ne conclut jamais ; elle rend des suspicions, un humain les
confirme, et elle ne couvre **que ce qu'elle a regardé**. C'est l'`Aide à la décision`, définie une
fois pour tout le dépôt dans [`CONTEXT-MAP.md`](../../../CONTEXT-MAP.md).

Il ne partage **rien** avec les deux autres contextes — pas même `DataSubjectRight`. Il ne touche
jamais au `Manifest` de [Casework](../casework/CONTEXT.md), et c'est une clause de ce glossaire, pas
une conséquence de l'architecture. Voir [`CONTEXT-MAP.md`](../../../CONTEXT-MAP.md).

Les identifiants du code sont en anglais ; les textes destinés à l'humain — libellés, messages,
documentation d'API — sont en français.

**On lance un `Screening`, et « scan » ne nomme pas ce geste-là.** Le geste central de ce contexte
n'a toujours qu'un seul mot. `Scan` a cessé d'être un mot interdit, mais il en nomme un **autre**,
plus petit et placé **avant** — voir `Scan`. La règle qui tient les deux séparés se teste : **une
phrase employant « scan » qui reste vraie sur le chemin collé emploie le mot à tort.** Le motif du
bannissement d'origine n'a rien perdu de sa valeur : un contexte qui a deux mots pour son geste
central en aura trois dans un an.

## Language

### Le relevé, d'où qu'il vienne, et ce qu'il devient

**ColumnListing** :
Le relevé : une ligne par colonne — table, colonne, type, commentaire, contraintes. Il a **deux
producteurs et un seul format**. Ou bien l'`Operator` le **colle**, l'ayant produit avec la requête
que le service lui fournit ; ou bien le service **scanne** la base et le relève lui-même. Rien en
aval ne distingue les deux : un `ColumnListing` scanné et un `ColumnListing` collé sont le même
objet, et la détection ne sait pas lequel des deux elle lit.
⚠️ **Jamais vérifié, quel que soit le chemin.** `Enregistré, jamais vérifié` vaut ici des deux côtés :
le service ne sait pas d'où vient ce relevé. Il en enregistre le nom de base — **recopié tel quel**
sur le chemin collé, **relevé par lui-même** sur le chemin connecté — sans jamais le vérifier ni s'en
servir pour identifier quoi que ce soit. C'est un repère pour l'humain qui relit un `Screening` trois
jours plus tard, jamais une identité sur laquelle bâtir une comparaison.
⚠️ **Se connecter ne vaut pas savoir où l'on est allé, et c'est contre-intuitif.** Une connexion
semble porter sa propre provenance ; elle ne la porte pas, pour deux raisons qui se cumulent. La
première est matérielle : la chaîne de connexion **ne survit pas au scan** — voir `Rien de réel ne
reste` —, donc le service ne détient plus, une seconde après, la moindre trace de l'hôte qu'il a
atteint. La seconde tiendrait même sans la première : une connexion sait quel **hôte** elle a joint,
jamais que c'était le **bon**. L'`Operator` qui saisit l'adresse de la recette obtient un relevé
sincère, entier et faux — exactement comme celui qui colle le relevé d'hier. Deux régimes de
provenance pour un même champ seraient deux règles pour un même fait, et la seconde finirait par
contredire la première.

⚠️ **Il est entier ou il n'existe pas.** La requête fournie **produit elle-même** le relevé plutôt
que de laisser un client SQL le mettre en forme : il déclare donc le SGBD dont il vient et le nombre
de colonnes qu'il porte, et une troncature au collage devient **détectable**. Un relevé dont il
manque un morceau est **refusé en bloc** — jamais ingéré en partie, si petite que soit la part
perdue. Motif : un `Screening` bâti sur 99 % d'un relevé **se lirait comme complet**, et l'`Omission
relue` repose entièrement sur le fait que le rapport de détection rend **toutes** les colonnes du
relevé. Trois colonnes que personne ne relira jamais, dans un artefact qui promet qu'on relit tout,
est la faille exacte que ce contexte existe pour ne pas avoir. Le coût est faible et réversible :
l'`Operator` relance sa requête, il ne perd aucun arbitrage.
Sur le chemin connecté la question ne se pose pas de la même façon — rien n'est retranscrit, donc
rien ne se tronque au collage — mais la règle est identique : un relevé de schéma qui échoue en
cours de route ne produit **aucun** `ColumnListing`, jamais un `ColumnListing` partiel.
⚠️ **Cette rigueur-là ne s'étend pas au `ColumnPreview`, et c'est délibéré.** Le schéma est tout ou
rien parce que l'`Omission relue` repose sur lui. Un aperçu manquant, lui, est **toléré** — il ne
retire aucune ligne du rapport de détection — à la seule condition d'être **nommé** manquant.

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
_Avoid_ : Schema, Catalog, Inventory, Dump, Export, Snapshot ⚠️ les cinq premiers sont
sur la liste _Avoid_ de `Manifest`, qui garde la clause « déclaré, non découvert » : les reprendre
ici ferait lire ce relevé comme un recensement du paysage du client, ce qu'il n'est pas.
⚠️ **`cartographie` a quitté cette liste, et le garde-fou qu'elle y tenait est remplacé par une
phrase.** Le mot nomme désormais quelque chose dans ce contexte, mais **pas ceci** : une
`Cartographie` est ce qu'un humain a arbitré, un `ColumnListing` est ce qu'une machine a relevé avant
que quiconque ait rien dit. Employer l'un pour l'autre ferait lire le relevé brut comme un travail
humain achevé.

**Scan** :
Le geste par lequel le service **relève lui-même** un `ColumnListing` : se connecter à la base du
client, lire son schéma, prélever quelques valeurs par colonne. Il s'arrête là. Ce qu'il produit est
un `ColumnListing` et un `ColumnPreview` par colonne — rien de plus, et surtout pas un `Screening`.
⚠️ **Il ne nomme pas le geste central, et une règle mécanique tient la frontière.** Lancer un
`Screening`, c'est faire **détecter** sur un `ColumnListing`, quelle que soit l'origine de celui-ci.
Scanner, c'est **fabriquer** ce `ColumnListing`, et seulement sur le chemin connecté. D'où le test :
**une phrase employant « scan » qui reste vraie sur le chemin collé emploie le mot à tort.** « Le
scan a mis quarante secondes » est faux quand on colle : le mot est bien employé. « Le scan a signalé
`ref_2` » est vrai quand on colle : il fallait dire « le `Screening` a signalé `ref_2` ».
⚠️ **Le mot a été interdit pendant toute la première vie de ce contexte**, au motif qu'« un contexte
qui a deux mots pour son geste central en aura trois dans un an ». Le motif n'a pas cessé d'être
juste ; ce qui a changé, c'est qu'il y a désormais **deux gestes**, et que le second n'avait pas de
nom. La réadmission ne tient qu'aussi longtemps que le test ci-dessus est appliqué.
⚠️ **Il n'existe pas sur le chemin collé**, et ce n'est pas une omission : là, l'`Operator` fait
lui-même, hors du service et avec la requête qu'on lui fournit, ce que le scan ferait pour lui.
_Avoid_ : Crawl, Discovery, Probe, Introspection, sondage, exploration ⚠️ `Discovery` et
`exploration` promettent que le service cherche ce qu'il ne sait pas d'avance trouver, alors qu'il
lit un schéma puis *n* colonnes nommées ; `Probe` et `sondage` promettent un prélèvement méthodique
visant à conclure, ce que le `ColumnPreview` n'est pas.

**ColumnPreview** — « aperçu » :
Les quelques valeurs — **cinq**, à la livraison — que le `Scan` a lues dans une colonne, et que
l'écran montre à l'`Operator` **pendant qu'il arbitre**. Elles ne prouvent rien, elles n'entrent dans
aucun chiffre rendu, et elles meurent : voir `Rien de réel ne reste`.
⚠️ **Un aperçu n'est jamais vide.** Il est **soit** des valeurs, **soit** une **raison nommée** de
n'en porter aucune — jamais rien. Deux raisons de nature différente coexistent : la colonne a été
**exclue** du prélèvement (un type binaire, dont cinq valeurs ne diraient rien à un humain), ou le
prélèvement a **échoué** (droits, délai, panne — toléré, à la différence du schéma). L'énumération
exacte des raisons relève de la spécification ; ce que le glossaire tient, c'est qu'il n'existe pas
de troisième forme et qu'aucune absence n'est muette.
Motif : c'est le geste du dialecte déclaré, appliqué un cran plus bas. Sans raison nommée, « cette
colonne ne contenait rien » et « on n'a pas regardé cette colonne » se liraient pareil à l'écran, ce
qui est l'`Omission silencieuse` réintroduite par une cellule vide. Et un aperçu et une raison posés
dans **deux champs** finiraient par se dissocier, comme finit toujours par se dissocier ce qu'un
chemin d'écriture peut écrire séparément.
⚠️ **`aperçu` a été un mot interdit et ne l'est plus, tandis qu'`échantillon` le reste**, et le
partage n'est pas une affaire de style. Un échantillon est un tirage dont la taille et la méthode
**portent une inférence** ; cinq valeurs choisies par rien n'en portent aucune, et le mot promettrait
la preuve que ce contexte refuse partout ailleurs — voir les listes _Avoid_ de `ScreenedColumn` et de
`RuleStrength`. Un aperçu, lui, dit ce qui a lieu : un humain regarde de ses yeux, et rien n'est
conclu. C'est aussi le mot avec lequel
[ADR-0012](../../adr/0012-la-connexion-le-scan-et-les-echantillons-entrent-dans-screening.md) grave
sa frontière — l'aperçu en deçà, l'analyse de contenu au-delà. ⚠️ Cet ADR écrit « échantillon » d'un
bout à l'autre : il **précède** ce terme, et on ne réécrit pas un enregistrement d'archive.
⚠️ **Le nombre est un ordre de grandeur, pas un paramètre de doctrine.** Cinq, trois ou huit ne
changent rien. Cinq cents change tout : ce n'est plus un aperçu qu'un humain lit de ses yeux, c'est
de l'analyse de contenu, et c'est ce qui rouvre l'ADR-0012.
⚠️ **Toutes les valeurs d'un aperçu ne se valent pas devant une règle, et trois ne comptent nulle
part.** Une règle de forme se prononce sur ce qu'elle a **réellement lu** : une valeur `NULL`, une
valeur **tronquée** par le SGBD, et un **doublon** d'une valeur déjà comptée sont écartés du compte —
ni au numérateur, ni au dénominateur. Les trois exclusions ont un seul motif commun : **compter deux
fois la même observation, ou compter une observation qu'on n'a pas eue, est la façon la plus courte
de fabriquer une preuve**. Une valeur tronquée n'a pas été lue en entier, donc elle n'a pas été lue ;
cinq fois le même IBAN est un IBAN vérifié une fois, jamais cinq vérifications indépendantes ; un
`NULL` n'est pas une valeur. ⚠️ Conséquence à assumer et à dire : la troncature transforme des vrais
positifs en **silences** — un courriel coupé à deux cents caractères n'est plus un courriel — et
c'est l'aperçu affiché, coupure visible, qui rend la main à l'`Operator`.
⚠️ **Sa durée de vie est glissante, et son expiration n'est pas une troisième raison.** Deux heures
réarmées à chaque écran **portant des aperçus** — ni l'historique, ni l'archive, ni l'accueil ne les
prolongent —, sous un plafond absolu de douze heures depuis le scan. Le glissant suit le rythme réel
d'un arbitrage, qui se compte en heures ; le plafond empêche qu'un onglet oublié fasse d'un cache une
rétention. Voir `Rien de réel ne reste`.
⚠️ **Et quand la durée est écoulée, rien ne s'affiche « vide ».** L'expiration se dit **à l'échelle
du rapport** — « les aperçus ont expiré ; ils ne reviendront pas pour ce rapport » — et le bloc
d'aperçu **quitte l'écran entièrement**. Écrire « valeurs expirées » dans la case en ferait une
**troisième forme**, que la clause ci-dessus interdit. L'aperçu n'est pas devenu vide : il n'existe
plus. Et le message est vrai sans réserve — relancer un scan ferait reculer ce rapport-là, donc aucun
geste ne rend ses aperçus.
_Avoid_ : Sample, échantillon, ValueSample, extrait, Excerpt, Snippet, Peek ⚠️ `Sample` et
`échantillon` sont les mots qu'on écrira par réflexe, et c'est précisément pour cela qu'ils sont
nommés ici ; `extrait` et `Excerpt` supposeraient un tout dont on aurait pris une part
représentative, ce qui est la même promesse sous un autre habit.

**ScanProgress** — « avancement du scan » :
Ce qu'un `Scan` en cours donne à voir pendant qu'il court : la **phase** où il en est, et le **compte
réel** de cette phase. Il vit **en mémoire du processus**, n'est **jamais** persisté, et porte un
`ScanId` propre — distinct de tout `ScreeningId`, parce qu'un scan peut être abandonné ou mourir sans
avoir jamais produit de `Screening`.
⚠️ **C'est lui, le transitoire qui vit à côté.** Le scan est asynchrone, et un `Screening` n'a aucun
état — voir plus bas. L'avancement n'est donc pas porté par le `Screening` : il est un objet
**distinct**, qui naît avant lui et meurt quand l'écran d'attente cède la place au rapport.
⚠️ **Son compte est vrai, ou il n'est pas.** Une barre **par phase**, chacune avec son dénominateur —
la **table** pour les aperçus, puisque le prélèvement coûte une requête par table ; la **colonne**
pour la détection. Les phases qui précèdent le retour du catalogue n'affichent **aucune barre** :
avant lui, aucun dénominateur n'est honnête. Et **aucun total global**, parce qu'en produire un
reviendrait à décider d'avance qu'une table de prélèvement « vaut » *n* colonnes de détection, ce qui
est le pourcentage inventé — pire qu'aucune barre.
⚠️ **L'écran qu'il alimente couvre une phase de plus que le scan** : la détection, qui existe aussi
sur le chemin collé. Ce n'est pas une entorse au test, parce que le test porte sur l'**objet** : quand
on colle, il n'y a ni écran d'attente ni `ScanProgress`. La raison est que l'`Operator` n'attend pas
un `ColumnListing`, il attend un rapport, et le faire attendre deux fois pour un seul geste serait
une fidélité au vocabulaire payée par lui.
⚠️ **Il est un fait du service, pas d'une session.** Un seul en vol par déploiement : un second
`Operator` arrivant pendant un scan voit le même écran et le même compte, et se voit **refuser** un
second lancement, celui en cours étant nommé. Fermer l'onglet n'arrête rien.
⚠️ **Il ne survit ni au processus, ni à l'abandon, et les deux se disent.** Le service redémarré, il
n'existe plus : l'écran d'attente répond alors « ce scan n'existe plus », avec la relance sous la
main, et jamais une redirection muette — ce serait l'`Omission silencieuse` déplacée sur l'écran
d'attente. Reprendre là où il s'était arrêté est **impossible par construction** : la chaîne de
connexion n'a pas survécu au scan — voir `Rien de réel ne reste`. Un scan abandonné, lui, ne laisse
**aucun objet** : ni `ColumnListing`, ni `Screening`, ni entrée d'historique, et le rapport courant
ne recule pas.
_Avoid_ : ScanState, ScanJob, statut du scan, progression, pourcentage ⚠️ `ScanState` et `statut`
rangeraient parmi les états ce qui a été délibérément tenu à côté d'eux ; `ScanJob` promet une file
et des reprises, quand il n'y en a qu'un et qu'il ne reprend jamais ; `pourcentage` est le mot qui
fait naître le total global que cette entrée refuse.

**Screening** :
Ce que la détection a rendu sur un `ColumnListing` : une `ScreenedColumn` par colonne du relevé, et
l'agrégat de ce contexte. C'est le **rapport de détection** que l'interface nomme, et c'est **l'acte
et son résultat**, comme une `Qualification` — il n'existe pas d'objet « lancement » distinct de
l'objet rendu.
Il est **détenu** et vit plusieurs jours : un rapport de détection s'arbitre en plusieurs fois,
colonne par colonne. Son grain est le **déploiement**, jamais le dossier ; il n'écrit rien au
`EvidenceLog`, n'a aucune échéance et vit jusqu'à ce qu'un `Operator` le supprime.
⚠️ **Relancer ne fusionne pas.** Relancer un `Screening` en produit un neuf ; les arbitrages du
précédent ne sont pas repris. C'est un écart assumé au précédent de `Reservation`, dont les réserves
fusionnent précisément pour ne jamais détruire un arbitrage humain, et il coûte du travail humain
réel — d'où la `ScreeningEngineIdentity`, qui dit au moins **pourquoi** le nouveau diffère.
_Avoid_ : Report, Audit, Assessment, Inventory, Analysis ⚠️ `Report` et `Audit`
promettent un document figé là où l'objet est vivant et s'arbitre.
⚠️ **`Scan` a quitté cette liste sans cesser d'être interdit ici.** Le mot nomme maintenant un geste
propre — voir `Scan` —, mais il ne nomme **jamais** cet objet : un `Screening` n'est pas « un scan »,
et « le scan a signalé `ref_2` » est fautif parce que la phrase resterait vraie sur le chemin collé,
où aucun scan n'a eu lieu. C'est pour la même raison que cette entrée ne dit plus « un re-scan ne
fusionne pas » : elle le disait déjà à tort, avant même que le mot ne soit réadmis.
⚠️ **`cartographie` a quitté cette liste pour une raison voisine.** Le mot nomme ce que l'humain a
arbitré ; un `Screening` est ce que la machine a rendu. Voir `Cartographie`.

**ScreeningEngineIdentity** :
Le nom et la version que le moteur joint au `Screening` qu'il a produit — celle de ses règles, celle
du modèle servi le cas échéant. Elle ne sert qu'à l'humain qui relit un rapport de détection
plusieurs jours après l'avoir lancé, ou qui en compare deux : le domaine ne l'interprète **jamais**
et aucune réponse publique ne la porte. Décalque exact de `QualificationEngineIdentity`, retenue
comprise.
⚠️ **Elle se découpe en autant de morceaux qu'il y a de choses capables de bouger seules**, et c'est
tout son usage : l'humain qui compare deux rapports a besoin de savoir **lequel** a changé. Le moteur
retenu en porte trois — ses règles de nom, ses règles de forme, le gel de ses lexiques —, là où il
n'en portait que deux.
⚠️ **C'est aussi elle qui dit qu'un rapport n'a pas eu de valeurs à lire, et c'est le seul endroit où
ce fait est écrit.** Le même moteur, dans la même version, rend deux choses différentes selon que
l'aperçu lui a été donné ou non : une colonne `ref_3` dont les valeurs sont des IBAN est signalée sur
le chemin connecté et `Unflagged` sur le chemin collé. Sans mention, les deux rapports se
compareraient comme s'ils étaient comparables. L'identité déclare donc ses règles de forme
**inactives** quand aucun aperçu ne lui est parvenu.
⚠️ **Et c'est ici, jamais sur la ligne, que ce fait doit s'écrire.** Une `ScreenedColumn` ne retient
pas qu'on a lu des valeurs chez elle : à ce grain, ce serait dire quelque chose de la **donnée**, ce
qu'`Unflagged` interdit. Porté par l'identité, le même fait ne parle que du **moteur**, et il vaut
pour le rapport entier.
_Avoid_ : modèle, moteur, provenance, signature, version ⚠️ `signature` est prise par le geste d'un
humain, qui est la seule signature de ce dépôt.

**IScreeningEngine** :
Le port par lequel le domaine fait détecter les données personnelles d'un `ColumnListing`. Il ne
nomme aucun moteur : le domaine ignore s'il parle à des règles locales, à un modèle servi, ou à un
troisième moteur pas encore écrit.
⚠️ **C'est la couture de réversibilité d'[ADR-0004](../../adr/0004-moteur-de-depistage-en-csharp-sans-second-sidecar.md),
et non une couture de test.** Le banc a désigné un dictionnaire, l'ADR en a tiré que le moteur vit
en C# dans `Infrastructure` ; ce port est ce qui rend cette décision réversible — un moteur qui
reviendrait en Python serait une implémentation de plus, et `Core` ne bougerait pas. C'est aussi
pourquoi il est **asynchrone** alors que le moteur retenu est local et déterministe : une signature
synchrone obligerait un futur moteur servi à bloquer sur son propre transport, et cette dette-là se
paierait dans `Core`.
⚠️ **Il rend une ligne par colonne, ou il échoue.** Un rapport de détection partiel n'existe pas :
c'est la même clause que « il est entier ou il n'existe pas », vue du moteur.
⚠️ **Il reçoit les aperçus à côté du relevé, jamais dedans, et ils ne ressortent pas.** Le
`ColumnListing` est **persisté** : y loger des valeurs ferait tomber `Rien de réel ne reste` par le
plus court des chemins, et effacerait au passage la clause qui veut qu'un relevé scanné et un relevé
collé soient le même objet. Les aperçus entrent donc **à part**, et le chemin collé n'en fournit
simplement aucun. Rien ne les fait ressortir : ce que le port rend est un `ScreenedListing`, dont
aucun champ ne sait porter une valeur — le motif est de la prose bornée, et il ne cite jamais une
valeur lue.
⚠️ **Il n'y a pas de second port pour les valeurs.** Un moteur de formes appelé à côté de celui-ci
rendrait deux rapports à fusionner, donc un étage qui arbitre « ce que dit le nom » contre « ce que
disent les valeurs » — l'étage exact que le modèle refuse plus bas. Il n'y a qu'une détection, et
elle lit les deux.
_Avoid_ : Scanner, Detector, Classifier, Analyzer, ArbitrationEngine ⚠️ `ArbitrationEngine` donnerait
à une machine le mot réservé au geste de l'`Operator`, qui est seul à produire une issue.
⚠️ **`Scanner` tient, alors même que `Scan` a été réadmis**, et c'est le test du chemin collé qui le
dit : ce port travaille identiquement sur un relevé collé, où aucun scan n'a eu lieu. Le scan est le
geste du connecteur ; ce port, lui, détecte.

**ScreenedListing** :
Ce qu'un `IScreeningEngine` rend : une `ScreenedColumn` par colonne du relevé, dans l'ordre du
relevé, **et l'identité du moteur qui les a produites**.
⚠️ **L'identité voyage avec les lignes, elle ne se lit pas à côté.** Le moteur la *joint* à ce qu'il
rend ; un moteur servi apprend la version qu'on lui sert au moment où il répond, et une propriété
posée à côté de l'appel dirait la version configurée plutôt que celle qui a répondu.
⚠️ **Ce n'est pas encore un `Screening`.** Il y manque ce que le moteur n'a pas à décider :
l'identité du rapport de détection, l'instant du lancement, le nom de base et le dialecte. C'est le
geste qui assemble, jamais le moteur.
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
« `ContactDetails`, 0,72 » ne s'arbitre pas. C'est du texte qui meurt, lu tel quel et jamais
analysé.
⚠️ **Le motif ne cite jamais une valeur lue.** Un motif est **enregistré** et vit aussi longtemps que
le `Screening` ; une valeur recopiée dedans survivrait à l'aperçu qui l'a montrée, et `Rien de réel
ne reste` cesserait d'être vrai par le plus étroit des chemins. Le motif dit donc la **forme**,
jamais l'occurrence : « les valeurs lues ont la forme d'un courriel », et non « la valeur
`p.martin@exemple.fr` a la forme d'un courriel ».
⚠️ **Et un motif de forme reste arbitrable, parce que l'aperçu est à côté de lui.** Seul, il
demanderait à l'`Operator` de croire un comptage qu'il ne peut pas contredire — c'est le « signal
dérivé sans les valeurs » que l'ADR-0012 a écarté nommément. Ce qui le rend vérifiable, c'est que
l'écran montre le `ColumnPreview` sur la même ligne : l'`Operator` lit la forme affirmée **et** les
valeurs qui la portent, et peut dire non. Retirer l'aperçu de l'écran ne serait donc pas une
économie d'affichage, ce serait rendre un motif invérifiable.
⚠️ **Cette clause borne la conception, pas la durée de vie.** Elle interdit de concevoir un écran qui
sépare le motif de son aperçu ; elle ne fait **pas** de l'expiration de l'aperçu une péremption du
rapport. Une fois les valeurs disparues, un motif de forme **reste arbitrable**, l'écran le disant :
refuser la signature transformerait une expiration en rapport périmé, et la seule issue laissée —
relancer — détruirait le travail déjà signé. Ce qui demeure sous les yeux de l'`Operator` n'est
d'ailleurs pas rien, l'ADR-0012 retenant le nom de la table **en propre et en premier**.
Symétriquement, une ligne `Unflagged` n'a **pas** de motif : il n'y a rien à motiver, et c'est ce qui
distingue « rien vu » de « vu et écarté ».
⚠️ **Le motif ne dit jamais ce qui n'a pas déclenché**, et c'est plus qu'une économie de prose.
Écrire « aucune règle de forme n'a confirmé » sur une colonne signalée par son nom serait une phrase
**enregistrée**, qui vit des mois et qui se lira, un jour, comme « les valeurs avaient l'air propres »
— le quitus que tout ce contexte refuse de délivrer, et qu'`Unflagged` refuse déjà à sa propre
échelle. Le motif dit ce qui **a** parlé ; c'est l'aperçu, affiché à côté et périssable, qui laisse
l'`Operator` juger le reste. La seule exception est celle qui existe déjà, et elle est de nature
opposée : le motif nomme les catégories qu'un **autre déclenchement** a portées et que l'ordre
d'arbitrage a écartées.
⚠️ **Deux familles de règles écrivent dans le même motif, dans un ordre fixe : le nom d'abord, la
forme ensuite.** Il n'y a qu'un motif par ligne, et les phrases s'y suivent. L'ordre n'est pas
esthétique : le motif est **borné**, et un motif trop long est coupé — en le disant, mais coupé. Ce
qui doit survivre à la coupe est le signal que l'ADR-0012 retient **en propre et en premier**, celui
du nom.
⚠️ **Une règle de forme ne peut qu'ajouter un signalement, jamais en retirer un.** C'est la clause la
plus lourde de l'entrée, et elle se lit sur trois colonnes réelles. `commentaire` dont les valeurs
lues sont des courriels est **signalée** alors que son nom n'a rien dit : c'est le gain. `email` dont
les cinq valeurs lues sont vides **reste signalée**, et `date_naissance` dont les cinq valeurs lues
sont des identifiants techniques **reste signalée** : cinq valeurs ne disent rien des trois millions
de lignes qu'on n'a pas lues, et les laisser éteindre un signalement reviendrait à délivrer un
certificat d'innocuité sur cinq lignes. C'est `Unflagged` vu depuis l'autre bout — celle-là refuse
d'affirmer l'innocuité d'une colonne, celle-ci refuse de la déduire d'un aperçu.
⚠️ **Il n'existe aucun étage qui arbitre « le nom » contre « les valeurs ».** Une règle de forme est
une règle **de plus**, versée au même sac que les règles de nom, et c'est l'ordre d'arbitrage de
`PersonalDataCategory` qui tranche, comme il l'a toujours fait. Un tel étage aurait été le seul
endroit du contexte où une famille de règles l'emporte sur une autre — c'est-à-dire la comparaison de
degrés que `RuleStrength` interdit, déplacée d'un cran.
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
⚠️ **Et les familles de règles l'héritent aussi, sans la moindre exception.** Une règle qui lit les
**valeurs** ne l'emporte pas sur une règle qui lit le **nom**, ni l'inverse : une colonne `numero`
que son nom rapproche d'`Identity` et dont les valeurs portent une clé d'IBAN rend `FinancialData`,
uniquement parce que `FinancialData` est plus haut au tableau. L'ordre décrit la **gravité de la
catégorie**, jamais la qualité de la règle qui l'a atteinte. Donner la préséance à une famille
rouvrirait, sous un autre nom, le classement des règles entre elles que la phrase précédente
interdit.

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
  signés et datés** qui vivent plusieurs jours, et qu'une relance ne reprend pas. Ce geste-là ne périme
  pas un contrat, il périme du travail humain.

_Avoid_ : DataCategory, catégorie, Label, Class, Tag, Type ⚠️ le raccourci `DataCategory` viendrait
frotter contre la liste de `DataSubjectRight` pour économiser huit caractères.

**Unflagged** :
La valeur rendue quand la détection n'a rien signalé sur une colonne. Exclusive : elle ne se combine
avec aucune autre.
⚠️ **Elle dit ce que le service n'a pas fait, jamais ce que la colonne est.** Une colonne `Unflagged`
n'est pas une colonne sans données personnelles — c'est une colonne où **rien n'a été vu**, ce qui
est un constat sur la détection et non sur la donnée.
⚠️ **La connexion n'y change rien, et rend même la clause plus nécessaire qu'avant.** On ne peut
plus dire que le service n'a jamais vu la donnée : il en a lu jusqu'à cinq valeurs. Mais cinq valeurs
ne disent rien du reste de la colonne, et une colonne dont les cinq lignes lues étaient vides ou
anodines demeure une colonne où **rien n'a été vu**. Le lecteur qui apprend que le service regarde
désormais les valeurs sera tenté de lire `Unflagged` comme un quitus : c'est très exactement ce que
cette valeur ne dit pas, et elle le dit d'autant moins que le prélèvement peut avoir été exclu ou
avoir échoué — voir `ColumnPreview`.
⚠️ **Deux colonnes très différentes rendent le même `Unflagged`, et c'est correct.** Celle où cinq
valeurs ont été lues sans que rien ne déclenche, et celle dont l'aperçu a été *exclu* ou a *échoué*,
portent la même valeur et **aucun motif** — seul l'aperçu affiché à côté les sépare, et il expire. La
tentation est alors de retenir sur la ligne que le service a bien lu cinq valeurs ici. Il ne le
retient pas : ce serait faire dire à `Unflagged` quelque chose de la **donnée**, quand elle ne dit
jamais que ce que la détection a fait. Or dans les deux cas, rien n'a été vu — elles **doivent** se
lire pareil. Ce qui distingue les deux rapports, lui, s'écrit à l'échelle du rapport, sur la
`ScreeningEngineIdentity`.
C'est `Enregistré, jamais vérifié` appliqué au seul endroit de ce contexte où il serait tentant de l'oublier,
parce qu'une machine qui déclare une colonne inoffensive est très exactement le témoignage qu'elle
n'a pas les moyens de porter.
⚠️ **Elle n'est pas le repli `PersonalDataUncategorised`**, et les confondre coûterait cher : celle-ci
dit « rien vu », celui-là dit « vu, personnel, mais aucune valeur ne va ». La règle qui les départage
est mécanique et se teste : **motif présent ⇔ ce n'est pas `Unflagged`.**
_Avoid_ : None, Unknown, Safe, Clean, NonPersonal, Negative, RAS ⚠️ tous affirment l'innocuité de la
colonne ; `None` et `Unknown` la feraient de surcroît lire comme une absence de valeur, alors qu'elle
en est une.

**PersonalDataUncategorised** :
Le repli : la valeur rendue quand la détection a reconnu une colonne comme **personnelle** sans
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
⚠️ **Aucune règle de forme n'y mène.** Une règle qui lit les valeurs sait d'avance quelle catégorie
elle vise — une clé d'IBAN vise `FinancialData`, et rien d'autre. Il n'existe pas de « forme reconnue
sans catégorie qui lui aille » : une forme qu'aucune catégorie n'attend est une forme qu'on n'a pas
écrite. Le seul chemin vers ce repli reste la règle du **conteneur libre**, qui ne dit pas qu'une
forme a été reconnue, mais que le schéma ne permet pas de lire.
⚠️ **Il n'y a pas de second repli.** Une valeur « non personnelle » a été explicitement écartée : elle
porterait sur la donnée un verdict d'innocuité que le service n'a pas les moyens de rendre : cinq
valeurs lues ne fondent rien sur le reste d'une colonne, et sur le chemin collé il n'en a lu aucune. `Unflagged` occupe cette place et dit la bonne chose, un constat sur la
détection et non sur la donnée. Voir la liste _Avoid_ d'`Unflagged`, où `NonPersonal` figure
nommément.
_Avoid_ : Other, Misc, Unclassified, Unknown, Generic, NonPersonal, divers, fourre-tout ⚠️ `Other` et
`Misc` en feraient une poubelle qu'on cesse de lire, alors que c'est la valeur qu'il faut lire en
premier ; `Unknown` en referait l'aveu d'ignorance qu'elle n'est pas.

**RuleStrength** :
Le degré de doute d'une `ScreenedColumn`, **dérivé de la règle qui a déclenché**. Externe et
déterministe : il ne doit rien à l'auto-évaluation d'un moteur. Il a **cinq** membres, et l'ordre
ci-dessous est celui dans lequel une règle parle le plus directement — le seul usage qu'on en fasse
jamais.

| Membre | Ce qui a déclenché |
|---|---|
| `ExactName` | le nom entier de la colonne figure au lexique |
| `CheckedValueForm` | les valeurs lues portent une **clé de contrôle** qui se vérifie |
| `Morphological` | une racine ou un affixe rapproche le nom d'une entrée du lexique |
| `ValueForm` | les valeurs lues ont une **forme** reconnue, sans clé de contrôle |
| `TypeHeuristic` | ni le nom ni ses valeurs n'ont parlé : le **type déclaré** de la colonne a parlé |

⚠️ **Les deux membres de forme ne se distinguent pas par le nombre de valeurs, mais par la clé**, et
c'est un résultat mesuré, pas une intuition. Un test de **forme** ne se multiplie pas sur cinq
valeurs : la forme est une propriété de la colonne, et « cinq sur cinq » n'y est qu'**une seule
observation affichée cinq fois** — d'où 81 % de faux positifs sur un code postal, qu'on en lise cinq
ou cinquante. Un test de **clé**, lui, se multiplie réellement : deux IBAN distincts valident à une
chance sur dix milliards. **Le nombre n'est donc pas le paramètre qui décide ; la présence d'une clé
l'est.**
⚠️ **`TypeHeuristic` parle du type déclaré au schéma, jamais de la forme des valeurs**, et la
confusion est facile parce que le mot « forme » va aux deux. Le type est ce que le SGBD annonce
(`jsonb`) ; la forme est ce que les valeurs montrent. Les fondre reviendrait à faire mentir le seul
membre qui existait déjà, et à rendre indistinguables une colonne qu'on n'a pas su lire et une
colonne dont on a lu le contenu.
⚠️ **Aucun membre ne varie avec le nombre de valeurs conformes.** Un degré qui monterait de « deux
sur cinq » à « cinq sur cinq » serait un ratio promu palier, c'est-à-dire un score par un autre
chemin — le même interdit que la clause suivante, vu depuis l'intérieur d'un membre plutôt qu'entre
deux membres.
⚠️ **Le degré ne dit pas combien de règles ont parlé, seulement laquelle a parlé le plus
directement.** Une colonne `iban` dont les valeurs sont des IBAN a été reconnue deux fois, par deux
familles ; elle porte pourtant `ExactName`, et son motif porte les deux phrases. Un membre
« corroboré » aurait fait dire au degré une **quantité de preuve**, ce qui est la définition du score
que ce type existe pour empêcher. L'`Operator` ne perd rien : ce qui a déclenché est écrit en toutes
lettres dans le motif.
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
une issue ; `Rejected` et `Ignored` diraient qu'on a jeté la ligne, alors qu'elle reste au rapport
de détection.

**La signature vit à côté de l'état, et aucun chemin d'écriture ne peut poser l'un sans l'autre.**
Qui a arbitré et quand vivent sur la `ScreenedColumn` elle-même — il n'y a pas de `EvidenceLog` ici, le
`Screening` n'en écrit aucune ligne et son grain est le déploiement. Il n'existe donc **ni `Retained`
ni `SetAside` non signé** : `Awaiting` est par construction le seul état sans signature, et une
signature manquante n'est pas un champ vide, c'est un état qui n'a pas eu lieu. Même mécanique que le
régime de `ReceptionDate`, et pour la même raison — deux champs qu'un chemin d'écriture peut dissocier
finissent par se dissocier.

**Un `Screening` n'a aucun état.** « Courant » est un **calcul** : le `Screening` le plus récent du
déploiement est le courant, tous les autres sont archivés par le seul fait qu'un plus récent existe.
Si `Archived` était un état, une transition ratée laisserait deux rapports de détection courants et
l'`Operator` arbitrerait le mauvais — même mécanique que le refus d'un état « en retard » dans
`Casework`, où le dépassement est un calcul pour que jamais un retard non détecté ne devienne un retard inexistant.
⚠️ **Le scan asynchrone n'y change rien, et c'est un choix de placement.** Il introduit bien un
transitoire — un scan court, puis n'existe plus —, mais ce transitoire vit dans un objet **distinct**,
le `ScanProgress`, qui naît avant le `Screening` et meurt avant lui. Le porter sur le `Screening`
aurait fait entrer un état par la porte de service, et l'aurait fait entrer sur l'agrégat même dont
l'absence d'état protège l'unicité du rapport courant.
L'avancement non plus n'est pas un état : « douze colonnes en attente » est un **compte** sur les
`ScreenedColumn`, jamais un état de haut niveau rassurant.

**Le geste de lot** — `ArbitrateInBatch` — existe pour qu'un rapport de détection de 5 000 colonnes
reste tenable : un écran intenable rétablit l'`Omission silencieuse` par épuisement, sans qu'aucune
ligne de doctrine n'ait été modifiée. Il est **borné à la table ouverte**, et à ses seules colonnes `Unflagged` encore
`Awaiting`.
⚠️ **Aucun geste de lot ne porte sur une colonne signalée** : une suspicion ne s'écarte jamais sans
avoir été lue une par une. La règle vit à un seul endroit, sur la ligne —
`ScreenedColumn.IsWithinReachOfABatchGesture` — et non dans la requête qui charge le lot, où elle
serait invisible à qui lit le domaine. Une ligne **déjà tranchée** est hors de portée elle aussi : le
lot liquide ce qui attend, il n'écrase pas sous un autre nom ce qu'un humain avait dit.
**Il pose n arbitrages individuels, jamais un état de lot** : chaque colonne atteinte porte sa propre
signature et sa propre date de service, exactement comme si elle avait été tranchée seule. Un
arbitrage partagé entre n lignes ferait de n actes un seul objet, et le premier réarbitrage individuel
le rendrait faux partout ailleurs. La commande nomme **une table**, jamais une liste de colonnes : une
liste de colonnes serait le seul chemin par lequel un formulaire forgé écarterait des colonnes
signalées en masse.
_Avoid_ : BulkArbitrate, ArbitrateAll, MassArbitrate, « valider tout » ⚠️ `All` ment sur la portée —
le geste n'atteint ni les signalées ni les déjà tranchées — et `Bulk`/`Mass` laissent croire à un état
de lot alors qu'il n'y a que n signatures.

**Cartographie** :
Le `Screening` **lu à travers les arbitrages de l'`Operator`** : les mêmes lignes, chacune portant ce
qu'un humain en a dit, avec son nom et sa date. C'est ce qui s'exporte, et c'est le seul artefact de
ce contexte qui sorte jamais du service.
⚠️ **Ce qui la sépare du `Screening` est l'auteur, jamais la sélection.** Un `Screening` est ce que la
machine a rendu ; une `Cartographie` est le même rapport une fois que l'humain a parlé. Elle porte
donc **toutes** les lignes — `Retained`, `SetAside` et `Awaiting` —, et un `SetAside` y est autant le
résultat du travail humain qu'un `Retained`. Une cartographie qui ne porterait que les `Retained`
serait le filtre que l'`Omission relue` interdit, déplacé du rapport vers l'export : elle se lirait
comme la liste **complète** des données personnelles du client, ce qu'aucun artefact d'ici ne peut
être.
⚠️ **C'est un calcul, jamais un objet enregistré.** Rien ne se persiste qui s'appelle une
cartographie : on lit les `ScreenedColumn` et leurs arbitrages. Même mécanique que « courant », que
l'avancement et que la sensibilité, et pour le même motif — un objet posé à côté de sa source serait
une seconde vérité sur les mêmes arbitrages, et le premier réarbitrage la rendrait fausse sans que
rien ne le signale.
⚠️ **Elle ne porte aucune valeur lue.** Un `ColumnPreview` meurt avec la session d'arbitrage et
n'atteint jamais l'export. Voir `Rien de réel ne reste`.
⚠️ **Le mot a été interdit sur `ColumnListing` et sur `Screening`**, à l'époque où rien ne sortait du
service et où il n'avait donc rien à nommer. Il désigne aujourd'hui le troisième terme de la
séquence — ce que la machine a relevé, ce que la machine a rendu, ce que l'humain en a fait — et les
deux entrées d'origine gardent chacune la phrase qui empêche de le confondre avec elles.
_Avoid_ : Inventory, Manifest, Registre, Catalogue, recensement ⚠️ `Manifest` et `Registre`
appartiennent à d'autres artefacts — celui de `Casework` et celui que le déployeur tient au titre de
l'art. 30 — et les emprunter ferait lire une cartographie comme une **déclaration**, ce qu'elle n'est
jamais : voir `Aucune modification vers le Manifest`.

### L'acteur

**Operator** :
L'humain, côté client, qui colle un `ColumnListing`, lance un `Screening` et arbitre ses
`ScreenedColumn`. **Seul** à produire une issue : le service signale, il ne retient ni n'écarte
jamais. Rien ne se déclenche sans lui — aucun processus périodique, aucune API publique de détection.
⚠️ **Homonyme assumé de l'`Operator` de `Casework`**, et rien de plus : même personne au bureau, même
mot au glossaire, pouvoirs différents et **aucun type partagé**. Il n'entre pas au noyau partagé, qui
vaut par sa petitesse et ne contient que `DataSubjectRight`. Factoriser un `Operator` commun serait la
première fissure dans cette clause, pour une économie nulle.
_Avoid_ : User, Agent, Admin, DPO, gestionnaire

### Ce qui entre, ce qui reste, et ce que le contexte ne fait pas

**Omission relue** :
Le régime d'erreur de ce contexte, et il n'est ni celui de `Qualification` ni celui de `Casework`.
L'erreur qui coûte ici est l'**omission**, comme dans `Casework` : une colonne portant des données
personnelles que la détection n'a pas signalée ne produit pas une ligne fausse, elle produit une
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

**Aucune modification vers le Manifest** :
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

**Ce qui entre est borné** :
Des données personnelles réelles **entrent** désormais dans ce contexte. Ce qui les tient n'est plus
une promesse générale, mais des bornes qui se vérifient une par une.
- Le service lit les noms de tables et de colonnes, les types, les contraintes et les commentaires —
  comme il l'a toujours fait, et sur les deux chemins.
- Il lit en plus, sur le chemin connecté seulement, **quelques valeurs par colonne** : cinq à la
  livraison. Voir `ColumnPreview`.
- La requête de prélèvement **nomme ses colonnes** une par une, depuis le schéma déjà relevé. Jamais
  d'étoile, jamais une table entière.
- Les **types binaires sont exclus** du prélèvement, et l'exclusion est **nommée** sur la ligne.
- Les **textes longs sont tronqués par le SGBD**, avant de traverser le réseau.
- Ce que le moteur fait de ces valeurs est borné aussi : il y cherche des **formes écrites d'avance**,
  jamais des mots. Aucun lexique ne s'applique aux valeurs.
⚠️ **Une règle de forme se juge à ce qu'elle rapporte, et certaines rapportent négativement.** Un
format sans clé de contrôle plafonne au taux de faux positifs de sa famille de colonnes, et quatre
sont **nommément écartés** parce qu'ils coûtent plus qu'ils ne rendent : le **code postal** (81 % de
faux positifs — une base réelle est pleine de codes produits, de codes INSEE de communes et d'années
× 100, tous à cinq chiffres), la **date seule** (95 à 98 % — une colonne date sur vingt à cinquante
est une date de naissance), les **coordonnées GPS seules** (50 à 70 %), et le couple **CNI/NEPH**
(indiscernables l'un de l'autre). Ils sont écrits ici pour la même raison qu'une liste _Avoid_ : sans
la trace de leur refus, quelqu'un les ajoutera dans six mois en croyant réparer un oubli. Le prix
d'une mauvaise règle n'est pas une ligne fausse de plus, c'est un `Operator` qui se met à survoler —
et l'`Omission silencieuse` rétablie par épuisement, sans qu'aucune ligne de doctrine n'ait bougé.
⚠️ **Un aperçu ne rend pas lisible ce que le schéma ne montrait pas.** Une colonne `jsonb` reste un
**conteneur libre** même quand on a lu cinq de ses valeurs : la règle qui la signale ne dit pas ce
qu'elle contient, elle dit que le schéma ne permet pas de le lire, et cinq valeurs n'y changent rien.
Ouvrir le document pour appliquer les règles champ par champ serait franchir la frontière que
l'ADR-0012 a tracée — on cesserait de regarder quelques valeurs comme un humain les lit pour
décortiquer la structure de la donnée du client. Et l'éteindre parce qu'on a lu des valeurs serait
laisser un aperçu **retirer** un signalement, ce que `ScreenedColumn` interdit.
⚠️ **Cette entrée remplace une clause renversée, et il faut le savoir en la lisant.** Ce contexte a
promis, pendant toute sa première vie, qu'« aucune donnée réelle n'entre » — pas de chaîne de
connexion, pas de socket vers la production du client, pas d'échantillon de valeurs, pas de sondage.
Les trois interdictions sont tombées par
[ADR-0012](../../adr/0012-la-connexion-le-scan-et-les-echantillons-entrent-dans-screening.md), qui
écrit ce que le renversement achète et ce qu'il coûte. Ce qui n'est **pas** tombé est la liste
ci-dessus, et l'entrée suivante.
⚠️ **Corollaire contre-intuitif, et il faut toujours le dire : ce n'est donc pas un NER.** Sa
prémisse d'origine — « `dt_naiss` n'est pas de la prose » — ne tient plus : une valeur de colonne
`commentaire`, elle, **est** de la prose. Le corollaire reste vrai pour une autre raison, et c'est
celle-là qu'il faut lire. Ce que le moteur reconnaît, ce sont des **formes**, pas des entités nommées
en contexte : `+33612345678` est un motif, reconnaissable par sa morphologie, sans modèle et sans
corpus d'entraînement ; « Madame Dupont a téléphoné » est une entité nommée dans une phrase, et ce
contexte n'y touche pas. Le service que rend ce corollaire — empêcher qu'on aille chercher des outils
calibrés pour un problème qu'on n'a pas — est plus utile qu'avant, parce que c'est **maintenant**, en
voyant des valeurs textuelles entrer, qu'un lecteur aura le réflexe d'y penser.
_Avoid_ : NER, échantillon, sondage de valeurs, scan de contenu, profilage ⚠️ ces cinq mots ont
survécu à la chute des trois interdictions, chacun pour une raison propre. `échantillon` et
`sondage de valeurs` promettent un tirage qui **porte une inférence**, ce que cinq valeurs ne font
pas — voir `ColumnPreview`. `scan de contenu` est celui qu'il faut garder **maintenant que `Scan` est
réadmis** : `Scan` nomme le geste, `scan de contenu` en nomme le franchissement — l'analyse du
contenu de la base du client au-delà de ce qu'un humain lit de ses yeux —, et c'est cette frontière
que l'ADR-0012 tient. `profilage` est un mot du RGPD (art. 4 § 4) et il désigne un traitement
automatisé visant à **évaluer une personne** : rien ici n'évalue une personne, on devine ce que
contient une **colonne**. L'employer ferait entrer le régime de l'art. 22 dans un outil qui n'y est
pas — une **erreur de droit** dans l'outil dont c'est le métier de ne pas en commettre.
⚠️ `connexion`, `chaîne de connexion` et `aperçu` ont quitté cette liste : ce sont désormais des mots
de ce contexte.

**Rien de réel ne reste** :
Ce qui entre ne se dépose nulle part. C'est la promesse que ce contexte tient à la place de celle
qu'il a perdue, et elle porte sur deux choses distinctes.
- **La chaîne de connexion ne survit pas au scan.** Elle vit en mémoire le temps du relevé. Jamais
  persistée, jamais journalisée, jamais tracée, jamais reprise dans un message d'erreur, jamais
  rejouée pour un second scan. Le service ne détient **aucun secret d'accès durable** à la base d'un
  client.
- **Les valeurs lues meurent avec la session d'arbitrage.** Un `ColumnPreview` vit dans un cache en
  mémoire du processus, avec une durée de vie **explicite, affichée et décomptée** : deux heures
  glissantes, réarmées par les seuls écrans qui montrent des aperçus, sous un plafond absolu de douze
  heures depuis le scan. Un redémarrage du service les efface : c'est un comportement à **dire** à
  l'`Operator`, pas un défaut à corriger. Elles n'apparaissent jamais dans un log, une trace, un
  message d'erreur, un motif de `ScreenedColumn`, ni dans la `Cartographie` exportée.
  ⚠️ **Le cache ne détient jamais qu'un seul jeu vivant** : quand un nouveau scan fait reculer le
  rapport courant, les aperçus du précédent sont évincés **sur-le-champ**. Une archive ne porte aucune
  valeur lue ; les garder en mémoire tiendrait en RAM ce que la base a le droit de refuser. C'est
  aussi ce qui rend inutile tout plafond en octets — cinq valeurs tronquées par le SGBD, pour cinq
  mille colonnes, pèsent quelques dizaines de mégaoctets, et il n'y en a qu'un jeu à la fois.
⚠️ **Ce sont deux promesses et non une, et elles ne se vérifient pas au même endroit.** Qui relit le
code du prélèvement contrôle ce qui **entre** ; qui relit le code de persistance contrôle ce qui
**reste**. Les fondre en une seule clause ferait un champ unique dont les deux moitiés finiraient par
diverger, et ce glossaire refuse ailleurs le même montage — voir la sensibilité et la signature.
⚠️ **Et elles se tiennent par un test, pas par une intention.** Un scan complet est joué contre une
base de fixture dont **toutes** les valeurs — et la chaîne de connexion elle-même — sont des
sentinelles improbables ; le test échoue si l'une d'elles apparaît dans un log, dans un attribut de
trace, dans le message ou la pile d'une exception traversante, dans la base après le scan, ou dans la
`Cartographie` exportée en JSON comme en CSV. Ces assertions restent **nommées séparément** : les
trois premières contrôlent ce qui **fuit**, les deux dernières ce qui **reste**, et les fondre en une
seule referait au banc d'essai le montage à champ unique que ce glossaire refuse dans le modèle.
⚠️ **Persister les aperçus pour éviter de rescanner a été explicitement écarté.** Ce serait faire du
service un détenteur durable de données personnelles du client, avec tout ce que cela entraîne :
chiffrement au repos, purge, droit d'accès sur nos propres sauvegardes. Le gain — ne pas relancer un
scan — est sans commune mesure avec le prix, et ce garde-fou n'est pas négociable.
_Avoid_ : rétention, conservation, cache persistant, archivage des valeurs, historique des valeurs
⚠️ tous supposent une durée que rien ici n'a ; « cache » employé seul est admis, parce qu'il dit
l'inverse — ce qui y entre en sort.
