namespace MicroserviceRgpd.BrowserTests.Requests;

/// <summary>
/// <b>L'<c>Operator</c> ne perd jamais une saisie par mégarde</b>, dans un vrai navigateur : un
/// formulaire modifié ne se ferme, par aucun des quatre modes de fermeture, sans que la confirmation
/// « Abandonner la saisie ? » l'ait demandé ; un formulaire non modifié se ferme directement.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>« Modifié » se lit sur les valeurs brutes</b>, comparées aux valeurs par défaut : saisir
/// puis effacer ne modifie rien, des espaces seuls modifient.
/// </para>
/// <para>
/// Tout se lit par le rôle et le nom accessible, comme dans <see cref="CreationDialog"/> ; les
/// libellés sont recopiés à dessein.
/// </para>
/// </remarks>
[Collection(BrowserCollection.Name)]
public class AbandonConfirmation(BrowserHarness harness)
{
  private const string DialogTitle = "Créer une nouvelle demande";

  private const string ConfirmationTitle = "Abandonner la saisie ?";

  /// <summary>Les quatre modes de fermeture de la modale de création.</summary>
  public static TheoryData<string> ClosingModes { get; } = ["Annuler", "la croix", "Échap", "le fond"];

  [Theory]
  [MemberData(nameof(ClosingModes))]
  public async Task ClosesAnUntouchedFormWithoutAsking(string mode)
  {
    await using var context = await harness.NewContextAsync();
    var page = await OnTheBoardAsync(context);
    await OpenAsync(page);

    await CloseByAsync(page, mode);

    await Expect(Dialog(page)).ToBeHiddenAsync();
    await Expect(Confirmation(page)).ToBeHiddenAsync();
  }

  [Theory]
  [MemberData(nameof(ClosingModes))]
  public async Task AsksBeforeClosingAModifiedForm(string mode)
  {
    await using var context = await harness.NewContextAsync();
    var page = await OnTheBoardAsync(context);
    await OpenAsync(page);
    await Field(page, "Nom").FillAsync("Dupont");

    await CloseByAsync(page, mode);

    await Expect(Confirmation(page)).ToBeVisibleAsync();
    await Expect(Confirmation(page)).ToContainTextAsync("Les informations saisies seront perdues.");
    await Expect(Dialog(page)).ToBeVisibleAsync();
  }

  /// <summary>
  /// <b>Chaque champ compte</b> : une modification de l'un seul d'entre eux, quel qu'il soit,
  /// suffit à demander la confirmation — y compris la date par défaut effacée.
  /// </summary>
  [Theory]
  [InlineData("Origine")]
  [InlineData("Date de réception")]
  [InlineData("Prénom")]
  [InlineData("Email")]
  [InlineData("Identité vérifiée")]
  [InlineData("Message")]
  [InlineData("Droits RGPD")]
  public async Task CountsAChangeInAnyField(string label)
  {
    await using var context = await harness.NewContextAsync();
    var page = await OnTheBoardAsync(context);
    await OpenAsync(page);

    var field = Field(page, label);

    switch (label)
    {
      case "Origine":
        await field.SelectOptionAsync(new SelectOptionValue { Label = "Courrier" });
        break;
      case "Droits RGPD":
        await field.SelectOptionAsync("Access");
        break;
      case "Identité vérifiée":
        await field.CheckAsync();
        break;
      case "Date de réception":
        await field.FillAsync(string.Empty);
        break;
      default:
        await field.FillAsync("x");
        break;
    }

    await CloseByAsync(page, "Annuler");

    await Expect(Confirmation(page)).ToBeVisibleAsync();
  }

  /// <summary>
  /// <b>Saisir puis effacer ne modifie rien</b> : le formulaire est revenu à ses valeurs par défaut,
  /// et l'abandon ne perdrait rien.
  /// </summary>
  [Fact]
  public async Task ClosesWithoutAskingWhenAnEntryWasErased()
  {
    await using var context = await harness.NewContextAsync();
    var page = await OnTheBoardAsync(context);
    await OpenAsync(page);

    await Field(page, "Nom").FillAsync("Dupont");
    await Field(page, "Nom").FillAsync(string.Empty);
    await Field(page, "Identité vérifiée").CheckAsync();
    await Field(page, "Identité vérifiée").UncheckAsync();

    await CloseByAsync(page, "Annuler");

    await Expect(Dialog(page)).ToBeHiddenAsync();
    await Expect(Confirmation(page)).ToBeHiddenAsync();
  }

  /// <summary>
  /// ⚠️ <b>Des espaces seuls sont une modification</b> : aucune saisie ne disparaît sans que
  /// l'<c>Operator</c> l'ait confirmé — même celle que le service, lui, tiendrait pour vide.
  /// </summary>
  [Fact]
  public async Task AsksWhenOnlySpacesWereEntered()
  {
    await using var context = await harness.NewContextAsync();
    var page = await OnTheBoardAsync(context);
    await OpenAsync(page);

    await Field(page, "Message").FillAsync("   ");

    await CloseByAsync(page, "Annuler");

    await Expect(Confirmation(page)).ToBeVisibleAsync();
  }

  /// <summary>
  /// ⚠️ <b>« Continuer la saisie » a le focus à l'ouverture de la confirmation</b> : une touche
  /// Entrée réflexe ramène au formulaire, la saisie intacte, et ne détruit rien.
  /// </summary>
  [Fact]
  public async Task FocusesContinueSoThatAReflexEnterDestroysNothing()
  {
    await using var context = await harness.NewContextAsync();
    var page = await OnTheBoardAsync(context);
    await OpenAsync(page);
    await FillTheFormAsync(page);

    await CloseByAsync(page, "Annuler");

    await Expect(ConfirmationButton(page, "Continuer la saisie")).ToBeFocusedAsync();

    await page.Keyboard.PressAsync("Enter");

    await Expect(Confirmation(page)).ToBeHiddenAsync();
    await Expect(Dialog(page)).ToBeVisibleAsync();
    await ExpectTheEntryIntactAsync(page);
  }

  /// <summary><b>« Continuer la saisie » ramène au formulaire</b>, la saisie intacte.</summary>
  [Theory]
  [MemberData(nameof(ClosingModes))]
  public async Task ContinuingComesBackToTheFormWithTheEntryIntact(string mode)
  {
    await using var context = await harness.NewContextAsync();
    var page = await OnTheBoardAsync(context);
    await OpenAsync(page);
    await FillTheFormAsync(page);
    await CloseByAsync(page, mode);

    await ConfirmationButton(page, "Continuer la saisie").ClickAsync();

    await Expect(Confirmation(page)).ToBeHiddenAsync();
    await Expect(Dialog(page)).ToBeVisibleAsync();
    await ExpectTheEntryIntactAsync(page);
  }

  /// <summary>
  /// ⚠️ <b>Échap sur la confirmation vaut « Continuer la saisie »</b> — et pas un abandon : elle ne
  /// referme que la confirmation, et un second Échap redemande la confirmation.
  /// </summary>
  [Fact]
  public async Task EscapeOnTheConfirmationContinuesTheEntry()
  {
    await using var context = await harness.NewContextAsync();
    var page = await OnTheBoardAsync(context);
    await OpenAsync(page);
    await FillTheFormAsync(page);
    await CloseByAsync(page, "Échap");
    await Expect(Confirmation(page)).ToBeVisibleAsync();

    await page.Keyboard.PressAsync("Escape");

    await Expect(Confirmation(page)).ToBeHiddenAsync();
    await Expect(Dialog(page)).ToBeVisibleAsync();
    await ExpectTheEntryIntactAsync(page);

    await page.Keyboard.PressAsync("Escape");

    await Expect(Confirmation(page)).ToBeVisibleAsync();
    await Expect(Dialog(page)).ToBeVisibleAsync();
  }

  /// <summary>
  /// ⚠️ <b>Même quand le navigateur ne laisse pas retenir Échap, la saisie ne se perd pas.</b>
  /// Chromium ne rend l'événement <c>cancel</c> annulable que si l'<c>Operator</c> a cliqué ou saisi
  /// depuis le dernier Échap retenu — et Échap lui-même ne compte pas. Des Échap répétés finissent
  /// donc par fermer la modale quoi que fasse le module. Le test rejoue ce que le navigateur fait
  /// alors : un <c>cancel</c> non annulable, puis la fermeture.
  /// </summary>
  [Fact]
  public async Task KeepsTheEntryWhenTheBrowserClosesOnEscapeRegardless()
  {
    await using var context = await harness.NewContextAsync();
    var page = await OnTheBoardAsync(context);
    await OpenAsync(page);
    await FillTheFormAsync(page);

    await Dialog(page).EvaluateAsync(
      "dialog => { dialog.dispatchEvent(new Event('cancel', { cancelable: false })); dialog.close(); }");

    await Expect(Confirmation(page)).ToBeVisibleAsync();
    await Expect(Dialog(page)).ToBeVisibleAsync();

    await ConfirmationButton(page, "Continuer la saisie").ClickAsync();

    await ExpectTheEntryIntactAsync(page);
  }

  /// <summary>
  /// <b>« Abandonner » ferme les deux modales sans rien enregistrer</b> — aucune requête ne part
  /// vers le service —, et la réouverture suivante repart des valeurs par défaut.
  /// </summary>
  [Theory]
  [MemberData(nameof(ClosingModes))]
  public async Task AbandoningClosesBothDialogsSavesNothingAndReopensOnTheDefaults(string mode)
  {
    await using var context = await harness.NewContextAsync();
    await context.Clock.SetFixedTimeAsync("2026-01-10T10:00:00Z");
    var page = await OnTheBoardAsync(context);
    await OpenAsync(page);
    await FillTheFormAsync(page);
    await CloseByAsync(page, mode);

    var sent = new List<string>();
    page.Request += (_, request) => sent.Add($"{request.Method} {request.Url}");

    await ConfirmationButton(page, "Abandonner").ClickAsync();

    await Expect(Confirmation(page)).ToBeHiddenAsync();
    await Expect(Dialog(page)).ToBeHiddenAsync();
    sent.ShouldBeEmpty("L'abandon a appelé le service.");

    await OpenAsync(page);

    await Expect(Field(page, "Origine")).ToHaveValueAsync("Email");
    await Expect(Field(page, "Date de réception")).ToHaveValueAsync("2026-01-10");

    foreach (var blank in new[] { "Nom", "Prénom", "Email", "Message" })
    {
      await Expect(Field(page, blank)).ToHaveValueAsync(string.Empty);
    }

    await Expect(Field(page, "Identité vérifiée")).Not.ToBeCheckedAsync();
    await Expect(Field(page, "Droits RGPD")).ToHaveValueAsync(string.Empty);

    // Revenu à ses valeurs par défaut, le formulaire rouvert se referme sans rien demander.
    await CloseByAsync(page, "Annuler");
    await Expect(Dialog(page)).ToBeHiddenAsync();
    await Expect(Confirmation(page)).ToBeHiddenAsync();
  }

  private static async Task FillTheFormAsync(IPage page)
  {
    await Field(page, "Origine").SelectOptionAsync(new SelectOptionValue { Label = "Courrier" });
    await Field(page, "Date de réception").FillAsync("2025-12-24");
    await Field(page, "Nom").FillAsync("Dupont");
    await Field(page, "Prénom").FillAsync("Marie");
    await Field(page, "Email").FillAsync("marie.dupont@example.org");
    await Field(page, "Identité vérifiée").CheckAsync();
    await Field(page, "Message").FillAsync("Je souhaite accéder à mes données.");
    await Field(page, "Droits RGPD").SelectOptionAsync("Access");
  }

  private static async Task ExpectTheEntryIntactAsync(IPage page)
  {
    await Expect(Field(page, "Origine")).ToHaveValueAsync("Letter");
    await Expect(Field(page, "Date de réception")).ToHaveValueAsync("2025-12-24");
    await Expect(Field(page, "Nom")).ToHaveValueAsync("Dupont");
    await Expect(Field(page, "Prénom")).ToHaveValueAsync("Marie");
    await Expect(Field(page, "Email")).ToHaveValueAsync("marie.dupont@example.org");
    await Expect(Field(page, "Identité vérifiée")).ToBeCheckedAsync();
    await Expect(Field(page, "Message")).ToHaveValueAsync("Je souhaite accéder à mes données.");
    await Expect(Field(page, "Droits RGPD")).ToHaveValueAsync("Access");
  }

  /// <summary>
  /// Ferme la modale de création par l'un des quatre modes. Le clic sur le fond tombe <b>dans le
  /// coin de la fenêtre</b>, loin de la modale centrée, comme dans <see cref="CreationDialog"/>.
  /// </summary>
  private static Task CloseByAsync(IPage page, string mode)
  {
    return mode switch
    {
      "Annuler" => Dialog(page).GetByRole(AriaRole.Button, new() { Name = "Annuler", Exact = true }).ClickAsync(),
      "la croix" => Dialog(page).GetByRole(AriaRole.Button, new() { Name = "Fermer", Exact = true }).ClickAsync(),
      "Échap" => page.Keyboard.PressAsync("Escape"),
      "le fond" => page.Mouse.ClickAsync(5, 5),
      _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Mode de fermeture inconnu."),
    };
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

  private static ILocator Confirmation(IPage page)
  {
    return page.GetByRole(AriaRole.Alertdialog, new() { Name = ConfirmationTitle, Exact = true, IncludeHidden = true });
  }

  private static ILocator ConfirmationButton(IPage page, string name)
  {
    return Confirmation(page).GetByRole(AriaRole.Button, new() { Name = name, Exact = true });
  }

  private static ILocator Field(IPage page, string label)
  {
    return Dialog(page).GetByLabel(label, new() { Exact = true });
  }
}
