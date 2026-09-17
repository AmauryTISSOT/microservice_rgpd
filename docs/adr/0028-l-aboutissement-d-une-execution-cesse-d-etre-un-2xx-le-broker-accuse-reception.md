# ADR-0028 — L'aboutissement d'une exécution cesse d'être un 2xx : le destinataire, ou le broker pour lui, accuse réception

- **Statut** : accepté
- **Date** : 2026-09-17
- **Décidé par** : l'US [#506](https://github.com/AmauryTISSOT/microservice_rgpd/issues/506)
  « Requests : exécuter une demande publie sur RabbitMQ, et le service sait enfin exercer un droit
  routé », ouverte par [#507](https://github.com/AmauryTISSOT/microservice_rgpd/issues/507)
- **Glossaires** : [`CONTEXT-MAP.md`](../../CONTEXT-MAP.md),
  [Requests](../contexts/requests/CONTEXT.md), [Configuration](../contexts/configuration/CONTEXT.md)
- **Amende deux glossaires** : dans `Requests`, `Terminée`, `Système hôte`, `Tentative d'exécution`
  et `Motif de blocage` changent d'énoncé, et une entrée dit ce qu'un accusé du broker prouve et ne
  prouve pas ; dans `Configuration`, la **connexion du déploiement** entre comme chose nommée.
- **Supplante, sur des points nommés** :
  - [ADR-0026](./0026-executer-une-demande-requests-lit-le-parametrage-et-appelle-le-systeme-hote.md)
    — la clause **« tout 2xx vaut "le droit a été appliqué" »**, et avec elle la phrase « l'hôte ne
    répond 2xx qu'une fois le droit appliqué ; l'exécution est synchrone ». Elle n'a pas
    d'équivalent sur un bus, où il n'y a pas de réponse. **L'aboutissement d'une exécution devient
    « le destinataire, ou le broker pour lui, a accusé réception »** ; le 2xx n'est plus la règle,
    seulement la forme que prend cet accusé sur le canal HTTP.

    Tout ce qui valait encore de l'ADR-0026 tient : la relation `Requests → Configuration` en
    _Customer/Supplier_, la revérification serveur et ses codes de refus (409, 422, 404), le journal
    d'exécution — une ligne par tentative, aucune donnée personnelle, survie à la suppression —,
    l'écriture de la tentative après l'acte, le « succès non enregistré » et sa seconde transaction,
    **aucune nouvelle tentative automatique**, et la réserve sur l'ADR-0022. Le journal garde sa
    forme, mais sa colonne et son résultat fermé changent d'objet : voir l'arbitrage 4. Ses
    conditions d'exécution et l'ordre de ses motifs de blocage, eux, étaient déjà supplantés par
    l'ADR-0027 ; le présent ADR n'en reprend que le cinquième motif.
  - [ADR-0027](./0027-un-droit-un-seul-canal-une-adresse-http-ou-un-routage-rabbitmq.md) — le
    **cinquième motif de blocage**, « Le {droit} s'exerce par RabbitMQ, que le service ne sait pas
    encore publier », déclaré provisoire et **écrit pour mourir**. Il est supprimé et remplacé, au
    même rang, par un motif **permanent** qui dit une autre vérité : la connexion au broker manque
    au déploiement.

    Tout le reste de l'ADR-0027 tient : « un droit, un seul canal », l'exclusivité tenue à
    l'écriture sans valeur dormante, l'absence de colonne discriminante, le canal d'exercice comme
    type fermé à trois cas, « il configure, il n'appelle pas », la topologie du bus propriété de
    l'exploitant, la connexion au broker comme affaire de déploiement, et le bandeau décidé sur la
    seule présence d'une clé.

## Contexte

Un intégrateur peut déclarer, droit par droit, le routage RabbitMQ par lequel le service exercera un
droit (ADR-0027). Le service, lui, ne sait pas publier : une demande dont le droit invoqué s'exerce
par un routage est bloquée par un motif que l'ADR-0027 a écrit pour être supprimé. Le Paramétrage
ment donc par omission — un droit y est configuré et inexécutable — et un système hôte qui ne
consomme qu'un bus n'a aucun usage du service.

L'ADR-0027 avait explicitement laissé ouverte la question qui commande tout le reste : « publier un
message à l'exécution d'une demande, **et ce qu'un succès veut dire sur ce canal** ». Sur HTTP, la
réponse de l'hôte porte le verdict, et l'ADR-0026 a pu décider que tout 2xx valait application. Un
bus n'a pas de réponse : le service parle à un broker, et le destinataire est un consommateur qu'il
ne voit jamais. Il faut donc dire ce qui vaut aboutissement quand personne ne répond, ce que le
journal garde d'une publication, par quelle forme le code exerce deux canaux sans les confondre, et
ce que le service fait d'un déploiement qui n'a pas de broker.

Cet ADR tranche ces points avant qu'une ligne de code soit écrite. Il ne touche à aucun code, à
aucun schéma, à aucun texte d'écran.

## Décision

**Une exécution aboutit quand le destinataire, ou le broker pour lui, a accusé réception.** Le geste
ne change pas : même bouton, mêmes conditions, même modale de confirmation, même journal
d'exécution, même passage à Terminée. Seul change ce qui fait foi de l'accusé, et le canal par
lequel il vient.

### 1. Publisher confirms et `mandatory`, non le fire-and-forget

Le service publie sur un channel ouvert en **publisher confirms**, avec suivi des confirmations, et
pose le drapeau **`mandatory`** sur chaque publication. Il **attend l'accusé du broker** avant de
conclure. Un `ack` vaut aboutissement ; un `nack`, un message qu'aucune file ne reçoit, une
confirmation qui n'arrive pas dans le délai, ou un broker injoignable laissent la demande En cours.

> **Contre le fire-and-forget.** Publier sans confirmation rend la main en quelques microsecondes,
> et fait passer à Terminée une demande dont rien ne prouve qu'elle a quitté le service. C'est
> exactement le mensonge que l'ADR-0026 refusait en écrivant la tentative après l'appel. Sans
> `mandatory`, un message publié sur un exchange sans binding disparaît **en silence** : c'est le
> mensonge le plus probable de ce canal, et le drapeau est ce qui l'empêche.

> **Contre l'accusé applicatif par file de retour.** Faire confirmer le système hôte lui-même — une
> file de retour que le service consommerait, une demande qui resterait En cours entre la
> publication et la réponse — prouverait le **traitement**, pas seulement la remise. Mais c'est un
> autre modèle d'exécution : un consommateur dans le service, un état d'attente que le statut ne
> sait pas dire, un contrat de retour à imposer à l'intégrateur. Il aura son ADR s'il est un jour
> ouvert ; il n'est pas ouvert ici.

### 2. Le corps identique à l'HTTP, non une enveloppe d'événement

Le message porte **exactement les cinq valeurs du corps HTTP de l'ADR-0026** — `requestId`, `right`,
`email`, `firstName`, `lastName` —, sérialisées de la même façon, les cinq clés toujours présentes.
Il porte `content-type: application/json`, un **`message-id` égal à l'identifiant de la demande**,
et il est **persistant**.

Le `message-id` est la clé d'idempotence du bus, comme `requestId` l'est en HTTP : il permet à un
consommateur de rejeter une republication **sans ouvrir le corps**. L'idempotence reste la charge du
destinataire, ici comme là.

> **Contre une enveloppe d'événement** — un type d'événement, une version de schéma, un horodatage,
> une source, et les cinq valeurs dans un `data`. Elle est la forme naturelle d'un bus, mais elle
> donnerait à l'intégrateur **deux contrats à implémenter** selon le canal qu'il choisit, pour un
> service qui n'émet qu'un seul message. Le contrat est unique ; le canal ne décide que du chemin
> par lequel il voyage.

### 3. Un port typé sur le genre, non deux ports ni un `switch` dans le handler

Le port de `Core/Requests` par lequel passe la remise prend le **canal d'exercice** — le genre de
l'ADR-0027 —, non une adresse HTTP. Le handler d'exécution ne connaît qu'un geste et ne filtre rien.
Le canal se filtre **à un seul endroit**, dans `Infrastructure`, où un adaptateur de répartition
délègue à l'adaptateur HTTP, inchangé dans son comportement, ou à l'adaptateur RabbitMQ.

> **Contre deux ports**, un par espèce. Le handler devrait choisir lequel appeler, donc filtrer le
> canal : le `switch` reviendrait dans `UseCases`, et chaque canal futur y rouvrirait une couture.

> **Contre un `switch` dans le handler.** Même objection, sans le détour : le contexte saurait par
> quel canal il fait exercer un droit, ce qui n'est pas de son ressort.

### 4. Une colonne de journal qui dit le canal, non des colonnes par espèce

La colonne du journal d'exécution qui portait l'URL appelée devient l'**exercice** : l'adresse
amputée de sa query string et de son fragment, **ou** l'exchange et la routing key en toutes
lettres. Une seule colonne, un seul renommage, les lignes existantes gardent leur valeur — une
adresse est un exercice. Aucune colonne n'est ajoutée, et **aucune colonne discriminante** ne redit
le canal : même motif qu'à l'ADR-0027.

Le résultat d'une tentative reste une **valeur fermée unique**, partagée par les deux canaux : les
cas existants sont réutilisés tels quels, et deux cas s'ajoutent pour ce que seul un bus produit —
un message que personne ne reçoit, et une publication refusée par le broker. Le statut HTTP reste
nul sur toute publication.

> **Contre des colonnes par espèce** — une pour l'URL, une pour l'exchange, une pour la routing key,
> dont deux sont toujours nulles. Trois états illégaux représentables, un journal qui ne se compte
> ni ne se filtre d'un seul tenant, et une preuve qui se lit différemment selon le canal. Ce que le
> journal doit prouver est **qu'un droit a été remis, et par où** : une phrase suffit à le dire.

### 5. Le blocage sur connexion absente, non une tentative vouée à l'échec

Quand le **déploiement** n'a aucune connexion au broker — un état que l'ADR-0027 a rendu légal —,
une demande dont le droit s'exerce par un routage est **bloquée**, au cinquième rang des motifs :
« Le {droit} s'exerce par RabbitMQ, mais aucune connexion n'est configurée ». Le bouton reste
éteint, l'infobulle nomme le droit, le serveur revérifie le motif juste avant de publier, et ce
refus **n'écrit aucune ligne** au journal — c'est le comportement des blocages de l'ADR-0026,
inchangé.

Ce motif et le bandeau de la page « Configuration RabbitMQ » se décident sur **la même règle**, pour
que l'`Operator` et l'intégrateur lisent la même vérité. Cette règle — « une clé vide ou faite
d'espaces vaut une clé absente » — vit à un seul endroit du noyau, et « ce déploiement sait
publier » devient une chose nommée du domaine plutôt qu'un booléen lu par un page model.

Le motif provisoire de l'ADR-0027 disparaît : rien à l'écran ne parle plus d'une limite qui n'existe
plus. L'ordre des motifs, lui, est conservé dans son motif — ce qui se corrige sur la demande passe
avant ce qui se règle ailleurs.

> **Contre une tentative vouée à l'échec** — publier quand même, échouer en erreur réseau, et écrire
> la ligne. Le journal se remplirait de tentatives qu'aucune topologie ne pouvait faire aboutir, et
> l'`Operator` lirait « le broker est injoignable » là où la vérité est « ce déploiement n'a pas de
> broker ». Un blocage dit à qui il faut s'adresser ; un échec ne dit que ce qui s'est passé.

### 6. Aucune reprise automatique du client AMQP

La reprise automatique du client AMQP est **désactivée**, et rien ne republie sans le geste de
l'`Operator`. La connexion est unique et partagée, ouverte **paresseusement** au premier besoin :
aucune connexion ne s'ouvre au démarrage ni au rendu d'un écran, et la disponibilité du service ne
dépend jamais de celle du broker.

> **Contre la reprise intégrée du client**, active par défaut. Elle rouvre connexion et channels
> dans le dos du service et peut **rejouer** ce qu'il croyait perdu : un droit serait remis au
> système hôte sans qu'aucun `Operator` l'ait demandé. C'est la même décision que le retrait des
> handlers de résilience du client HTTP à l'ADR-0026, pour le même motif — **aucune nouvelle
> tentative automatique**, et un seul geste, un seul effet possible.

### Ce que « Terminée » cesse de signifier sur ce canal

⚠️ Sur un routage, **Terminée ne dit plus que le droit a été appliqué**. Le service sait que le
broker a **accepté** le message ; il ne sait **jamais** que le système hôte l'a **traité**. Le
destinataire est un consommateur qu'il ne voit pas, qui peut être arrêté, en retard, ou en train
d'échouer.

Cette perte de sens n'est pas tue : les écrans la disent, et l'`Operator` en est averti **avant** de
confirmer une action irréversible. Le glossaire de `Requests` est amendé en conséquence —
« Terminée », « Système hôte », « Tentative d'exécution » et « Motif de blocage » changent d'énoncé,
et une entrée dit ce qu'un accusé du broker prouve et ne prouve pas.

C'est le prix de la publication, et il est payé en toutes lettres : un canal sans réponse ne peut
pas rendre un verdict d'application. Ce qu'il peut prouver — que le droit a été **remis**, et que
quelqu'un en a accusé réception — est ce que l'aboutissement signifie désormais, sur les deux
canaux.

## Les options écartées

Outre les six options écartées ci-dessus, chacune en regard de son arbitrage :

- **Garder « tout 2xx vaut application » pour HTTP et écrire une seconde règle pour le bus.** Deux
  définitions de l'aboutissement, à tenir d'accord dans le journal, dans le statut et dans les mots
  des écrans. Une seule règle, dont le 2xx est une forme, dit la même chose sans se dédoubler.
- **Laisser le cinquième motif provisoire en place** et bloquer les droits routés sur un déploiement
  sans broker par un sixième motif. L'ADR-0027 a écrit ce motif pour mourir ; le garder ferait lire
  comme une règle une limite qui n'existe plus.
- **Un statut « publiée, en attente de traitement »** entre En cours et Terminée. C'est l'accusé
  applicatif déguisé en état : sans file de retour, rien ne l'en ferait jamais sortir.
- **Déclarer l'exchange à la publication**, pour qu'un routage jamais créé ne soit pas un échec. Ce
  serait s'arroger la topologie du bus, que l'ADR-0027 laisse à l'exploitant. Le message non
  routable est précisément ce qui le lui signale.
- **Un délai de confirmation aligné sur celui de l'appel HTTP.** Un accusé de broker se compte en
  millisecondes ; faire patienter l'`Operator` aussi longtemps que pour une application synchrone
  n'a aucune contrepartie. Le délai du bus est réglable et nettement plus court.
- **Une chaîne de connexion URI** pour le broker. Les identifiants n'entrent pas dans une URL, pour
  le motif qui interdit déjà l'`userinfo` dans une `EndpointUrl` (ADR-0016).
- **Arrêter le démarrage quand aucun broker n'est configuré.** Un service sans bus doit démarrer
  normalement ; c'est un réglage **présent mais aberrant** qui doit l'arrêter, comme le délai de
  l'ADR-0026.

## Conséquences, y compris celles qui coûtent

⚠️ **« Terminée » ne veut pas la même chose selon le canal.** Une colonne de statut porte désormais
deux sens : droit appliqué sur HTTP, message accepté par le broker sur un routage. Le tableau ne les
distingue pas ; seuls la modale, le journal et le glossaire le disent.

⚠️ **Une confirmation non reçue à temps laisse un doute qui ne se lève pas.** Le message a peut-être
été publié. L'`Operator` en est averti, et ré-exécuter peut republier : c'est le `message-id` qui
protège, chez le consommateur, et nulle part ailleurs.

⚠️ **Le blocage sur connexion absente hérite de l'angle mort du bandeau** (ADR-0027) : il se décide
sur la présence d'une clé, jamais sur un broker joignable. Une clé posée et un broker éteint ne
bloquent rien, et l'exécution échoue en erreur réseau. C'est le prix du « aucun test réseau au
rendu », et il est conservé.

⚠️ **Le service dépend d'un client AMQP et d'un broker en conteneur pour ses tests.** La couture
fonctionnelle de l'exécution exige désormais Docker et quelques secondes de démarrage de plus.

⚠️ **Un consommateur non idempotent appliquera deux fois un droit republié.** Le service donne tout
ce qu'il faut pour l'éviter — un `message-id` stable —, et ne peut rien de plus : sur un bus, il ne
voit pas qui l'écoute.

⚠️ **L'ADR-0027 porte un motif de blocage qu'il déclarait provisoire et que celui-ci supprime.** Sa
suite datée le nomme ; la table de `CONTEXT-MAP.md` reste l'endroit où les deux se lisent ensemble.

## Ce que cet ADR n'ouvre pas

- **L'accusé applicatif du système hôte** — une file de retour consommée par le service, et une
  demande en attente entre la publication et la réponse. Autre modèle d'exécution, autre ADR.
- **Un gabarit de message configurable**, une enveloppe d'événement, des en-têtes AMQP maison.
- **La saisie de la connexion au broker à l'écran** : elle reste de la configuration de déploiement.
- **La déclaration ou la vérification de l'exchange par le service** (ADR-0027, inchangé).
- **Tout autre broker que RabbitMQ**, et tout vocabulaire de broker générique à l'écran.
- **Les nouvelles tentatives automatiques**, la relance d'une exécution qui n'a pas abouti, et toute
  file de rejets.
- **L'authentification et les secrets vers le système hôte** — toujours fermés (ADR-0016, ADR-0026).
- **Un droit portant deux canaux à la fois** (ADR-0027, inchangé).
- **La lecture du journal d'exécution à l'écran**, et sa durée de conservation (ADR-0026).
- **Annuler une demande**, l'autre acte qui ferait changer le statut (ADR-0026).
