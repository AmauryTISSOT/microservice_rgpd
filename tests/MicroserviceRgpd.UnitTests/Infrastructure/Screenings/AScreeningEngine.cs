using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.Infrastructure.Screenings;
using MicroserviceRgpd.TestDoubles.Ollama;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Screenings;

/// <summary>
/// Le moteur de détection, tel que le service l'obtient — <b>par son port et par son câblage</b>.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Aucun test ne connaît les classes internes des moteurs.</b> Ils passent tous par
/// <see cref="IScreeningEngine"/>, ou par le câblage lui-même quand c'est un refus au démarrage qu'on
/// éprouve — jamais par l'implémentation. Le lexique, la découpe des
/// identifiants, les cinq règles et la régression d'A2 n'ont ainsi aucun test qui les épingle : un
/// moteur qui changerait de forme n'invaliderait rien de ce dossier — c'est la couture de
/// réversibilité d'ADR-0004 prise au mot.
/// </para>
/// <para>
/// <b>Le câblage reçoit une configuration</b>, parce que c'est elle qui choisit le moteur
/// (ADR-0025). Sans rien dire, c'est le lexique.
/// </para>
/// </remarks>
internal static class AScreeningEngine
{
  /// <summary>L'adresse sous laquelle le double d'Ollama répond. Aucun réseau n'est jamais touché.</summary>
  private const string OllamaAddress = "http://ollama";

  /// <summary>Le moteur que le service câble quand la configuration ne dit rien : le lexique.</summary>
  internal static IScreeningEngine Wired()
  {
    return Wired(new ConfigurationBuilder().Build());
  }

  /// <summary>
  /// Le moteur que le service câble sous cette configuration, résolu par son port — et, s'il en
  /// appelle un, branché sur ce double d'Ollama.
  /// </summary>
  internal static IScreeningEngine Wired(
    IConfiguration configuration,
    HttpMessageHandler? ollama = null,
    ILoggerProvider? logs = null)
  {
    var services = new ServiceCollection().AddScreeningEngine(configuration);

    if (logs is not null)
    {
      services.AddLogging(logging => logging.AddProvider(logs));
    }

    if (ollama is not null)
    {
      services
        .AddHttpClient(ScreeningEngineServiceExtensions.OllamaClientName)
        .ConfigurePrimaryHttpMessageHandler(() => ollama)
        .SetHandlerLifetime(Timeout.InfiniteTimeSpan);
    }

    return services.BuildServiceProvider().GetRequiredService<IScreeningEngine>();
  }

  /// <summary>Le moteur A2, drapeau allumé, branché sur ce double d'Ollama.</summary>
  internal static IScreeningEngine WiredToA2(OllamaDouble ollama)
  {
    return Wired(Configuration(A2On()), ollama);
  }

  /// <summary>
  /// Le moteur A2 branché sur ce double d'Ollama, journal capturé, sous l'échéance donnée — courte
  /// quand c'est elle qu'on éprouve.
  /// </summary>
  internal static IScreeningEngine WiredToA2(OllamaDouble ollama, RecordedLogs logs, string deadlineSeconds = "30")
  {
    var settings = A2On();
    settings[ScreeningEngineServiceExtensions.EmbeddingsDeadlineKey] = deadlineSeconds;

    return Wired(Configuration(settings), ollama, logs);
  }

  /// <summary>Les réglages complets d'un déploiement qui allume A2.</summary>
  internal static Dictionary<string, string?> A2On()
  {
    return new Dictionary<string, string?>
    {
      [ScreeningEngineServiceExtensions.EmbeddingsEnabledKey] = "true",
      [ScreeningEngineServiceExtensions.OllamaBaseAddressKey] = OllamaAddress,
      [ScreeningEngineServiceExtensions.EmbeddingsDeadlineKey] = "30",
    };
  }

  internal static IConfiguration Configuration(Dictionary<string, string?> settings)
  {
    return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
  }
}
