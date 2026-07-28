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
tests/
  MicroserviceRgpd.UnitTests/         # Domaine, handlers et adaptateurs, isolés
  MicroserviceRgpd.IntegrationTests/  # Persistance sur un vrai PostgreSQL (Testcontainers)
  MicroserviceRgpd.FunctionalTests/   # Endpoints de bout en bout (WebApplicationFactory)
  MicroserviceRgpd.AspireTests/       # Volontairement vide — voir le commentaire du .csproj
```

L'agrégat de démonstration du template a été supprimé : le service n'expose pour l'instant que
`GET /hello`. Le premier agrégat métier est à créer.

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
| Tests            | xUnit, NSubstitute, Shouldly, Testcontainers |

## Démarrer

```sh
dotnet build MicroserviceRgpd.slnx
dotnet test  MicroserviceRgpd.slnx
```

## Avant d'ouvrir une PR

**La commande à passer au vert**, depuis la racine du dépôt :

```sh
dotnet test MicroserviceRgpd.slnx && pytest
```

Elle couvre les deux moitiés du service : les tests .NET de la solution, puis les tests `pytest`
du sidecar de qualification. Docker doit être démarré — les tests d'intégration et fonctionnels
montent un PostgreSQL en container.

> Le sidecar Python n'est pas encore dans le dépôt : jusqu'à son arrivée, seule la première
> moitié de la commande a de quoi s'exécuter.

**Il n'y a ni CI ni hook git, et c'est délibéré.** Les tests à container coûtent une dizaine de
secondes de démarrage ; un `pre-commit` qui les lance serait désactivé dans la semaine, et un
garde-fou désactivé est pire qu'absent — il donne l'illusion d'une protection.

```sh
# API seule
dotnet run --project src/MicroserviceRgpd.Web

# Avec orchestration Aspire (dépendances en containers + dashboard)
dotnet run --project src/MicroserviceRgpd.AspireHost
```

Les tests d'intégration et fonctionnels utilisent Testcontainers : **Docker doit être démarré**.

### Migrations EF Core

```sh
dotnet ef migrations add <Nom> --project src/MicroserviceRgpd.Infrastructure --startup-project src/MicroserviceRgpd.Web
dotnet ef database update      --project src/MicroserviceRgpd.Infrastructure --startup-project src/MicroserviceRgpd.Web
```

## Conventions

- Erreurs de l'API : `application/problem+json` (RFC 9457) avec `traceId`, forme **unique** —
  validation FastEndpoints, exceptions non gérées et codes rendus par la plateforme compris.
- `Directory.Packages.props` : versions centralisées (Central Package Management).
- `TreatWarningsAsErrors` est actif, audit NuGet inclus — un package vulnérable casse le build.
