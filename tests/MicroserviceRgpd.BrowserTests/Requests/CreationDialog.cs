namespace MicroserviceRgpd.BrowserTests.Requests;

/// <summary>
/// <b>La modale de création s'ouvre et se ferme</b>, dans un vrai navigateur : l'<c>Operator</c>
/// clique « Créer une demande » sur le tableau des demandes, lit la modale « Créer une nouvelle
/// demande », et la referme par l'un des quatre modes de fermeture qu'un navigateur offre — « Annuler », la
/// croix, Échap, ou un clic sur le fond.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Tant qu'aucune saisie n'est possible, chacun des quatre ferme directement.</b> La
/// confirmation d'abandon viendra avec son propre ticket, en même temps que la saisie qui la rend
/// nécessaire.
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

  [Fact]
  public async Task ClosesOnCancel()
  {
    await using var context = await harness.NewContextAsync();
    var page = await OnTheBoardAsync(context);
    await OpenAsync(page);

    await Dialog(page).GetByRole(AriaRole.Button, new() { Name = "Annuler", Exact = true }).ClickAsync();

    await Expect(Dialog(page)).ToBeHiddenAsync();
  }

  [Fact]
  public async Task ClosesOnTheCross()
  {
    await using var context = await harness.NewContextAsync();
    var page = await OnTheBoardAsync(context);
    await OpenAsync(page);

    await Dialog(page).GetByRole(AriaRole.Button, new() { Name = "Fermer", Exact = true }).ClickAsync();

    await Expect(Dialog(page)).ToBeHiddenAsync();
  }

  [Fact]
  public async Task ClosesOnEscape()
  {
    await using var context = await harness.NewContextAsync();
    var page = await OnTheBoardAsync(context);
    await OpenAsync(page);

    await page.Keyboard.PressAsync("Escape");

    await Expect(Dialog(page)).ToBeHiddenAsync();
  }

  /// <summary>
  /// Le clic tombe <b>dans le coin de la fenêtre</b>, loin de la modale centrée : c'est le fond, et
  /// rien d'autre, qui le reçoit.
  /// </summary>
  [Fact]
  public async Task ClosesOnAClickOnTheBackdrop()
  {
    await using var context = await harness.NewContextAsync();
    var page = await OnTheBoardAsync(context);
    await OpenAsync(page);

    await page.Mouse.ClickAsync(5, 5);

    await Expect(Dialog(page)).ToBeHiddenAsync();
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
