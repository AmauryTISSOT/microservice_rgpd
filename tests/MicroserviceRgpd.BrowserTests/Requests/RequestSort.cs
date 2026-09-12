using System.Text.RegularExpressions;

namespace MicroserviceRgpd.BrowserTests.Requests;

/// <summary>
/// <b>L'<c>Operator</c> trie les demandes par date de réception</b>, dans un vrai navigateur : le menu
/// en haut à droite du tableau réordonne les lignes sans rechargement, départage deux demandes reçues
/// le même jour par leur date de création, et ordonne les résultats de la recherche en cours.
/// </summary>
/// <remarks>
/// <para>
/// Chaque scénario enregistre ses propres demandes, <b>reconnaissables par un marqueur unique</b>, et
/// ne lit l'ordre que des siennes : la base est partagée par toute la collection, et d'autres lignes
/// s'intercalent entre elles.
/// </para>
/// <para>
/// ⚠️ <b>Les demandes sont créées dans un ordre qui n'est pas celui de leur réception</b> : un tri sur
/// la seule date de création, ou l'ordre d'insertion, donnerait un autre ordre que celui attendu.
/// </para>
/// </remarks>
[Collection(BrowserCollection.Name)]
public class RequestSort(BrowserHarness harness)
{
  private const string MostRecent = "Date de réception la plus récente";

  private const string Oldest = "Date de réception la plus ancienne";

  /// <summary>
  /// <b>« La plus ancienne » puis « la plus récente » réordonnent les lignes</b>, sans rechargement.
  /// À l'ouverture, « la plus récente » est sélectionnée, et les lignes sont déjà dans son ordre.
  /// </summary>
  [Fact]
  public async Task ReordersTheRowsFromTheOldestThenFromTheMostRecent()
  {
    var marker = Marker();
    await harness.RecordRequestAsync(lastName: $"Douze-{marker}", firstName: "A", receivedOn: "2026-01-12");
    await harness.RecordRequestAsync(lastName: $"Dix-{marker}", firstName: "B", receivedOn: "2026-01-10");
    await harness.RecordRequestAsync(lastName: $"Onze-{marker}", firstName: "C", receivedOn: "2026-01-11");

    await using var context = await harness.NewContextAsync();
    var page = await OnTheBoardAsync(context);
    var loaded = await page.EvaluateAsync<double>("performance.timeOrigin");

    await Expect(SortMenu(page)).ToHaveValueAsync("newest");
    await ExpectOrderAsync(page, marker, "Douze", "Onze", "Dix");

    await SortMenu(page).SelectOptionAsync(new SelectOptionValue { Label = Oldest });

    await ExpectOrderAsync(page, marker, "Dix", "Onze", "Douze");

    await SortMenu(page).SelectOptionAsync(new SelectOptionValue { Label = MostRecent });

    await ExpectOrderAsync(page, marker, "Douze", "Onze", "Dix");
    (await page.EvaluateAsync<double>("performance.timeOrigin")).ShouldBe(loaded, "Le tri a rechargé la page.");
  }

  /// <summary>
  /// <b>Deux demandes reçues le même jour sont départagées par leur date de création</b>, dans le
  /// sens du tri choisi : la plus récemment créée d'abord quand la réception la plus récente vient
  /// d'abord, la première créée d'abord dans l'autre sens.
  /// </summary>
  /// <remarks>
  /// La demande reçue le lendemain est créée <b>entre</b> les deux autres : un tri sur la seule date
  /// de création la rendrait au milieu.
  /// </remarks>
  [Fact]
  public async Task BreaksATieOnTheReceptionDayByTheCreationInstantInBothDirections()
  {
    var marker = Marker();
    await harness.RecordRequestAsync(lastName: $"Premiere-{marker}", firstName: "A", receivedOn: "2026-01-20");
    await harness.RecordRequestAsync(lastName: $"Lendemain-{marker}", firstName: "B", receivedOn: "2026-01-21");
    await harness.RecordRequestAsync(lastName: $"Derniere-{marker}", firstName: "C", receivedOn: "2026-01-20");

    await using var context = await harness.NewContextAsync();
    var page = await OnTheBoardAsync(context);

    await ExpectOrderAsync(page, marker, "Lendemain", "Derniere", "Premiere");

    await SortMenu(page).SelectOptionAsync(new SelectOptionValue { Label = Oldest });

    await ExpectOrderAsync(page, marker, "Premiere", "Derniere", "Lendemain");

    await SortMenu(page).SelectOptionAsync(new SelectOptionValue { Label = MostRecent });

    await ExpectOrderAsync(page, marker, "Lendemain", "Derniere", "Premiere");
  }

  /// <summary>
  /// ⚠️ <b>Avec une recherche active, le tri ordonne les résultats, sans réafficher les lignes
  /// cachées</b>. La recherche vidée, chaque ligne qu'elle cachait reparaît à sa place dans l'ordre
  /// choisi.
  /// </summary>
  [Fact]
  public async Task SortsTheResultsOfTheSearchWithoutShowingTheHiddenRows()
  {
    var marker = Marker();
    await harness.RecordRequestAsync(email: $"{marker}-trouvee-douze@example.org", receivedOn: "2026-01-12");
    await harness.RecordRequestAsync(email: $"{marker}-ecartee-onze@example.org", receivedOn: "2026-01-11");
    await harness.RecordRequestAsync(email: $"{marker}-trouvee-dix@example.org", receivedOn: "2026-01-10");

    await using var context = await harness.NewContextAsync();
    var page = await OnTheBoardAsync(context);
    await ExpectOrderAsync(page, marker, "trouvee-douze", "ecartee-onze", "trouvee-dix");

    await Search(page).FillAsync($"{marker}-trouvee");
    await ExpectOrderAsync(page, marker, "trouvee-douze", "trouvee-dix");

    await SortMenu(page).SelectOptionAsync(new SelectOptionValue { Label = Oldest });

    await ExpectOrderAsync(page, marker, "trouvee-dix", "trouvee-douze");
    await Expect(Rows(page, "ecartee-onze")).ToHaveCountAsync(0);

    await Search(page).FillAsync(string.Empty);

    await ExpectOrderAsync(page, marker, "trouvee-dix", "ecartee-onze", "trouvee-douze");
  }

  /// <summary>
  /// <b>Le menu de tri est en haut à droite du tableau</b> : au-dessus de son cadre, sur la ligne de
  /// la recherche, et aligné sur son bord droit.
  /// </summary>
  [Fact]
  public async Task SitsAtTheTopRightOfTheTable()
  {
    await harness.RecordRequestAsync(email: $"{Marker()}@example.org");

    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    await page.SetViewportSizeAsync(1280, 720);
    await page.GotoAsync("/demandes");

    var menu = await BoxOfAsync(SortMenu(page));
    var search = await BoxOfAsync(Search(page));
    var frame = await BoxOfAsync(page.GetByRole(AriaRole.Region, new() { Name = "Liste des demandes", Exact = true }));

    (menu.Y + menu.Height).ShouldBeLessThanOrEqualTo(frame.Y, "Le menu de tri n'est pas au-dessus du tableau.");
    (menu.X + menu.Width).ShouldBe(frame.X + frame.Width, 1, "Le menu de tri n'est pas aligné sur le bord droit du tableau.");
    menu.X.ShouldBeGreaterThan(search.X + search.Width, "Le menu de tri n'est pas à droite de la recherche.");
    (menu.Y + (menu.Height / 2)).ShouldBe(search.Y + (search.Height / 2), 1, "Le menu de tri n'est pas sur la ligne de la recherche.");
  }

  /// <summary>
  /// Attend que les lignes affichées qui portent <paramref name="marker"/> se lisent dans cet ordre,
  /// chacune reconnue par ce qui la distingue.
  /// </summary>
  private static Task ExpectOrderAsync(IPage page, string marker, params string[] distinctive)
  {
    return Expect(Rows(page, marker)).ToHaveTextAsync(
      distinctive.Select(text => new Regex(Regex.Escape(text))).ToArray());
  }

  private static async Task<LocatorBoundingBoxResult> BoxOfAsync(ILocator locator)
  {
    return await locator.BoundingBoxAsync() ?? throw new InvalidOperationException("L'élément n'est pas affiché.");
  }

  private static string Marker()
  {
    return Guid.NewGuid().ToString("N");
  }

  private static async Task<IPage> OnTheBoardAsync(IBrowserContext context)
  {
    var page = await context.NewPageAsync();
    await page.GotoAsync("/demandes");

    return page;
  }

  private static ILocator SortMenu(IPage page)
  {
    return page.GetByRole(AriaRole.Combobox, new() { Name = "Trier par", Exact = true });
  }

  private static ILocator Search(IPage page)
  {
    return page.GetByRole(AriaRole.Searchbox, new() { Name = "Rechercher une demande", Exact = true });
  }

  /// <summary>
  /// Les lignes affichées du tableau qui portent <paramref name="text"/>, dans l'ordre du tableau.
  /// Une ligne cachée ne s'y trouve pas.
  /// </summary>
  private static ILocator Rows(IPage page, string text)
  {
    return page.GetByRole(AriaRole.Row).Filter(new() { HasText = text });
  }
}
