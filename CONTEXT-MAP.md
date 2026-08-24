# Carte des contextes

Ce dépôt porte **trois contextes bornés**, et ils se distinguent par le **temps** : l'un ne connaît
que l'instant d'un verdict, l'autre que la durée d'une instruction, le troisième que le temps
d'**avant** — celui où aucune demande n'existe encore.

Les deux premiers parlent du même sujet, les droits que le RGPD ouvre aux personnes concernées. Le
troisième n'en parle pas du tout : il regarde le paysage de données du client avant que quiconque ne
réclame quoi que ce soit. C'est pourquoi il ne partage rien avec eux — voir *Relations*.

Les identifiants du code sont en anglais ; les textes destinés à l'humain — libellés, messages,
documentation d'API — sont en français. La prose française porte les identifiants anglais tels
quels : on écrit « la `Qualification` », « le `EvidenceLog` ».

## Contextes

- [Qualification](./docs/contexts/qualification/CONTEXT.md) — **l'instant du verdict.** Reçoit un
  texte libre français d'une application tierce et dit quels droits il exerce. Ne conserve que
  l'acte de l'avoir qualifié.
- [Casework](./docs/contexts/casework/CONTEXT.md) — **la durée de l'instruction.** Tient le dossier
  d'une demande d'exercice de droits de son arrivée à sa clôture, appelle les systèmes du client
  là où il les atteint, et produit la preuve qu'une procédure a été suivie — y compris là où elle
  ne l'a pas été.
- [Screening](./docs/contexts/screening/CONTEXT.md) — **le temps d'avant.** Détecte, dans le relevé
  des colonnes d'une base du client — collé par un `Operator`, ou relevé par le service lui-même
  lorsqu'il **scanne** la base —, les colonnes qui portent vraisemblablement des données
  personnelles, et les lui rend une par une pour qu'il les retienne ou les écarte. C'est la
  **détection des données personnelles**, et ce qu'elle rend est un **rapport de détection**. Il se
  connecte et lit quelques valeurs par colonne, mais **n'en garde aucune** — ni les valeurs, ni la
  chaîne de connexion —, et ne touche jamais au `Manifest`.

## Relations

- **Noyau partagé — `DataSubjectRight` et lui seul, et il ne lie que deux contextes sur trois.** La
  taxonomie fermée de sept valeurs n'est le modèle ni de `Qualification` ni de `Casework` : elle est
  écrite par le RGPD, articles 15 à 21. Les deux s'y **conforment**, aucun ne la **possède**. Elle
  vit donc en dehors des deux, dans
  `src/MicroserviceRgpd.Core/SharedKernel/`, et sa clause de gouvernance est déjà écrite en toutes
  lettres dans [`data-subject-rights.wire.json`](./data-subject-rights.wire.json) : *ajouter une
  valeur est une rupture du contrat public, pas une extension — un événement de niveau ADR.*

  Le noyau partagé vaut par sa **petitesse**. Ce qui n'est pas vrai partout sans exception n'y entre
  pas : `Capability`, par exemple, est du `Casework` pur et resterait dehors ; et l'`Operator` de
  `Screening` porte le même mot que celui de `Casework` **sans partager aucun type** — l'identité de
  mot n'est pas une identité de modèle, et factoriser sur elle serait la première fissure.

  ⚠️ `Screening` n'y touche pas. Il ne rattache **jamais** une colonne à un `DataSubjectRight` : une
  colonne « courriel » ne relève pas d'un droit plutôt qu'un autre, elle relève de tous. L'y
  raccrocher ferait peser une troisième dépendance sur le noyau sans rien apporter — sa taxonomie à
  lui, `PersonalDataCategory`, lui appartient en propre et n'a pas le RGPD pour auteur.

- **`Qualification` → `Casework` : fournisseur amont *optionnel*.** Une demande peut arriver déjà
  qualifiée — par un formulaire où la personne coche son droit, ou par un opérateur qui l'atteste.
  Seul le texte libre invoque la qualification. Un `Case` doit donc pouvoir s'ouvrir, s'instruire
  et se clore sans qu'aucune qualification n'ait jamais eu lieu.

- **`Screening` : `Separate Ways` intégral, et aucun noyau partagé du tout.** C'est le seul contexte
  du dépôt qui n'a **aucune** intersection avec les autres : pas de noyau partagé, pas de fournisseur
  amont, pas même un identifiant opaque qui traverserait comme le `qualificationId` que porte un
  `Case`. Il vit avant, il ne connaît aucune demande, et rien de ce qu'il produit ne descend nulle
  part. En particulier, **rien ne va du `Screening` au `Manifest`** — c'est la clause `Aucune modification vers
  le Manifest`, écrite dans son glossaire : un `Manifest` pré-rempli par une machine se lirait comme
  complet, ce qui est l'`Omission silencieuse` sous sa forme la plus dangereuse.

- **Aucune dépendance de compilation entre `Screening` et les deux autres, dans les deux sens.** Le
  garde de `tests/MicroserviceRgpd.ArchitectureTests/` s'étend, avec la même lecture au niveau de
  l'IL. ⚠️ Ce qui rend cette règle tenable est justement l'absence d'intersection : il n'y a rien à
  factoriser, donc rien à négocier — à la différence de `Casework → Qualification`, où la règle doit
  résister à une tentation réelle.

- **Separate Ways pour tout le reste.** Rien d'autre ne traverse la frontière. `QualificationOpinion`,
  `LexiconOpinion`, `ReviewSignal`, `DeclaredConfidence`, `Mode dégradé` n'ont aucun sens dans la
  durée. Un `Case` référence un `qualificationId` **opaque**, qu'il ne déréférence jamais. Il n'y a
  donc aucune couche anticorruption : on ne traduit pas un opaque.

- **Aucune dépendance de compilation `Casework` → `Qualification`.** C'est l'optionnalité rendue
  vérifiable : sans cette règle, la promesse « le second contexte se démontre sans GPU » ne serait
  qu'une intention. Elle est gardée par un test du code compilé — un test de signatures seules
  afficherait vert sur un gestionnaire qui appelle le moteur dans un corps de méthode, c'est-à-dire
  sur la fuite même que l'on craint. Le garde vit dans `tests/MicroserviceRgpd.ArchitectureTests/`,
  et il est posé **avant** le contexte qu'il garde : la première ligne de `Casework` naîtra déjà
  sous surveillance.

## Langue de système

Cinq termes sont vrais des trois côtés à la fois, et ils sont écrits ici plutôt que dupliqués dans
les trois glossaires — une doctrine tenue partout doit être écrite **une fois, au-dessus**, sinon
elle n'est tenue nulle part.

**Aide à la décision** :
La posture du service, sur toute sa durée : il propose, recense, rappelle et prouve ; il ne tranche
jamais. Aucun de ces verbes n'est « décider ». L'issue est toujours le fait d'un humain, nommé et
daté — qu'il s'agisse de valider une `Qualification`, de clore un `Case` ou de retenir une
`ScreenedColumn`. Une machine ne produit jamais une issue.
_Avoid_ : décision, arbitrage, verdict automatique, automatisation
⚠️ **Ce que cette liste interdit est de nommer une issue que la _machine_ produirait**, jamais de
nommer le geste d'un humain. `Case.Arbitrate` et l'écran d'arbitrage d'une réserve de `Locate` sont
donc légitimes, et le mot y est repris sciemment : ils ne nomment que l'`Operator` tranchant, nommé
et daté — c'est-à-dire très exactement ce que la posture exige, et non ce qu'elle refuse. Un
`ArbitrationEngine`, un « arbitrage automatique » ou un seuil qui trancherait tomberaient, eux,
sous la liste.

⚠️ Cette posture n'emporte **pas** la même économie d'erreur d'un contexte à l'autre, et c'est le
piège que le découpage rend visible. Chez `Qualification`, l'erreur est une ligne fausse qu'un humain
a sous les yeux : c'est l'`Erreur relue`, et elle coûte peu. Chez `Casework`, l'erreur est une ligne
manquante que personne ne verra jamais : c'est l'`Omission silencieuse`, et la relecture n'a aucune
prise sur elle. Chez `Screening`, l'erreur qui coûte est bien l'omission — mais elle est **relisible**,
et seulement parce que le rapport de détection rend **toutes** les colonnes du relevé, y compris
celles où rien n'a été vu : c'est l'`Omission relue`, et elle cesse d'exister le jour où quelqu'un
filtre l'affichage. Chaque régime est défini dans le glossaire du contexte où il vaut, et **nulle
part ailleurs**.

**Geste** :
Un **acte** posé par l'`Operator` ou par le service, et le mot est choisi pour ce qu'il **n'est
pas** : un état. Un geste laisse *n* traces signées et datées, chacune se tenant seule ; il ne pose
jamais un objet d'état partagé entre elles. C'est lui qui rend audible le refus des états qui court
dans tout le dépôt — « un `Screening` n'a aucun état », l'avancement est un **compte** et non un
état de haut niveau rassurant, `ArbitrateInBatch` « pose *n* arbitrages individuels, jamais un état
de lot ». Il vaut des deux côtés de la frontière : `Screening` et `Casework` l'emploient au même
sens, et il n'appartient donc à aucun des deux.
_Avoid_ : action, opération, commande, traitement
⚠️ **Cette entrée ne tranche rien.** Elle écrit un mot que le dépôt emploie déjà partout — dans les
deux glossaires, dans les pages, dans les commentaires de domaine — et qu'aucune entrée ne
définissait. Le défaut n'était pas le mot, c'était son absence de définition : un terme employé de
part et d'autre de la frontière et défini nulle part est un terme que le prochain lecteur devra
deviner.
⚠️ **Un geste n'est pas une commande, et la nuance porte.** `commande` est prise par CQRS :
`ArbitrateTableInBatchCommand` est le **message** qui transporte le geste, quand le geste est
l'**acte** que l'humain pose. Un message se rejoue, un acte se signe. `traitement` est réservé au
sens que le RGPD lui donne et ne nommera jamais autre chose ici. `action` et `opération`, eux, ne
disent pas l'opposition à l'état — qui est tout le propos du mot.
⚠️ **Le mot vit dans le code, et une part en est figée.** `ScreenedColumn.IsWithinReachOfABatchGesture`
et `ScreenedTable.WithinReachOfABatchGestureInThisTable` le portent en anglais, `Gesture` ; la
migration appliquée `20260806092543_AddDeliveryGesturesAndLedgerCounts` aussi, et celle-là est de
l'histoire datée qu'on ne renomme pas. Un renommage du terme laisserait donc le mot vivant quelque
part quoi qu'il arrive — raison de plus pour le définir plutôt que le remplacer.
⚠️ **Nommer un geste n'est pas nommer une décision de machine**, et l'entrée « Aide à la décision »
ci-dessus le dit déjà : ce que sa liste interdit est de nommer une issue que la *machine*
produirait, jamais de nommer le geste d'un humain. Le geste nommé du dépôt est **le geste de lot**
de `Screening` — `ArbitrateInBatch` —, borné à sa table et à ses seules colonnes `Unflagged` encore
`Awaiting`, et le mot y est repris sciemment.

**Microservice RGPD** :
Le nom du produit, tel que l'utilisateur le lit : le wordmark devant les quatre entrées du panneau,
le titre de l'accueil, et la moitié droite du titre d'onglet de chaque écran. C'est un **nom propre**
— il dit ce que le service *est* —, et la capitale à **M**icroservice est ce qui le distingue du nom
commun « microservice RGPD » que portent les phrases du domaine. Comme le `Layout`, il n'appartient à
aucun des trois contextes.
_Avoid_ : Droits des personnes concernées, l'application, l'outil, la plateforme, le portail
⚠️ **Le nom retiré est « Droits des personnes concernées »**, et il l'est pour une raison de
registre : c'était une **description** de ce que le service fait, là où l'utilisateur qui ouvre
l'application cherche un nom. Voir l'ADR-0008, qui supplante sur ce point l'ADR-0006.
⚠️ **Ce que le service *fait* n'est plus dit par son nom**, et une seule phrase le dit encore : la
présentation de l'accueil (`Navigation.Presentation`). C'est la contrepartie du changement de
registre, et c'est ce qui rend cette phrase moins facultative qu'avant.
⚠️ **Le `RGPD` du nom du produit n'est pas celui des `demandes RGPD`.** Ici il nomme le service ;
là il qualifie les demandes que le règlement régit. L'identité de mot n'est pas une identité de
sens, et c'est pourquoi l'entrée « Tableau des demandes RGPD » garde le sien alors que celle de la
configuration a perdu le complément qui répétait celui-ci.

**Version du produit** :
La version de l'application `MicroserviceRgpd.Web` — seule UI et seule API du produit —, celle qui
s'affiche à droite du header sous la forme `v0.1.0` et se journalise au démarrage.
Déclarée une seule fois, identique dans tous les environnements, elle nomme *ce que l'on voit à
l'écran*, et rien d'autre.
_Avoid_ : version du moteur, version du modèle, version d'API, version du sidecar, build, révision,
SHA
⚠️ **Le dépôt connaît déjà une autre version, et elle n'est pas celle du produit.**
`QualificationEngineIdentity` et `ScreeningEngineIdentity` sont le nom et la version qu'un *moteur*
joint à l'avis ou au rapport qu'il a produit — celle de ses règles, celle du modèle servi —,
réservées à la provenance, jamais publiques, et que le domaine n'interprète pas ; le glossaire de
`Screening` va jusqu'à mettre « version » en `_Avoid_` de cette entrée, précisément pour que le mot
ne nomme pas le concept. La version du produit, elle, est publique par construction et ne dit rien
d'aucun moteur : un bump du produit ne change pas l'identité d'un moteur, et une nouvelle version de
lexique ou de modèle ne bump pas le produit. Les deux ne se lisent ni ne se dérivent l'une de l'autre.
⚠️ Elle est tout aussi distincte des versions de **contrat**, qui gardent chacune leur cadence : le
`v1` du document Swagger/Scalar est la version du contrat HTTP de l'API, et le sidecar porte les
siennes — contrat HTTP, moteur lexical. On peut livrer `v0.2.0` sans toucher au `v1`, et l'inverse.
Coupler l'une à l'autre ferait d'un changement d'écran une rupture de contrat, ou d'une rupture de
contrat un simple bump.

**Layout** :
Le cadre fixe que les treize écrans de la surface portent tous, écrit **une seule fois** dans
`_Layout.cshtml` : la feuille de style et la police que le service sert lui-même, le **panneau
latéral** et le **header**, la balise `<main>` qui enveloppe l'écran, et l'absence de pied de page
comme de lien d'évitement. Ce qui ne varie pas d'un écran à l'autre en relève ; ce qui varie est
l'écran. Le layout n'appartient à aucun des trois contextes — c'est ce qui les porte tous, et c'est
pourquoi il est nommé ici plutôt que dans l'un des trois glossaires.
_Avoid_ : chrome, habillage, shell, coque, enveloppe
⚠️ **Le mot retiré est « chrome »**, et il l'est pour une raison de lecture : il se lisait comme le
navigateur Google Chrome — que ce dépôt nomme par ailleurs pour de vrai, dans `scripts/run-project.sh`.
Voir l'ADR-0007, qui supplante sur ce point l'ADR-0006.
⚠️ **Le layout porte DEUX régions de navigation, et aucune n'est le layout : elles en sont des
parties.** Le **panneau latéral** (`sidepanel`) porte les quatre points d'entrée et se replie ; le
**header** (`header`) porte ce qui ne doit jamais disparaître — le hamburger, le nom du service, la
version — et survit au repli. `Navigation` continue de nommer le modèle des points d'entrée, et ses
quatre entrées restent des `EntryPoint`. La relation ne vaut que dans ce sens — un test de police
relève du layout et non de la navigation, alors qu'un test de panneau relève des deux, par la partie.
C'est ce qui autorise `SharedLayout` à porter les deux familles d'assertions. Voir l'ADR-0009.
⚠️ **Le mot est « header », pas « bandeau ».** Le dépôt emploie déjà « bandeau » pour tout autre
chose — le bandeau d'avertissement permanent d'un écran (`DepositScreen`, `CaseScreen`,
`LocateHandlerTests`) —, et deux objets sans rapport ne portent pas le même mot. C'est la raison
pour laquelle le code et la prose disent tous deux `header` ici, là où le panneau, lui, se dit
`sidepanel` dans le code et « panneau latéral » en français.
⚠️ **« Enveloppe » reste à `TransportEnvelope`**, qui nomme l'emballage HTTP d'un transport de
`Casework` — un tout autre objet, et la raison pour laquelle le mot est en `_Avoid_` ici.
⚠️ **Trois endroits gardent le mot _chrome_, et aucun n'est un reste à balayer.**
`docs/design/DESIGN.md` le porte au sens du designer, en anglais, à propos du langage de design de
Notion — c'est le sens où il est juste. L'ADR-0005 et l'ADR-0006 le portent au sens retiré, dans leur
prose française : ce sont des comptes-rendus datés, et un ADR acté parle avec les mots de sa date.

## Décisions

- `docs/adr/` — décisions de **système**, valables au-delà d'un seul contexte.
- `docs/contexts/<contexte>/adr/` — décisions propres à un contexte. Aucun n'existe à ce jour.

Onze ADR de système sont en vigueur, et **quatre en supplantent un autre — toujours sur des points
nommés**. Trois visent l'ADR-0006 : l'ADR-0007 rouvre ce qu'il avait explicitement laissé fermé,
l'ADR-0008 renverse deux de ses clauses — la réserve n° 1, qui tenait le wordmark métier pour le
contrepoids du mot « microservice », et la règle « un seul nom, aucune forme courte » —, et
l'ADR-0010 retire la clause « Trois entrées, et pas une quatrième ». Le quatrième vise l'ADR-0007 :
l'ADR-0009 lui retire la clause « le layout porte **une** barre ». Aucun texte supplanté n'a été
édité ; l'ADR-0006 porte en fin de fichier une **suite datée** qui se borne à nommer ses trois points
morts, pour qu'un lecteur qui l'ouvre seul ne les tienne pas pour vivants.

- [ADR-0001](./docs/adr/0001-architecture-polyglotte-et-moteur-auto-heberge.md) — l'architecture
  polyglotte et le moteur auto-hébergé.
- [ADR-0002](./docs/adr/0002-deux-contextes-bornes-et-noyau-partage.md) — le découpage par le temps
  et le noyau partagé d'un seul type. ⚠️ Son titre dit « deux contextes » : il est **daté, pas
  faux** — il décidait de ce qui existait alors, et ses décisions restent en vigueur.
- [ADR-0003](./docs/adr/0003-troisieme-contexte-sans-intersection-et-garde-des-traversees.md) — le
  troisième contexte borné, son absence totale d'intersection, et le garde énoncé en **liste
  blanche** : rien ne traverse d'un contexte à l'autre sauf les deux traversées vers le noyau
  partagé, écrites en toutes lettres. Un quatrième contexte naîtrait donc interdit partout, et son
  `CONTEXT.md` ne peut pas entrer sans que quelqu'un écrive sa ligne.
- [ADR-0004](./docs/adr/0004-moteur-de-depistage-en-csharp-sans-second-sidecar.md) — le moteur de la
  détection des données personnelles vit en C# dans `Infrastructure`, sans second sidecar Python.
  ⚠️ Son nom de fichier et son titre portent le mot que l'interface n'emploie plus : ils sont
  **datés, pas faux** — un ADR acté parle avec les mots de sa date, et on ne le réécrit pas.
- [ADR-0005](./docs/adr/0005-design-language-documente-police-embarquee-et-fichiers-statiques.md) —
  le design language documenté est adopté, sa police est embarquée, et le service ouvre ses fichiers
  statiques.
- [ADR-0006](./docs/adr/0006-trois-points-d-entree-renommes-en-francais-identifiants-inchanges.md) —
  les trois points d'entrée sont renommés dans la langue humaine — « Configuration du microservice
  RGPD », « Détection des données personnelles », « Tableau des demandes RGPD » — et les
  identifiants C# ne bougent pas.
- [ADR-0007](./docs/adr/0007-le-cadre-partage-des-ecrans-se-nomme-layout.md) — le cadre partagé des
  écrans se nomme `Layout`, la barre se nomme `Navigation`, et le mot `Chrome` quitte le dépôt.
  ⚠️ **Il supplante l'ADR-0006 sur un point précis** : celui-ci rangeait « le mot `Chrome` de
  `ChromeNavigation` » parmi ce que sa décision n'ouvrait pas. Cette clause ne vaut plus ; tout le
  reste de l'ADR-0006 reste en vigueur, et son texte n'a pas été édité.
- [ADR-0008](./docs/adr/0008-le-service-se-nomme-microservice-rgpd-et-la-configuration-porte-deux-noms.md)
  — le service se nomme « Microservice RGPD », et l'écran de configuration porte **deux noms** :
  `Configuration` dans la barre, `Configuration du microservice RGPD` sur la carte de l'accueil et
  en titre. ⚠️ **Il supplante l'ADR-0006 sur deux points** : la réserve n° 1, qui tenait le wordmark
  métier pour le contrepoids autorisant le mot « microservice » dans la barre, et la règle « un seul
  nom, aucune forme courte ». Tout le reste de l'ADR-0006 reste en vigueur, et son texte n'a pas été
  édité.
- [ADR-0009](./docs/adr/0009-la-navigation-passe-en-panneau-lateral-repliable-sans-javascript.md) —
  la navigation passe en **panneau latéral repliable**, le layout garde un **header** qui survit au
  repli, et le repli n'a pas une ligne de JavaScript. ⚠️ **Il supplante l'ADR-0007 sur un point** :
  celui-ci posait que le layout portait **une** barre, nommée `Navigation`. Le layout en porte
  désormais deux ; `Navigation` et `EntryPoint` gardent leur sens.
- [ADR-0010](./docs/adr/0010-un-quatrieme-point-d-entree-la-qualification-a-sa-surface.md) — la
  `Qualification` acquiert un **quatrième point d'entrée**, à `/qualification`, nommé
  « Qualification » et posé au **troisième rang** du panneau. ⚠️ **Il supplante l'ADR-0006 sur un
  troisième point** : la clause « Trois entrées, et pas une quatrième », dont le motif — la
  lisibilité d'une barre horizontale — a disparu avec la barre que l'ADR-0009 a retirée. Tout ce que
  l'ADR-0006 décide par ailleurs reste en vigueur, hors les deux points que les ADR-0007 et 0008 lui
  avaient déjà retirés, et son texte n'a pas été réécrit.
- [ADR-0011](./docs/adr/0011-les-internes-des-moteurs-sur-la-surface-de-l-operator.md) — les
  **internes des moteurs** — les deux `QualificationOpinion`, la `DeclaredConfidence`, les deux
  `QualificationEngineIdentity` et les trois latences — paraissent sur la surface de l'`Operator`,
  et le **contrat HTTP ne bouge pas** : `QualificationOutcome` s'élargit pour les porter,
  `QualifyResponse` les jette à la projection. ⚠️ **Il ne supplante rien** — il relit ce que
  « public » veut dire, et garde **séparées** les deux raisons d'afficher ces internes : expliquer le
  `ReviewSignal` d'un côté, le diagnostic de l'autre.

⚠️ **Les ADR-0010 et 0011 sont deux et non un, et c'est délibéré** : ce sont deux décisions sans
rapport, qui se défont séparément — on peut retirer la porte du panneau sans rien changer à ce que
l'écran montre, et l'inverse. Le dépôt supplante **par points nommés** ; un ADR fondu ne saurait plus
se supplanter à moitié.

⚠️ [ADR-0001](./docs/adr/0001-architecture-polyglotte-et-moteur-auto-heberge.md) **précède le
découpage** : il a été écrit quand le dépôt n'avait qu'un contexte. Il se lit comme un ADR de
système, sa clause porteuse pour `Casework` étant l'auto-hébergement intégral et le refus de toute
sous-traitance au sens de l'art. 28 — sur lesquels tout le reste s'appuie. Il n'a pas été réécrit :
on supplante un ADR, on ne l'édite pas.

## Où vivent les contextes dans le code

`src/` est découpé **par couche** (Clean Architecture : `Core`, `UseCases`, `Infrastructure`, `Web`),
et chaque contexte **traverse** les quatre. Il n'existe donc pas de `src/<contexte>/` où poser un
`CONTEXT.md` — d'où `docs/contexts/<contexte>/`, qui s'écarte sciemment du layout multi-contexte
générique. À l'intérieur des couches, les contextes se lisent au dossier : `Core/Qualifications/`,
`Core/Casework/`, `Core/Screenings/`, `Core/SharedKernel/`.

⚠️ **Deux de ces dossiers sont au pluriel, et pour la même raison mécanique** : un type `Qualification`
dans un espace de noms `Qualification`, un type `Screening` dans un espace de noms `Screening`, sont
un piège de résolution de noms en C# — le compilateur doit départager le type et l'espace de noms à
chaque usage, et il ne le fait pas partout de la même façon. Le dossier prend donc le pluriel là où le
contexte a un type qui porte son nom, et le garde d'ADR-0003 lit l'appartenance **par préfixe** pour
que ce pluriel ne lui échappe pas. `Casework` et `SharedKernel` restent au singulier : aucun type ne
porte ces noms-là.
