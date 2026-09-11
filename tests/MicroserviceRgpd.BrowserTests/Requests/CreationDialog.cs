namespace MicroserviceRgpd.BrowserTests.Requests;

/// <summary>
/// <b>La modale de création s'ouvre</b>, dans un vrai navigateur : l'<c>Operator</c> clique « Créer
/// une demande » sur le tableau des demandes et lit la modale « Créer une nouvelle demande ».
/// </summary>
/// <remarks>
/// <para>
/// Ses quatre modes de fermeture — « Annuler », la croix, Échap, un clic sur le fond — se vérifient
/// dans <see cref="AbandonConfirmation"/>, formulaire modifié ou non. Le formulaire lui-même se
/// vérifie dans <see cref="CreationForm"/>.
/// </para>
/// <para>
/// Tout se lit par le rôle et le nom accessible, comme dans <c>SidepanelToTheBoard</c> ; les
/// libellés sont recopiés à dessein.
/// </para>
/// </remarks>
[Collection(BrowserCollection.Name)]
public class CreationDialog(BrowserHarness harness)
{
  private const string DialogTitle = "Créer une nouvelle demande";

  [Fact]
  public async Task OpensOnTheCreateButton()
  {
    await using var context = await harness.NewContextAsync();
    var page = await OnTheBoardAsync(context);

    await Expect(Dialog(page)).ToBeHiddenAsync();

    await OpenAsync(page);

    await Expect(Dialog(page)).ToBeVisibleAsync();
    await Expect(Dialog(page).GetByRole(AriaRole.Heading, new() { Name = DialogTitle, Exact = true })).ToBeVisibleAsync();
  }

  /// <summary>
  /// ⚠️ <b>Un clic dans la modale ne la ferme pas</b>, même hors de tout bouton : c'est ce qui
  /// distingue le fond de la modale elle-même.
  /// </summary>
  [Fact]
  public async Task StaysOpenOnAClickInsideTheDialog()
  {
    await using var context = await harness.NewContextAsync();
    var page = await OnTheBoardAsync(context);
    await OpenAsync(page);

    await Dialog(page).GetByRole(AriaRole.Heading, new() { Name = DialogTitle, Exact = true }).ClickAsync();

    await Expect(Dialog(page)).ToBeVisibleAsync();
  }

  private static async Task<IPage> OnTheBoardAsync(IBrowserContext context)
  {
    var page = await context.NewPageAsync();
    await page.GotoAsync("/demandes");

    return page;
  }

  private static Task OpenAsync(IPage page)
  {
    return page.GetByRole(AriaRole.Button, new() { Name = "Créer une demande", Exact = true }).ClickAsync();
  }

  private static ILocator Dialog(IPage page)
  {
    return page.GetByRole(AriaRole.Dialog, new() { Name = DialogTitle, Exact = true, IncludeHidden = true });
  }
}
