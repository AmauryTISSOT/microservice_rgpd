# Carte des contextes

Ce dépôt porte **quatre contextes bornés**. Ils se distinguent par le temps qu'ils couvrent :
l'instant d'un verdict, la durée d'une instruction, le temps d'avant — celui où aucune demande
n'existe encore —, et ce qui vaut pour toutes les demandes à la fois.

Les identifiants du code sont en anglais ; les textes lus par un humain — libellés, messages,
documentation d'API — sont en français. La prose française reprend les identifiants anglais tels
quels : « la `Qualification` », « le `EvidenceLog` ».

## Contextes

- [Qualification](./docs/contexts/qualification/CONTEXT.md) — **l'instant du verdict.** Reçoit un
  texte libre en français depuis une application tierce et dit quels droits il exerce. Ne conserve
  que l'acte de l'avoir qualifié.
- [Casework](./docs/contexts/casework/CONTEXT.md) — **la durée de l'instruction.** Tient le dossier
  d'une demande d'exercice de droits, de son arrivée à sa clôture. Appelle les systèmes du client là
  où il les atteint et produit la preuve qu'une procédure a été suivie — y compris là où elle ne
  l'a pas été.
- [Screening](./docs/contexts/screening/CONTEXT.md) — **le temps d'avant.** Détecte les colonnes qui
  portent vraisemblablement des données personnelles, dans le relevé des colonnes d'une base du
  client. Le relevé est collé par un `Operator`, ou produit par le service lui-même quand il
  **scanne** la base. Chaque colonne est rendue une par une à l'`Operator`, qui la retient ou
  l'écarte. C'est la **détection des données personnelles** ; ce qu'elle produit est un **rapport de
  détection**. Le service se connecte et lit quelques valeurs par colonne, mais n'en garde aucune —
  ni les valeurs, ni la chaîne de connexion — et ne touche jamais aux `DeclaredSystem`.
- [Configuration](./docs/contexts/configuration/CONTEXT.md) — **ce qui vaut pour toutes les
  demandes.** Tient le `Settings` — à l'écran, le **Paramétrage** — qui associe à chacun des six
  droits l'adresse à laquelle le service l'exercera. Un droit, une adresse ; un droit sans adresse
  est « non configuré ». Enregistrer une adresse n'appelle rien.

`Qualification`, `Casework` et `Configuration` parlent du même sujet : les droits que le RGPD ouvre
aux personnes concernées. `Screening` regarde le paysage de données du client avant que quiconque
réclame quoi que ce soit.

## Relations

**Noyau partagé : `DataSubjectRight`, et lui seul.** Cette taxonomie fermée de sept valeurs est
écrite par le RGPD, articles 15 à 21. Trois contextes s'y conforment — `Qualification`, `Casework`
et, depuis l'ADR-0016, `Configuration` — et aucun ne la possède. Elle vit donc en dehors d'eux, dans
`src/MicroserviceRgpd.Core/SharedKernel/`. Sa clause de gouvernance est écrite dans
[`data-subject-rights.wire.json`](./data-subject-rights.wire.json) : *ajouter une valeur est une
rupture du contrat public, pas une extension — un événement de niveau ADR.*

`Configuration` en lit six valeurs sur sept, avec leur libellé français et leur article, qu'il ne
recopie pas. `OutOfScope` est un verdict, pas un droit qu'on exerce : il n'a pas d'adresse.

Le noyau partagé vaut par sa petitesse. Ce qui n'est pas vrai des contextes qui le partagent n'y
entre pas.
`Capability` est du `Casework` pur et reste dehors. L'`Operator` de `Screening` porte le même mot
que celui de `Casework` sans partager aucun type : l'identité de mot n'est pas une identité de
modèle.

⚠️ **`Screening` ne touche pas au noyau partagé.** Il ne rattache jamais une colonne à un
`DataSubjectRight` : une colonne « courriel » ne relève pas d'un droit plutôt qu'un autre, elle
relève de tous. Sa propre taxonomie, `PersonalDataCategory`, lui appartient en propre et n'a pas le
RGPD pour auteur.

**`Qualification` → `Casework` : fournisseur amont optionnel.** Une demande peut arriver déjà
qualifiée — par un formulaire où la personne coche son droit, ou par un opérateur qui l'atteste.
Seul le texte libre invoque la qualification. Un `Case` doit donc pouvoir s'ouvrir, s'instruire et
se clore sans qu'aucune qualification n'ait eu lieu.

**`Screening` : `Separate Ways` intégral.** C'est le seul contexte sans aucune intersection avec les
autres : pas de noyau partagé, pas de fournisseur amont, pas même un identifiant opaque qui
traverserait comme le `qualificationId` que porte un `Case`. Rien ne va du `Screening` aux
`DeclaredSystem` — c'est la clause `Aucune modification vers le Manifest`, écrite dans son
glossaire : un catalogue pré-rempli par une machine se lirait comme complet, ce qui est
l'`Omission silencieuse` sous sa forme la plus dangereuse. La clause garde le nom du `Manifest`, que
l'ADR-0016 a retiré de l'écran et du glossaire de `Casework` ; elle protège désormais l'ensemble des
`DeclaredSystem`.

**`Configuration` : `Separate Ways`, en attendant le recâblage.** Rien ne lit encore le
Paramétrage : un `Case` instruit toujours sur les `DeclaredSystem`, qu'aucun écran ne permet plus de
déclarer. C'est la dette que l'ADR-0016 nomme. Le jour où l'instruction appellera par droit, une
traversée entre `Casework` et `Configuration` s'ouvrira, et elle demandera son propre ADR.

**`Separate Ways` pour tout le reste.** `QualificationOpinion`, `LexiconOpinion`, `ReviewSignal`,
`DeclaredConfidence` et `Mode dégradé` n'ont aucun sens dans la durée. Un `Case` référence un
`qualificationId` opaque qu'il ne déréférence jamais. Il n'y a donc aucune couche anticorruption :
on ne traduit pas un opaque.

**Deux gardes de compilation**, tous deux dans `tests/MicroserviceRgpd.ArchitectureTests/`, tous deux
lus au niveau de l'IL :

- Rien ne traverse d'un contexte à l'autre sauf trois lignes, toutes vers le noyau partagé :
  `Qualification`, `Casework` et `Configuration`. En particulier, aucune dépendance `Casework` →
  `Qualification` — l'optionnalité rendue vérifiable ; un test de signatures seules afficherait vert
  sur un gestionnaire qui appelle le moteur dans un corps de méthode, c'est-à-dire sur la fuite même
  que l'on craint.
- Aucun type hors contexte n'en atteint plusieurs, sauf trois fichiers nommés — dont
  l'`AppDbContext`, qui porte les tables de `Casework`, `Screening` et `Configuration`.

## Langue de système

Cinq termes valent au-delà d'un seul contexte. Ils sont écrits ici plutôt que dupliqués dans les
glossaires.

### Aide à la décision

La posture du service : il propose, recense, rappelle et prouve ; il ne tranche jamais. L'issue est
toujours le fait d'un humain, nommé et daté — valider une `Qualification`, clore un `Case`, retenir
une `ScreenedColumn`. Une machine ne produit jamais une issue.

_Avoid_ : décision, arbitrage, verdict automatique, automatisation

Ces mots sont interdits pour nommer une issue que la **machine** produirait, jamais pour nommer le
geste d'un humain. `Case.Arbitrate` et l'écran d'arbitrage d'une réserve de `Locate` sont donc
légitimes. Un `ArbitrationEngine`, un « arbitrage automatique » ou un seuil qui trancherait
tomberaient, eux, sous la liste.

⚠️ **La posture n'emporte pas la même économie d'erreur d'un contexte à l'autre.** Chez
`Qualification`, l'erreur est une ligne fausse qu'un humain a sous les yeux : l'`Erreur relue`, et
elle coûte peu. Chez `Casework`, c'est une ligne manquante que personne ne verra : l'`Omission
silencieuse`, sur laquelle la relecture n'a aucune prise. Chez `Screening`, l'omission est
**relisible**, parce que le rapport rend toutes les colonnes du relevé, y compris celles où rien n'a
été vu : l'`Omission relue` — qui cesse d'exister le jour où quelqu'un filtre l'affichage. Chaque
régime est défini dans le glossaire du contexte où il vaut, et nulle part ailleurs.

### Geste

Un acte posé par l'`Operator` ou par le service. Le mot est choisi pour ce qu'il n'est **pas** : un
état. Un geste laisse *n* traces signées et datées, chacune se tenant seule ; il ne pose jamais un
objet d'état partagé entre elles. C'est ce qui rend audible le refus des états qui court dans tout le
dépôt : un `Screening` n'a aucun état, l'avancement est un compte, `ArbitrateInBatch` pose *n*
arbitrages individuels et jamais un état de lot.

`Screening` et `Casework` emploient le mot au même sens ; il n'appartient donc à aucun des deux.
`Configuration` ne l'emploie pas : poser l'adresse d'un droit est un réglage, qui remplace l'état
précédent sans laisser de trace datée. Le `Settings` est un état, et c'est ce qui le tient hors de
la matière de preuve.

_Avoid_ : action, opération, commande, traitement

`commande` est prise par CQRS : `ArbitrateTableInBatchCommand` est le **message** qui transporte le
geste, quand le geste est l'**acte** que l'humain pose. Un message se rejoue, un acte se signe.
`traitement` est réservé au sens que le RGPD lui donne. `action` et `opération` ne disent pas
l'opposition à l'état, qui est tout le propos du mot.

⚠️ **Le mot est figé par endroits dans le code.**
`ScreenedColumn.IsWithinReachOfABatchGesture` et
`ScreenedTable.WithinReachOfABatchGestureInThisTable` le portent en anglais ; la migration appliquée
`20260806092543_AddDeliveryGesturesAndLedgerCounts` aussi, et celle-là ne se renomme pas.

### Microservice RGPD

Le nom du produit, tel que l'utilisateur le lit : le wordmark devant les quatre entrées du panneau,
le titre de l'accueil, la moitié droite du titre d'onglet de chaque écran. C'est un **nom propre** :
la capitale à **M**icroservice le distingue du nom commun « microservice RGPD » que portent les
phrases du domaine. Comme le `Layout`, il n'appartient à aucun des quatre contextes.

_Avoid_ : Droits des personnes concernées, l'application, l'outil, la plateforme, le portail

« Droits des personnes concernées » a été retiré parce que c'était une description de ce que le
service fait, là où l'utilisateur cherche un nom (ADR-0008). Ce que le service fait n'est plus dit
que par une seule phrase : la présentation de l'accueil (`Navigation.Presentation`).

⚠️ **Le `RGPD` du nom du produit n'est pas celui des `demandes RGPD`.** Ici il nomme le service ; là
il qualifie les demandes que le règlement régit. C'est pourquoi l'entrée « Tableau des demandes
RGPD » garde son complément alors que celle du Paramétrage l'a perdu.

### Version du produit

La version de l'application `MicroserviceRgpd.Web` — seule UI et seule API du produit. Elle
s'affiche à droite du header sous la forme `v0.1.0` et se journalise au démarrage. Déclarée une
seule fois, identique dans tous les environnements, elle nomme ce que l'on voit à l'écran.

_Avoid_ : version du moteur, version du modèle, version d'API, version du sidecar, build, révision,
SHA

⚠️ **Le dépôt connaît deux autres familles de versions, sans rapport avec celle-ci.**

- La version d'un **moteur** : `QualificationEngineIdentity` et `ScreeningEngineIdentity` joignent un
  nom et une version à l'avis ou au rapport produit. Réservées à la provenance, jamais publiques, non
  interprétées par le domaine. Un bump du produit ne change pas l'identité d'un moteur, et une
  nouvelle version de lexique ou de modèle ne bump pas le produit.
- Les versions de **contrat** : le `v1` du document Swagger/Scalar est la version du contrat HTTP de
  l'API ; le sidecar porte les siennes. On peut livrer `v0.2.0` sans toucher au `v1`, et l'inverse.

### Layout

Le cadre fixe que les onze écrans de la surface portent tous, écrit une seule fois dans
`_Layout.cshtml` : la feuille de style et la police que le service sert lui-même, le **panneau
latéral**, le **header**, la balise `<main>` qui enveloppe l'écran, et l'absence de pied de page
comme de lien d'évitement. Ce qui ne varie pas d'un écran à l'autre en relève ; ce qui varie est
l'écran. Le layout n'appartient à aucun des quatre contextes.

_Avoid_ : chrome, habillage, shell, coque, enveloppe

« chrome » a été retiré parce qu'il se lisait comme le navigateur Google Chrome, que ce dépôt nomme
par ailleurs dans `scripts/run-project.sh` (ADR-0007). « Enveloppe » reste à `TransportEnvelope`, qui
nomme l'emballage HTTP d'un transport de `Casework`.

Le layout porte **deux** régions de navigation, qui en sont des parties : le **panneau latéral**
(`sidepanel`) porte les quatre points d'entrée et se replie ; le **header** porte ce qui ne doit
jamais disparaître — le hamburger, le nom du service, la version — et survit au repli. `Navigation`
continue de nommer le modèle des points d'entrée, et ses quatre entrées restent des `EntryPoint`. La
relation ne vaut que dans ce sens : un test de police relève du layout seul, un test de panneau
relève des deux. C'est ce qui autorise `SharedLayout` à porter les deux familles d'assertions
(ADR-0009).

Le mot est **header**, pas « bandeau » : le dépôt emploie déjà « bandeau » pour le bandeau
d'avertissement permanent d'un écran (`DepositScreen`, `CaseScreen`, `LocateHandlerTests`).

⚠️ **Onze écrans, et non treize : les deux routes de la `Cartographie` n'en sont pas.**
`cartographie.json` et `cartographie.csv` sont des Razor Pages qui rendent un fichier, jamais une
page — pas de layout, pas de panneau, pas de header. Aucun test de layout ne les couvre, et aucun ne
doit les couvrir.

## Décisions

- `docs/adr/` — décisions de **système**, valables au-delà d'un seul contexte.
- `docs/contexts/<contexte>/adr/` — décisions propres à un contexte. Aucune à ce jour.

Seize ADR de système sont en vigueur. Un ADR supplanté n'est jamais édité : la supplantation est
écrite dans l'ADR qui supplante, toujours sur un point nommé. L'ADR-0006, visé quatre fois, et
l'ADR-0008 portent en fin de fichier une suite datée qui nomme leurs points morts.

| ADR | Objet | Supplante |
| --- | --- | --- |
| [0001](./docs/adr/0001-architecture-polyglotte-et-moteur-auto-heberge.md) | L'architecture polyglotte et le moteur auto-hébergé. Écrit avant le découpage, quand le dépôt n'avait qu'un contexte ; sa clause porteuse pour `Casework` est l'auto-hébergement intégral et le refus de toute sous-traitance au sens de l'art. 28. | — |
| [0002](./docs/adr/0002-deux-contextes-bornes-et-noyau-partage.md) | Le découpage par le temps et le noyau partagé d'un seul type. Son titre dit « deux contextes » : il est daté, pas faux. | — |
| [0003](./docs/adr/0003-troisieme-contexte-sans-intersection-et-garde-des-traversees.md) | Le troisième contexte, son absence d'intersection, et le garde énoncé en **liste blanche** : rien ne traverse sauf les deux traversées vers le noyau partagé. Un quatrième contexte naîtrait interdit partout. | — |
| [0004](./docs/adr/0004-moteur-de-depistage-en-csharp-sans-second-sidecar.md) | Le moteur de détection vit en C# dans `Infrastructure`, sans second sidecar Python. Son titre porte un mot que l'interface n'emploie plus : daté, pas faux. | — |
| [0005](./docs/adr/0005-design-language-documente-police-embarquee-et-fichiers-statiques.md) | Le design language documenté, la police embarquée, l'ouverture des fichiers statiques. | — |
| [0006](./docs/adr/0006-trois-points-d-entree-renommes-en-francais-identifiants-inchanges.md) | Les trois points d'entrée renommés en langue humaine, identifiants C# inchangés. | — |
| [0007](./docs/adr/0007-le-cadre-partage-des-ecrans-se-nomme-layout.md) | Le cadre partagé se nomme `Layout`, la barre `Navigation`, et `Chrome` quitte le dépôt. | 0006 : la clause qui laissait `ChromeNavigation` hors périmètre. |
| [0008](./docs/adr/0008-le-service-se-nomme-microservice-rgpd-et-la-configuration-porte-deux-noms.md) | Le service se nomme « Microservice RGPD » ; l'écran de configuration porte deux noms. | 0006 : la réserve n° 1 et la règle « un seul nom, aucune forme courte ». |
| [0009](./docs/adr/0009-la-navigation-passe-en-panneau-lateral-repliable-sans-javascript.md) | La navigation passe en panneau latéral repliable ; le header survit au repli ; zéro ligne de JavaScript. | 0007 : la clause « le layout porte **une** barre ». |
| [0010](./docs/adr/0010-un-quatrieme-point-d-entree-la-qualification-a-sa-surface.md) | La `Qualification` acquiert un quatrième point d'entrée, à `/qualification`, au troisième rang du panneau. | 0006 : la clause « Trois entrées, et pas une quatrième », dont le motif a disparu avec la barre. |
| [0011](./docs/adr/0011-les-internes-des-moteurs-sur-la-surface-de-l-operator.md) | Les internes des moteurs paraissent sur la surface de l'`Operator` ; le contrat HTTP ne bouge pas. `QualificationOutcome` s'élargit, `QualifyResponse` les jette à la projection. | — |
| [0012](./docs/adr/0012-la-connexion-le-scan-et-les-echantillons-entrent-dans-screening.md) | La connexion, le scan et les échantillons de valeurs entrent dans `Screening`. Deux garde-fous survivent : aucun secret d'accès durable, aucune valeur enregistrée. Renverse une clause de glossaire, `Aucune donnée réelle n'entre`. | — |
| [0013](./docs/adr/0013-la-clause-d-incompletude-varie-avec-l-origine-du-releve.md) | La clause d'incomplétude varie avec l'origine du relevé : une clause, quatre parties, une seule qui varie — le périmètre lu. L'origine est une valeur fermée `Collé` / `Scanné`, enregistrée avec le rapport. | 0012 : la réserve qui rangeait le contenu de la clause parmi ce qu'il ne décide pas. |
| [0014](./docs/adr/0014-le-screening-n-enregistre-pas-qui-a-arbitre.md) | `Screening` n'enregistre pas qui a arbitré : l'`Arbitration` devient `(State, RenderedOn)`. Ce que la trace doit prouver est qu'un humain a tranché, et la date le prouve. Asymétrie assumée avec `Casework`, qui enregistre qui a tranché. | — |
| [0015](./docs/adr/0015-le-scan-survit-a-la-requete-qui-l-a-lance.md) | Un scan survit à la requête HTTP qui l'a lancé. Ce que le garde interdit est ce qui part tout seul, pas ce qui court plus longtemps qu'un échange. Amende `NothingRunsInTheBackgroundTests` sans élargir sa liste. | — |
| [0016](./docs/adr/0016-le-manifest-cede-la-place-au-parametrage-un-droit-une-adresse.md) | Le `Manifest` cède la place au Paramétrage : un droit, une adresse. Nouveau contexte `Configuration`, troisième consommateur du noyau partagé ; `DeclaredSystem` reste, et la dette de recâblage de l'instruction est nommée. | 0008 : les noms « Configuration » / « Configuration du microservice RGPD », et « `Manifest` reste `Manifest` ». 0006 : `Manifest` parmi les identifiants inchangés. |

⚠️ **Les ADR-0010 et 0011 sont deux et non un, délibérément** : ce sont deux décisions sans rapport,
qui se défont séparément. Le dépôt supplante par points nommés ; un ADR fondu ne saurait plus se
supplanter à moitié.

## Où vivent les contextes dans le code

`src/` est découpé **par couche** (Clean Architecture : `Core`, `UseCases`, `Infrastructure`, `Web`),
et chaque contexte traverse les quatre. Il n'existe donc pas de `src/<contexte>/` où poser un
`CONTEXT.md` — d'où `docs/contexts/<contexte>/`. À l'intérieur des couches, les contextes se lisent
au dossier : `Core/Qualifications/`, `Core/Casework/`, `Core/Screenings/`, `Core/Configuration/`,
`Core/SharedKernel/`.

⚠️ **Deux dossiers sont au pluriel, pour une raison mécanique.** Un type `Qualification` dans un
espace de noms `Qualification` est un piège de résolution de noms en C# : le compilateur doit
départager le type et l'espace de noms à chaque usage, et il ne le fait pas partout de la même façon.
Le dossier prend donc le pluriel là où le contexte a un type qui porte son nom. Le garde d'ADR-0003
lit l'appartenance **par préfixe** pour que ce pluriel ne lui échappe pas. `Casework`,
`Configuration` et `SharedKernel` restent au singulier : aucun type ne porte ces noms. Le préfixe attrape aussi tout
dossier qui commence par le nom d'un contexte : c'est pourquoi le point de montage de `Web` s'appelle
`Composition/` et non `Configurations/` (ADR-0016).
