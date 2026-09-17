using System.Globalization;
using MicroserviceRgpd.Infrastructure.Configuration;
using Testcontainers.RabbitMq;

namespace MicroserviceRgpd.FunctionalTests;

/// <summary>
/// Le service démarré <b>devant un vrai broker</b> : un RabbitMQ en conteneur, et les clés de la
/// section <c>RabbitMq</c> posées sur ses vraies valeurs.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Rien n'est substitué dans le conteneur</b> (ADR-0028) : c'est le vrai client AMQP du service
/// qui publie, sur la vraie connexion qu'il ouvre lui-même. Ce qu'une file reçoit est ce qu'un
/// intégrateur recevrait — les cinq clés, le <c>content-type</c>, la persistance, le
/// <c>message-id</c> —, et un exchange sans binding rend vraiment le message.
/// </para>
/// <para>
/// ⚠️ <b>Le broker était naguère un nom de machine jamais joint</b>, et cela suffisait au bandeau,
/// qui se décide sur la seule présence d'une clé (ADR-0027). Il ne suffit plus à l'exécution : une
/// publication ne se prouve que contre un broker. La contrepartie est assumée — <b>Docker devient
/// nécessaire à cette collection</b>, et son démarrage coûte quelques secondes.
/// </para>
/// <para>
/// Les clés sont posées par <c>UseSetting</c>, sous les noms exacts que le déploiement emploie —
/// cités sur les constantes de production, jamais recopiés.
/// </para>
/// </remarks>
public sealed class ABrokerConfiguredWebApplicationFactory : CustomWebApplicationFactory<Program>
{
  /// <summary>L'identifiant du broker de test — le déploiement en déclare toujours un.</summary>
  public const string UserName = "rgpd-en-test";

  /// <summary>Le mot de passe du broker de test.</summary>
  public const string Password = "mot-de-passe-du-deploiement";

  /// <summary>
  /// Le délai d'attente d'une confirmation dans tout hôte de test, en secondes — <b>réduit</b>, pour
  /// qu'un délai dépassé s'éprouve sans attendre les dix secondes d'un déploiement (ADR-0028).
  /// </summary>
  public const int PublishTimeoutSeconds = 2;

  private readonly RabbitMqContainer _broker = new RabbitMqBuilder("rabbitmq:4-alpine")
    .WithUsername(UserName)
    .WithPassword(Password)
    .Build();

  /// <summary>L'hôte du broker, tel que le conteneur l'expose — un nom que le service joint vraiment.</summary>
  public string HostName => _broker.Hostname;

  /// <summary>Le port AMQP du conteneur, tel que la machine hôte le voit.</summary>
  public int Port => _broker.GetMappedPublicPort(5672);

  /// <summary>Le broker démarre avec la base : l'hôte n'est bâti qu'ensuite.</summary>
  public override async Task InitializeAsync()
  {
    await _broker.StartAsync();
    await base.InitializeAsync();
  }

  public override async Task DisposeAsync()
  {
    await base.DisposeAsync();
    await _broker.DisposeAsync();
  }

  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
    base.ConfigureWebHost(builder);

    builder.UseSetting(RabbitMqOptions.HostNameKey, HostName);
    builder.UseSetting(RabbitMqOptions.PortKey, Port.ToString(CultureInfo.InvariantCulture));
    builder.UseSetting(RabbitMqOptions.UserNameKey, UserName);
    builder.UseSetting(RabbitMqOptions.PasswordKey, Password);
    builder.UseSetting(
      RabbitMqOptions.PublishTimeoutSecondsKey, PublishTimeoutSeconds.ToString(CultureInfo.InvariantCulture));
  }
}

/// <summary>
/// La collection de <see cref="ABrokerConfiguredWebApplicationFactory"/> : un hôte se bâtit une
/// fois, et un hôte dont le déploiement déclare une connexion est une autre forme d'hôte.
/// </summary>
[CollectionDefinition(Name)]
public class ABrokerConfiguredWebCollection : ICollectionFixture<ABrokerConfiguredWebApplicationFactory>
{
  public const string Name = "Web avec une connexion RabbitMQ configurée";
}
