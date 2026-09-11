# Les tests à container

Trois des quatre projets de test montent une **base PostgreSQL réelle** via
[Testcontainers](https://dotnet.testcontainers.org/) : `IntegrationTests`, `FunctionalTests` et
`BrowserTests`.
Image **`postgres:18-alpine`**, schéma posé par les **migrations du dépôt**.

> Ce document décrivait auparavant une pile SQL Server. Elle n'a jamais existé dans le dépôt tel
> qu'il est aujourd'hui : **PostgreSQL est le seul provider supporté**, il n'existe aucun repli en
> mémoire ni aucun repli local.

## Prérequis

**Docker doit être démarré.** Sans lui, `IntegrationTests`, `FunctionalTests` et `BrowserTests` échouent au
démarrage — ce n'est pas contournable par une option de configuration, et c'est délibéré : une base
en mémoire ne partage ni les types (`text[]`, `timestamptz`), ni les contraintes de nullité, ni la
génération de clés de Npgsql, c'est-à-dire précisément ce que ces tests sont là pour tenir.

Le premier lancement télécharge l'image — une centaine de mégaoctets, la variante Alpine étant
choisie pour cela ; le démarrage d'un container coûte ensuite une poignée de secondes.

## Qui monte quoi

| Projet | Container | Schéma | Portée |
| --- | --- | --- | --- |
| `UnitTests` | **aucun** | — | domaine, handlers, adaptateurs HTTP moqués |
| `IntegrationTests` | un, partagé par toute la suite | `MigrateAsync()` | **la persistance de la trace d'audit, et rien d'autre** |
| `FunctionalTests` | un par fabrique d'application | `Migrate()` | l'endpoint public de bout en bout |
| `BrowserTests` | un, partagé par toute la suite | `Migrate()` | un parcours d'écrans dans un vrai Chromium |
| `AspireTests` | **aucun**, et il doit le rester | — | volontairement vide — `IsTestProject=false`, il ne compte pas parmi les quatre |

### `IntegrationTests`

`PostgreSqlFixture` monte **un seul container pour toute la suite**, partagé par une
`ICollectionFixture` : en monter un par classe coûterait des dizaines de secondes pour vérifier la
même migration. Le schéma vient de `MigrateAsync()`, jamais d'`EnsureCreated()` — ce sont les
migrations du dépôt que ces tests vérifient, pas le modèle dont elles sont issues.

Ce projet a **un seul rôle** : la persistance de la trace d'audit. Ce qui le sauve de la
suppression est une conséquence du contrat public — sans `GET`, la trace est inatteignable depuis
l'endpoint, donc aucun test fonctionnel ne peut la voir.

### `FunctionalTests`

`CustomWebApplicationFactory` monte son propre container, pose la chaîne de connexion dans
`ConnectionStrings__DefaultConnection` — variable d'environnement, seul moyen de la fournir assez
tôt, le `ConfigurationManager` de `Program` étant construit avant tout `ConfigureAppConfiguration`
— puis applique `Migrate()`.

**Les deux moteurs de qualification y sont substitués**, sur le port `IQualificationEngine` du
domaine. La trace d'audit, elle, **n'est pas substituée** : elle écrit dans le vrai PostgreSQL, et
c'est ce qui donne du sens à « qualifier, écrire, répondre ».

### `BrowserTests`

`BrowserHarness` monte **un seul container, un seul service et un seul Chromium** pour toute la
suite, partagés par une `ICollectionFixture`. Le service tourne sous **Kestrel, sur un port que
l'OS attribue** (`WebApplicationFactory.UseKestrel(0)`) : le serveur en mémoire des tests
fonctionnels n'est atteignable par aucun navigateur. La chaîne de connexion passe par la même
variable d'environnement, puis `Migrate()`.

**Chromium est installé par la fixture elle-même**, par le pilote que le paquet
`Microsoft.Playwright` copie à côté des binaires : ni `pwsh`, ni `playwright install` à la main.
Le premier lancement télécharge le navigateur dans le cache de Playwright
(`~/.cache/ms-playwright` sous Linux) ; les suivants le trouvent en place.

Les tests lisent l'écran **comme l'`Operator` le lit** — par le rôle et le nom accessible d'un lien,
d'un titre, d'un bouton —, jamais par une classe CSS ni par la forme du DOM. Rien n'y est substitué
tant qu'aucun parcours n'approche un moteur.

### `AspireTests`

Volontairement vide, et à garder vide. Tout test démarrant l'`AppHost` démarrerait aussi le sidecar
de qualification **et le container Ollama** : il exigerait un GPU, ne tournerait donc jamais — et un
test qui ne tourne jamais ment. Le `.csproj` porte cette raison en commentaire ; ni référence à
l'`AppHost`, ni `Aspire.Hosting.Testing`.

## Aucun test n'appelle Ollama

La frontière est posée sur **`IQualificationEngine`**, doublé dans `UnitTests` et `FunctionalTests`.
Pas sur le fil HTTP, pas sur des réponses enregistrées, pas sur un vrai sidecar marqué et exclu :
les enregistrements pourrissent en silence et rendent indiscernable une régression de code d'une
montée de version de modèle.

Les adaptateurs HTTP vers le sidecar sont couverts dans `UnitTests` avec un `HttpMessageHandler`
moqué — c'est là que la palette de codes du sidecar, les dépassements d'échéance et les branches
`503`/`504` publiques sont atteints délibérément.

## Lancer les tests

```sh
dotnet test MicroserviceRgpd.slnx
```

Les containers sont créés et détruits par le cycle de vie xUnit (`IAsyncLifetime`), y compris
lorsque des tests échouent.

## Paquets

Versions centralisées dans `Directory.Packages.props` :

```xml
<PackageVersion Include="Testcontainers" Version="4.13.0" />
<PackageVersion Include="Testcontainers.PostgreSql" Version="4.13.0" />
```

Référencés sans version par `IntegrationTests`, `FunctionalTests` et `BrowserTests`.
