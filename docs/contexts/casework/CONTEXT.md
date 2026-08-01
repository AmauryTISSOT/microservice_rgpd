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
déclaration d'aujourd'hui ne réécrit pas la preuve d'hier. Il survit à la clôture, anonymisé, et ne
porte jamais de contenu : il dit « un fichier a été remis le 12/04 couvrant 2 systèmes sur 6 »,
jamais ce qu'il y avait dedans.
_Avoid_ : Register, Record, Journal, AuditTrail, History, registre ⚠️ « registre » désigne l'art. 30
en RGPD, hors périmètre de ce service.

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

### Les acteurs, et leurs pouvoirs délibérément inégaux

**Operator** :
L'humain, côté client, qui instruit les `Case`. **Seul** à produire une issue : seul à confirmer un
`Claim`, à arbitrer une réserve de `Locate`, à motiver un refus, à clore un `Case`. L'`Adapter` ne
fait avancer que l'exécution, et toujours en réponse à un appel du service ; une minuterie ne
transite rien, elle remonte dans la file. La personne concernée n'est pas un acteur du cycle.
_Avoid_ : User, Agent, Admin, gestionnaire

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
