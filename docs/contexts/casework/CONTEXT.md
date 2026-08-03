# Casework

Ce contexte instruit une demande d'exercice de droits dans la **durée** : de son arrivée à la
clôture de son dossier. Il appelle les systèmes du client là où il les atteint, réclame un constat
là où il ne les atteint pas, et produit la preuve qu'une procédure a été suivie — y compris là où
elle ne l'a pas été.

Il **répond du dossier, jamais des données** : il ne prouve pas qu'un droit a été honoré, il prouve
qu'une procédure a été suivie, par qui et quand. Le nom même du contexte le porte — on instruit un
`Case`, on ne jure pas qu'il finit bien.

Les identifiants du code sont en anglais ; les textes destinés à l'humain sont en français.

## Language

### Le dossier et ses trois niveaux

**Case** :
Le dossier ouvert pour une demande d'exercice de droits, et l'agrégat de ce contexte. Il porte
l'identité déclarée du demandeur, les `Designations` sous lesquelles on le cherche, la date de
réception, et les `Claim` qu'on lui reconnaît. Une demande, un `Case` — jamais deux, parce que la
règle « lire avant d'effacer » traverse les `Claim` et qu'aucune frontière plus fine ne peut la
tenir.
_Avoid_ : Request, Dossier, File, Ticket, demande

**Claim** :
La réclamation d'**un** `DataSubjectRight` à l'intérieur d'un `Case`. C'est l'unité à laquelle le
service répond à la personne au sujet d'un droit. Elle atteste l'**acte de répondre**, jamais le
fait qu'un droit ait été satisfait.
_Avoid_ : Item, Section, Part, Entitlement, Right, volet ⚠️ homonyme dormant :
`System.Security.Claims.Claim`

**Step** :
Le travail dû sur **un** `DeclaredSystem` pour un `Claim` donné. Son grain est le système, jamais la
`Capability` : l'opérateur a besoin de savoir où en est son effacement chez tel prestataire, jamais
où en est le `Locate`.
_Avoid_ : Task, Action, Operation, Job, démarche

### Les états, et ce qu'ils refusent de dire

**États d'un `Case`** — `Open`, `Closed`. Il n'y a pas d'état « en retard » : le dépassement du délai
est un **calcul**, jamais un état, sinon un retard non détecté deviendrait un retard inexistant et la
preuve dépendrait de ce qu'une minuterie ait tourné.

**ClosingCause** :
Ce par quoi un `Case` s'est clos. Trois valeurs : `Answered` (dérivée des `Claim`), `Abandoned`
(non dérivable, motif saisi), `NotApplicable` (la demande n'exerçait aucun droit). Toujours signée
par un humain, jamais par la machine seule.
_Avoid_ : Rejected, Cancelled, OutOfScope ⚠️ `OutOfScope` est une valeur de `DataSubjectRight`,
donc du noyau partagé : le mot est pris.

**États d'un `Claim`** — `Open`, `Answered`, `Refused`.
`Answered` atteste que le service a répondu, **jamais** que le droit a été satisfait : un `Claim` se
clôt `Answered` alors même que des `Step` sont restés inatteints, et l'incomplétude reste visible
dans les `Step` plutôt que masquée par un état de haut niveau rassurant. `Refused` est distinct par
sa charge probatoire propre et par la motivation humaine qu'il exige.
_Avoid_ : Honoured, Fulfilled, Satisfied, Completed, Done

**États d'un `Step`** — `ToDo`, `Awaiting`, `Done`, `OutOfReach`, `Untreated`.
`Done` est **déclaré**, jamais vérifié. `OutOfReach` et `Untreated` ne fusionnent pas : le premier
est **structurel et annonçable au premier jour** (une comptabilité scellée dix ans ne pourra jamais
effacer), le second est un **aveu constaté à la fin** ; ils n'ont ni le même lecteur — la réponse à
la personne d'un côté, la preuve destinée au contrôle de l'autre — ni la même vérité dans le temps,
`OutOfReach` figeant ce que le `Manifest` disait au moment du `Case`.
_Avoid_ : Pending, Failed, Skipped, Blocked

### Le temps, et ce qu'on en fait

**Le délai de l'art. 12.3 court avant nous, et rien ne l'arrête.** Le mois part de la réception par
n'importe quel canal officiel du responsable, sans que celui-ci en soit averti : le service hérite
d'un compteur lancé depuis un nombre de jours inconnu, et la date de réception lui est **déclarée**.
Rien ne le suspend — ni une `OpenQuestion`, ni une vérification d'identité, la suspension n'ayant
aucune base textuelle.

**La file est une requête, jamais un processus.** Le dépassement, l'échéance déclarée d'un `202`,
l'ancienneté d'une `OpenQuestion`, une `Delivery` non remise, un `Ledger` échu : tout se recalcule à
l'instant où l'`Operator` regarde. Rien ne tourne, donc rien ne peut s'arrêter en silence — un
processus de fond interrompu rendrait une file **vide et rassurante**, soit l'`Omission silencieuse`
sous sa forme la plus dangereuse, et ferait dépendre la preuve de ce qu'un `cron` ait tourné.
⚠️ De ces cinq échéances, quatre ne sont que des **colonnes** sur des lignes déjà présentes : le
`Case` est ouvert, il est déjà dans la file. L'échéance **trie**, elle n'ajoute pas. Seul le `Ledger`
échu fait naître une ligne, son `Case` étant clos depuis cinq ans — d'où sa section propre sur
l'écran, qui tient aussi lieu de parade au geste irréversible qu'il porte.
⚠️ **Aucun seuil, nulle part.** Une `OpenQuestion` affiche la date à laquelle elle a été posée,
jamais « sans réponse depuis N jours » : aucun nombre du droit ne fonde N, un seuil réglable serait
une case à laisser pourrir de plus, et trier sur lui ferait passer devant un compteur sans force
juridique. On montre le fait, l'`Operator` juge.

**ExtensionDeclaration** :
Ce que l'`Operator` déclare lorsqu'il prolonge de deux mois au titre de l'art. 12.3 : un **motif**,
la **date à laquelle il a informé la personne** de la prolongation et de ses motifs, sa signature et
sa date. Le service ne prolonge rien et ne notifie personne — il **réclame** une déclaration et
l'enregistre, comme il le fait de l'identité et de la remise.
Le déplacement de l'échéance est un **calcul** sur la date de la déclaration, jamais une propriété de
l'objet : déclarée dans le mois, elle porte le délai à trois mois ; déclarée après, elle s'inscrit
quand même — le fait est gardé — mais le dénominateur ne bouge pas, sans quoi un clic blanchirait un
dépassement déjà acquis. Jamais barrée : on enregistre un fait laid plutôt qu'on ne fabrique un faux.
_Avoid_ : Extension, Delay, Postponement, Deferral, prolongation

**La relance a lieu à l'ouverture d'un `Case`.** Un `202` d'`Adapter` porte une échéance déclarée, et
c'est le service qui revient — mais seulement quand l'`Operator` ouvre le dossier, jamais depuis la
file, qui n'émet aucun appel. Il n'existe ni compteur de tentatives, ni temporisation, ni abandon
automatique, ni escalade : l'`Operator` n'a jamais cessé d'être le seul à produire une issue, et une
escalade automatique lui retirerait une décision qui est la sienne.

**La personne n'apprend rien du temps qui passe.** Le service ne lui parle jamais — ni accusé de
réception, ni avertissement d'échéance, ni relance de courtoisie. Les trois seules communications qui
lui sont dues — la prolongation, le refus et ses mentions de l'art. 12.4, la remise — sont des actes
humains **déclarés** au service, jamais émis par lui. Le temps est de la matière de preuve, lue par
le contrôle ; il n'est pas de la matière de relation.

### Le paysage déclaré du client

**DeclaredSystem** :
Un endroit où des données personnelles vivent chez le client, parce qu'**un humain l'a déclaré**.
Unité de recensement, jamais de déploiement : le paysage du client, pas sa topologie. L'adjectif
n'est pas décoratif — c'est parce que ces systèmes sont déclarés, et non découverts, que
l'`Omission silencieuse` est le risque cardinal de ce contexte.
_Avoid_ : System, Target, Datastore, source

**Manifest** :
Le catalogue des `DeclaredSystem` et de leurs `Capability`, déclaré par un humain et détenu par le
service. À **gros grain** : des systèmes, jamais des champs — la précision du contenu appartient à
l'application. Il **vieillit exprès**, daté par système, et se vérifie contre l'`Adapter` sans jamais
se corriger en silence.
_Avoid_ : Schema, Catalog, Inventory, Map, cartographie

**Capability** :
Ce qu'un `Adapter` sait faire sur un `DeclaredSystem` donné. Quatre valeurs — `Locate`, `Read`,
`Erase`, `Rectify` — dont les droits se composent, parce que le catalogue exige un grain plus fin
que le droit. `Locate` est le plancher : sans lui, une affirmation n'a pas de dénominateur. Un
système sans aucune `Capability` reste pleinement légitime : il est traité à la main.
_Avoid_ : Operation (pris par l'art. 4.2), acte, verbe, diligence, permission

**Adapter** :
Le programme que l'application du client implémente et que le service appelle pour exercer une
`Capability` sur ses `DeclaredSystem`. Le sens est **unique** : le service appelle toujours,
l'application ne rappelle jamais.
_Avoid_ : Connector, Plugin, Integration, Webhook

### La personne, et comment on la désigne

**Designation** :
Un attribut déclaré qui **pointe vers** la personne — une adresse électronique, un nom, un
téléphone, une référence interne. Il ne prétend ni à l'unicité ni à l'exactitude : la personne n'a
pas d'identifiant, et l'identifiant natif d'une application est souvent la clé qui ouvre le moins
de portes.
_Avoid_ : Identifier, SubjectId, Identity, Key, Selector

**Designations** :
Le sac de `Designation` d'un `Case` — la seule identité qui circule. Il s'**enrichit** en cours
d'instruction : une réserve confirmée par l'`Operator` qui introduit une désignation nouvelle la
verse au sac, où elle sert aux appels suivants. Ce qui n'apporte rien reste local au système qui
l'a produit, en vocabulaire opaque.
_Avoid_ : IdentityGraph, Aliases, Profil

**IdentityDeclaration** :
Ce que le canal d'entrée a **déclaré** sur l'identité du demandeur. Vocabulaire fermé de quatre
valeurs — `ApplicationSession`, `ChannelControl`, `OperatorAttested`, `Unverified` — portée par le
`Case`, puisque l'identité est une propriété de la personne et non d'un droit. Le service enregistre
la déclaration et n'en juge **jamais** la valeur ; il ne vérifie lui-même aucune identité.
`Unverified` doit exister : sans la valeur laide, l'opérateur pressé coche la valeur propre et le
service fabrique un faux au lieu d'enregistrer un vide.
La motivation qu'exigent `Unverified` et `OperatorAttested` s'écrit en **deux morceaux** : une
**méthode**, vocabulaire fermé qui se compte et qui survit à la clôture — « recoupement d'un attribut
que le demandeur n'a pas reçu de nous », « reconnaissance personnelle », « rappel sur un contact
déjà enregistré », « aucune » — et un **détail** en prose libre, irréductiblement nominatif, qui
meurt avec le `Case`. Le contrôle juge ainsi la *pratique* sans qu'un seul nom lui survive.
_Avoid_ : Authentication, Verification, TrustLevel, niveau de confiance

⚠️ Aucune pièce d'identité n'entre dans le service, tous canaux confondus. Le `Ledger` consigne le
**fait** qu'une pièce est passée, jamais la pièce.

**ClaimOrigin** :
D'où vient la reconnaissance d'un droit dans un `Case` : `Named` (la personne l'a désigné),
`Attested` (l'`Operator` l'affirme), `Proposed` (une `Qualification` l'a proposé, un humain le
confirme dans le `Case`). Un `Claim` `Proposed` non confirmé est une `OpenQuestion` — visible
pendant que le délai court.
_Avoid_ : Source, Channel, provenance

### Ce que le service détient, et pour combien de temps

**Ledger** :
La matière de preuve d'un `Case` : qui a déclaré quoi et quand, les motifs, les constats, les
tentatives, le compte et la provenance des `Designation`. En **ajout seul**, daté et signé — la
déclaration d'aujourd'hui ne réécrit pas la preuve d'hier. Il ne porte jamais de contenu : il dit
« un fichier a été remis le 12/04 couvrant 2 systèmes sur 6 », jamais ce qu'il y avait dedans.
Il est **anonyme par construction, jamais par expurgation** : on n'y écrit aucune `Designation` ni
aucun nom de personne concernée, dès la première ligne. L'anonymiser à la clôture aurait exigé de le
réécrire — dans la seule structure du dispositif dont l'invariant est qu'on ne la réécrit pas.
⚠️ Il n'est anonyme que **côté personne concernée** : il nomme l'`Operator`, définitivement, parce
que « par qui » est un tiers de ce que le service prouve. C'est donc un fichier de données
personnelles sur les salariés du client, et son effacement leur est légitimement refusé.
⚠️ Il consigne les faits qui **changent** quelque chose, jamais leur répétition : un appel s'inscrit
s'il rend un verdict différent du précédent, et pas autrement. Trente-cinq relances rendant le même
`202` n'ont aucun signataire — c'est un affichage qui les a déclenchées, non un humain — et
noieraient sous du bruit de mécanique ce que le contrôle vient lire. Trois dates disent tout :
appelé, échéance déclarée, résultat. On ne saura donc jamais combien de fois on a relancé.
_Avoid_ : Register, Record, Journal, AuditTrail, History, registre ⚠️ « registre » désigne l'art. 30
en RGPD, hors périmètre de ce service.

**Prose de travail / prose de preuve** :
Toute prose saisie par l'`Operator` tombe dans l'un des deux régimes, et c'est son **lecteur** qui
les sépare, jamais son contenu. La **prose de travail** — réserve de `Locate`, `OpenQuestion` — est
écrite pendant l'instruction, dit *quelle ligne appartient à qui*, et nomme donc par nature, souvent
des tiers non demandeurs ; elle vit sur le `Case` et meurt à la clôture, le `Ledger` n'en gardant que
le fait daté : « 1 réserve arbitrée le 12/04 ». La **prose de preuve** — motif d'un `Refused`, motif
d'un `Abandoned`, constat de clôture — est écrite à un point de décision, dit *pourquoi on a décidé
cela*, et n'est pas nominative par nature ; elle entre dans le `Ledger` et survit.
La règle tient par le **placement** — deux champs à deux endroits, dont un seul survit — et non par
la discipline d'un `Operator` à qui l'on demanderait de s'auto-censurer dans un champ unique.
_Avoid_ : commentaire, note, annotation

**RetrievedData** :
Ce que les appels `Read` ont ramené des systèmes du client. Hors de l'agrégat : durée de vie propre,
détruite sans réécrire le `Case`. **Effacée à la remise** — pas anonymisée — et n'entrant jamais
dans le `Ledger`. Son séjour est inévitable, la lecture précédant l'effacement ; il doit être
minimal, un service qui entreposerait les exports devenant la donnée la plus concentrée du système
d'information de son client.
_Avoid_ : PersonalData, SubjectData, Payload, Export, contenu

**OpenQuestion** :
Une question datée qui attend une réponse, accrochée à ce qu'elle empêche réellement d'avancer — le
`Case` pour la désignation de la personne, un `Claim` pour le contenu d'un droit. Elle **n'arrête
jamais** le délai de l'art. 12.3 et ne barre jamais la route à l'`Operator`.
_Avoid_ : Blocker, Pending, Hold, Query, blocage ⚠️ le nom `Blocker` ferait dans son nom même la
promesse inverse, et quelqu'un finirait par écrire le code qui bloque.

### Ce qui meurt à la clôture, et ce qui reste

**La clôture détruit le nominatif à l'instant même.** Les `Designations`, le détail de
l'`IdentityDeclaration`, le texte d'origine et toute la prose de travail disparaissent quand
l'`Operator` clôt le `Case` — sans fenêtre de conservation, parce qu'aucun risque juridique ne
demande le nominatif : la preuve d'une procédure est anonyme, et le service ne prouve jamais qu'un
droit a été honoré. Un délai « au cas où » n'aurait entreposé que le sac de désignations de gens
ayant demandé à disparaître.
⚠️ Le geste est donc **irréversible**, seul du dispositif à l'être. Sa parade est un geste délibéré
dans la surface de l'`Operator`, jamais de la donnée gardée en réserve.
⚠️ Une personne qui demande l'effacement de son `Case` **encore ouvert** est servie par la clôture
elle-même — `Abandoned`, et tout le nominatif tombe à l'instant. Il n'existe aucune fonction
d'effacement de `Case` distincte. Le seul arbitrage réel se pose franchement à la personne :
poursuivre l'instruction exige ses `Designations`, donc **poursuivre ou effacer, jamais les deux**.

**Le `Ledger` vit cinq ans à compter de la clôture**, non configurable — la prescription civile de
droit commun, seul horizon qui soit un vrai nombre du droit plutôt qu'une intuition : la preuve vit
aussi longtemps que l'action qu'elle sert à défendre. Une durée réglable par client serait une case
qui pourrit en silence, et de la donnée gardée trop longtemps ne fait aucun bruit. À échéance le
`Ledger` est détruit **en entier** — pas de second étage d'anonymisation, qui rouvrirait
l'expurgation que sa définition ferme.
⚠️ Cette destruction n'est **jamais automatique**. Un `Ledger` échu apparaît dans une **section
propre** de l'écran de la file — sa ligne n'a ni personne, ni droit, ni délai, et son bouton ne doit
jamais voisiner ceux des `Case` — où l'`Operator` le détruit d'un geste délibéré et signé : un
`Ledger` expiré est ainsi une **ligne présente**, vue tous les jours, jamais une ligne manquante que
nulle relecture ne lèverait. La section reste affichée, et vide, les années où rien n'est échu.
⚠️ Deux coûts assumés : un `Operator` inactif garde au-delà de cinq ans — visible, jamais barré — et
la destruction ne laisse **aucune trace**, un `Ledger` détruit ne pouvant consigner sa propre
destruction. On ne prouvera pas qu'on a purgé.

### Les acteurs, et leurs pouvoirs délibérément inégaux

**Operator** :
L'humain, côté client, qui instruit les `Case`. **Seul** à produire une issue : seul à confirmer un
`Claim`, à arbitrer une réserve de `Locate`, à motiver un refus, à clore un `Case`. L'`Adapter` ne
fait avancer que l'exécution, et toujours en réponse à un appel du service. La personne concernée
n'est pas un acteur du cycle.
_Avoid_ : User, Agent, Admin, gestionnaire

⚠️ **Il n'existe aucun troisième moteur.** Ce qui fait remonter du travail dans la file est une
**requête**, évaluée quand l'`Operator` regarde — une propriété de la surface, pas un acteur du
domaine. Aucun nom ne lui est donné : `Timer`, `Scheduler`, `DueWork` et `Reminder` promettraient
tous une chose qui tourne, et quelqu'un finirait par écrire le processus qui tourne — même mécanique
que le refus de `Blocker`.

### Ce que le service ne fait pas

**Omission silencieuse** :
Le régime d'erreur de ce contexte. Omettre un `DeclaredSystem` est une infraction, et rien ne la
signale : elle ne produit pas une ligne fausse, elle produit une **ligne manquante**, qu'aucune
relecture ne peut lever — on ne valide pas l'absence de ce qu'on ne voit pas. Trois conséquences
non négociables en découlent : le service ne présente **jamais** un recensement comme complet,
l'incomplétude est visible par construction, et la déclaration du `Manifest` est un prérequis, pas
une option.
_Avoid_ : oubli, erreur de recensement, faux négatif, angle mort

**Greffier, pas témoin** :
Le service enregistre des **déclarations** horodatées et signées, jamais des faits vérifiés. Un
`Step` `Done` prouve qu'on a déclaré l'avoir fait ; une `IdentityDeclaration` prouve ce que le canal
a affirmé. Le service n'a jamais le droit de bloquer une clôture : un `Case` peut se clore en
constatant qu'un système n'a pas été traité, et cette trace-là est la plus précieuse de toutes.
_Avoid_ : vérification, contrôle, garantie, attestation de conformité
