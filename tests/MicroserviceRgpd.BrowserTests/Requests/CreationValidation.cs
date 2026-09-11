using System.Text.RegularExpressions;

namespace MicroserviceRgpd.BrowserTests.Requests;

/// <summary>
/// <b>Le navigateur valide la saisie avec les règles du service</b>, dans un vrai navigateur : les
/// erreurs n'apparaissent qu'au clic sur « Créer », chacune sous son champ, le focus va au premier
/// champ en erreur ; puis seuls les champs en erreur se revalident en direct, jusqu'à leur correction.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>C'est un confort : le serveur fait foi.</b> Tant que l'envoi n'est pas branché, un
/// formulaire valide ne produit aucun effet — ce que ces tests lisent, ce sont les refus.
/// </para>
/// <para>
/// Un refus se lit comme le lit un lecteur d'écran : la <b>description accessible</b> du champ, qui
/// porte le message affiché sous lui, et son état <c>aria-invalid</c>. Les libellés et les messages
/// sont recopiés à dessein.
/// </para>
/// </remarks>
[Collection(BrowserCollection.Name)]
public class CreationValidation(BrowserHarness harness)
{
  private const string DialogTitle = "Créer une nouvelle demande";

  private const string IdentificationMissing = "Renseignez un email, ou un nom et un prénom.";
  private const string ReceivedOnMissing = "La date de réception est obligatoire.";
  private const string ReceivedOnMalformed = "La date de réception doit être au format jj/mm/aaaa.";
  private const string ReceivedOnInTheFuture = "La date de réception ne peut pas être dans le futur.";
  private const string EmailInvalid = "L'adresse email n'est pas valide.";
  private const string LastNameTooLong = "Le nom ne peut pas dépasser 100 caractères.";
  private const string FirstNameTooLong = "Le prénom ne peut pas dépasser 100 caractères.";
  private const string MessageMissing = "Le message est obligatoire.";
  private const string MessageTooLong = "Le message ne peut pas dépasser 10 000 caractères.";
  private const string RightMissing = "Sélectionnez un droit RGPD.";

  private static readonly string[] Validated = ["Date de réception", "Nom", "Prénom", "Email", "Message", "Droits RGPD"];

  /// <summary>
  /// <b>Un formulaire laissé à ses valeurs par défaut</b>, au clic sur « Créer » : chaque message sous
  /// son champ — l'identification sous l'email et sous le nom et le prénom qui manquent tous deux —,
  /// la date du jour acceptée, le focus sur le nom, premier champ en erreur, et « Créer » toujours
  /// cliquable.
  /// </summary>
  [Fact]
  public async Task ShowsEveryRefusalUnderItsFieldOnCreateAndFocusesTheFirst()
  {
    await using var context = await harness.NewContextAsync();
    var page = await OpenedAsync(context);

    await CreateAsync(page);

    await ExpectNoRefusalAsync(page, "Date de réception");
    await ExpectTheRefusalAsync(page, "Nom", IdentificationMissing);
    await ExpectTheRefusalAsync(page, "Prénom", IdentificationMissing);
    await ExpectTheRefusalAsync(page, "Email", IdentificationMissing);
    await ExpectTheRefusalAsync(page, "Message", MessageMissing);
    await ExpectTheRefusalAsync(page, "Droits RGPD", RightMissing);

    await Expect(Dialog(page).GetByText(MessageMissing, new() { Exact = true })).ToBeVisibleAsync();
    await Expect(Dialog(page).GetByText(RightMissing, new() { Exact = true })).ToBeVisibleAsync();
    await Expect(Dialog(page).GetByText(IdentificationMissing, new() { Exact = true })).ToHaveCountAsync(3);

    await Expect(Field(page, "Nom")).ToBeFocusedAsync();
    await Expect(Dialog(page)).ToBeVisibleAsync();
    await Expect(CreateButton(page)).ToBeEnabledAsync();
  }

  /// <summary>
  /// <b>Avant le premier clic sur « Créer », rien ne s'affiche pendant la saisie</b>, si fautive
  /// soit-elle : l'<c>Operator</c> n'est pas interrompu.
  /// </summary>
  [Fact]
  public async Task ShowsNoRefusalWhileTypingBeforeTheFirstAttempt()
  {
    await using var context = await harness.NewContextAsync();
    var page = await OpenedAsync(context);

    await Field(page, "Date de réception").FillAsync(string.Empty);
    await Field(page, "Nom").FillAsync(new string('N', 101));
    await Field(page, "Email").FillAsync("pas-un-email");
    await Field(page, "Message").FillAsync("   ");
    await Field(page, "Message").FillAsync(string.Empty);
    await Field(page, "Droits RGPD").SelectOptionAsync("Access");
    await Field(page, "Droits RGPD").SelectOptionAsync(string.Empty);

    foreach (var label in Validated)
    {
      await ExpectNoRefusalAsync(page, label);
    }
  }

  /// <summary>
  /// <b>Après le premier clic, un champ corrigé perd son message aussitôt</b>, sans nouveau clic —
  /// y compris l'identification, qu'un email suffit à satisfaire sous les trois champs à la fois.
  /// </summary>
  [Fact]
  public async Task DropsTheRefusalOfACorrectedFieldWithoutAnotherClick()
  {
    await using var context = await harness.NewContextAsync();
    var page = await OpenedAsync(context);
    await CreateAsync(page);

    await Field(page, "Message").FillAsync("Je souhaite accéder à mes données.");
    await ExpectNoRefusalAsync(page, "Message");
    await ExpectTheRefusalAsync(page, "Droits RGPD", RightMissing);

    await Field(page, "Droits RGPD").SelectOptionAsync("Access");
    await ExpectNoRefusalAsync(page, "Droits RGPD");

    await Field(page, "Email").FillAsync("marie.dupont@example.org");
    await ExpectNoRefusalAsync(page, "Email");
    await ExpectNoRefusalAsync(page, "Nom");
    await ExpectNoRefusalAsync(page, "Prénom");
  }

  /// <summary>
  /// <b>Un champ en erreur se revalide en direct, et son message suit sa nouvelle faute</b> : l'email
  /// qui manquait, saisi mal formé, dit désormais qu'il n'est pas valide.
  /// </summary>
  [Fact]
  public async Task FollowsTheNewFaultOfAFieldStillInError()
  {
    await using var context = await harness.NewContextAsync();
    var page = await OpenedAsync(context);
    await CreateAsync(page);

    await Field(page, "Email").FillAsync("marie.dupont@");

    await ExpectTheRefusalAsync(page, "Email", EmailInvalid);
    await ExpectNoRefusalAsync(page, "Nom");
    await ExpectNoRefusalAsync(page, "Prénom");

    await Field(page, "Email").FillAsync("marie.dupont@example.org");

    await ExpectNoRefusalAsync(page, "Email");
  }

  /// <summary>
  /// ⚠️ <b>Seuls les champs en erreur se revalident en direct.</b> Un champ valide au premier clic, rendu
  /// fautif ensuite, ne s'en plaint qu'au clic suivant.
  /// </summary>
  [Fact]
  public async Task LeavesTheValidFieldsAloneUntilTheNextAttempt()
  {
    await using var context = await harness.NewContextAsync();
    var page = await OpenedAsync(context);
    await CreateAsync(page);

    await Field(page, "Date de réception").FillAsync(string.Empty);
    await ExpectNoRefusalAsync(page, "Date de réception");

    await CreateAsync(page);
    await ExpectTheRefusalAsync(page, "Date de réception", ReceivedOnMissing);
    await Expect(Field(page, "Date de réception")).ToBeFocusedAsync();
  }

  /// <summary>
  /// <b>Une date réellement vide est obligatoire</b> ; <b>une date commencée mais incomplète est mal
  /// formée</b> — deux messages distincts, pour que l'<c>Operator</c> ne prenne pas l'une pour l'autre.
  /// </summary>
  [Fact]
  public async Task TellsAnEmptyDateFromAMalformedOne()
  {
    await using var context = await harness.NewContextAsync();
    var page = await OpenedAsync(context);
    var receivedOn = Field(page, "Date de réception");

    await receivedOn.FillAsync(string.Empty);
    await CreateAsync(page);
    await ExpectTheRefusalAsync(page, "Date de réception", ReceivedOnMissing);

    await receivedOn.FocusAsync();
    await page.Keyboard.TypeAsync("12");
    await CreateAsync(page);
    await ExpectTheRefusalAsync(page, "Date de réception", ReceivedOnMalformed);
  }

  /// <summary>
  /// <b>Une date postérieure à aujourd'hui à Paris est refusée</b>, et celle du jour acceptée. À
  /// 23 h 30 UTC le 10 janvier, il est déjà le 11 à Paris : le 11 passe, le 12 non.
  /// </summary>
  [Fact]
  public async Task RefusesADateAfterTodayInParis()
  {
    await using var context = await harness.NewContextAsync();
    await context.Clock.SetFixedTimeAsync("2026-01-10T23:30:00Z");
    var page = await OpenedAsync(context);
    var receivedOn = Field(page, "Date de réception");

    await receivedOn.FillAsync("2026-01-12");
    await CreateAsync(page);
    await ExpectTheRefusalAsync(page, "Date de réception", ReceivedOnInTheFuture);

    await receivedOn.FillAsync("2026-01-11");
    await ExpectNoRefusalAsync(page, "Date de réception");
  }

  /// <summary>
  /// <b>Un texte collé au-delà de sa limite reste entier</b> — rien ne le tronque — <b>et produit le
  /// message de dépassement</b> : 101 caractères pour le nom et le prénom, 255 pour l'email, 10 001
  /// pour le message.
  /// </summary>
  [Fact]
  public async Task KeepsATextPastItsLimitWholeAndRefusesIt()
  {
    await using var context = await harness.NewContextAsync();
    var page = await OpenedAsync(context);

    var lastName = new string('N', 101);
    var firstName = new string('P', 101);
    var email = new string('a', 243) + "@example.org";
    var message = new string('m', 10_001);

    await Field(page, "Nom").FillAsync(lastName);
    await Field(page, "Prénom").FillAsync(firstName);
    await Field(page, "Email").FillAsync(email);
    await Field(page, "Message").FillAsync(message);
    await CreateAsync(page);

    await Expect(Field(page, "Nom")).ToHaveValueAsync(lastName);
    await Expect(Field(page, "Prénom")).ToHaveValueAsync(firstName);
    await Expect(Field(page, "Email")).ToHaveValueAsync(email);
    await Expect(Field(page, "Message")).ToHaveValueAsync(message);

    await ExpectTheRefusalAsync(page, "Nom", LastNameTooLong);
    await ExpectTheRefusalAsync(page, "Prénom", FirstNameTooLong);
    await ExpectTheRefusalAsync(page, "Email", EmailInvalid);
    await ExpectTheRefusalAsync(page, "Message", MessageTooLong);
  }

  /// <summary>
  /// <b>Les limites se comptent au caractère près, en unités UTF-16</b>, comme le <c>.Length</c> du
  /// serveur : 100 caractères passent, 254 pour l'email, 10 000 pour le message — et un émoji en
  /// compte deux.
  /// </summary>
  [Fact]
  public async Task AcceptsATextAtItsLimitCountedInUtf16Units()
  {
    await using var context = await harness.NewContextAsync();
    var page = await OpenedAsync(context);

    await Field(page, "Nom").FillAsync(new string('N', 100));
    await Field(page, "Prénom").FillAsync(string.Concat(Enumerable.Repeat("😀", 50)));
    await Field(page, "Email").FillAsync(new string('a', 242) + "@example.org");
    await Field(page, "Message").FillAsync(new string('m', 10_000));
    await CreateAsync(page);

    foreach (var label in new[] { "Nom", "Prénom", "Email", "Message" })
    {
      await ExpectNoRefusalAsync(page, label);
    }

    await Field(page, "Prénom").FillAsync(string.Concat(Enumerable.Repeat("😀", 50)) + "a");
    await CreateAsync(page);

    await ExpectTheRefusalAsync(page, "Prénom", FirstNameTooLong);
  }

  /// <summary>
  /// <b>Un nom seul, sans email, n'identifie pas</b> : le message va sous l'email et sous le prénom
  /// qui manque — pas sous le nom, qui est là.
  /// </summary>
  [Fact]
  public async Task PlacesTheIdentificationRefusalUnderTheEmailAndTheMissingName()
  {
    await using var context = await harness.NewContextAsync();
    var page = await OpenedAsync(context);

    await Field(page, "Nom").FillAsync("Dupont");
    await CreateAsync(page);

    await ExpectTheRefusalAsync(page, "Email", IdentificationMissing);
    await ExpectTheRefusalAsync(page, "Prénom", IdentificationMissing);
    await ExpectNoRefusalAsync(page, "Nom");
  }

  /// <summary><b>Avec un email, un nom partiel ne bloque pas</b> : l'email suffit à identifier.</summary>
  [Fact]
  public async Task LetsAPartialNameThroughWhenTheEmailIsGiven()
  {
    await using var context = await harness.NewContextAsync();
    var page = await OpenedAsync(context);

    await Field(page, "Nom").FillAsync("Dupont");
    await Field(page, "Email").FillAsync("marie.dupont@example.org");
    await CreateAsync(page);

    await ExpectNoRefusalAsync(page, "Nom");
    await ExpectNoRefusalAsync(page, "Prénom");
    await ExpectNoRefusalAsync(page, "Email");
    await ExpectTheRefusalAsync(page, "Message", MessageMissing);
  }

  /// <summary>
  /// <b>Des espaces seuls valent un champ vide</b> — le message est obligatoire, le nom manque — et
  /// <b>les bordures ne comptent pas</b> : un email entouré d'espaces reste valide.
  /// </summary>
  [Fact]
  public async Task TrimsEveryFieldBeforeJudgingIt()
  {
    await using var context = await harness.NewContextAsync();
    var page = await OpenedAsync(context);

    await Field(page, "Nom").FillAsync("   ");
    await Field(page, "Prénom").FillAsync("Marie");
    await Field(page, "Message").FillAsync(" \n\t ");
    await CreateAsync(page);

    await ExpectTheRefusalAsync(page, "Nom", IdentificationMissing);
    await ExpectTheRefusalAsync(page, "Email", IdentificationMissing);
    await ExpectNoRefusalAsync(page, "Prénom");
    await ExpectTheRefusalAsync(page, "Message", MessageMissing);

    await Field(page, "Email").FillAsync("  marie.dupont@example.org  ");
    await Field(page, "Nom").FillAsync("  " + new string('N', 100) + "  ");
    await CreateAsync(page);

    await ExpectNoRefusalAsync(page, "Email");
    await ExpectNoRefusalAsync(page, "Nom");
  }

  /// <summary>
  /// <b>Un formulaire rouvert repart sans erreur</b>, et sans première tentative : la saisie qui suit
  /// n'affiche rien avant le prochain clic sur « Créer ».
  /// </summary>
  [Fact]
  public async Task ForgetsItsRefusalsWhenReopened()
  {
    await using var context = await harness.NewContextAsync();
    var page = await OpenedAsync(context);
    await CreateAsync(page);
    await ExpectTheRefusalAsync(page, "Message", MessageMissing);

    await Dialog(page).GetByRole(AriaRole.Button, new() { Name = "Annuler", Exact = true }).ClickAsync();
    await OpenAsync(page);

    foreach (var label in Validated)
    {
      await ExpectNoRefusalAsync(page, label);
    }

    await Field(page, "Email").FillAsync("pas-un-email");
    await ExpectNoRefusalAsync(page, "Email");
  }

  private static async Task ExpectTheRefusalAsync(IPage page, string label, string message)
  {
    await Expect(Field(page, label)).ToHaveAccessibleDescriptionAsync(message);
    await Expect(Field(page, label)).ToHaveAttributeAsync("aria-invalid", "true");
  }

  private static async Task ExpectNoRefusalAsync(IPage page, string label)
  {
    await Expect(Field(page, label)).ToHaveAccessibleDescriptionAsync(new Regex("^$"));
    await Expect(Field(page, label)).Not.ToHaveAttributeAsync("aria-invalid", "true");
  }

  private static async Task<IPage> OpenedAsync(IBrowserContext context)
  {
    var page = await context.NewPageAsync();
    await page.GotoAsync("/demandes");
    await OpenAsync(page);

    return page;
  }

  private static async Task OpenAsync(IPage page)
  {
    await page.GetByRole(AriaRole.Button, new() { Name = "Créer une demande", Exact = true }).ClickAsync();
    await Expect(Dialog(page)).ToBeVisibleAsync();
  }

  private static Task CreateAsync(IPage page)
  {
    return CreateButton(page).ClickAsync();
  }

  private static ILocator CreateButton(IPage page)
  {
    return Dialog(page).GetByRole(AriaRole.Button, new() { Name = "Créer", Exact = true });
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
