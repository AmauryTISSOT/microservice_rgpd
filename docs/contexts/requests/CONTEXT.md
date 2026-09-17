# Requests

Ce contexte enregistre une **demande RGPD dès sa réception** : ce qui est arrivé, par quel canal,
quand, de qui, et quel droit la personne invoque. L'`Operator` peut ensuite **modifier une demande**
pour corriger une erreur de saisie, puis **exécuter la demande** : le droit invoqué est remis au
système hôte — appelé à son adresse, ou publié sur son routage —, et la demande passe à Terminée
quand le destinataire, ou le broker pour lui, en accuse réception. Une demande porte une date limite
de réponse et un statut ;
l'exécution est le seul `Gesture` qui fait changer le statut.

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
Une `Qualification` peut le **proposer** ; seul l'enregistrement par l'`Operator` le **choisit**,
et la demande ne référence aucune qualification. `OutOfScope` n'en est pas un : c'est un verdict
de `Qualification`, et une demande ne l'invoque jamais.
_Avoid_ : type de droit, type de demande, catégorie

**Fiche** :
La lecture à l'écran d'une demande enregistrée, telle que le service la tient : une surface ancrée au
bord droit du tableau, en lecture seule. Elle n'instruit rien et ne modifie rien — les gestes restent
sur la ligne. Dans le code : `sheet`.
_Avoid_ : détail, panneau (pris par la navigation, ADR-0009), popup, aperçu

### Le délai

**Date limite de réponse** :
Le jour avant lequel le responsable doit répondre : la date de réception plus un mois, ramenée au
dernier jour du mois suivant quand ce jour n'y existe pas (31 janvier → 28 ou 29 février). Fixée
quand la demande est enregistrée, et tenue par la demande : elle ne se recalcule pas d'elle-même.
Une modification la refait par la même règle, et elle ne change donc que si la date de réception a
changé. Elle part de la
date de réception, jamais de l'instant d'enregistrement.
_Avoid_ : échéance légale, délai, date d'échéance, StatutoryDeadline

**Échéance proche** :
Le signalement d'une demande **En cours** dont la date limite de réponse tombe entre aujourd'hui et
aujourd'hui plus sept jours, bornes comprises. Le jour même de la date limite, la demande est en
échéance proche, pas en retard.
_Avoid_ : urgent, bientôt en retard

**En retard** :
Le signalement d'une demande **En cours** dont la date limite de réponse est antérieure à
aujourd'hui.
_Avoid_ : échue, dépassée, hors délai

⚠️ **Un signalement n'est pas un statut.** Il se lit à l'instant où l'on regarde, à partir de la
date limite et d'« aujourd'hui » à l'heure de Paris ; rien ne l'enregistre. Une demande Terminée ou
Annulée n'est jamais signalée.

### Le statut

**Statut** :
Où en est la demande : **En cours**, **Terminée** ou **Annulée**. Une demande naît En cours. C'est
un état tenu par la demande, et non la trace d'un `Gesture`.
_Avoid_ : état, étape, phase, avancement

**Terminée** :
Le statut d'une demande dont le droit invoqué a été **remis** au système hôte, et dont le
destinataire — ou le broker pour lui — a accusé réception. Une demande Terminée est close : elle ne
se modifie plus et ne s'exécute plus.
⚠️ **Terminée ne dit pas que le droit a été appliqué.** Sur une adresse HTTP, un 2xx est ce que le
destinataire répond quand il a appliqué le droit ; sur un routage, le service sait seulement que le
broker a accepté le message, jamais qu'un consommateur l'a traité. Voir `Publication confirmée` et
[ADR-0028](../../adr/0028-l-aboutissement-d-une-execution-cesse-d-etre-un-2xx-le-broker-accuse-reception.md).
_Avoid_ : exécutée, traitée, répondue, fermée, appliquée

**Demande close** :
Une demande Terminée ou Annulée.
_Avoid_ : archivée, clôturée

**Annulée** :
Le statut d'une demande que le responsable n'instruira pas, et qu'il conserve. Ne se confond pas
avec la suppression : une demande annulée reste dans le service.
_Avoid_ : supprimée, abandonnée, rejetée

### La personne

**Identification** :
Ce qui permet de retrouver la personne : un email, **ou** un nom et un prénom ensemble. L'un des
deux suffit ; un nom seul ou un prénom seul, sans email, ne suffit pas.
_Avoid_ : identité (réservé à `Identité vérifiée`), coordonnées, contact

**Identité vérifiée** :
L'attestation, par l'`Operator`, qu'il a vérifié l'identité de la personne. Déclarative, facultative
à la réception, et exigée pour exécuter la demande.
_Avoid_ : authentifié, identifié, KYC

### Qui agit

**Operator** :
L'humain qui enregistre la demande, qui la corrige et qui l'exécute. Tant que le service
n'authentifie personne, il n'a pas de nom : chaque demande est créée, modifiée et exécutée par
`operator`.
_Avoid_ : utilisateur, agent, gestionnaire

**Enregistrer une demande** :
Le `Gesture` par lequel l'`Operator` fait entrer une demande dans le service : il est signé et daté
de l'instant où il est posé, distinct de la date de réception qu'il déclare. Voir la langue de
système dans [`CONTEXT-MAP.md`](../../../CONTEXT-MAP.md).
_Avoid_ : ouvrir, déposer, créer un dossier

**Modifier une demande** :
Le `Gesture` par lequel un `Operator` corrige une erreur de *saisie* dans les données de la demande
telle qu'elle a été enregistrée. Geste interne, sans effet juridique. Il laisse une empreinte
— `ModifiedAt`, `ModifiedBy` — qui n'est affichée nulle part et s'écrase à chaque correction. Une
modification qui ne change aucune valeur n'a pas eu lieu : elle ne laisse rien. Une demande close,
Terminée ou Annulée, n'est plus modifiable. Corriger la date de réception recalcule la date limite
de réponse. Voir [ADR-0023](../../adr/0023-modifier-une-demande-est-un-geste.md).
_Avoid_ : rectification — le **droit de rectification** (art. 16) est le droit invoqué par la
personne concernée pour faire corriger *ses* données chez le responsable de traitement. Il porte sur
les données du sujet, jamais sur le dossier qui l'enregistre. Aussi : éditer, mettre à jour, amender

**Supprimer une demande** :
Le retrait définitif d'une demande par l'`Operator`, quel que soit son statut : elle disparaît du
service sans laisser de trace, pas même celle de son enregistrement. Ce n'est **pas** un `Gesture` :
un `Gesture` laisse une trace datée, la suppression efface celle qui existait. « Sans trace »
s'entend dans `Requests` : un Message qualifié depuis la modale reste dans la trace d'audit de
`Qualification`, sans lien vers la demande (ADR-0024).
_Avoid_ : annuler, effacer, archiver, clôturer

⚠️ **« Effacer » est pris par le droit à l'effacement** (art. 17), que la demande peut invoquer.
Supprimer une demande qui invoque l'effacement n'exerce pas ce droit : cela retire la demande
elle-même.

⚠️ **« Sans trace » s'entend des données de la demande.** Le journal d'exécution survit à la
suppression : ses lignes gardent l'identifiant de la demande, qui ne mène alors plus à personne.

### L'exécution

**Exécuter une demande** :
Le `Gesture` par lequel l'`Operator` fait **remettre** le droit invoqué au système hôte, par le
**canal d'exercice** — terme du glossaire de `Configuration`, sans rapport avec le canal d'arrivée de
l'**origine** — que le Paramétrage associe à ce droit. **Même geste, même bouton, même modale, même
journal, quel que soit le canal** : un appel HTTP à l'adresse déclarée, ou une publication sur le
routage déclaré. Il n'est offert que sur une demande En cours, dont l'identité est vérifiée, qui
porte un email, dont le droit porte un canal, et — sur un droit routé — dont le déploiement déclare
une connexion au broker. Quand le destinataire, ou le broker pour lui, accuse réception, la demande
passe à Terminée ; sinon son statut ne change pas, et l'`Operator` peut recommencer. Ses traces sont
les tentatives du journal d'exécution, pas une empreinte sur la demande.
_Avoid_ : exercer (c'est la personne concernée qui exerce son droit), traiter (réservé au sens du
RGPD), transmettre, envoyer, clôturer

**Motif de blocage** :
La raison pour laquelle une demande ne peut pas être exécutée, la première dans cet ordre : demande
close, identité non vérifiée, email manquant, droit non configuré, connexion au broker absente sur un
droit routé. Les deux derniers nomment le droit. Ce qui se corrige sur la demande passe avant ce qui
se règle dans le Paramétrage, et ce qui se règle dans le Paramétrage avant ce qui se règle dans le
déploiement.
⚠️ **« Connexion absente » parle du déploiement, jamais du Paramétrage** : le routage est bon, et
c'est le service qui n'a nulle part où publier. Ce motif et le bandeau de la page « Configuration
RabbitMQ » se décident sur la **même** règle, pour que l'`Operator` et l'intégrateur lisent la même
vérité. Il ne promet pas non plus un broker joignable : un déploiement qui déclare une connexion
devant un broker éteint n'est pas bloqué, et c'est l'exécution qui échouera.
_Avoid_ : erreur, refus, prérequis

**Système hôte** :
Ce qui, chez le responsable, applique réellement les droits sur les données de la personne. Le
service lui **remet** un droit ; il ne voit jamais ces données lui-même.
⚠️ **Ce n'est pas nécessairement une application joignable.** Sur une adresse HTTP, c'est une
application qui répond ; sur un routage, c'est un **consommateur que le service ne voit jamais** —
qui peut être arrêté, en retard, ou en train d'échouer, sans que le service en sache rien.
_Avoid_ : backend, application cliente, SI, système tiers, destinataire joignable

**Publication confirmée** :
L'accusé de réception que le broker rend au service pour un message publié. Il prouve que le broker
a **accepté** le message et l'a routé vers au moins une file. Il ne prouve **pas** qu'un consommateur
l'a lu, ni qu'il l'a traité, ni que le droit a été appliqué. Une publication qu'aucune file ne reçoit
n'est pas confirmée : le broker rend le message, et la demande reste En cours.
_Avoid_ : accusé de traitement, acquittement du système hôte, livraison

**Tentative d'exécution** :
Une **remise** du droit au système hôte pour une demande — un appel HTTP **ou** une publication —, et
son résultat : **Succès**, **Réponse non 2xx**, **Message non routable**, **Publication refusée par
le broker**, **Délai dépassé**, **Erreur réseau**, ou **Succès non enregistré** quand la remise a
abouti mais que la demande n'a pas pu passer à Terminée. Les sept valeurs forment **un seul
vocabulaire fermé**, partagé par les deux canaux : « Réponse non 2xx » n'a de sens que sur HTTP, et
les deux cas du bus n'en ont que sur un routage. Elle dit **par où** la remise est partie — l'adresse
appelée, ou l'exchange et la routing key en toutes lettres —, combien de temps elle a duré, jusqu'à
la réponse ou à l'accusé. Datée, signée `operator`, elle ne porte aucune donnée personnelle, et son
statut HTTP est nul sur toute publication. Une exécution refusée pour un motif de blocage ne remet
rien et n'est pas une tentative.
_Avoid_ : essai, appel (seul), log

**Journal d'exécution** :
L'ensemble des tentatives d'exécution, une ligne par tentative, qui s'empilent sans jamais
s'écraser. Il prouve qu'un droit a été demandé au système hôte, et survit à la suppression de la
demande.
_Avoid_ : historique, audit, trace d'audit (prise par `Qualification`), logs
