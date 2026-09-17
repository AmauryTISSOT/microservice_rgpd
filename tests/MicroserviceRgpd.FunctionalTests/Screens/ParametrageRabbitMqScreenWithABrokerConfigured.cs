using System.Net;
using System.Text.RegularExpressions;
using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MicroserviceRgpd.FunctionalTests.Screens;

/// <summary>
/// La face RabbitMQ sur un déploiement <b>dont la connexion au broker est configurée</b> : le
/// bandeau ne paraît pas, quel que soit le nombre de routages posés.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Aucun broker ne répond derrière la clé</b>, et c'est ce qui donne son sens à cette classe :
/// l'hôte déclaré ne se résout nulle part. La page rend quand même, et rien ne l'a fait attendre —
/// la décision se prend sur la <b>seule présence</b> de la clé, sans le moindre test réseau
/// (ADR-0027).
/// </para>
/// <para>
/// <b>Un hôte à part</b>, parce que la clé se pose à la construction de l'hôte : c'est la seule
/// chose qui distingue ce déploiement de celui des autres tests d'écran.
/// </para>
/// </remarks>
[Collection(ABrokerConfiguredWebCollection.Name)]
public class ParametrageRabbitMqScreenWithABrokerConfigured(ABrokerConfiguredWebApplicationFactory factory)
  : IAsyncLifetime
{
  private const string RabbitMq = "/parametrage/rabbitmq";
  private const string Save = "/parametrage/rabbitmq?handler=Set";

  private readonly HttpClient _client = factory.CreateClient(
    new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

  public Task InitializeAsync() => ForgetEveryChannelAsync();

  public Task DisposeAsync() => ForgetEveryChannelAsync();

  /// <summary>
  /// Le critère du ticket : <b>clé présente, pas de bandeau</b> — sur un service vierge comme sur un
  /// service où plusieurs droits portent un routage.
  /// </summary>
  [Fact]
  public async Task NeverWarnsWhenTheDeploymentDeclaresABrokerConnection()
  {
    TheBrokerConnectionAdvisory.In(WebUtility.HtmlDecode(await ReadAsync())).ShouldBeNull();

    await SaveAndExpectARedirectAsync("Erasure", "rgpd.exercices", "droit.effacement");
    await SaveAndExpectARedirectAsync("Access", "rgpd.exercices", "droit.acces");

    var screen = WebUtility.HtmlDecode(await ReadAsync());

    // Les deux routages sont bien là : c'est la clé qui fait taire le bandeau, pas un écran vide.
    screen.ShouldContain("droit.effacement");
    screen.ShouldContain("droit.acces");

    TheBrokerConnectionAdvisory.In(screen).ShouldBeNull("Un déploiement qui déclare sa connexion n'a rien à se voir reprocher.");
  }

  /// <summary>
  /// ⚠️ <b>La page ne joint jamais le broker pour se rendre.</b> L'hôte déclaré ne se résout nulle
  /// part — aucun broker de ce nom n'existe —, et la page répond quand même. Un rendu qui aurait
  /// tenté d'y ouvrir une connexion aurait fait dépendre l'affichage d'un écran de la disponibilité
  /// du bus.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>C'est la réponse qu'on lit, pas le temps qu'elle a pris.</b> Un chronomètre aurait mesuré
  /// la vitesse à laquelle la machine du jour échoue à résoudre un nom, et non l'absence d'appel.
  /// </remarks>
  [Fact]
  public async Task RendersWithoutReachingTheDeclaredBrokerHost()
  {
    await SaveAndExpectARedirectAsync("Portability", "rgpd.exercices", "droit.portabilite");

    (await _client.GetAsync(RabbitMq)).StatusCode.ShouldBe(HttpStatusCode.OK);
  }

  /// <summary>
  /// <b>La clé ne se lit pas à l'écran</b> : elle nomme un hôte de déploiement, et cette face ne
  /// montre que ce que l'intégrateur y a déclaré.
  /// </summary>
  [Fact]
  public async Task NeverShowsTheDeclaredBrokerHostOnTheScreen()
  {
    await SaveAndExpectARedirectAsync("Erasure", "rgpd.exercices", "droit.effacement");

    (await ReadAsync()).ShouldNotContain(ABrokerConfiguredWebApplicationFactory.HostName);
  }

  private async Task<string> ReadAsync()
  {
    var response = await _client.GetAsync(RabbitMq);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);

    return await response.Content.ReadAsStringAsync();
  }

  /// <summary>
  /// Enregistre le routage d'un droit, jeton anti-rejeu compris, <b>et exige la redirection</b> :
  /// ces tests ne parlent pas du refus, et une saisie que le serveur aurait rejetée n'aurait pas
  /// posé le routage dont ils prétendent lire l'effet.
  /// </summary>
  private async Task<HttpResponseMessage> SaveAndExpectARedirectAsync(string right, string exchange, string key)
  {
    var saved = await _client.PostAsync(Save, new FormUrlEncodedContent(
    [
      new("__RequestVerificationToken", await AntiforgeryTokenAsync()),
      new("Form.Right", right),
      new("Form.Exchange", exchange),
      new("Form.RoutingKey", key),
    ]));

    saved.StatusCode.ShouldBe(HttpStatusCode.Found);

    return saved;
  }

  private async Task<string> AntiforgeryTokenAsync()
  {
    var token = Regex.Match(
      await ReadAsync(),
      @"<input name=""__RequestVerificationToken""[^>]*value=""([^""]+)""");

    token.Success.ShouldBeTrue("La face RabbitMQ ne porte aucun jeton anti-rejeu.");

    return token.Groups[1].Value;
  }

  /// <summary>Ramène le service à son état d'installation : aucune ligne de Paramétrage.</summary>
  private async Task ForgetEveryChannelAsync()
  {
    using var scope = factory.Services.CreateScope();

    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<Settings>().ExecuteDeleteAsync();
  }
}
