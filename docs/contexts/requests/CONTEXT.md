# Requests

Ce contexte enregistre une **demande RGPD dès sa réception** : ce qui est arrivé, par quel canal,
quand, de qui, et quel droit la personne invoque. L'`Operator` peut ensuite **modifier une demande**
pour corriger une erreur de saisie, **prolonger la demande** de deux mois quand le règlement l'y
autorise, puis **exécuter la demande** : le système hôte applique le droit invoqué, et la demande
passe à Terminée. Une demande porte une date limite de réponse et un statut ; l'exécution est le
seul `Gesture` qui fait changer le statut.

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
Deux gestes seulement la déplacent : une modification la refait par la même règle, et elle ne change
donc que si la date de réception a changé ; une **prolongation** la reporte de deux mois. Sur une
demande prolongée, la même règle s'applique augmentée de ces deux mois. Elle part de la date de
réception, jamais de l'instant d'enregistrement. Dans le code : `ResponseDeadline`. Voir
[ADR-0029](../../adr/0029-prolonger-une-demande-est-un-geste-une-seule-fois-de-deux-mois.md).
_Avoid_ : échéance légale, délai, date d'échéance, StatutoryDeadline

⚠️ **C'est la date limite de réponse qui fait foi partout** — le tableau, les signalements,
l'échéance qui engage le responsable. Prolongée ou non, c'est toujours elle que l'on lit : aucune
autre date ne prend sa place.

**Prolongation** :
Les deux mois dont la date limite de réponse d'une demande est reportée au titre de l'article 12 §3,
portant le total autorisé à trois mois à compter de la réception. Une demande est prolongée, ou elle
ne l'est pas : il n'y en a **qu'une seule par demande**, jamais une seconde, et elle n'est possible
que **tant que la date limite de réponse n'est pas passée**. La durée n'est pas saisie : deux mois
est une constante du domaine, pas un champ. Rien ne défait une prolongation — **pas de retour
arrière** ; une modification de la demande la conserve, même quand les dates recalculées la placent
rétroactivement après une échéance dépassée. La fenêtre garde la porte d'entrée du geste, elle ne
s'applique pas à une prolongation déjà posée. Voir
[ADR-0029](../../adr/0029-prolonger-une-demande-est-un-geste-une-seule-fois-de-deux-mois.md).
_Avoid_ : prorogation, extension, report, délai supplémentaire, rallonge

**Date limite initiale** :
La date limite de réponse telle qu'elle valait **avant** la prolongation. Enregistrée et non
recalculée par soustraction : `AddMonths` n'est pas inversible (31 décembre + 2 mois = 28 février,
et 28 février − 2 mois = 28 décembre), et c'est elle qui fixe l'échéance de l'obligation d'informer
la personne concernée. Elle n'existe que sur une demande prolongée. Dans le code :
`InitialResponseDeadline`.
_Avoid_ : ancienne date limite, date limite d'origine, date limite avant prolongation,
OriginalDeadline, PreviousDeadline

⚠️ **La date limite initiale est une trace, pas une source.** Elle dit ce qui valait avant ; la date
qui fait foi partout ailleurs reste la date limite de réponse.

**Motif de prolongation** :
La raison pour laquelle la demande est prolongée, prise dans un **vocabulaire fermé à deux valeurs**
— la **complexité de la demande** et le **nombre de demandes** —, parce que l'article 12 §3 n'en
ouvre pas une troisième. Il n'y a pas de valeur « Autre ». Il ne se confond pas avec le **motif de
blocage**, que le serveur calcule pour dire pourquoi une demande ne s'exécute pas. Dans le code :
`ExtensionGround`.
_Avoid_ : ExtensionMotive, ExtensionReason, raison, cause, motif (seul)

**Justification de la prolongation** :
Le texte écrit par l'`Operator` pour dire le **fait concret** — quelle complexité, quel afflux — qui
justifie la prolongation. Obligatoire : le motif fermé prouve que l'`Operator` est resté dans le
cadre, la justification fournit ce qu'une autorité de contrôle demandera. C'est le premier texte
libre saisi par l'`Operator` dans ce service. Dans le code : `ExtensionJustification`.
_Avoid_ : ExtensionMotive, motivation, commentaire, explication, note

⚠️ **Le texte libre s'appelle « justification », jamais « motive ».** Un lecteur francophone lisant
`ExtensionMotive` y verrait « motif » — c'est-à-dire l'exact opposé : le motif est la valeur fermée,
la justification est le texte.

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
Le statut d'une demande dont le droit invoqué a été appliqué par le système hôte. Une demande
Terminée est close : elle ne se modifie plus et ne s'exécute plus.
_Avoid_ : exécutée, traitée, répondue, fermée

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
de réponse — et, sur une demande prolongée, les deux dates ensemble, sans défaire la prolongation.
Voir [ADR-0023](../../adr/0023-modifier-une-demande-est-un-geste.md) et
[ADR-0029](../../adr/0029-prolonger-une-demande-est-un-geste-une-seule-fois-de-deux-mois.md).
_Avoid_ : rectification — le **droit de rectification** (art. 16) est le droit invoqué par la
personne concernée pour faire corriger *ses* données chez le responsable de traitement. Il porte sur
les données du sujet, jamais sur le dossier qui l'enregistre. Aussi : éditer, mettre à jour, amender

**Prolonger une demande** :
Le `Gesture` par lequel l'`Operator` reporte de deux mois la date limite de réponse d'une demande,
au titre de l'article 12 §3. Il exige un **motif de prolongation** et une **justification de la
prolongation** ; il n'est offert que sur une demande En cours dont la date limite de réponse n'est
pas passée, et **une seule fois**. La durée ne se saisit pas. Il ne change pas le statut, et rien ne
le défait : modifier une demande prolongée recalcule la date limite initiale et la date limite de
réponse ensemble, depuis la date de réception corrigée, sans toucher au motif, à la justification,
ni à la date à laquelle la prolongation a été posée.
Le service n'enregistre pas qui a prolongé — aucun `Operator` n'est identifié — et n'informe pas la
personne concernée : l'`Operator` l'informe ailleurs. Voir
[ADR-0029](../../adr/0029-prolonger-une-demande-est-un-geste-une-seule-fois-de-deux-mois.md).
_Avoid_ : prorogation, extension, report, délai supplémentaire, rallonge

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
Le `Gesture` par lequel l'`Operator` fait appliquer le droit invoqué par le système hôte, par le
**canal d'exercice** — terme du glossaire de `Configuration`, sans rapport avec le canal d'arrivée de
l'**origine** — que le Paramétrage associe à ce droit. Il n'est offert que sur une demande En cours, dont
l'identité est vérifiée, qui porte un email, et dont le droit porte une adresse HTTP — un droit exercé
par RabbitMQ est bloqué tant que le service ne sait pas publier. Quand le système hôte confirme
avoir appliqué le droit, la demande passe à Terminée ; sinon son statut ne change pas, et
l'`Operator` peut recommencer. Ses traces sont les tentatives du journal d'exécution, pas une
empreinte sur la demande.
_Avoid_ : exercer (c'est la personne concernée qui exerce son droit), traiter (réservé au sens du
RGPD), transmettre, envoyer, clôturer

**Motif de blocage** :
La raison pour laquelle une demande ne peut pas être exécutée, la première dans cet ordre : demande
close, identité non vérifiée, email manquant, droit non configuré, exercice par RabbitMQ. Les deux
derniers nomment le droit ; le dernier est provisoire, et disparaîtra le jour où le service publiera
sur RabbitMQ.
_Avoid_ : erreur, refus, prérequis

**Système hôte** :
L'application du responsable qui applique réellement les droits sur les données de la personne. Le
service lui demande d'appliquer un droit ; il ne voit jamais ces données lui-même.
_Avoid_ : backend, application cliente, SI, système tiers

**Tentative d'exécution** :
Un appel au système hôte pour une demande, et son résultat : **Succès**, **Réponse non 2xx**,
**Délai dépassé**, **Erreur réseau**, ou **Succès non enregistré** quand le système hôte a appliqué
le droit mais que la demande n'a pas pu passer à Terminée. Datée, signée `operator`, elle ne porte
aucune donnée personnelle. Une exécution refusée pour un motif de blocage n'appelle rien et n'est pas
une tentative.
_Avoid_ : essai, appel (seul), log

**Journal d'exécution** :
L'ensemble des tentatives d'exécution, une ligne par tentative, qui s'empilent sans jamais
s'écraser. Il prouve qu'un droit a été demandé au système hôte, et survit à la suppression de la
demande.
_Avoid_ : historique, audit, trace d'audit (prise par `Qualification`), logs
