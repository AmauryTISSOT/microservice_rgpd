# Carte des contextes

Ce dépôt porte **quatre contextes bornés**. Ils se distinguent par le temps qu'ils couvrent :
l'instant d'un verdict, l'arrivée d'une demande, le temps d'avant — celui où aucune demande
n'existe encore —, et ce qui vaut pour toutes les demandes à la fois.

Les identifiants du code sont en anglais ; les textes lus par un humain — libellés, messages,
documentation d'API — sont en français. La prose française reprend les identifiants anglais tels
quels : « la `Qualification` », « le `Settings` ».

## Contextes

- [Qualification](./docs/contexts/qualification/CONTEXT.md) — **l'instant du verdict.** Reçoit un
  texte libre en français depuis une application tierce et dit quels droits il exerce. Ne conserve
  que l'acte de l'avoir qualifié.
- [Requests](./docs/contexts/requests/CONTEXT.md) — **l'arrivée d'une demande.** Enregistre une
  demande d'exercice de droits dès sa réception : par quel canal, quand, de qui, et quel droit la
  personne invoque. Son type est la `DataSubjectRequest` — à l'écran, une **demande**. L'`Operator`
  peut ensuite **modifier une demande** pour corriger une erreur de saisie. Une demande porte une
  date limite de réponse et un statut, mais l'instruction n'y existe pas : rien ne fait encore
  changer le statut.
- [Screening](./docs/contexts/screening/CONTEXT.md) — **le temps d'avant.** Détecte les colonnes qui
  portent vraisemblablement des données personnelles, dans le relevé des colonnes d'une base du
  client. Le relevé est collé par un `Operator`, ou produit par le service lui-même quand il
  **scanne** la base. Chaque colonne est rendue une par une à l'`Operator`, qui la retient ou
  l'écarte. C'est la **détection des données personnelles** ; ce qu'elle produit est un **rapport de
  détection**. Le service se connecte et lit quelques valeurs par colonne, mais n'en garde aucune —
  ni les valeurs, ni la chaîne de connexion.
- [Configuration](./docs/contexts/configuration/CONTEXT.md) — **ce qui vaut pour toutes les
  demandes.** Tient le `Settings` — à l'écran, le **Paramétrage** — qui associe à chacun des six
  droits l'adresse à laquelle le service l'exercera. Un droit, une adresse ; un droit sans adresse
  est « non configuré ». Enregistrer une adresse n'appelle rien.

`Qualification`, `Requests` et `Configuration` parlent du même sujet : les droits que le RGPD ouvre
aux personnes concernées. `Screening` regarde le paysage de données du client avant que quiconque
réclame quoi que ce soit.

## Relations

**Noyau partagé : `DataSubjectRight`, et lui seul.** Cette taxonomie fermée de sept valeurs est
écrite par le RGPD, articles 15 à 21. Trois contextes s'y conforment — `Qualification`, et depuis
les ADR-0016 et 0017, `Configuration` et `Requests` — et aucun ne la possède. Elle vit donc en
dehors d'eux, dans `src/MicroserviceRgpd.Core/SharedKernel/`. Sa clause de gouvernance est écrite
dans [`data-subject-rights.wire.json`](./data-subject-rights.wire.json) : *ajouter une valeur est une
rupture du contrat public, pas une extension — un événement de niveau ADR.*

`Configuration` et `Requests` en lisent six valeurs sur sept, avec leur libellé français et leur
article, qu'ils ne recopient pas. `OutOfScope` est un verdict, pas un droit qu'on exerce : il n'a
pas d'adresse, et aucune demande ne l'invoque.

Le noyau partagé vaut par sa petitesse. Ce qui n'est pas vrai des contextes qui le partagent n'y
entre pas. L'`Operator` de `Screening` porte le même mot que celui de `Requests` sans partager aucun
type : l'identité de mot n'est pas une identité de modèle.

⚠️ **`Screening` ne touche pas au noyau partagé.** Il ne rattache jamais une colonne à un
`DataSubjectRight` : une colonne « courriel » ne relève pas d'un droit plutôt qu'un autre, elle
relève de tous. Sa propre taxonomie, `PersonalDataCategory`, lui appartient en propre et n'a pas le
RGPD pour auteur.

**`Requests` et `Qualification` : aucune relation déclarée.** Le droit invoqué d'une demande est
choisi par l'`Operator`, jamais lu d'un verdict : une demande s'enregistre sans qu'aucune
qualification n'ait eu lieu, et n'en référence aucune. La matrice interdit les deux sens ; le jour
où une relation s'ouvrira, elle demandera son propre ADR.

**`Screening` : `Separate Ways` intégral.** C'est le seul contexte sans aucune intersection avec les
autres : pas de noyau partagé, pas de fournisseur amont, pas même un identifiant opaque.

**`Configuration` : `Separate Ways`.** Rien ne lit encore le Paramétrage : le service n'exerce
aucun droit. Le jour où une demande sera exercée à l'adresse de son droit, une traversée
`Requests → Configuration` s'ouvrira, et elle demandera son propre ADR.

**`Separate Ways` pour tout le reste.** `QualificationOpinion`, `LexiconOpinion`, `ReviewSignal`,
`DeclaredConfidence` et `Mode dégradé` ne valent qu'à l'instant du verdict et ne sortent pas de
`Qualification`. Il n'y a donc aucune couche anticorruption : rien ne traverse qui demanderait à être
traduit.

**Deux gardes de compilation**, tous deux dans `tests/MicroserviceRgpd.ArchitectureTests/`, tous deux
lus au niveau de l'IL :

- Rien ne traverse d'un contexte à l'autre sauf trois lignes, toutes vers le noyau partagé :
  `Qualification`, `Requests` et `Configuration`. En particulier, aucune dépendance entre `Requests`
  et `Qualification`, dans un sens comme dans l'autre — l'absence de relation rendue vérifiable ; un
  test de signatures seules afficherait vert sur un gestionnaire qui appelle le moteur dans un corps
  de méthode, c'est-à-dire sur la fuite même que l'on craint.
- Aucun type hors contexte n'en atteint plusieurs, sauf trois fichiers nommés — dont
  l'`AppDbContext`, qui porte les tables de `Requests`, `Screening` et `Configuration`.

## Langue de système

Cinq termes valent au-delà d'un seul contexte. Ils sont écrits ici plutôt que dupliqués dans les
glossaires.

### Aide à la décision

La posture du service : il propose, recense, rappelle et prouve ; il ne tranche jamais. L'issue est
toujours le fait d'un humain, nommé et daté — valider une `Qualification`, retenir une
`ScreenedColumn`. Une machine ne produit jamais une issue.

_Avoid_ : décision, arbitrage, verdict automatique, automatisation

Ces mots sont interdits pour nommer une issue que la **machine** produirait, jamais pour nommer le
`Gesture` d'un humain. `ArbitrateColumn`, `ArbitrateInBatch` et l'écran d'arbitrage d'une table sont
donc légitimes. Un `ArbitrationEngine`, un « arbitrage automatique » ou un seuil qui trancherait
tomberaient, eux, sous la liste.

⚠️ **La posture n'emporte pas la même économie d'erreur d'un contexte à l'autre.** Chez
`Qualification`, l'erreur est une ligne fausse qu'un humain a sous les yeux : l'`Erreur relue`, et
elle coûte peu. Chez `Screening`, l'erreur qui coûte est l'omission, et elle est **relisible**,
parce que le rapport rend toutes les colonnes du relevé, y compris celles où rien n'a été vu :
l'`Omission relue` — qui cesse d'exister le jour où quelqu'un filtre l'affichage. Chaque régime est
défini dans le glossaire du contexte où il vaut, et nulle part ailleurs.

### Gesture

Un acte posé par l'`Operator` ou par le service. Le mot est choisi pour ce qu'il n'est **pas** : un
état. Un `Gesture` laisse *n* traces datées, chacune se tenant seule ; il ne pose jamais un objet
d'état partagé entre elles. C'est ce qui rend audible le refus des états qui court dans tout le
dépôt : un `Screening` n'a aucun état, l'avancement est un compte, `ArbitrateInBatch` pose *n*
arbitrages individuels et jamais un état de lot.

`Screening` et `Requests` emploient le mot au même sens ; il n'appartient donc à aucun des deux.
Enregistrer une demande est un `Gesture` : sa trace est datée de l'instant où il est posé —
distinct de la date de réception qu'il déclare — et signée `operator`, en attendant
l'authentification. **Modifier une demande** en est un second : sa trace est l'empreinte
`ModifiedAt` / `ModifiedBy`, qu'aucun écran n'affiche, et une modification qui ne change aucune
valeur n'a pas eu lieu — elle ne laisse rien (ADR-0023). Un arbitrage de `Screening` est daté et ne
se signe pas (ADR-0014).
`Configuration` n'emploie pas le mot : poser l'adresse d'un droit est un réglage, qui remplace l'état
précédent sans laisser de trace datée. Le `Settings` est un état, et c'est ce qui le tient hors de
la matière de preuve.

⚠️ **L'empreinte de modification est une trace qui s'écrase, et c'est une exception assumée.** Un
`Gesture` laisse *n* traces se tenant seules ; modifier une demande n'en laisse qu'une, la dernière,
qui remplace la précédente. Elle dit qu'une demande a été corrigée et quand, jamais combien de fois
ni quoi. Ce n'est pas un journal inachevé : l'ADR-0023 range l'historique des modifications parmi ce
qu'il n'ouvre pas, et le motif y est écrit.

⚠️ **Tout acte de l'`Operator` n'est pas un `Gesture`.** Supprimer une demande est posé par
l'`Operator`, mais ne laisse aucune trace et efface celle de l'enregistrement : c'est un retrait,
pas un `Gesture`. Le statut d'une demande, lui, est un état qu'elle tient, non la trace d'un
`Gesture`.

_Avoid_ : action, opération, commande, traitement

`commande` est prise par CQRS : `ArbitrateTableInBatchCommand` est le **message** qui transporte le
`Gesture`, quand le `Gesture` est l'**acte** que l'humain pose. Un message se rejoue, un acte se
date. `traitement` est réservé au sens que le RGPD lui donne. `action` et `opération` ne disent pas
l'opposition à l'état, qui est tout le propos du mot.

⚠️ **Le code portait le mot avant la langue de système, et ne change pas.**
`ScreenedColumn.IsWithinReachOfABatchGesture` et `ScreenedTable.WithinReachOfABatchGestureInThisTable`
le portent ; la migration appliquée `20260806092543_AddDeliveryGesturesAndLedgerCounts` aussi, et
celle-là ne se renomme pas.

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

Le cadre fixe que les douze écrans de la surface portent tous, écrit une seule fois dans
`_Layout.cshtml` : la feuille de style et la police que le service sert lui-même, le **panneau
latéral**, le **header**, la balise `<main>` qui enveloppe l'écran, et l'absence de pied de page
comme de lien d'évitement. Ce qui ne varie pas d'un écran à l'autre en relève ; ce qui varie est
l'écran. Le layout n'appartient à aucun des quatre contextes.

Le layout ne charge aucun script. Il offre une section où l'écran qui en a besoin pose le sien — un
module ES que le service sert lui-même —, et un seul écran s'en sert aujourd'hui : le tableau des
demandes RGPD (ADR-0018).

_Avoid_ : chrome, habillage, shell, coque, enveloppe

« chrome » a été retiré parce qu'il se lisait comme le navigateur Google Chrome, que ce dépôt nomme
par ailleurs dans `scripts/run-project.sh` (ADR-0007).

Le layout porte **deux** régions de navigation, qui en sont des parties : le **panneau latéral**
(`sidepanel`) porte les quatre points d'entrée et se replie ; le **header** porte ce qui ne doit
jamais disparaître — le hamburger, le nom du service, la version — et survit au repli. `Navigation`
continue de nommer le modèle des points d'entrée, et ses quatre entrées restent des `EntryPoint`. La
relation ne vaut que dans ce sens : un test de police relève du layout seul, un test de panneau
relève des deux. C'est ce qui autorise `SharedLayout` à porter les deux familles d'assertions
(ADR-0009).

Le mot est **header**, pas « bandeau » : un **bandeau** est un message posé dans une surface — un
écran ou une modale —, là où le header est une région du layout, la même sur tous les écrans. Le
rapport de détection de `Screening` s'ouvre sur un bandeau — la base, le SGBD, le lancement, le
moteur — et en porte un second pour ses comptes (`banner` dans le code) ; la modale de création
d'une demande porte un bandeau d'échec, qui dit qu'un envoi a échoué autrement que par un refus et
laisse la saisie en place. Le toast n'en est pas un — qu'il dise « Demande créée », « Demande
supprimée » ou qu'une suppression a échoué : il se pose au-dessus de l'écran, le temps de dire ce
qu'il dit, puis s'efface. La confirmation de suppression, elle, n'a pas de bandeau : quelle que
soit l'issue, elle se ferme, et c'est le toast qui la dit.

⚠️ **Douze écrans, et non quatorze : les deux routes de la `Cartographie` n'en sont pas.**
`cartographie.json` et `cartographie.csv` sont des Razor Pages qui rendent un fichier, jamais une
page — pas de layout, pas de panneau, pas de header. Aucun test de layout ne les couvre, et aucun ne
doit les couvrir.

## Décisions

- `docs/adr/` — décisions de **système**, valables au-delà d'un seul contexte.
- `docs/contexts/<contexte>/adr/` — décisions propres à un contexte. Aucune à ce jour.

Vingt-trois ADR de système sont en vigueur. Un ADR supplanté n'est jamais édité : la supplantation est
écrite dans l'ADR qui supplante, toujours sur un point nommé. L'ADR-0006 et l'ADR-0008 portent en
fin de fichier une suite datée qui nomme leurs points morts jusqu'à l'ADR-0016 ; l'ADR-0017, qui
vise l'ADR-0006 une cinquième fois, n'y ajoute rien et écrit ses supplantations chez lui ;
l'ADR-0018, qui vise les ADR-0005 et 0009, fait de même, comme les ADR-0021 et 0023, qui visent
l'ADR-0017.

| ADR | Objet | Supplante |
| --- | --- | --- |
| [0001](./docs/adr/0001-architecture-polyglotte-et-moteur-auto-heberge.md) | L'architecture polyglotte et le moteur auto-hébergé. Écrit avant le découpage, quand le dépôt n'avait qu'un contexte ; sa clause porteuse est l'auto-hébergement intégral et le refus de toute sous-traitance au sens de l'art. 28. | — |
| [0002](./docs/adr/0002-deux-contextes-bornes-et-noyau-partage.md) | Le découpage par le temps et le noyau partagé d'un seul type. Son titre dit « deux contextes » : il est daté, pas faux. Son second contexte est tombé avec l'ADR-0017. | — |
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
| [0014](./docs/adr/0014-le-screening-n-enregistre-pas-qui-a-arbitre.md) | `Screening` n'enregistre pas qui a arbitré : l'`Arbitration` devient `(State, RenderedOn)`. Ce que la trace doit prouver est qu'un humain a tranché, et la date le prouve. | — |
| [0015](./docs/adr/0015-le-scan-survit-a-la-requete-qui-l-a-lance.md) | Un scan survit à la requête HTTP qui l'a lancé. Ce que le garde interdit est ce qui part tout seul, pas ce qui court plus longtemps qu'un échange. Amende `NothingRunsInTheBackgroundTests` sans élargir sa liste. | — |
| [0016](./docs/adr/0016-le-manifest-cede-la-place-au-parametrage-un-droit-une-adresse.md) | Le `Manifest` cède la place au Paramétrage : un droit, une adresse. Nouveau contexte `Configuration`, consommateur du noyau partagé. | 0008 : les noms « Configuration » / « Configuration du microservice RGPD », et « `Manifest` reste `Manifest` ». 0006 : `Manifest` parmi les identifiants inchangés. |
| [0017](./docs/adr/0017-casework-est-retire-requests-le-remplace.md) | L'ancien contexte d'instruction, `Casework`, est retiré ; `Requests` le remplace, avec la `DataSubjectRequest` pour type et `/demandes` pour route. `Requests` ne consomme que le noyau partagé ; aucune relation avec `Qualification`. « Geste » devient `Gesture`. | 0002 : le second contexte, « la durée de l'instruction ». 0003 : la liste blanche des traversées. 0006 : l'adresse `/dossiers`, `Queue` parmi les identifiants inchangés, et le contrat d'`Adapter` comme motif de cette liste. 0014 : l'asymétrie assumée. 0016 : la dette de recâblage. |
| [0018](./docs/adr/0018-la-doctrine-zero-javascript-est-levee-pour-toute-l-application.md) | La doctrine « zéro JavaScript » est levée pour toute l'application : JavaScript vanilla en modules ES, `<dialog>` natif, ni framework ni bundler, servi par le service, chargé seulement là où un écran en a besoin. Le serveur rend, le script anime. | 0005 : « la surface reste rendue par le serveur, sans JavaScript ». 0009 : « le service continue de ne servir aucun JavaScript », et le motif qui écartait la persistance du repli par `localStorage`. |
| [0019](./docs/adr/0019-la-reception-exige-date-message-droit-et-identification.md) | Enregistrer une demande exige une date de réception, un message, un droit parmi les six, et un email ou un nom et un prénom. Le serveur fait foi ; la validation du navigateur est un confort. Renverse deux clauses de glossaire, « rien n'est obligatoire au dépôt » et le défaut `J+9`. | — |
| [0020](./docs/adr/0020-playwright-dotnet-pour-les-tests-navigateur.md) | Playwright pour .NET, en C# et xUnit, pour les tests navigateur : le service sous Kestrel sur un port réel, PostgreSQL Testcontainers, Chromium seul, installé par la fixture sans `pwsh`. | — |
| [0021](./docs/adr/0021-la-demande-tient-un-statut-et-une-date-limite-de-reponse.md) | La demande tient un statut (`RequestStatus`, né `InProgress`) et une date limite de réponse fixée à la réception, date de réception plus un mois. Le statut est un état, pas la trace d'un `Gesture` ; les signalements ne sont pas enregistrés. | 0017 : « l'instruction, les délais et les statuts n'y existent pas », pour les délais et les statuts. |
| [0022](./docs/adr/0022-supprimer-une-demande-ne-laisse-aucune-trace.md) | Supprimer une demande la retire définitivement, quel que soit son statut, sans trace : ce n'est pas un `Gesture`. Une demande déjà partie se lit comme supprimée. | — |
| [0023](./docs/adr/0023-modifier-une-demande-est-un-geste.md) | Modifier une demande est un `Gesture` : il laisse une empreinte (`ModifiedAt`, `ModifiedBy`) non affichée, qui s'écrase au lieu de s'empiler. Une modification sans changement n'a pas eu lieu ; une demande close est refusée en `Conflict` avant toute validation ; la date limite est recalculée par la règle de l'ADR-0021 ; le dernier enregistrement l'emporte, sans verrou optimiste. | 0017 : « il ne connaît à ce jour qu'un `Gesture` ». Honore l'ADR-0021, sans le supplanter : « l'US qui l'ouvrira devra recalculer la date limite ». |

⚠️ **Les ADR-0010 et 0011 sont deux et non un, délibérément** : ce sont deux décisions sans rapport,
qui se défont séparément. Le dépôt supplante par points nommés ; un ADR fondu ne saurait plus se
supplanter à moitié.

## Où vivent les contextes dans le code

`src/` est découpé **par couche** (Clean Architecture : `Core`, `UseCases`, `Infrastructure`, `Web`),
et chaque contexte traverse les quatre. Il n'existe donc pas de `src/<contexte>/` où poser un
`CONTEXT.md` — d'où `docs/contexts/<contexte>/`. À l'intérieur des couches, les contextes se lisent
au dossier : `Core/Qualifications/`, `Core/Requests/`, `Core/Screenings/`, `Core/Configuration/`,
`Core/SharedKernel/`.

⚠️ **Deux dossiers sont au pluriel, pour une raison mécanique.** Un type `Qualification` dans un
espace de noms `Qualification` est un piège de résolution de noms en C# : le compilateur doit
départager le type et l'espace de noms à chaque usage, et il ne le fait pas partout de la même façon.
Le dossier prend donc le pluriel là où le contexte a un type qui porte son nom. Le garde d'ADR-0003
lit l'appartenance **par préfixe** pour que ce pluriel ne lui échappe pas. `Configuration` et
`SharedKernel` restent au singulier : aucun type ne porte ces noms. `Requests` est au pluriel par son
nom même, et aucun type ne s'appelle `Request`. Le préfixe attrape aussi tout dossier qui commence
par le nom d'un contexte : c'est pourquoi le point de montage de `Web` s'appelle `Composition/` et
non `Configurations/` (ADR-0016).

⚠️ **Le risque d'englobement est plus large pour `Requests`**, que le vocabulaire HTTP emploie aussi.
Tout segment qui commence par ce mot — un `RequestsLogging/`, un `RequestsPipeline/` — se lirait
comme du `Requests`, et le garde y verrait des traversées qui n'en sont pas. Un dossier qui ne
relève pas du contexte ne commence donc pas par ce mot (ADR-0017). Le singulier, `RequestLogging/`,
n'est pas attrapé.
