using MicroserviceRgpd.Infrastructure.Screenings;
using MicroserviceRgpd.TestDoubles.Ollama;
using Microsoft.AspNetCore.TestHost;

namespace MicroserviceRgpd.FunctionalTests;

/// <summary>
/// Le service démarré <b>drapeau A2 allumé</b>, tel qu'un déploiement qui sert Ollama le démarre :
/// c'est le câblage réel qui choisit le moteur, et seul le transport vers Ollama est doublé.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>La doublure se pose sur le fil HTTP, pas sur le port.</b> Une doublure de
/// <c>IScreeningEngine</c> prouverait la présence de cette doublure, et rien du drapeau, de
/// l'artefact embarqué ni du moteur ; ici, tout cela est réel, et seul Ollama répond à la place
/// d'Ollama. Aucun test ne parle à un vrai Ollama.
/// </para>
/// <para>
/// Le drapeau est posé par <c>UseSetting</c>, sous la clé exacte que le câblage lit.
/// </para>
/// </remarks>
public sealed class A2OnWebApplicationFactory : CustomWebApplicationFactory<Program>
{
  /// <summary>Ollama, tel que le moteur A2 le voit.</summary>
  public OllamaDouble Ollama { get; } = new();

  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
    base.ConfigureWebHost(builder);

    builder.UseSetting(ScreeningEngineServiceExtensions.EmbeddingsEnabledKey, "true");
    builder.UseSetting(ScreeningEngineServiceExtensions.OllamaBaseAddressKey, "http://ollama");
    builder.UseSetting(ScreeningEngineServiceExtensions.EmbeddingsDeadlineKey, "30");

    builder.ConfigureTestServices(services => services
      .AddHttpClient(ScreeningEngineServiceExtensions.OllamaClientName)
      .ConfigurePrimaryHttpMessageHandler(() => Ollama)
      .SetHandlerLifetime(Timeout.InfiniteTimeSpan));
  }
}

/// <summary>
/// La collection de <see cref="A2OnWebApplicationFactory"/> : un hôte se bâtit une fois, et un hôte
/// drapeau allumé est une autre forme d'hôte.
/// </summary>
[CollectionDefinition(Name)]
public class A2OnWebCollection : ICollectionFixture<A2OnWebApplicationFactory>
{
  public const string Name = "Web avec le moteur A2";
}
