using System.Net;
using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.Adapters;
using MicroserviceRgpd.Infrastructure.Casework.Adapters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Casework;

/// <summary>
/// Le câblage des appels d'<c>Adapter</c>, et surtout le <b>refus de démarrer sans secret</b>.
/// </summary>
/// <remarks>
/// C'est ici que se vérifie qu'aucun mode « sans » ne survit à l'intégration : ni repli, ni valeur
/// d'usine, ni drapeau qui l'éteindrait. Un tel drapeau serait une case à laisser pourrir, et le
/// jour où quelqu'un la coche « le temps de tester », la route la plus dangereuse de l'application
/// du client est ouverte à qui l'atteint.
/// </remarks>
public class AdapterRegistrationTests
{
  /// <summary>Le secret absent arrête le démarrage, plutôt que d'ouvrir un mode « sans ».</summary>
  [Fact]
  public void RefusesToStartWithoutASharedSecret()
  {
    var nothingConfigured = new ConfigurationBuilder().Build();

    Should.Throw<ArgumentException>(() => new ServiceCollection().AddAdapterCalls(nothingConfigured));
  }

  /// <summary>
  /// Une clé présente mais vide est le même vide, et elle arrête le démarrage pareillement : une
  /// chaîne vide comparée en temps constant serait un secret que n'importe qui devine.
  /// </summary>
  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  public void RefusesToStartOnASecretThatIsOnlyThereInName(string secret)
  {
    Should.Throw<ArgumentException>(() => new ServiceCollection().AddAdapterCalls(Configured(secret)));
  }

  /// <summary>Le secret posé, le service sait appeler — et signaler les désaccords.</summary>
  [Fact]
  public void WiresTheCallsAndTheDisagreementsOnceTheSecretIsThere()
  {
    using var services = new ServiceCollection()
      .AddLogging()
      .AddAdapterCalls(Configured("un-secret-partage"))
      .BuildServiceProvider();

    services.GetRequiredService<IAdapterCalls>().ShouldBeOfType<HttpAdapterCalls>();
    services.GetRequiredService<IAdapterDisagreements>().ShouldBeOfType<AdapterDisagreements>();
  }

  /// <summary>
  /// <b>Le signalement est un singleton</b>, et c'est ce qui lui donne le grain du déploiement :
  /// une instance par dossier ne se souviendrait de rien, et la panne unique serait criée N fois.
  /// </summary>
  [Fact]
  public void KeepsOneSetOfDisagreementsForTheWholeDeployment()
  {
    using var services = new ServiceCollection()
      .AddLogging()
      .AddAdapterCalls(Configured("un-secret-partage"))
      .BuildServiceProvider();

    using var first = services.CreateScope();
    using var second = services.CreateScope();

    first.ServiceProvider.GetRequiredService<IAdapterDisagreements>()
      .ShouldBeSameAs(second.ServiceProvider.GetRequiredService<IAdapterDisagreements>());
  }

  /// <summary>
  /// <b>Le service ne relance jamais tout seul</b>, et c'est vrai du client réellement câblé — pas
  /// seulement de celui qu'un test construit à la main. Le pipeline standard du dépôt reprendrait
  /// jusqu'à trois fois un <c>POST</c> tombé sur un <c>5xx</c> ; ce test le remet en place, puis
  /// vérifie qu'il a bien été remplacé.
  /// </summary>
  /// <remarks>
  /// L'enjeu déborde ce lot : au lot où <c>Erase</c> arrive, une reprise serait une destruction
  /// rejouée trois fois chez le client, et le contrat lui promet le contraire.
  /// </remarks>
  [Fact]
  public async Task AsksOnceAndNeverRetriesOnTheWiredUpClient()
  {
    var adapter = AdapterDouble.RespondingWith(HttpStatusCode.InternalServerError);

    var services = new ServiceCollection().AddLogging();

    // Le défaut du dépôt, tel que ServiceDefaults le pose sur tous les clients : sans cette ligne,
    // le test passerait sans rien prouver.
    services.ConfigureHttpClientDefaults(http => http.AddStandardResilienceHandler());

    services.AddAdapterCalls(Configured("un-secret-partage"));
    services.AddHttpClient<IAdapterCalls, HttpAdapterCalls>()
      .ConfigurePrimaryHttpMessageHandler(() => adapter);

    await using var provider = services.BuildServiceProvider();

    await Should.ThrowAsync<AdapterFailure>(() => provider.GetRequiredService<IAdapterCalls>()
      .AskAsync<Found>(ALocate()));

    adapter.Asked.Count.ShouldBe(1);
  }

  private static AdapterCall ALocate()
  {
    return new AdapterCall(
      AdapterAddress.From("https://brocanto.example.fr/rgpd"),
      DeclaredSystemId.From("boutique"),
      Capability.Locate,
      [Designation.Of(DesignationKind.Email, "helene.petit@example.fr")]);
  }

  /// <summary>Ce qu'une capacité rend — ici, rien qui n'arrive jamais.</summary>
  private sealed record Found(int Count);

  private static IConfiguration Configured(string secret)
  {
    return new ConfigurationBuilder()
      .AddInMemoryCollection(new Dictionary<string, string?> { [AdapterServiceExtensions.SecretKey] = secret })
      .Build();
  }
}
