# Clean Architecture Aspire Host

This project uses .NET Aspire to orchestrate the application and its dependencies.

## Le moteur LLM, éteint par défaut

Le moteur génératif est commandé par un seul réglage, `Llm:Enabled` dans l'`appsettings` de ce projet. **Absent, il vaut « éteint »** — et c'est le cas d'un clone frais du dépôt.

Éteint, la pile ne contient **ni serveur de modèles Ollama, ni modèle à tirer** : elle démarre d'une seule commande sur un poste sans carte graphique, sans télécharger les quelques gigaoctets du modèle. Le sidecar de qualification, lui, est toujours là — le lexique y vit —, et `POST /qualifications` rend une qualification en `Mode dégradé`.

Allumé (`"Enabled": "true"`), la composition est celle d'avant ce drapeau : Ollama entre dans la pile avec son volume de modèles persistant, le modèle est tiré au démarrage, et le sidecar attend qu'il le soit. Le prérequis est alors le **NVIDIA Container Toolkit** côté Docker.

L'`AppHost` est l'**unique vérité** de ce drapeau : il le propage au service .NET comme au sidecar, qui ne peuvent donc pas diverger sous Aspire. Lancés séparément, chacun lit sa propre configuration.

## PostgreSQL Container

The Aspire host runs a PostgreSQL container named `microservice_rgpd_bdd` and automatically provides the connection string to the Web application.

### Running the Application

1. Set `MicroserviceRgpd.AspireHost` as the startup project
2. Run the application (F5 or Ctrl+F5)
3. The Aspire Dashboard will open, showing all running resources including the PostgreSQL container
4. The Web application will automatically connect to the PostgreSQL container

### Connection String

When running through Aspire, the connection string is automatically provided and overrides `DefaultConnection` in appsettings.json. The connection is named "cleanarchitecture" and is referenced in the Web project.

### Running Without Aspire

PostgreSQL is the only supported provider — there is no local fallback. To run the Web project directly, set `ConnectionStrings:DefaultConnection` in appsettings.json to a reachable PostgreSQL instance; startup fails fast otherwise.

### Database Migrations

To create a new migration, from the Web project directory:
```bash
dotnet ef migrations add MigrationName -c AppDbContext -p ../MicroserviceRgpd.Infrastructure/MicroserviceRgpd.Infrastructure.csproj -s MicroserviceRgpd.Web.csproj -o Data/Migrations
```

To update the database:
```bash
dotnet ef database update -c AppDbContext -p ../MicroserviceRgpd.Infrastructure/MicroserviceRgpd.Infrastructure.csproj -s MicroserviceRgpd.Web.csproj
```

Note: migrations are applied automatically at startup in the Development environment.

### Container Persistence

The container is configured with `ContainerLifetime.Persistent` and a data volume, so data persists between application runs. To reset the database, you can:
1. Delete the container through the Aspire dashboard
2. Use the Docker CLI: `docker rm -f microservice_rgpd_bdd`
