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

**Le contrat d'`Adapter` — ce que le service appelle chez le client — est documenté dans
[`docs/api/adapter.md`](docs/api/adapter.md)** : l'appel sortant, le secret partagé et sa clause de
périmètre, le `202` et son échéance déclarée, les deux refus. Le sens est unique : le service
appelle, l'application ne rappelle jamais.

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

L'orchestration Aspire démarre aussi le **sidecar de qualification** (`src/sidecar`) : la pile
entière part d'une seule commande. [`uv`](https://docs.astral.sh/uv/) doit être installé — Aspire lui
délègue la création de l'environnement virtuel et l'installation des dépendances.

**Le moteur LLM est éteint par défaut, et un clone frais démarre donc sans GPU et sans télécharger
un octet de modèle** : ni container Ollama ni modèle n'entrent dans la pile. `POST /qualifications`
répond, avec le seul témoin lexical pour avis — la qualification sort en `Mode dégradé`.

Pour l'allumer, passer `Llm:Enabled` à `"true"` dans
[`src/MicroserviceRgpd.AspireHost/appsettings.json`](src/MicroserviceRgpd.AspireHost/appsettings.json).
L'AppHost est l'**unique vérité** de ce drapeau : il le propage au service .NET comme au sidecar, qui
ne peuvent donc pas diverger. Le container Ollama entre alors dans la pile, et le modèle est tiré au
premier démarrage puis conservé dans un volume nommé ; comptez plusieurs gigaoctets.

**L'API seule tourne elle aussi sans LLM par défaut.** Lancé à la main contre un sidecar local, le
service .NET ne lit plus l'AppHost : le drapeau qui compte est alors `Qualification:Llm:Enabled` dans
[`src/MicroserviceRgpd.Web/appsettings.json`](src/MicroserviceRgpd.Web/appsettings.json), éteint lui
aussi. Qui veut le LLM dans ce mode doit **l'écrire des deux côtés** — ici, et dans l'environnement du
sidecar (`QUALIFICATION_LLM_ENABLED`) : hors Aspire, plus aucune source commune ne les tient
d'accord.

Allumé, le container Ollama réclame le GPU (`WithGPUSupport()` dans l'AppHost), ce qui suppose une
carte **NVIDIA** et le [NVIDIA Container Toolkit](https://docs.nvidia.com/datacenter/cloud-native/container-toolkit/latest/install-guide.html)
installé côté Docker. Pour vérifier avant de lancer la pile :

```sh
docker run --rm --gpus=all --entrypoint nvidia-smi ollama/ollama:0.13.0 -L
```

Sans ce prérequis, le container refuse de démarrer — c'est délibéré : le repli silencieux sur le
processeur donnait une pile qui « marche » mais dont personne n'attend les réponses.

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
uv run --directory src/sidecar pytest
```

La suite du sidecar ne demande **ni réseau sortant, ni GPU, ni clé d'API** : elle n'exerce que le
lexique déterministe et la frontière HTTP du sidecar. `uv` crée l'environnement virtuel et installe
les dépendances verrouillées à la première exécution.

`--directory`, et non `--project` : il déplace aussi le répertoire courant, ce dont dépend toute la
collecte. Avec `--project`, pytest garde la racine du dépôt pour `rootdir`, ne lit donc jamais le
`testpaths` de `src/sidecar/pyproject.toml`, balaie tout le dépôt et ramasse `temoin/tests/` — qui
porte le même nom de paquet que `src/sidecar/tests/` et fait échouer la collecte.

**Il n'y a ni CI ni hook git, et c'est délibéré.** Les tests à container coûtent une dizaine de
secondes de démarrage ; un `pre-commit` qui les lance serait désactivé dans la semaine, et un
garde-fou désactivé est pire qu'absent — il donne l'illusion d'une protection.

## Conventions

- Erreurs de l'API : `application/problem+json` (RFC 9457) avec `traceId`, forme **unique** —
  validation FastEndpoints, exceptions non gérées et codes rendus par la plateforme compris.
- `Directory.Packages.props` : versions centralisées (Central Package Management).
- `TreatWarningsAsErrors` est actif, audit NuGet inclus — un package vulnérable casse le build.
