namespace MicroserviceRgpd.BrowserTests.Requests;

/// <summary>
/// <b>L'<c>Operator</c> cherche une demande</b> par son email, son nom ou son prénom, dans un vrai
/// navigateur : le tableau se filtre à chaque frappe, sans rechargement, en ignorant la casse, les
/// accents et les espaces de bord ; le ✕ vide la recherche ; une recherche qui ne trouve rien le dit.
/// </summary>
/// <remarks>
/// <para>
/// Chaque scénario enregistre ses propres demandes, <b>reconnaissables par un marqueur unique</b> :
/// la base est partagée par toute la collection, et d'autres lignes sont sur le tableau. Un scénario
/// ne lit que les siennes.
/// </para>
/// <para>
/// ⚠️ <b>Une ligne cachée ne se trouve pas par son rôle</b> : « cachée » serait vrai aussi d'une ligne
/// qui n'existe pas. Chaque ligne qu'un scénario attend cachée est donc d'abord vue affichée.
/// </para>
/// </remarks>
[Collection(BrowserCollection.Name)]
public class RequestSearch(BrowserHarness harness)
{
  private const string NoMatch = "Aucune demande ne correspond à votre recherche";

  private const string NoRequestYet = "Aucune demande pour le moment";

  /// <summary>
  /// <b>Le tableau se filtre à chaque frappe</b>, sans rechargement : chaque caractère resserre la
  /// liste, sans qu'Entrée soit jamais pressée.
  /// </summary>
  [Fact]
  public async Task FiltersAtEachKeystroke()
  {
    var marker = Marker();
    await harness.RecordRequestAsync(lastName: $"{marker}-alpha", firstName: "Jeanne");
    await harness.RecordRequestAsync(lastName: $"{marker}-alpine", firstName: "Paul");

    await using var context = await harness.NewContextAsync();
    var page = await OnTheBoardAsync(context);
    var loaded = await page.EvaluateAsync<double>("performance.timeOrigin");

    await Search(page).PressSequentiallyAsync($"{marker}-alp");

    await Expect(Row(page, $"{marker}-alpha")).ToBeVisibleAsync();
    await Expect(Row(page, $"{marker}-alpine")).ToBeVisibleAsync();

    await Search(page).PressSequentiallyAsync("h");

    await Expect(Row(page, $"{marker}-alpha")).ToBeVisibleAsync();
    await Expect(Row(page, $"{marker}-alpine")).ToBeHiddenAsync();

    (await page.EvaluateAsync<double>("performance.timeOrigin")).ShouldBe(loaded, "La recherche a rechargé la page.");
  }

  /// <summary>
  /// <b>Une demande reste affichée si son email, son nom ou son prénom contient le texte saisi</b> —
  /// n'importe où, et dans l'un des trois seulement. Une demande qui ne le contient nulle part est
  /// cachée.
  /// </summary>
  [Fact]
  public async Task KeepsTheRequestsWhoseEmailLastNameOrFirstNameContainsTheText()
  {
    var marker = Marker();
    await harness.RecordRequestAsync(email: $"jeanne.{marker}@example.org");
    await harness.RecordRequestAsync(lastName: $"Martin{marker}", firstName: "Paul");
    await harness.RecordRequestAsync(lastName: "Durand", firstName: $"Luc{marker}");
    var other = Marker();
    await harness.RecordRequestAsync(email: $"{other}@example.org");

    await using var context = await harness.NewContextAsync();
    var page = await OnTheBoardAsync(context);
    await Expect(Row(page, other)).ToBeVisibleAsync();

    await Search(page).FillAsync(marker);

    await Expect(Row(page, $"jeanne.{marker}@example.org")).ToBeVisibleAsync();
    await Expect(Row(page, $"Martin{marker}")).ToBeVisibleAsync();
    await Expect(Row(page, $"Luc{marker}")).ToBeVisibleAsync();
    await Expect(Row(page, other)).ToBeHiddenAsync();
  }

  /// <summary>
  /// <b>La casse et les accents sont ignorés, dans les deux sens</b> : « helene » trouve « Hélène »,
  /// « Hélène » trouve « Helene », « DUPONT » trouve « Dupont » et « dupont » trouve « DUPONT ». Une
  /// demande d'une autre personne, elle, est cachée.
  /// </summary>
  [Theory]
  [InlineData("Hélène", "helene")]
  [InlineData("Helene", "Hélène")]
  [InlineData("Dupont", "DUPONT")]
  [InlineData("DUPONT", "dupont")]
  [InlineData("Françoise", "FRANCOISE")]
  public async Task IgnoresCaseAndAccentsBothWays(string recorded, string typed)
  {
    var marker = Marker();
    await harness.RecordRequestAsync(email: $"{marker}@example.org", lastName: "Martin", firstName: recorded);
    var other = Marker();
    await harness.RecordRequestAsync(email: $"{other}@example.org", lastName: "Bernard", firstName: "Jeanne");

    await using var context = await harness.NewContextAsync();
    var page = await OnTheBoardAsync(context);
    await Expect(Row(page, other)).ToBeVisibleAsync();

    await Search(page).FillAsync(typed);

    await Expect(Row(page, marker)).ToBeVisibleAsync();
    await Expect(Row(page, other)).ToBeHiddenAsync();
  }

  /// <summary>
  /// <b>Les espaces en début et en fin de saisie sont ignorés</b> : un texte entouré d'espaces trouve
  /// ce que le texte seul trouve.
  /// </summary>
  [Fact]
  public async Task IgnoresTheSpacesAroundTheText()
  {
    var marker = Marker();
    await harness.RecordRequestAsync(email: $"{marker}@example.org");
    var other = Marker();
    await harness.RecordRequestAsync(email: $"{other}@example.org");

    await using var context = await harness.NewContextAsync();
    var page = await OnTheBoardAsync(context);
    await Expect(Row(page, other)).ToBeVisibleAsync();

    await Search(page).FillAsync($"  {marker}\t ");

    await Expect(Row(page, marker)).ToBeVisibleAsync();
    await Expect(Row(page, other)).ToBeHiddenAsync();
    await Expect(NoMatchMessage(page)).ToBeHiddenAsync();
  }

  /// <summary>
  /// <b>Le ✕ vide la recherche et réaffiche toutes les demandes</b> ; le focus revient au champ, pour
  /// qu'une nouvelle recherche commence aussitôt.
  /// </summary>
  [Fact]
  public async Task ClearingTheSearchShowsEveryRequestAgain()
  {
    var marker = Marker();
    await harness.RecordRequestAsync(email: $"{marker}@example.org");
    var other = Marker();
    await harness.RecordRequestAsync(email: $"{other}@example.org");

    await using var context = await harness.NewContextAsync();
    var page = await OnTheBoardAsync(context);
    await Expect(Row(page, other)).ToBeVisibleAsync();

    await Search(page).FillAsync(marker);
    await Expect(Row(page, other)).ToBeHiddenAsync();

    await ClearButton(page).ClickAsync();

    await Expect(Search(page)).ToHaveValueAsync(string.Empty);
    await Expect(Search(page)).ToBeFocusedAsync();
    await Expect(Row(page, marker)).ToBeVisibleAsync();
    await Expect(Row(page, other)).ToBeVisibleAsync();
  }

  /// <summary>
  /// <b>Une recherche sans résultat le dit</b> : « Aucune demande ne correspond à votre recherche »
  /// paraît à la place des lignes, et s'en va dès que la recherche retrouve une demande.
  /// </summary>
  [Fact]
  public async Task SaysNothingMatchesWhenTheSearchFindsNothing()
  {
    var marker = Marker();
    await harness.RecordRequestAsync(email: $"{marker}@example.org");

    await using var context = await harness.NewContextAsync();
    var page = await OnTheBoardAsync(context);
    await Expect(Row(page, marker)).ToBeVisibleAsync();
    await Expect(NoMatchMessage(page)).ToBeHiddenAsync();

    await Search(page).FillAsync(Marker());

    await Expect(NoMatchMessage(page)).ToBeVisibleAsync();
    await Expect(Row(page, marker)).ToBeHiddenAsync();
    await Expect(page.GetByText(NoRequestYet, new() { Exact = true })).ToBeHiddenAsync();

    await Search(page).FillAsync(marker);

    await Expect(NoMatchMessage(page)).ToBeHiddenAsync();
    await Expect(Row(page, marker)).ToBeVisibleAsync();
  }

  /// <summary>
  /// ⚠️ <b>Sur un tableau vide, une recherche ne dit pas qu'elle ne trouve rien</b> : « Aucune demande
  /// pour le moment » dit déjà qu'il n'y a rien à trouver, et reste seul.
  /// </summary>
  [Fact]
  public async Task SaysOnlyThereIsNoRequestYetOnAnEmptyBoard()
  {
    await harness.DeleteAllRequestsAsync();

    await using var context = await harness.NewContextAsync();
    var page = await OnTheBoardAsync(context);

    await Search(page).FillAsync(Marker());

    await Expect(page.GetByText(NoRequestYet, new() { Exact = true })).ToBeVisibleAsync();
    await Expect(NoMatchMessage(page)).ToBeHiddenAsync();
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

  private static ILocator Search(IPage page)
  {
    return page.GetByRole(AriaRole.Searchbox, new() { Name = "Rechercher une demande", Exact = true });
  }

  private static ILocator ClearButton(IPage page)
  {
    return page.GetByRole(AriaRole.Button, new() { Name = "Vider la recherche", Exact = true });
  }

  /// <summary>
  /// La ligne du tableau qui porte <paramref name="text"/>. Son rôle ne la trouve que si elle est
  /// affichée : cachée, elle ne se distingue pas d'une ligne absente.
  /// </summary>
  private static ILocator Row(IPage page, string text)
  {
    return page.GetByRole(AriaRole.Row).Filter(new() { HasText = text });
  }

  private static ILocator NoMatchMessage(IPage page)
  {
    return page.GetByText(NoMatch, new() { Exact = true });
  }
}
