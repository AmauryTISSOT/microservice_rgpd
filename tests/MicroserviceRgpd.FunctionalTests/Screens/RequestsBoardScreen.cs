using System.Text.RegularExpressions;
using MicroserviceRgpd.FunctionalTests.Layout;

namespace MicroserviceRgpd.FunctionalTests.Screens;

/// <summary>
/// Le <b>tableau des demandes RGPD</b> du contexte <c>Requests</c>, exercé par sa <b>seule frontière
/// HTTP</b>. L'<c>Operator</c> y arrive depuis le panneau ou l'accueil, lit le nom de l'écran, et
/// trouve en haut à droite du contenu le bouton « Créer une demande ».
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>L'écran ne porte RIEN D'AUTRE, et c'est ce que ces tests gardent d'abord</b> — ni liste,
/// ni message « aucune demande ». La raison est consignée dans le gabarit de l'écran.
/// </para>
/// <para>
/// ⚠️ <b>Le bouton est encore inerte</b> : la modale qu'il ouvrira arrive avec le ticket suivant.
/// Il ne soumet donc aucun formulaire et ne mène nulle part. Sa place — en haut à droite du
/// contenu, et non dans le header (ADR-0009) — se vérifie à l'œil ; ce qui est gardé ici est qu'il
/// vit dans le <c>main</c> et nulle part ailleurs.
/// </para>
/// <para>
/// Le marquage de l'entrée courante et les libellés du panneau, eux, sont gardés pour tous les
/// écrans à la fois par <see cref="SharedLayout"/>.
/// </para>
/// </remarks>
[Collection(WebCollection.Name)]
public class RequestsBoardScreen(CustomWebApplicationFactory<Program> factory)
{
  private const string Board = "/demandes";

  /// <summary>Le nom que l'écran porte en titre, en onglet et dans la barre, recopié à dessein.</summary>
  private const string ScreenName = "Tableau des demandes RGPD";

  /// <summary>Le libellé du bouton, recopié à dessein.</summary>
  private const string CreateLabel = "Créer une demande";

  private readonly LayoutSurface _layout = new(factory);

  /// <summary>
  /// <b>L'écran répond à <c>/demandes</c> et porte son nom</b> — en onglet et en unique titre de
  /// premier niveau.
  /// </summary>
  [Fact]
  public async Task AnswersAtItsAddressAndCarriesItsName()
  {
    var rendered = await _layout.ReadAsync(Board);

    rendered.ShouldContain($"<title>{ScreenName} —", Case.Sensitive);

    Regex.Matches(LayoutSurface.MainOf(rendered), @"<h1\b[^>]*>(.*?)</h1>", RegexOptions.Singleline)
      .Select(heading => LayoutSurface.TextIn(heading.Groups[1].Value))
      .ShouldBe([ScreenName], "L'écran ne porte pas son nom en unique titre.");
  }

  /// <summary>
  /// <b>Le bouton « Créer une demande » vit dans le contenu de la page</b>, une fois, et pas dans le
  /// header : l'ADR-0009 ne pose aucune action dans la barre du service.
  /// </summary>
  [Fact]
  public async Task CarriesTheCreateButtonInTheContentAndNotInTheHeader()
  {
    var rendered = await _layout.ReadAsync(Board);

    ButtonsIn(LayoutSurface.MainOf(rendered))
      .Select(button => LayoutSurface.TextIn(button.Contents))
      .ShouldBe([CreateLabel], "Le contenu ne porte pas le bouton « Créer une demande », une fois.");

    LayoutSurface.TextIn(LayoutSurface.HeaderIn(rendered)).ShouldNotContain(
      CreateLabel, Case.Sensitive, "Le bouton de création s'est glissé dans le header.");
  }

  /// <summary>
  /// ⚠️ <b>Le bouton est inerte</b> : un simple bouton, hors de tout formulaire, qui ne soumet rien
  /// et ne mène nulle part. Un bouton sans type, dans un formulaire, enverrait ce formulaire au
  /// premier clic.
  /// </summary>
  [Fact]
  public async Task KeepsTheCreateButtonInertForNow()
  {
    var main = LayoutSurface.MainOf(await _layout.ReadAsync(Board));
    var button = ButtonsIn(main).ShouldHaveSingleItem();

    button.Attributes.ShouldContain(@"type=""button""", Case.Sensitive, "Le bouton de création n'est pas un simple bouton.");
    button.Attributes.ShouldNotContain("formaction", Case.Insensitive, "Le bouton de création envoie quelque part.");

    main.ShouldNotContain("<form", Case.Insensitive, "L'écran porte un formulaire, que rien n'envoie encore.");
  }

  /// <summary>
  /// ⚠️ <b>Rien d'autre que le titre et le bouton</b> : ni liste, ni tableau, ni lien, ni phrase
  /// « aucune demande ». Ce qui se lit dans le contenu est exactement le nom de l'écran puis le
  /// libellé du bouton.
  /// </summary>
  [Fact]
  public async Task CarriesNothingButTheTitleAndTheButton()
  {
    var main = LayoutSurface.MainOf(await _layout.ReadAsync(Board));

    LayoutSurface.TextIn(main).ShouldBe(
      $"{ScreenName} {CreateLabel}", "Le contenu de l'écran porte autre chose que son titre et son bouton.");

    foreach (var element in new[] { "<table", "<ul", "<ol", "<a " })
    {
      main.ShouldNotContain(element, Case.Insensitive, $"L'écran porte un {element}>, qu'aucune demande ne remplit encore.");
    }
  }

  /// <summary>
  /// <b>Plus rien ne mène à l'ancien tableau</b> : ni le panneau, ni les cartes de l'accueil. Les
  /// écrans de <c>/dossiers</c> répondent encore jusqu'à leur retrait, mais plus rien n'y conduit.
  /// </summary>
  [Fact]
  public async Task NothingLeadsToTheFormerQueueAnymore()
  {
    var doorstep = await _layout.ReadAsync(LayoutSurface.Doorstep);

    IEnumerable<string> addresses =
    [
      .. LayoutSurface.LinksIn(LayoutSurface.SidepanelIn(doorstep)).Select(link => link.Address),
      .. LayoutSurface.LinkBlocksIn(LayoutSurface.MainOf(doorstep)).Select(link => link.Address),
    ];

    addresses.ShouldNotContain(
      address => address.StartsWith("/dossiers", StringComparison.Ordinal),
      "Un lien de l'accueil mène encore au tableau des dossiers.");
  }

  private static IReadOnlyList<(string Attributes, string Contents)> ButtonsIn(string fragment)
  {
    return
    [
      .. Regex.Matches(fragment, @"<button\b([^>]*)>(.*?)</button>", RegexOptions.Singleline)
        .Select(button => (button.Groups[1].Value, button.Groups[2].Value)),
    ];
  }
}
