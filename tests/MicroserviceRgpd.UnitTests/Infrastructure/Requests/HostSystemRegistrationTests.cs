using MicroserviceRgpd.Core.Requests;
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
  /// pour appeler, et derrière lui l'adaptateur HTTP.
  /// </summary>
  [Fact]
  public void ProvidesTheHostSystemByItsPort()
  {
    using var services = Registered(timeout: null);
    using var scope = services.CreateScope();

    scope.ServiceProvider.GetRequiredService<IHostSystem>().ShouldBeOfType<HostSystemByChannel>();

    // Le destinataire que le dispatcher demande : sa résolution est l'assertion.
    scope.ServiceProvider.GetRequiredService<HttpHostSystem>();
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

    return new ServiceCollection()
      .AddSingleton(TimeProvider.System)
      .AddHostSystem(configuration)
      .BuildServiceProvider();
  }
}
