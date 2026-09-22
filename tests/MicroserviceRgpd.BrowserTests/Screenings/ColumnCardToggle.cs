using System.Globalization;
using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.BrowserTests.Screenings;

/// <summary>
/// <b>La fiche d'une colonne tranchée</b>, dans un vrai navigateur : repliée sur son issue, elle ne se
/// rouvre que par son bouton « Annuler », et se replie par « Fermer ».
/// </summary>
/// <remarks>
/// ⚠️ <b>Le clic sur l'issue est donné à la souris, à ses coordonnées</b>, et non par
/// <c>ClickAsync</c> : le résumé ne reçoit plus la souris, et Playwright refuserait de cliquer un
/// élément qui ne la reçoit pas — ce qui est précisément ce qu'on vérifie.
/// </remarks>
[Collection(BrowserCollection.Name)]
public class ColumnCardToggle(BrowserHarness harness)
{
  /// <summary>
  /// <b>Cliquer l'issue ne rouvre pas la fiche</b> : seul « Annuler » la déplie sur ses deux
  /// boutons, et « Fermer » la replie sans rien trancher.
  /// </summary>
  [Fact]
  public async Task OpensASettledCardOnlyThroughItsCancelButton()
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();

    await DepositAsync(page);
    await page.GotoAsync("/detection/table?schema=public&table=adherents");

    var card = page.Locator("details.column-card#colonne-email");
    await Expect(card).ToHaveAttributeAsync("open", "");

    await card.GetByRole(AriaRole.Button, new() { Name = "Retenir", Exact = true }).ClickAsync();
    await Expect(card.Locator("summary .settled")).ToHaveTextAsync("retenue");
    await Expect(card).Not.ToHaveAttributeAsync("open", "");

    var outcome = card.Locator("summary .settled");
    var box = (await outcome.BoundingBoxAsync()).ShouldNotBeNull();
    await page.Mouse.ClickAsync(box.X + (box.Width / 2), box.Y + (box.Height / 2));

    (await card.EvaluateAsync<bool>("card => card.open")).ShouldBeFalse("Cliquer l'issue a rouvert la fiche.");

    await card.GetByText("Annuler", new() { Exact = true }).ClickAsync();
    (await card.EvaluateAsync<bool>("card => card.open")).ShouldBeTrue("« Annuler » n'a pas rouvert la fiche.");

    await card.GetByText("Fermer", new() { Exact = true }).ClickAsync();
    (await card.EvaluateAsync<bool>("card => card.open")).ShouldBeFalse("« Fermer » n'a pas replié la fiche.");
  }

  /// <summary>Dépose un relevé d'une table, où « email » est signalée, par l'écran de dépôt.</summary>
  private static async Task DepositAsync(IPage page)
  {
    var generatedOn = new DateTimeOffset(2026, 8, 10, 9, 30, 0, TimeSpan.Zero).ToString("O", CultureInfo.InvariantCulture);
    string[] lines =
    [
      $$"""{"format":"{{ColumnListing.FormatVersion}}","dialecte":"postgresql","base":"galette_prod","genere_le":"{{generatedOn}}"}""",
      """{"schema":"public","table":"adherents","colonne":"email","position":1,"type":"varchar(255)","nullable":true,"commentaire_colonne":null,"commentaire_table":null,"table_referencee":null}""",
      """{"schema":"public","table":"adherents","colonne":"montant","position":2,"type":"numeric(10,2)","nullable":true,"commentaire_colonne":null,"commentaire_table":null,"table_referencee":null}""",
      """{"fin":true,"colonnes":2}""",
    ];

    await page.GotoAsync("/detection/depot");
    await page.Locator("#paste").FillAsync(string.Join('\n', lines));
    await page.GetByRole(AriaRole.Button, new() { Name = "Déposer et détecter", Exact = true }).ClickAsync();
    await page.WaitForURLAsync("**/detection");
  }
}
