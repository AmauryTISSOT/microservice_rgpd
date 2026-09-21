namespace MicroserviceRgpd.BrowserTests.Requests;

/// <summary>
/// <b>La fiche d'une demande se tient à l'écran</b>, dans un vrai navigateur : sur une fiche longue,
/// sur un message immense, sur un écran étroit, et sous un système qui demande que rien ne bouge.
/// </summary>
/// <remarks>
/// <para>
/// Ce qui se mesure ici ne se lit pas dans le HTML : c'est la place que le navigateur donne à la
/// fiche, et le chemin qu'elle parcourt en s'ouvrant. Ce que la fiche <b>porte</b>, en revanche, se
/// garde dans <c>RequestSheet</c> ; aucun scénario n'est écrit deux fois.
/// </para>
/// <para>
/// ⚠️ <b>Le message immense est posé à même la table</b> : 10 000 caractères sont ce qu'une personne
/// écrit quand elle raconte son affaire, pas ce qu'un scénario saisit dans la modale.
/// </para>
/// <para>
/// La base est partagée par toute la collection : chaque scénario enregistre la demande qu'il lit et
/// la retrouve par son email unique.
/// </para>
/// </remarks>
[Collection(BrowserSheetCollection.Name)]
public class SheetOnScreen(BrowserHarness harness)
{
  /// <summary>Le nom accessible de l'œil, recopié à dessein.</summary>
  private const string Eye = "Voir la fiche de la demande";

  /// <summary>Le premier bloc de la fiche, à quoi elle se reconnaît, recopié à dessein.</summary>
  private const string FirstBlock = "La personne";

  /// <summary>
  /// La part de la fenêtre que la zone du message ne dépasse pas — les 40 vh de la feuille de style,
  /// recopiés à dessein.
  /// </summary>
  private const float MessageZone = 0.4f;

  /// <summary>Le pixel de tolérance : une hauteur de bloc s'arrondit, et un pixel d'écart ne dit rien.</summary>
  private const float APixel = 1;

  /// <summary>Ce qu'une personne écrit quand elle raconte son affaire : bien plus que la fiche n'en montre.</summary>
  private static readonly string HugeMessage = string.Join(
    "\n",
    Enumerable.Repeat("Je souhaite accéder à l'ensemble des données que vous détenez sur moi.", 140));

  /// <summary>
  /// ⚠️ <b>Seuls les blocs défilent</b> : amener le dernier fait en vue laisse la tête et le pied à
  /// leur place — le titre, la croix et « Fermer » restent lisibles, et la page ne bouge pas. Sinon
  /// les quatre modes de fermeture se réduiraient à trois pour qui n'a pas de clavier.
  /// </summary>
  [Fact]
  public async Task KeepsTheHeadAndTheFootInSightWhileTheBlocksScroll()
  {
    await using var context = await harness.NewContextAsync();
    var page = await BoardAsync(context, 1280, 720);
    await OpenTheSheetOfAHugeMessageAsync(page);

    await Sheet(page).GetByRole(AriaRole.Definition).Last.ScrollIntoViewIfNeededAsync();

    await Expect(Sheet(page).GetByRole(AriaRole.Definition).Last).ToBeInViewportAsync();
    await Expect(CloseButtonOf(page)).ToBeInViewportAsync(new() { Ratio = 1 });
    await Expect(CrossOf(page)).ToBeInViewportAsync(new() { Ratio = 1 });
    await Expect(TitleOf(page)).ToBeInViewportAsync(new() { Ratio = 1 });
    (await page.EvaluateAsync<double>("window.scrollY")).ShouldBe(0, "La page a défilé, pas les blocs de la fiche.");
  }

  /// <summary>
  /// <b>« Fermer » reste au bas de la fiche, même quand la demande est courte</b> : les blocs prennent
  /// la hauteur laissée par la tête et le pied, et le pied ne remonte pas sous le dernier bloc.
  /// </summary>
  [Fact]
  public async Task KeepsTheFootAtTheBottomOfTheSheetForAShortRequest()
  {
    await using var context = await harness.NewContextAsync();
    var page = await BoardAsync(context, 1280, 1400);
    await OpenTheSheetOfAShortMessageAsync(page);

    var sheet = await BoxOfAsync(Sheet(page));
    var close = await BoxOfAsync(CloseButtonOf(page));

    (sheet.Y + sheet.Height - (close.Y + close.Height)).ShouldBeLessThan(
      64, "« Fermer » n'est pas au bas de la fiche : le pied est remonté sous le dernier bloc.");
  }

  /// <summary>
  /// <b>Le message défile dans sa propre zone</b>, de hauteur bornée à 40 vh : le budget à répartir
  /// est celui de la fenêtre, et un message de 10 000 caractères n'en prend pas davantage.
  /// </summary>
  [Fact]
  public async Task BoundsTheMessageToItsOwnScrollingZone()
  {
    const int height = 720;
    await using var context = await harness.NewContextAsync();
    var page = await BoardAsync(context, 1280, height);
    await OpenTheSheetOfAHugeMessageAsync(page);

    var shown = await MessageOf(page).EvaluateAsync<float>("p => p.clientHeight");
    var written = await MessageOf(page).EvaluateAsync<float>("p => p.scrollHeight");

    shown.ShouldBeLessThanOrEqualTo(MessageZone * height + APixel, "La zone du message dépasse les 40 vh.");
    written.ShouldBeGreaterThan(shown, "Le message tient dans sa zone : rien ne prouve qu'il y défilerait.");
    await Expect(MessageOf(page)).ToHaveCSSAsync("overflow-y", "auto");
  }

  /// <summary>
  /// ⚠️ <b>Un message immense ne repousse ni le statut ni l'enregistrement hors de vue</b> : il
  /// écarte ce qui le suit de sa propre zone, et de rien de plus.
  /// </summary>
  [Fact]
  public async Task PushesNeitherTheStatusNorTheRecordFurtherThanItsOwnZone()
  {
    const int height = 720;
    await using var context = await harness.NewContextAsync();
    var page = await BoardAsync(context, 1280, height);

    await OpenTheSheetOfAShortMessageAsync(page);
    var (statusBelow, recordBelow) = await DistancesUnderTheMessageAsync(page);
    await CloseAsync(page);

    await OpenTheSheetOfAHugeMessageAsync(page);
    var (statusPushedTo, recordPushedTo) = await DistancesUnderTheMessageAsync(page);

    (statusPushedTo - statusBelow).ShouldBeLessThanOrEqualTo(
      MessageZone * height + APixel,
      "Le message immense repousse le statut de plus que sa propre zone.");
    (recordPushedTo - recordBelow).ShouldBeLessThanOrEqualTo(
      MessageZone * height + APixel,
      "Le message immense repousse l'enregistrement de plus que sa propre zone.");
  }

  /// <summary>
  /// <b>La fiche glisse depuis le bord droit</b> : une image après le clic, elle est encore en route
  /// vers la place qu'elle prendra.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Le clic part du navigateur lui-même</b>, et la première image qui suit est lue dans la
  /// foulée : depuis le test, aucun aller-retour n'arriverait assez tôt pour surprendre un glissement
  /// de 150 ms.
  /// </remarks>
  [Fact]
  public async Task SlidesInFromTheRightEdge()
  {
    await using var context = await harness.NewContextAsync();
    var page = await BoardAsync(context, 1280, 720);

    var onTheWay = await XOfTheSheetOneFrameAfterTheClickAsync(page);
    var settled = await XOfTheSettledSheetAsync(page);

    onTheWay.ShouldBeGreaterThan(settled + APixel, "La fiche est arrivée sans glisser depuis le bord droit.");
  }

  /// <summary>
  /// ⚠️ <b>Sous un système qui demande que rien ne bouge, la fiche est là d'emblée</b> : elle ne
  /// glisse pas, elle n'est pas non plus plus lente à venir.
  /// </summary>
  [Fact]
  public async Task CancelsTheSlideUnderReducedMotion()
  {
    await using var context = await harness.NewContextAsync(ReducedMotion.Reduce);
    var page = await BoardAsync(context, 1280, 720);

    var onTheWay = await XOfTheSheetOneFrameAfterTheClickAsync(page);
    var settled = await XOfTheSettledSheetAsync(page);

    onTheWay.ShouldBe(settled, APixel, "La fiche glisse encore alors que le système demande que rien ne bouge.");
  }

  /// <summary>
  /// <b>Sous 600 px, la fiche prend toute la largeur</b> : un message se lit sur une colonne
  /// utilisable, et non sur la moitié d'un écran de téléphone.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Toute la largeur est celle de la page, gouttière de défilement exclue</b> : la feuille de
  /// style la réserve en permanence, et aucune surface de l'écran ne va au-delà.
  /// </remarks>
  [Fact]
  public async Task TakesTheWholeWidthOnANarrowWindow()
  {
    await using var context = await harness.NewContextAsync();
    var page = await BoardAsync(context, 390, 800);
    await OpenTheSheetOfAShortMessageAsync(page);

    var box = await BoxOfAsync(Sheet(page));
    var width = await page.EvaluateAsync<float>("document.body.clientWidth");

    box.X.ShouldBe(0, APixel, "La fiche laisse une bande à sa gauche sur un écran étroit.");
    box.Width.ShouldBe(width, APixel, "La fiche ne prend pas toute la largeur d'un écran étroit.");
  }

  /// <summary>
  /// ⚠️ <b>Au-dessus du seuil, la fiche reste une fiche</b> : elle laisse voir le tableau à sa
  /// gauche, et ne prend la largeur entière que sur un écran étroit.
  /// </summary>
  [Fact]
  public async Task KeepsTheTableInSightAboveTheThreshold()
  {
    await using var context = await harness.NewContextAsync();
    var page = await BoardAsync(context, 700, 800);
    await OpenTheSheetOfAShortMessageAsync(page);

    var box = await BoxOfAsync(Sheet(page));

    box.X.ShouldBeGreaterThan(APixel, "La fiche prend toute la largeur au-dessus du seuil des 600 px.");
  }

  /// <summary>
  /// L'abscisse de la fiche <b>une image après le clic sur l'œil</b> : le clic et la lecture partent
  /// du navigateur, dans la même tâche, pour attraper le glissement à son début.
  /// </summary>
  private async Task<float> XOfTheSheetOneFrameAfterTheClickAsync(IPage page)
  {
    var row = await ARecordedRowOfAShortMessageAsync(page);
    var eye = await EyeOf(row).ElementHandleAsync();

    return await Sheet(page).EvaluateAsync<float>(
      """
      (sheet, eye) => {
        eye.click();

        return new Promise(resolve => requestAnimationFrame(() => resolve(sheet.getBoundingClientRect().x)));
      }
      """,
      eye);
  }

  /// <summary>L'abscisse de la fiche une fois arrivée : la place qu'elle garde.</summary>
  private static async Task<float> XOfTheSettledSheetAsync(IPage page)
  {
    await Expect(Sheet(page)).ToBeVisibleAsync();
    await WaitForTheSheetToSettleAsync(page);

    return (await BoxOfAsync(Sheet(page))).X;
  }

  /// <summary>
  /// Ce qui sépare le haut du message du haut du statut, puis de l'enregistrement : de combien le
  /// message écarte ce qui le suit.
  /// </summary>
  private static async Task<(float Status, float Record)> DistancesUnderTheMessageAsync(IPage page)
  {
    var distances = await page.EvaluateAsync<float[]>(
      """
      () => {
        const top = name => document.querySelector(`[data-field="${name}"]`).getBoundingClientRect().top;

        return [top("status") - top("message"), top("createdAt") - top("message")];
      }
      """);

    return (distances[0], distances[1]);
  }

  private async Task OpenTheSheetOfAShortMessageAsync(IPage page)
  {
    await OpenTheSheetAsync(await ARecordedRowOfAShortMessageAsync(page));
  }

  private async Task OpenTheSheetOfAHugeMessageAsync(IPage page)
  {
    var email = UniqueEmail();
    var message = await harness.RecordRequestAsync(lastName: "Martin", firstName: "Jeanne", email: email);

    await harness.SetMessageAsync(message, HugeMessage);

    await OpenTheSheetAsync(await OpenedRowOfAsync(page, email));
  }

  private async Task<ILocator> ARecordedRowOfAShortMessageAsync(IPage page)
  {
    var email = UniqueEmail();

    await harness.RecordRequestAsync(lastName: "Martin", firstName: "Jeanne", email: email);

    return await OpenedRowOfAsync(page, email);
  }

  /// <summary>Ouvre le tableau à cette taille de fenêtre, et attend ses lignes.</summary>
  private static async Task<IPage> BoardAsync(IBrowserContext context, int width, int height)
  {
    var page = await context.NewPageAsync();
    await page.SetViewportSizeAsync(width, height);

    return page;
  }

  /// <summary>Ouvre le tableau, et rend la ligne de la demande qui porte cet email.</summary>
  private static async Task<ILocator> OpenedRowOfAsync(IPage page, string email)
  {
    await page.GotoAsync("/demandes");
    var row = page.GetByRole(AriaRole.Row).Filter(new() { HasText = email });
    await Expect(row).ToHaveCountAsync(1);

    return row;
  }

  private static async Task OpenTheSheetAsync(ILocator row)
  {
    await EyeOf(row).ClickAsync();
    await Expect(Sheet(row.Page)).ToBeVisibleAsync();
    await WaitForTheSheetToSettleAsync(row.Page);
  }

  /// <summary>
  /// Attend que la fiche soit <b>arrivée</b> : le glissement de l'ouverture dure 150 ms, et une
  /// mesure prise en chemin ne dit rien de la place que la fiche prend.
  /// </summary>
  private static async Task WaitForTheSheetToSettleAsync(IPage page)
  {
    await Sheet(page).EvaluateAsync("sheet => Promise.all(sheet.getAnimations().map(slide => slide.finished))");
  }

  /// <summary>Referme la fiche par Échap, et attend qu'elle soit partie — glissement compris.</summary>
  private static async Task CloseAsync(IPage page)
  {
    await page.Keyboard.PressAsync("Escape");
    await Expect(Sheet(page)).ToBeHiddenAsync();
  }

  private static async Task<LocatorBoundingBoxResult> BoxOfAsync(ILocator locator)
  {
    return await locator.BoundingBoxAsync() ?? throw new InvalidOperationException("La surface n'est pas affichée.");
  }

  private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

  private static ILocator MessageOf(IPage page) => Sheet(page).Locator("[data-field='message']");

  private static ILocator TitleOf(IPage page) => Sheet(page).GetByRole(AriaRole.Heading).First;

  private static ILocator CloseButtonOf(IPage page) => WaysOut(page).Filter(new() { HasText = "Fermer" });

  private static ILocator CrossOf(IPage page) => WaysOut(page).Filter(new() { HasNotText = "Fermer" });

  private static ILocator WaysOut(IPage page)
  {
    return Sheet(page).GetByRole(AriaRole.Button, new() { Name = "Fermer", Exact = true });
  }

  /// <summary>
  /// La fiche : le seul <c>dialog</c> qui porte le bloc « La personne ». ⚠️ Elle ne se cherche
  /// pas par son nom accessible — c'est le nom de la personne, vide tant que rien ne l'y écrit.
  /// </summary>
  private static ILocator Sheet(IPage page)
  {
    return page
      .GetByRole(AriaRole.Dialog, new() { IncludeHidden = true })
      .Filter(new() { Has = page.GetByRole(AriaRole.Heading, new() { Name = FirstBlock, Exact = true, IncludeHidden = true }) });
  }

  private static ILocator EyeOf(ILocator row)
  {
    return row.GetByRole(AriaRole.Button, new() { Name = Eye, Exact = true });
  }
}
