namespace MicroserviceRgpd.BrowserTests.Requests;

/// <summary>
/// <b>Le tableau des demandes occupe l'écran et garde ses repères</b>, dans un vrai navigateur : il
/// prend toute la largeur et toute la hauteur sous le bouton « Créer une demande », seul son corps
/// défile — l'en-tête des colonnes et ce qui est au-dessus du tableau restent en vue —, et sur un
/// écran étroit il défile horizontalement sans que la page le fasse.
/// </summary>
/// <remarks>
/// <para>
/// Ce qui se mesure ici ne se lit pas dans le HTML : c'est la place que le navigateur donne au
/// tableau. Le tableau est lu par sa <b>région</b> — « Liste des demandes » — et ses en-têtes de
/// colonne, comme l'<c>Operator</c> les atteint ; jamais par une classe CSS.
/// </para>
/// <para>
/// La base est partagée par toute la collection : chaque scénario enregistre assez de demandes pour
/// déborder, sans compter sur celles des autres.
/// </para>
/// </remarks>
[Collection(BrowserCollection.Name)]
public class BoardFrame(BrowserHarness harness)
{
  /// <summary>Assez de lignes pour déborder de n'importe quelle fenêtre de ces scénarios.</summary>
  private const int Overflowing = 40;

  /// <summary>
  /// La marge tolérée entre le bord du tableau et celui de la fenêtre : la gouttière du contenu et
  /// celle de la barre de défilement, pas davantage.
  /// </summary>
  private const float Gutter = 56;

  /// <summary>
  /// ⚠️ <b>Seul le corps du tableau défile</b> : amener la dernière ligne en vue laisse l'en-tête des
  /// colonnes et le bouton « Créer une demande » à leur place, et la page ne bouge pas.
  /// </summary>
  [Fact]
  public async Task KeepsTheColumnHeadersAndTheButtonInSightWhileTheBodyScrolls()
  {
    await harness.RecordRequestsAsync(Overflowing);
    await using var context = await harness.NewContextAsync();
    var page = await BoardAsync(context, 1280, 720);

    await Rows(page).Last.ScrollIntoViewIfNeededAsync();

    await Expect(Rows(page).Last).ToBeInViewportAsync();
    await Expect(Rows(page).First).Not.ToBeInViewportAsync();
    await Expect(Header(page, "Email")).ToBeInViewportAsync(new() { Ratio = 1 });
    await Expect(CreateButton(page)).ToBeInViewportAsync(new() { Ratio = 1 });
    (await page.EvaluateAsync<double>("window.scrollY")).ShouldBe(0, "La page a défilé, pas le corps du tableau.");
  }

  /// <summary>
  /// <b>Le tableau occupe la largeur et la hauteur disponibles sous le bouton</b> — et pas plus : une
  /// longue liste ne le fait pas sortir de la fenêtre.
  /// </summary>
  [Fact]
  public async Task FillsTheWidthAndTheHeightUnderTheButton()
  {
    const int width = 1600;
    const int height = 900;
    await harness.RecordRequestsAsync(Overflowing);
    await using var context = await harness.NewContextAsync();
    var page = await BoardAsync(context, width, height);

    var frame = await BoxOfAsync(Frame(page));
    var button = await BoxOfAsync(CreateButton(page));
    var title = await BoxOfAsync(page.GetByRole(AriaRole.Heading, new() { Name = "Tableau des demandes RGPD", Level = 1 }));

    frame.Y.ShouldBeGreaterThan(button.Y + button.Height, "Le tableau n'est pas sous le bouton.");
    (height - (frame.Y + frame.Height)).ShouldBeInRange(0, Gutter, "Le tableau n'occupe pas la hauteur disponible.");
    (width - (frame.X + frame.Width)).ShouldBeInRange(0, Gutter, "Le tableau n'occupe pas la largeur disponible.");
    frame.X.ShouldBe(title.X, 1, "Le tableau ne part pas du bord gauche du contenu.");
    (button.X + button.Width).ShouldBe(frame.X + frame.Width, 1, "Le tableau ne s'étend pas jusque sous le bouton.");
  }

  /// <summary>
  /// <b>Sur un écran étroit, le tableau défile horizontalement</b> : toutes les colonnes, jusqu'à
  /// celle des actions, restent atteignables, et la page elle-même ne défile pas de côté.
  /// </summary>
  [Fact]
  public async Task ScrollsSidewaysOnANarrowWindowWhileThePageDoesNot()
  {
    await harness.RecordRequestsAsync(1);
    await using var context = await harness.NewContextAsync();
    var page = await BoardAsync(context, 640, 800);

    (await Frame(page).EvaluateAsync<bool>("frame => frame.scrollWidth > frame.clientWidth"))
      .ShouldBeTrue("Le tableau tient dans un écran étroit : rien ne prouve qu'il y défilerait.");
    await Expect(Header(page, "Statut")).Not.ToBeInViewportAsync();

    var lastColumn = page.GetByRole(AriaRole.Columnheader).Last;
    await lastColumn.ScrollIntoViewIfNeededAsync();

    // Pas un ratio de 1 : la dernière colonne affleure le bord du cadre, qui la rogne d'une fraction de pixel.
    await Expect(lastColumn).ToBeInViewportAsync(new() { Ratio = 0.95f });
    await Expect(Header(page, "Statut")).ToBeInViewportAsync(new() { Ratio = 1 });
    (await page.EvaluateAsync<bool>("document.documentElement.scrollWidth > document.documentElement.clientWidth"))
      .ShouldBeFalse("La page défile horizontalement.");
    (await page.EvaluateAsync<double>("window.scrollX")).ShouldBe(0, "La page a défilé de côté, pas le tableau.");
  }

  /// <summary>
  /// ⚠️ <b>La région qui défile s'atteint au clavier</b> : sans souris, la tabulation passe du bouton
  /// « Créer une demande » au tableau, que le clavier fait alors défiler.
  /// </summary>
  [Fact]
  public async Task LetsTheKeyboardReachAndScrollTheTable()
  {
    await harness.RecordRequestsAsync(Overflowing);
    await using var context = await harness.NewContextAsync();
    var page = await BoardAsync(context, 1280, 720);

    await CreateButton(page).FocusAsync();
    await page.Keyboard.PressAsync("Tab");

    await Expect(Frame(page)).ToBeFocusedAsync();

    await page.Keyboard.PressAsync("End");

    await Expect(Rows(page).Last).ToBeInViewportAsync();
    await Expect(Header(page, "Email")).ToBeInViewportAsync(new() { Ratio = 1 });
  }

  private static async Task<IPage> BoardAsync(IBrowserContext context, int width, int height)
  {
    var page = await context.NewPageAsync();
    await page.SetViewportSizeAsync(width, height);
    await page.GotoAsync("/demandes");

    return page;
  }

  private static async Task<LocatorBoundingBoxResult> BoxOfAsync(ILocator locator)
  {
    return await locator.BoundingBoxAsync() ?? throw new InvalidOperationException("L'élément n'est pas affiché.");
  }

  private static ILocator Frame(IPage page)
  {
    return page.GetByRole(AriaRole.Region, new() { Name = "Liste des demandes", Exact = true });
  }

  private static ILocator Rows(IPage page)
  {
    return Frame(page).GetByRole(AriaRole.Rowgroup).Last.GetByRole(AriaRole.Row);
  }

  private static ILocator Header(IPage page, string name)
  {
    return Frame(page).GetByRole(AriaRole.Columnheader, new() { Name = name, Exact = true });
  }

  private static ILocator CreateButton(IPage page)
  {
    return page.GetByRole(AriaRole.Button, new() { Name = "Créer une demande", Exact = true });
  }
}
