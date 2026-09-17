using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Configuration;

/// <summary>
/// Le câblage de la <b>connexion du déploiement au broker</b> : pas d'hôte, pas de connexion — un
/// état légal qui démarre ; un hôte présent avec un réglage aberrant arrête le démarrage, comme
/// <c>HostSystem:TimeoutSeconds</c> (ADR-0028).
/// </summary>
public class BrokerConnectionRegistrationTests
{
  private const string Host = "rabbitmq.brocanto.example.fr";

  /// <summary>
  /// <b>Un déploiement qui ne dit rien n'a pas de connexion</b>, et il démarre : c'est l'état d'un
  /// service installé avant son bus (ADR-0027).
  /// </summary>
  [Fact]
  public void HasNoConnectionWhenNoHostIsDeclared()
  {
    using var services = Registered([]);

    StateOf(services).Current.ShouldBeOfType<BrokerConnection.Absent>();
  }

  /// <summary>
  /// ⚠️ <b>Une clé vide ou faite d'espaces vaut une clé absente</b> : un hôte qui ne nomme aucune
  /// machine ne connecte rien. La règle est écrite au seul endroit qui rend l'état.
  /// </summary>
  [Theory]
  [InlineData("")]
  [InlineData(" ")]
  [InlineData("\t  ")]
  public void ReadsABlankHostAsNoConnectionAtAll(string blank)
  {
    using var services = Registered(new() { [RabbitMqOptions.HostNameKey] = blank });

    StateOf(services).Current.ShouldBeOfType<BrokerConnection.Absent>();
  }

  /// <summary>
  /// <b>Un hôte déclaré vaut connexion</b> — sans qu'aucun broker n'ait été joint : la décision se
  /// prend sur la seule présence de la clé, jamais sur un test réseau (ADR-0027).
  /// </summary>
  [Fact]
  public void HasAConnectionAsSoonAsAHostIsDeclared()
  {
    using var services = Registered(new() { [RabbitMqOptions.HostNameKey] = Host });

    StateOf(services).Current.ShouldBeOfType<BrokerConnection.Configured>();
  }

  /// <summary>
  /// <b>L'état est unique</b> : trois chemins le liront, et ils doivent lire la même réponse.
  /// </summary>
  [Fact]
  public void AnswersTheSameStateToEveryReader()
  {
    using var services = Registered(new() { [RabbitMqOptions.HostNameKey] = Host });

    StateOf(services).ShouldBeSameAs(StateOf(services));
  }

  /// <summary>
  /// <b>Les valeurs d'un déploiement qui ne dit que son hôte</b> : le port, le vhost et le délai de
  /// confirmation ont des valeurs par défaut, et <c>PublishTimeoutSeconds</c> vaut <b>10</b> — un
  /// ack local est affaire de millisecondes, et faire patienter l'<c>Operator</c> n'a pas de
  /// contrepartie.
  /// </summary>
  [Fact]
  public void FallsBackToTheDefaultsOfADeploymentThatOnlyNamesItsHost()
  {
    var options = OptionsOf(new() { [RabbitMqOptions.HostNameKey] = Host });

    options.HostName.ShouldBe(Host);
    options.Port.ShouldBe(5672);
    options.VirtualHost.ShouldBe("/");
    options.UserName.ShouldBeNull();
    options.Password.ShouldBeNull();
    options.UseTls.ShouldBeFalse();
    options.PublishTimeoutSeconds.ShouldBe(10);
    options.PublishTimeout.ShouldBe(TimeSpan.FromSeconds(10));
  }

  /// <summary>Ce que l'exploitant déclare est ce qui est lu, clé par clé.</summary>
  [Fact]
  public void ReadsEverythingTheDeploymentDeclares()
  {
    var options = OptionsOf(new()
    {
      [RabbitMqOptions.HostNameKey] = "  " + Host + "  ",
      [RabbitMqOptions.PortKey] = "5671",
      [RabbitMqOptions.VirtualHostKey] = "rgpd",
      [RabbitMqOptions.UserNameKey] = "microservice-rgpd",
      [RabbitMqOptions.PasswordKey] = "un-secret-de-deploiement",
      [RabbitMqOptions.UseTlsKey] = "true",
      [RabbitMqOptions.PublishTimeoutSecondsKey] = "3",
    });

    // ⚠️ L'hôte est rogné : les espaces autour d'un nom de machine ne nomment rien.
    options.HostName.ShouldBe(Host);
    options.Port.ShouldBe(5671);
    options.VirtualHost.ShouldBe("rgpd");
    options.UserName.ShouldBe("microservice-rgpd");
    options.Password.ShouldBe("un-secret-de-deploiement");
    options.UseTls.ShouldBeTrue();
    options.PublishTimeoutSeconds.ShouldBe(3);
  }

  /// <summary>
  /// <b>Un port aberrant arrête le démarrage</b>, et le message nomme la clé fautive : lu comme un
  /// port nul ou arrondi, une faute de frappe passerait pour un réglage.
  /// </summary>
  [Theory]
  [InlineData("0")]
  [InlineData("-1")]
  [InlineData("65536")]
  [InlineData("5672.0")]
  [InlineData("cinq mille")]
  [InlineData("")]
  [InlineData(" 5672")]
  public void RefusesToStartOnAnAbsurdPort(string raw)
  {
    Refusal(new() { [RabbitMqOptions.HostNameKey] = Host, [RabbitMqOptions.PortKey] = raw })
      .Message.ShouldContain(RabbitMqOptions.PortKey);
  }

  /// <summary>
  /// <b>Un délai de confirmation aberrant arrête le démarrage</b>, exactement comme
  /// <c>HostSystem:TimeoutSeconds</c>.
  /// </summary>
  [Theory]
  [InlineData("0")]
  [InlineData("-5")]
  [InlineData("1.5")]
  [InlineData("10s")]
  [InlineData("")]
  public void RefusesToStartOnAnAbsurdPublishTimeout(string raw)
  {
    Refusal(new() { [RabbitMqOptions.HostNameKey] = Host, [RabbitMqOptions.PublishTimeoutSecondsKey] = raw })
      .Message.ShouldContain(RabbitMqOptions.PublishTimeoutSecondsKey);
  }

  /// <summary>Un drapeau TLS qui n'est ni vrai ni faux n'est pas un drapeau.</summary>
  [Theory]
  [InlineData("oui")]
  [InlineData("1")]
  [InlineData("")]
  public void RefusesToStartOnATlsFlagThatIsNeitherTrueNorFalse(string raw)
  {
    Refusal(new() { [RabbitMqOptions.HostNameKey] = Host, [RabbitMqOptions.UseTlsKey] = raw })
      .Message.ShouldContain(RabbitMqOptions.UseTlsKey);
  }

  /// <summary>
  /// ⚠️ <b>Sans hôte, rien n'est lu ni jugé</b> : un déploiement sans bus démarre, quelles que
  /// soient les miettes de réglages restées dans son fichier. Il n'y a pas de connexion à refuser.
  /// </summary>
  [Fact]
  public void JudgesNothingWhenTheDeploymentHasNoHost()
  {
    var options = OptionsOf(new()
    {
      [RabbitMqOptions.PortKey] = "cinq mille",
      [RabbitMqOptions.PublishTimeoutSecondsKey] = "-3",
    });

    options.HostName.ShouldBeNull();
    options.Port.ShouldBe(5672);
    options.PublishTimeoutSeconds.ShouldBe(10);
  }

  private static ArgumentException Refusal(Dictionary<string, string?> settings) =>
    Should.Throw<ArgumentException>(() => new ServiceCollection().AddBrokerConnection(ConfigurationOf(settings)));

  private static RabbitMqOptions OptionsOf(Dictionary<string, string?> settings)
  {
    using var services = Registered(settings);

    return services.GetRequiredService<RabbitMqOptions>();
  }

  private static IBrokerConnectionState StateOf(ServiceProvider services) =>
    services.GetRequiredService<IBrokerConnectionState>();

  private static ServiceProvider Registered(Dictionary<string, string?> settings) =>
    new ServiceCollection()
      .AddBrokerConnection(ConfigurationOf(settings))
      .BuildServiceProvider();

  private static IConfiguration ConfigurationOf(Dictionary<string, string?> settings) =>
    new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
}
