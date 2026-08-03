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

  private static IConfiguration Configured(string secret)
  {
    return new ConfigurationBuilder()
      .AddInMemoryCollection(new Dictionary<string, string?> { [AdapterServiceExtensions.SecretKey] = secret })
      .Build();
  }
}
