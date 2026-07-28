var builder = DistributedApplication.CreateBuilder(args);

// Add PostgreSQL container
var postgres = builder.AddPostgres("postgres")
  .WithContainerName("microservice_rgpd_bdd")
  .WithLifetime(ContainerLifetime.Persistent)
  .WithDataVolume();

// Add the database
var cleanArchDb = postgres.AddDatabase("cleanarchitecture");

// Le sidecar Python, où vivent les moteurs de qualification. Il entre ici dès sa naissance pour
// que la pile entière démarre d'une seule commande : un moteur qu'on ne peut pas démontrer sans
// une procédure à part finit par n'être démontré par personne.
// `uv sync` est joué avant le démarrage — l'environnement virtuel n'est donc pas un prérequis
// manuel, et la version des dépendances est celle du fichier de verrouillage versionné.
var sidecar = builder.AddUvicornApp("qualification-sidecar", "../sidecar", "qualification_sidecar.app:app")
  .WithUv()
  .WithHttpHealthCheck("/health");

// Add the web project with the database connection
// La référence au sidecar est ce qui fait résoudre « http://qualification-sidecar » : le service
// .NET ne connaît que ce nom, jamais un hôte ni un port.
builder.AddProject<Projects.MicroserviceRgpd_Web>("web")
  .WithReference(cleanArchDb)
  .WithReference(sidecar)
  .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName)
  .WaitFor(cleanArchDb)
  .WaitFor(sidecar);

builder
  .Build()
  .Run();
