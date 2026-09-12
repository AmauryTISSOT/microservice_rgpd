namespace MicroserviceRgpd.BrowserTests.Requests;

/// <summary>
/// <b>L'œil d'une ligne ouvre la fiche de sa demande</b>, dans un vrai navigateur : la lecture à
/// l'écran de ce que le service en tient, ancrée au bord droit, en lecture seule — et <b>quatre
/// gestes la referment</b> : « Fermer », la croix, Échap, un clic sur le fond.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Aucune fermeture ne demande de confirmation</b> : contrairement à la modale de saisie, rien
/// n'est en jeu dans une lecture. Et après chacune, le focus revient sur l'œil de la ligne d'où l'on
/// vient : la navigation au clavier reprend là où elle s'était arrêtée.
/// </para>
/// <para>
/// ⚠️ <b>Ce que la fiche porte ne se juge pas ici</b> : les valeurs des cinq blocs sont vides tant
/// que la story suivante ne les y verse pas. Les libellés que le serveur rend se gardent dans
/// <c>RequestsBoardScreen</c>.
/// </para>
/// <para>
/// La fiche ne se cherche pas par son nom accessible — c'est le nom de la personne, que rien n'y
/// écrit encore —, mais par le premier de ses cinq blocs. Chaque demande se retrouve par son email
/// unique, la base étant partagée par toute la collection.
/// </para>
/// </remarks>
[Collection(BrowserCollection.Name)]
public class RequestSheet(BrowserHarness harness)
{
  /// <summary>Le nom accessible de l'œil, recopié à dessein.</summary>
  private const string Eye = "Voir la fiche de la demande";

  /// <summary>Le premier bloc de la fiche, à quoi elle se reconnaît, recopié à dessein.</summary>
  private const string FirstBlock = "La personne";

  /// <summary>Le titre de la confirmation d'abandon : celle qui ne doit jamais se montrer ici.</summary>
  private const string ConfirmationTitle = "Abandonner la saisie ?";

  /// <summary>Les quatre modes de fermeture de la fiche.</summary>
  public static TheoryData<string> ClosingModes { get; } = ["Fermer", "la croix", "Échap", "le fond"];

  /// <summary>
  /// <b>L'œil ouvre la fiche</b>, fermée jusque-là, avec ses cinq blocs titrés.
  /// </summary>
  [Fact]
  public async Task OpensTheSheetOnTheEyeOfTheRow()
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var row = await ARecordedRowAsync(page);

    await Expect(Sheet(page)).ToBeHiddenAsync();

    await EyeOf(row).ClickAsync();

    await Expect(Sheet(page)).ToBeVisibleAsync();

    foreach (var block in new[] { FirstBlock, "La demande", "Le délai", "Le statut", "L'enregistrement" })
    {
      await Expect(Sheet(page).GetByRole(AriaRole.Heading, new() { Name = block, Exact = true })).ToBeVisibleAsync();
    }
  }

  /// <summary>
  /// <b>La fiche est ancrée au bord droit de la fenêtre, sur toute sa hauteur</b> : elle sort du
  /// bord, elle ne se pose pas au centre comme la modale de saisie.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Le bord droit est celui de la page, gouttière de défilement exclue</b> : la feuille de
  /// style la réserve en permanence, et aucune surface de l'écran ne va au-delà.
  /// </remarks>
  [Fact]
  public async Task AnchorsTheSheetToTheRightEdgeAtFullHeight()
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    await page.SetViewportSizeAsync(1280, 720);
    await OpenTheSheetAsync(await ARecordedRowAsync(page));

    var box = await Sheet(page).BoundingBoxAsync() ?? throw new InvalidOperationException("La fiche n'est pas affichée.");
    var width = await page.EvaluateAsync<float>("document.body.clientWidth");
    var height = await page.EvaluateAsync<float>("document.documentElement.clientHeight");
    box.Y.ShouldBe(0, 1, "La fiche ne part pas du haut de la fenêtre.");
    box.Height.ShouldBe(height, 1, "La fiche n'occupe pas toute la hauteur de la fenêtre.");
    (box.X + box.Width).ShouldBe(width, 1, "La fiche n'est pas ancrée au bord droit de la fenêtre.");
    box.X.ShouldBeGreaterThan(width / 2, "La fiche déborde sur la moitié gauche de l'écran.");
  }

  /// <summary>
  /// ⚠️ <b>La fiche s'ouvre instantanément</b> : aucun appel ne part au service. Tout ce qu'elle
  /// montre, la page l'a déjà.
  /// </summary>
  [Fact]
  public async Task OpensWithoutCallingTheService()
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var row = await ARecordedRowAsync(page);
    await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

    var sent = new List<string>();
    page.Request += (_, request) => sent.Add($"{request.Method} {request.Url}");

    await EyeOf(row).ClickAsync();

    await Expect(Sheet(page)).ToBeVisibleAsync();
    sent.ShouldBeEmpty("L'ouverture de la fiche a appelé le service.");
  }

  /// <summary>
  /// <b>Chacun des quatre gestes referme la fiche, sans confirmation</b> : rien n'est en jeu dans une
  /// lecture, et rien ne barre le passage.
  /// </summary>
  [Theory]
  [MemberData(nameof(ClosingModes))]
  public async Task ClosesWithoutAskingAnything(string mode)
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    await OpenTheSheetAsync(await ARecordedRowAsync(page));

    await CloseByAsync(page, mode);

    await Expect(Sheet(page)).ToBeHiddenAsync();
    await Expect(Confirmation(page)).ToBeHiddenAsync();
  }

  /// <summary>
  /// <b>Après chaque fermeture, le focus est sur l'œil de la ligne d'où l'on vient</b> : sans souris,
  /// la navigation reprend là où elle s'était arrêtée.
  /// </summary>
  [Theory]
  [MemberData(nameof(ClosingModes))]
  public async Task GivesTheFocusBackToTheEyeOfTheRow(string mode)
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var row = await ARecordedRowAsync(page);
    await OpenTheSheetAsync(row);

    await CloseByAsync(page, mode);

    await Expect(Sheet(page)).ToBeHiddenAsync();
    await Expect(EyeOf(row)).ToBeFocusedAsync();
  }

  /// <summary>
  /// ⚠️ <b>Cliquer une cellule de la ligne, hors des trois boutons, n'ouvre rien</b> : un email se
  /// sélectionne et se copie sans qu'une fiche s'ouvre.
  /// </summary>
  [Fact]
  public async Task OpensNothingOnAClickInACellOfTheRow()
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var row = await ARecordedRowAsync(page);

    await row.GetByRole(AriaRole.Cell).First.ClickAsync();

    await Expect(Sheet(page)).ToBeHiddenAsync();
  }

  /// <summary>Ouvre la fiche de cette ligne, et attend qu'elle soit là.</summary>
  private static async Task OpenTheSheetAsync(ILocator row)
  {
    await EyeOf(row).ClickAsync();
    await Expect(Sheet(row.Page)).ToBeVisibleAsync();
  }

  /// <summary>
  /// Ferme la fiche par l'un des quatre modes. Le clic sur le fond tombe <b>dans le coin haut gauche
  /// de la fenêtre</b>, loin de la fiche ancrée au bord droit.
  /// </summary>
  private static Task CloseByAsync(IPage page, string mode)
  {
    return mode switch
    {
      "Fermer" => CloseButtonOf(page).ClickAsync(),
      "la croix" => CrossOf(page).ClickAsync(),
      "Échap" => page.Keyboard.PressAsync("Escape"),
      "le fond" => page.Mouse.ClickAsync(5, 5),
      _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Mode de fermeture inconnu."),
    };
  }

  /// <summary>
  /// La sortie nommée du pied : des deux boutons « Fermer » de la fiche, c'est celui qui donne son
  /// libellé à lire — la croix, elle, ne se nomme que pour qui ne la voit pas.
  /// </summary>
  private static ILocator CloseButtonOf(IPage page)
  {
    return WaysOut(page).Filter(new() { HasText = "Fermer" });
  }

  /// <summary>La croix de la tête : le bouton « Fermer » qui ne donne rien à lire.</summary>
  private static ILocator CrossOf(IPage page)
  {
    return WaysOut(page).Filter(new() { HasNotText = "Fermer" });
  }

  private static ILocator WaysOut(IPage page)
  {
    return Sheet(page).GetByRole(AriaRole.Button, new() { Name = "Fermer", Exact = true });
  }

  /// <summary>
  /// La fiche : le seul <c>dialog</c> qui porte les cinq blocs d'une demande. ⚠️ Elle ne se cherche
  /// pas par son nom accessible — c'est le nom de la personne, vide tant que rien ne l'y écrit.
  /// </summary>
  private static ILocator Sheet(IPage page)
  {
    return page
      .GetByRole(AriaRole.Dialog, new() { IncludeHidden = true })
      .Filter(new() { Has = page.GetByRole(AriaRole.Heading, new() { Name = FirstBlock, Exact = true, IncludeHidden = true }) });
  }

  private static ILocator Confirmation(IPage page)
  {
    return page.GetByRole(AriaRole.Alertdialog, new() { Name = ConfirmationTitle, Exact = true, IncludeHidden = true });
  }

  private static ILocator EyeOf(ILocator row)
  {
    return row.GetByRole(AriaRole.Button, new() { Name = Eye, Exact = true });
  }

  /// <summary>
  /// Enregistre une demande <b>par le use case</b>, ouvre le tableau, et rend sa ligne — celle qui
  /// porte son email unique.
  /// </summary>
  private async Task<ILocator> ARecordedRowAsync(IPage page)
  {
    var email = $"{Guid.NewGuid():N}@example.org";

    await harness.RecordRequestAsync(lastName: "Martin", firstName: "Jeanne", email: email);
    await page.GotoAsync("/demandes");

    var row = page.GetByRole(AriaRole.Row).Filter(new() { HasText = email });
    await Expect(row).ToHaveCountAsync(1);

    return row;
  }
}
