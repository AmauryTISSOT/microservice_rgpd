var builder = DistributedApplication.CreateBuilder(args);

// Add PostgreSQL container
var postgres = builder.AddPostgres("postgres")
  .WithContainerName("microservice_rgpd_bdd")
  .WithLifetime(ContainerLifetime.Persistent)
  .WithDataVolume();

// Add the database
var cleanArchDb = postgres.AddDatabase("cleanarchitecture");

// Le serveur de modèles du moteur LLM. Son volume de modèles est nommé et persistant : le modèle
// pèse plusieurs gigaoctets, et le retélécharger à chaque démarrage ferait du « la pile entière
// démarre d'une seule commande » une promesse que personne ne tiendrait deux fois.
var ollama = builder.AddOllama("ollama")
  .WithContainerName("microservice_rgpd_ollama")
  .WithLifetime(ContainerLifetime.Persistent)
  .WithDataVolume("microservice_rgpd_ollama_modeles");

// Le modèle est tiré par Aspire au démarrage, et non par une procédure manuelle à côté.
var qualificationModel = ollama.AddModel("qualification-model", RequiredSetting("Llm:Model"));

// Le sidecar Python, où vivent les moteurs de qualification. Il entre ici dès sa naissance pour
// que la pile entière démarre d'une seule commande : un moteur qu'on ne peut pas démontrer sans
// une procédure à part finit par n'être démontré par personne.
// `uv sync` est joué avant le démarrage — l'environnement virtuel n'est donc pas un prérequis
// manuel, et la version des dépendances est celle du fichier de verrouillage versionné.
builder.AddUvicornApp("qualification-sidecar", "../sidecar", "qualification_sidecar.app:app")
  .WithUv()
  .WithHttpHealthCheck("/health")
  // Le point de bascule vers un autre fournisseur compatible OpenAI tient en ces deux lignes :
  // une adresse et un nom de modèle. Aucune logique de qualification n'en dépend.
  .WithEnvironment(
    "QUALIFICATION_LLM_BASE_URL",
    ReferenceExpression.Create($"{ollama.Resource.PrimaryEndpoint}/v1"))
  .WithEnvironment("QUALIFICATION_LLM_MODEL", RequiredSetting("Llm:Model"))
  // Ollama ignore la clé, mais le protocole en exige une. Chez un fournisseur qui la vérifie, elle
  // relèvera des secrets utilisateur, jamais d'un fichier versionné.
  .WithEnvironment("QUALIFICATION_LLM_API_KEY", RequiredSetting("Llm:ApiKey"))
  // Température nulle et seed fixe : rejouer une requête est une opération nulle, ce qui retire
  // toute raison de reprendre — et le sidecar ne reprend jamais.
  .WithEnvironment("QUALIFICATION_LLM_TEMPERATURE", RequiredSetting("Llm:Temperature"))
  .WithEnvironment("QUALIFICATION_LLM_SEED", RequiredSetting("Llm:Seed"))
  // Les deux échéances voyagent ensemble parce que c'est leur *écart* qui compte : le sidecar
  // abandonne le premier, et la lenteur arrive donc nommée jusqu'à .NET plutôt qu'anonyme. Le
  // sidecar refuse de démarrer si l'inégalité stricte n'est pas tenue.
  .WithEnvironment("QUALIFICATION_LLM_DEADLINE_SECONDS", RequiredSetting("Llm:SidecarDeadlineSeconds"))
  .WithEnvironment("QUALIFICATION_LLM_CALLER_DEADLINE_SECONDS", RequiredSetting("Llm:CallerDeadlineSeconds"))
  // Le sidecar attend que le modèle soit tiré : sans cela, le premier appel manuel après un
  // démarrage à froid tomberait sur un `502` qui ne dirait rien d'autre que « pas encore prêt ».
  .WaitFor(qualificationModel);

// Add the web project with the database connection
builder.AddProject<Projects.MicroserviceRgpd_Web>("web")
  .WithReference(cleanArchDb)
  .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName)
  .WaitFor(cleanArchDb);

builder
  .Build()
  .Run();

// Les chiffres du moteur — modèle, échéances, seed, température — sont **arbitraires et assumés**,
// et devront être révisés le jour où le modèle sera mesuré. Ils vivent donc en configuration ; une
// valeur manquante arrête le démarrage plutôt que de laisser une chaîne vide voyager jusqu'au
// sidecar, où elle serait beaucoup plus difficile à rattacher à sa cause.
string RequiredSetting(string key) =>
  builder.Configuration[key]
  ?? throw new InvalidOperationException(
    $"Le réglage « {key} » est absent de la configuration de l'AppHost : le moteur LLM ne se configure pas tout seul.");
