using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.Infrastructure.Configuration;
using MicroserviceRgpd.Infrastructure.Requests;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Requests;

/// <summary>
/// Le câblage du système hôte, et d'abord son délai : <c>HostSystem:TimeoutSeconds</c>, 30 par
/// défaut, et un démarrage refusé sur toute valeur qui n'est pas un entier strictement positif
/// (ADR-0026).
/// </summary>
public class HostSystemRegistrationTests
{
  [Fact]
  public void WaitsThirtySecondsWhenNothingIsConfigured()
  {
    using var services = Registered(timeout: null);

    ClientOf(services).Timeout.ShouldBe(TimeSpan.FromSeconds(30));
  }

  [Fact]
  public void WaitsTheConfiguredNumberOfSeconds()
  {
    using var services = Registered(timeout: "45");

    ClientOf(services).Timeout.ShouldBe(TimeSpan.FromSeconds(45));
  }

  /// <summary>
  /// <b>Le port rend le système hôte joint par le canal</b> : le seul endroit où le canal se filtre
  /// pour remettre un droit, et derrière lui les deux adaptateurs.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>La portée et le conteneur se ferment en asynchrone</b> : la connexion au broker ne se
  /// ferme pas autrement (RabbitMQ.Client 7.x), et le conteneur refuse un <c>Dispose</c> synchrone
  /// sur un singleton qui ne sait que <c>DisposeAsync</c>. L'hôte ASP.NET, lui, ferme déjà ainsi.
  /// </remarks>
  [Fact]
  public async Task ProvidesTheHostSystemByItsPort()
  {
    await using var services = Registered(timeout: null);
    await using var scope = services.CreateAsyncScope();

    scope.ServiceProvider.GetRequiredService<IHostSystem>().ShouldBeOfType<HostSystemByChannel>();

    // Les deux destinataires que le dispatcher demande : leur résolution est l'assertion.
    scope.ServiceProvider.GetRequiredService<HttpHostSystem>();
    scope.ServiceProvider.GetRequiredService<RabbitMqHostSystem>();
  }

  /// <summary>
  /// ⚠️ <b>La connexion au broker est un singleton</b> : une seule, partagée par toutes les
  /// exécutions — deux portées voient la même (ADR-0028).
  /// </summary>
  [Fact]
  public async Task SharesASingleBrokerConnectionAcrossEveryScope()
  {
    await using var services = Registered(timeout: null);
    await using var first = services.CreateAsyncScope();
    await using var second = services.CreateAsyncScope();

    first.ServiceProvider.GetRequiredService<IBrokerChannels>()
      .ShouldBeSameAs(second.ServiceProvider.GetRequiredService<IBrokerChannels>());
  }

  /// <summary>
  /// ⚠️ <b>Résoudre le système hôte n'ouvre aucune socket</b> : la connexion au broker est
  /// paresseuse, et un déploiement sans bus résout ses services comme les autres (ADR-0027).
  /// </summary>
  [Fact]
  public async Task OpensNothingWhenTheHostSystemIsResolved()
  {
    await using var services = Registered(timeout: null);
    await using var scope = services.CreateAsyncScope();

    // Aucun hôte n'est déclaré : si la résolution joignait un broker, elle n'aurait pas où aller.
    Should.NotThrow(() => scope.ServiceProvider.GetRequiredService<IHostSystem>());
  }

  /// <summary>
  /// <b>Une faute de configuration arrête le démarrage</b> : zéro, négatif, décimal, illisible ou vide,
  /// elle ne passe pas pour un délai nul ou arrondi.
  /// </summary>
  [Theory]
  [InlineData("0")]
  [InlineData("-5")]
  [InlineData("1.5")]
  [InlineData("30s")]
  [InlineData("trente")]
  [InlineData("")]
  [InlineData(" 30")]
  public void RefusesToStartWithATimeoutThatIsNotAStrictlyPositiveInteger(string raw)
  {
    var configuration = new ConfigurationBuilder()
      .AddInMemoryCollection(new Dictionary<string, string?> { ["HostSystem:TimeoutSeconds"] = raw })
      .Build();

    Should.Throw<ArgumentException>(() => new ServiceCollection().AddHostSystem(configuration))
      .Message.ShouldContain("HostSystem:TimeoutSeconds");
  }

  private static HttpClient ClientOf(ServiceProvider services) =>
    services.GetRequiredService<IHttpClientFactory>().CreateClient(HostSystemServiceExtensions.ClientName);

  private static ServiceProvider Registered(string? timeout)
  {
    var settings = new Dictionary<string, string?>();

    if (timeout is not null)
    {
      settings["HostSystem:TimeoutSeconds"] = timeout;
    }

    var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

    // ⚠️ Les deux ensemble : l'adaptateur RabbitMQ lit la connexion que le déploiement déclare, et
    // c'est AddBrokerConnection qui l'enregistre — comme dans le câblage réel.
    return new ServiceCollection()
      .AddSingleton(TimeProvider.System)
      .AddBrokerConnection(configuration)
      .AddHostSystem(configuration)
      .BuildServiceProvider();
  }
}
