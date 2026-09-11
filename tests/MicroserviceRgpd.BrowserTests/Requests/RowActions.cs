namespace MicroserviceRgpd.BrowserTests.Requests;

/// <summary>
/// <b>Les trois actions de chaque ligne</b>, dans un vrai navigateur : la poubelle, le crayon et
/// l'œil montrent leur infobulle au survol ; le crayon et l'œil, inertes en attendant leurs US, ne
/// font rien au clic.
/// </summary>
/// <remarks>
/// <para>
/// Chaque test enregistre sa propre demande <b>par la modale de création</b>, reconnaissable à son
/// email unique, puis recharge le tableau : la base est partagée par toute la collection.
/// </para>
/// <para>
/// Les boutons se trouvent par leur rôle et leur nom accessible ; l'infobulle, par le texte qu'elle
/// montre. Ce que le serveur rend — les trois boutons et leurs libellés — se garde dans
/// <c>RequestConsultation</c>.
/// </para>
/// </remarks>
[Collection(BrowserCollection.Name)]
public class RowActions(BrowserHarness harness)
{
  /// <summary>
  /// <b>Chaque action montre son infobulle au survol</b>, et la cache tant qu'on ne la survole pas.
  /// </summary>
  [Theory]
  [InlineData("Supprimer la demande")]
  [InlineData("Modifier la demande")]
  [InlineData("Voir la fiche de la demande")]
  public async Task ShowsItsTooltipOnHover(string action)
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var row = await RowOfANewRequestAsync(page);

    var tooltip = row.GetByText(action, new() { Exact = true });

    await Expect(tooltip).ToBeHiddenAsync();

    await row.GetByRole(AriaRole.Button, new() { Name = action, Exact = true }).HoverAsync();

    await Expect(tooltip).ToBeVisibleAsync();
  }

  /// <summary>
  /// ⚠️ <b>Le crayon et l'œil sont inertes</b> : un clic ne change rien à la page — ni navigation, ni
  /// rechargement, ni nouvel onglet, ni fenêtre ouverte.
  /// </summary>
  [Theory]
  [InlineData("Modifier la demande")]
  [InlineData("Voir la fiche de la demande")]
  public async Task DoesNothingOnAClick(string action)
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var row = await RowOfANewRequestAsync(page);

    var address = page.Url;
    var newPages = 0;
    context.Page += (_, _) => newPages++;

    // Un marqueur posé sur la fenêtre : un rechargement, même vers la même adresse, l'effacerait.
    await page.EvaluateAsync("() => { window.untouched = true; }");

    await row.GetByRole(AriaRole.Button, new() { Name = action, Exact = true }).ClickAsync();

    page.Url.ShouldBe(address, "Le clic a mené ailleurs.");
    (await page.EvaluateAsync<bool>("() => window.untouched === true")).ShouldBeTrue("Le clic a rechargé la page.");
    await Expect(page.GetByRole(AriaRole.Dialog)).ToHaveCountAsync(0);
    await Expect(page.GetByRole(AriaRole.Alertdialog)).ToHaveCountAsync(0);
    newPages.ShouldBe(0, "Le clic a ouvert un onglet.");
  }

  /// <summary>
  /// Enregistre une demande par la modale, recharge le tableau, et rend sa ligne — celle qui porte
  /// son email unique.
  /// </summary>
  private static async Task<ILocator> RowOfANewRequestAsync(IPage page)
  {
    var email = $"{Guid.NewGuid():N}@example.org";

    await page.GotoAsync("/demandes");
    await page.GetByRole(AriaRole.Button, new() { Name = "Créer une demande", Exact = true }).ClickAsync();

    var dialog = page.GetByRole(AriaRole.Dialog, new() { Name = "Créer une nouvelle demande", Exact = true });
    await dialog.GetByLabel("Email", new() { Exact = true }).FillAsync(email);
    await dialog.GetByLabel("Message", new() { Exact = true }).FillAsync($"Je souhaite accéder à mes données. {Guid.NewGuid()}");
    await dialog.GetByLabel("Droits RGPD", new() { Exact = true }).SelectOptionAsync("Access");
    await dialog.GetByRole(AriaRole.Button, new() { Name = "Créer", Exact = true }).ClickAsync();
    await Expect(dialog).ToBeHiddenAsync();

    await page.ReloadAsync();

    var row = page.GetByRole(AriaRole.Row).Filter(new() { HasText = email });
    await Expect(row).ToHaveCountAsync(1);

    return row;
  }
}
