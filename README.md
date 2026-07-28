# microservice_rgpd

Microservice backend de gestion des données personnelles (RGPD), en .NET 10 / Clean Architecture.

Généré depuis [ardalis/CleanArchitecture](https://github.com/ardalis/CleanArchitecture) v11.1.1 (variante `clean-arch`).

## Architecture

Clean Architecture aux frontières (règle de dépendance vers l'intérieur), organisation en vertical slices
dans la couche `UseCases` : un dossier par feature.

```
src/
  MicroserviceRgpd.Core/            # Entités, value objects (Vogen), domain events, specifications — aucune I/O
  MicroserviceRgpd.UseCases/        # Handlers CQRS, organisés en vertical slices
  MicroserviceRgpd.Infrastructure/  # EF Core, repository, dispatch d'événements, email
  MicroserviceRgpd.Web/             # Endpoints FastEndpoints (REPR), composition racine
  MicroserviceRgpd.AspireHost/      # Orchestration locale des dépendances
  MicroserviceRgpd.ServiceDefaults/ # OpenTelemetry, health checks, résilience HTTP, service discovery
  sidecar/                          # Sidecar Python : les moteurs de qualification et leur suite pytest
tests/
  MicroserviceRgpd.UnitTests/         # Domaine, handlers et adaptateurs, isolés
  MicroserviceRgpd.IntegrationTests/  # Persistance sur un vrai PostgreSQL (Testcontainers)
  MicroserviceRgpd.FunctionalTests/   # Endpoints de bout en bout (WebApplicationFactory)
  MicroserviceRgpd.AspireTests/       # Volontairement vide — voir le commentaire du .csproj
```

L'agrégat de démonstration du template a été supprimé. Le service expose `POST /qualifications`,
qui rend une qualification RGPD **dans le même échange**, et `GET /hello` en endpoint de fumée.
Il n'existe **aucun `GET`** sur la ressource de qualification : c'est un acte dont on repart avec
le résultat, jamais une ressource qu'on relit.

**Le contrat public est documenté dans [`docs/api/qualifications.md`](docs/api/qualifications.md)** :
requête, réponse, codes d'erreur, règle d'évolution et avertissements d'exploitation. Un intégrateur
n'a besoin que de ce document.

Deux moteurs qualifient le texte. Celui dont l'avis fait verdict est un LLM auto-hébergé ; le second
est un lexique déterministe, qui ne vote pas mais **corrobore ou conteste** — c'est de leur
comparaison que sort le `reviewSignal`. Si l'un des deux se tait, le service rend quand même un
verdict avec `degraded: true` ; si les deux se taisent, il rend un `503` ou un `504`.

La base est **PostgreSQL**, fournie en container par Aspire (`microservice_rgpd_bdd`). C'est le seul
provider supporté : il n'existe pas de repli local, Docker est donc requis pour lancer le service
comme pour exécuter les tests fonctionnels.

## Stack

| Rôle             | Choix                                        |
| ---------------- | -------------------------------------------- |
| Médiation / CQRS | Mediator (martinothamar, source-generated)   |
| HTTP             | FastEndpoints 7.1 (REPR) + Scalar            |
| Données          | EF Core 10 + Npgsql (PostgreSQL)             |
| Result pattern   | Ardalis.Result                               |
| Value objects    | Vogen (source generator)                     |
| Specifications   | Ardalis.Specification                        |
| Logs             | Serilog + sink OpenTelemetry                 |
| Observabilité    | OpenTelemetry 1.17 via ServiceDefaults       |
| Qualification    | Sidecar Python (FastAPI / uvicorn), lancé par Aspire |
| Tests            | xUnit, NSubstitute, Shouldly, Testcontainers ; `pytest` côté sidecar |

## Démarrer

```sh
dotnet build MicroserviceRgpd.slnx
```

Pour les tests, la porte à passer avant PR est plus bas — elle a **deux moitiés**, et lancer la
seule solution .NET laisserait le sidecar Python hors du filet.

```sh
# API seule
dotnet run --project src/MicroserviceRgpd.Web

# Avec orchestration Aspire (dépendances en containers + dashboard)
dotnet run --project src/MicroserviceRgpd.AspireHost
```

L'orchestration Aspire démarre aussi le **sidecar de qualification** (`src/sidecar`) et le container
**Ollama** qui sert le modèle : la pile entière part d'une seule commande.
[`uv`](https://docs.astral.sh/uv/) doit être installé — Aspire lui délègue la création de
l'environnement virtuel et l'installation des dépendances. Le modèle est tiré au premier démarrage
et conservé dans un volume nommé ; comptez plusieurs gigaoctets, et un GPU pour que les temps de
réponse aient un sens.

Les tests d'intégration et fonctionnels utilisent Testcontainers : **Docker doit être démarré**.
Voir [`TESTCONTAINERS_IMPLEMENTATION.md`](TESTCONTAINERS_IMPLEMENTATION.md). **Aucun test ne
démarre Ollama, ni ne s'approche d'un GPU.**

### Migrations EF Core

```sh
dotnet ef migrations add <Nom> --project src/MicroserviceRgpd.Infrastructure --startup-project src/MicroserviceRgpd.Web
dotnet ef database update      --project src/MicroserviceRgpd.Infrastructure --startup-project src/MicroserviceRgpd.Web
```

## Avant d'ouvrir une PR

**La porte à passer au vert**, depuis la racine du dépôt — les deux moitiés du service, les tests
.NET de la solution puis les tests `pytest` du sidecar de qualification :

```sh
dotnet test MicroserviceRgpd.slnx
uv run --project src/sidecar pytest
```

La suite du sidecar ne demande **ni réseau sortant, ni GPU, ni clé d'API** : elle n'exerce que le
lexique déterministe et la frontière HTTP du sidecar. `uv` crée l'environnement virtuel et installe
les dépendances verrouillées à la première exécution.

**Il n'y a ni CI ni hook git, et c'est délibéré.** Les tests à container coûtent une dizaine de
secondes de démarrage ; un `pre-commit` qui les lance serait désactivé dans la semaine, et un
garde-fou désactivé est pire qu'absent — il donne l'illusion d'une protection.

## Conventions

- Erreurs de l'API : `application/problem+json` (RFC 9457) avec `traceId`, forme **unique** —
  validation FastEndpoints, exceptions non gérées et codes rendus par la plateforme compris.
- `Directory.Packages.props` : versions centralisées (Central Package Management).
- `TreatWarningsAsErrors` est actif, audit NuGet inclus — un package vulnérable casse le build.
