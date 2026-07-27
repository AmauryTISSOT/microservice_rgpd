var builder = DistributedApplication.CreateBuilder(args);

// Add PostgreSQL container
var postgres = builder.AddPostgres("postgres")
  .WithContainerName("microservice_rgpd_bdd")
  .WithLifetime(ContainerLifetime.Persistent)
  .WithDataVolume();

// Add the database
var cleanArchDb = postgres.AddDatabase("cleanarchitecture");

// Add the web project with the database connection
builder.AddProject<Projects.MicroserviceRgpd_Web>("web")
  .WithReference(cleanArchDb)
  .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName)
  .WaitFor(cleanArchDb);

builder
  .Build()
  .Run();
