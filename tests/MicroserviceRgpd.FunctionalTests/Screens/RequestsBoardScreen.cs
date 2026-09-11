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
/// ⚠️ <b>Le bouton ouvre une modale, que le serveur rend avec l'écran</b> : un <c>dialog</c>
/// natif, fermé au chargement, titré « Créer une nouvelle demande ». Ce qu'il fait au clic se
/// vérifie dans un vrai navigateur (<c>MicroserviceRgpd.BrowserTests</c>) ; ce qui est gardé ici est
/// ce que le serveur rend. Sa place — en haut à droite du contenu, et non dans le header
/// (ADR-0009) — se vérifie à l'œil ; ce qui est gardé ici est qu'il vit dans le <c>main</c> et
/// nulle part ailleurs.
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

  /// <summary>Le titre de la modale de création, recopié à dessein.</summary>
  private const string DialogTitle = "Créer une nouvelle demande";

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

    ButtonsIn(OutsideTheDialog(LayoutSurface.MainOf(rendered)))
      .Select(button => LayoutSurface.TextIn(button.Contents))
      .ShouldBe([CreateLabel], "Le contenu ne porte pas le bouton « Créer une demande », une fois.");

    LayoutSurface.TextIn(LayoutSurface.HeaderIn(rendered)).ShouldNotContain(
      CreateLabel, Case.Sensitive, "Le bouton de création s'est glissé dans le header.");
  }

  /// <summary>
  /// ⚠️ <b>Le bouton de création est un simple bouton</b>, hors de tout formulaire, qui ne soumet rien
  /// et ne mène nulle part : c'est le module qui ouvre la modale. Un bouton sans type, dans un
  /// formulaire, enverrait ce formulaire au premier clic.
  /// </summary>
  [Fact]
  public async Task KeepsTheCreateButtonASimpleButton()
  {
    var outside = OutsideTheDialog(LayoutSurface.MainOf(await _layout.ReadAsync(Board)));
    var button = ButtonsIn(outside).ShouldHaveSingleItem();

    button.Attributes.ShouldContain(@"type=""button""", Case.Sensitive, "Le bouton de création n'est pas un simple bouton.");
    button.Attributes.ShouldNotContain("formaction", Case.Insensitive, "Le bouton de création envoie quelque part.");

    outside.ShouldNotContain("<form", Case.Insensitive, "L'écran porte un formulaire hors de la modale.");
  }

  /// <summary>
  /// ⚠️ <b>Rien d'autre que le titre et le bouton</b> hors de la modale : ni liste, ni tableau, ni
  /// lien, ni phrase « aucune demande ». Ce qui se lit dans le contenu, la modale mise à part, est
  /// exactement le nom de l'écran puis le libellé du bouton.
  /// </summary>
  [Fact]
  public async Task CarriesNothingButTheTitleAndTheButton()
  {
    var outside = OutsideTheDialog(LayoutSurface.MainOf(await _layout.ReadAsync(Board)));

    LayoutSurface.TextIn(outside).ShouldBe(
      $"{ScreenName} {CreateLabel}", "Le contenu de l'écran porte autre chose que son titre et son bouton.");

    foreach (var element in new[] { "<table", "<ul", "<ol", "<a " })
    {
      outside.ShouldNotContain(element, Case.Insensitive, $"L'écran porte un {element}>, qu'aucune demande ne remplit encore.");
    }
  }

  /// <summary>
  /// <b>La modale de création est rendue par le serveur</b>, une fois, dans le contenu de l'écran :
  /// un <c>dialog</c> natif, <b>fermé au chargement</b> — l'<c>Operator</c> l'ouvre, la page ne
  /// l'ouvre jamais pour lui —, dont le nom accessible est son titre.
  /// </summary>
  [Fact]
  public async Task RendersTheCreationDialogClosedAndNamedByItsTitle()
  {
    var main = LayoutSurface.MainOf(await _layout.ReadAsync(Board));

    Dialogs.Matches(main).ShouldHaveSingleItem("Le tableau doit porter une modale de création, une seule.");

    var dialog = Dialogs.Match(main);
    var attributes = dialog.Groups["attributes"].Value;
    var contents = dialog.Groups["contents"].Value;

    Regex.IsMatch(attributes, @"\bopen\b").ShouldBeFalse("La modale est ouverte au chargement.");

    var labelledBy = Regex.Match(attributes, @"aria-labelledby=""([^""]+)""");
    labelledBy.Success.ShouldBeTrue("La modale ne se nomme pas par son titre.");

    var title = Regex.Match(contents, $@"<h2\b[^>]*\bid=""{Regex.Escape(labelledBy.Groups[1].Value)}""[^>]*>(.*?)</h2>", RegexOptions.Singleline);
    title.Success.ShouldBeTrue("Le titre de la modale n'est pas celui qui la nomme.");
    LayoutSurface.TextIn(title.Groups[1].Value).ShouldBe(DialogTitle);
  }

  /// <summary>
  /// <b>La modale porte ses boutons</b> : la croix, qui se nomme « Fermer » pour qui ne la voit
  /// pas, la qualification du droit par IA au fil du formulaire, puis « Annuler » et « Créer ».
  /// Aucun ne soumet quoi que ce soit : c'est le module qui enverra la demande.
  /// </summary>
  [Fact]
  public async Task RendersTheCrossTheAiQualificationTheCancelAndTheCreateButtonsInTheDialog()
  {
    var dialog = DialogIn(LayoutSurface.MainOf(await _layout.ReadAsync(Board)));
    var buttons = ButtonsIn(dialog);

    buttons
      .Select(button => Regex.Match(button.Attributes, @"aria-label=""([^""]+)""") is { Success: true } label
        ? label.Groups[1].Value
        : LayoutSurface.TextIn(button.Contents))
      .ShouldBe(
        ["Fermer", "Qualification du droit par IA", "Annuler", "Créer"],
        "La modale ne porte pas la croix, la qualification par IA, « Annuler » et « Créer », dans cet ordre.");

    buttons.ShouldAllBe(
      button => button.Attributes.Contains(@"type=""button""", StringComparison.Ordinal),
      "Un bouton de la modale soumet quelque chose.");
  }

  /// <summary>
  /// <b>Plus rien ne mène à l'ancien tableau</b> : ni le panneau, ni les cartes de l'accueil. Les
  /// écrans de <c>/dossiers</c> sont retirés et rendent un 404 : un lien vers eux mènerait au vide.
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

  /// <summary>Une modale entière, balise ouvrante comprise : ses attributs, puis ce qu'elle porte.</summary>
  private static readonly Regex Dialogs =
    new(@"<dialog\b(?<attributes>[^>]*)>(?<contents>.*?)</dialog>", RegexOptions.Singleline);

  /// <summary>La modale de création, entière — balise ouvrante comprise.</summary>
  private static string DialogIn(string main)
  {
    var dialog = Dialogs.Match(main);

    dialog.Success.ShouldBeTrue("Le tableau ne porte pas de modale de création.");

    return dialog.Value;
  }

  /// <summary>Le contenu de l'écran, la modale retirée : ce que l'Operator lit tant qu'il ne l'a pas ouverte.</summary>
  private static string OutsideTheDialog(string main)
  {
    return Dialogs.Replace(main, string.Empty);
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
