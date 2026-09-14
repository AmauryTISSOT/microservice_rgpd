# Mock du système hôte

Un faux système hôte, **local uniquement**, qui expose une route par droit configurable dans l'écran
`/parametrage`. Il existe pour qu'on puisse y renseigner des adresses **réellement joignables** et
simuler un temps de traitement, sans dépendre d'un vrai système hôte.

Le service n'appelle toujours pas ces adresses ([ADR-0016](../../docs/adr/0016-le-manifest-cede-la-place-au-parametrage-un-droit-une-adresse.md)) : le mock n'est qu'une
cible. Il ne valide aucune requête, ne rend aucune donnée factice et ne simule ni erreur ni timeout.

## Routes

| Droit (article RGPD) | Route | Code | Corps de réponse |
| --- | --- | --- | --- |
| Accès (15) | `GET /rights/access` | `200 OK` | `{"status":"ok"}` |
| Portabilité (20) | `GET /rights/portability` | `200 OK` | `{"status":"ok"}` |
| Rectification (16) | `PATCH /rights/rectification` | `204 No Content` | aucun |
| Effacement (17) | `DELETE /rights/erasure` | `204 No Content` | aucun |
| Limitation (18) | `POST /rights/restriction` | `202 Accepted` | `{"status":"ok"}` |
| Opposition (21) | `POST /rights/objection` | `202 Accepted` | `{"status":"ok"}` |
| — | `GET /health` | `200 OK` | sans délai |

- Les six routes de droit répondent **après le délai simulé**, jamais avant.
- Corps, en-têtes et absence de corps sont indifférents : seule la méthode compte. Une méthode non
  prévue rend `405 Method Not Allowed`.
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
curl -i -X DELETE http://localhost:5199/rights/erasure
```

## Tester

```sh
uv run pytest                                  # depuis ce dossier
uv run --project src/mock-host pytest src/mock-host   # depuis la racine du dépôt
```

La suite réduit le délai via `MOCK_DELAY_MS` et vérifie qu'aucune réponse n'arrive avant lui.
