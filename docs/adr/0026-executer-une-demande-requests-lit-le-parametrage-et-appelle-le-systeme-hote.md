# ADR-0026 — Exécuter une demande : Requests lit le Paramétrage et appelle le système hôte

- **Statut** : accepté
- **Date** : 2026-09-14
- **Décidé par** : l'US « Requests : l'Operator exécute une demande auprès du système hôte »
- **Glossaires** : [`CONTEXT-MAP.md`](../../CONTEXT-MAP.md), [Requests](../contexts/requests/CONTEXT.md),
  [Configuration](../contexts/configuration/CONTEXT.md)
- **Supplante, sur des points nommés** :
  - [ADR-0016](./0016-le-manifest-cede-la-place-au-parametrage-un-droit-une-adresse.md) — « **Le
    déclenchement de l'appel** vers un `EndpointUrl` », parmi ce qu'il n'ouvrait pas. L'appel est
    ouvert ici ; son authentification et son secret restent fermés.
  - [ADR-0017](./0017-casework-est-retire-requests-le-remplace.md) et
    [ADR-0003](./0003-troisieme-contexte-sans-intersection-et-garde-des-traversees.md) — la liste
    blanche des traversées, qui gagne une quatrième ligne : `Requests → Configuration`.
  - [ADR-0021](./0021-la-demande-tient-un-statut-et-une-date-limite-de-reponse.md) — « terminer »
    parmi les actes qui font changer le statut qu'il n'ouvrait pas. Annuler reste fermé.
- **Réserve sur l'[ADR-0022](./0022-supprimer-une-demande-ne-laisse-aucune-trace.md)**, sans le
  supplanter : le journal d'exécution survit à la suppression d'une demande.
- **Renverse une clause de glossaire** : `Identité vérifiée`, « Déclarative et facultative : rien
  n'en dépend ».

## Contexte

Le Paramétrage associe à chacun des six droits l'adresse à laquelle le service le fera appliquer,
mais rien ne lisait cette adresse (ADR-0016). Une demande naissait En cours et y restait : aucun
`Gesture` ne la faisait changer de statut (ADR-0021). Le service enregistrait donc des demandes sans
jamais les faire aboutir.

Faire appliquer un droit par le système hôte pose cinq questions que les ADR précédents laissaient
ouvertes : par quel chemin `Requests` apprend l'adresse d'un droit, sous quelles conditions l'appel
part, ce que l'appel envoie et ce qui vaut réussite, ce qu'il laisse derrière lui, et ce qu'il
advient de cette trace quand la demande est supprimée.

## Décision

**Exécuter une demande est un `Gesture`.** L'`Operator` fait appliquer le droit invoqué par le
système hôte, à l'adresse que le Paramétrage associe à ce droit. Ses traces sont les tentatives du
**journal d'exécution**, une ligne par tentative ; il ne pose aucune empreinte sur la demande. Le
passage à Terminée est un état, pas une trace.

### La relation `Requests → Configuration`

**`Configuration` est fournisseur amont, `Requests` conformiste en aval.** Le handler d'exécution,
dans `UseCases/Requests`, lit le `Settings` et l'`EndpointUrl` du droit tels que `Configuration` les
publie, sans les traduire. La relation est écrite en toutes lettres dans la liste blanche de
`ContextIsolationTests` : `(Requests, Configuration)`. Le sens inverse reste interdit :
`Configuration` ignore qu'on le lit.

### Les conditions d'exécution

Une demande s'exécute si elle est **En cours**, si son **identité est vérifiée**, si elle porte un
**email**, et si son droit a une **adresse configurée**. Quand l'une manque, le **motif de blocage**
est la première dans cet ordre : « Demande close », « Identité non vérifiée », « Email manquant »,
« Aucune adresse configurée pour le {droit} ». L'ordre met d'abord ce qui se corrige sur la demande,
puis ce qui se règle dans le Paramétrage. La condition « droit renseigné » n'existe pas : l'invariant
de l'ADR-0019 la garantit.

**Le serveur fait foi.** Les conditions sont calculées au rendu de la ligne, recalculées à
l'ouverture de la modale, qui charge son récapitulatif par un handler (`GET ?handler=Execution`),
puis recalculées une dernière fois juste avant l'appel. Un refus à ce stade n'appelle pas l'hôte et
n'écrit aucune tentative : **409** pour une demande close, par cohérence avec la modification,
**422** pour les autres motifs, **404** pour une demande disparue. Le `ProblemDetails` porte le motif
en français et la ligne à jour, rendue par `_RequestRow`.

L'exécution est un handler de page (`POST /demandes?handler=Execute`), avec le jeton anti-rejeu,
comme la création, la modification et la suppression. Aucune route publique, rien dans Swagger.

### Le contrat de l'appel

- **`POST`** sur l'`EndpointUrl` du droit, `Content-Type: application/json`, sans en-tête
  d'authentification.
- **Corps**, exactement ces cinq clés : `requestId` (GUID en texte), `right` (nom canonique de
  `data-subject-rights.wire.json`), `email`, `firstName`, `lastName`. Un prénom ou un nom absent
  part à `null` : les cinq clés sont toujours présentes.
- **Tout 2xx vaut « le droit a été appliqué »**, `202 Accepted` compris. L'hôte ne répond 2xx qu'une
  fois le droit appliqué ; l'exécution est synchrone.
- **Les redirections ne sont pas suivies** : un 3xx est une réponse non 2xx.
- **Délai** : `HostSystem:TimeoutSeconds`, 30 par défaut, sur l'appel entier. Une valeur qui n'est
  pas un entier strictement positif arrête le démarrage.
- **Aucune nouvelle tentative automatique.** Le client HTTP retire les handlers de résilience que
  `ServiceDefaults` pose par défaut (`RemoveAllResilienceHandlers`), comme les moteurs de
  `Qualification` et de `Screening`.
- **`requestId` est une clé d'idempotence.** L'hôte doit traiter deux appels portant le même
  `requestId` comme un seul. C'est ce qui couvre la double exécution concurrente et la nouvelle
  tentative après un succès non enregistré.
- **L'appel va à son terme** même si le navigateur s'en va : `RequestAborted` n'est pas propagé.
  Ce que le garde de l'ADR-0015 interdit est ce qui part tout seul, pas ce qui survit à la requête
  qui l'a lancé.

L'appel passe par un port de `Core/Requests`, implémenté dans `Infrastructure`. **Les tests ne le
remplacent pas** : fonctionnels et navigateur appellent un système hôte factice qui écoute sur un
port réel, et dont l'adresse est posée dans le Paramétrage. C'est le vrai client HTTP qui est
éprouvé — méthode, corps, délai, redirections non suivies, erreur réseau.

### Le journal d'exécution

Une **tentative d'exécution** est un agrégat à part, `ExecutionAttempt`, table `execution_attempts`.
Elle référence la demande par son `DataSubjectRequestId`, **sans clé étrangère**. Elle porte : le
droit, l'URL appelée **sans query string ni fragment**, l'instant de début en UTC, la durée, le
résultat, le statut HTTP s'il y en a un, et l'auteur `operator`. Aucune donnée personnelle, aucun
corps de réponse.

Le résultat est une valeur fermée : **Succès**, **Réponse non 2xx**, **Délai dépassé**, **Erreur
réseau**, **Succès non enregistré**.

**La tentative s'écrit après l'appel.** Sur un 2xx, la tentative et le passage à Terminée partent
dans la même transaction. Si elle échoue, une seconde transaction écrit la tentative en **Succès non
enregistré**, l'échec est journalisé en erreur, et l'`Operator` lit que le système hôte a appliqué
le droit sans que la demande ait pu passer à Terminée. Sur un échec de l'appel, la tentative s'écrit
seule, et le statut ne change pas.

**Le journal survit à la suppression de la demande.** Supprimer une demande retire toutes ses
données (ADR-0022) mais laisse ses tentatives : elles gardent un identifiant qui ne mène plus à
personne, et prouvent qu'un droit a été demandé au système hôte. « Sans trace » s'entend des données
de la demande.

### `Identité vérifiée` devient une condition

Facultative à la réception, l'attestation est exigée pour exécuter. Faire appliquer un droit à la
mauvaise personne — lui transmettre des données, effacer les siennes — est le risque que la
vérification d'identité existe pour prévenir (art. 12.6).

## Les options écartées

- **Un port de lecture dans `Core/Requests`**, implémenté par un adaptateur hors contexte. La même
  dépendance, cachée derrière une exception de plus dans `ContextlessTypeTests` au lieu d'être lue
  dans la liste blanche.
- **Le page model qui lit l'URL et la passe à la commande.** Une isolation de façade : la
  revérification serveur ne relirait plus le Paramétrage.
- **Supprimer le journal avec la demande.** « Sans trace » serait tenu à la lettre, mais une demande
  d'effacement exécutée puis supprimée ne laisserait aucune preuve que l'effacement a été demandé.
- **Un verrou pendant l'appel** — une transaction ouverte jusqu'à 30 s, ou un statut « exécution en
  cours ». Le premier tient une connexion ; le second est un état que le dépôt refuse. L'idempotence
  de `requestId` couvre le même risque sans rien tenir.
- **Écrire une tentative « en cours » avant l'appel**, pour qu'un plantage laisse une trace. C'est
  un état.
- **Traiter 202 comme un échec**, puisqu'il annonce un traitement différé. Il aurait fallu
  l'exécution asynchrone pour lui donner un sens ; le contrat demande à l'hôte de ne répondre 2xx
  qu'après.
- **Renvoyer l'URL affichée et refuser si elle a changé.** Le récapitulatif chargé à l'ouverture
  réduit l'écart à quelques secondes, sans un second chemin de refus.

## Conséquences, y compris celles qui coûtent

⚠️ **Un plantage pendant l'appel ne laisse aucune trace.** Le système hôte a pu appliquer le droit ;
le journal n'en dit rien et la demande reste En cours. L'`Operator` réexécute, et l'idempotence de
`requestId` évite le double effet — si l'hôte la respecte.

⚠️ **Deux exécutions concurrentes appellent deux fois l'hôte.** Même motif, même garde.

⚠️ **Une demande identifiée par nom et prénom seuls ne s'exécute pas** tant qu'on ne lui a pas
ajouté un email.

⚠️ **Le journal grossit sans borne.** Aucune purge n'existe ; sa durée de conservation n'est pas
décidée.

⚠️ **L'écart entre le récapitulatif et l'appel n'est pas nul.** Une adresse changée dans le
Paramétrage entre l'ouverture de la modale et la confirmation est appelée sans que la modale l'ait
montrée.

⚠️ **Une query string n'est pas un endroit pour un secret.** Le journal l'écarte, mais elle part à
l'hôte et se lit dans la modale comme dans le Paramétrage.

## Ce que cet ADR n'ouvre pas

- **L'authentification et les secrets vers le système hôte** (ADR-0016).
- **L'exécution asynchrone**, où l'hôte accuse réception puis confirme plus tard.
- **Les nouvelles tentatives automatiques.**
- **Le choix de la méthode HTTP ou d'en-têtes** dans le Paramétrage.
- **L'écran de consultation du journal d'exécution**, et sa durée de conservation.
- **Annuler une demande**, l'autre acte qui fait changer le statut.
