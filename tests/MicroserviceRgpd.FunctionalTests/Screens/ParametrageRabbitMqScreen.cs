using System.Net;
using System.Text.RegularExpressions;
using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.Infrastructure.Data;
using MicroserviceRgpd.UseCases.Configuration.SetRightEndpoint;
using MicroserviceRgpd.UseCases.Configuration.SetRightRabbitMqRouting;
using Microsoft.EntityFrameworkCore;

namespace MicroserviceRgpd.FunctionalTests.Screens;

/// <summary>
/// La <b>seconde face du Paramétrage</b> — « Configuration RabbitMQ » —, exercée par sa seule
/// frontière HTTP. Elle donne à <b>lire</b> les routages en vigueur : les formulaires viennent
/// ensuite. Les deux faces se rejoignent par des <b>onglets rendus par le serveur</b>, sans une
/// ligne de JavaScript.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Les deux pages portent le même <c>h1</c></b>, et c'est ce qui les fait lire comme un seul
/// écran à deux faces plutôt que comme deux écrans voisins. Le panneau latéral ne bouge pas : on
/// n'a pas quitté le Paramétrage, et « Paramétrage » y reste marqué courant — ce que le harnais de
/// layout garde pour toute la surface, cette page comprise.
/// </para>
/// <para>
/// ⚠️ <b>Chaque test part d'un service vierge</b>, pour la même raison que sur la page HTTP : le
/// Paramétrage est un singleton, et la base est partagée par toute la collection.
/// </para>
/// </remarks>
[Collection(WebCollection.Name)]
public class ParametrageRabbitMqScreen(CustomWebApplicationFactory<Program> factory) : IAsyncLifetime
{
  private const string Http = "/parametrage";
  private const string RabbitMq = "/parametrage/rabbitmq";

  /// <summary>Le nom que les <b>deux</b> faces portent en tête, recopié à dessein.</summary>
  private const string ScreenName = "Paramétrage du microservice RGPD";

  private const string HttpTab = "Configuration HTTP";
  private const string RabbitMqTab = "Configuration RabbitMQ";

  /// <summary>
  /// <b>Les six droits, leur libellé français et leur article</b>, recopiés à dessein : un test qui
  /// lirait le SmartEnum qu'il vérifie ne vérifierait plus rien. C'est l'ordre du règlement — 15,
  /// 16, 17, 18, 20, 21 — et l'énumération est <b>non contiguë</b>.
  /// </summary>
  private static readonly (string Label, int Article)[] TheSixRights =
  [
    ("droit d'accès", 15),
    ("droit de rectification", 16),
    ("droit à l'effacement", 17),
    ("droit à la limitation du traitement", 18),
    ("droit à la portabilité", 20),
    ("droit d'opposition", 21),
  ];

  private readonly HttpClient _client = factory.CreateClient(
    new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

  public Task InitializeAsync() => ForgetEveryChannelAsync();

  public Task DisposeAsync() => ForgetEveryChannelAsync();

  /// <summary>La page répond, et elle porte <b>le même titre que la page HTTP</b>.</summary>
  [Fact]
  public async Task CarriesTheSameHeadingAsTheHttpPage()
  {
    var rabbitMq = WebUtility.HtmlDecode(await ReadAsync(RabbitMq));

    rabbitMq.ShouldContain($"<h1>{ScreenName}</h1>");
    WebUtility.HtmlDecode(await ReadAsync(Http)).ShouldContain($"<h1>{ScreenName}</h1>");
  }

  /// <summary>
  /// <b>Les deux faces portent les mêmes deux onglets</b>, dans le même ordre, et chacune marque le
  /// sien — et lui seul — <c>aria-current="page"</c>.
  /// </summary>
  [Theory]
  [InlineData(Http, HttpTab)]
  [InlineData(RabbitMq, RabbitMqTab)]
  public async Task MarksItsOwnTabAndOnlyItOnBothFaces(string screen, string expected)
  {
    var tabs = TabsIn(WebUtility.HtmlDecode(await ReadAsync(screen)));

    tabs.Select(tab => (tab.Address, tab.Label))
      .ShouldBe([(Http, HttpTab), (RabbitMq, RabbitMqTab)]);

    tabs.Where(tab => tab.IsCurrent).Select(tab => tab.Label).ShouldBe([expected]);
  }

  /// <summary>
  /// <b>Les onglets sont des liens rendus par le serveur</b> : la navigation d'une face à l'autre
  /// tient sans une ligne de JavaScript, et l'adresse de chaque onglet répond d'elle-même.
  /// </summary>
  [Fact]
  public async Task NavigatesFromOneFaceToTheOtherWithoutAnyScript()
  {
    foreach (var screen in new[] { Http, RabbitMq })
    {
      var rendered = WebUtility.HtmlDecode(await ReadAsync(screen));

      // ⚠️ C'EST LA RÉGION D'ONGLETS QU'ON LIT, PAS LA PAGE ENTIÈRE. Ce que ce test garde est que LES
      // ONGLETS sont des liens ; un module qu'un écran poserait un jour pour tout autre chose n'a pas
      // à faire échouer cette phrase-là.
      TabsRegionIn(rendered).ShouldNotContain("<script", Case.Insensitive);

      foreach (var tab in TabsIn(rendered))
      {
        tab.Address.ShouldNotBeEmpty("Un onglet qui ne mène nulle part n'est pas un lien.");
        (await _client.GetAsync(tab.Address)).StatusCode.ShouldBe(HttpStatusCode.OK);
      }
    }
  }

  /// <summary>
  /// La page liste <b>les six droits dans l'ordre des articles</b>, chacun avec son libellé français
  /// et son article — lus sur le noyau partagé, jamais recopiés par le contexte.
  /// </summary>
  [Fact]
  public async Task ListsTheSixRightsInArticleOrderEachWithItsLabelAndArticle()
  {
    var screen = WebUtility.HtmlDecode(await ReadAsync(RabbitMq));

    var sections = screen.Split("<div class=\"right\">").Skip(1).ToList();

    sections.Count.ShouldBe(TheSixRights.Length);

    foreach (var (section, right) in sections.Zip(TheSixRights))
    {
      section.ShouldContain($"<h2>{right.Label}</h2>");
      section.ShouldContain($"Article {right.Article}");
    }
  }

  /// <summary>
  /// <b>Sur un service vierge, les six droits s'affichent « non configuré »</b> : rien n'est semé au
  /// démarrage, et la page le dit en toutes lettres plutôt que de laisser un blanc.
  /// </summary>
  [Fact]
  public async Task ShowsEveryRightAsUnconfiguredOnAVirginService()
  {
    foreach (var section in SectionsIn(WebUtility.HtmlDecode(await ReadAsync(RabbitMq))).Values)
    {
      section.ShouldContain("non configuré");
    }
  }

  /// <summary>
  /// <b>Un droit portant un routage affiche son exchange et sa routing key</b>, et lui seul : les
  /// cinq autres restent « non configuré ».
  /// </summary>
  [Fact]
  public async Task ShowsTheExchangeAndTheRoutingKeyOfARightThatCarriesARouting()
  {
    const string Exchange = "rgpd.exercices";
    const string Key = "droit.effacement";

    await RouteAsync(DataSubjectRight.Erasure, Exchange, Key);

    var sections = SectionsIn(WebUtility.HtmlDecode(await ReadAsync(RabbitMq)));

    sections["droit à l'effacement"].ShouldContain(Exchange);
    sections["droit à l'effacement"].ShouldContain(Key);
    sections["droit à l'effacement"].ShouldNotContain("non configuré");

    foreach (var untouched in sections.Where(section => section.Key != "droit à l'effacement"))
    {
      untouched.Value.ShouldContain(
        "non configuré", customMessage: $"Le {untouched.Key} ne devait pas être touché.");
    }
  }

  /// <summary>
  /// ⚠️ <b>Un droit réglé sur l'autre canal n'est pas « non configuré »</b> sur cette page : le mot
  /// garde son sens — ni adresse, ni routage —, et cette page ne revendique pas le routage d'un
  /// droit qui n'en a pas. L'affichage du réglage d'en face vient au ticket des formulaires.
  /// </summary>
  [Fact]
  public async Task DoesNotCallARightConfiguredOverHttpUnconfigured()
  {
    await AddressAsync(DataSubjectRight.Access, "https://brocanto.example.fr/rgpd/acces");

    SectionsIn(WebUtility.HtmlDecode(await ReadAsync(RabbitMq)))["droit d'accès"]
      .ShouldNotContain("non configuré");
  }

  private async Task<string> ReadAsync(string screen)
  {
    var response = await _client.GetAsync(screen);

    response.StatusCode.ShouldBe(HttpStatusCode.OK, $"L'adresse {screen} doit répondre.");

    return await response.Content.ReadAsStringAsync();
  }

  /// <summary>Les sections des six droits, chacune sous son libellé.</summary>
  private static IReadOnlyDictionary<string, string> SectionsIn(string screen)
  {
    var sections = screen.Split("<div class=\"right\">").Skip(1).ToList();

    return TheSixRights.ToDictionary(
      right => right.Label,
      right => sections.Single(section => section.Contains($"<h2>{right.Label}</h2>", StringComparison.Ordinal)));
  }

  /// <summary>
  /// Les onglets du Paramétrage, dans l'ordre où la page les pose : leur adresse, ce qu'ils donnent
  /// à lire, et le marquage de la face courante. ⚠️ Ils se lisent dans la <b>région d'onglets</b>,
  /// jamais dans la page entière : le panneau latéral porte lui aussi un lien vers
  /// <c>/parametrage</c>, et une lecture large l'aurait pris pour un onglet.
  /// </summary>
  private static IReadOnlyList<(string Address, string Label, bool IsCurrent)> TabsIn(string screen)
  {
    return
    [
      .. Regex.Matches(TabsRegionIn(screen), @"<a\b([^>]*)>(.*?)</a>", RegexOptions.Singleline).Select(link =>
      (
        Address: Regex.Match(link.Groups[1].Value, @"\bhref=""([^""]*)""").Groups[1].Value,
        Label: link.Groups[2].Value.Trim(),
        IsCurrent: link.Groups[1].Value.Contains(@"aria-current=""page""", StringComparison.Ordinal)
      )),
    ];
  }

  /// <summary>La région d'onglets d'une page rendue, reconnue à sa classe.</summary>
  private static string TabsRegionIn(string screen)
  {
    var tabs = Regex.Match(screen, @"<nav\b[^>]*\bclass=""tabs""[^>]*>(.*?)</nav>", RegexOptions.Singleline);

    tabs.Success.ShouldBeTrue("L'écran ne porte aucune région d'onglets.");

    return tabs.Groups[1].Value;
  }

  /// <summary>
  /// Pose le routage d'un droit par le use case — le seul chemin qui existe aujourd'hui, la page
  /// étant en lecture seule jusqu'au ticket des formulaires.
  /// </summary>
  private async Task RouteAsync(DataSubjectRight right, string exchange, string key)
  {
    using var scope = factory.Services.CreateScope();

    var written = await scope.ServiceProvider.GetRequiredService<Mediator.IMediator>().Send(
      new SetRightRabbitMqRoutingCommand(
        right,
        new RabbitMqRouting(ExchangeName.From(exchange), RoutingKey.From(key))));

    written.IsSuccess.ShouldBeTrue();
  }

  /// <summary>Pose l'adresse HTTP d'un droit, par le même chemin.</summary>
  private async Task AddressAsync(DataSubjectRight right, string url)
  {
    using var scope = factory.Services.CreateScope();

    var written = await scope.ServiceProvider.GetRequiredService<Mediator.IMediator>().Send(
      new SetRightEndpointCommand(right, EndpointUrl.From(url)));

    written.IsSuccess.ShouldBeTrue();
  }

  /// <summary>Ramène le service à son état d'installation : aucune ligne de Paramétrage.</summary>
  private async Task ForgetEveryChannelAsync()
  {
    using var scope = factory.Services.CreateScope();

    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<Settings>().ExecuteDeleteAsync();
  }
}
