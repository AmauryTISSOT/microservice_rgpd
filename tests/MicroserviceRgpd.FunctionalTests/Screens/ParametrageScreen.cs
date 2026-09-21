using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.FunctionalTests.Layout;
using MicroserviceRgpd.Infrastructure.Data;
using MicroserviceRgpd.UseCases.Configuration.SetRightRabbitMqRouting;
using Microsoft.EntityFrameworkCore;

namespace MicroserviceRgpd.FunctionalTests.Screens;

/// <summary>
/// L'écran du <c>Settings</c> — le <b>Paramétrage</b> —, exercé par sa <b>seule frontière HTTP</b>.
/// Un intégrateur arrivant sur un service vierge y voit les <b>six droits RGPD d'emblée</b>, chacun
/// avec son libellé français et son article, tous « non configuré ». Il y saisit, droit par droit,
/// l'adresse d'exercice, la relit, la corrige sans repartir de zéro, et l'efface pour ramener le
/// droit à « non configuré ».
/// </summary>
/// <remarks>
/// <para>
/// Ce que ces tests gardent n'est pas la mise en page : c'est que les six droits paraissent, que
/// <see cref="Core.SharedKernel.DataSubjectRight.OutOfScope"/> ne paraît <b>jamais</b>, qu'un service
/// vierge les dit tous « non configuré » sans qu'aucune ligne n'ait été semée, qu'un enregistrement
/// ou un effacement ne touche <b>qu'un droit</b>, et que l'écran <b>énumère sans compter</b> —
/// aucun agrégat, aucun ratio « 4/6 ».
/// </para>
/// <para>
/// ⚠️ <b>Chaque test part d'un service vierge.</b> Le Paramétrage est un singleton, et la base est
/// partagée par toute la collection : sans remise à zéro, l'adresse posée par un test serait l'état
/// initial du suivant, et l'ordre d'exécution déciderait du verdict. La remise à zéro est un
/// <i>arrangement</i> — la ligne unique retirée, exactement l'état d'un service qu'on vient
/// d'installer —, jamais une assertion : tout ce qui est vérifié l'est par la frontière HTTP.
/// </para>
/// </remarks>
[Collection(SettingsWebCollection.Name)]
public class ParametrageScreen(CustomWebApplicationFactory<Program> factory) : IAsyncLifetime
{
  private const string Parametrage = "/parametrage";

  /// <summary>L'adresse du formulaire d'un droit : la page, et le gestionnaire d'enregistrement.</summary>
  private const string Save = "/parametrage?handler=Set";

  /// <summary>L'autre face du Paramétrage, celle qui porte les routages.</summary>
  private const string RabbitMq = "/parametrage/rabbitmq";

  /// <summary>
  /// <b>L'avertissement de remplacement</b>, recopié à dessein : il <b>nomme le réglage qui sera
  /// remplacé</b> — le routage —, et sans ce mot l'intégrateur ne saurait pas ce qu'il perd.
  /// </summary>
  private const string ReplacementWarning =
    "Enregistrer une adresse remplacera le routage RabbitMQ actuel : un droit ne porte qu'un seul canal.";

  /// <summary>Le bouton d'enregistrement d'une mini-form, ce que l'avertissement doit précéder.</summary>
  private const string SaveButton = ">Enregistrer</button>";

  /// <summary>Le bouton Effacer d'un droit, tel que le détail le pose à côté d'Enregistrer.</summary>
  private const string ClearButton = ">Effacer le canal</button>";

  /// <summary>L'adresse du bouton Effacer d'un droit : la page, et le gestionnaire d'effacement.</summary>
  private const string Clear = "/parametrage?handler=Clear";

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
  /// article RGPD. La <b>liste</b> les porte tous, dans l'ordre du règlement ; le <b>détail</b> de
  /// chacun dit son article.
  /// </summary>
  [Fact]
  public async Task ListsTheSixRightsEachWithItsFrenchLabelAndArticle()
  {
    // Les libellés portent une apostrophe — « droit d'accès », « droit à l'effacement » —, que le
    // moteur de rendu encode en `&#x27;`. On relit ce que l'humain lit, entités décodées, comme le
    // font les assertions de phrase du layout.
    var list = ListIn(WebUtility.HtmlDecode(await ReadAsync()));

    list.Select(item => item.Label).ShouldBe(TheSixRights.Select(right => Titled(right.Label)));

    foreach (var (_, label, article) in TheSixRights)
    {
      (await SectionAsync(label)).ShouldContain($"Article {article} du RGPD");
    }
  }

  /// <summary>
  /// <b>Sur un service vierge, les six droits s'affichent « non configuré ».</b> Rien n'est semé au
  /// démarrage : tant qu'aucune adresse n'a été enregistrée, les six lignes de la liste portent
  /// l'état « non configuré », et un droit sans URL est dit explicitement tel.
  /// </summary>
  [Fact]
  public async Task ShowsEveryRightAsUnconfiguredOnAVirginService()
  {
    var list = ListIn(WebUtility.HtmlDecode(await ReadAsync()));

    list.Count.ShouldBe(TheSixRights.Length);

    foreach (var item in list)
    {
      item.Contents.ShouldContain("non configuré");
    }
  }

  /// <summary>
  /// <b>Sans droit demandé, le détail ouvre le premier des six</b> — le droit d'accès —, et un nom
  /// que l'écran ignore l'ouvre aussi : l'adresse n'est qu'une lecture, et une faute de frappe n'a
  /// pas à se solder par une erreur.
  /// </summary>
  [Theory]
  [InlineData(Parametrage)]
  [InlineData(Parametrage + "?droit=Profiling")]
  [InlineData(Parametrage + "?droit=OutOfScope")]
  public async Task OpensTheFirstRightWhenTheAddressNamesNoneOfTheSix(string address)
  {
    var response = await _client.GetAsync(address);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    SectionIn(WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()), "droit d'accès");
  }

  /// <summary>
  /// <b>La liste marque le droit ouvert, et lui seul</b> — <c>aria-current</c> à qui écoute la page,
  /// comme le panneau latéral marque l'écran courant.
  /// </summary>
  [Fact]
  public async Task MarksTheOpenRightAndOnlyItInTheList()
  {
    var list = ListIn(WebUtility.HtmlDecode(await ReadAsync("Erasure")));

    list.Where(item => item.IsCurrent).Select(item => item.Label).ShouldBe(["Droit à l'effacement"]);
  }

  /// <summary>
  /// <b>Chaque ligne de la liste mène à la face du canal de son droit</b> : un droit routé se relit
  /// sur la face RabbitMQ, un droit adressé sur la face HTTP, et un droit « non configuré » reste sur
  /// la face courante. Ce sont des liens, que le serveur rend — sans une ligne de JavaScript.
  /// </summary>
  [Fact]
  public async Task LeadsEachRightOfTheListToTheFaceOfItsChannel()
  {
    await SaveAsync("Access", "https://brocanto.example.fr/rgpd/acces");
    await RoutingAsync(DataSubjectRight.Erasure, "rgpd.exercices", "droit.effacement");

    var list = ListIn(WebUtility.HtmlDecode(await ReadAsync())).ToDictionary(item => item.Label, item => item.Address);

    list["Droit d'accès"].ShouldBe("/parametrage?droit=Access");
    list["Droit à l'effacement"].ShouldBe("/parametrage/rabbitmq?droit=Erasure");
    list["Droit de rectification"].ShouldBe("/parametrage?droit=Rectification");

    var fromTheOtherFace = ListIn(WebUtility.HtmlDecode(await ReadRabbitMqAsync()))
      .ToDictionary(item => item.Label, item => item.Address);

    fromTheOtherFace["Droit d'accès"].ShouldBe("/parametrage?droit=Access");
    fromTheOtherFace["Droit de rectification"].ShouldBe("/parametrage/rabbitmq?droit=Rectification");
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
  /// ⚠️ <b>L'ancien écran du Manifest n'existe plus</b>, ni son écran de reprise : la configuration
  /// passe par le seul Paramétrage, et une adresse qu'on aurait gardée en favori ne mène nulle part
  /// plutôt qu'à un second modèle de configuration.
  /// </summary>
  [Theory]
  [InlineData("/manifest")]
  [InlineData("/manifest/boutique")]
  public async Task NoLongerServesTheManifestScreen(string address)
  {
    (await _client.GetAsync(address)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
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
  /// Le refus se dit <b>dans le détail du droit qu'on saisissait</b> — c'est lui que l'écran rouvre —,
  /// et la saisie refusée y reste pour être corrigée : l'intégrateur n'a pas à la retaper, ni à
  /// chercher de quel droit il s'agit.
  /// </summary>
  [Fact]
  public async Task SaysTheRefusalInTheSectionOfTheRightBeingEditedAndKeepsWhatWasTyped()
  {
    var response = await SaveAsync("Objection", "/rgpd/opposition");
    var screen = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

    var objection = SectionIn(screen, "droit d'opposition");

    objection.ShouldContain("n'est pas une URL http ou https absolue");
    objection.ShouldContain("value=\"/rgpd/opposition\"");

    // Le refus se dit une fois, là, et nulle part ailleurs sur l'écran.
    Regex.Matches(screen, "n'est pas une URL").Count.ShouldBe(1);
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
  /// l'écran, <b>le droit écrit ouvert</b>, et recharger la page relit l'état au lieu de renvoyer la
  /// saisie.
  /// </summary>
  [Fact]
  public async Task RedirectsBackToTheScreenAfterSaving()
  {
    var response = await SaveAsync("Erasure", "https://brocanto.example.fr/rgpd/effacement");

    response.StatusCode.ShouldBe(HttpStatusCode.Found);
    response.Headers.Location!.OriginalString.ShouldBe($"{Parametrage}?droit=Erasure");
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

  /// <summary>
  /// Le critère du ticket : <b>effacer l'adresse d'un droit configuré le ramène à « non
  /// configuré »</b>, et l'effacement suit le Post-Redirect-Get — une redirection vers l'écran, que
  /// recharger ne renvoie pas.
  /// </summary>
  [Fact]
  public async Task ClearsTheEndpointOfAConfiguredRightBackToUnconfigured()
  {
    const string Endpoint = "https://brocanto.example.fr/rgpd/effacement";

    await SaveAsync("Erasure", Endpoint);

    var response = await ClearAsync("Erasure");

    response.StatusCode.ShouldBe(HttpStatusCode.Found);
    response.Headers.Location!.OriginalString.ShouldBe($"{Parametrage}?droit=Erasure");

    var erasure = await SectionAsync("droit à l'effacement");

    erasure.ShouldContain("non configuré");
    erasure.ShouldNotContain(Endpoint);
  }

  /// <summary>
  /// <b>Effacer un droit ne touche que ce droit.</b> Les six droits configurés, un seul effacé : les
  /// cinq autres se relisent à l'identique, adresse pour adresse.
  /// </summary>
  [Fact]
  public async Task EachClearTouchesOnlyItsOwnRight()
  {
    foreach (var (name, _, _) in TheSixRights)
    {
      await SaveAsync(name, $"https://brocanto.example.fr/rgpd/{name.ToLowerInvariant()}");
    }

    var before = await SectionsAsync();

    await ClearAsync("Portability");

    var after = await SectionsAsync();

    foreach (var (_, label, _) in TheSixRights.Where(right => right.Label != Portability))
    {
      after[label].ShouldBe(before[label]);
    }

    after[Portability].ShouldContain("non configuré");
  }

  /// <summary>
  /// <b>Effacer n'est offert qu'à un droit configuré</b> : un droit « non configuré » n'a rien à
  /// oublier, et un bouton qui ne ferait rien serait une promesse vide.
  /// </summary>
  [Fact]
  public async Task OffersToClearOnlyARightThatIsConfigured()
  {
    await SaveAsync("Objection", "https://brocanto.example.fr/rgpd/opposition");

    var sections = await SectionsAsync();

    sections["droit d'opposition"].ShouldContain(ClearButton);

    foreach (var (_, label, _) in TheSixRights.Where(right => right.Name != "Objection"))
    {
      sections[label].ShouldNotContain(ClearButton);
    }
  }

  /// <summary>
  /// <b>Effacer un droit déjà « non configuré » ne change rien</b> — même sur un service vierge, où
  /// aucune ligne n'existe encore : l'envoi suit la même redirection, et les six droits restent
  /// « non configuré ». Un double envoi du bouton ne se solde donc pas par une erreur.
  /// </summary>
  [Fact]
  public async Task ClearingARightThatIsNotConfiguredChangesNothing()
  {
    var response = await ClearAsync("Access");

    response.StatusCode.ShouldBe(HttpStatusCode.Found);
    response.Headers.Location!.OriginalString.ShouldBe($"{Parametrage}?droit=Access");

    foreach (var (_, label, _) in TheSixRights)
    {
      (await SectionAsync(label)).ShouldContain("non configuré");
    }
  }

  /// <summary>
  /// ⚠️ <b>Un effacement forgé est refusé en le nommant</b> — <c>OutOfScope</c> compris —, rendu à
  /// l'écran sans redirection, et rien n'est effacé.
  /// </summary>
  [Theory]
  [InlineData("OutOfScope")]
  [InlineData("Profiling")]
  public async Task RefusesToClearARightTheScreenDoesNotConfigure(string right)
  {
    const string Endpoint = "https://brocanto.example.fr/rgpd/acces";

    await SaveAsync("Access", Endpoint);

    var response = await ClearAsync(right);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    response.Headers.Location.ShouldBeNull();
    WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync())
      .ShouldContain($"« {right} » n'est pas un droit du Paramétrage");

    (await SectionAsync("droit d'accès")).ShouldContain(Endpoint);
  }

  /// <summary>
  /// <b>Un effacement sans jeton anti-rejeu est refusé</b>, et l'adresse reste : un site tiers n'a pas
  /// à déconfigurer un droit à la place de l'intégrateur.
  /// </summary>
  [Fact]
  public async Task RefusesAClearThatCarriesNoAntiforgeryToken()
  {
    const string Endpoint = "https://brocanto.example.fr/rgpd/acces";

    await SaveAsync("Access", Endpoint);

    var response = await _client.PostAsync(Clear, new FormUrlEncodedContent([new("Form.Right", "Access")]));

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    (await SectionAsync("droit d'accès")).ShouldContain(Endpoint);
  }

  /// <summary>
  /// <b>Un droit réglé sur l'autre canal montre son routage ici</b> — l'exchange et la routing key,
  /// tels que la face RabbitMQ les porte —, et l'avertissement qui prévient qu'enregistrer une
  /// adresse le remplacera. ⚠️ <b>L'avertissement précède le bouton</b> : lu après, il aurait
  /// prévenu d'un geste déjà fait.
  /// </summary>
  [Fact]
  public async Task ShowsTheRoutingOfARightConfiguredOverRabbitMqAndWarnsBeforeTheSaveButton()
  {
    const string Exchange = "rgpd.exercices";
    const string Key = "droit.acces";

    await RoutingAsync(DataSubjectRight.Access, Exchange, Key);

    var access = Flattened(await SectionAsync("droit d'accès"));

    access.ShouldContain(Exchange);
    access.ShouldContain(Key);
    access.ShouldContain(ReplacementWarning);
    access.IndexOf(ReplacementWarning, StringComparison.Ordinal)
      .ShouldBeLessThan(access.IndexOf(SaveButton, StringComparison.Ordinal));
  }

  /// <summary>
  /// ⚠️ <b>« Non configuré » ne se dit que d'un droit sans adresse NI routage</b> : un droit réglé
  /// sur RabbitMQ est configuré, et l'appeler « non configuré » ici ferait de cette face la seule
  /// du service qui mente sur son état.
  /// </summary>
  [Fact]
  public async Task DoesNotCallARightConfiguredOverRabbitMqUnconfigured()
  {
    await RoutingAsync(DataSubjectRight.Access, "rgpd.exercices", "droit.acces");

    (await SectionAsync("droit d'accès")).ShouldNotContain("non configuré");
  }

  /// <summary>
  /// <b>Après un remplacement, aucune des deux faces ne revendique plus l'ancien canal</b> :
  /// enregistrer une adresse sur un droit qui portait un routage efface ce routage — la face HTTP
  /// montre l'adresse et n'avertit plus, la face RabbitMQ ne montre plus l'exchange ni la routing
  /// key.
  /// </summary>
  [Fact]
  public async Task ReplacesTheRoutingOfARightByAnAddressAndNeitherFaceClaimsTheOldRouting()
  {
    const string Exchange = "rgpd.exercices";
    const string Key = "droit.effacement";
    const string Endpoint = "https://brocanto.example.fr/rgpd/effacement";

    await RoutingAsync(DataSubjectRight.Erasure, Exchange, Key);

    (await SaveAsync("Erasure", Endpoint)).StatusCode.ShouldBe(HttpStatusCode.Found);

    var erasure = Flattened(await SectionAsync("droit à l'effacement"));

    erasure.ShouldContain(Endpoint);
    erasure.ShouldNotContain(Exchange);
    erasure.ShouldNotContain(Key);
    erasure.ShouldNotContain(ReplacementWarning);
    erasure.ShouldNotContain("non configuré");

    var onTheOtherFace = SectionIn(
      WebUtility.HtmlDecode(await ReadRabbitMqAsync("Erasure")), "droit à l'effacement");

    onTheOtherFace.ShouldNotContain(Exchange);
    onTheOtherFace.ShouldNotContain(Key);
    onTheOtherFace.ShouldContain(Endpoint);
  }

  /// <summary>
  /// Pose le routage RabbitMQ d'un droit <b>par le use case</b> : c'est le réglage de l'autre face,
  /// et cette face n'a pas à l'écrire.
  /// </summary>
  private async Task RoutingAsync(DataSubjectRight right, string exchange, string key)
  {
    using var scope = factory.Services.CreateScope();

    var written = await scope.ServiceProvider.GetRequiredService<Mediator.IMediator>().Send(
      new SetRightRabbitMqRoutingCommand(
        right, new RabbitMqRouting(ExchangeName.From(exchange), RoutingKey.From(key))));

    written.IsSuccess.ShouldBeTrue();
  }

  /// <summary>La face RabbitMQ, lue telle quelle : ce que l'autre face revendique encore.</summary>
  private async Task<string> ReadRabbitMqAsync(string? right = null)
  {
    var response = await _client.GetAsync(right is null ? RabbitMq : $"{RabbitMq}?droit={right}");

    response.StatusCode.ShouldBe(HttpStatusCode.OK);

    return await response.Content.ReadAsStringAsync();
  }

  /// <summary>La face HTTP, le droit donné ouvert — le premier des six sans droit donné.</summary>
  private async Task<string> ReadAsync(string? right = null)
  {
    var response = await _client.GetAsync(right is null ? Parametrage : $"{Parametrage}?droit={right}");

    response.StatusCode.ShouldBe(HttpStatusCode.OK);

    return await response.Content.ReadAsStringAsync();
  }

  /// <summary>Le détail d'un droit, ouvert par son adresse et retrouvé par son libellé — entités décodées.</summary>
  private async Task<string> SectionAsync(string label) =>
    SectionIn(WebUtility.HtmlDecode(await ReadAsync(NameOf(label))), label);

  /// <summary>
  /// Les six détails, chacun sous son libellé. Le jeton anti-rejeu en est retiré : il change à
  /// chaque rendu, et deux lectures d'un même état doivent se comparer à l'identique.
  /// </summary>
  private async Task<IReadOnlyDictionary<string, string>> SectionsAsync()
  {
    var sections = new Dictionary<string, string>();

    foreach (var (_, label, _) in TheSixRights)
    {
      sections[label] = Regex.Replace(
        await SectionAsync(label), @"<input name=""__RequestVerificationToken""[^>]*>", string.Empty);
    }

    return sections;
  }

  /// <summary>
  /// ⚠️ <b>L'avertissement ne paraît que pour un droit à remplacer</b> : un droit « non configuré »
  /// n'a rien à perdre, et un droit déjà adressé ne remplace que son propre réglage. Avertir
  /// partout aurait fait lire l'avertissement comme un ornement de la page.
  /// </summary>
  [Fact]
  public async Task WarnsOfNoReplacementWhenThereIsNothingToReplace()
  {
    (await SectionAsync("droit d'accès")).ShouldNotContain(ReplacementWarning);

    (await SaveAsync("Access", "https://brocanto.example.fr/rgpd/acces"))
      .StatusCode.ShouldBe(HttpStatusCode.Found);

    Flattened(await SectionAsync("droit d'accès")).ShouldNotContain(ReplacementWarning);
  }

  /// <summary>
  /// La section, ses blancs de gabarit réduits à une espace : une phrase que le gabarit coupe en
  /// deux lignes reste <b>une</b> phrase, et c'est elle qu'on lit — pas sa mise en page.
  /// </summary>
  private static string Flattened(string section) => Regex.Replace(section, @"\s+", " ");

  /// <summary>
  /// <b>Le détail du droit ouvert</b> — la seule section de droit que l'écran porte —, et l'assurance
  /// que c'est bien celui du libellé donné : un détail qui ouvrirait un autre droit ferait lire ses
  /// réglages sous le mauvais nom.
  /// </summary>
  private static string SectionIn(string screen, string label)
  {
    var section = Regex.Match(screen, @"<section\b[^>]*\bclass=""right""[^>]*>(.*?)</section>", RegexOptions.Singleline);

    section.Success.ShouldBeTrue("L'écran ne porte aucun détail de droit.");
    section.Groups[1].Value.ShouldContain($">{Titled(label)}</h2>");

    return section.Groups[1].Value;
  }

  /// <summary>Le nom canonique d'un droit, celui que l'adresse et sa mini-form portent.</summary>
  private static string NameOf(string label) => TheSixRights.Single(right => right.Label == label).Name;

  /// <summary>Le libellé tel qu'il ouvre une ligne de la liste ou le titre du détail.</summary>
  private static string Titled(string label) => string.Concat(label[..1].ToUpperInvariant(), label[1..]);

  /// <summary>
  /// <b>La liste des droits</b>, dans l'ordre où l'écran la pose : le libellé de chaque ligne, son
  /// adresse, ce qu'elle dit, et le marquage du droit ouvert.
  /// </summary>
  private static IReadOnlyList<(string Label, string Address, string Contents, bool IsCurrent)> ListIn(string screen)
  {
    var list = Regex.Match(screen, @"<nav\b[^>]*\bclass=""rights""[^>]*>(.*?)</nav>", RegexOptions.Singleline);

    list.Success.ShouldBeTrue("L'écran ne porte aucune liste des droits.");

    return
    [
      .. Regex.Matches(list.Groups[1].Value, @"<a\b([^>]*)>(.*?)</a>", RegexOptions.Singleline).Select(link =>
      (
        Label: Regex.Match(link.Groups[2].Value, @"class=""right-name"">(.*?)</span>").Groups[1].Value,
        Address: Regex.Match(link.Groups[1].Value, @"\bhref=""([^""]*)""").Groups[1].Value,
        Contents: link.Groups[2].Value,
        IsCurrent: link.Groups[1].Value.Contains(@"aria-current=""true""", StringComparison.Ordinal)
      )),
    ];
  }

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

  /// <summary>
  /// Envoie le bouton <b>Effacer</b> de la mini-form d'un droit, jeton anti-rejeu compris : l'envoi
  /// ne porte que le droit, jamais d'adresse.
  /// </summary>
  private async Task<HttpResponseMessage> ClearAsync(string right)
  {
    return await _client.PostAsync(Clear, new FormUrlEncodedContent(
    [
      new("__RequestVerificationToken", await AntiforgeryTokenAsync()),
      new("Form.Right", right),
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
