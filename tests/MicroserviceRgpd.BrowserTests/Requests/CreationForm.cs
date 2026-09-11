namespace MicroserviceRgpd.BrowserTests.Requests;

/// <summary>
/// <b>Le formulaire de la modale de création repart de zéro à chaque ouverture</b>, dans un vrai
/// navigateur : ses valeurs par défaut, « aujourd'hui » recalculé à Paris, et le focus sur le premier
/// champ.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>L'horloge du navigateur est figée par le test</b>, à des instants où le jour de Paris n'est
/// pas celui de l'UTC — et jamais celui du serveur, qui rend la date réelle. Une date par défaut qui
/// viendrait du serveur, de l'UTC ou du fuseau de la machine se verrait donc ici.
/// </para>
/// <para>
/// Tout se lit par le rôle et le nom accessible, comme dans <see cref="CreationDialog"/> ; les
/// libellés sont recopiés à dessein.
/// </para>
/// </remarks>
[Collection(BrowserCollection.Name)]
public class CreationForm(BrowserHarness harness)
{
  private const string DialogTitle = "Créer une nouvelle demande";

  /// <summary>
  /// <b>À l'ouverture, les valeurs par défaut sont en place et le focus est sur l'origine.</b> À
  /// 23 h 30 UTC la veille du passage à l'heure d'été, il est déjà 0 h 30 le lendemain à Paris.
  /// </summary>
  [Fact]
  public async Task OpensOnItsDefaultsWithTheFocusOnTheFirstField()
  {
    await using var context = await harness.NewContextAsync();
    await context.Clock.SetFixedTimeAsync("2026-03-28T23:30:00Z");
    var page = await OnTheBoardAsync(context);

    await OpenAsync(page);

    await ExpectTheDefaultsAsync(page, today: "2026-03-29");
    await Expect(Field(page, "Origine")).ToBeFocusedAsync();
  }

  /// <summary>
  /// ⚠️ <b>« Aujourd'hui » se recalcule à chaque ouverture</b> : une page ouverte avant minuit, à Paris
  /// et en heure d'été, ne propose pas la veille — ni n'interdit le jour même — une fois minuit passé.
  /// </summary>
  [Fact]
  public async Task RecomputesTodayInParisAtEachOpening()
  {
    await using var context = await harness.NewContextAsync();
    await context.Clock.SetFixedTimeAsync("2026-07-14T21:50:00Z");
    var page = await OnTheBoardAsync(context);

    await OpenAsync(page);
    await Expect(Field(page, "Date de réception")).ToHaveValueAsync("2026-07-14");
    await Dialog(page).GetByRole(AriaRole.Button, new() { Name = "Annuler", Exact = true }).ClickAsync();

    await page.Clock.SetFixedTimeAsync("2026-07-14T22:10:00Z");
    await OpenAsync(page);

    await Expect(Field(page, "Date de réception")).ToHaveValueAsync("2026-07-15");
    await Expect(Field(page, "Date de réception")).ToHaveAttributeAsync("max", "2026-07-15");
  }

  /// <summary>
  /// <b>Une saisie, une fermeture sans créer, puis une réouverture ramènent les valeurs par
  /// défaut</b> : l'<c>Operator</c> n'hérite jamais d'une saisie précédente.
  /// </summary>
  [Fact]
  public async Task ComesBackToItsDefaultsWhenReopenedAfterAnEntry()
  {
    await using var context = await harness.NewContextAsync();
    await context.Clock.SetFixedTimeAsync("2026-01-10T10:00:00Z");
    var page = await OnTheBoardAsync(context);
    await OpenAsync(page);

    await Field(page, "Origine").SelectOptionAsync(new SelectOptionValue { Label = "Courrier" });
    await Field(page, "Date de réception").FillAsync("2025-12-24");
    await Field(page, "Nom").FillAsync("Dupont");
    await Field(page, "Prénom").FillAsync("Marie");
    await Field(page, "Email").FillAsync("marie.dupont@example.org");
    await Field(page, "Identité vérifiée").CheckAsync();
    await Field(page, "Message").FillAsync("Je souhaite accéder à mes données.");
    await Field(page, "Droits RGPD").SelectOptionAsync("Access");

    await Dialog(page).GetByRole(AriaRole.Button, new() { Name = "Annuler", Exact = true }).ClickAsync();
    await OpenAsync(page);

    await ExpectTheDefaultsAsync(page, today: "2026-01-10");
    await Expect(Field(page, "Origine")).ToBeFocusedAsync();
  }

  /// <summary>
  /// <b>La qualification du droit par IA se voit, mais ne se déclenche pas</b> : le bouton est
  /// désactivé, et son infobulle dit « Bientôt disponible ».
  /// </summary>
  [Fact]
  public async Task ShowsTheAiQualificationDisabledWithItsTooltip()
  {
    await using var context = await harness.NewContextAsync();
    var page = await OnTheBoardAsync(context);
    await OpenAsync(page);

    var button = Dialog(page).GetByRole(AriaRole.Button, new() { Name = "Qualification du droit par IA", Exact = true });

    await Expect(button).ToBeVisibleAsync();
    await Expect(button).ToBeDisabledAsync();
    await Expect(button).ToHaveAttributeAsync("title", "Bientôt disponible");
    await Expect(button).ToHaveAccessibleDescriptionAsync("Bientôt disponible");
  }

  private static async Task ExpectTheDefaultsAsync(IPage page, string today)
  {
    await Expect(Field(page, "Origine")).ToHaveValueAsync("Email");
    await Expect(Field(page, "Date de réception")).ToHaveValueAsync(today);
    await Expect(Field(page, "Date de réception")).ToHaveAttributeAsync("max", today);

    foreach (var blank in new[] { "Nom", "Prénom", "Email", "Message" })
    {
      await Expect(Field(page, blank)).ToHaveValueAsync(string.Empty);
    }

    await Expect(Field(page, "Identité vérifiée")).Not.ToBeCheckedAsync();
    await Expect(Field(page, "Droits RGPD")).ToHaveValueAsync(string.Empty);
    await Expect(Field(page, "Droits RGPD").Locator("option:checked")).ToHaveTextAsync("Sélectionner un droit");
  }

  private static async Task<IPage> OnTheBoardAsync(IBrowserContext context)
  {
    var page = await context.NewPageAsync();
    await page.GotoAsync("/demandes");

    return page;
  }

  private static async Task OpenAsync(IPage page)
  {
    await page.GetByRole(AriaRole.Button, new() { Name = "Créer une demande", Exact = true }).ClickAsync();
    await Expect(Dialog(page)).ToBeVisibleAsync();
  }

  private static ILocator Dialog(IPage page)
  {
    return page.GetByRole(AriaRole.Dialog, new() { Name = DialogTitle, Exact = true, IncludeHidden = true });
  }

  private static ILocator Field(IPage page, string label)
  {
    return Dialog(page).GetByLabel(label, new() { Exact = true });
  }
}
