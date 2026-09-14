var builder = DistributedApplication.CreateBuilder(args);

// Le drapeau qui commande l'existence du moteur LLM. Sous Aspire, c'est ici — et **seulement ici** —
// qu'il se décide : l'AppHost le propage au service .NET et au sidecar, ce qui est le seul mécanisme
// qui empêche les deux moitiés de diverger, celui-là même qui tient déjà l'écart des deux échéances.
// Hors Aspire, chaque processus lit sa propre configuration ; la divergence y est possible mais
// inoffensive, et aucun garde d'accord n'est ajouté pour elle.
var llmIsOn = FlagIsOn("Llm:Enabled");

// Le drapeau de la détection par A2 (ADR-0025), **indépendant** du précédent : l'un n'allume pas
// l'autre. Sous Aspire, c'est lui aussi ici qu'il se décide, et le service le lit tel quel.
var embeddingsAreOn = FlagIsOn("Screening:Embeddings:Enabled");

// Ce que ces deux drapeaux font naître côté Ollama. La décision vit dans `OllamaWiring.cs`, le seul
// morceau de ce câblage qu'un test éprouve sans construire d'hôte.
var ollamaWiring = OllamaWiring.For(llmIsOn, embeddingsAreOn);

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
  // Posé dans les deux états, comme celui du LLM : éteint ici, le service ne retombe pas sur la
  // valeur de son propre fichier, qui pourrait dire autre chose.
  .WithEnvironment("Screening__Embeddings__Enabled", AsEnvironmentValue(embeddingsAreOn))
  .WaitFor(cleanArchDb)
  .WaitFor(sidecar);

// Les deux éteints, rien de ce qui suit n'existe : ni serveur de modèles, ni modèle à tirer, ni un
// seul réglage de moteur propagé. C'est ce qui fait tenir la promesse « la pile entière démarre d'une
// seule commande » sur un poste sans carte graphique. Aucune annonce n'est émise : le risque assumé
// est qu'une coquille dans le nom d'une clé éteigne un moteur sans un mot.
if (ollamaWiring.Exists)
{
  // Le serveur de modèles, **un seul** pour les deux moteurs. Son volume de modèles est nommé et
  // persistant : le retélécharger à chaque démarrage ferait du « la pile entière démarre d'une seule
  // commande » une promesse que personne ne tiendrait deux fois.
  var ollama = builder.AddOllama("ollama")
    .WithContainerName("microservice_rgpd_ollama")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume("microservice_rgpd_ollama_models");

  // Sans le GPU, Ollama replie l'inférence générative sur le processeur — la qualification passe
  // alors de quelques secondes à plusieurs dizaines, et un moteur qu'on n'a pas la patience
  // d'attendre est un moteur qu'on cesse d'essayer. Le prérequis est le NVIDIA Container Toolkit côté
  // Docker ; à défaut, le conteneur refuse de démarrer plutôt que de retomber silencieusement sur le
  // processeur. L'encodeur d'A2, lui, tourne sur le processeur : la détection seule n'exige rien.
  if (ollamaWiring.RequiresGpu)
  {
    ollama.WithGPUSupport();
  }

  if (ollamaWiring.PullsQualificationModel)
  {
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

  if (ollamaWiring.PullsEncoder)
  {
    // L'encodeur est celui que nomme le manifest de l'artefact embarqué par le service, lu dans ce
    // fichier même : aucun réglage de l'AppHost ne peut le désaccorder du modèle mesuré.
    var encoder = ollama.AddModel(
      "screening-encoder",
      EncoderModel.TagFrom(Path.Combine(
        builder.AppHostDirectory,
        "../MicroserviceRgpd.Infrastructure/Screenings/Embeddings/Artefact/manifest.json")));

    web
      // L'adresse vient de la ressource, jamais d'un hôte ou d'un port écrit à la main.
      .WithEnvironment("Screening__Embeddings__OllamaBaseAddress", ollama.Resource.PrimaryEndpoint)
      .WithEnvironment("Screening__Embeddings__DeadlineSeconds", RequiredSetting("Screening:Embeddings:DeadlineSeconds"))
      // Le service vérifie l'encodeur au premier dépôt : sans cette attente, un démarrage à froid
      // ferait échouer la détection sur un modèle encore en cours de téléchargement.
      .WaitFor(encoder);
  }
}

// Le mock du système hôte, pour que les adresses collées dans `/parametrage` soient joignables. Le
// service les appelle quand une demande est exécutée (ADR-0026), mais aucune référence ne le relie
// au mock, qui n'est qu'une cible pour la démonstration. Éteint par défaut, et alors la pile est exactement celle d'avant lui.
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

// Les chiffres des moteurs — modèle, échéances, seed, température — sont **arbitraires et assumés**,
// et devront être révisés le jour où le modèle sera mesuré. Ils vivent donc en configuration ; une
// valeur manquante arrête le démarrage plutôt que de laisser une chaîne vide voyager jusqu'au
// sidecar, où elle serait beaucoup plus difficile à rattacher à sa cause.
string RequiredSetting(string key) =>
  builder.Configuration[key]
  ?? throw new InvalidOperationException(
    $"Le réglage « {key} » est absent de la configuration de l'AppHost : un moteur ne se configure pas tout seul.");

// Le repli et le refus d'un drapeau sont décrits sur `AppHostFlag`.
bool FlagIsOn(string key) => AppHostFlag.IsOn(key, builder.Configuration[key]);

// Ce qu'un drapeau devient une fois écrit dans une variable d'environnement : le service .NET et le
// sidecar lisent tous deux ces deux mots-là.
static string AsEnvironmentValue(bool on) => on ? "true" : "false";
