namespace MicroserviceRgpd.BrowserTests.Requests;

/// <summary>
/// <b>Les trois actions de chaque ligne</b>, dans un vrai navigateur : la poubelle, le crayon et
/// l'œil montrent leur infobulle au survol.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Ce que font le crayon et l'œil ne se juge pas ici</b> : cette classe dit ce que la
/// <i>ligne</i> offre, et l'ouverture d'une surface appartient au scénario de cette surface — voir
/// <see cref="ModificationDialog"/> pour la modale, <see cref="RequestSheet"/> pour la fiche.
/// </para>
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
