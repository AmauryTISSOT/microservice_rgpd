using System.Text.RegularExpressions;

namespace MicroserviceRgpd.BrowserTests.Navigation;

/// <summary>
/// <b>Le panneau latéral mène au tableau des demandes</b>, dans un vrai navigateur : l'<c>Operator</c>
/// part de l'accueil, clique l'entrée « Tableau des demandes RGPD », et arrive sur un écran qui porte
/// ce nom et le bouton « Créer une demande ».
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Tout se lit comme l'<c>Operator</c> le lit</b> : par le rôle et le nom accessible — un lien,
/// un titre, un bouton —, jamais par une classe CSS ni par la forme du DOM. Une retouche de la
/// feuille ou du gabarit qui laisse l'écran dire la même chose ne doit rien casser ici.
/// </para>
/// <para>
/// Les libellés sont <b>recopiés à dessein</b>, comme dans les tests fonctionnels : un test qui lit
/// la constante qu'il vérifie ne vérifie plus rien.
/// </para>
/// </remarks>
[Collection(BrowserCollection.Name)]
public class SidepanelToTheBoard(BrowserHarness harness)
{
  /// <summary>Le nom de l'écran, que porte aussi son entrée du panneau, recopié à dessein.</summary>
  private const string ScreenName = "Tableau des demandes RGPD";

  [Fact]
  public async Task LeadsFromTheDoorstepToTheBoardAndItsCreateButton()
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    await page.GotoAsync("/");

    await page
      .GetByRole(AriaRole.Navigation, new() { Name = "Points d'entrée" })
      .GetByRole(AriaRole.Link, new() { Name = ScreenName, Exact = true })
      .ClickAsync();

    await Expect(page).ToHaveURLAsync(new Regex("/demandes$"));
    await Expect(page.GetByRole(AriaRole.Heading, new() { Name = ScreenName, Exact = true, Level = 1 })).ToBeVisibleAsync();
    await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Créer une demande", Exact = true })).ToBeVisibleAsync();
  }
}
