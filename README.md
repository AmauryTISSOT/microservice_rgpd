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
  MicroserviceRgpd.UnitTests/         # Domaine et handlers isolés
  MicroserviceRgpd.IntegrationTests/  # Repository sur base réelle
  MicroserviceRgpd.FunctionalTests/   # Endpoints de bout en bout (WebApplicationFactory)
  MicroserviceRgpd.AspireTests/       # Orchestration
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

- `Directory.Packages.props` : versions centralisées (Central Package Management).
- `TreatWarningsAsErrors` est actif, audit NuGet inclus — un package vulnérable casse le build.
