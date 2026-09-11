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
/// <b>Sous le bouton, le tableau des demandes enregistrées</b> : ces tests gardent qu'il est là,
/// seul ; ce qu'il rend se garde dans <c>RequestConsultation</c>.
/// </para>
/// <para>
/// ⚠️ <b>Le bouton ouvre une modale, que le serveur rend avec l'écran</b> : un <c>dialog</c>
/// natif, fermé au chargement, titré « Créer une nouvelle demande ». Ce qu'il fait au clic se
/// vérifie dans un vrai navigateur (<c>MicroserviceRgpd.BrowserTests</c>) ; ce qui est gardé ici est
/// ce que le serveur rend. Sa place — en haut à droite du contenu, et non dans le header
/// (ADR-0009) — se vérifie à l'œil ; ce qui est gardé ici est qu'il vit dans le <c>main</c> et
/// nulle part ailleurs. La confirmation d'abandon d'une saisie est rendue de même, fermée, à côté
/// de la modale.
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

  /// <summary>Le titre de la confirmation d'abandon, recopié à dessein.</summary>
  private const string ConfirmationTitle = "Abandonner la saisie ?";

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

    ButtonsIn(OutsideTheDialogs(LayoutSurface.MainOf(rendered)))
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
    var outside = OutsideTheDialogs(LayoutSurface.MainOf(await _layout.ReadAsync(Board)));
    var button = ButtonsIn(outside).ShouldHaveSingleItem();

    button.Attributes.ShouldContain(@"type=""button""", Case.Sensitive, "Le bouton de création n'est pas un simple bouton.");
    button.Attributes.ShouldNotContain("formaction", Case.Insensitive, "Le bouton de création envoie quelque part.");

    outside.ShouldNotContain("<form", Case.Insensitive, "L'écran porte un formulaire hors de la modale.");
  }

  /// <summary>
  /// <b>Hors des modales, l'écran porte son titre, son bouton, puis le tableau des demandes</b> — un
  /// seul tableau, et ni liste ni lien : les demandes se lisent dans le tableau et nulle part
  /// ailleurs. Ce que le tableau rend se garde dans <c>RequestConsultation</c>.
  /// </summary>
  [Fact]
  public async Task CarriesTheTitleTheButtonThenTheTableOfRequests()
  {
    var outside = OutsideTheDialogs(LayoutSurface.MainOf(await _layout.ReadAsync(Board)));

    LayoutSurface.TextIn(outside).ShouldStartWith(
      $"{ScreenName} {CreateLabel}", Case.Sensitive, "Le contenu de l'écran ne s'ouvre plus sur son titre et son bouton.");

    Regex.Matches(outside, @"<table\b").Count.ShouldBe(1, "L'écran ne porte pas un tableau des demandes, un seul.");

    foreach (var element in new[] { "<ul", "<ol", "<a " })
    {
      outside.ShouldNotContain(element, Case.Insensitive, $"L'écran porte un {element}>, hors du tableau des demandes.");
    }
  }

  /// <summary>
  /// <b>Les deux modales sont rendues par le serveur</b>, une fois chacune, dans le contenu de
  /// l'écran : la création, puis la confirmation d'abandon. Ce sont des <c>dialog</c> natifs,
  /// <b>fermés au chargement</b> — l'<c>Operator</c> les ouvre, la page ne les ouvre jamais pour
  /// lui —, dont le nom accessible est le titre.
  /// </summary>
  [Fact]
  public async Task RendersBothDialogsClosedAndNamedByTheirTitle()
  {
    var dialogs = Dialogs.Matches(LayoutSurface.MainOf(await _layout.ReadAsync(Board)));

    dialogs.Select(NameOf).ShouldBe(
      [DialogTitle, ConfirmationTitle],
      "Le tableau doit porter la modale de création puis la confirmation d'abandon, une fois chacune.");

    dialogs.ShouldNotContain(
      dialog => Regex.IsMatch(dialog.Groups["attributes"].Value, @"\bopen\b"), "Une modale est ouverte au chargement.");
  }

  /// <summary>
  /// <b>La confirmation d'abandon dit ce qu'elle coûte</b> : une modale d'alerte, décrite par
  /// « Les informations saisies seront perdues. », qui porte « Abandonner » puis « Continuer la
  /// saisie ». Aucun des deux ne soumet quoi que ce soit.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>« Continuer la saisie » a le focus par défaut</b>, et lui seul : une touche Entrée réflexe
  /// ne détruit rien. C'est le serveur qui le déclare ; le navigateur l'honore à l'ouverture.
  /// </remarks>
  [Fact]
  public async Task RendersTheAbandonConfirmationWithContinueFocusedByDefault()
  {
    var confirmation = DialogNamed(LayoutSurface.MainOf(await _layout.ReadAsync(Board)), ConfirmationTitle);
    var attributes = confirmation.Groups["attributes"].Value;

    attributes.ShouldContain(@"role=""alertdialog""", Case.Sensitive, "La confirmation ne se présente pas comme une alerte.");

    var describedBy = Regex.Match(attributes, @"aria-describedby=""([^""]+)""");
    describedBy.Success.ShouldBeTrue("La confirmation ne se décrit pas.");

    var description = Regex.Match(
      confirmation.Value, $@"<p\b[^>]*\bid=""{Regex.Escape(describedBy.Groups[1].Value)}""[^>]*>(.*?)</p>", RegexOptions.Singleline);
    description.Success.ShouldBeTrue("La confirmation n'est pas décrite par sa phrase.");
    LayoutSurface.TextIn(description.Groups[1].Value).ShouldBe("Les informations saisies seront perdues.");

    var buttons = ButtonsIn(confirmation.Value);

    buttons
      .Select(button => LayoutSurface.TextIn(button.Contents))
      .ShouldBe(["Abandonner", "Continuer la saisie"], "La confirmation ne porte pas « Abandonner » puis « Continuer la saisie ».");

    buttons.ShouldAllBe(
      button => button.Attributes.Contains(@"type=""button""", StringComparison.Ordinal),
      "Un bouton de la confirmation soumet quelque chose.");

    buttons
      .Where(button => Regex.IsMatch(button.Attributes, @"\bautofocus\b"))
      .Select(button => LayoutSurface.TextIn(button.Contents))
      .ShouldBe(["Continuer la saisie"], "« Continuer la saisie » n'a pas le focus par défaut, ou ne l'a pas seul.");
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
    return DialogNamed(main, DialogTitle).Value;
  }

  /// <summary>La modale qui porte ce nom accessible, une seule.</summary>
  private static Match DialogNamed(string main, string name)
  {
    return Dialogs.Matches(main).Where(dialog => NameOf(dialog) == name)
      .ShouldHaveSingleItem($"Le tableau ne porte pas la modale « {name} », une fois.");
  }

  /// <summary>
  /// Le nom accessible d'une modale : le texte du titre que son <c>aria-labelledby</c> désigne, ou
  /// <see langword="null"/> si elle ne se nomme pas par un titre qu'elle porte.
  /// </summary>
  private static string? NameOf(Match dialog)
  {
    var labelledBy = Regex.Match(dialog.Groups["attributes"].Value, @"aria-labelledby=""([^""]+)""");

    if (!labelledBy.Success)
    {
      return null;
    }

    var title = Regex.Match(
      dialog.Groups["contents"].Value,
      $@"<h2\b[^>]*\bid=""{Regex.Escape(labelledBy.Groups[1].Value)}""[^>]*>(.*?)</h2>",
      RegexOptions.Singleline);

    return title.Success ? LayoutSurface.TextIn(title.Groups[1].Value) : null;
  }

  /// <summary>Le contenu de l'écran, les modales retirées : ce que l'Operator lit tant qu'il n'en a ouvert aucune.</summary>
  private static string OutsideTheDialogs(string main)
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
