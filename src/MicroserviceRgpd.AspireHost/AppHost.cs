var builder = DistributedApplication.CreateBuilder(args);

// Le drapeau qui commande l'existence du moteur LLM. Sous Aspire, c'est ici — et **seulement ici** —
// qu'il se décide : l'AppHost le propage au service .NET et au sidecar, ce qui est le seul mécanisme
// qui empêche les deux moitiés de diverger, celui-là même qui tient déjà l'écart des deux échéances.
// Hors Aspire, chaque processus lit sa propre configuration ; la divergence y est possible mais
// inoffensive, et aucun garde d'accord n'est ajouté pour elle.
var llmIsOn = FlagIsOn("Llm:Enabled");

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
// Le lexique vit dans ce même sidecar : celui-ci est là dans tous les cas, moteur génératif ou non.
var sidecar = builder.AddUvicornApp("qualification-sidecar", "../sidecar", "qualification_sidecar.app:app")
  .WithUv()
  .WithHttpHealthCheck("/health")
  .WithEnvironment("QUALIFICATION_LLM_ENABLED", AsEnvironmentValue(llmIsOn));

// Add the web project with the database connection
// La référence au sidecar est ce qui fait résoudre « http://qualification-sidecar » : le service
// .NET ne connaît que ce nom, jamais un hôte ni un port.
var web = builder.AddProject<Projects.MicroserviceRgpd_Web>("web")
  .WithReference(cleanArchDb)
  .WithReference(sidecar)
  .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName)
  // Le même drapeau que celui posé au sidecar, écrit sous la clé que le service lit. Le nom suit la
  // sous-section propre au moteur LLM côté service : ses réglages y sont regroupés pour qu'éteindre
  // le moteur rende visiblement inertes les siens.
  .WithEnvironment("Qualification__Llm__Enabled", AsEnvironmentValue(llmIsOn))
  // Le lexique n'a aucun amont, donc aucune échéance imbriquée : la sienne ne répond qu'à une
  // propriété, ne jamais rallonger le temps de réponse du service. Elle est donc requise même moteur
  // éteint.
  .WithEnvironment("Qualification__LexiconDeadlineSeconds", RequiredSetting("Qualification:LexiconDeadlineSeconds"))
  .WaitFor(cleanArchDb)
  .WaitFor(sidecar);

// Éteint, rien de ce qui suit n'existe : ni serveur de modèles, ni modèle de plusieurs gigaoctets à
// tirer, ni un seul réglage LLM propagé. C'est ce qui fait tenir la promesse « la pile entière
// démarre d'une seule commande » sur un poste sans carte graphique. Aucune annonce n'est émise : le
// risque assumé est qu'une coquille dans le nom de la clé éteigne le moteur sans un mot.
if (llmIsOn)
{
  // Le serveur de modèles du moteur LLM. Son volume de modèles est nommé et persistant : le modèle
  // pèse plusieurs gigaoctets, et le retélécharger à chaque démarrage ferait du « la pile entière
  // démarre d'une seule commande » une promesse que personne ne tiendrait deux fois.
  // Sans le GPU, Ollama replie l'inférence sur le processeur — la qualification passe alors de
  // quelques secondes à plusieurs dizaines, et un moteur qu'on n'a pas la patience d'attendre est un
  // moteur qu'on cesse d'essayer. Le prérequis est le NVIDIA Container Toolkit côté Docker ; à
  // défaut, le conteneur refuse de démarrer plutôt que de retomber silencieusement sur le processeur.
  var ollama = builder.AddOllama("ollama")
    .WithContainerName("microservice_rgpd_ollama")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithGPUSupport()
    .WithDataVolume("microservice_rgpd_ollama_models");

  // Le modèle est tiré par Aspire au démarrage, et non par une procédure manuelle à côté.
  var qualificationModel = ollama.AddModel("qualification-model", RequiredSetting("Llm:Model"));

  sidecar
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
    // Le client HTTP .NET vers le sidecar lit `Llm:CallerDeadlineSeconds` **ici même**, quelques
    // lignes plus bas, et non un second chiffre à tenir en accord de tête.
    .WithEnvironment("QUALIFICATION_LLM_DEADLINE_SECONDS", RequiredSetting("Llm:SidecarDeadlineSeconds"))
    .WithEnvironment("QUALIFICATION_LLM_CALLER_DEADLINE_SECONDS", RequiredSetting("Llm:CallerDeadlineSeconds"))
    // Le sidecar attend que le modèle soit tiré : sans cela, le premier appel manuel après un
    // démarrage à froid tomberait sur un `502` qui ne dirait rien d'autre que « pas encore prêt ».
    // Moteur éteint, cette attente disparaît avec le modèle qu'elle attendait.
    .WaitFor(qualificationModel);

  // L'échéance du client LLM est **le même chiffre** que celui donné au sidecar comme échéance de
  // son appelant : les faire lire au même réglage est ce qui empêche les deux moitiés de l'inégalité
  // de diverger en silence. Le sidecar refuse de démarrer si l'ordre strict n'est pas tenu — encore
  // faut-il qu'il parle du chiffre réellement appliqué ici.
  web.WithEnvironment("Qualification__Llm__DeadlineSeconds", RequiredSetting("Llm:CallerDeadlineSeconds"));
}

// Le mock du système hôte, pour que les adresses collées dans `/parametrage` soient joignables. Le
// service ne les appelle pas (ADR-0016) : aucune référence ne le relie au mock, qui n'est qu'une
// cible pour la démonstration. Éteint par défaut, et alors la pile est exactement celle d'avant lui.
// Le port est fixe, et c'est tout son intérêt : une URL saisie dans le Paramétrage reste valable
// d'un lancement à l'autre, là où un port attribué par Aspire la rendrait caduque au suivant.
// Le certificat de développement est écarté : Aspire le poserait d'office sur uvicorn, et
// `http://localhost:5199` rendrait alors une réponse vide. L'API est encore marquée expérimentale ;
// le mock n'étant que local, le risque qu'elle bouge est assumé.
if (FlagIsOn("MockHost:Enabled"))
{
#pragma warning disable ASPIRECERTIFICATES001
  builder.AddUvicornApp("mock-host", "../mock-host", "mock_host.app:app")
    .WithUv()
    .WithoutHttpsCertificate()
    .WithEndpoint("http", endpoint => endpoint.Port = 5199)
    .WithHttpHealthCheck("/health");
#pragma warning restore ASPIRECERTIFICATES001
}

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

// Un drapeau est le **seul** réglage de sa section à disposer d'un repli, et ce repli est le choix
// sûr : une pile qui ne dit rien démarre sans serveur de modèles — donc sans GPU et sans
// téléchargement — et sans mock. Une valeur qui n'est ni « true » ni « false » arrête le démarrage
// plutôt que d'éteindre : lue comme un « non », elle ferait passer une coquille pour une décision —
// même traitement que réservent déjà au drapeau LLM le service .NET et le sidecar.
bool FlagIsOn(string key)
{
  var raw = builder.Configuration[key];

  if (string.IsNullOrEmpty(raw))
  {
    return false;
  }

  if (!bool.TryParse(raw, out var enabled))
  {
    throw new InvalidOperationException(
      $"Le réglage « {key} » de l'AppHost vaut « {raw} », qui n'est ni « true » ni « false ».");
  }

  return enabled;
}

// Ce qu'un drapeau devient une fois écrit dans une variable d'environnement : le service .NET et le
// sidecar lisent tous deux ces deux mots-là.
static string AsEnvironmentValue(bool on) => on ? "true" : "false";
