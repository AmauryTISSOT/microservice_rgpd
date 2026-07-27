# microservice_rgpd

Microservice backend de gestion des données personnelles (RGPD), en .NET 10 / Clean Architecture.

Généré depuis [ardalis/CleanArchitecture](https://github.com/ardalis/CleanArchitecture) v11.1.1 (variante `clean-arch`).

## Architecture

Clean Architecture aux frontières (règle de dépendance vers l'intérieur), organisation en vertical slices
dans la couche `UseCases` (un dossier par feature : `Create/`, `Delete/`, `Get/`, `List/`, `Update/`).

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

L'agrégat `Contributor` livré par le template est un **exemple de référence** : il sert de modèle pour
le premier agrégat métier réel, puis doit être supprimé.

## Stack

| Rôle | Choix | Licence |
|---|---|---|
| Médiation / CQRS | Mediator (martinothamar, source-generated) | MIT |
| HTTP | FastEndpoints 7.1 (REPR) + Scalar | Apache 2.0 / MIT |
| Données | EF Core 10 (SQL Server / SQLite) | MIT |
| Result pattern | Ardalis.Result | MIT |
| Value objects | Vogen (source generator) | MIT |
| Specifications | Ardalis.Specification | MIT |
| Logs | Serilog + sink OpenTelemetry | Apache 2.0 |
| Observabilité | OpenTelemetry 1.17 via ServiceDefaults | Apache 2.0 |
| Tests | xUnit, NSubstitute, Shouldly, Testcontainers | MIT / BSD |

Aucune dépendance sous licence commerciale : ni MediatR, ni AutoMapper, ni FluentAssertions 8+, ni MassTransit 9.

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

## Conformité RGPD — décisions à prendre avant le premier agrégat métier

Ces choix conditionnent le modèle de données et coûtent cher à rattraper :

- **Crypto-shredding** plutôt que soft/hard delete. Une clé AES par sujet de données, stockée hors base
  (Key Vault / Vault). L'effacement (art. 17) détruit la clé, pas la ligne — la piste d'audit (art. 5§2)
  est préservée et l'effacement se propage partout à la fois (backups, caches, réplicas).
  Le soft delete seul **n'est pas conforme** : la donnée reste lisible en base.
- **Implémentation EF Core** : `ISaveChangesInterceptor` pour chiffrer à l'écriture (après assignation
  des IDs, sinon `Guid.Empty`) + `IMaterializationInterceptor` pour déchiffrer, `null` si la clé a disparu.
- **Journal d'effacement en deux phases** : intention avant destruction de la clé, confirmation après,
  dans un log immuable non supprimable.
- **Aucune PII dans les logs ni les traces OpenTelemetry** — prévoir un processeur de redaction.
- **Rétention et purge automatiques**, export machine-readable (art. 20), journal de consentement horodaté.

## Conventions

- `Directory.Packages.props` : versions centralisées (Central Package Management).
- `TreatWarningsAsErrors` est actif, audit NuGet inclus — un package vulnérable casse le build.
