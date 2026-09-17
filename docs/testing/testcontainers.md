# Les tests à container

Trois des cinq projets de test montent une **base PostgreSQL réelle** via
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
| `IntegrationTests` | **un seul**, et une base par classe qui en demande une | `MigrateAsync()` | **ce que seule une vraie base prouve** : la trace d'audit, et les colonnes du Paramétrage |
| `FunctionalTests` | un par fabrique d'application, soit **un par collection** | `Migrate()` | l'endpoint public de bout en bout |
| `BrowserTests` | **un par collection**, cinq en tout | `Migrate()` | un parcours d'écrans dans un vrai Chromium |
| `AspireTests` | **aucun**, et il doit le rester | — | volontairement vide — `IsTestProject=false`, il ne compte pas parmi les cinq |

### `IntegrationTests`

`PostgreSqlServer` monte **un seul container pour tout le projet**, et rend à qui lui en demande une
**base neuve et vide**. `PostgreSqlFixture` en prend une, la migre, et la partage par une
`ICollectionFixture` avec les classes qui attendent un schéma à jour. Le schéma vient de
`MigrateAsync()`, jamais d'`EnsureCreated()` — ce sont les migrations du dépôt que ces tests
vérifient, pas le modèle dont elles sont issues.

⚠️ **Les classes qui éprouvent une migration ont besoin d'une base *non migrée*** : elles remontent à
un état d'avant, y écrivent des lignes de cette date-là, puis jouent la migration. Chacune montait
donc son propre container — dix containers pour dix schémas, et une minute d'horloge. Elles
demandent maintenant leur base à `PostgreSqlServer` : `CREATE DATABASE` coûte quelques dizaines de
millisecondes, l'isolement est le même — deux bases d'un serveur ne partagent ni table, ni séquence,
ni transaction —, et **elles gardent leur parallélisme**, n'étant rassemblées dans aucune collection.

⚠️ **Le container n'est jamais arrêté en code.** xUnit v2 n'offre pas de point d'accroche à
l'échelle de l'assembly : c'est le *resource reaper* de Testcontainers (Ryuk) qui le retire à la
mort du processus de test. Le désactiver (`TESTCONTAINERS_RYUK_DISABLED`) laisserait un container
derrière chaque exécution.

Ce projet ne porte que ce qu'**aucune autre couche ne peut prouver**. Son premier rôle est la
persistance de la trace d'audit : c'est une conséquence du contrat public — sans `GET`, la trace est
inatteignable depuis l'endpoint, donc aucun test fonctionnel ne peut la voir.

Le second, depuis l'ADR-0027, est la ligne du **Paramétrage** : une valeur dormante ne se voit pas
dans l'agrégat qui vient de l'effacer, elle se voit dans la ligne. `SettingsChannelPersistenceTests`
y vérifie qu'un canal posé efface **en base** les colonnes de l'autre canal du même droit.

⚠️ **`SeedRequestsTests` prend sa propre base**, comme les tests de reprise de migration : il y
rejoue `scripts/seed-requests.sql`, et les cent demandes plantées fausseraient les classes qui
comptent les lignes de `data_subject_requests` dans la base partagée. Le script est joué par `psql`
dans le container, qui s'ouvre sur la base par défaut du serveur : un `\connect` posé en tête le
mène à la sienne.

### `FunctionalTests`

`CustomWebApplicationFactory` monte son propre container, pose la chaîne de connexion dans
`ConnectionStrings__DefaultConnection` — variable d'environnement, seul moyen de la fournir assez
tôt, le `ConfigurationManager` de `Program` étant construit avant tout `ConfigureAppConfiguration`
— puis applique `Migrate()`.

Il y a **une fabrique par collection**, et la moitié d'entre elles portent une forme d'hôte à part —
drapeau A2 allumé, LLM éteint, broker configuré ou injoignable. Les autres démarrent le câblage
ordinaire, et sont **cinq** : voir `WebCollection.cs`, qui dit pourquoi. Comme dans la suite
navigateur, chaque collection a **sa propre base**, ses tests y restent en série, et le découpage
reste au **grain de la classe**.

**Les deux moteurs de qualification y sont substitués**, sur le port `IQualificationEngine` du
domaine. La trace d'audit, elle, **n'est pas substituée** : elle écrit dans le vrai PostgreSQL, et
c'est ce qui donne du sens à « qualifier, écrire, répondre ».

### `BrowserTests`

`BrowserHarness` monte **un container, un service et un Chromium par collection**, partagés par une
`ICollectionFixture`. La suite en compte **cinq** — voir `BrowserCollection.cs`, qui dit pourquoi :
xUnit ne parallélise pas les tests d'une même collection, et une collection unique faisait passer
les 265 tests un par un, six minutes durant. Cinq harnais tournent donc de front, chacun sur **sa
propre base** ; le découpage reste au **grain de la classe**, et deux classes qui se lisent l'état
l'une de l'autre doivent rester ensemble. Le service tourne sous **Kestrel, sur un port que
l'OS attribue** (`WebApplicationFactory.UseKestrel(0)`) : le serveur en mémoire des tests
fonctionnels n'est atteignable par aucun navigateur. La chaîne de connexion passe par la même
variable d'environnement, puis `Migrate()`.

**Chromium est installé par la fixture elle-même**, par le pilote que le paquet
`Microsoft.Playwright` copie à côté des binaires : ni `pwsh`, ni `playwright install` à la main.
Le premier lancement télécharge le navigateur dans le cache de Playwright
(`~/.cache/ms-playwright` sous Linux) ; les suivants le trouvent en place.

⚠️ **Le téléchargement n'apporte pas les bibliothèques système dont Chromium dépend.** Un poste
de développement qui fait déjà tourner un navigateur les a ; une image Linux minimale peut ne pas
les avoir, et le lancement de Chromium y échoue. La fixture ne les installe pas — cela demanderait
les droits root : il faut alors les poser une fois, par le gestionnaire de paquets du système.

Les tests lisent l'écran **comme l'`Operator` le lit** — par le rôle et le nom accessible d'un lien,
d'un titre, d'un bouton —, jamais par une classe CSS ni par la forme du DOM. Seuls les deux moteurs
de qualification y sont substitués, par la même `QualificationEngineDouble` que les tests
fonctionnels : le harnais les expose en `Verdict` et `Lexicon`, et un test les remet à zéro avant de
leur dicter un avis.

### `AspireTests`

Volontairement vide, et à garder vide. Tout test démarrant l'`AppHost` démarrerait aussi le sidecar
de qualification **et le container Ollama** : il exigerait un GPU, ne tournerait donc jamais — et un
test qui ne tourne jamais ment. Le `.csproj` porte cette raison en commentaire ; ni référence à
l'`AppHost`, ni `Aspire.Hosting.Testing`.

## Aucun test n'appelle Ollama

La frontière est posée sur **`IQualificationEngine`**, doublé dans `UnitTests`, `FunctionalTests` et
`BrowserTests` — ces deux derniers par la même doublure, qui vit dans `MicroserviceRgpd.TestDoubles`.
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
