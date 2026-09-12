using System.Text.RegularExpressions;
using MicroserviceRgpd.Core.Requests;

namespace MicroserviceRgpd.BrowserTests.Requests;

/// <summary>
/// <b>Le crayon d'une demande, dans un vrai navigateur</b> : offert tant que la demande est En
/// cours, éteint sur une demande close — Terminée ou Annulée —, où il dit <b>pourquoi</b>. Offert,
/// il ouvre la <b>modale pré-remplie</b> des valeurs de sa demande, en mode « Modifier ».
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Le crayon éteint porte <c>aria-disabled</c>, jamais <c>disabled</c></b> : l'infobulle des
/// boutons de ligne se montre au survol et au focus visible, qu'un bouton réellement désactivé ne
/// reçoit ni l'un ni l'autre. L'explication exigée ne s'afficherait alors jamais — ni à la souris,
/// ni au clavier. C'est aussi pourquoi le module teste <c>aria-disabled</c>, et non <c>disabled</c>,
/// pour savoir si le geste est offert.
/// </para>
/// <para>
/// ⚠️ <b>Rien ne s'enregistre ici</b> : cette classe garde l'ouverture, l'abandon et le retour du
/// focus. L'enregistrement d'une modification viendra avec son US.
/// </para>
/// <para>
/// Les demandes se créent <b>par le use case</b>, et leur statut se pose <b>à même la table</b> :
/// aucun <c>Gesture</c> ne sait encore clore une demande. Chacune se retrouve par son email unique,
/// la base étant partagée par toute la collection. ⚠️ <b>Aucune de ses valeurs n'est celle par
/// défaut de la modale</b> : un pré-remplissage qui ne ferait rien se lirait sinon comme une
/// réussite.
/// </para>
/// <para>
/// Ce que le serveur rend se garde dans <c>RequestConsultation</c> et <c>RequestValuesReading</c>.
/// </para>
/// </remarks>
[Collection(BrowserCollection.Name)]
public class ModificationDialog(BrowserHarness harness)
{
  /// <summary>Le nom accessible du crayon, le même quel que soit le statut de la demande.</summary>
  private const string Pencil = "Modifier la demande";

  /// <summary>Ce que le crayon éteint donne à lire, recopié à dessein.</summary>
  private const string Refusal = "Une demande close ne peut plus être modifiée";

  /// <summary>Le titre de la modale en mode modification, et le libellé de son bouton primaire.</summary>
  private const string DialogTitle = "Modifier la demande";

  /// <summary>Le libellé du bouton primaire en mode modification, recopié à dessein.</summary>
  private const string SubmitLabel = "Modifier";

  /// <summary>Le titre de la modale en mode création : c'est celui que le serveur rend.</summary>
  private const string CreationTitle = "Créer une nouvelle demande";

  /// <summary>Le titre de la confirmation d'abandon, recopié à dessein.</summary>
  private const string ConfirmationTitle = "Abandonner la saisie ?";

  /// <summary>Ce que le toast dit d'un pré-remplissage en échec, recopié à dessein.</summary>
  private const string LoadFailed = "La demande n'a pas pu être chargée.";

  /// <summary>Les valeurs de la demande enregistrée, dont aucune n'est celle par défaut de la modale.</summary>
  private const string RecordedReceivedOn = "2025-12-24";

  /// <summary>Le nom enregistré : la modale part vide, il ne peut donc pas passer inaperçu.</summary>
  private const string RecordedLastName = "Martin";

  /// <summary>Le prénom enregistré, vide lui aussi par défaut.</summary>
  private const string RecordedFirstName = "Jeanne";

  /// <summary>Le droit invoqué : la modale n'en propose aucun par défaut.</summary>
  private const string RecordedRight = "Erasure";

  /// <summary>La lecture des valeurs d'une demande, celle que le crayon déclenche.</summary>
  private static readonly Regex ValuesHandler = new(@"/demandes\?handler=Values&");

  /// <summary>Les quatre modes de fermeture de la modale, comme pour la création.</summary>
  public static TheoryData<string> ClosingModes { get; } = ["Annuler", "la croix", "Échap", "le fond"];

  /// <summary>
  /// <b>Sur une demande En cours, le crayon est offert</b> : il ne porte pas <c>aria-disabled</c>, et
  /// son infobulle dit son nom, non un refus.
  /// </summary>
  [Fact]
  public async Task LeavesThePencilOfferedOnARequestInProgress()
  {
    await using var context = await harness.NewContextAsync();
    var row = await RowOfARequestAsync(context, status: null);
    var pencil = PencilOf(row);

    await Expect(pencil).Not.ToHaveAttributeAsync("aria-disabled", "true");
    await Expect(row.GetByText(Refusal, new() { Exact = true })).ToHaveCountAsync(0);

    await pencil.HoverAsync();

    await Expect(row.GetByText(Pencil, new() { Exact = true })).ToBeVisibleAsync();
  }

  /// <summary>
  /// <b>Sur une demande close, le crayon est éteint et dit pourquoi</b> : <c>aria-disabled</c>, et
  /// son explication, cachée tant qu'on ne le survole pas.
  /// </summary>
  [Theory]
  [InlineData(nameof(RequestStatus.Completed))]
  [InlineData(nameof(RequestStatus.Cancelled))]
  public async Task DimsThePencilOnAClosedRequestAndSaysWhyOnHover(string status)
  {
    await using var context = await harness.NewContextAsync();
    var row = await RowOfARequestAsync(context, RequestStatus.FromName(status));
    var pencil = PencilOf(row);
    var explanation = row.GetByText(Refusal, new() { Exact = true });

    await Expect(pencil).ToHaveAttributeAsync("aria-disabled", "true");
    await Expect(explanation).ToBeHiddenAsync();

    await pencil.HoverAsync();

    await Expect(explanation).ToBeVisibleAsync();
  }

  /// <summary>
  /// ⚠️ <b>L'explication s'atteint au clavier</b>, sans souris : la tabulation passe de la poubelle
  /// au crayon, dont le focus visible montre l'infobulle. C'est tout l'intérêt d'un bouton éteint par
  /// <c>aria-disabled</c> — un bouton désactivé ne recevrait pas ce focus.
  /// </summary>
  [Fact]
  public async Task ShowsTheExplanationToTheKeyboardAlone()
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var row = await RowOfARequestAsync(page, RequestStatus.Completed);
    var pencil = PencilOf(row);
    var explanation = row.GetByText(Refusal, new() { Exact = true });

    await row.GetByRole(AriaRole.Button, new() { Name = "Supprimer la demande", Exact = true }).FocusAsync();

    await Expect(explanation).ToBeHiddenAsync();

    await page.Keyboard.PressAsync("Tab");

    await Expect(pencil).ToBeFocusedAsync();
    await Expect(explanation).ToBeVisibleAsync();
  }

  /// <summary>
  /// <b>Le crayon ouvre la modale pré-remplie, en mode « Modifier »</b> : le titre et le bouton
  /// primaire le disent, les huit champs portent les valeurs enregistrées, et le focus arrive sur le
  /// premier champ — la correction commence sans toucher la souris.
  /// </summary>
  [Fact]
  public async Task OpensThePrefilledDialogOnThePencil()
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var request = await ARecordedRequestAsync(page);

    await Expect(Dialog(page)).ToBeHiddenAsync();

    await OpenTheModificationAsync(request);

    await Expect(Dialog(page).GetByRole(AriaRole.Heading, new() { Name = DialogTitle, Exact = true })).ToBeVisibleAsync();
    await Expect(Dialog(page).GetByRole(AriaRole.Button, new() { Name = SubmitLabel, Exact = true })).ToBeVisibleAsync();

    await ExpectTheRecordedValuesAsync(page, request);
    await Expect(Field(page, "Origine")).ToBeFocusedAsync();
  }

  /// <summary>
  /// ⚠️ <b>Pendant le chargement, tous les crayons du tableau sont éteints</b> — <c>aria-busy</c> et
  /// <c>aria-disabled</c>, sans que leur texte change : un libellé qui change sous le curseur
  /// déplace la mise en page et fait perdre au bouton son nom accessible en pleine action. Ce verrou
  /// <b>global</b> rend impossible d'ouvrir la mauvaise demande : un second clic sur une autre ligne
  /// ne lance rien, et c'est bien la demande du premier clic qui s'ouvre.
  /// </summary>
  [Fact]
  public async Task DimsEveryPencilWhileTheValuesLoadAndOpensTheRequestThatWasClicked()
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var other = await ARecordedRequestAsync(page);
    var sought = await ARecordedRequestAsync(page);

    var read = 0;
    var release = new TaskCompletionSource();
    await page.RouteAsync(ValuesHandler, async route =>
    {
      Interlocked.Increment(ref read);
      await release.Task;
      await route.ContinueAsync();
    });

    await PencilOf(sought.Row).ClickAsync();

    foreach (var pencil in new[] { PencilOf(sought.Row), PencilOf(other.Row) })
    {
      await Expect(pencil).ToHaveAttributeAsync("aria-busy", "true");
      await Expect(pencil).ToHaveAttributeAsync("aria-disabled", "true");
      await Expect(pencil).ToHaveAccessibleNameAsync(Pencil);
    }

    // Le second clic, sur une autre ligne, ne lance pas de seconde lecture. ⚠️ Il est forcé : un
    // crayon `aria-disabled` reste cliquable pour un humain, mais Playwright refuserait de l'atteindre.
    await PencilOf(other.Row).ClickAsync(new() { Force = true });

    release.SetResult();

    await Expect(Dialog(page)).ToBeVisibleAsync();
    read.ShouldBe(1, "Le verrou a laissé partir une seconde lecture.");
    await ExpectTheRecordedValuesAsync(page, sought);
  }

  /// <summary>
  /// ⚠️ <b>« Créer une demande » est verrouillé pendant le chargement, lui aussi</b> : la modale est
  /// unique, et une création ouverte entre-temps serait écrasée par les valeurs qui arrivent. C'est
  /// bien la modification qui s'ouvre, et elle seule.
  /// </summary>
  [Fact]
  public async Task LocksTheCreationWhileTheValuesLoad()
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var request = await ARecordedRequestAsync(page);
    var creation = page.GetByRole(AriaRole.Button, new() { Name = "Créer une demande", Exact = true });

    var release = new TaskCompletionSource();
    await page.RouteAsync(ValuesHandler, async route =>
    {
      await release.Task;
      await route.ContinueAsync();
    });

    await PencilOf(request.Row).ClickAsync();

    await Expect(creation).ToBeDisabledAsync();

    await creation.ClickAsync(new() { Force = true });
    release.SetResult();

    await Expect(Dialog(page)).ToBeVisibleAsync();
    await Expect(Creation(page)).ToBeHiddenAsync();
    await Expect(creation).ToBeEnabledAsync();
  }

  /// <summary>
  /// <b>Le verrou tombe à l'ouverture</b> : la modale refermée, les crayons sont rendus — celui d'une
  /// demande close excepté, qui reste éteint.
  /// </summary>
  [Fact]
  public async Task ReleasesThePencilsOnOpeningWithoutRelightingAClosedOne()
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var closed = await ARecordedRequestAsync(page, RequestStatus.Completed);
    var open = await ARecordedRequestAsync(page);

    await OpenTheModificationAsync(open);
    await CloseByAsync(page, "Annuler");

    await Expect(PencilOf(open.Row)).Not.ToHaveAttributeAsync("aria-busy", "true");
    await Expect(PencilOf(open.Row)).Not.ToHaveAttributeAsync("aria-disabled", "true");
    await Expect(PencilOf(closed.Row)).Not.ToHaveAttributeAsync("aria-busy", "true");
    await Expect(PencilOf(closed.Row)).ToHaveAttributeAsync("aria-disabled", "true");
  }

  /// <summary>
  /// <b>Un pré-remplissage en échec n'ouvre rien</b>, et le toast dit pourquoi. Le verrou tombe comme
  /// à l'ouverture : le crayon est aussitôt bon pour un second essai.
  /// </summary>
  [Theory]
  [InlineData("le serveur a échoué")]
  [InlineData("la demande n'existe plus")]
  [InlineData("le réseau est coupé")]
  public async Task SaysTheFailureWithoutOpeningTheDialog(string failure)
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var request = await ARecordedRequestAsync(page);

    await (failure switch
    {
      "le serveur a échoué" => page.RouteAsync(ValuesHandler, route => route.FulfillAsync(new() { Status = 500 })),
      "la demande n'existe plus" => page.RouteAsync(ValuesHandler, route => route.FulfillAsync(new() { Status = 404 })),
      _ => page.RouteAsync(ValuesHandler, route => route.AbortAsync("internetdisconnected")),
    });

    await PencilOf(request.Row).ClickAsync();

    await Expect(Toast(page)).ToHaveTextAsync(LoadFailed);
    await Expect(Dialog(page)).ToBeHiddenAsync();
    await Expect(PencilOf(request.Row)).Not.ToHaveAttributeAsync("aria-busy", "true");
    await Expect(PencilOf(request.Row)).Not.ToHaveAttributeAsync("aria-disabled", "true");
  }

  /// <summary>
  /// <b>Le crayon d'une demande close n'ouvre rien</b> : il est éteint par <c>aria-disabled</c>, que
  /// le navigateur laisse pourtant cliquer — c'est le module qui refuse, et aucune lecture ne part.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Le clic est forcé</b> : Playwright tient un <c>aria-disabled</c> pour inatteignable, là où
  /// le navigateur, lui, délivre le clic. C'est justement ce clic-là qui doit ne rien faire.
  /// </remarks>
  [Fact]
  public async Task OpensNothingOnThePencilOfAClosedRequest()
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var request = await ARecordedRequestAsync(page, RequestStatus.Cancelled);

    var read = 0;
    await page.RouteAsync(ValuesHandler, route =>
    {
      Interlocked.Increment(ref read);

      return route.ContinueAsync();
    });

    await PencilOf(request.Row).ClickAsync(new() { Force = true });

    await Expect(Dialog(page)).ToBeHiddenAsync();
    await Expect(page.GetByText(LoadFailed, new() { Exact = true })).ToHaveCountAsync(0);
    read.ShouldBe(0, "Le crayon d'une demande close a fait lire ses valeurs.");
  }

  /// <summary>
  /// <b>Une modale à laquelle personne n'a touché se ferme sans rien demander</b>, par chacun des
  /// quatre modes : il n'y a aucune saisie à perdre.
  /// </summary>
  [Theory]
  [MemberData(nameof(ClosingModes))]
  public async Task ClosesAnUntouchedModificationWithoutAsking(string mode)
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    await OpenTheModificationAsync(await ARecordedRequestAsync(page));

    await CloseByAsync(page, mode);

    await Expect(Dialog(page)).ToBeHiddenAsync();
    await Expect(Confirmation(page)).ToBeHiddenAsync();
  }

  /// <summary>
  /// <b>Une correction commencée ne se perd pas</b> : chacun des quatre modes demande confirmation,
  /// et « Continuer la saisie » — l'action par défaut — ramène au formulaire.
  /// </summary>
  [Theory]
  [MemberData(nameof(ClosingModes))]
  public async Task AsksBeforeClosingAModifiedEntryAndContinuesByDefault(string mode)
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var request = await ARecordedRequestAsync(page);
    await OpenTheModificationAsync(request);

    await Field(page, "Nom").FillAsync("Martinez");

    await CloseByAsync(page, mode);

    await Expect(Confirmation(page)).ToBeVisibleAsync();
    await Expect(Dialog(page)).ToBeVisibleAsync();
    await Expect(ConfirmationButton(page, "Continuer la saisie")).ToBeFocusedAsync();

    await page.Keyboard.PressAsync("Enter");

    await Expect(Confirmation(page)).ToBeHiddenAsync();
    await Expect(Dialog(page)).ToBeVisibleAsync();
    await Expect(Field(page, "Nom")).ToHaveValueAsync("Martinez");
  }

  /// <summary>
  /// ⚠️ <b>Une correction ramenée aux valeurs de départ ne modifie rien</b> : la modale se ferme
  /// sans rien demander, par chacun des quatre modes — il n'y a plus rien à perdre.
  /// </summary>
  [Theory]
  [MemberData(nameof(ClosingModes))]
  public async Task ClosesWithoutAskingWhenTheEntryCameBackToItsStartingValues(string mode)
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    await OpenTheModificationAsync(await ARecordedRequestAsync(page));

    await Field(page, "Nom").FillAsync("Martinez");
    await Field(page, "Nom").FillAsync(RecordedLastName);
    await Field(page, "Identité vérifiée").UncheckAsync();
    await Field(page, "Identité vérifiée").CheckAsync();

    await CloseByAsync(page, mode);

    await Expect(Dialog(page)).ToBeHiddenAsync();
    await Expect(Confirmation(page)).ToBeHiddenAsync();
  }

  /// <summary>
  /// ⚠️ <b>La comparaison porte sur les valeurs brutes</b>, celles trouvées à l'ouverture — et non
  /// sur ce que le service en ferait : des espaces ajoutés modifient, quand le service les rognerait.
  /// C'est la règle de la création, sur un autre point de départ.
  /// </summary>
  [Fact]
  public async Task AsksWhenOnlySpacesWereAdded()
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    await OpenTheModificationAsync(await ARecordedRequestAsync(page));

    await Field(page, "Nom").FillAsync($"{RecordedLastName} ");

    await CloseByAsync(page, "Annuler");

    await Expect(Confirmation(page)).ToBeVisibleAsync();
  }

  /// <summary>
  /// <b>Le focus revient sur le crayon de la ligne</b> après la fermeture : la navigation au clavier
  /// reprend là où elle s'était arrêtée.
  /// </summary>
  [Theory]
  [MemberData(nameof(ClosingModes))]
  public async Task GivesTheFocusBackToThePencilOfTheRow(string mode)
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var request = await ARecordedRequestAsync(page);
    await OpenTheModificationAsync(request);

    await CloseByAsync(page, mode);

    await Expect(Dialog(page)).ToBeHiddenAsync();
    await Expect(PencilOf(request.Row)).ToBeFocusedAsync();
  }

  /// <summary>
  /// ⚠️ <b>Si la ligne a disparu — supprimée depuis un autre onglet —, le focus va au cadre du
  /// tableau</b>, et non en haut de la page.
  /// </summary>
  [Fact]
  public async Task GivesTheFocusToTheTableFrameWhenTheRowIsGone()
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var request = await ARecordedRequestAsync(page);
    await OpenTheModificationAsync(request);

    await request.Row.EvaluateAsync("row => row.remove()");

    await CloseByAsync(page, "Annuler");

    await Expect(page.GetByRole(AriaRole.Region, new() { Name = "Liste des demandes", Exact = true })).ToBeFocusedAsync();
  }

  /// <summary>
  /// ⚠️ <b>« Créer une demande » rouvre une modale vide</b> après une modification refermée : les
  /// valeurs chargées ont été écrites en propriétés, jamais en attributs, et ne sont donc jamais
  /// devenues les valeurs par défaut du formulaire. Sans quoi l'<c>Operator</c> créerait par accident
  /// un doublon de la demande qu'il vient d'ouvrir.
  /// </summary>
  [Fact]
  public async Task ReopensAnEmptyCreationAfterAModification()
  {
    await using var context = await harness.NewContextAsync();
    await context.Clock.SetFixedTimeAsync("2026-01-10T10:00:00Z");
    var page = await context.NewPageAsync();
    await OpenTheModificationAsync(await ARecordedRequestAsync(page));
    await CloseByAsync(page, "Annuler");

    await page.GetByRole(AriaRole.Button, new() { Name = "Créer une demande", Exact = true }).ClickAsync();

    await Expect(Creation(page)).ToBeVisibleAsync();
    await Expect(Creation(page).GetByRole(AriaRole.Button, new() { Name = "Créer", Exact = true })).ToBeVisibleAsync();
    await Expect(CreationField(page, "Origine")).ToHaveValueAsync("Email");
    await Expect(CreationField(page, "Date de réception")).ToHaveValueAsync("2026-01-10");

    foreach (var blank in new[] { "Nom", "Prénom", "Email", "Message" })
    {
      await Expect(CreationField(page, blank)).ToHaveValueAsync(string.Empty);
    }

    await Expect(CreationField(page, "Identité vérifiée")).Not.ToBeCheckedAsync();
    await Expect(CreationField(page, "Droits RGPD")).ToHaveValueAsync(string.Empty);
  }

  private static async Task ExpectTheRecordedValuesAsync(IPage page, ARequest request)
  {
    await Expect(Field(page, "Origine")).ToHaveValueAsync("Letter");
    await Expect(Field(page, "Date de réception")).ToHaveValueAsync(RecordedReceivedOn);
    await Expect(Field(page, "Nom")).ToHaveValueAsync(RecordedLastName);
    await Expect(Field(page, "Prénom")).ToHaveValueAsync(RecordedFirstName);
    await Expect(Field(page, "Email")).ToHaveValueAsync(request.Email);
    await Expect(Field(page, "Identité vérifiée")).ToBeCheckedAsync();
    await Expect(Field(page, "Message")).ToHaveValueAsync(request.Message);
    await Expect(Field(page, "Droits RGPD")).ToHaveValueAsync(RecordedRight);
  }

  /// <summary>Ouvre la modale sur cette demande, et attend qu'elle soit là.</summary>
  private static async Task OpenTheModificationAsync(ARequest request)
  {
    await PencilOf(request.Row).ClickAsync();
    await Expect(Dialog(request.Page)).ToBeVisibleAsync();
  }

  /// <summary>
  /// Ferme la modale par l'un des quatre modes. Le clic sur le fond tombe <b>dans le coin de la
  /// fenêtre</b>, loin de la modale centrée, comme pour la création.
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

  private static ILocator PencilOf(ILocator row)
  {
    return row.GetByRole(AriaRole.Button, new() { Name = Pencil, Exact = true });
  }

  private static ILocator Dialog(IPage page)
  {
    return page.GetByRole(AriaRole.Dialog, new() { Name = DialogTitle, Exact = true, IncludeHidden = true });
  }

  private static ILocator Creation(IPage page)
  {
    return page.GetByRole(AriaRole.Dialog, new() { Name = CreationTitle, Exact = true, IncludeHidden = true });
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

  private static ILocator CreationField(IPage page, string label)
  {
    return Creation(page).GetByLabel(label, new() { Exact = true });
  }

  private static ILocator Toast(IPage page)
  {
    return page.GetByRole(AriaRole.Status);
  }

  private async Task<ILocator> RowOfARequestAsync(IBrowserContext context, RequestStatus? status)
  {
    return await RowOfARequestAsync(await context.NewPageAsync(), status);
  }

  private async Task<ILocator> RowOfARequestAsync(IPage page, RequestStatus? status)
  {
    return (await ARecordedRequestAsync(page, status)).Row;
  }

  /// <summary>
  /// Enregistre une demande, lui pose ce statut quand il en faut un autre qu'En cours, ouvre le
  /// tableau et rend de quoi la retrouver : sa ligne, son email unique et son message.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Le tableau est relu à chaque appel</b> : la seconde demande d'un scénario doit figurer sur
  /// la page où la première se lit déjà.
  /// </remarks>
  private async Task<ARequest> ARecordedRequestAsync(IPage page, RequestStatus? status = null)
  {
    var email = $"{Guid.NewGuid():N}@example.org";
    var message = await harness.RecordRequestAsync(
      lastName: RecordedLastName,
      firstName: RecordedFirstName,
      email: email,
      receivedOn: RecordedReceivedOn,
      origin: Origin.Letter,
      identityVerified: true,
      right: RecordedRight);

    if (status is not null)
    {
      await harness.SetStatusAsync(message, status);
    }

    await page.GotoAsync("/demandes");

    var row = page.GetByRole(AriaRole.Row).Filter(new() { HasText = email });
    await Expect(row).ToHaveCountAsync(1);

    return new ARequest(page, row, email, message);
  }

  /// <summary>Une demande enregistrée, telle que le tableau la montre.</summary>
  private sealed record ARequest(IPage Page, ILocator Row, string Email, string Message);
}
