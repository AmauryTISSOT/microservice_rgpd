using System.Text.RegularExpressions;
using MicroserviceRgpd.Core.Qualifications;
using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.BrowserTests.Requests;

/// <summary>
/// <b>La proposition de droit vit et meurt avec la modale</b>, dans un vrai navigateur : elle n'a
/// d'effet qu'à travers l'<c>Operator</c>, et ne survit pas à la modale où elle a été demandée.
/// </summary>
/// <remarks>
/// <para>
/// Le chemin réel passe par les doublures de moteur du harnais ; l'attente, qu'il faut pouvoir faire
/// durer au-delà d'une fermeture, se retient par l'interception de route de Playwright.
/// </para>
/// <para>
/// ⚠️ <b>Chaque proposition dicte ici <c>Portability</c></b>, qu'aucune modale n'a d'elle-même : une
/// réponse tardive qui s'appliquerait se lirait dans le select.
/// </para>
/// <para>
/// Tout se lit par le rôle et le nom accessible ; les phrases sont recopiées à dessein.
/// </para>
/// </remarks>
[Collection(BrowserCollection.Name)]
public class ProposalLifetime
{
  private const string Qualify = "Qualification du droit par IA";

  private const string CreationTitle = "Créer une nouvelle demande";

  private const string ModificationTitle = "Modifier la demande";

  private const string ConfirmationTitle = "Abandonner la saisie ?";

  /// <summary>La mention d'une proposition à relire, recopiée à dessein.</summary>
  private const string ToReview = "À relire";

  private const string Justification = "La personne demande à emporter ses données.";

  /// <summary>L'appel au handler <c>Propose</c>, celui que le bouton déclenche.</summary>
  private static readonly Regex ProposeHandler = new(@"/qualification\?handler=Propose$");

  /// <summary>L'appel au handler de création du tableau.</summary>
  private static readonly Regex CreateHandler = new(@"/demandes\?handler=Create$");

  private readonly BrowserHarness _harness;

  public ProposalLifetime(BrowserHarness harness)
  {
    _harness = harness;

    harness.Verdict.Reset();
    harness.Lexicon.Reset();

    harness.Verdict.Qualification = Qualification.Of([DataSubjectRight.Portability]);
    harness.Lexicon.Qualification = Qualification.Of([DataSubjectRight.Portability]);
    harness.Verdict.Justification = Justification;
  }

  /// <summary>
  /// Chaque fermeture effective d'une modale, avec le mode où elle se fait : l'abandon confirmé et
  /// l'enregistrement réussi à la création, où la saisie est modifiée ; les fermetures sans changement
  /// à la modification, dont le Message est déjà là.
  /// </summary>
  public static TheoryData<string, string> EffectiveClosings { get; } = new()
  {
    { "create", "l'abandon confirmé" },
    { "create", "l'enregistrement réussi" },
    { "modify", "Annuler" },
    { "modify", "la croix" },
    { "modify", "Échap" },
    { "modify", "le fond" },
    { "modify", "Modifier sans rien corriger" },
  };

  /// <summary>
  /// <b>Une fermeture effective abandonne la qualification en cours</b>, quel qu'en soit le geste :
  /// la réponse qui arrive après ne touche ni la modale, ni celle qu'on a rouverte depuis, et la
  /// réouverture ne montre aucune note.
  /// </summary>
  [Theory]
  [MemberData(nameof(EffectiveClosings))]
  public async Task AbandonsTheQualificationWhenTheDialogReallyCloses(string mode, string closing)
  {
    await using var context = await _harness.NewContextAsync();
    var (dialog, email) = await OpenAsync(context, mode);
    var page = dialog.Page;
    var held = await HoldTheProposalAsync(page);

    if (mode == "create")
    {
      await FillTheEntryAsync(dialog, email);
    }

    await QualifyButton(dialog).ClickAsync();
    await Expect(QualifyButton(dialog)).ToHaveAttributeAsync("aria-busy", "true");

    await CloseByAsync(dialog, closing);
    await Expect(dialog).ToBeHiddenAsync();

    var reopened = await ReopenAsync(page, mode, email);
    await held.ReleaseAsync();

    var expected = mode == "create" ? string.Empty : "Erasure";
    await Expect(Field(reopened, "Droits RGPD")).ToHaveValueAsync(expected);
    await Expect(Note(reopened)).ToBeHiddenAsync();
    await Expect(QualifyButton(reopened)).ToBeEnabledAsync();
    await Expect(QualifyButton(reopened)).Not.ToHaveAttributeAsync("aria-busy", new Regex("."));
    await Expect(Rows(page, email)).ToHaveCountAsync(closing == "l'abandon confirmé" ? 0 : 1);
  }

  /// <summary>
  /// <b>Une proposition arrivée ne survit pas à la fermeture</b> : la modale rouverte ne montre
  /// aucune note.
  /// </summary>
  [Fact]
  public async Task ShowsNoNoteOnceTheDialogIsReopened()
  {
    await using var context = await _harness.NewContextAsync();
    var (dialog, email) = await OpenAsync(context, "modify");
    var page = dialog.Page;

    await QualifyButton(dialog).ClickAsync();
    await Expect(Note(dialog)).ToContainTextAsync(Justification);

    await CloseByAsync(dialog, "la croix");
    await Button(Confirmation(page), "Abandonner").ClickAsync();
    await Expect(dialog).ToBeHiddenAsync();

    var reopened = await ReopenAsync(page, "modify", email);

    await Expect(Note(reopened)).ToBeHiddenAsync();
  }

  /// <summary>
  /// <b>Ouvrir la confirmation d'abandon ne l'abandonne pas</b> : revenir à la saisie laisse courir la
  /// qualification, et la proposition arrive — y compris quand le navigateur n'a pas laissé retenir
  /// Échap, et que la modale s'est rouverte sous la confirmation.
  /// </summary>
  [Theory]
  [InlineData("la croix")]
  [InlineData("Échap non retenu")]
  public async Task LetsTheQualificationRunWhenTheEntryIsResumed(string closing)
  {
    await using var context = await _harness.NewContextAsync();
    var (dialog, _) = await OpenAsync(context, "create");
    var page = dialog.Page;
    var held = await HoldTheProposalAsync(page);

    await Field(dialog, "Message").FillAsync("Envoyez-moi mes données dans un format lisible.");
    await QualifyButton(dialog).ClickAsync();
    await Expect(QualifyButton(dialog)).ToHaveAttributeAsync("aria-busy", "true");

    await CloseByAsync(dialog, closing);
    await Expect(Confirmation(page)).ToBeVisibleAsync();
    await Button(Confirmation(page), "Continuer la saisie").ClickAsync();
    await Expect(Confirmation(page)).ToBeHiddenAsync();

    await held.ReleaseAsync();

    await Expect(Field(dialog, "Droits RGPD")).ToHaveValueAsync("Portability");
    await Expect(Note(dialog)).ToContainTextAsync(Justification);
  }

  /// <summary>
  /// <b>Un « Créer » refusé par le serveur laisse courir la qualification</b> : la modale reste
  /// ouverte, et la proposition y arrive.
  /// </summary>
  [Fact]
  public async Task LetsTheQualificationRunWhenTheSavingIsRefused()
  {
    await using var context = await _harness.NewContextAsync();
    var (dialog, email) = await OpenAsync(context, "create");
    var page = dialog.Page;
    var held = await HoldTheProposalAsync(page);
    await page.RouteAsync(CreateHandler, route => route.FulfillAsync(new()
    {
      Status = 400,
      ContentType = "application/problem+json",
      Body = """{"status":400,"errors":{"lastName":["Le nom ne peut pas dépasser 100 caractères."]}}""",
    }));

    await FillTheEntryAsync(dialog, email);
    await QualifyButton(dialog).ClickAsync();
    await Expect(QualifyButton(dialog)).ToHaveAttributeAsync("aria-busy", "true");

    await Button(dialog, "Créer").ClickAsync();
    await Expect(Field(dialog, "Nom")).ToHaveAccessibleDescriptionAsync("Le nom ne peut pas dépasser 100 caractères.");

    await held.ReleaseAsync();

    await Expect(dialog).ToBeVisibleAsync();
    await Expect(Field(dialog, "Droits RGPD")).ToHaveValueAsync("Portability");
    await Expect(Note(dialog)).ToContainTextAsync(Justification);
  }

  /// <summary>
  /// <b>Un select rempli par la proposition est une valeur changée</b> : fermer demande la
  /// confirmation d'abandon, et abandonner ne crée ni ne modifie la demande.
  /// </summary>
  /// <remarks>
  /// À la modification, le Message est déjà là : seule la proposition a changé la saisie.
  /// </remarks>
  [Theory]
  [InlineData("create")]
  [InlineData("modify")]
  public async Task AbandoningAProposalNeitherCreatesNorModifiesTheRequest(string mode)
  {
    await using var context = await _harness.NewContextAsync();
    var (dialog, email) = await OpenAsync(context, mode);
    var page = dialog.Page;

    if (mode == "create")
    {
      await FillTheEntryAsync(dialog, email);
    }

    var message = await Field(dialog, "Message").InputValueAsync();

    await QualifyButton(dialog).ClickAsync();
    await Expect(Field(dialog, "Droits RGPD")).ToHaveValueAsync("Portability");

    await CloseByAsync(dialog, "la croix");
    await Expect(Confirmation(page)).ToBeVisibleAsync();
    await Button(Confirmation(page), "Abandonner").ClickAsync();
    await Expect(dialog).ToBeHiddenAsync();

    (await _harness.CountOfRequestsAsync(message)).ShouldBe(mode == "create" ? 0 : 1);

    if (mode == "modify")
    {
      await page.ReloadAsync();
      var reopened = await ReopenAsync(page, mode, email);

      await Expect(Field(reopened, "Droits RGPD")).ToHaveValueAsync("Erasure");
    }
  }

  /// <summary>
  /// <b>La note reste quand l'<c>Operator</c> reprend la main</b> : changer le select, puis le
  /// Message, n'efface pas ce qui lui a été proposé.
  /// </summary>
  [Fact]
  public async Task KeepsTheNoteWhenTheOperatorChangesTheRightOrTheMessage()
  {
    await using var context = await _harness.NewContextAsync();
    var (dialog, _) = await OpenAsync(context, "modify");

    await QualifyButton(dialog).ClickAsync();
    await Expect(Note(dialog)).ToContainTextAsync(Justification);

    await Field(dialog, "Droits RGPD").SelectOptionAsync("Objection");
    await Expect(Note(dialog)).ToContainTextAsync(Justification);

    await Field(dialog, "Message").FillAsync("Finalement, je m'oppose au traitement.");
    await Expect(Note(dialog)).ToContainTextAsync(Justification);
    await Expect(Note(dialog)).ToBeVisibleAsync();
  }

  /// <summary>
  /// <b>Une nouvelle qualification remplace la note de la précédente</b> : sa justification, et sa
  /// mention « À relire ».
  /// </summary>
  [Fact]
  public async Task ReplacesTheNoteOfThePreviousQualification()
  {
    const string First = "Le texte pourrait viser la portabilité.";
    var confident = _harness.Verdict.DeclaredConfidence;

    _harness.Verdict.Justification = First;
    _harness.Verdict.DeclaredConfidence = DeclaredConfidence.Low;

    await using var context = await _harness.NewContextAsync();
    var (dialog, _) = await OpenAsync(context, "modify");

    await QualifyButton(dialog).ClickAsync();
    await Expect(Note(dialog)).ToContainTextAsync(First);
    await Expect(Note(dialog).GetByText(ToReview, new() { Exact = true })).ToBeVisibleAsync();

    _harness.Verdict.Justification = Justification;
    _harness.Verdict.DeclaredConfidence = confident;
    var answered = dialog.Page.WaitForResponseAsync(ProposeHandler);
    await QualifyButton(dialog).ClickAsync();
    await answered;

    await Expect(Note(dialog)).ToContainTextAsync(Justification);
    await Expect(Note(dialog)).Not.ToContainTextAsync(First);
    await Expect(Note(dialog).GetByText(ToReview, new() { Exact = true })).ToBeHiddenAsync();
  }

  /// <summary>
  /// <b>Une nouvelle qualification remplace la note même quand elle échoue</b> — coupure réseau ou
  /// réponse illisible : la note de la précédente ne reste pas sous les yeux.
  /// </summary>
  [Theory]
  [InlineData("une coupure réseau")]
  [InlineData("une réponse illisible")]
  public async Task ReplacesTheNoteEvenWhenTheNextQualificationFails(string failure)
  {
    await using var context = await _harness.NewContextAsync();
    var (dialog, _) = await OpenAsync(context, "modify");
    var page = dialog.Page;

    await QualifyButton(dialog).ClickAsync();
    await Expect(Note(dialog)).ToContainTextAsync(Justification);

    await page.RouteAsync(ProposeHandler, route => failure == "une coupure réseau"
      ? route.AbortAsync()
      : route.FulfillAsync(new() { Status = 200, ContentType = "application/json", Body = "{" }));
    await QualifyButton(dialog).ClickAsync();

    await Expect(Note(dialog)).ToBeHiddenAsync();
    await Expect(QualifyButton(dialog)).ToBeEnabledAsync();
  }

  /// <summary>
  /// Retient la réponse du handler <c>Propose</c> jusqu'à ce que le test la libère. La libération
  /// attend que la requête ait fini — répondue, ou abandonnée par la page — et que la page ait eu le
  /// temps de s'en saisir : ce qu'une réponse tardive ferait est alors fait.
  /// </summary>
  private static async Task<HeldProposal> HoldTheProposalAsync(IPage page)
  {
    var held = new HeldProposal(page);

    page.RequestFinished += (_, request) => held.Ended(request);
    page.RequestFailed += (_, request) => held.Ended(request);

    await page.RouteAsync(ProposeHandler, async route =>
    {
      await held.Released;

      try
      {
        await route.ContinueAsync();
      }
      catch (PlaywrightException)
      {
        // La page a abandonné la requête : il n'y a plus rien à laisser passer.
      }
    });

    return held;
  }

  private sealed class HeldProposal(IPage page)
  {
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly TaskCompletionSource _ended = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Released => _release.Task;

    public void Ended(IRequest request)
    {
      if (ProposeHandler.IsMatch(request.Url))
      {
        _ended.TrySetResult();
      }
    }

    public async Task ReleaseAsync()
    {
      _release.SetResult();
      await _ended.Task.WaitAsync(TimeSpan.FromSeconds(30));

      // La réponse lue, le module la traite en tâches et micro-tâches : une tâche plus tard, c'est fait.
      await page.EvaluateAsync("() => new Promise(done => setTimeout(done, 100))");
    }
  }

  /// <summary>
  /// Ouvre le tableau, puis la modale dans ce mode — sur une demande enregistrée au droit
  /// d'effacement, pour la modification — et rend la modale ouverte, avec l'email unique de la
  /// demande qu'elle crée ou modifie.
  /// </summary>
  private async Task<(ILocator Dialog, string Email)> OpenAsync(IBrowserContext context, string mode)
  {
    var page = await context.NewPageAsync();
    var email = $"{Guid.NewGuid():N}@example.org";

    if (mode == "modify")
    {
      await _harness.RecordRequestAsync(
        lastName: "Martin", firstName: "Jeanne", email: email, origin: Origin.Letter, right: "Erasure");
    }

    await page.GotoAsync("/demandes");

    return (await ReopenAsync(page, mode, email), email);
  }

  /// <summary>Ouvre la modale dans ce mode, depuis le tableau déjà chargé.</summary>
  private static async Task<ILocator> ReopenAsync(IPage page, string mode, string email)
  {
    if (mode == "create")
    {
      await page.GetByRole(AriaRole.Button, new() { Name = "Créer une demande", Exact = true }).ClickAsync();

      var creation = Dialog(page, CreationTitle);
      await Expect(creation).ToBeVisibleAsync();

      return creation;
    }

    await Rows(page, email).GetByRole(AriaRole.Button, new() { Name = "Modifier la demande", Exact = true }).ClickAsync();

    var modification = Dialog(page, ModificationTitle);
    await Expect(modification).ToBeVisibleAsync();

    return modification;
  }

  /// <summary>Une saisie de création valide, sous cet email, au droit d'accès.</summary>
  private static async Task FillTheEntryAsync(ILocator dialog, string email)
  {
    await Field(dialog, "Email").FillAsync(email);
    await Field(dialog, "Message").FillAsync($"Envoyez-moi mes données dans un format lisible. {Guid.NewGuid()}");
    await Field(dialog, "Droits RGPD").SelectOptionAsync("Access");
  }

  private static Task CloseByAsync(ILocator dialog, string closing)
  {
    var page = dialog.Page;

    return closing switch
    {
      "Annuler" => Button(dialog, "Annuler").ClickAsync(),
      "la croix" => Button(dialog, "Fermer").ClickAsync(),
      "Échap" => page.Keyboard.PressAsync("Escape"),
      "le fond" => page.Mouse.ClickAsync(5, 5),
      "Modifier sans rien corriger" => Button(dialog, "Modifier").ClickAsync(),
      "l'enregistrement réussi" => Button(dialog, "Créer").ClickAsync(),
      "l'abandon confirmé" => AbandonAsync(dialog),
      "Échap non retenu" => dialog.EvaluateAsync(
        "dialog => { dialog.dispatchEvent(new Event('cancel', { cancelable: false })); dialog.close(); }"),
      _ => throw new ArgumentOutOfRangeException(nameof(closing), closing, "Fermeture inconnue."),
    };
  }

  private static async Task AbandonAsync(ILocator dialog)
  {
    await Button(dialog, "Fermer").ClickAsync();
    await Button(Confirmation(dialog.Page), "Abandonner").ClickAsync();
  }

  private static ILocator Dialog(IPage page, string title)
  {
    return page.GetByRole(AriaRole.Dialog, new() { Name = title, Exact = true });
  }

  private static ILocator Confirmation(IPage page)
  {
    return page.GetByRole(AriaRole.Alertdialog, new() { Name = ConfirmationTitle, Exact = true });
  }

  private static ILocator Rows(IPage page, string email)
  {
    return page.GetByRole(AriaRole.Row).Filter(new() { HasText = email });
  }

  private static ILocator Field(ILocator dialog, string label)
  {
    return dialog.GetByLabel(label, new() { Exact = true });
  }

  private static ILocator Button(ILocator surface, string name)
  {
    return surface.GetByRole(AriaRole.Button, new() { Name = name, Exact = true });
  }

  private static ILocator QualifyButton(ILocator dialog)
  {
    return Button(dialog, Qualify);
  }

  /// <summary>La note de la proposition, sous le select : la note de la modale.</summary>
  private static ILocator Note(ILocator dialog)
  {
    return dialog.GetByRole(AriaRole.Note);
  }
}
