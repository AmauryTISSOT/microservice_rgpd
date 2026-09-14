# Mock du système hôte

Un faux système hôte, **local uniquement**, qui expose une route par droit configurable dans l'écran
`/parametrage`. Il sert de cible **réellement joignable** aux adresses qu'on y renseigne : quand
l'`Operator` exécute une demande, le service fait un `POST` JSON à l'adresse du droit invoqué
([ADR-0026](../../docs/adr/0026-executer-une-demande-requests-lit-le-parametrage-et-appelle-le-systeme-hote.md)).
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

Une valeur de `MockHost:Enabled` qui n'est ni `true` ni `false`, ou un `MOCK_DELAY_MS` qui n'est pas
un entier positif ou nul, arrête le démarrage.

## Lancer

Sous Aspire, passer `MockHost:Enabled` à `"true"` puis lancer la pile comme d'habitude :

```sh
dotnet run --project src/MicroserviceRgpd.AspireHost
```

La ressource `mock-host` apparaît dans le dashboard et passe « healthy » grâce à `/health`. Pour
changer le délai sous Aspire, poser `MOCK_DELAY_MS` dans l'environnement du processus qui lance
l'AppHost.

Seul, depuis ce dossier :

```sh
uv sync
uv run uvicorn mock_host.app:app --port 5199
MOCK_DELAY_MS=500 uv run uvicorn mock_host.app:app --port 5199
curl -i -X POST http://localhost:5199/rights/erasure
curl -i -X POST "http://localhost:5199/rights/access?status=503&delay_ms=0"
```

## Tester

```sh
uv run pytest                                  # depuis ce dossier
uv run --project src/mock-host pytest src/mock-host   # depuis la racine du dépôt
```

La suite réduit le délai via `MOCK_DELAY_MS` ou `delay_ms` et vérifie qu'aucune réponse n'arrive
avant lui.
