# Clean Architecture Aspire Host

This project uses .NET Aspire to orchestrate the application and its dependencies.

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
