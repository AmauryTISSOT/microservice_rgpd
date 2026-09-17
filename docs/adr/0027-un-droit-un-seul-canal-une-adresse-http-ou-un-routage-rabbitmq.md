# ADR-0027 — Un droit, un seul canal : une adresse HTTP ou un routage RabbitMQ, jamais les deux

- **Statut** : accepté
- **Date** : 2026-09-17
- **Décidé par** : l'US [#489](https://github.com/AmauryTISSOT/microservice_rgpd/issues/489)
  « Configuration : l'intégrateur déclare, droit par droit, le routage RabbitMQ par lequel le
  service exercera un droit », ouverte par
  [#490](https://github.com/AmauryTISSOT/microservice_rgpd/issues/490)
- **Glossaires** : [`CONTEXT-MAP.md`](../../CONTEXT-MAP.md),
  [Configuration](../contexts/configuration/CONTEXT.md), [Requests](../contexts/requests/CONTEXT.md)
- **Supplante, sur des points nommés** :
  - [ADR-0016](./0016-le-manifest-cede-la-place-au-parametrage-un-droit-une-adresse.md) — la clause
    **« un droit, une adresse »**, jusque dans son titre : le `Settings` associe désormais à chacun
    des six droits du périmètre un **canal d'exercice**, dont l'adresse HTTP n'est plus qu'une
    espèce. Le reste de l'ADR-0016 tient : le grain par droit, le singleton, la naissance
    paresseuse, « non configuré » comme état valide, « enregistrer n'émet aucun appel », le contexte
    `Configuration` et sa traversée vers le noyau partagé.
  - [ADR-0026](./0026-executer-une-demande-requests-lit-le-parametrage-et-appelle-le-systeme-hote.md)
    — deux points, et deux seulement :
    - la clause **« son droit a une adresse configurée »** parmi les conditions d'exécution, qui
      devient « son droit s'exerce par une adresse HTTP » ;
    - **la liste et l'ordre des motifs de blocage**, qui passent de quatre à cinq et dont le
      quatrième est reformulé (voir « Les motifs de blocage » ci-dessous).

    Tout le reste de l'ADR-0026 tient sans retouche : la relation `Requests → Configuration` en
    _Customer/Supplier_, le contrat de l'appel HTTP et sa règle « tout 2xx vaut application », le
    journal d'exécution, la revérification serveur et ses codes de refus, la réserve sur
    l'ADR-0022.

## Contexte

Le Paramétrage n'offre qu'une manière de brancher le service sur le système hôte : une adresse HTTP,
droit par droit (ADR-0016), que `Requests` lit pour exécuter une demande (ADR-0026). Un système hôte
qui n'expose pas d'API HTTP — et beaucoup consomment un bus de messages — n'a aujourd'hui aucun
endroit où se déclarer.

Ouvrir un second moyen d'exercer un droit pose une question que ni l'ADR-0016 ni l'ADR-0026 n'ont eu
à trancher, parce qu'il n'y avait qu'un moyen : **un droit peut-il en porter deux à la fois ?** De
la réponse dépendent la forme de l'agrégat, celle de la base, ce que les écrans montrent, et ce que
`Requests` lit avant d'appeler.

Cet ADR répond à cette question et fixe les décisions dont dépend le reste de l'US. Il ne touche à
aucun code.

## Décision

**Un droit, un seul canal.** Un droit du périmètre porte une adresse HTTP, **ou** un routage
RabbitMQ, **ou** rien. Jamais les deux. Un droit qui n'a ni l'une ni l'autre est **« non
configuré »**, et c'est un état valide, comme il l'était sous l'ADR-0016.

### Le canal d'exercice, et son vocabulaire

**« Canal » est le genre.** Le **canal d'exercice** est le moyen par lequel le service fera exercer
un droit ; l'**adresse HTTP** et le **routage RabbitMQ** en sont les deux espèces, et « non
configuré » le troisième cas. C'est un type fermé à trois cas, dont `NotConfigured` est un membre
nommé et non un `null` : « non configuré » est un état du glossaire, pas un trou, et le nommer rend
exhaustive toute lecture d'un canal.

Le mot « canal » est donc **promu, pas banni** : il entre au glossaire de `Configuration` comme
entrée à part entière. L'US d'origine demandait de l'écarter du vocabulaire du routage tout en
l'employant elle-même à chaque paragraphe pour désigner le genre ; la lecture retenue range ce mot
là où elle l'emploie. Son `Avoid` sur l'entrée « Routage RabbitMQ » signifie qu'un routage est un
canal **parmi deux**, jamais le genre lui-même.

La formule d'en-tête du glossaire de `Configuration`, « un droit, une adresse », devient **« un
droit, un seul canal »**.

### L'exclusivité est tenue à l'écriture

**Poser un canal sur un droit efface l'autre, sans valeur dormante.** Enregistrer un routage
RabbitMQ efface l'adresse HTTP du même droit ; enregistrer une adresse efface le routage. Rien n'est
conservé en réserve : un retour en arrière est un geste explicite, jamais une résurrection.

L'invariant est tenu **au seul endroit qui écrit**, et non par la discipline des appelants. Aucun
autre droit n'est touché par le geste. `OutOfScope` n'a pas de canal : le lui demander lève, à la
lecture comme à l'écriture, comme l'ADR-0016 l'avait décidé pour l'adresse.

**Aucune colonne discriminante en base.** L'exclusivité étant garantie à l'écriture, la présence
d'un exchange **est** le canal RabbitMQ. Une colonne qui redirait le canal créerait un second état
illégal représentable.

### Il configure, il n'appelle pas — la clause vaut pour le bus

La clause de l'ADR-0016 s'étend au second canal, et elle y est plus forte encore :

- **Enregistrer un routage n'ouvre aucune connexion au broker.** Le service doit être configurable
  avant que le broker existe.
- **Enregistrer un routage ne déclare ni ne vérifie l'exchange.** La **topologie du bus reste la
  propriété de l'exploitant** : ce n'est pas au Paramétrage de créer ce sur quoi il publiera.
- **Le bandeau qui avertit qu'aucune connexion RabbitMQ n'est configurée sur le déploiement se
  décide sur la seule présence d'une clé de configuration**, sans le moindre test réseau.
  L'affichage d'un écran ne dépend jamais de la disponibilité du broker, et un routage reste
  enregistrable quand la clé est absente.

**La connexion au broker relève du déploiement, pas de l'écran** : hôte, port, vhost et identifiants
restent de la configuration de déploiement, pour qu'aucun secret ne transite par la base ni par la
surface — le même motif qui interdit l'`userinfo` dans une `EndpointUrl` (ADR-0016).

### Les motifs de blocage (supplante l'ADR-0026)

La condition « son droit a une adresse configurée » devient « son droit s'exerce par une adresse
HTTP ». Les quatre motifs de blocage de l'ADR-0026 deviennent **cinq**, et l'ordre complet est :

1. **Demande close**
2. **Identité non vérifiée**
3. **Email manquant**
4. **« Le {droit} n'est pas configuré »** — reformulation de « Aucune adresse configurée pour le
   {droit} », qui disait une contrainte devenue fausse : un droit peut désormais être configuré de
   deux façons.
5. **« Le {droit} s'exerce par RabbitMQ, que le service ne sait pas encore publier »** — motif
   nouveau, **provisoire**, en dernier rang.

L'ordre de l'ADR-0026 est conservé dans son motif : ce qui se corrige sur la demande passe avant ce
qui se règle dans le Paramétrage. Le cinquième motif vient en dernier parce qu'il ne se corrige
**nulle part** aujourd'hui.

**Le cinquième motif est le seul élément de cette US conçu pour être supprimé.** Il dit la vérité à
l'`Operator` : le droit *est* configuré, c'est le service qui ne sait pas encore publier. Il
disparaîtra avec l'US de publication.

Une exécution bloquée par ce motif **n'appelle rien, ne publie rien, n'écrit aucune tentative** au
journal d'exécution : c'est le comportement des blocages de l'ADR-0026, inchangé. Une demande dont
le droit porte une adresse HTTP s'exécute exactement comme avant.

### Ce que cette US laisse délibérément faux

Un intégrateur peut déclarer un routage que le service ne sait pas exercer. C'est assumé : la
déclaration précède la publication, le motif de blocage dit la vérité à l'`Operator`, et le bandeau
la dit à l'intégrateur.

## Les sept écarts assumés au texte de l'US

L'US d'origine décrit une solution ; sept points de sa lettre ont été écartés, chacun pour une
raison qui tient hors de ce ticket.

1. **Une seule commande d'effacement, partagée par les deux canaux**, au lieu des deux commandes
   symétriques demandées. Le canal étant un type fermé, effacer un routage et effacer une adresse
   sont le même geste : ramener un droit à « non configuré ». Deux handlers identiques au caractère
   près seraient une divergence en attente.
2. **Le motif de blocage reformulé garde le nom du droit** — « le {droit} n'est pas configuré » — au
   lieu d'être aplati en « droit non configuré ». Ce motif sert d'infobulle au bouton éteint d'une
   ligne parmi d'autres : le nom du droit y est l'information utile.
3. **Le champ « Adresse appelée » de la modale de confirmation est renommé « Exercice »** et affiche
   le canal — l'adresse, ou l'exchange et la routing key, ou l'absence — au lieu d'afficher un tiret
   pour un droit réglé sur RabbitMQ. Un champ nommé d'après une espèce ne peut pas montrer le genre.
4. **Les colonnes du routage sont en `text` sans longueur déclarée**, et non en `varchar(255)`. Une
   longueur en base compterait des caractères quand la contrainte du domaine compte des **octets
   UTF-8** : déclarer 255 en base donnerait à lire une limite qui n'est pas celle qu'on applique. La
   contrainte reste la propriété du value object, comme celles d'`EndpointUrl` — absolue, schéma,
   userinfo —, dont aucune n'est reportée en base.
5. **La projection droit par droit et l'accesseur de l'agrégat sont renommés pour parler de canal.**
   L'US ne demandait pas ce renommage, mais le glossaire l'impose dès lors que « canal » devient le
   genre : un accesseur qui dit « endpoint » nommerait une espèce là où il rend le genre.
6. **Un `<fieldset>` apparaît sur la page RabbitMQ seule**, pour rattacher l'exchange et la routing
   key au droit sans alourdir chaque libellé d'une périphrase. La page HTTP, qui n'a qu'un champ par
   droit, n'est pas modifiée sur ce point.
7. **Les onglets, les avertissements d'exclusivité et le bandeau de connexion sont testés à la
   couture fonctionnelle et non en Playwright**, que l'US demandait. Ces trois-là sont rendus par le
   serveur, sans JavaScript : la couture fonctionnelle les couvre entièrement, en une fraction du
   temps, et donne sur la clé de configuration un contrôle que la couture navigateur — un vrai
   Kestrel sur un port réel — n'offre pas. Un seul scénario reste en Playwright : la modale
   affichant le motif provisoire, seul endroit où du JavaScript compose l'affichage.

## Les options écartées

- **Laisser un droit porter les deux canaux**, avec une règle de préséance à l'exécution. Il aurait
  fallu écrire cette préséance quelque part et la lire partout ; l'écran aurait montré deux réglages
  dont un seul agit. L'exclusivité déplace la question là où elle se décide, à l'écriture.
- **Conserver l'autre canal en valeur dormante**, pour qu'un retour en arrière soit gratuit. Une
  valeur invisible qui ressuscite au prochain effacement est un piège ; le geste explicite coûte une
  saisie, une fois.
- **Une colonne discriminante nommant le canal en vigueur.** Un second état illégal représentable en
  base, à maintenir d'accord avec le premier.
- **Deux commandes d'effacement symétriques**, comme l'US le demandait — voir l'écart n° 1.
- **Vérifier ou déclarer l'exchange à l'enregistrement**, pour refuser tôt un routage impossible.
  C'est ouvrir une connexion au broker depuis un écran de configuration, et s'arroger une topologie
  qui appartient à l'exploitant.
- **Un bouton « tester »** : tester la connexion ou publier un message d'essai, c'est publier.
- **Refuser l'enregistrement d'un routage quand la connexion n'est pas configurée sur le
  déploiement.** L'intégrateur doit pouvoir préparer le branchement avant la mise en service du bus.
- **Un vocabulaire de broker générique à l'écran.** RabbitMQ est ce qui est décidé ; nommer un
  « bus » abstrait promettrait des brokers que rien ne sert.

## Conséquences, y compris celles qui coûtent

⚠️ **Un droit peut être configuré et inexécutable.** Un routage déclaré bloque l'exécution avec le
cinquième motif, jusqu'à l'US de publication. C'est le prix de la déclaration avant la publication.

⚠️ **Un motif de blocage est écrit pour mourir.** Le cinquième motif, sa documentation et ses tests
devront être supprimés avec l'US de publication, et l'ordre repassera à quatre. Un motif provisoire
qu'on oublie de retirer se lit comme une règle.

⚠️ **Changer de canal détruit un réglage.** Enregistrer une adresse sur un droit qui portait un
routage efface ce routage, et réciproquement. Les deux écrans avertissent avant le clic ; rien ne se
récupère après.

⚠️ **L'état illégal reste représentable dans les champs privés de l'agrégat**, qui garde des
propriétés plates nullables pour son état persistant et compose le canal à la lecture. Il ne l'est
plus dans le type que le reste du monde manipule. L'alternative — dix-huit champs de stockage privés
et une propriété calculée par droit — a été écartée pour son coût en configuration EF, sans gain sur
l'invariant réellement observable.

⚠️ **L'ADR-0016 porte dans son titre une clause que celui-ci supplante.** « Un droit, une adresse »
reste lisible en tête d'un ADR en vigueur par ailleurs. Le dépôt n'édite pas un ADR supplanté : la
table de `CONTEXT-MAP.md` est le seul endroit où les deux se lisent ensemble.

⚠️ **Le bandeau de connexion peut mentir par optimisme.** Il se décide sur la présence d'une clé,
pas sur un broker joignable : une clé présente et un broker éteint ne produisent aucun avertissement.
C'est le prix du « aucun test réseau au rendu ».

## Ce que cet ADR n'ouvre pas

- **Publier un message à l'exécution d'une demande**, et ce qu'un succès veut dire sur ce canal :
  seconde US, avec son propre ADR. La règle « tout 2xx vaut application » de l'ADR-0026 n'a pas
  d'équivalent immédiat sur un bus.
- **Le corps du message**, un gabarit, les propriétés AMQP — en-têtes, persistance, type de contenu.
- **La saisie de la connexion au broker à l'écran**, et toute clé de déploiement autre que celle qui
  décide le bandeau : port, vhost, identifiants et TLS viendront avec la publication.
- **La vérification ou la déclaration de l'exchange** sur le broker.
- **Tout autre broker que RabbitMQ**, et tout vocabulaire de broker générique à l'écran.
- **Un cinquième point d'entrée dans le panneau latéral** : le Paramétrage reste une entrée, à deux
  pages.
- **L'authentification et les secrets vers le système hôte** — toujours fermés, comme sous les
  ADR-0016 et 0026.
- **La datation par droit** : le Paramétrage ne sait toujours ni quand un canal a été posé, ni par
  qui.
