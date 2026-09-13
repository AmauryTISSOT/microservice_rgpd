using System.Text.RegularExpressions;
using MicroserviceRgpd.Core.Qualifications;
using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.BrowserTests.Requests;

/// <summary>
/// <b>La modale d'une demande fait proposer le droit par la qualification</b>, dans un vrai
/// navigateur : « Qualification du droit par IA » envoie le Message — et lui seul — au handler
/// <c>Propose</c> de l'écran de qualification, et un droit unique proposé est sélectionné, sa
/// justification sous le select.
/// </summary>
/// <remarks>
/// <para>
/// Le chemin réel passe par les doublures de moteur du harnais ; seule l'attente, qu'il faut pouvoir
/// observer, se retient par l'interception de route de Playwright.
/// </para>
/// <para>
/// ⚠️ <b>Plusieurs droits, <c>OutOfScope</c> et les échecs</b> ne sont pas gardés ici : ils viennent
/// avec leur propre ticket. La vie de la proposition à la fermeture de la modale est gardée par
/// <see cref="ProposalLifetime"/>.
/// </para>
/// <para>
/// Tout se lit par le rôle et le nom accessible ; les phrases sont recopiées à dessein.
/// </para>
/// </remarks>
[Collection(BrowserCollection.Name)]
public class RightProposal
{
  private const string Qualify = "Qualification du droit par IA";

  private const string CreationTitle = "Créer une nouvelle demande";

  private const string ModificationTitle = "Modifier la demande";

  /// <summary>Le refus d'un Message vide, recopié à dessein.</summary>
  private const string EmptyMessage = "Le message est vide : il n'y a rien à qualifier.";

  /// <summary>La mention d'une proposition à relire, recopiée à dessein.</summary>
  private const string ToReview = "À relire";

  private const string Justification = "La personne demande la suppression de ses données.";

  /// <summary>L'appel au handler <c>Propose</c>, celui que le bouton déclenche.</summary>
  private static readonly Regex ProposeHandler = new(@"/qualification\?handler=Propose$");

  private readonly BrowserHarness _harness;

  public RightProposal(BrowserHarness harness)
  {
    _harness = harness;

    harness.Verdict.Reset();
    harness.Lexicon.Reset();
  }

  /// <summary>Les deux modes de la modale : la création, et la modification d'une demande enregistrée.</summary>
  public static TheoryData<string> Modes { get; } = ["create", "modify"];

  /// <summary>
  /// <b>Le bouton est offert dans les deux modes</b> : actif, sous son libellé, sans l'infobulle
  /// « Bientôt disponible » ni aucune autre.
  /// </summary>
  [Theory]
  [MemberData(nameof(Modes))]
  public async Task OffersTheQualificationInBothModes(string mode)
  {
    await using var context = await _harness.NewContextAsync();
    var dialog = await OpenAsync(context, mode);

    var button = QualifyButton(dialog);

    await Expect(button).ToBeVisibleAsync();
    await Expect(button).ToBeEnabledAsync();
    await Expect(button).Not.ToHaveAttributeAsync("title", new Regex("."));
    await Expect(button).ToHaveAccessibleDescriptionAsync(string.Empty);
  }

  /// <summary>
  /// <b>Un Message vide ou fait d'espaces est refusé sous le champ Message</b> : aucune qualification
  /// ne part — ni requête, ni moteur appelé —, et le droit déjà choisi reste choisi.
  /// </summary>
  [Theory]
  [InlineData("")]
  [InlineData("   \n ")]
  public async Task RefusesAnEmptyMessageWithoutQualifying(string blank)
  {
    await using var context = await _harness.NewContextAsync();
    var dialog = await OpenAsync(context, "create");
    var page = dialog.Page;
    var sent = 0;
    page.Request += (_, request) => sent += ProposeHandler.IsMatch(request.Url) ? 1 : 0;

    await Field(dialog, "Droits RGPD").SelectOptionAsync("Objection");
    await Field(dialog, "Message").FillAsync(blank);
    await QualifyButton(dialog).ClickAsync();

    await Expect(Field(dialog, "Message")).ToHaveAccessibleDescriptionAsync(EmptyMessage);
    await Expect(Field(dialog, "Droits RGPD")).ToHaveValueAsync("Objection");
    await Expect(QualifyButton(dialog)).ToBeEnabledAsync();
    sent.ShouldBe(0, "Un Message vide est parti à la qualification.");
    _harness.Verdict.CallCount.ShouldBe(0);
    _harness.Lexicon.CallCount.ShouldBe(0);
  }

  /// <summary>
  /// <b>Le refus cède dès que le Message change</b> : il disait qu'il n'y avait rien à qualifier, et
  /// ce n'est plus vrai.
  /// </summary>
  [Fact]
  public async Task LiftsTheRefusalOnceTheMessageChanges()
  {
    await using var context = await _harness.NewContextAsync();
    var dialog = await OpenAsync(context, "create");

    await QualifyButton(dialog).ClickAsync();
    await Expect(Field(dialog, "Message")).ToHaveAccessibleDescriptionAsync(EmptyMessage);

    await Field(dialog, "Message").FillAsync("Supprimez mes données.");

    await Expect(Field(dialog, "Message")).ToHaveAccessibleDescriptionAsync(string.Empty);
  }

  /// <summary>
  /// <b>Pendant la qualification, le bouton dit qu'il travaille</b> : désactivé, <c>aria-busy</c>,
  /// son icône de chargement en vue et son libellé toujours lisible. Le reste de la modale reste
  /// utilisable — les champs se saisissent, et le bouton primaire est actif.
  /// </summary>
  [Theory]
  [MemberData(nameof(Modes))]
  public async Task ShowsTheLoadingWithoutLockingTheDialog(string mode)
  {
    Dictate(DataSubjectRight.Erasure);
    await using var context = await _harness.NewContextAsync();
    var dialog = await OpenAsync(context, mode);
    var release = await HoldTheProposalAsync(dialog.Page);

    await Field(dialog, "Message").FillAsync("Supprimez mes données.");
    await QualifyButton(dialog).ClickAsync();

    var button = QualifyButton(dialog);
    await Expect(button).ToHaveAttributeAsync("aria-busy", "true");
    await Expect(button).ToBeDisabledAsync();
    await Expect(button).ToHaveTextAsync(Qualify);
    await Expect(Spinner(button)).ToBeVisibleAsync();

    await Field(dialog, "Nom").FillAsync("Durand");
    await Expect(Field(dialog, "Nom")).ToHaveValueAsync("Durand");
    await Expect(Primary(dialog, mode)).ToBeEnabledAsync();

    release.SetResult();

    await Expect(Field(dialog, "Droits RGPD")).ToHaveValueAsync("Erasure");
  }

  /// <summary>
  /// <b>Toute fin de requête rend le bouton</b> — une proposition comme un échec : l'icône s'en va,
  /// <c>aria-busy</c> tombe, et le bouton se réactive pour une nouvelle qualification.
  /// </summary>
  [Theory]
  [InlineData("une proposition")]
  [InlineData("deux moteurs muets")]
  public async Task GivesTheButtonBackWhenTheRequestEnds(string ending)
  {
    Dictate(DataSubjectRight.Erasure);

    if (ending == "deux moteurs muets")
    {
      _harness.Verdict.Silence = new HttpRequestException("La doublure de verdict se tait.");
      _harness.Lexicon.Silence = new HttpRequestException("La doublure du lexique se tait.");
    }

    await using var context = await _harness.NewContextAsync();
    var dialog = await OpenAsync(context, "create");
    var page = dialog.Page;

    await Field(dialog, "Message").FillAsync("Supprimez mes données.");
    var answered = page.WaitForResponseAsync(ProposeHandler);
    await QualifyButton(dialog).ClickAsync();
    await answered;

    var button = QualifyButton(dialog);
    await Expect(button).ToBeEnabledAsync();
    await Expect(button).Not.ToHaveAttributeAsync("aria-busy", new Regex("."));
    await Expect(Spinner(button)).ToBeHiddenAsync();
  }

  /// <summary>
  /// <b>L'icône de chargement ne tourne que si le système ne demande pas que rien ne bouge</b> : sous
  /// <c>prefers-reduced-motion: reduce</c>, elle est là, immobile.
  /// </summary>
  [Theory]
  [InlineData(ReducedMotion.Reduce, false)]
  [InlineData(ReducedMotion.NoPreference, true)]
  public async Task TurnsTheIconOnlyWhenMotionIsWelcome(ReducedMotion motion, bool turns)
  {
    await using var context = await _harness.NewContextAsync(motion);
    var dialog = await OpenAsync(context, "create");
    var release = await HoldTheProposalAsync(dialog.Page);

    await Field(dialog, "Message").FillAsync("Supprimez mes données.");
    await QualifyButton(dialog).ClickAsync();

    var spinner = Spinner(QualifyButton(dialog));
    await Expect(spinner).ToBeVisibleAsync();
    var animation = await spinner.EvaluateAsync<string>("icon => getComputedStyle(icon).animationName");

    release.SetResult();

    (animation != "none").ShouldBe(turns, $"L'animation de l'icône est « {animation} ».");
  }

  /// <summary>
  /// <b>Un droit unique proposé est sélectionné, même si un autre l'était déjà</b>, et seul le
  /// Message est parti — ni l'origine, ni la personne.
  /// </summary>
  [Fact]
  public async Task SelectsTheSingleProposedRightOverThePreviousChoice()
  {
    Dictate(DataSubjectRight.Portability);
    await using var context = await _harness.NewContextAsync();
    var dialog = await OpenAsync(context, "modify");
    var page = dialog.Page;
    await Expect(Field(dialog, "Droits RGPD")).ToHaveValueAsync("Erasure");

    var message = await Field(dialog, "Message").InputValueAsync();
    var sent = page.WaitForRequestAsync(ProposeHandler);
    await QualifyButton(dialog).ClickAsync();
    var request = await sent;

    await Expect(Field(dialog, "Droits RGPD")).ToHaveValueAsync("Portability");

    var fields = System.Web.HttpUtility.ParseQueryString(request.PostData ?? string.Empty);
    fields.AllKeys.Order(StringComparer.Ordinal).ShouldBe(["Text", "__RequestVerificationToken"]);
    fields["Text"].ShouldBe(message);
    _harness.Verdict.ReceivedText.ShouldNotBeNull().Value.ShouldBe(message);
  }

  /// <summary>
  /// <b>La note sous le select porte la justification</b>, et « À relire » quand la proposition n'est
  /// pas corroborée ou que le service n'était pas entier — jamais sinon.
  /// </summary>
  [Theory]
  [InlineData("Corroborated", false)]
  [InlineData("NeedsReview", true)]
  [InlineData("Contested", true)]
  [InlineData("degraded", true)]
  public async Task NotesTheJustificationAndWhetherToReviewIt(string outcome, bool toReview)
  {
    Dictate(DataSubjectRight.Erasure);
    _harness.Verdict.Justification = Justification;

    switch (outcome)
    {
      case "NeedsReview":
        _harness.Verdict.DeclaredConfidence = DeclaredConfidence.Low;
        break;
      case "Contested":
        _harness.Lexicon.Qualification = Qualification.Of([DataSubjectRight.Access]);
        break;
      case "degraded":
        _harness.Lexicon.Silence = new HttpRequestException("La doublure du lexique se tait.");
        break;
    }

    await using var context = await _harness.NewContextAsync();
    var dialog = await OpenAsync(context, "create");

    await Field(dialog, "Message").FillAsync("Supprimez mes données.");
    await QualifyButton(dialog).ClickAsync();

    var note = Note(dialog);
    await Expect(note).ToContainTextAsync(Justification);

    // La mention se lit à l'écran, ou pas : le texte d'un élément caché compterait dans le contenu.
    var mention = note.GetByText(ToReview, new() { Exact = true });

    if (toReview)
    {
      await Expect(mention).ToBeVisibleAsync();
    }
    else
    {
      await Expect(mention).ToBeHiddenAsync();
    }
  }

  /// <summary>
  /// ⚠️ <b>« À relire » accompagne tout service non entier, même un signal <c>Corroborated</c></b> :
  /// un verdict sans contrôle ne se lit jamais comme un verdict contrôlé.
  /// </summary>
  /// <remarks>
  /// Le domaine ne rend pas aujourd'hui ce couple — un moteur muet donne toujours <c>NeedsReview</c> —,
  /// et les doublures ne peuvent donc pas le produire : la réponse est dictée par interception de
  /// route, pour que la règle de l'écran tienne d'elle-même, sans dépendre de celle du domaine.
  /// </remarks>
  [Fact]
  public async Task AsksForAReviewOfADegradedProposalEvenWhenCorroborated()
  {
    await using var context = await _harness.NewContextAsync();
    var dialog = await OpenAsync(context, "create");
    await dialog.Page.RouteAsync(ProposeHandler, route => route.FulfillAsync(new()
    {
      Status = 200,
      ContentType = "application/json",
      Body = $$"""{"rights":[{"name":"Erasure","label":"droit à l'effacement"}],"reviewSignal":"Corroborated","degraded":true,"justification":"{{Justification}}"}""",
    }));

    await Field(dialog, "Message").FillAsync("Supprimez mes données.");
    await QualifyButton(dialog).ClickAsync();

    await Expect(Note(dialog)).ToContainTextAsync(Justification);
    await Expect(Note(dialog).GetByText(ToReview, new() { Exact = true })).ToBeVisibleAsync();
  }

  /// <summary>
  /// <b>Aucune note avant une qualification</b> : il n'y a encore rien à relire ni à justifier.
  /// </summary>
  [Fact]
  public async Task ShowsNoNoteBeforeAQualification()
  {
    await using var context = await _harness.NewContextAsync();
    var dialog = await OpenAsync(context, "create");

    await Expect(Note(dialog)).ToBeHiddenAsync();
  }

  /// <summary>Les deux doublures s'accordent sur ces droits.</summary>
  private void Dictate(params DataSubjectRight[] rights)
  {
    _harness.Verdict.Qualification = Qualification.Of(rights);
    _harness.Lexicon.Qualification = Qualification.Of(rights);
  }

  /// <summary>
  /// Retient la réponse du handler <c>Propose</c> jusqu'à ce que le test la libère : l'attente, qui
  /// passe sinon en un éclair, se laisse observer.
  /// </summary>
  private static async Task<TaskCompletionSource> HoldTheProposalAsync(IPage page)
  {
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    await page.RouteAsync(ProposeHandler, async route =>
    {
      await release.Task;
      await route.ContinueAsync();
    });

    return release;
  }

  /// <summary>
  /// Ouvre le tableau, puis la modale dans ce mode — sur une demande enregistrée au droit
  /// d'effacement, pour la modification — et rend la modale ouverte.
  /// </summary>
  private async Task<ILocator> OpenAsync(IBrowserContext context, string mode)
  {
    var page = await context.NewPageAsync();

    if (mode == "create")
    {
      await page.GotoAsync("/demandes");
      await page.GetByRole(AriaRole.Button, new() { Name = "Créer une demande", Exact = true }).ClickAsync();

      var creation = Dialog(page, CreationTitle);
      await Expect(creation).ToBeVisibleAsync();

      return creation;
    }

    var email = $"{Guid.NewGuid():N}@example.org";
    await _harness.RecordRequestAsync(
      lastName: "Martin", firstName: "Jeanne", email: email, origin: Origin.Letter, right: "Erasure");
    await page.GotoAsync("/demandes");

    var row = page.GetByRole(AriaRole.Row).Filter(new() { HasText = email });
    await row.GetByRole(AriaRole.Button, new() { Name = "Modifier la demande", Exact = true }).ClickAsync();

    var modification = Dialog(page, ModificationTitle);
    await Expect(modification).ToBeVisibleAsync();

    return modification;
  }

  private static ILocator Dialog(IPage page, string title)
  {
    return page.GetByRole(AriaRole.Dialog, new() { Name = title, Exact = true });
  }

  private static ILocator Field(ILocator dialog, string label)
  {
    return dialog.GetByLabel(label, new() { Exact = true });
  }

  private static ILocator QualifyButton(ILocator dialog)
  {
    return dialog.GetByRole(AriaRole.Button, new() { Name = Qualify, Exact = true });
  }

  private static ILocator Primary(ILocator dialog, string mode)
  {
    return dialog.GetByRole(AriaRole.Button, new() { Name = mode == "create" ? "Créer" : "Modifier", Exact = true });
  }

  /// <summary>
  /// L'icône de chargement : l'image que le bouton porte. Elle n'a ni rôle ni nom — elle est cachée
  /// aux technologies d'assistance, qui lisent <c>aria-busy</c> —, et se lit donc par sa balise.
  /// </summary>
  private static ILocator Spinner(ILocator button)
  {
    return button.Locator("svg");
  }

  /// <summary>La note de la proposition, sous le select : la note de la modale.</summary>
  private static ILocator Note(ILocator dialog)
  {
    return dialog.GetByRole(AriaRole.Note);
  }
}
