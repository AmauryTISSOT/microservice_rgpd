# Mock du système hôte

Un faux système hôte, **local uniquement**, cible des **deux canaux d'exercice** que l'écran
`/parametrage` propose
([ADR-0027](../../docs/adr/0027-un-droit-un-seul-canal-une-adresse-http-ou-un-routage-rabbitmq.md)) :

- une **adresse HTTP** — il expose une route par droit, et le service y fait un `POST` JSON quand
  l'`Operator` exécute une demande
  ([ADR-0026](../../docs/adr/0026-executer-une-demande-requests-lit-le-parametrage-et-appelle-le-systeme-hote.md)) ;
- un **routage RabbitMQ** — il consomme l'exchange `rgpd.rights`, journalise ce qu'il reçoit et
  acquitte
  ([ADR-0028](../../docs/adr/0028-l-aboutissement-d-une-execution-cesse-d-etre-un-2xx-le-broker-accuse-reception.md)).

Le mock ne valide aucune requête et ne rend aucune donnée factice ; il simule un temps de traitement,
et, à la demande, un échec ou une attente.

## Routes

| Droit (article RGPD) | Route | Code | Corps de réponse |
| --- | --- | --- | --- |
| Accès (15) | `POST /rights/access` | `200 OK` | `{"status":"ok"}` |
| Portabilité (20) | `POST /rights/portability` | `200 OK` | `{"status":"ok"}` |
| Rectification (16) | `POST /rights/rectification` | `204 No Content` | aucun |
| Effacement (17) | `POST /rights/erasure` | `204 No Content` | aucun |
| Limitation (18) | `POST /rights/restriction` | `202 Accepted` | `{"status":"ok"}` |
| Opposition (21) | `POST /rights/objection` | `202 Accepted` | `{"status":"ok"}` |
| — | `GET /health` | `200 OK` | sans délai |

- Les six routes de droit répondent **après le délai simulé**, jamais avant.
- Les six routes de droit n'acceptent que `POST`, la méthode du contrat d'exécution. Toute autre
  méthode rend `405 Method Not Allowed`.
- Corps, en-têtes et absence de corps sont indifférents.
- Chaque appel d'une route de droit est écrit sur la sortie standard — méthode, route, corps reçu —,
  donc visible dans les logs de la ressource `mock-host` du dashboard Aspire.

## Les URL à coller dans `/parametrage`

| Droit | Adresse |
| --- | --- |
| Accès | `http://localhost:5199/rights/access` |
| Portabilité | `http://localhost:5199/rights/portability` |
| Rectification | `http://localhost:5199/rights/rectification` |
| Effacement | `http://localhost:5199/rights/erasure` |
| Limitation | `http://localhost:5199/rights/restriction` |
| Opposition | `http://localhost:5199/rights/objection` |

Le port `5199` est **fixe**, déclaré dans
[`AppHost.cs`](../MicroserviceRgpd.AspireHost/AppHost.cs) : les adresses saisies restent valables
d'un lancement à l'autre.

## Le routage à saisir dans `/parametrage`

L'onglet **RabbitMQ** du Paramétrage demande deux choses par droit : un **exchange** et une
**routing key**. Voici ce que le mock écoute.

| Champ | Valeur |
| --- | --- |
| Exchange | `rgpd.rights` |
| Routing key | `rights.` suivi de ce qu'on veut — par exemple `rights.erasure` |

Le mock se lie en `rights.#` : **toute** routing key commençant par `rights.` lui parvient, quel que
soit le droit. Les valeurs suggérées, par symétrie avec les routes HTTP :

| Droit | Exchange | Routing key |
| --- | --- | --- |
| Accès | `rgpd.rights` | `rights.access` |
| Portabilité | `rgpd.rights` | `rights.portability` |
| Rectification | `rgpd.rights` | `rights.rectification` |
| Effacement | `rgpd.rights` | `rights.erasure` |
| Limitation | `rgpd.rights` | `rights.restriction` |
| Opposition | `rgpd.rights` | `rights.objection` |

**C'est le mock qui déclare la topologie** — l'exchange (topic, durable), sa file
`rgpd.rights.mock-host` (durable) et le binding `rights.#` —, jamais le service : la topologie du
bus appartient à l'exploitant (ADR-0027). Une routing key qui ne commence pas par `rights.`
n'atteint donc aucune file, et l'exécution est refusée comme **non routable** — ce que le journal
d'exécution de la demande dit en toutes lettres.

### Ce que le consommateur journalise

Chaque message reçu écrit une ligne sur la sortie standard, donc visible dans les logs de la
ressource `mock-host` du dashboard Aspire :

```text
rgpd.rights reçu : droit=Erasure demande=3f1d9a3c-… message-id=3f1d9a3c-…
```

Le message est ensuite **acquitté**, ce qui fait passer la demande à **Terminée** côté service. Un
message retenu obtient le **temps de traitement simulé** de `MOCK_DELAY_MS`, comme un appel HTTP ;
un doublon ne l'obtient pas — c'est cela, « ignoré ».

Aucune donnée personnelle n'entre dans la ligne : ni email, ni prénom, ni nom.

### L'idempotence est la charge du consommateur

Le service pose un `message-id` **égal à l'identifiant de la demande** (ADR-0028) précisément pour
qu'une republication se reconnaisse sans qu'on ouvre le corps. Le mock s'en sert : il garde en
mémoire les `message-id` déjà vus, et **le dit** quand il en revoit un.

```text
rgpd.rights ignoré (doublon) : droit=Erasure demande=3f1d9a3c-… message-id=3f1d9a3c-…
```

C'est la moitié « l'idempotence est la charge du consommateur » de l'avertissement de la modale
d'exécution, ici **démontrée** plutôt qu'affirmée. La mémoire est celle du processus et ne survit
pas à son redémarrage : c'est une démonstration, pas un magasin d'idempotence.

Un message **sans** `message-id` est accepté, et la ligne dit qu'il n'est pas déduplicable — plutôt
que de laisser croire à une garantie qu'il ne porte pas. Un doublon est **acquitté comme un neuf** :
le refuser le ferait revenir sans fin.

## Simuler un échec ou une attente

Deux paramètres, ajoutés à l'adresse collée dans `/parametrage`, changent la réponse **d'un seul
droit** — celui dont l'adresse les porte —, à chaque appel :

| Paramètre | Valeurs | Effet |
| --- | --- | --- |
| `status` | entier de `200` à `599` | le code de réponse, à la place du code de succès de la route. Corps `{"status":"ok"}` pour un `2xx`, `{"status":"error"}` au-delà, aucun pour `204` et `304` |
| `delay_ms` | entier positif ou nul | le délai avant la réponse, en millisecondes, à la place de `MOCK_DELAY_MS` |

```text
http://localhost:5199/rights/erasure?status=503              # le système hôte refuse
http://localhost:5199/rights/access?delay_ms=40000           # au-delà du délai d'exécution (30 s par défaut)
http://localhost:5199/rights/objection?status=500&delay_ms=0 # un échec immédiat
```

Un `status` ou un `delay_ms` illisible — pas un entier, négatif, ou un code hors de `200`–`599` —
rend `400 Bad Request` sans attendre, avec un `{"detail": …}` qui nomme le paramètre. Les autres
paramètres sont ignorés.

## Configuration

| Réglage | Où | Défaut | Rôle |
| --- | --- | --- | --- |
| `MockHost:Enabled` | [`appsettings.json` de l'AppHost](../MicroserviceRgpd.AspireHost/appsettings.json) | `false` | `true` fait entrer le mock dans la pile Aspire |
| `MOCK_DELAY_MS` | environnement du mock | `2000` | le délai simulé des six routes de droit, en millisecondes ; `0` répond sans attendre |
| `MOCK_RABBITMQ_HOST` | environnement du mock | — | l'hôte du broker. **Sa seule présence allume le consommateur** ; son absence est un état légal, et le mock reste alors la cible HTTP qu'il a toujours été |
| `MOCK_RABBITMQ_PORT` | environnement du mock | `5672` | le port AMQP |
| `MOCK_RABBITMQ_VHOST` | environnement du mock | `/` | le vhost |
| `MOCK_RABBITMQ_USER` | environnement du mock | `guest` | l'identifiant de connexion |
| `MOCK_RABBITMQ_PASSWORD` | environnement du mock | `guest` | le mot de passe de connexion |

Sous Aspire, les quatre réglages du broker sont posés par
[`AppHost.cs`](../MicroserviceRgpd.AspireHost/AppHost.cs), à partir de la ressource `rabbitmq` — les
mêmes valeurs que celles qu'il donne au service sous la section `RabbitMq`. Rien à saisir.

Une valeur de `MockHost:Enabled` qui n'est ni `true` ni `false`, un `MOCK_DELAY_MS` qui n'est pas
un entier positif ou nul, ou un `MOCK_RABBITMQ_PORT` qui n'est pas un port entre 1 et 65535, arrête
le démarrage.

Le mot de passe est le **seul** des cinq à ne pas être rogné : une espace peut en faire partie.

**Le mock n'attend pas le broker.** Il démarre, répond sur ses routes HTTP et passe « healthy »
broker éteint ; la connexion AMQP se raccroche seule quand le broker arrive, et une panne en cours
de route — broker redémarré, exchange déclaré autrement — est annoncée sur la sortie standard puis
retentée. C'est le pendant, côté mock, de l'absence de `WaitFor` entre le service et le broker dans
l'AppHost.

## Lancer

Sous Aspire, passer `MockHost:Enabled` à `"true"` puis lancer la pile comme d'habitude :

```sh
dotnet run --project src/MicroserviceRgpd.AspireHost
```

La ressource `mock-host` apparaît dans le dashboard et passe « healthy » grâce à `/health`. Pour
changer le délai sous Aspire, poser `MOCK_DELAY_MS` dans l'environnement du processus qui lance
l'AppHost.

La ressource `rabbitmq` démarre avec elle, **sans drapeau** : sa console de gestion est joignable
depuis le dashboard, de quoi voir l'exchange, la file et les messages sans outil tiers.

Seul, depuis ce dossier :

```sh
uv sync
uv run uvicorn mock_host.app:app --port 5199
MOCK_RABBITMQ_HOST=localhost uv run uvicorn mock_host.app:app --port 5199   # avec le consommateur
MOCK_DELAY_MS=500 uv run uvicorn mock_host.app:app --port 5199
curl -i -X POST http://localhost:5199/rights/erasure
curl -i -X POST "http://localhost:5199/rights/access?status=503&delay_ms=0"
```

## Démontrer le parcours complet

1. `MockHost:Enabled` à `"true"` dans l'`appsettings.json` de l'AppHost, puis
   `dotnet run --project src/MicroserviceRgpd.AspireHost`. Les ressources `rabbitmq`, `web` et
   `mock-host` apparaissent au dashboard ; le journal de `mock-host` annonce la file qu'il consomme.
2. Dans `/parametrage`, onglet **RabbitMQ**, poser sur le droit voulu l'exchange `rgpd.rights` et la
   routing key qui lui correspond — par exemple `rights.erasure`. **Rien n'est semé** : ce geste est
   celui de l'intégrateur, et c'est lui que la démonstration montre.
3. Enregistrer une demande invoquant ce droit, puis l'**exécuter**.
4. Le journal de `mock-host` écrit la ligne `rgpd.rights reçu : …`, la demande passe à **Terminée**,
   et le journal d'exécution de la demande porte la ligne correspondante.
5. Pour voir le doublon, republier le **même** `message-id` depuis la console de gestion du broker
   (*Exchanges → `rgpd.rights` → Publish message*, routing key `rights.erasure`, propriété
   `message_id` égale à l'identifiant de la demande) : `mock-host` annonce qu'il l'ignore. Dans la
   vraie vie, ce doublon naît d'une exécution dont la confirmation s'est perdue — le message était
   arrivé, la demande n'est pas passée à Terminée, et l'`Operator` réexécute : le service republie
   sous le même `message-id`, puisqu'il vaut l'identifiant de la demande.

La console de gestion du broker, joignable depuis le dashboard, montre l'exchange, la file et le
compte de messages à chaque étape.

## Tester

```sh
uv run pytest                                  # depuis ce dossier
uv run --project src/mock-host pytest src/mock-host   # depuis la racine du dépôt
```

La suite réduit le délai via `MOCK_DELAY_MS` ou `delay_ms` et vérifie qu'aucune réponse n'arrive
avant lui. Elle éprouve aussi le consommateur **sans broker** : ce qu'il journalise et ce qu'il fait
d'un doublon vivent dans `mock_host/rights_journal.py`, qui ne connaît ni socket ni AMQP.
