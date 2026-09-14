using System.Globalization;
using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.Infrastructure.Screenings.Embeddings;
using Polly;

namespace MicroserviceRgpd.Infrastructure.Screenings;

/// <summary>
/// Branche le moteur de détection : <b>l'un ou l'autre</b> des deux moteurs, choisi au démarrage par
/// un drapeau (ADR-0025).
/// </summary>
public static class ScreeningEngineServiceExtensions
{
  /// <summary>La section de configuration propre au moteur A2.</summary>
  private const string Section = "Screening:Embeddings";

  /// <summary>
  /// Le drapeau qui choisit le moteur. <b>Absent, il vaut « éteint »</b>, et c'est le lexique qui
  /// détecte. Publique parce que l'AppHost la pose : deux noms tenus en accord de tête finiraient par
  /// diverger en silence.
  /// </summary>
  public const string EmbeddingsEnabledKey = $"{Section}:Enabled";

  /// <summary>L'adresse d'Ollama, lue seulement drapeau allumé.</summary>
  public const string OllamaBaseAddressKey = $"{Section}:OllamaBaseAddress";

  /// <summary>
  /// L'échéance d'<b>un</b> appel à Ollama, en secondes, lue seulement drapeau allumé. Arbitraire et
  /// assumée, comme les autres échéances du dépôt : elle n'a pas été mesurée.
  /// </summary>
  public const string EmbeddingsDeadlineKey = $"{Section}:DeadlineSeconds";

  /// <summary>
  /// Le nom du client HTTP vers Ollama. Public pour qu'un hôte de test lui substitue son transport —
  /// et rien d'autre : l'adresse, l'échéance et le pipeline restent ceux du câblage.
  /// </summary>
  public const string OllamaClientName = "screening-ollama";

  /// <summary>
  /// Enregistre le moteur <b>par son port</b> : ce qui le consomme ne doit pouvoir apprendre ni
  /// lequel des deux il est, ni qu'il vit dans cet assemblage.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Aucun composant ne choisit à l'exécution.</b> Le drapeau est lu ici, une fois, et un seul
  /// moteur existe dans le conteneur : deux rapports d'une même pile viennent du même moteur, et une
  /// panne d'Ollama n'est jamais rattrapée par le lexique dans le dos de l'<c>Operator</c>.
  /// </para>
  /// <para>
  /// <b>Un seul exemplaire pour tout le service</b>, dans les deux cas. Les lexiques gelés comme
  /// l'artefact A2 sont chargés une fois et ne changent plus ; l'artefact l'est <b>dès
  /// l'enregistrement</b>, pour qu'un artefact abîmé arrête le démarrage plutôt que le premier dépôt.
  /// </para>
  /// </remarks>
  /// <exception cref="ArgumentException">Le drapeau n'est ni « true » ni « false », ou A2 allumé manque de son adresse ou de son échéance.</exception>
  /// <exception cref="InvalidOperationException">A2 allumé, l'artefact embarqué est incohérent.</exception>
  public static IServiceCollection AddScreeningEngine(
    this IServiceCollection services,
    IConfiguration configuration)
  {
    return services.AddScreeningEngine(configuration, A2Artefact.OpenEmbedded);
  }

  /// <summary>
  /// Le même câblage, sur un artefact dont les fichiers s'ouvrent autrement — le seul moyen
  /// d'éprouver qu'un artefact abîmé arrête le démarrage.
  /// </summary>
  internal static IServiceCollection AddScreeningEngine(
    this IServiceCollection services,
    IConfiguration configuration,
    Func<string, Stream> artefactFile)
  {
    ArgumentNullException.ThrowIfNull(configuration);

    if (!EmbeddingsAreOn(configuration))
    {
      services.AddSingleton<IScreeningEngine, RulesAndLexiconScreeningEngine>();

      return services;
    }

    var address = configuration[OllamaBaseAddressKey];
    Guard.Against.NullOrEmpty(address, OllamaBaseAddressKey,
      $"A2 est allumé sans adresse d'Ollama : renseigner {OllamaBaseAddressKey}.");

    var deadline = Deadline(configuration);
    var artefact = A2Artefact.Load(artefactFile);

    // Le pipeline standard hérité des réglages du dépôt est REMPLACÉ, comme pour les moteurs de
    // qualification : ses reprises rejoueraient un lot entier sur un encodeur déjà lent, et ses trente
    // secondes contrediraient l'échéance réglée ici. Une seule échéance, par appel, et rien d'autre.
#pragma warning disable EXTEXP0001
    services
      .AddHttpClient(OllamaClientName, client =>
      {
        client.BaseAddress = new Uri(address, UriKind.Absolute);

        // L'échéance appartient au pipeline, et à lui seul : les cent secondes par défaut de
        // HttpClient couperaient un lot avant elle, sous une annulation muette.
        client.Timeout = Timeout.InfiniteTimeSpan;
      })
      .RemoveAllResilienceHandlers()
      .AddResilienceHandler(OllamaClientName, pipeline => pipeline.AddTimeout(deadline));
#pragma warning restore EXTEXP0001

    services.AddSingleton<IScreeningEngine>(provider =>
      new A2ScreeningEngine(provider.GetRequiredService<IHttpClientFactory>(), artefact));

    return services;
  }

  /// <summary>
  /// Dit si A2 détecte. <b>Absent de la configuration, il ne détecte pas</b> — le repli sûr : la pile
  /// démarre sans GPU, sans téléchargement et sans Ollama.
  /// </summary>
  /// <remarks>
  /// Une valeur qui n'est ni « true » ni « false » arrête le démarrage plutôt que d'éteindre : lue
  /// comme un « non », elle ferait passer une faute de frappe pour une décision.
  /// </remarks>
  private static bool EmbeddingsAreOn(IConfiguration configuration)
  {
    var raw = configuration[EmbeddingsEnabledKey];

    if (string.IsNullOrEmpty(raw))
    {
      return false;
    }

    if (!bool.TryParse(raw, out var enabled))
    {
      throw new ArgumentException(
        $"Le réglage {EmbeddingsEnabledKey} vaut « {raw} », qui n'est ni « true » ni « false ».",
        EmbeddingsEnabledKey);
    }

    return enabled;
  }

  /// <summary>Lit l'échéance d'A2, ou refuse — une valeur absente n'est pas une valeur par défaut.</summary>
  private static TimeSpan Deadline(IConfiguration configuration)
  {
    var raw = configuration[EmbeddingsDeadlineKey];

    Guard.Against.NullOrEmpty(raw, EmbeddingsDeadlineKey,
      $"A2 est allumé sans échéance : renseigner {EmbeddingsDeadlineKey}.");

    if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) || seconds <= 0)
    {
      throw new ArgumentException(
        $"Le réglage {EmbeddingsDeadlineKey} vaut « {raw} », qui n'est pas une durée en secondes strictement positive.",
        EmbeddingsDeadlineKey);
    }

    return TimeSpan.FromSeconds(seconds);
  }
}
