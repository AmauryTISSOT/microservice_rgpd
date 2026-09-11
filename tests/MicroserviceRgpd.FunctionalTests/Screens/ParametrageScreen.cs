using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.FunctionalTests.Layout;
using MicroserviceRgpd.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MicroserviceRgpd.FunctionalTests.Screens;

/// <summary>
/// L'écran du <c>Settings</c> — le <b>Paramétrage</b> —, exercé par sa <b>seule frontière HTTP</b>.
/// Un intégrateur arrivant sur un service vierge y voit les <b>six droits RGPD d'emblée</b>, chacun
/// avec son libellé français et son article, tous « non configuré ». Il y saisit, droit par droit,
/// l'adresse d'exercice, la relit, et la corrige sans repartir de zéro.
/// </summary>
/// <remarks>
/// <para>
/// Ce que ces tests gardent n'est pas la mise en page : c'est que les six droits paraissent, que
/// <see cref="Core.SharedKernel.DataSubjectRight.OutOfScope"/> ne paraît <b>jamais</b>, qu'un service
/// vierge les dit tous « non configuré » sans qu'aucune ligne n'ait été semée, qu'un enregistrement
/// ne touche <b>qu'un droit</b>, et que l'écran <b>énumère sans compter</b> — aucun agrégat, aucun
/// ratio « 4/6 ».
/// </para>
/// <para>
/// ⚠️ <b>Chaque test part d'un service vierge.</b> Le Paramétrage est un singleton, et la base est
/// partagée par toute la collection : sans remise à zéro, l'adresse posée par un test serait l'état
/// initial du suivant, et l'ordre d'exécution déciderait du verdict. La remise à zéro est un
/// <i>arrangement</i> — la ligne unique retirée, exactement l'état d'un service qu'on vient
/// d'installer —, jamais une assertion : tout ce qui est vérifié l'est par la frontière HTTP.
/// </para>
/// </remarks>
[Collection(WebCollection.Name)]
public class ParametrageScreen(CustomWebApplicationFactory<Program> factory) : IAsyncLifetime
{
  private const string Parametrage = "/parametrage";

  /// <summary>L'adresse du formulaire d'un droit : la page, et le gestionnaire d'enregistrement.</summary>
  private const string Save = "/parametrage?handler=Set";

  /// <summary>Le nom que l'écran porte en titre et en tête, recopié à dessein.</summary>
  private const string ScreenName = "Paramétrage du microservice RGPD";

  /// <summary>Le nom <b>que la barre</b> donne au même écran, recopié à dessein lui aussi.</summary>
  private const string NavigationLabel = "Paramétrage";

  /// <summary>
  /// <b>Les six droits, leur libellé français et leur article</b>, recopiés à dessein : un test qui
  /// lirait le SmartEnum qu'il vérifie ne vérifierait plus rien. C'est l'ordre du règlement — 15, 16,
  /// 17, 18, 20, 21 — et l'énumération est <b>non contiguë</b> : l'art. 19 n'ouvre aucun droit, l'art.
  /// 22 est hors périmètre. Chaque droit porte aussi son <b>nom canonique</b>, celui que sa
  /// mini-form envoie.
  /// </summary>
  private static readonly (string Name, string Label, int Article)[] TheSixRights =
  [
    ("Access", "droit d'accès", 15),
    ("Rectification", "droit de rectification", 16),
    ("Erasure", "droit à l'effacement", 17),
    ("Restriction", "droit à la limitation du traitement", 18),
    ("Portability", "droit à la portabilité", 20),
    ("Objection", "droit d'opposition", 21),
  ];

  private const string Portability = "droit à la portabilité";

  /// <summary>
  /// Les redirections ne sont pas suivies : c'est la redirection elle-même qu'on vérifie. Un
  /// enregistrement qui rendrait directement sa page ferait d'un rechargement un second envoi.
  /// </summary>
  private readonly HttpClient _client = factory.CreateClient(
    new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

  public Task InitializeAsync() => ForgetEveryEndpointAsync();

  public Task DisposeAsync() => ForgetEveryEndpointAsync();

  /// <summary>
  /// Le critère du ticket : les six droits paraissent, chacun avec son libellé français et son
  /// article RGPD.
  /// </summary>
  [Fact]
  public async Task ListsTheSixRightsEachWithItsFrenchLabelAndArticle()
  {
    // Les libellés portent une apostrophe — « droit d'accès », « droit à l'effacement » —, que le
    // moteur de rendu encode en `&#x27;`. On relit ce que l'humain lit, entités décodées, comme le
    // font les assertions de phrase du layout.
    var screen = WebUtility.HtmlDecode(await ReadAsync());

    foreach (var (_, label, article) in TheSixRights)
    {
      screen.ShouldContain(label);
      screen.ShouldContain($"Article {article}");
    }
  }

  /// <summary>
  /// <b>Sur un service vierge, les six droits s'affichent « non configuré ».</b> Rien n'est semé au
  /// démarrage : tant qu'aucune adresse n'a été enregistrée, les six sections portent l'état « non
  /// configuré », et un droit sans URL est dit explicitement tel.
  /// </summary>
  [Fact]
  public async Task ShowsEveryRightAsUnconfiguredOnAVirginService()
  {
    var screen = await ReadAsync();

    // Le préambule ouvre la liste : une section par droit, six droits.
    var sections = screen.Split("<div class=\"right\">");

    sections.Length.ShouldBe(TheSixRights.Length + 1);

    foreach (var section in sections.Skip(1))
    {
      section.ShouldContain("non configuré");
    }
  }

  /// <summary>
  /// ⚠️ <b><see cref="Core.SharedKernel.DataSubjectRight.OutOfScope"/> ne paraît jamais.</b> Ce n'est
  /// pas un droit qu'on exerce, c'est le verdict qu'aucun ne l'est : son libellé « hors périmètre »
  /// n'a rien à faire sur un écran qui configure les six droits.
  /// </summary>
  [Fact]
  public async Task NeverShowsOutOfScope()
  {
    (await ReadAsync()).ShouldNotContain("hors périmètre");
  }

  /// <summary>
  /// <b>Aucun compte, aucun ratio, aucun taux.</b> Ni « 4 droits configurés sur 6 », ni
  /// « couverture : 66 % » : un dénominateur présenterait la configuration comme complète. L'écran
  /// énumère, il ne compte pas. ⚠️ <b>Les articles ne sont pas des comptes</b> : « Article 15 » est
  /// un chiffre qu'on lit et qu'on ignore, de même nature que les références du seuil.
  /// </summary>
  [Fact]
  public async Task ShowsNoCountNoRatioAndNoRateAnywhereOnTheScreen()
  {
    // La feuille de style est retirée par prudence — ses `100%` seraient de la mise en page, pas un
    // taux affiché. L'écran n'en porte aucune en propre, mais le geste garde la règle si elle revenait.
    var screen = Regex.Replace(await ReadAsync(), "(?s)<style>.*?</style>", string.Empty);

    screen.ShouldNotContain("%");

    // Un compte ou un ratio — « 6 droits », « 4 sur 6 », « 4/6 » — et non un numéro d'article, qui
    // n'est jamais collé à « droit », « configurés », « sur N » ni « /N ».
    Regex.IsMatch(screen, @"\d+\s*(?:droits?\b|configurés?\b|sur\s+\d|/\s*\d)").ShouldBeFalse(
      "L'écran porte un compte ou un ratio, là où il ne doit qu'énumérer.");
  }

  /// <summary>
  /// <b>L'écran porte son nom en titre et en tête — « Paramétrage du microservice RGPD » —, et sa
  /// forme courte « Paramétrage » dans la barre</b>, où le wordmark « Microservice RGPD » la précède.
  /// </summary>
  [Fact]
  public async Task CarriesTheFullNameInTheTabAndTitleAndTheShortOneInTheBar()
  {
    var screen = await ReadAsync();

    screen.ShouldContain($"<title>{ScreenName} —");
    screen.ShouldContain($"<h1>{ScreenName}</h1>");
    LayoutSurface.SidepanelIn(screen).ShouldContain($">{NavigationLabel}</a>");
  }

  /// <summary>
  /// <b>L'entrée de barre « Manifest » a été retirée au profit du Paramétrage.</b> Le panneau ne
  /// mène plus vers <c>/manifest</c> : la configuration passe désormais par <c>/parametrage</c>.
  /// </summary>
  [Fact]
  public async Task NoLongerOffersTheManifestEntryInTheBar()
  {
    LayoutSurface.SidepanelIn(await ReadAsync()).ShouldNotContain("/manifest");
  }

  /// <summary>
  /// Le critère du ticket : définir l'adresse d'un droit, et la <b>relire à l'écran</b>. La section
  /// du droit ne se dit plus « non configuré » ; les cinq autres, si.
  /// </summary>
  [Fact]
  public async Task DefinesTheEndpointOfARightAndReadsItBackOnTheScreen()
  {
    const string Endpoint = "https://brocanto.example.fr/rgpd/acces";

    (await SaveAsync("Access", Endpoint)).StatusCode.ShouldBe(HttpStatusCode.Found);

    var access = await SectionAsync("droit d'accès");

    access.ShouldContain(Endpoint);
    access.ShouldNotContain("non configuré");

    foreach (var (_, label, _) in TheSixRights.Skip(1))
    {
      (await SectionAsync(label)).ShouldContain("non configuré");
    }
  }

  /// <summary>
  /// <b>Corriger sans repartir de zéro</b> : le champ d'un droit configuré porte déjà son adresse, et
  /// l'enregistrer de nouveau la <b>remplace</b> — l'ancienne disparaît, l'autre droit configuré ne
  /// bouge pas.
  /// </summary>
  [Fact]
  public async Task RevisesAnAlreadyConfiguredEndpointWithoutTouchingTheOtherRights()
  {
    const string Before = "https://brocanto.example.fr/rgpd/rectification";
    const string After = "https://brocanto.example.fr/rgpd/v2/rectification";
    const string Erasure = "https://brocanto.example.fr/rgpd/effacement";

    await SaveAsync("Rectification", Before);
    await SaveAsync("Erasure", Erasure);

    (await SectionAsync("droit de rectification")).ShouldContain($"value=\"{Before}\"");

    (await SaveAsync("Rectification", After)).StatusCode.ShouldBe(HttpStatusCode.Found);

    var rectification = await SectionAsync("droit de rectification");

    rectification.ShouldContain(After);
    rectification.ShouldNotContain(Before);
    (await SectionAsync("droit à l'effacement")).ShouldContain(Erasure);
    (await SectionAsync("droit d'accès")).ShouldContain("non configuré");
  }

  /// <summary>
  /// <b>Chaque enregistrement ne touche qu'un droit.</b> Les six droits configurés, un seul repris :
  /// les cinq autres se relisent à l'identique, adresse pour adresse.
  /// </summary>
  [Fact]
  public async Task EachSaveTouchesOnlyItsOwnRight()
  {
    foreach (var (name, _, _) in TheSixRights)
    {
      await SaveAsync(name, $"https://brocanto.example.fr/rgpd/{name.ToLowerInvariant()}");
    }

    var before = await SectionsAsync();

    await SaveAsync("Portability", "https://ailleurs.example.fr/portabilite");

    var after = await SectionsAsync();

    foreach (var (_, label, _) in TheSixRights.Where(right => right.Label != Portability))
    {
      after[label].ShouldBe(before[label]);
    }

    after[Portability].ShouldContain("https://ailleurs.example.fr/portabilite");
    after[Portability].ShouldNotContain("https://brocanto.example.fr/rgpd/portability");
  }

  /// <summary>
  /// <b>Une adresse invalide est refusée, et le refus est rendu à l'écran</b> : un statut 200, le
  /// message du type qui porte la règle, et <b>aucune redirection</b> — une redirection aurait perdu
  /// le refus en chemin. Rien n'est enregistré : le droit reste « non configuré ».
  /// </summary>
  [Theory]
  [InlineData("/rgpd/acces", "n'est pas une URL http ou https absolue")]
  [InlineData("https://", "n'est pas une URL http ou https absolue")]
  [InlineData("pas une adresse", "n'est pas une URL http ou https absolue")]
  [InlineData("ftp://brocanto.example.fr/rgpd", "n'est pas une URL http ou https absolue")]
  [InlineData("https://jean:secret@brocanto.example.fr/rgpd", "ne porte pas d'identifiants")]
  [InlineData("", "absente ou vide")]
  public async Task RefusesAnInvalidAddressAndSaysSoOnTheScreenWithoutRedirecting(string address, string refusal)
  {
    var response = await SaveAsync("Access", address);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    response.Headers.Location.ShouldBeNull();
    WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()).ShouldContain(refusal);

    (await SectionAsync("droit d'accès")).ShouldContain("non configuré");
  }

  /// <summary>
  /// Le refus se dit <b>dans la section du droit qu'on saisissait</b>, et la saisie refusée y reste
  /// pour être corrigée : l'intégrateur n'a pas à la retaper, ni à chercher de quel droit il s'agit.
  /// </summary>
  [Fact]
  public async Task SaysTheRefusalInTheSectionOfTheRightBeingEditedAndKeepsWhatWasTyped()
  {
    var response = await SaveAsync("Objection", "/rgpd/opposition");
    var screen = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

    var objection = SectionIn(screen, "droit d'opposition");

    objection.ShouldContain("n'est pas une URL http ou https absolue");
    objection.ShouldContain("value=\"/rgpd/opposition\"");

    SectionIn(screen, "droit d'accès").ShouldNotContain("n'est pas une URL");
  }

  /// <summary>
  /// <b>Le HTTP est accepté au même titre que le HTTPS</b> : un environnement local ou interne se
  /// configure aussi, et l'exigence de chiffrement relève du déploiement.
  /// </summary>
  [Fact]
  public async Task AcceptsHttpAsWellAsHttps()
  {
    const string Endpoint = "http://intranet.brocanto.local/rgpd/limitation";

    (await SaveAsync("Restriction", Endpoint)).StatusCode.ShouldBe(HttpStatusCode.Found);

    (await SectionAsync("droit à la limitation du traitement")).ShouldContain(Endpoint);
  }

  /// <summary>
  /// ⚠️ <b>Aucun appel réseau à l'enregistrement.</b> L'adresse enregistrée désigne un port d'écoute
  /// ouvert par le test : si le service l'appelait — pour « vérifier » l'adresse, la sonder, la
  /// réveiller —, une connexion y attendrait. Configurer une adresse reste une écriture locale.
  /// </summary>
  [Fact]
  public async Task EmitsNoNetworkCallWhenSaving()
  {
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();

    try
    {
      var port = ((IPEndPoint)listener.LocalEndpoint).Port;
      var endpoint = $"http://127.0.0.1:{port}/rgpd/portabilite";

      (await SaveAsync("Portability", endpoint)).StatusCode.ShouldBe(HttpStatusCode.Found);
      (await SectionAsync(Portability)).ShouldContain(endpoint);

      listener.Pending().ShouldBeFalse("Le service a ouvert une connexion vers l'adresse qu'il enregistrait.");
    }
    finally
    {
      listener.Stop();
    }
  }

  /// <summary>
  /// <b>L'enregistrement suit le Post-Redirect-Get</b> : l'écriture répond par une redirection vers
  /// l'écran, et recharger la page relit l'état au lieu de renvoyer la saisie.
  /// </summary>
  [Fact]
  public async Task RedirectsBackToTheScreenAfterSaving()
  {
    var response = await SaveAsync("Erasure", "https://brocanto.example.fr/rgpd/effacement");

    response.StatusCode.ShouldBe(HttpStatusCode.Found);
    response.Headers.Location!.OriginalString.ShouldBe(Parametrage);
  }

  /// <summary>
  /// <b>Un envoi sans jeton anti-rejeu est refusé</b>, et rien n'est enregistré : le formulaire est
  /// le seul chemin vers le Paramétrage, et un site tiers n'a pas à y écrire à la place de
  /// l'intégrateur.
  /// </summary>
  [Fact]
  public async Task RefusesASaveThatCarriesNoAntiforgeryToken()
  {
    var response = await _client.PostAsync(Save, new FormUrlEncodedContent(
    [
      new("Form.Right", "Access"),
      new("Form.Url", "https://tiers.example.fr/rgpd"),
    ]));

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    (await SectionAsync("droit d'accès")).ShouldContain("non configuré");
  }

  /// <summary>
  /// ⚠️ <b>Un droit forgé est refusé en le nommant</b> — <c>OutOfScope</c> compris, qui est un verdict
  /// et non un droit qu'on exerce. Les droits de l'écran sont clos : un nom qu'ils ignorent n'est pas
  /// une saisie humaine, et rien n'est enregistré.
  /// </summary>
  [Theory]
  [InlineData("OutOfScope")]
  [InlineData("Profiling")]
  public async Task RefusesARightTheScreenDoesNotConfigure(string right)
  {
    var response = await SaveAsync(right, "https://brocanto.example.fr/rgpd");

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync())
      .ShouldContain($"« {right} » n'est pas un droit du Paramétrage");

    (await ReadAsync()).ShouldNotContain("https://brocanto.example.fr/rgpd");
  }

  private async Task<string> ReadAsync()
  {
    var response = await _client.GetAsync(Parametrage);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);

    return await response.Content.ReadAsStringAsync();
  }

  /// <summary>La section d'un droit, retrouvée par son libellé — entités décodées.</summary>
  private async Task<string> SectionAsync(string label) =>
    SectionIn(WebUtility.HtmlDecode(await ReadAsync()), label);

  /// <summary>
  /// Les six sections, chacune sous son libellé. Le jeton anti-rejeu en est retiré : il change à
  /// chaque rendu, et deux lectures d'un même état doivent se comparer à l'identique.
  /// </summary>
  private async Task<IReadOnlyDictionary<string, string>> SectionsAsync()
  {
    var screen = Regex.Replace(
      WebUtility.HtmlDecode(await ReadAsync()),
      @"<input name=""__RequestVerificationToken""[^>]*>",
      string.Empty);

    return TheSixRights.ToDictionary(right => right.Label, right => SectionIn(screen, right.Label));
  }

  private static string SectionIn(string screen, string label) =>
    screen.Split("<div class=\"right\">").Single(section => section.Contains($"<h2>{label}</h2>", StringComparison.Ordinal));

  /// <summary>
  /// Remplit la mini-form d'un droit et la renvoie, jeton anti-rejeu compris — exactement ce que
  /// fait un navigateur, et le seul chemin qui existe vers le Paramétrage.
  /// </summary>
  private async Task<HttpResponseMessage> SaveAsync(string right, string url)
  {
    return await _client.PostAsync(Save, new FormUrlEncodedContent(
    [
      new("__RequestVerificationToken", await AntiforgeryTokenAsync()),
      new("Form.Right", right),
      new("Form.Url", url),
    ]));
  }

  private async Task<string> AntiforgeryTokenAsync()
  {
    var token = Regex.Match(
      await ReadAsync(),
      @"<input name=""__RequestVerificationToken""[^>]*value=""([^""]+)""");

    token.Success.ShouldBeTrue("Le Paramétrage ne porte aucun jeton anti-rejeu.");

    return token.Groups[1].Value;
  }

  /// <summary>Ramène le service à son état d'installation : aucune ligne de Paramétrage.</summary>
  private async Task ForgetEveryEndpointAsync()
  {
    using var scope = factory.Services.CreateScope();

    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<Settings>().ExecuteDeleteAsync();
  }
}
