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
l'ancienneté d'une `OpenQuestion`, une `Delivery` non remise, un `EvidenceLog` échu : tout se recalcule à
l'instant où l'`Operator` regarde. Rien ne tourne, donc rien ne peut s'arrêter en silence — un
processus de fond interrompu rendrait une file **vide et rassurante**, soit l'`Omission silencieuse`
sous sa forme la plus dangereuse, et ferait dépendre la preuve de ce qu'un `cron` ait tourné.
⚠️ De ces cinq échéances, quatre ne sont que des **colonnes** sur des lignes déjà présentes : le
`Case` est ouvert, il est déjà dans la file. L'échéance **trie**, elle n'ajoute pas. Seul le `EvidenceLog`
échu fait naître une ligne, son `Case` étant clos depuis cinq ans — d'où sa section propre sur
l'écran, qui tient aussi lieu de parade au geste irréversible qu'il porte.
⚠️ **Aucun seuil, nulle part.** Une `OpenQuestion` affiche la date à laquelle elle a été posée,
jamais « sans réponse depuis N jours » : aucun nombre du droit ne fonde N, un seuil réglable serait
une case à laisser pourrir de plus, et trier sur lui ferait passer devant un compteur sans force
juridique. On montre le fait, l'`Operator` juge.

**ReceptionDate** :
La date de réception **et le régime sous lequel le service la sait** : déclarée par quelqu'un, ou
**tenue pour défaut**. Les deux ne se confondent jamais — une date nue serait indiscernable d'une date
affirmée par un humain, et le drapeau vit donc à côté d'elle, là où aucun chemin d'écriture ne peut
poser l'une sans l'autre.
À défaut de déclaration, le service tient **neuf jours pour déjà courus** — `J+9 (défaut)` — et
l'affiche ainsi : il nomme la **règle appliquée**, jamais la date qu'elle a produite, qui se lirait
comme un fait. Le nombre n'est pas réglable : une case à régler serait une case à laisser pourrir, et
sa valeur basse serait celle que tout le monde garderait. Le régime ne change **rien au calcul** de
l'échéance — un dossier dont personne n'a déclaré la date n'a droit à aucun délai de faveur.
⚠️ **Elle ne se confond pas avec l'instant du dépôt, et c'est le dépôt manuel qui l'exige.** Une
demande transcrite d'une boîte aux lettres a été reçue **avant** d'entrer dans le service, parfois de
beaucoup. La preuve garde donc les **deux** dates : celle du geste — quand le service a su — et celle
de la réception, avec son régime. Une seule aurait fait choisir entre dater le geste et dater le
délai, et dater la ligne d'ouverture de la réception ferait dire à la preuve que le service savait
depuis trois semaines.
_Avoid_ : ReceivedAt, StartDate, date d'entrée ⚠️ « date d'entrée » nommerait le geste du service là
où le délai part de la réception par le client.

**ExtensionDeclaration** :
Ce que l'`Operator` déclare lorsqu'il prolonge de deux mois au titre de l'art. 12.3 : un **motif**,
la **date à laquelle il a informé la personne** de la prolongation et de ses motifs, et la date de sa
déclaration. Le service ne prolonge rien et ne notifie personne — il **réclame** une déclaration et
l'enregistre, comme il le fait de l'identité et de la remise.
La **signature n'est pas sur l'objet** : elle est sur la ligne de `EvidenceLog` que le geste écrit, comme
celle de tout autre geste d'humain. Une signature portée deux fois finirait par se contredire, et
c'est la preuve — non le dossier — qui doit nommer qui a répondu.
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
Un appel `Read` porte, en plus des `Designations`, le `DataSubjectRight` **au titre duquel** on lit —
jamais la forme attendue. L'art. 20 a un périmètre matériel plus étroit que l'art. 15 et il se décide
ligne par ligne : seule l'application peut le trancher, et elle tranche du même geste le périmètre et
la forme. Imposer la forme depuis le service reviendrait à arbitrer la seule chose qu'il ne sait pas
arbitrer, ayant renoncé au grain du champ.
_Avoid_ : Operation (pris par l'art. 4.2), acte, verbe, diligence, permission

**Désaccord `Manifest`/`Adapter`** :
Ce qu'un refus d'`Adapter` révèle : le paysage déclaré et le programme qui le sert ne parlent plus
du même système, ou les deux moitiés du secret ont divergé. Il se signale **une seule fois, au grain
du déploiement** — une panne unique n'est pas N pannes, et la crier une fois par dossier ferait
dépendre le volume du signal du nombre de demandes en cours, qui n'en dit rien. Il ne corrige jamais
le `Manifest` en silence : un humain tranche lequel des deux avait tort.
⚠️ Le `EvidenceLog`, lui, garde **toutes** les tentatives, datées, dossier par dossier : ce sont deux
lecteurs et deux grains — l'exploitant d'un côté, le contrôle de l'autre.
_Avoid_ : erreur, panne, incident, alerte

**Adapter** :
Le programme que l'application du client implémente et que le service appelle pour exercer une
`Capability` sur ses `DeclaredSystem`. Le sens est **unique** : le service appelle toujours,
l'application ne rappelle jamais.
Son contrat est écrit dans [`docs/api/adapter.md`](../../api/adapter.md), **clause de périmètre
comprise** : le contrat ne dit jamais « authentifiez-vous », il dit un secret partagé **et** un
`Adapter` hors d'atteinte de l'extérieur — les deux ensemble, jamais l'un sans l'autre.
Il répond de quatre façons, et de quatre seulement : il **sert**, il **diffère** en déclarant une
échéance, ou il refuse — d'un des **deux refus** qui ne se confondent pas, `SecretRefused` et
`SystemNotServed`, parce qu'on ne les répare pas au même endroit. Tout le reste — serveur muet,
statut hors contrat, différé sans échéance lisible — est une **panne**, et n'entre dans aucun
vocabulaire fermé : personne ne saurait qu'en conclure.
⚠️ Le mot **verdict** est réservé à la `Qualification` et ne nomme jamais la réponse d'un
`Adapter` : une machine ne rend pas d'issue.
_Avoid_ : Connector, Plugin, Integration, Webhook ⚠️ pour sa réponse : Verdict, Result, Status

**Vérification du `Manifest`** :
⚠️ **Le mot « vérification » est sur la liste _Avoid_ de `Enregistré, jamais vérifié`, et il est repris ici
sciemment** — il n'y a pas de contradiction, parce que les deux ne portent pas sur la même chose. Ce
qui reste interdit, c'est de vérifier ce qu'un humain **déclare** : un `Step` `Done` prouve qu'on a
déclaré l'avoir fait, jamais qu'il l'a été. Ici, l'objet vérifié n'est pas une déclaration sur le
monde mais **une déclaration sur le service lui-même** — quelle adresse répond, et à quoi — et c'est
la seule chose que le service puisse constater de ses propres yeux, en appelant. Tout le reste reste
enregistré, jamais vérifié.
L'opération d'exploitation qui confronte le catalogue déclaré à ce que les `Adapter` servent
réellement, système par système, pour les seuls `DeclaredSystem` dotés d'une adresse. Elle
**rapporte** l'écart et ne corrige **jamais** le `Manifest` : un humain tranche lequel des deux avait
tort. Son grain est le **déploiement** — elle ne touche aucun `Case` et n'écrit rien au `EvidenceLog`.
Elle ne se déclenche que sur demande : rien ne tourne, sans quoi un processus interrompu rendrait un
rapport **vide et rassurant**, soit l'`Omission silencieuse` sous sa forme la plus dangereuse.
⚠️ **Une seule `Capability` sur quatre est vérifiable, et les trois autres sont nommées comme telles.**
`Locate` se sonde — c'est le plancher, une requête et rien de destructeur. `Erase` et `Rectify` ne se
constateraient qu'en détruisant ou en réécrivant des données réelles, **définitivement** ; `Read`
n'a pas encore de forme d'appel fixée, le contrat lui promettant un champ de plus, et sonder avant
qu'elle le soit enverrait chez le client une requête que le contrat ne décrit pas. Elles sont donc
rapportées **non vérifiables**, une par une, plutôt que tues — le silence les aurait fait lire comme
conformes.
⚠️ **Un `Adapter` nu ne prouve rien de son catalogue non plus.** Son `200` peut venir d'une route qui
sert tout à tout le monde, système inconnu compris : la vérification ne le rappelle pas sous le vrai
secret et ne conclut rien de ses capacités, plutôt que d'écrire un accord que personne n'a constaté.
⚠️ **Non vérifiable et sans conclusion ne fusionnent pas**, pour la raison qui sépare `OutOfReach` de
`Untreated` : le premier est structurel et annonçable au premier jour, le second est le constat d'un
jour, que la prochaine passe peut lever.
_Avoid_ : audit, contrôle, conformité, synchronisation, réconciliation ⚠️ « synchroniser » promet
dans son nom la correction que cette opération refuse.

**Appel au faux secret** :
Un `Locate` — plancher obligatoire, non destructeur, sac de désignations vide — envoyé à un `Adapter`
avec un secret **volontairement invalide**. Un `200` en réponse prouve un `Adapter` **nu**,
c'est-à-dire ouvert à qui l'atteint : il a travaillé pour un appelant que le contrat lui demandait de
refuser. C'est un résultat d'**exploitation**, jamais une affaire de dossier.
Le secret présenté est **fixe et public**, et sans aucun rapport avec celui du déploiement : un faux
dérivé du vrai le livrerait, octet par octet, à l'`Adapter` même dont on soupçonne qu'il ne garde
rien. Sa publicité ne coûte rien — il n'ouvre aucune porte, il est fait pour s'en faire fermer une.
⚠️ **La sonde ne lit jamais le corps de ce qu'elle reçoit.** Le corps d'un `200` rendu à un secret
faux est fait des données personnelles que l'`Adapter` n'aurait pas dû servir : les lire ferait
entrer dans le service, au titre de la vérification, exactement ce qu'elle vient dénoncer.
⚠️ Seul le `200` — et le `202`, qui est un travail **pris en charge** — démontre quelque chose. Un
refus de secret dit que la porte a été fermée **ce jour-là, sur ce chemin-là**, jamais que le
périmètre réseau, l'autre moitié du dispositif, est en place ; et un serveur muet ne se range pas
avec les portes fermées.
_Avoid_ : test de pénétration, scan, audit de sécurité, attaque

### La personne, et comment on la désigne

**Designation** :
Un attribut déclaré qui **pointe vers** la personne — une adresse électronique, un nom, un
téléphone, une référence interne. Il ne prétend ni à l'unicité ni à l'exactitude : la personne n'a
pas d'identifiant, et l'identifiant natif d'une application est souvent la clé qui ouvre le moins
de portes.
_Avoid_ : Identifier, SubjectId, Identity, Key, Selector

**Designations** :
Le sac de `Designation` d'un `Case` — la seule identité qui circule. Il s'**enrichit** en cours
d'instruction : une `Reservation` que l'`Operator` rattache et qui propose une désignation nouvelle
la verse au sac, où elle sert aux appels suivants. Ce qui n'apporte rien reste local au système qui
l'a produit, en vocabulaire opaque.
⚠️ **Seul un rattachement enrichit** : une réserve écartée laisse le sac intact — c'est justement ce
qu'un humain vient de dire. Et le sac part en **copie** à chaque appel : ce qui a traversé la
frontière est ce sous quoi *cet* appel-là a cherché, qu'un arbitrage postérieur ne doit pas pouvoir
réécrire après coup.
_Avoid_ : IdentityGraph, Aliases, Profil

**IdentityDeclaration** :
Ce que le canal d'entrée a **déclaré** sur l'identité du demandeur. Vocabulaire fermé de quatre
valeurs — `ApplicationSession`, `ChannelControl`, `OperatorAttested`, `Unverified` — portée par le
`Case`, puisque l'identité est une propriété de la personne et non d'un droit. Le service enregistre
la déclaration et n'en juge **jamais** la valeur ; il ne vérifie lui-même aucune identité.
`Unverified` doit exister : sans la valeur laide, l'opérateur pressé coche la valeur propre et le
service fabrique un faux au lieu d'enregistrer un vide.
`Unverified` et `OperatorAttested` sont les deux valeurs qui **ne reposent sur aucun contrôle du
canal** : les deux autres s'appuient sur un dispositif que le client a mis en place et qui existe
indépendamment d'un dossier donné ; celles-ci reposent sur ce qu'un humain a fait — ou n'a pas fait
— pour *ce* dossier-là, et lui seul peut dire quoi. C'est très exactement ce qu'une
`IdentityMotivation` vient recueillir.
_Avoid_ : Authentication, Verification, TrustLevel, niveau de confiance ⚠️ le nom du prédicat évite
« confiance » : il ne dit rien de ce que la déclaration vaut, seulement de **qui** l'a produite.

**IdentityMotivation** :
Ce que l'humain a pesé avant d'ouvrir un droit sous une identité qui ne repose sur aucun contrôle du
canal. Elle s'écrit en **deux champs qui ne se confondent jamais** : une **méthode**
(`IdentityVerificationMethod`), vocabulaire fermé de quatre valeurs qui **se compte** et **survit à
la clôture** dans le `EvidenceLog` — `AttributeCrosscheck` (« recoupement d'un attribut que le demandeur
n'a pas reçu de nous »), `PersonalRecognition`, `CallbackOnKnownContact`, `None` — et un **détail**
en prose libre, irréductiblement nominatif, qui vit sur le `Case` et **meurt avec lui**. Le contrôle
juge ainsi la *pratique* sans qu'un seul nom lui survive. La règle tient par le **placement** — deux
champs à deux endroits, dont un seul survit — comme pour le texte qui meurt et le texte qui reste.

Elle est **réclamée** là où un accès pourrait être remis à un imposteur, c'est-à-dire sur le
croisement `Access` × (`Unverified` | `OperatorAttested`), et **nulle part ailleurs** : la croiser
avec les six autres droits ferait réclamer une motivation à chaque dépôt, et celle qui compte se
noierait dans les autres.

⚠️ **Elle ne barre jamais la route.** Un dossier ouvert sans elle est un dossier **faible et visible
comme tel** : la réclamation reste affichée tant qu'elle n'est pas satisfaite, pendant que le délai
court. Un refus à l'entrée l'aurait fait disparaître — soit en renvoyant la personne à son silence,
soit en faisant cocher n'importe quoi.

⚠️ **`None` est une réponse ; son absence n'en est pas une.** `None` est ce que quelqu'un a déclaré
après avoir regardé ; l'absence est le fait que personne n'ait pesé. Les confondre ferait signer par
défaut un aveu que personne n'a écrit — c'est la distinction que `ReceptionDate` tient pour la date,
et pour la même raison.

⚠️ **La réclamation peut être satisfaite après coup, et elle doit pouvoir l'être.** Une exigence
qu'on ne peut pas satisfaire cesse d'être lue : un bandeau permanent s'apprend à ne plus se voir, et
la faiblesse qu'il devait rendre visible redeviendrait invisible. Une motivation écrite plus tard
s'ajoute au `EvidenceLog` sans réécrire la ligne d'ouverture — l'**écart entre les deux dates** est
précisément ce que le contrôle doit voir, un accès pesé le vendredi n'étant pas un accès pesé avant
d'être ouvert le lundi. Elle ne touche à aucun `Claim` : c'est ce que le gel de `ClaimOrigin`
protège.

⚠️ **Le mot `Verification` est sur la liste _Avoid_ d'`IdentityDeclaration`, et il est repris ici
sciemment** — `IdentityVerificationMethod`. Il n'y a pas de contradiction : ce que la liste interdit
est de nommer *ce que le service aurait vérifié*, car il ne vérifie aucune identité. Ce type ne
nomme pas une vérification du service, mais **ce qu'un humain déclare avoir fait** — et le service
l'enregistre sans en juger la valeur, comme le reste.
_Avoid_ : Justification, Reason, Rationale, IdentityProof, preuve d'identité

⚠️ Aucune pièce d'identité n'entre dans le service, tous canaux confondus. Le `EvidenceLog` consigne le
**fait** qu'une pièce est passée, jamais la pièce.

**ClaimOrigin** :
D'où vient la reconnaissance d'un droit dans un `Case` : `Named` (la personne l'a désigné),
`Attested` (l'`Operator` l'affirme), `Proposed` (une `Qualification` l'a proposé, un humain le
confirme dans le `Case`). Un `Claim` `Proposed` non confirmé est une `OpenQuestion` — visible
pendant que le délai court.

**Un `Claim` garde la porte sous laquelle il est né.** L'origine **et** l'`IdentityDeclaration` en
vigueur à cet instant se **figent** sur le `Claim`, sur sa propre ligne et jamais par une jointure
vers le `Case` : le dossier porte l'identité déclarée d'aujourd'hui, qui se reprend, mais une
déclaration relevée en fin de dossier ne réécrit pas la preuve d'hier — l'accès ouvert lundi l'a été
sous `Unverified`, et le rappel passé vendredi ne le rend pas rétroactivement propre.

⚠️ `Proposed` **n'est pas un quatrième `ClaimState`**, et la confirmation n'en est pas un non plus :
la provenance d'un droit et l'état de la réponse due à la personne sont deux questions distinctes.
Un état de plus aurait fait porter à la réponse une question de provenance, qu'il aurait fallu faire
retomber quelque part à la clôture.

⚠️ **Il n'existe aucun vestibule.** La confirmation a lieu **dans le `Case` ouvert**, pendant que le
mois de l'art. 12.3 court : une salle d'attente aurait fait passer pour « pas encore commencé » un
compteur déjà lancé, et la demande y aurait attendu hors de la file.
_Avoid_ : Source, Channel, provenance

### Ce qu'un `Locate` rapporte, et ce qu'il refuse de trancher

**Locating** :
Ce qu'un `Locate` a rendu, pour **un** `DeclaredSystem` d'**un** `Case` : la dernière issue de
l'appel, sa date, l'échéance d'un `202`, le **nombre de `Designation` sous lesquelles on avait
cherché**, un noyau certain de références opaques, et les `Reservation` que le système n'a pas su
trancher. Un système par ligne, réécrite à chaque appel servi — sauf les réserves, qui **fusionnent**
plutôt qu'elles ne se remplacent : une réserve déjà arbitrée par un humain n'est jamais réécrite ni
retirée par un appel ultérieur.
⚠️ **Un zéro n'est pas une absence de `Locating`.** « Appelé, rien trouvé » a une valeur de preuve
que « pas appelé » n'a pas, et une panne laisse donc le système **sans localisation** plutôt qu'avec
un zéro que personne n'a déclaré.
⚠️ Le compte des désignations n'est pas décoratif : c'est lui qui dit si la réponse d'hier répond
encore à la question qu'on pose aujourd'hui, et donc **quand on rappelle**. Il n'existe aucune
minuterie ; les quatre raisons de rappeler sont : on n'avait jamais appelé, le sac s'est enrichi,
l'échéance d'un `202` est passée, l'appel avait été refusé. Toutes se constatent à l'ouverture du
dossier.
_Avoid_ : Attachment, Search, Lookup, Result, LocateRecord, recherche ⚠️ `Attachment` ferait d'un
`Locate` à zéro « un rattachement portant zéro rattachement » ; `Record` est sur la liste du
`EvidenceLog`.

**Reading** :
Ce qu'un `Read` a **tenté** sur **un** `DeclaredSystem` au titre d'**un** `DataSubjectRight` : la
dernière issue de l'appel, sa date, l'échéance d'un `202`, et le nombre de `Designation` sous
lesquelles on avait lu. Elle vit **dans** le `Case`, à la différence de la `RetrievedData` qu'elle
accompagne — elle ne porte aucun octet, et ce qui reste ici est ce qui **meurt avec le dossier**
tandis que la pièce, elle, meurt à la remise.
⚠️ **Son grain est (droit, système)**, là où celui du `Locating` est le système seul : un `Locate`
cherche *la personne* et la chercher deux fois parce qu'elle réclame deux droits enverrait deux fois
la même requête ; un `Read` lit *au titre d'un droit*, et le périmètre n'est pas le même.
⚠️ **L'absence de `Reading` n'est pas une pièce vide.** « Pas appelé » et « appelé, rien rendu » sont
deux déclarations différentes, et une panne laisse donc le couple sans lecture plutôt qu'avec une
pièce vide que personne n'a servie. Aucune valeur ne dit « en panne » : une panne n'est ni réponse ni
refus, et ne laisse rien.
⚠️ Les quatre raisons de rappeler sont celles du `Locating`, et une lecture **déjà servie n'en est
pas une** : sa pièce est détenue, elle répond à la question qu'on pose sous le sac d'aujourd'hui, et
repasser rapatrierait une seconde fois les données de quelqu'un — c'est-à-dire allongerait le séjour
que tout ce dispositif cherche à raccourcir.
_Avoid_ : Extraction, Fetch, Retrieval, ReadRecord, lecture ⚠️ `Retrieval` se confondrait avec la
`RetrievedData` qu'elle n'est justement pas ; `Record` est sur la liste du `EvidenceLog`.

**RetrievedPiece** :
Ce qu'un `Read` **servi** vient de rendre, avant que le service n'en fasse quoi que ce soit : une
`TransportEnvelope` et des octets. C'est la forme sur le **fil**, et elle ne porte ni dossier, ni
droit, ni date — l'`Adapter` n'en connaît aucun. La `RetrievedData` est ce qu'elle devient une fois
attribuée et datée ; l'une traverse la frontière, l'autre est détenue.
_Avoid_ : File, Document, Attachment, Blob, fichier ⚠️ `File` et `Document` promettent une chose
nommée et structurée, alors que ce sont des octets dont on ne sait rien.

**TransportEnvelope** :
Tout ce que le service sait d'une pièce, et il n'en saura jamais rien d'autre : un `Content-Type` et
un nom de fichier, **recopiés sans interprétation** de ce que le transport lui a mis dans la main. Du
nom, seul le **dernier segment** est gardé ; à défaut d'en-tête relisible, il retombe sur le
`system_id` — `send_file()` seul doit suffire, et ne rien écrire du tout ne doit pas perdre la pièce.
⚠️ **Elle ne valide rien et ne refuse rien.** Un `Content-Type` fantaisiste est recopié tel quel : le
service ne prétend pas savoir ce que l'application a exporté, et le seul lecteur de ces deux champs
est l'humain qui ouvrira la pièce.
_Avoid_ : MediaType, Metadata, FileInfo, Attachment ⚠️ `Metadata` promettrait une description du
contenu, alors que ce sont deux chaînes que personne n'a vérifiées.

**OpaqueReference** :
Le mot par lequel **l'application** désigne une ligne qu'elle a rattachée — `clients#1203`,
`/var/log/app-2026-03.log:88`. Le service ne la découpe pas, ne la compare pas d'un système à
l'autre, n'en tire aucun compte : il la garde et la réaffiche mot pour mot à un humain qui saura la
lire chez le client. C'est le grain du champ, fermé dans le `Manifest`, qui l'est encore au retour.
_Avoid_ : Id, Key, RowId, Locator, identifiant ⚠️ tous promettraient une structure que le service
s'interdit de lire.

**Reservation** :
Une ligne qu'un système a trouvée **sans pouvoir dire si c'est la personne**, avec le **motif** du
doute en prose française et, éventuellement, les `Designation` que cette ligne-là propose. Elle porte
un `ReservationState` — `Awaiting`, `Attached`, `SetAside` — dont seuls les deux derniers sont
tranchés, et toujours par un `Operator` nommé et daté au `EvidenceLog`.
⚠️ **Une réserve n'est pas un demi-rattachement.** Tant que personne ne l'a arbitrée, elle ne compte
pour aucun rattachement : c'est ce qui fait réclamer un constat sur un `Step` `Done` d'un système qui
n'en porte aucun. Le service ne tranche **jamais** de lui-même — il n'existe ni score, ni seuil, ni
règle de majorité.
⚠️ **Le motif est du texte qui meurt**, lu tel quel et jamais analysé : il nomme par nature des
tiers non demandeurs — « l'autre Jean Dupont » —, vit sur le `Case` et meurt à la clôture. Une
réserve **sans** motif est une panne du contrat, pas une réserve : ce serait un doute qu'on
demanderait de trancher sans dire lequel.
⚠️ **Les `designations` d'une réserve sont le seul champ que le service interprète**, et seulement
après le rattachement. Sans elles, la réserve reste locale et opaque : elle s'arbitre, mais elle
n'apprend rien à personne d'autre.
_Avoid_ : Candidate, Match, Suggestion, Doubt, Ambiguity ⚠️ `Match` et `Candidate` promettent un
rapprochement que le service ne fait pas, et un score qu'il n'a pas.

### Ce que le service détient, et pour combien de temps

**EvidenceLog** :
La matière de preuve d'un `Case` : qui a déclaré quoi et quand, les motifs, les constats, les
tentatives, le compte et la provenance des `Designation`. En **ajout seul**, daté et signé — la
déclaration d'aujourd'hui ne réécrit pas la preuve d'hier. Il ne porte jamais de contenu : il dit
« un fichier a été remis le 12/04 couvrant 2 systèmes sur 6 », jamais ce qu'il y avait dedans.
⚠️ Ce dénombrement s'arrête au `EvidenceLog` et ne descend **jamais** dans la `Delivery` : son lecteur est
le contrôle, qui juge une pratique et pour qui « 2 sur 6 » est une mesure. Écrit à la personne, le
même chiffre lui affirmerait que le client a exactement six systèmes — donnant à une déclaration qui
vieillit exprès l'autorité d'un recensement, ce que l'`Omission silencieuse` interdit.
⚠️ Du sac de désignations il garde le **compte et la provenance, jamais les valeurs** : « recherché
sous 2 désignations, dont 1 ajoutée par arbitrage le 12/04 ». Le contrôle juge ainsi l'effort de
recherche — a-t-on cherché sous une seule adresse, ou sous ce qu'on avait ? — sans qu'une seule
désignation lui survive. Une réserve arbitrée y laisse le **sens** de l'arbitrage, son système, son
signataire et sa date ; jamais sa référence opaque ni le motif que l'application avait écrit, qui
sont du texte qui meurt et disparaissent avec le `Case`.
Il est **anonyme par construction, jamais par expurgation** : on n'y écrit aucune `Designation` ni
aucun nom de personne concernée, dès la première ligne. L'anonymiser à la clôture aurait exigé de le
réécrire — dans la seule structure du dispositif dont l'invariant est qu'on ne la réécrit pas.
⚠️ Il n'est anonyme que **côté personne concernée** : il nomme l'`Operator`, définitivement, parce
que « par qui » est un tiers de ce que le service prouve. C'est donc un fichier de données
personnelles sur les salariés du client, et son effacement leur est légitimement refusé.
⚠️ Il consigne les faits qui **changent** quelque chose, jamais leur répétition : un appel s'inscrit
s'il rend un verdict différent du précédent, et pas autrement. Cette règle est tenue par
l'**appelant**, jamais par le `EvidenceLog` : celui-ci ne se relit pas — une écriture qui lirait la ligne
d'avant serait une écriture qu'une ligne d'avant pourrait faire mentir. Tant qu'aucune relance
n'existe, chaque tentative part d'un geste distinct et s'inscrit. Trente-cinq relances rendant le même
`202` n'ont aucun signataire — c'est un affichage qui les a déclenchées, non un humain — et
noieraient sous du bruit de mécanique ce que le contrôle vient lire. Trois dates disent tout :
appelé, échéance déclarée, résultat. On ne saura donc jamais combien de fois on a relancé.
_Avoid_ : Register, Record, Journal, AuditTrail, History, registre ⚠️ « registre » désigne l'art. 30
en RGPD, hors périmètre de ce service.

**SignerVerification** :
Ce que valait le nom d'un `Operator` au moment où il l'a saisi. Il s'écrit au `EvidenceLog` **en même temps
que le nom et par le même geste** : un nom enregistré seul serait relu dans dix ans comme si quelqu'un
s'était identifié. Une seule valeur aujourd'hui — `Unauthenticated` —, et c'est la raison d'être du
type : la surface n'authentifie personne, choix de PoC assumé, et le jour où elle le fera une seconde
valeur entrera ici sans que le `EvidenceLog` d'aujourd'hui devienne indiscernable de celui de demain.
_Avoid_ : Authentication, TrustLevel, niveau de confiance ⚠️ la valeur ne dit **rien** de la confiance
qu'on accorde au nom, seulement de ce que le service a vérifié — c'est-à-dire rien.

**Texte qui meurt / texte qui reste** :
Toute prose saisie par l'`Operator` tombe dans l'un des deux régimes, et c'est son **lecteur** qui
les sépare, jamais son contenu. Le **texte qui meurt** — réserve de `Locate`, `OpenQuestion` — est
écrit pendant l'instruction, dit *quelle ligne appartient à qui*, et nomme donc par nature, souvent
des tiers non demandeurs ; il vit sur le `Case` et meurt à la clôture, le `EvidenceLog` n'en gardant que
le fait daté : « 1 réserve arbitrée le 12/04 ». Le **texte qui reste** — motif d'un `Refused`, motif
d'un `Abandoned`, constat de clôture — est écrit à un point de décision, dit *pourquoi on a décidé
cela*, et n'est pas nominatif par nature ; il entre dans le `EvidenceLog` et survit.
La règle tient par le **placement** — deux champs à deux endroits, dont un seul survit — et non par
la discipline d'un `Operator` à qui l'on demanderait de s'auto-censurer dans un champ unique.
_Avoid_ : commentaire, note, annotation

**RetrievedData** :
Ce que les appels `Read` ont ramené des systèmes du client. Hors de l'agrégat : durée de vie propre,
détruite sans réécrire le `Case`. **Effacée à la remise** — pas anonymisée — et n'entrant jamais
dans le `EvidenceLog`. Son séjour est inévitable, la lecture précédant l'effacement ; il doit être
minimal, un service qui entreposerait les exports devenant la donnée la plus concentrée du système
d'information de son client.
Une pièce par couple (`DataSubjectRight`, `DeclaredSystem`) — le périmètre matériel de l'art. 20
n'étant pas celui de l'art. 15, une pièce par système seul aurait forcé à retenir le plus large —, et
le service **n'en ouvre jamais le corps** : c'est un flux d'octets,
écrit dans le vocabulaire de l'application et non dans un vocabulaire commun — il n'en existe aucun
sur le terrain, et en inventer un le ferait payer à chaque `Adapter`. Le grain du champ, fermé dans
le `Manifest`, l'est donc aussi au retour. Le service n'en connaît que ce que le **transport** lui
met dans la main — `Content-Type`, `Content-Disposition: filename=` — qu'il **recopie sans
l'interpréter**, en ne gardant du nom que son dernier segment ; à défaut d'en-tête il retombe sur le
`system_id`. Il en résulte qu'il **rassemble sans jamais fusionner** : les pièces se posent côte à
côte, elles ne se concatènent pas.
⚠️ Le service ne saura donc jamais qu'un `Adapter` a servi du PDF pour une portabilité, ni qu'il a
ignoré le `DataSubjectRight` de l'appel. Faute réelle, silencieuse, imputable au client — même régime
que le `Step` `Done` déclaré et jamais vérifié.
_Avoid_ : PersonalData, SubjectData, Payload, Export, contenu

**OpenQuestion** :
Une question datée qui attend une réponse, accrochée à ce qu'elle empêche réellement d'avancer — le
`Case` pour la désignation de la personne, un `Claim` pour le contenu d'un droit. Elle **n'arrête
jamais** le délai de l'art. 12.3 et ne barre jamais la route à l'`Operator`.
La question de la désignation naît sous **trois** conditions, et il les faut toutes : tous les
`Locate` ont répondu, aucun n'a rien rattaché, et aucune `Reservation` n'attend un humain. Un
système qui a différé ou refusé n'a pas répondu — conclure avant de l'avoir entendu ferait poser une
question dont on ne sait pas encore si elle se pose. Un `Case` sans aucun système atteignable n'en
pose aucune : il n'y a pas six zéros à mal lire, il n'y a eu aucun appel.
⚠️ **Une réserve en attente n'est pas un zéro**, et c'est la condition la moins évidente des trois.
Elle ne compte pour aucun rattachement, mais quelque chose a bel et bien été trouvé sous ce qu'on
avait : ce qui manque est un **regard**, pas une désignation de plus. Poser la question là ferait
afficher « aucun rattachement nulle part » juste au-dessus des lignes que le système vient de rendre.
Elle ne se pose qu'**une fois** par sujet — la reposer à chaque ouverture de dossier ferait de sa
date le reflet du dernier regard plutôt que celui du jour où le doute est né — et elle se **retire**
le jour où le dossier y répond. Une question sans issue deviendrait un bandeau permanent, et un
bandeau permanent s'apprend à ne plus se voir : c'est la mécanique qui vaut déjà pour la réclamation
d'une `IdentityMotivation`, et pour la même raison. Rien n'est perdu de la preuve — le jour de la
question est au `EvidenceLog`, ce qui y a répondu porte sa propre ligne datée, et le contrôle lit l'écart
entre les deux. L'écran, lui, ne montre que ce qui attend encore.
_Avoid_ : Blocker, Pending, Hold, Query, blocage ⚠️ le nom `Blocker` ferait dans son nom même la
promesse inverse, et quelqu'un finirait par écrire le code qui bloque.

### Ce qui est remis à la personne

**Delivery** :
Ce que le service tend à l'`Operator` au titre d'**un** `Claim` : la `DeliveryLetter` et les
`RetrievedData` de ce `Claim`, rassemblées. Une par `Claim`, jamais une par `Case` — deux droits sont
deux réponses, deux dates de remise, et le service ne sait de toute façon pas fusionner.
La remise se fait en **deux gestes distincts** : télécharger, puis déclarer remis. Le service tend le
paquet à l'`Operator`, **jamais à la personne** — il ne s'expose pas hors du réseau de son client et
ne fait confiance à aucune coordonnée qu'il n'a pas vérifiée, l'`IdentityDeclaration` pouvant valoir
`Unverified`. C'est le second geste, et lui seul, qui date la remise au `EvidenceLog` et détruit les
`RetrievedData` : **la remise est une affirmation, pas un transfert d'octets**, et confondre les deux
ferait dater la preuve du moment où un fichier a quitté un serveur.
⚠️ Le service ne prouvera donc **jamais** que la personne a reçu quoi que ce soit — c'est enregistré,
jamais vérifié. ⚠️ Entre les deux gestes le paquet existe en deux exemplaires, dont l'un hors de portée pour
toujours ; une `Delivery` téléchargée et non déclarée remise remonte dans la file de l'`Operator`,
ligne présente vue tous les jours plutôt que ligne manquante.
⚠️ **Le second geste demande le premier.** Déclarer remis un paquet que personne n'a jamais eu en
main daterait au `EvidenceLog` un geste qui n'a pas eu lieu, et détruirait des `RetrievedData` que
personne n'a tendues.
⚠️ **La `Delivery` n'est jamais gardée** : elle est recomposée à chaque geste à partir des
`RetrievedData` détenues. L'entreposer aurait fait un second exemplaire des données de quelqu'un,
dont l'effacement serait devenu une seconde chose à ne pas oublier.
Elle **assemble sans jamais fusionner** : une pièce par `DeclaredSystem`, chacune sous un dossier
portant son identifiant. Deux applications peuvent servir une pièce du même nom, et mettre l'archive
à plat en aurait écrasé une — une réponse incomplète que rien n'aurait signalée.
La ligne `DeliveryDeclared` du `EvidenceLog` porte le rapport « 2 systèmes sur 6 » : combien la réponse
couvrait, sur combien elle avait à répondre. ⚠️ Les deux moitiés sont comptées sur **le même
ensemble** — celui que la `DeliveryLetter` énumère —, jamais l'une sur la page et l'autre sur le
catalogue du jour : deux ensembles mesurés l'un contre l'autre écriraient « 6 sur 5 » le jour où
quelqu'un retire du catalogue un système que le dossier portait. Il est **écrit** plutôt que relu
plus tard : le recensement vieillit exprès, et le relire dans trois ans jugerait la pratique d'hier
au paysage de demain. ⚠️ Ce rapport **s'arrête au `EvidenceLog`** et ne descend jamais dans la
`DeliveryLetter`.
_Avoid_ : Export, Package, Response, Bundle, Download, envoi

**DeliveryLetter** :
La page que le service écrit lui-même dans chaque `Delivery`, seul texte du dossier dont il soit
l'auteur. Elle range les `DeclaredSystem` du `Case` en **trois listes** : ceux dont une pièce est
jointe ; ceux qui ont été interrogés **sans qu'aucun rattachement soit trouvé sous les `Designations`
dont on dispose** ; ceux qui ne sont pas couverts, nommés un par un dans les mots du champ « contient »
du `Manifest`. Elle se clôt en disant que cette liste est celle des systèmes **recensés**, et qu'elle
ne garantit pas qu'il n'en existe pas d'autres.
Une pièce **vide** n'est pas jointe à l'archive : elle est une réponse datée, la page la range parmi
les systèmes interrogés sans rattachement, et joindre en plus un fichier de zéro octet ferait deux
dires contradictoires dans le même envoi.
Elle **énumère et ne compte jamais** : pas de total, pas de ratio, pas de dénominateur — nommer
« l'export commercial transmis chaque mois à notre agence » est actionnable pour la personne là où
« 4 sur 6 » ne lui apprend rien et lui ment sur l'exhaustivité du recensement.
La troisième liste ne fusionne pas avec la première : une pièce vide n'est pas une réponse. Et la
deuxième ne dit jamais « vous n'avez rien chez nous » — un `Locate` ne distingue pas « cherché, aucun
rattachement » de « désignation insuffisante », et la seule phrase vraie porte ce doute avec elle,
invitant la personne à fournir d'autres `Designation`.
Le service l'écrit **sans ouvrir une seule pièce** : le `Manifest` et les `Step` du `Case` lui
suffisent, l'enveloppe du transport distinguant à elle seule la pièce absente, la pièce vide et la
pièce pleine. L'incomplétude ne coûte donc rien à l'`Adapter`.
Elle énumère les systèmes du travail dû **et** ceux dont une pièce est détenue : un `DeclaredSystem`
recensé **après** l'ouverture du `Case` n'a aucun `Step`, et sa pièce partirait pourtant dans
l'archive. La page annoncerait alors moins que ce que la personne reçoit — la façon la plus sûre de
lui faire croire qu'elle a tout.
⚠️ Le service n'y écrit **pas un chiffre**, et le nom du droit s'y lit en toutes lettres — « au titre
du droit d'accès », jamais « article 15 ». Les mots d'un `DeclaredSystem` sont ceux du client : s'il
a nommé son système avec un nombre, la page le recopie sans le réécrire.
_Avoid_ : Summary, Report, Notice, Manifest (le mot est pris), note (pris par la prose)

### Ce qui meurt à la clôture, et ce qui reste

**La clôture détruit le nominatif à l'instant même.** Les `Designations`, le détail de
l'`IdentityDeclaration`, le texte d'origine et tout le texte qui meurt disparaissent quand
l'`Operator` clôt le `Case` — sans fenêtre de conservation, parce qu'aucun risque juridique ne
demande le nominatif : la preuve d'une procédure est anonyme, et le service ne prouve jamais qu'un
droit a été honoré. Un délai « au cas où » n'aurait entreposé que le sac de désignations de gens
ayant demandé à disparaître.
⚠️ **Les `RetrievedData` tombent avec le reste**, bien qu'elles vivent hors de l'agrégat : ce sont
les données de la personne telles que les systèmes du client les ont rendues — les plus concentrées
du dispositif —, et les laisser survivre aurait vidé le geste de son sens à l'endroit où il compte
le plus. Elles sont détruites **en dernier**, quand la preuve est déjà écrite : une panne entre les
deux laisse des pièces détenues un moment de trop, visible et réparable, plutôt qu'un dossier clos
dont rien ne dirait qu'il l'a été.
⚠️ **La méthode de l'`IdentityDeclaration` survit**, et elle seule : elle se compte, son lecteur est
le contrôle, et la faire disparaître ferait perdre sous quel régime le dossier a été instruit au
moment même où l'on veut pouvoir en juger la pratique.

**La clôture réclame, et ne bloque jamais.** À l'instant de clore, l'écran compte les `Step` dont
personne n'a dit où ils en étaient — ni `Done`, ni `OutOfReach`, ni même l'aveu `Untreated` — et les
`Claim` restés sans issue. Il les compte plutôt qu'il ne les renomme : la liste des travaux dus est
déjà dépliée juste au-dessus, système par système, et la redire sous le bouton n'aurait fait que
répéter le même écran deux fois. Puis il laisse signer. **La clôture ne propage rien et ne gèle rien** :
aucun `Claim` ne passe `Answered`, aucun `Step` ne change d'état, et un `Step` laissé `ToDo` dans un
`Case` clos **reste** `ToDo`, où il se lit comme l'oubli qu'il est. Le recouvrir d'une cause de
clôture rassurante aurait perdu la seule trace que l'`Omission silencieuse` laisse jamais.
⚠️ **Répondre est donc un geste à part, au grain du droit.** Clore un dossier ne peut pas valoir
réponse sur six droits d'un seul clic : chaque `Claim` porte une réponse due à la personne, et
`Answered` s'y déclare un droit à la fois, sous un nom et une date. `Refused`, lui, a sa charge
probatoire propre — les mentions de l'art. 12.4 sont dues à la personne — et attend son propre geste.
⚠️ Le geste est donc **irréversible**, seul du dispositif à l'être. Sa parade est un geste délibéré
dans la surface de l'`Operator`, jamais de la donnée gardée en réserve.
⚠️ Une personne qui demande l'effacement de son `Case` **encore ouvert** est servie par la clôture
elle-même — `Abandoned`, et tout le nominatif tombe à l'instant. Il n'existe aucune fonction
d'effacement de `Case` distincte. Le seul arbitrage réel se pose franchement à la personne :
poursuivre l'instruction exige ses `Designations`, donc **poursuivre ou effacer, jamais les deux**.

**Le `EvidenceLog` vit cinq ans à compter de la clôture**, non configurable — la prescription civile de
droit commun, seul horizon qui soit un vrai nombre du droit plutôt qu'une intuition : la preuve vit
aussi longtemps que l'action qu'elle sert à défendre. Une durée réglable par client serait une case
qui pourrit en silence, et de la donnée gardée trop longtemps ne fait aucun bruit. À échéance le
`EvidenceLog` est détruit **en entier** — pas de second étage d'anonymisation, qui rouvrirait
l'expurgation que sa définition ferme.
⚠️ Cette destruction n'est **jamais automatique**. Un `EvidenceLog` échu apparaît dans une **section
propre** de l'écran de la file — sa ligne n'a ni personne, ni droit, ni délai, et son bouton ne doit
jamais voisiner ceux des `Case` — où l'`Operator` le détruit d'un geste délibéré, confirmé case
cochée comme l'est la clôture : un `EvidenceLog` expiré est ainsi une **ligne présente**, vue tous les
jours, jamais une ligne manquante que nulle relecture ne lèverait. La section reste affichée, et
vide, les années où rien n'est échu.
⚠️ **C'est le seul geste du dispositif qui ne porte pas de signature**, et c'est une conséquence de
ce qui suit, non un oubli : le seul endroit où ce nom aurait pu s'écrire est le `EvidenceLog` qui
disparaît. L'écrire ailleurs — seconde table, journal — aurait rouvert l'expurgation que la
définition du `EvidenceLog` ferme ; le réclamer pour ne l'écrire nulle part aurait été la façade d'une
preuve. La parade au geste irréversible reste donc entière : elle est **dans l'écran**.
⚠️ Deux coûts assumés : un `Operator` inactif garde au-delà de cinq ans — visible, jamais barré — et
la destruction ne laisse **aucune trace**, un `EvidenceLog` détruit ne pouvant consigner sa propre
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

**Enregistré, jamais vérifié** :
Le service enregistre des **déclarations** horodatées et signées, jamais des faits vérifiés. Un
`Step` `Done` prouve qu'on a déclaré l'avoir fait ; une `IdentityDeclaration` prouve ce que le canal
a affirmé. Le service n'a jamais le droit de bloquer une clôture : un `Case` peut se clore en
constatant qu'un système n'a pas été traité, et cette trace-là est la plus précieuse de toutes.
_Avoid_ : vérification, contrôle, garantie, attestation de conformité
