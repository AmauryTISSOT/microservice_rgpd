using System.Net;
using System.Text.RegularExpressions;
using MicroserviceRgpd.FunctionalTests.Layout;

namespace MicroserviceRgpd.FunctionalTests.Screens;

/// <summary>
/// L'écran du <c>Settings</c> — le <b>Paramétrage</b> —, exercé par sa <b>seule frontière HTTP</b>.
/// Un intégrateur arrivant sur un service vierge y voit les <b>six droits RGPD d'emblée</b>, chacun
/// avec son libellé français et son article, tous « non configuré ». C'est la colonne vertébrale
/// verticale du Paramétrage, ici <b>en lecture seule</b> — la saisie et l'effacement viennent après.
/// </summary>
/// <remarks>
/// Ce que ces tests gardent n'est pas la mise en page : c'est que les six droits paraissent, que
/// <see cref="Core.SharedKernel.DataSubjectRight.OutOfScope"/> ne paraît <b>jamais</b>, qu'un service
/// vierge les dit tous « non configuré » sans qu'aucune ligne n'ait été semée, et que l'écran
/// <b>énumère sans compter</b> — aucun agrégat, aucun ratio « 4/6 ».
/// </remarks>
[Collection(WebCollection.Name)]
public class ParametrageScreen(CustomWebApplicationFactory<Program> factory)
{
  private const string Parametrage = "/parametrage";

  /// <summary>Le nom que l'écran porte en titre et en tête, recopié à dessein.</summary>
  private const string ScreenName = "Paramétrage du microservice RGPD";

  /// <summary>Le nom <b>que la barre</b> donne au même écran, recopié à dessein lui aussi.</summary>
  private const string NavigationLabel = "Paramétrage";

  /// <summary>
  /// <b>Les six droits, leur libellé français et leur article</b>, recopiés à dessein : un test qui
  /// lirait le SmartEnum qu'il vérifie ne vérifierait plus rien. C'est l'ordre du règlement — 15, 16,
  /// 17, 18, 20, 21 — et l'énumération est <b>non contiguë</b> : l'art. 19 n'ouvre aucun droit, l'art.
  /// 22 est hors périmètre.
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

    foreach (var (label, article) in TheSixRights)
    {
      screen.ShouldContain(label);
      screen.ShouldContain($"Article {article}");
    }
  }

  /// <summary>
  /// <b>Sur un service vierge, les six droits s'affichent « non configuré ».</b> Rien n'est semé au
  /// démarrage, et rien dans ce lot ne sait écrire une adresse : les six sections portent donc
  /// l'état « non configuré », et un droit sans URL est dit explicitement tel.
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

  private async Task<string> ReadAsync()
  {
    var response = await _client.GetAsync(Parametrage);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);

    return await response.Content.ReadAsStringAsync();
  }
}
