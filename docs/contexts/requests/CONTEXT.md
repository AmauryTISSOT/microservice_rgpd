# Requests

Ce contexte enregistre une **demande RGPD dès sa réception** : ce qui est arrivé, par quel canal,
quand, de qui, et quel droit la personne invoque. Il ne connaît à ce jour que ce `Gesture`-là ;
l'instruction, les délais et les statuts n'y existent pas encore.

Les identifiants du code sont en anglais (`DataSubjectRequest`, `Origin`) ; les textes destinés à
l'humain sont en français (« demande », « Courrier »).

## Language

### La demande

**DataSubjectRequest** :
Une demande d'exercice d'un droit RGPD adressée par une personne, telle que l'`Operator` l'enregistre
à sa réception. À l'écran : une **demande**.
_Avoid_ : Case, dossier, ticket, requête, Request (seul)

**Origin** :
Le canal par lequel la demande est arrivée : `Email` ou `Letter` — à l'écran « Email » ou
« Courrier ». Il ne change aucune autre règle.
_Avoid_ : source, provenance, Channel

**Date de réception** :
Le jour où la demande est arrivée chez le responsable, déclaré par l'`Operator`. Jamais postérieure
au jour même, jour qui s'entend à l'heure de Paris. Elle ne se confond pas avec l'instant de
l'enregistrement : une demande transcrite d'un courrier a été reçue avant d'entrer dans le service.
_Avoid_ : date d'ouverture, date de dépôt, date de création

**Message** :
Le contenu de la demande tel qu'il a été reçu — le corps de l'email, la transcription du courrier —
recopié par l'`Operator`.
_Avoid_ : messages, commentaire, note, texte libre

**Droit invoqué** :
Le seul `DataSubjectRight` que la demande exerce, choisi par l'`Operator` parmi les six droits.
`OutOfScope` n'en est pas un : c'est un verdict de `Qualification`, et une demande ne l'invoque
jamais.
_Avoid_ : type de demande, catégorie, qualification

### La personne

**Identification** :
Ce qui permet de retrouver la personne : un email, **ou** un nom et un prénom ensemble. L'un des
deux suffit ; un nom seul ou un prénom seul, sans email, ne suffit pas.
_Avoid_ : identité (réservé à `Identité vérifiée`), coordonnées, contact

**Identité vérifiée** :
L'attestation, par l'`Operator`, qu'il a vérifié l'identité de la personne. Déclarative et
facultative : rien n'en dépend.
_Avoid_ : authentifié, identifié, KYC

### Qui agit

**Operator** :
L'humain qui enregistre la demande. Tant que le service n'authentifie personne, il n'a pas de nom :
chaque demande est créée par `operator`.
_Avoid_ : utilisateur, agent, gestionnaire

**Enregistrer une demande** :
Le `Gesture` par lequel l'`Operator` fait entrer une demande dans le service : il est signé et daté
de l'instant où il est posé, distinct de la date de réception qu'il déclare. Voir la langue de
système dans [`CONTEXT-MAP.md`](../../../CONTEXT-MAP.md).
_Avoid_ : ouvrir, déposer, créer un dossier
