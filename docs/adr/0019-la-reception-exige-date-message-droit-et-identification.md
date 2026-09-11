# ADR-0019 — La réception exige date, message, droit et identification

- **Statut** : accepté
- **Date** : 2026-09-11
- **Décidé par** : le PRD [#344](https://github.com/AmauryTISSOT/microservice_rgpd/issues/344),
  livré par [#353](https://github.com/AmauryTISSOT/microservice_rgpd/issues/353) (le domaine),
  [#355](https://github.com/AmauryTISSOT/microservice_rgpd/issues/355) (le serveur) et
  [#358](https://github.com/AmauryTISSOT/microservice_rgpd/issues/358) (le navigateur), et tracé par
  [#362](https://github.com/AmauryTISSOT/microservice_rgpd/issues/362)
- **Glossaires** : [Requests](../contexts/requests/CONTEXT.md)
- **Renverse deux clauses de glossaire**, pas un ADR — comme l'ADR-0012 : la doctrine « rien n'est
  obligatoire au dépôt », que le glossaire de `Casework` portait sous l'entrée `IdentityMotivation`
  et que le formulaire de dépôt écrivait en toutes lettres ; et le défaut `J+9`, sous l'entrée
  `ReceptionDate` du même glossaire. Ce glossaire a été supprimé par
  l'[ADR-0017](./0017-casework-est-retire-requests-le-remplace.md) ; les deux clauses ne sont pas
  tombées avec lui, elles sont renversées ici, et le glossaire de `Requests` écrit déjà l'inverse.
- **Ne supplante aucun ADR.** L'[ADR-0014](./0014-le-screening-n-enregistre-pas-qui-a-arbitre.md)
  rappelle que le dépôt de `Casework` exigeait un nom de signataire ; le décrire n'était pas le
  décider, et l'ADR-0017 a déjà éteint l'asymétrie qu'il en tirait.

## Contexte

Le dépôt d'un `Case` n'exigeait presque rien, et c'était une doctrine. Le formulaire de dépôt
l'écrivait en toutes lettres : « Rien n'est obligatoire ici sauf le nom du signataire. Ni
désignation, ni droit, ni date, ni motivation : le service ne barre jamais la route, et un dossier
faible doit rester enregistrable et **visible comme tel**. » Le glossaire de `Casework` en donnait
le motif à propos de l'`IdentityMotivation` — la justification réclamée avant d'ouvrir un accès sous
une identité non vérifiée —, et le motif valait pour tout ce que le dépôt n'exigeait pas :

> **Elle ne barre jamais la route.** Un dossier ouvert sans elle est un dossier **faible et visible
> comme tel** : la réclamation reste affichée tant qu'elle n'est pas satisfaite, pendant que le
> délai court.

Et, sous `ReceptionDate`, il comblait la date qu'on n'avait pas déclarée :

> À défaut de déclaration, le service tient **neuf jours pour déjà courus** — `J+9 (défaut)` — et
> l'affiche ainsi : il nomme la **règle appliquée**, jamais la date qu'elle a produite.

Les deux clauses se tenaient par ce qui venait après le dépôt. Un `Case` s'instruisait : sa
faiblesse restait affichée sur l'écran du dossier, une réclamation ouverte jusqu'à ce qu'on la
satisfasse ; son échéance de l'art. 12.3 se calculait, et une date manquante devait donc être
comblée par une règle, nommée comme telle, pour que le délai coure quand même.

`Requests` n'a rien de cela. Une demande s'enregistre, et c'est tout : pas d'instruction, pas
d'échéance, pas de réclamation, pas même encore de liste. Une demande enregistrée sans date, sans
droit ou sans personne à retrouver ne serait visible comme faible nulle part. Et un défaut de date
n'aurait plus aucun délai à faire courir : il ne produirait qu'une date que personne n'a déclarée.

## Décision

**Enregistrer une demande exige quatre choses**, et le service refuse la saisie à laquelle l'une
d'elles manque :

1. **Une date de réception**, au format ISO sur le fil, et jamais postérieure à aujourd'hui à
   Paris. Pas de borne basse : un courrier peut être resté longtemps en attente.
2. **Un message** — le contenu de la demande tel qu'il a été reçu —, de 10 000 caractères au plus.
3. **Un droit invoqué**, l'un des six `DataSubjectRight` qu'on exerce. `OutOfScope` est refusé,
   avec le même message que l'absence de droit : c'est un verdict de `Qualification`, pas un droit
   qu'une personne invoque.
4. **Une identification** : un email, **ou** un nom et un prénom ensemble. Un nom seul ou un prénom
   seul, sans email, ne suffit pas ; un email suffit, et un nom ou un prénom partiel ne bloque plus.

Le reste est facultatif. L'origine a toujours une valeur — `Email` par défaut, ou `Letter`.
« Identité vérifiée » est une case à cocher, sans règle. Le nom, le prénom et l'email ne sont
contraints que dans leur longueur — 100, 100 et 254 caractères — et, pour l'email, dans sa forme.

**Le défaut `J+9` est abandonné, et aucun autre ne le remplace.** Le serveur ne comble jamais une
date absente : il la refuse. L'écran propose aujourd'hui à Paris comme valeur du champ, que
l'`Operator` voit et corrige ; c'est une proposition de l'écran, pas un défaut du service, et la date
qui arrive au serveur est toujours une date qu'un humain a laissée là.

**Le serveur fait foi.** Les règles sont écrites une fois, dans la fabrique
`DataSubjectRequest.Receive`, qu'appelle `RecordDataSubjectRequestHandler`. Elle ne s'arrête pas à
la première faute : elle rend **toutes** les raisons de refuser la saisie, chacune rattachée à un
champ. Le handler de la page, `POST /demandes?handler=Create`, répond 201, ou 400
`ValidationProblem` indexé par champ, et dans ce cas rien n'est enregistré. Un appel HTTP envoyé
directement au serveur, sans passer par l'écran, ne peut pas enregistrer une demande invalide.

**La validation du navigateur est un confort.** Elle applique les mêmes règles avant l'envoi, pour
que l'`Operator` lise ses erreurs sans attendre le serveur ; elle ne décide rien. Si le serveur refuse
une saisie que le navigateur avait acceptée, ses erreurs s'affichent sous les mêmes champs, de la
même manière. Les dix messages sont écrits une seule fois, côté serveur, dans
`DataSubjectRequestMessages`, et fournis au navigateur par la page (ADR-0018).

**Les erreurs sont placées dès le domaine.** L'erreur d'identification est rattachée à l'email **et**
à chacun des champs nom et prénom qui manquent, dans la fabrique elle-même, pour que le serveur et
l'écran la posent aux mêmes endroits.

**Ce qui est vide l'est vraiment.** Les valeurs sont enregistrées sans leurs espaces de début et de
fin, et un champ qui ne contient que des espaces est vide. L'email est conservé tel que saisi, sans
passage en minuscules : la trace dit ce que la personne a écrit.

**« Aujourd'hui » s'entend à Paris**, ni en UTC, ni au fuseau du poste. La conversion vit à un seul
endroit côté serveur, `ParisCalendar`, lu par `RecordDataSubjectRequestHandler` et par la page ; le
navigateur la refait à chaque ouverture de la modale, par `Intl.DateTimeFormat` et le fuseau
`Europe/Paris`.

## Ce que chaque renversement retire, et ce qu'il laisse

**« Rien n'est obligatoire au dépôt. »** Son motif était de ne jamais barrer la route : un refus à
l'entrée aurait renvoyé la personne à son silence, ou fait cocher n'importe quoi. Le premier risque
tenait à ce qu'un `Case` avait une suite où sa faiblesse restait lisible ; `Requests` n'en a pas. Le
second tient toujours, et c'est pourquoi les quatre exigences sont celles qu'une demande reçue porte
d'elle-même : sa date, son contenu, son auteur, et ce qu'elle réclame. Aucune n'est une case qu'on
remplirait pour passer : ni motivation, ni désignation, ni vérification d'identité — « Identité
vérifiée » reste facultative, et rien n'en dépend.

**`J+9 (défaut)`.** Son motif était le délai : sans date déclarée, l'échéance devait partir d'un
jour, et le service choisissait le plus défavorable en le nommant. Sans échéance, le motif est
éteint. Ce qui survit de la clause est la distinction qu'elle protégeait — une date déclarée par un
humain ne se confond jamais avec une date produite par une règle — et elle est tenue plus
strictement : il n'y a plus de date produite par une règle.

**Ce qui n'est pas renversé** : la date de réception ne se confond pas avec l'instant de
l'enregistrement. Le glossaire de `Casework` l'exigeait pour le dépôt manuel ; celui de `Requests` le
reprend. La demande porte les deux, `received_on` déclaré et `created_at` lu sur l'horloge.

## Les options écartées

- **Garder « rien n'est obligatoire », en rendant la faiblesse visible.** Il n'existe aucun écran où
  elle le serait : `Requests` n'a ni dossier, ni réclamation, ni liste.
- **Garder `J+9`, ou tout autre défaut de date.** Il n'y a pas de délai à faire courir, et une date
  posée par une règle se lirait comme une date déclarée dès que l'écran qui la nommait « défaut »
  n'existerait plus.
- **Exiger l'email.** Un courrier n'en porte souvent pas. Le nom et le prénom ensemble en tiennent
  lieu.
- **Accepter « hors périmètre », ou un « droit inconnu ».** Le droit invoqué est ce que la demande
  réclame ; une demande qui n'en invoque aucun n'est pas une demande d'exercice de droits. Aider
  l'`Operator` à le reconnaître est le rôle de la qualification par IA, présente dans la modale
  mais désactivée.
- **Valider dans le navigateur seulement.** Un appel HTTP envoyé directement au serveur aurait
  enregistré n'importe quoi.
- **Valider au serveur seulement.** Tenable, puisque le serveur fait foi ; mais chaque erreur aurait
  demandé un envoi, et le PRD veut que l'`Operator` voie ses erreurs à la saisie.
- **La validation native du navigateur** — `required`, `maxlength`, `type="email"`. `maxlength`
  tronque en silence un texte collé trop long, là où l'`Operator` doit lire pourquoi il est refusé ;
  les bulles natives ne portent pas les messages du service et ne se placent pas sous les champs.
  Le formulaire porte donc `novalidate`, et l'email est un champ texte.
- **Arrêter la fabrique à la première erreur.** L'`Operator` corrigerait une faute, renverrait, et en
  découvrirait une autre.

## Conséquences, y compris celles qui coûtent

⚠️ **Une demande à laquelle manque l'une des quatre choses ne peut pas être enregistrée.** Un
courrier sans aucune identification, une demande dont l'`Operator` ne sait pas dire quel droit elle
invoque, restent hors du service. C'est le prix de la décision, et il est assumé : la trace d'une
telle demande vit ailleurs, là où l'`Operator` l'a reçue.

⚠️ **Le droit doit être choisi, même dans le doute.** La liste s'ouvre sur « Sélectionner un droit »,
qui oblige à un choix conscient ; elle ne peut pas empêcher un choix au hasard. Rien dans le service
ne le signale, et rien ne le corrigera tant que la qualification par IA reste désactivée.

**Les règles sont écrites deux fois, les messages une seule.** La logique vit en C# et en
JavaScript (ADR-0018) ; la divergence est tenue par les mêmes mesures des deux côtés — longueurs en
unités UTF-16 (`.Length` et `.length`), même expression WHATWG pour l'email —, par le serveur qui
fait foi, et par les tests aux trois coutures — domaine, HTTP, navigateur (ADR-0020). Un plafond
qui change se change dans l'objet valeur, dans le message qui l'écrit en toutes lettres, et dans le
module.

**Le fuseau `Europe/Paris` doit être résolu partout où le service tourne.** C'est le premier emploi
d'un fuseau nommé dans le dépôt ; une image sans données de fuseau ferait échouer la conversion.

**L'`Operator` ne signe pas la saisie.** Là où le dépôt de `Casework` exigeait un nom de signataire,
`Requests` enregistre la constante `operator` : ce n'est pas une donnée qu'on lui demande, et la
signature d'un humain nommé attend l'authentification.

## Ce que cet ADR n'ouvre pas

- **Les statuts, l'échéance de l'art. 12.3, la prolongation.** Si l'instruction revient, un délai
  reviendra avec elle ; il partira de la date déclarée, et aucun défaut ne sera rétabli sans ADR.
- **La vérification de l'identité**, qui reste une déclaration de l'`Operator`, sans règle.
- **La détection de doublons.**
- **La qualification du droit par IA**, et toute relation entre `Requests` et `Qualification`.
- **Le filtrage des droits proposés selon leur configuration dans le Paramétrage.** Les six sont
  toujours proposés, qu'ils aient une adresse ou non.
