# ADR-0013 — La clause d'incomplétude varie avec l'origine du relevé

- **Statut** : accepté
- **Date** : 2026-08-25
- **Décidé par** : [Carte — Le `Screening` se connecte, scanne et prélève](https://github.com/AmauryTISSOT/microservice_rgpd/issues/261), et le ticket qui a tranché les neuf décisions : [#282](https://github.com/AmauryTISSOT/microservice_rgpd/issues/282). La neuvième était **cet ADR** ; sa rédaction est [#289](https://github.com/AmauryTISSOT/microservice_rgpd/issues/289).
- **Supplante, sur un point** : [ADR-0012](./0012-la-connexion-le-scan-et-les-echantillons-entrent-dans-screening.md), qui a rendu **fausse** la partie 1 de la clause d'incomplétude — *« le service n'a jamais vu une seule valeur »* — tout en rangeant le contenu de la clause parmi ce qu'il **ne décide pas** : *« Cet ADR décide, il ne nomme pas »*, et sa section « Ce que cet ADR n'ouvre pas ». Cette réserve ne vaut plus : la **partie 1** est décidée ici. Tout ce que l'ADR-0012 décide par ailleurs reste en vigueur, et **son texte n'a pas été édité** — on supplante un ADR, on ne le réécrit pas, précédent de l'[ADR-0010](./0010-un-quatrieme-point-d-entree-la-qualification-a-sa-surface.md).
- **Aucune suite datée n'est appendue à l'ADR-0012**, et c'est délibéré : [#263](https://github.com/AmauryTISSOT/microservice_rgpd/issues/263) a refusé de réécrire un enregistrement d'archive alors même que l'ADR-0012 écrit « échantillon » d'un bout à l'autre. La suite de l'ADR-0006 existe parce que **trois** points morts s'y étaient accumulés sur des mois, hors de vue d'un lecteur qui l'ouvrirait seul ; ici le point est **unique**, il est nommé ci-dessus, et les deux ADR se rejoignent par la même carte. Le jour où l'ADR-0012 en portera un deuxième, la suite se justifiera d'elle-même.
- **Ne touche pas l'[ADR-0006](./0006-trois-points-d-entree-renommes-en-francais-identifiants-inchanges.md)**, qui gèle le texte de la **partie 4** — la relation au `Manifest`. Cette partie ne varie pas, et le gel tient.
- **Ne touche pas l'export.** [#269](https://github.com/AmauryTISSOT/microservice_rgpd/issues/269) a décidé que le fichier exporté ne porte **aucune** clause, sous aucune forme. Le périmètre de cet ADR est celui des **quatre écrans de détection**.
- ⚠️ **Il décide, il ne rédige pas.** La rédaction exacte des trois motifs réécrits, les libellés des quatre comptes, le contenu du répertoire de phrases interdites et l'entrée de glossaire de l'aperçu relèvent de **la spec du workflow connecté**. Celle-ci n'est pas un pas sur la route de la carte : [#270](https://github.com/AmauryTISSOT/microservice_rgpd/issues/270), qui en portait le périmètre, a été **clos hors périmètre** pour ce motif, et la spec se produit par `/to-spec` une fois la carte close. C'est là que ces textes s'écrivent, et #270 reste la liste de ce qu'elle doit couvrir. La migration, elle, relève de [#283](https://github.com/AmauryTISSOT/microservice_rgpd/issues/283), qui l'a tranchée sur les quatre changements en jeu — dont les **deux ajouts** décidés ici.

## Contexte

La clause d'incomplétude est ce que le rapport de détection dit de **ce qu'il n'a pas regardé**.
Elle porte quatre parties — le périmètre lu, le hors périmètre, les catégories hors de portée, la
relation au `Manifest` — et elle est une propriété de **toute** réponse rendant un `Screening` ou une
`ScreenedColumn`. Elle porte aussi sa propre règle de gouvernance, écrite dans
`IncompletenessClause.cs` :

> **Ajouter** un exemple hors périmètre est une **PR** : ça élargit la reconnaissance, c'est gratuit
> et ça ne trompe personne. **Retirer** un exemple, ou toucher au **périmètre lu**, aux **catégories
> hors de portée** ou à la **relation au `Manifest`**, est un **ADR** : ça rétrécit une reconnaissance
> déjà tenue à des `Operator`, ce qui est exactement l'érosion que l'`Omission silencieuse` redoute.

L'ADR-0012 a fait entrer la connexion, le scan et les échantillons de valeurs dans `Screening`. Sur
le chemin scanné, la partie 1 de la clause devient donc **fausse** : elle promet que le service n'a
lu qu'un relevé collé et n'a *jamais vu une seule valeur*. Le chemin scanné touche au **périmètre
lu** et aux **catégories hors de portée**, et à rien d'autre — deux des trois motifs d'ADR sur trois.

Deux faits ont élargi le grief au-delà de ce qu'il annonçait, et ils sont la vraie matière de cet ADR.

**Ce ne sont pas deux phrases, mais quatre.** La phrase « le service n'a jamais vu une seule valeur »
vit à **cinq** endroits du dépôt, dont **deux affichés hors de la clause** : le `hint` sous le
compteur *Signalées*, sur `Report.cshtml:71` **et** sur `Archive.cshtml:65`, plus deux commentaires
XML (`ScreeningCounts.cs:28`, `ScreeningTally.cs:16`). La clause n'est donc **pas** le seul endroit
du produit où se dit ce que la détection n'a pas regardé, et le `hint` est lu **plus souvent**
qu'elle : il est en haut de l'écran et tient en deux lignes, là où la clause est en bas.

**Et rien ne porte l'origine du relevé.** `Screening` n'a aucun champ *collé / connecté*, et
`ScreeningCounts` — le seul argument du constructeur de la clause — n'en porte pas davantage. Toute
variante de texte exigeait donc un **fait neuf**, et c'est le vrai coût de cette décision.

**Le fond n'est pas procédural.** La règle existe contre l'érosion. Or élargir ce qui a été lu, c'est
**rétrécir l'incomplétude reconnue** : hier le service disait « je n'ai vu aucune valeur », demain il
dira « j'en ai vu cinq ». Il admet **moins** d'angles morts qu'avant. C'est très exactement le cas
que la règle vise, et non son contournement.

## Décision

### 1. Une clause, quatre parties, **une seule qui varie**

Ni une rédaction unique assez vague pour aller aux deux chemins, ni deux clauses complètes.

| Partie | Sort |
| --- | --- |
| 1 — le périmètre lu | **varie** avec l'origine du relevé |
| 2 — hors périmètre | constante |
| 3 — catégories hors de portée | constante — voir le point 6 |
| 4 — relation au `Manifest` | constante, et toujours gelée par l'ADR-0006 |

**Contre la rédaction unique** : elle coûte à l'`Operator` du chemin collé la promesse la plus forte
du produit — *le service n'a vu aucune valeur* —, laquelle reste **intégralement vraie** chez lui. On
ne paie pas la vérité du chemin neuf avec celle du chemin existant.

**Contre les deux clauses** : trois parties sur quatre n'ont aucune raison de diverger, et le jour où
l'on en corrige une, l'autre est oubliée.

⚠️ **La variance est portée par un type, jamais par un `if` sur un booléen.**

### 2. L'origine du relevé : une valeur fermée, **enregistrée**, et un cas nul refusé

`Collé` / `Scanné`, **enregistrée avec le rapport**.

⚠️ **Elle ne peut pas être une information de passage.** La clause est rendue jusque sur l'écran
d'archive, des mois après que la chaîne de connexion a cessé d'exister. Un rapport scanné archivé qui
rendrait la phrase du collé est très exactement la panne que cet ADR ferme, revenue par la porte du
temps.

⚠️ **Un booléen est refusé, et pas pour des raisons de style** : il ferait du collage le cas normal
et de la connexion l'exception, quand les deux chemins **coexistent** et qu'aucun ne remplace l'autre.

⚠️ **Un troisième cas, `non renseigné` en position 0, est refusé bruyamment** — à l'enregistrement
comme à l'affichage. Sans lui, `Collé = 0` est la valeur qu'on obtient **par accident** : six mois
plus tard, un chemin neuf crée un `Screening` sans renseigner l'origine ; le code compile, les tests
passent, le rapport s'enregistre — et affiche « celui que vous avez collé » sur un rapport scanné. La
panne serait rentrée par la porte de derrière, et **aucun témoin du point 8 ne la verrait**, puisque
de leur point de vue l'origine *est* `Collé`. Mettre `Scanné` en 0 ne fait que déplacer le mensonge ;
un commentaire n'arrête personne. C'est la mécanique que la clause applique déjà à ses comptes — elle
est inconstruisible sans les comptes de **ce** relevé ; elle devient inconstruisible sans son origine.

⚠️ **L'origine ne voyage pas dans `ScreeningCounts`.** Cet objet s'appelle *les comptes du rapport* ;
y ranger une non-quantité lui ferait perdre ce que son nom dit. La clause reçoit **deux choses**, les
comptes et l'origine, et ce qu'elle a besoin de savoir reste lisible dans sa signature.

### 3. La partie 1 sur le chemin scanné

**Chez le chemin collé, pas un caractère ne bouge.**

**Les valeurs rejoignent `ReadAsSignal`**, en cinquième puce. Elles ont servi à **deviner**, pas à
trier : une colonne `ref_3` dont le nom ne dit rien est signalée parce que ses valeurs portent une
clé d'IBAN. Les ranger dans `ReadOnlyToFilter` serait faux, et leur ouvrir une **troisième liste**
contredirait [#267](https://github.com/AmauryTISSOT/microservice_rgpd/issues/267), qui a tranché
qu'une règle de forme est *un `Trigger` de plus versé au même sac*.

**La queue de `FilterStatement` tombe, et rien ne la remplace.** La phrase se coupe exactement là :

> « Lus seulement pour écarter, jamais comme indice de sens : la forme d'une colonne ne dit pas ce
> qu'elle porte ~~, et le service n'a jamais vu une seule valeur~~. »

Le début reste vrai chez le scan — le type et la nullabilité n'y sont toujours pas des indices de
sens — et il **porte déjà sa propre justification**. ⚠️ **C'est la différence avec le point 7**, où
la queue porte la preuve et ne peut donc pas être coupée.

**Le texte chiffre : « cinq valeurs au plus », jamais « quelques ».** C'est la méthode du reste de la
partie 1, qui écrit « 4 980 colonnes dans 312 tables ». Un `Operator` qui lit « quelques » ne sait
pas si le service a lu cinq lignes ou cinquante mille, imagine le pire, et bloque — alors que le
chiffre exact est la **bonne** nouvelle du produit.

⚠️ **Le chiffre vient du même endroit que la requête de prélèvement.** Écrit à la main des deux
côtés, il divergerait au premier changement de borne, et le texte mentirait sans que rien ne rougisse.

### 4. Le biais du premier venu entre dans la partie 1

Porté par une **propriété distincte** de `ReadPerimeter`, pour la raison qui structure toute la
clause : qu'il se teste.

> « Ces cinq valeurs sont les cinq premières que la base a rendues, dans l'ordre où elle les a
> rendues : elles ne représentent pas la colonne. »

[#278](https://github.com/AmauryTISSOT/microservice_rgpd/issues/278) a établi les deux limites de
l'aperçu sans dire **où** elles s'écrivent. Elles ne sont pas de même nature :

| Limite | Portée | Où |
| --- | --- | --- |
| le premier venu | tout le rapport — une limite de la **lecture** | **la partie 1** |
| la coupure à 254 caractères | **une** valeur précise | **la ligne**, au contact de la valeur coupée |

⚠️ **Sans cette phrase, la puce neuve devient dangereuse.** Une table de deux millions de courriels
réels peut n'en montrer que cinq en `example.com` — jeux d'essai, comptes de démonstration, données
d'amorçage —, et l'`Operator` qui les lit comme représentatives **écarte une colonne qu'il fallait
retenir**. C'est une omission causée par l'écran lui-même, et la seule chose qui l'empêche est de la
dire.

**Sur le chemin collé, cette propriété est absente** — pas vide : absente. Il n'y a pas d'aperçu à
qualifier.

### 5. Quatre comptes de colonnes sans aperçu, un par famille — et la **raison survit à l'aperçu**

Sur le chemin scanné, `ReadPerimeter` porte **quatre comptes de plus**, un par famille close de
[#278](https://github.com/AmauryTISSOT/microservice_rgpd/issues/278) : type non prélevable, droits
refusés, aucune valeur retournée, lecture échouée.

**Quatre plutôt qu'un compte global**, et le motif vient de #278 lui-même : *« droits refusés »* vit
à part parce qu'elle est la seule que l'`Operator` puisse **corriger**. « 412 colonnes sans aperçu »
ne fait rien faire à personne ; « 18 par droits refusés » envoie l'`Operator` demander un accès à son
DBA et rescanner. Agréger, c'est éteindre la seule des quatre qui appelle un geste.

⚠️ **Les zéros s'affichent**, pour la raison exacte qui interdit un cas spécial au seuil zéro dans le
reste de la clause : un énoncé particulier quand il n'y a rien à dire rétablirait la réassurance un
cran plus haut.

⚠️ **Sur le chemin collé, les quatre comptes sont absents, jamais à zéro.** Rendre « 0 par droits
refusés » sur un relevé collé affirmerait qu'un prélèvement a eu lieu et n'a rien refusé — une
incomplétude inventée là où il n'y en a pas.

**Conséquence structurelle : la raison d'absence d'aperçu quitte l'aperçu éphémère et devient une
propriété enregistrée de la colonne.** Sans elle, le compte est **incalculable une heure après le
scan**, les aperçus mourant avec la session d'arbitrage. Un rapport afficherait quatre comptes le
matin et plus rien l'après-midi — et l'écran d'archive deviendrait **plus rassurant** que celui du
jour même, ce que ce dépôt interdit partout.

⚠️ **Ce n'est pas une entorse à `Rien de réel ne reste`.** Une raison n'est pas une valeur lue : c'est
un fait sur ce que le service **a pu regarder**, exactement la matière de cette clause.
`droits refusés` ne porte ni donnée du client, ni hôte, ni identifiant, et la règle de #278 — *la
prose ne cite jamais le message du pilote* — tient inchangée.

⚠️ **Mais elle amende [#263](https://github.com/AmauryTISSOT/microservice_rgpd/issues/263)**, qui
posait qu'« un aperçu est **soit** des valeurs, **soit** une raison nommée ». Les deux membres n'ont
plus la même durée de vie. L'interdiction de fond ne bouge pas — **un aperçu ne porte jamais des
valeurs *et* une raison** —, mais l'entrée de glossaire est à réécrire, et elle part avec la spec
[#270](https://github.com/AmauryTISSOT/microservice_rgpd/issues/270).

### 6. Les trois catégories hors de portée restent ; **leurs motifs sont réécrits**

La liste ne bouge pas. Les motifs, si — deux des trois ne parlaient que du **schéma** ou du **nom**,
et le chemin scanné les rend faux ou incomplets.

| Catégorie | Motif d'origine | État |
| --- | --- | --- |
| Données de santé | « une détection **de schéma** ne la verra pas » | **faux** — le scan voit des valeurs |
| Catégorie particulière | « c'est une **finalité** » | vrai, formulation à élargir |
| Infractions (art. 10) | « rien dans un **nom de colonne** » | vrai sur le fond, muet sur les valeurs |

**La santé reste hors de portée, mais plus pour la raison écrite.** Cinq valeurs d'une colonne
`notes` — `⟨null⟩`, `RAS`, `à rappeler`, `reporté par le patient`, `ok` — ne disent rien, et le
diagnostic est quarante mille lignes plus bas. La vraie raison est ailleurs : **aucune forme ne dit
« donnée de santé »**. Un IBAN porte une clé de contrôle, un courriel une arobase ; un diagnostic est
de la **prose**, et le moteur de [#267](https://github.com/AmauryTISSOT/microservice_rgpd/issues/267)
reconnaît des formes, jamais des entités nommées en contexte.

Les trois motifs réécrits nomment chacun ce que **ni le nom, ni le type, ni quelques valeurs** ne
peuvent atteindre : une **forme** qui n'existe pas, une **finalité** que rien ne déclare, une
**qualité du responsable** que rien n'annonce.

⚠️ **La faire sortir de la liste sur le chemin scanné aurait été le pire des trois choix** :
promettre qu'on sait voir la santé parce qu'on a lu cinq valeurs, dans la seule partie du produit qui
existe pour dire l'inverse.

⚠️ **Bénéfice de forme, et il n'est pas accessoire** : ainsi réécrits, les trois motifs sont vrais
des deux côtés, donc **la partie 3 reste constante** — ce que le point 1 exige. Et ils redeviennent
conformes à ce que la documentation de `CategoryBeyondReach` affirme déjà : *« l'une cache la donnée,
l'autre cache le régime, la troisième cache le responsable »*. La phrase était juste ; les rédactions
avaient dérivé.

### 7. Le compteur *Signalées* : la queue est **remplacée**, pas coupée

Sur `Report.cshtml` comme sur `Archive.cshtml`, sur le chemin scanné, le `hint` dit que le complément
est ce que la détection **n'a pas vu**, jamais ce qui serait inoffensif — **parce que** le service
n'a lu que cinq valeurs par colonne, et que cinq valeurs ne disent pas ce qu'une colonne contient.

⚠️ **Traitement inverse de celui du point 3, et c'est délibéré.** Là-bas, le début de phrase portait
sa propre justification et la queue était un ajout : on l'a coupée. **Ici, la queue *est* la preuve**
— ce qui interdit de conclure « les 4 933 autres colonnes sont inoffensives », c'est précisément le
fait qu'on n'a pas regardé leur contenu. La couper laisserait une affirmation sans son fondement, et
l'`Operator` referait le raisonnement interdit tout seul.

**Le remplacement dit la même chose que le biais du point 4**, au moment où l'`Operator` en a besoin :
en haut de l'écran, avant d'avoir déroulé quoi que ce soit, et non tout en bas.

**Les deux commentaires XML** (`ScreeningCounts.cs:28`, `ScreeningTally.cs:16`) suivent le même sort.
Ils ne s'affichent pas, mais ils sont le motif écrit d'un compte : laissés faux, ils réintroduiraient
la phrase au premier écran qu'on écrira en les lisant.

### 8. Le témoin : **deux niveaux, écrit par la négative, et symétrique**

**Niveau 1 — sur l'objet.** Construit avec l'origine `Scanné`, le périmètre porte les valeurs dans
`ReadAsSignal`, la mise en garde du premier venu et les quatre comptes ; avec `Collé`, aucun des
trois, et la phrase d'origine mot pour mot.

**Niveau 2 — sur l'écran, écrit par la négative.** Un répertoire des **phrases du chemin collé** —
« celui que vous avez collé », « n'a jamais vu une seule valeur » — dont aucune ne peut apparaître
dans quoi que ce soit d'affiché quand l'origine est `Scanné`.

⚠️ **Le niveau 1 seul aurait été vert pendant que l'écran ment.** Le `hint` du point 7 est écrit en
dur dans deux `.cshtml` : aucun test sur l'objet ne le verra jamais.

**La forme par la négative reprend le précédent d'`IncompletenessClauseTravelsWithEveryAnswerTests`**,
dont le commentaire dit pourquoi : *« une liste des gestes qui doivent porter la clause n'aurait
protégé que ce qu'on a pensé à y écrire ; celle-ci interroge tout ce que le contexte rend, et c'est
le geste qu'on ajoutera un jour sans y penser qui la fera rougir. »*

⚠️ **Et le témoin est symétrique — chaque origine a son répertoire interdit.** Un témoin qui ne
garderait que le chemin scanné laisserait passer une **correspondance inversée** : le code qui rend
la phrase du scan sur un relevé collé serait vert. La symétrie ne coûte rien et attrape la seule
panne qu'un test asymétrique ne peut pas voir.

## Conséquences

- **Le `Screening` porte un fait de plus, et il est enregistré** : l'origine du relevé. C'est le vrai
  coût de cette décision, et il se paie en migration — laquelle relève de
  [#283](https://github.com/AmauryTISSOT/microservice_rgpd/issues/283), dont la question « une
  migration ou deux ? » s'est posée sur **quatre** changements et non deux : deux retraits
  (`SignedBy`, le schéma enrichi) et **deux ajouts** — l'origine du relevé et la raison d'absence
  d'aperçu, tous deux décidés ici.
- **La colonne porte, elle aussi, un fait de plus et enregistré** : la raison d'absence d'aperçu.
  C'est la conséquence structurelle du point 5, et elle amende
  [#263](https://github.com/AmauryTISSOT/microservice_rgpd/issues/263) sur la durée de vie des deux
  membres d'un aperçu, sans toucher à l'interdiction de fond.
- **La clause cesse d'être un texte entièrement constant.** Elle l'était ; elle ne l'est plus sur sa
  partie 1. Trois parties sur quatre restent constantes, et c'est le point 6 qui les y maintient.
- **La clause n'est plus le seul endroit du produit qui dise ce qui n'a pas été regardé** — ce qu'on
  croyait avant ce ticket. Le `hint` du compteur *Signalées* le dit aussi, sur deux écrans, et il est
  lu plus souvent. Toute décision future sur ce que le service reconnaît ne pas avoir vu doit couvrir
  **les deux**, plus les deux commentaires XML qui les motivent.
- **Le témoin d'écran devient le garde-fou principal**, le témoin d'objet ne pouvant rien voir de ce
  qui est écrit en dur dans un `.cshtml`.
- **Rien de tout ceci n'atteint l'export.** [#269](https://github.com/AmauryTISSOT/microservice_rgpd/issues/269)
  tient : le fichier ne porte aucune clause, sous aucune forme.

## Alternatives écartées

| Alternative | Motif du rejet |
| --- | --- |
| **Une rédaction unique**, assez vague pour être vraie des deux côtés | coûte à l'`Operator` du chemin collé la promesse la plus forte du produit, laquelle reste intégralement vraie chez lui |
| **Deux clauses complètes**, une par chemin | trois parties sur quatre n'ont aucune raison de diverger, et le jour où l'on en corrige une, l'autre est oubliée |
| **Un booléen `estScanné`** plutôt qu'une valeur fermée | ferait du collage le cas normal et de la connexion l'exception, quand les deux chemins coexistent et qu'aucun ne remplace l'autre |
| **Deux cas seulement**, sans le `non renseigné` refusé | `Collé = 0` devient la valeur qu'on obtient par accident, et la panne rentre par la porte de derrière sans qu'aucun témoin ne la voie |
| **Faire voyager l'origine dans `ScreeningCounts`** | l'objet s'appelle *les comptes du rapport* ; y ranger une non-quantité lui ferait perdre ce que son nom dit, et cacherait dans un objet voisin ce que la signature de la clause doit dire |
| **Ouvrir une troisième liste** pour les valeurs, à côté du signal et du filtre | contredit [#267](https://github.com/AmauryTISSOT/microservice_rgpd/issues/267) : une règle de forme est un `Trigger` de plus versé au même sac, ni plus ni moins qu'une règle de nom |
| **Écrire « quelques valeurs »** au lieu du chiffre | l'`Operator` ne sait pas si le service a lu cinq lignes ou cinquante mille, imagine le pire, et bloque — quand le chiffre exact est la bonne nouvelle |
| **Un compte global** « 412 colonnes sans aperçu » | éteint la seule des quatre familles que l'`Operator` puisse corriger, et qui appelle un geste : les droits refusés |
| **Sortir la santé des catégories hors de portée** sur le chemin scanné | promettrait qu'on sait voir la santé parce qu'on a lu cinq valeurs, dans la seule partie du produit qui existe pour dire l'inverse |
| **Couper la queue du `hint`** comme celle de `FilterStatement` | là-bas la queue était un ajout ; ici elle **est** la preuve, et la couper laisserait une affirmation sans son fondement |
| **Un témoin d'objet seul** | serait vert pendant que l'écran ment : le `hint` est écrit en dur dans deux `.cshtml` |
| **Un témoin asymétrique**, gardant le seul chemin scanné | laisserait passer la correspondance inversée — la phrase du scan rendue sur un relevé collé |
| **Un amendement inscrit sur l'ADR-0012** | c'est réécrire une archive, ce que [#263](https://github.com/AmauryTISSOT/microservice_rgpd/issues/263) a explicitement refusé de faire ; le dépôt supplante par points nommés |
| **Une PR sans ADR** | la clause impose un ADR pour toucher au périmètre lu, et le fond n'est pas procédural : élargir ce qui a été lu rétrécit l'incomplétude reconnue, ce qui est l'érosion même que la règle vise |

## Ce que cet ADR n'ouvre pas

- **La rédaction des textes.** Les trois motifs réécrits du point 6, les libellés des quatre comptes
  du point 5, le contenu du répertoire de phrases interdites du point 8 et l'entrée de glossaire de
  l'aperçu relèvent de la spec du workflow connecté, dont
  [#270](https://github.com/AmauryTISSOT/microservice_rgpd/issues/270) tient la liste.
- **La migration** des `Screening` existants, tranchée par
  [#283](https://github.com/AmauryTISSOT/microservice_rgpd/issues/283).
- **L'export**, qui ne porte aucune clause, sous aucune forme —
  [#269](https://github.com/AmauryTISSOT/microservice_rgpd/issues/269).
- **La partie 4 de la clause**, gelée par l'ADR-0006 et non touchée ici.
- **Les parties 2 et 3 dans leur composition** : le hors-périmètre ne bouge pas, et la liste des
  trois catégories hors de portée non plus — seuls leurs **motifs** sont réécrits, et cela pour
  qu'elle **reste** constante.
- **Le pont `Screening` → `Manifest`**, qui reste interdit.
- **Les ADR 0001 à 0012**, qui ne sont pas édités.
