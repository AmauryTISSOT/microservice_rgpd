using System.Text.RegularExpressions;
using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.Web.Pages.Requests;

namespace MicroserviceRgpd.BrowserTests.Requests;

/// <summary>
/// <b>L'<c>Operator</c> prolonge le délai de réponse depuis la flèche d'horloge de la ligne</b>, dans
/// un vrai navigateur : la modale récapitule la demande et <b>les deux dates</b>, avertit qu'il faudra
/// informer la personne concernée, propose les deux motifs du règlement — et sur confirmation, la
/// ligne prend la nouvelle date limite et la mention « Prolongée », sans rechargement (ADR-0029).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Aucune date n'est calculée par le navigateur</b> : celle que la modale annonce est celle que
/// le serveur écrit, au caractère près. C'est ce que <c>AnnouncesExactlyTheDeadlineItWrites</c> tient.
/// </para>
/// <para>
/// Les textes attendus se lisent sur les constantes du serveur — <see cref="ExtensionConfirmation"/>,
/// <see cref="ExtensionGround"/>, <see cref="RequestRow"/> : une phrase retouchée là l'est ici.
/// </para>
/// </remarks>
[Collection(BrowserModificationCollection.Name)]
public class RequestExtensionDialog(BrowserHarness harness)
{
  /// <summary>Les trois façons de renoncer.</summary>
  public static TheoryData<string> ClosingModes { get; } = ["Annuler", "la croix", "Échap"];

  /// <summary>
  /// <b>La modale récapitule la demande et les deux dates</b> : la date limite en vigueur, celle qui en
  /// résultera, l'avertissement daté de la date limite <b>initiale</b>, les deux motifs du règlement —
  /// et rien d'autre —, et une justification vide.
  /// </summary>
  [Fact]
  public async Task SummarizesTheRequestAndBothDeadlines()
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var email = UniqueEmail();
    var row = await RowOfARequestAsync(page, email);

    await ExtensionOf(row).ClickAsync();

    var dialog = Dialog(page);

    await Expect(dialog).ToBeVisibleAsync();
    await Expect(Fact(dialog, ExtensionConfirmation.RightLabel)).ToHaveTextAsync(
      $"Droit d'accès (art. {DataSubjectRight.Access.Article})");
    await Expect(Fact(dialog, ExtensionConfirmation.FirstNameLabel)).ToHaveTextAsync("Jeanne");
    await Expect(Fact(dialog, ExtensionConfirmation.LastNameLabel)).ToHaveTextAsync("Martin");
    await Expect(Fact(dialog, ExtensionConfirmation.EmailLabel)).ToHaveTextAsync(email);
    await Expect(Fact(dialog, ExtensionConfirmation.CurrentDeadlineLabel)).ToHaveTextAsync("15/02/2026");
    await Expect(Fact(dialog, ExtensionConfirmation.ResultingDeadlineLabel)).ToHaveTextAsync("15/04/2026");

    // L'avertissement est daté de la date limite INITIALE : c'est avant elle qu'il faut informer.
    await Expect(dialog).ToHaveAccessibleDescriptionAsync(
      string.Format(System.Globalization.CultureInfo.InvariantCulture, ExtensionConfirmation.WarningFormat, "15/02/2026"));

    // LES DEUX MOTIFS DU RÈGLEMENT, DERRIÈRE L'INVITE, ET PAS UN TROISIÈME.
    var ground = Ground(dialog);
    (await ground.Locator("option").AllTextContentsAsync()).ShouldBe(
    [
      ExtensionConfirmation.GroundPrompt,
      ExtensionGround.Complexity.FrenchLabel,
      ExtensionGround.NumberOfRequests.FrenchLabel,
    ]);
    await Expect(ground).ToHaveValueAsync("");

    // ⚠️ RIEN N'EST PRÉ-REMPLI : la justification est le fait concret que l'Operator écrit.
    await Expect(Justification(dialog)).ToHaveValueAsync("");
    await Expect(Button(page, ExtensionConfirmation.Cancel)).ToBeFocusedAsync();
  }

  /// <summary>
  /// <b>La confirmation prolonge la demande sans rechargement</b> : la ligne prend la nouvelle date
  /// limite et la mention « Prolongée », la modale se ferme, et le bandeau dit « Demande prolongée ».
  /// </summary>
  [Fact]
  public async Task ExtendsTheRowWithoutReloadingAndSaysSo()
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var row = await RowOfARequestAsync(page, UniqueEmail());

    // Un marqueur posé sur la fenêtre : un rechargement l'effacerait.
    await page.EvaluateAsync("() => { window.untouched = true; }");
    await Expect(Deadline(row)).ToContainTextAsync("15/02/2026");
    await Expect(Deadline(row)).Not.ToContainTextAsync(RequestRow.Extended);

    await ExtensionOf(row).ClickAsync();
    await Ground(Dialog(page)).SelectOptionAsync(nameof(ExtensionGround.Complexity));
    await Justification(Dialog(page)).FillAsync("Les données sont réparties sur quatre systèmes.");
    await Button(page, ExtensionConfirmation.Confirm).ClickAsync();

    await Expect(Dialog(page)).ToBeHiddenAsync();
    await Expect(page.GetByRole(AriaRole.Status).And(page.GetByText(ExtensionConfirmation.Extended, new() { Exact = true })))
      .ToBeVisibleAsync();
    await Expect(Deadline(row)).ToContainTextAsync("15/04/2026");
    await Expect(Deadline(row)).ToContainTextAsync(RequestRow.Extended);
    (await page.EvaluateAsync<bool>("() => window.untouched === true")).ShouldBeTrue("La prolongation a rechargé la page.");
  }

  /// <summary>
  /// ⚠️ <b>La date annoncée est celle que le serveur écrit, au caractère près</b> — repli de fin de mois
  /// compris : une date limite au 31 décembre donne le 28 février, et non le 3 mars que
  /// <c>Date.setMonth</c> rendrait. Le navigateur ne calcule rien.
  /// </summary>
  [Fact]
  public async Task AnnouncesExactlyTheDeadlineItWrites()
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var email = UniqueEmail();
    var message = await harness.RecordRequestAsync("Martin", "Jeanne", email);
    await harness.SetResponseDeadlineAsync(message, new DateOnly(2026, 12, 31));
    await page.GotoAsync("/demandes");

    var row = page.GetByRole(AriaRole.Row).Filter(new() { HasText = email });
    await Expect(row).ToHaveCountAsync(1);

    await ExtensionOf(row).ClickAsync();
    await Expect(Dialog(page)).ToBeVisibleAsync();

    var announced = (await Fact(Dialog(page), ExtensionConfirmation.ResultingDeadlineLabel).TextContentAsync())
      .ShouldNotBeNull();

    announced.ShouldBe("28/02/2027");

    await Ground(Dialog(page)).SelectOptionAsync(nameof(ExtensionGround.NumberOfRequests));
    await Justification(Dialog(page)).FillAsync("Quatre cents demandes ce mois-ci.");
    await Button(page, ExtensionConfirmation.Confirm).ClickAsync();

    await Expect(Dialog(page)).ToBeHiddenAsync();
    await Expect(Deadline(row)).ToContainTextAsync(announced);
  }

  /// <summary>
  /// <b>« Annuler », la croix et Échap ferment la modale sans rien changer</b> : aucun appel, la date
  /// limite est celle d'avant, et la ligne ne porte pas la mention.
  /// </summary>
  [Theory]
  [MemberData(nameof(ClosingModes))]
  public async Task ClosesWithoutChangingAnything(string mode)
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var row = await RowOfARequestAsync(page, UniqueEmail());

    await ExtensionOf(row).ClickAsync();
    await Expect(Dialog(page)).ToBeVisibleAsync();
    await Ground(Dialog(page)).SelectOptionAsync(nameof(ExtensionGround.Complexity));

    var sent = new List<string>();
    page.Request += (_, request) => sent.Add($"{request.Method} {request.Url}");

    await CloseByAsync(page, mode);

    await Expect(Dialog(page)).ToBeHiddenAsync();
    await Expect(Deadline(row)).ToContainTextAsync("15/02/2026");
    await Expect(Deadline(row)).Not.ToContainTextAsync(RequestRow.Extended);
    sent.ShouldBeEmpty("La fermeture a appelé le service.");
  }

  /// <summary>
  /// ⚠️ <b>Une modale abandonnée repart vierge</b> : la saisie de la fois d'avant ne revient pas à
  /// l'ouverture suivante.
  /// </summary>
  [Fact]
  public async Task ReopensOnAnEmptyEntry()
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var row = await RowOfARequestAsync(page, UniqueEmail());

    await ExtensionOf(row).ClickAsync();
    await Ground(Dialog(page)).SelectOptionAsync(nameof(ExtensionGround.Complexity));
    await Justification(Dialog(page)).FillAsync("Une justification abandonnée.");
    await CloseByAsync(page, "Annuler");
    await Expect(Dialog(page)).ToBeHiddenAsync();

    await ExtensionOf(row).ClickAsync();

    await Expect(Ground(Dialog(page))).ToHaveValueAsync("");
    await Expect(Justification(Dialog(page))).ToHaveValueAsync("");
  }

  /// <summary>
  /// <b>Le parcours clavier va d'un bout à l'autre du geste, sans piège de focus</b> : la flèche
  /// d'horloge s'atteint par la tabulation, Entrée ouvre la modale, la tabulation y parcourt le motif
  /// puis la justification jusqu'à « Prolonger », et la fermeture rend le focus à la ligne.
  /// </summary>
  [Fact]
  public async Task WalksTheWholeGestureWithTheKeyboardAlone()
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var row = await RowOfARequestAsync(page, UniqueEmail());

    await ExtensionOf(row).FocusAsync();
    await page.Keyboard.PressAsync("Enter");

    var dialog = Dialog(page);
    await Expect(dialog).ToBeVisibleAsync();

    // « Annuler » a le focus : une touche Entrée réflexe ne prolonge rien.
    await Expect(Button(page, ExtensionConfirmation.Cancel)).ToBeFocusedAsync();

    await Ground(dialog).FocusAsync();
    await Ground(dialog).SelectOptionAsync(nameof(ExtensionGround.Complexity));
    await page.Keyboard.PressAsync("Tab");
    await Expect(Justification(dialog)).ToBeFocusedAsync();

    await page.Keyboard.TypeAsync("Quatre systèmes à interroger.");
    await page.Keyboard.PressAsync("Tab");
    await Expect(Button(page, ExtensionConfirmation.Cancel)).ToBeFocusedAsync();
    await page.Keyboard.PressAsync("Tab");
    await Expect(Button(page, ExtensionConfirmation.Confirm)).ToBeFocusedAsync();

    await page.Keyboard.PressAsync("Enter");

    await Expect(dialog).ToBeHiddenAsync();
    await Expect(Deadline(row)).ToContainTextAsync(RequestRow.Extended);
    await Expect(ExtensionOf(row)).ToBeFocusedAsync();
  }

  /// <summary>
  /// <b>Une prolongation mal saisie est refusée sur le champ fautif</b> : la modale reste ouverte, le
  /// refus du serveur s'affiche sous le motif <b>et</b> sous la justification, le premier fautif prend
  /// le focus — et <b>rien de ce qui a été écrit n'est perdu</b>. La ligne, elle, n'a pas bougé.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Le motif est forgé depuis le script</b> : le choix fermé de l'écran ne le propose pas. C'est
  /// justement ce que le domaine revérifie.
  /// </remarks>
  [Fact]
  public async Task ShowsTheServerRefusalUnderEachFaultyFieldAndKeepsTheEntry()
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var row = await RowOfARequestAsync(page, UniqueEmail());

    await ExtensionOf(row).ClickAsync();

    var dialog = Dialog(page);
    await Expect(dialog).ToBeVisibleAsync();

    await ForgeTheGroundAsync(page, "Autre");
    await Justification(dialog).FillAsync("   ");
    await Button(page, ExtensionConfirmation.Confirm).ClickAsync();

    await ExpectTheRefusalAsync(Ground(dialog), DataSubjectRequestMessages.ExtensionGroundMissing);
    await ExpectTheRefusalAsync(Justification(dialog), DataSubjectRequestMessages.ExtensionJustificationMissing);

    // LA MODALE RESTE OUVERTE, LA SAISIE EST LÀ, ET LE PREMIER CHAMP FAUTIF A LE FOCUS.
    await Expect(dialog).ToBeVisibleAsync();
    await Expect(Ground(dialog)).ToBeFocusedAsync();
    await Expect(Justification(dialog)).ToHaveValueAsync("   ");

    await Expect(Deadline(row)).ToContainTextAsync("15/02/2026");
    await Expect(Deadline(row)).Not.ToContainTextAsync(RequestRow.Extended);
  }

  /// <summary>
  /// <b>Une justification au-delà du plafond est refusée sous son champ</b>, et le message dit la
  /// borne en toutes lettres. Le motif, lui, est juste : il ne porte aucun refus.
  /// </summary>
  [Fact]
  public async Task RefusesAJustificationOverItsCeilingUnderItsOwnField()
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var row = await RowOfARequestAsync(page, UniqueEmail());

    await ExtensionOf(row).ClickAsync();

    var dialog = Dialog(page);
    await Expect(dialog).ToBeVisibleAsync();

    await Ground(dialog).SelectOptionAsync(nameof(ExtensionGround.Complexity));
    await Justification(dialog).FillAsync(new string('j', ExtensionJustification.MaxLength + 1));
    await Button(page, ExtensionConfirmation.Confirm).ClickAsync();

    await ExpectTheRefusalAsync(Justification(dialog), DataSubjectRequestMessages.ExtensionJustificationTooLong);
    await Expect(Ground(dialog)).Not.ToHaveAttributeAsync("aria-invalid", "true");
    await Expect(dialog).ToBeVisibleAsync();
  }

  /// <summary>
  /// ⚠️ <b>Un refus corrigé, puis confirmé, prolonge</b> : la modale corrigée part sans que l'écran ne
  /// garde de trace du refus d'avant — ni sous les champs, ni au rouvrir.
  /// </summary>
  [Fact]
  public async Task ExtendsOnceTheRefusedEntryIsCorrected()
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var row = await RowOfARequestAsync(page, UniqueEmail());

    await ExtensionOf(row).ClickAsync();

    var dialog = Dialog(page);
    await Expect(dialog).ToBeVisibleAsync();

    await Justification(dialog).FillAsync("Quatre systèmes à interroger.");
    await Button(page, ExtensionConfirmation.Confirm).ClickAsync();

    await ExpectTheRefusalAsync(Ground(dialog), DataSubjectRequestMessages.ExtensionGroundMissing);

    await Ground(dialog).SelectOptionAsync(nameof(ExtensionGround.Complexity));
    await Button(page, ExtensionConfirmation.Confirm).ClickAsync();

    await Expect(dialog).ToBeHiddenAsync();
    await Expect(Deadline(row)).ToContainTextAsync("15/04/2026");
    await Expect(Deadline(row)).ToContainTextAsync(RequestRow.Extended);
  }

  /// <summary>Le refus s'affiche dans la place que le serveur a rendue sous le champ, et le marque fautif.</summary>
  private static async Task ExpectTheRefusalAsync(ILocator field, string refusal)
  {
    await Expect(field).ToHaveAccessibleDescriptionAsync(refusal);
    await Expect(field).ToHaveAttributeAsync("aria-invalid", "true");
  }

  /// <summary>
  /// Pose sur le choix fermé une valeur qu'il ne propose pas : l'option est ajoutée au vol, puis
  /// choisie. C'est l'envoi forgé que le domaine revérifie.
  /// </summary>
  private static Task ForgeTheGroundAsync(IPage page, string ground) =>
    page.EvaluateAsync(
      """
      (ground) => {
        const select = document.getElementById("request-extension-ground");
        select.add(new Option(ground, ground));
        select.value = ground;
      }
      """,
      ground);

  private static Task CloseByAsync(IPage page, string mode)
  {
    return mode switch
    {
      "Annuler" => Button(page, ExtensionConfirmation.Cancel).ClickAsync(),
      "la croix" => Button(page, "Fermer").ClickAsync(),
      "Échap" => page.Keyboard.PressAsync("Escape"),
      _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Mode de fermeture inconnu."),
    };
  }

  private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

  /// <summary>
  /// Enregistre une demande reçue le 15/01/2026 — Jeanne Martin, droit d'accès —, ouvre le tableau et
  /// rend sa ligne. Sa date limite de réponse est donc le 15/02/2026.
  /// </summary>
  private async Task<ILocator> RowOfARequestAsync(IPage page, string email)
  {
    await harness.RecordRequestAsync("Martin", "Jeanne", email, right: nameof(DataSubjectRight.Access));
    await page.GotoAsync("/demandes");

    var row = page.GetByRole(AriaRole.Row).Filter(new() { HasText = email });
    await Expect(row).ToHaveCountAsync(1);

    return row;
  }

  private static ILocator ExtensionOf(ILocator row) =>
    row.GetByRole(AriaRole.Button, new() { Name = RequestRow.ExtensionOffered, Exact = true });

  private static ILocator Deadline(ILocator row) => row.Locator("td[data-field=responseDeadline]");

  private static ILocator Dialog(IPage page) =>
    page.GetByRole(AriaRole.Dialog, new() { Name = ExtensionConfirmation.Title, Exact = true, IncludeHidden = true });

  private static ILocator Button(IPage page, string name) =>
    Dialog(page).GetByRole(AriaRole.Button, new() { Name = name, Exact = true });

  private static ILocator Ground(ILocator dialog) =>
    dialog.GetByLabel(ExtensionConfirmation.GroundLabel, new() { Exact = true });

  private static ILocator Justification(ILocator dialog) =>
    dialog.GetByLabel(ExtensionConfirmation.JustificationLabel, new() { Exact = true });

  /// <summary>La valeur que la modale montre sous le libellé <paramref name="label"/>.</summary>
  private static ILocator Fact(ILocator dialog, string label) =>
    dialog.Locator("dt").Filter(new() { HasTextRegex = new Regex($"^{Regex.Escape(label)}$") }).Locator("xpath=following-sibling::dd[1]");
}
