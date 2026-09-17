using System.Globalization;
using System.Text.RegularExpressions;
using MicroserviceRgpd.Core.Requests;

namespace MicroserviceRgpd.BrowserTests.Requests;

/// <summary>
/// <b>L'<c>Operator</c> enregistre sa correction</b>, dans un vrai navigateur : la saisie est jugée
/// avec les règles et les mots de la création, la ligne se met à jour <b>sans rechargement</b> — à sa
/// place selon le tri en cours, cachée si la recherche ne la retient plus, avec sa nouvelle date
/// limite et le signalement de celle-ci —, et le toast dit « Demande modifiée ».
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Une correction qui ne corrige rien ne part pas</b> : la modale se ferme sans requête ni
/// toast, et des espaces ajoutés en fin de champ comptent pour rien — le domaine les rognerait.
/// </para>
/// <para>
/// <b>Les échecs de l'enregistrement se gardent ici aussi</b> : le bandeau d'un échec technique et
/// d'une demande close, la ligne d'une demande supprimée ailleurs qui s'en va, et l'inertie du bouton
/// comme de la modale pendant l'envoi. Dans tous les cas, la saisie reste là où elle peut resservir.
/// </para>
/// <para>
/// L'ouverture de la modale, son abandon et le retour du focus se gardent dans
/// <see cref="ModificationDialog"/> ; ce que le serveur enregistre, dans <c>RequestModifying</c>.
/// </para>
/// <para>
/// Les demandes se créent <b>par le use case</b>, et chacune se retrouve par son email unique, la
/// base étant partagée par toute la collection.
/// </para>
/// </remarks>
[Collection(BrowserModificationCollection.Name)]
public class RequestModification(BrowserHarness harness)
{
  /// <summary>Le titre de la modale en mode modification, qui la nomme.</summary>
  private const string DialogTitle = "Modifier la demande";

  /// <summary>Le libellé du bouton primaire en mode modification, recopié à dessein.</summary>
  private const string SubmitLabel = "Modifier";

  /// <summary>Ce que le toast dit d'une correction enregistrée, recopié à dessein.</summary>
  private const string Modified = "Demande modifiée";

  /// <summary>Ce que le toast dit d'une demande qui n'existe plus, recopié à dessein.</summary>
  private const string Vanished = "La demande a été supprimée entre-temps.";

  /// <summary>Ce que le bandeau de la modale dit d'un échec technique, recopié à dessein.</summary>
  private const string Failure = "La demande n'a pas pu être enregistrée. Votre saisie est conservée : vous pouvez réessayer.";

  /// <summary>
  /// Ce que le bandeau dit d'une demande close pendant la correction, recopié à dessein. ⚠️ Il
  /// n'invite pas à réessayer : aucun second essai ne rouvrira une demande close.
  /// </summary>
  private const string ClosedFailure =
    "Cette demande a été close pendant votre correction : elle n'est plus modifiable, et votre correction n'a pas été enregistrée.";

  /// <summary>Le nom de la demande enregistrée, celui que la recherche des scénarios parcourt.</summary>
  private const string RecordedLastName = "Martin";

  private const string RecordedFirstName = "Jeanne";

  private const string Oldest = "Date de réception la plus ancienne";

  private const string NoMatch = "Aucune demande ne correspond à votre recherche";

  private const string IdentificationMissing = "Renseignez un email, ou un nom et un prénom.";

  private const string MessageMissing = "Le message est obligatoire.";

  private const string RightMissing = "Sélectionnez un droit RGPD.";

  /// <summary>L'enregistrement d'une correction, celui que « Modifier » déclenche.</summary>
  private static readonly Regex ModifyHandler = new(@"/demandes\?handler=Modify$");

  /// <summary>
  /// <b>Une saisie fautive affiche toutes ses erreurs d'un coup</b>, chacune sous son champ, avec les
  /// mots de la création, et le focus va au premier champ en erreur. Rien n'est envoyé, et la modale
  /// reste ouverte.
  /// </summary>
  [Fact]
  public async Task ShowsEveryRefusalUnderItsFieldWithTheWordsOfTheCreation()
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var request = await ARecordedRequestAsync(page);
    var sent = await CountingTheSendsAsync(page);
    await OpenTheModificationAsync(request);

    await Field(page, "Nom").FillAsync(string.Empty);
    await Field(page, "Prénom").FillAsync(string.Empty);
    await Field(page, "Email").FillAsync(string.Empty);
    await Field(page, "Message").FillAsync("   ");
    await Field(page, "Droits RGPD").SelectOptionAsync(string.Empty);
    await ModifyAsync(page);

    await ExpectTheRefusalAsync(page, "Nom", IdentificationMissing);
    await ExpectTheRefusalAsync(page, "Prénom", IdentificationMissing);
    await ExpectTheRefusalAsync(page, "Email", IdentificationMissing);
    await ExpectTheRefusalAsync(page, "Message", MessageMissing);
    await ExpectTheRefusalAsync(page, "Droits RGPD", RightMissing);

    await Expect(Field(page, "Nom")).ToBeFocusedAsync();
    await Expect(Dialog(page)).ToBeVisibleAsync();
    await ExpectNothingSaidAsync(page);
    sent().ShouldBe(0, "Une saisie fautive est partie au serveur.");
  }

  /// <summary>
  /// <b>Une correction enregistrée met la ligne à jour sans rechargement</b>, et le toast
  /// « Demande modifiée » le dit. La demande est modifiée en base.
  /// </summary>
  [Fact]
  public async Task UpdatesTheRowWithoutReloadingAndSaysSo()
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var request = await ARecordedRequestAsync(page);
    await OpenTheModificationAsync(request);

    // Un marqueur posé sur la fenêtre : un rechargement l'effacerait.
    await page.EvaluateAsync("() => { window.untouched = true; }");

    var corrected = $"Je souhaite effacer mes données. {Guid.NewGuid()}";
    await Field(page, "Nom").FillAsync("Martinez");
    await Field(page, "Message").FillAsync(corrected);
    await ModifyAsync(page);

    await Expect(Dialog(page)).ToBeHiddenAsync();
    await Expect(Toast(page)).ToBeVisibleAsync();
    await Expect(CellOf(RowOf(page, request.Email), 1)).ToHaveTextAsync("Martinez");
    (await page.EvaluateAsync<bool>("() => window.untouched === true")).ShouldBeTrue("La modification a rechargé la page.");
    (await harness.CountOfRequestsAsync(corrected)).ShouldBe(1);
    (await harness.CountOfRequestsAsync(request.Message)).ShouldBe(0);
  }

  /// <summary>
  /// <b>Corriger la date de réception change la date limite affichée et son signalement</b> : la
  /// ligne rendue par le serveur porte les deux, et le module ne les recalcule pas (ADR-0021).
  /// </summary>
  /// <remarks>
  /// <para>
  /// Le service tourne sur l'heure réelle : les dates se posent relativement à aujourd'hui à Paris.
  /// Une demande reçue il y a deux mois est en retard ; reçue il y a un mois et trois jours, sa date
  /// limite tombe dans les jours qui viennent, et l'échéance est proche.
  /// </para>
  /// <para>
  /// ⚠️ <b>La date limite attendue ne se recalcule pas ici</b> : la règle qui la déduit de la date de
  /// réception n'est écrite qu'au domaine, et la redire en ferait une seconde écriture. Le test lit
  /// donc celle qui était affichée, et exige qu'elle ait changé — avec son signalement.
  /// </para>
  /// </remarks>
  [Fact]
  public async Task ShowsTheNewDeadlineAndItsSignal()
  {
    var today = ParisCalendar.Today(TimeProvider.System);
    var overdue = today.AddMonths(-2);
    var dueSoon = today.AddMonths(-1).AddDays(3);

    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var request = await ARecordedRequestAsync(page, receivedOn: Iso(overdue));
    var deadline = DeadlineOf(RowOf(page, request.Email));
    await Expect(deadline).ToContainTextAsync("En retard");
    var wasShowing = await deadline.TextContentAsync();

    await OpenTheModificationAsync(request);
    await Field(page, "Date de réception").FillAsync(Iso(dueSoon));
    await ModifyAsync(page);

    await Expect(Toast(page)).ToBeVisibleAsync();
    await Expect(DeadlineOf(RowOf(page, request.Email))).ToContainTextAsync("Échéance proche");
    (await DeadlineOf(RowOf(page, request.Email)).TextContentAsync())
      .ShouldNotBe(wasShowing, "La date limite affichée n'a pas suivi la date de réception corrigée.");
  }

  /// <summary>
  /// <b>La ligne se replace selon le tri en cours</b> quand la correction change sa position : elle
  /// passe par l'insertion de la création, et le tri sélectionné décide — « la plus récente » comme
  /// « la plus ancienne ».
  /// </summary>
  /// <remarks>
  /// Les dates se posent relativement à aujourd'hui à Paris : une date de réception future serait
  /// refusée, et le service tourne sur l'heure réelle.
  /// </remarks>
  [Theory]
  [InlineData("newest")]
  [InlineData("oldest")]
  public async Task PlacesTheCorrectedRowInTheSelectedOrder(string order)
  {
    var today = ParisCalendar.Today(TimeProvider.System);

    await using var context = await harness.NewContextAsync();
    await harness.DeleteAllRequestsAsync();
    var page = await context.NewPageAsync();
    var middle = await ARecordedRequestAsync(page, receivedOn: Iso(today.AddDays(-60)));
    var latest = await ARecordedRequestAsync(page, receivedOn: Iso(today.AddDays(-30)));
    var earliest = await ARecordedRequestAsync(page, receivedOn: Iso(today.AddDays(-90)));

    if (order is "oldest")
    {
      await SortMenu(page).SelectOptionAsync(new SelectOptionValue { Label = Oldest });
      await ExpectOrderAsync(page, earliest.Email, middle.Email, latest.Email);
    }
    else
    {
      await ExpectOrderAsync(page, latest.Email, middle.Email, earliest.Email);
    }

    // La plus ancienne devient la plus récente : elle change de bout, dans les deux ordres.
    await OpenTheModificationAsync(earliest);
    await Field(page, "Date de réception").FillAsync(Iso(today));
    await ModifyAsync(page);

    await Expect(Toast(page)).ToBeVisibleAsync();
    await (order is "oldest"
      ? ExpectOrderAsync(page, middle.Email, latest.Email, earliest.Email)
      : ExpectOrderAsync(page, earliest.Email, latest.Email, middle.Email));
  }

  /// <summary>
  /// <b>La ligne disparaît du tableau si la correction la sort de la recherche en cours</b> — et non
  /// du tableau : la recherche vidée, elle reparaît, corrigée et à sa place.
  /// </summary>
  [Fact]
  public async Task HidesTheRowWhenTheCorrectionTakesItOutOfTheSearch()
  {
    await using var context = await harness.NewContextAsync();
    await harness.DeleteAllRequestsAsync();
    var page = await context.NewPageAsync();
    var request = await ARecordedRequestAsync(page);

    await Search(page).FillAsync(RecordedLastName);
    await Expect(RowOf(page, request.Email)).ToBeVisibleAsync();

    await OpenTheModificationAsync(request);
    await Field(page, "Nom").FillAsync("Dupont");
    await ModifyAsync(page);

    await Expect(Toast(page)).ToBeVisibleAsync();
    await Expect(RowOf(page, request.Email)).ToBeHiddenAsync();
    await Expect(NoMatchMessage(page)).ToBeVisibleAsync();

    await Search(page).FillAsync(string.Empty);

    await Expect(NoMatchMessage(page)).ToBeHiddenAsync();
    await Expect(CellOf(RowOf(page, request.Email), 1)).ToHaveTextAsync("Dupont");
  }

  /// <summary>
  /// <b>Enregistrer sans avoir rien changé ferme la modale sans requête ni toast</b> : il n'y a rien
  /// à dire d'une correction qui ne corrige rien. ⚠️ <b>Des espaces ajoutés en fin de champ comptent
  /// comme « rien changé »</b> — le domaine les rognerait, et le toast mentirait.
  /// </summary>
  [Theory]
  [InlineData("rien touché")]
  [InlineData("des espaces ajoutés en fin de champ")]
  public async Task ClosesWithoutSendingWhenNothingChanged(string entry)
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var request = await ARecordedRequestAsync(page);
    var sent = await CountingTheSendsAsync(page);
    await OpenTheModificationAsync(request);

    if (entry is not "rien touché")
    {
      await Field(page, "Nom").FillAsync($"{RecordedLastName}  ");
      await Field(page, "Prénom").FillAsync($"  {RecordedFirstName}");
    }

    await ModifyAsync(page);

    await Expect(Dialog(page)).ToBeHiddenAsync();
    await Expect(Confirmation(page)).ToBeHiddenAsync();
    await ExpectNothingSaidAsync(page);
    sent().ShouldBe(0, "Une correction qui ne corrige rien est partie au serveur.");
  }

  /// <summary>
  /// <b>Une correction réelle, elle, part</b> — même quand des espaces s'y ajoutent : c'est le
  /// pendant du test précédent, sans quoi « rien n'est jamais envoyé » le passerait aussi.
  /// </summary>
  [Fact]
  public async Task SendsTheCorrectionWhenAValueReallyChanged()
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var request = await ARecordedRequestAsync(page);
    var sent = await CountingTheSendsAsync(page);
    await OpenTheModificationAsync(request);

    await Field(page, "Nom").FillAsync($"{RecordedLastName}e ");
    await ModifyAsync(page);

    await Expect(Toast(page)).ToBeVisibleAsync();
    await Expect(CellOf(RowOf(page, request.Email), 1)).ToHaveTextAsync($"{RecordedLastName}e");
    sent().ShouldBe(1);
  }

  /// <summary>
  /// ⚠️ <b>Le focus revient sur le crayon de la ligne corrigée</b>, la nouvelle : l'ancienne est
  /// partie avec celui qui avait ouvert la modale. Sans quoi toute correction enregistrée renverrait
  /// au cadre du tableau — le recours d'une ligne disparue —, et la navigation au clavier perdrait sa
  /// place à chaque fois.
  /// </summary>
  [Fact]
  public async Task GivesTheFocusBackToThePencilOfTheCorrectedRow()
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var request = await ARecordedRequestAsync(page);
    await OpenTheModificationAsync(request);

    await Field(page, "Nom").FillAsync("Martinez");
    await ModifyAsync(page);

    await Expect(Toast(page)).ToBeVisibleAsync();
    await Expect(PencilOf(RowOf(page, request.Email))).ToBeFocusedAsync();
  }

  /// <summary>Les échecs de l'enregistrement qui ne sont pas un refus de la saisie.</summary>
  public static TheoryData<string> Failures { get; } = ["une erreur serveur", "une coupure réseau", "un jeton anti-rejeu périmé"];

  /// <summary>
  /// <b>Un échec technique pose le bandeau et conserve la saisie</b> : la modale reste ouverte, telle
  /// que l'<c>Operator</c> l'a remplie, et rien n'est enregistré. Le bandeau distingue « le serveur
  /// n'a pas voulu » de « j'ai mal saisi », qui va sous ses champs.
  /// </summary>
  /// <remarks>
  /// Le jeton périmé n'est pas simulé : le cookie qui le porte est effacé, et c'est le vrai service
  /// qui refuse.
  /// </remarks>
  [Theory]
  [MemberData(nameof(Failures))]
  public async Task ShowsTheBannerAndKeepsTheEntryOnATechnicalFailure(string failure)
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var request = await ARecordedRequestAsync(page);
    await OpenTheModificationAsync(request);

    switch (failure)
    {
      case "une erreur serveur":
        await page.RouteAsync(ModifyHandler, route => route.FulfillAsync(new() { Status = 500 }));
        break;
      case "une coupure réseau":
        await page.RouteAsync(ModifyHandler, route => route.AbortAsync("internetdisconnected"));
        break;
      default:
        await context.ClearCookiesAsync();
        break;
    }

    var corrected = $"Je souhaite effacer mes données. {Guid.NewGuid()}";
    await Field(page, "Nom").FillAsync("Martinez");
    await Field(page, "Message").FillAsync(corrected);
    await ModifyAsync(page);

    await Expect(Banner(page)).ToBeVisibleAsync();
    await Expect(Dialog(page)).ToBeVisibleAsync();
    await ExpectNothingSaidAsync(page);
    await Expect(ModifyButton(page)).ToBeEnabledAsync();

    await Expect(Field(page, "Nom")).ToHaveValueAsync("Martinez");
    await Expect(Field(page, "Message")).ToHaveValueAsync(corrected);
    await Expect(Field(page, "Email")).ToHaveValueAsync(request.Email);

    (await harness.CountOfRequestsAsync(corrected)).ShouldBe(0);
    (await harness.CountOfRequestsAsync(request.Message)).ShouldBe(1);
  }

  /// <summary>
  /// <b>Après un échec, l'<c>Operator</c> réessaie sans rien ressaisir</b> : la même saisie repart, le
  /// bandeau s'en va, et la correction est enregistrée.
  /// </summary>
  [Fact]
  public async Task SavesOnRetryAfterAFailure()
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var request = await ARecordedRequestAsync(page);
    await OpenTheModificationAsync(request);

    await page.RouteAsync(ModifyHandler, route => route.AbortAsync("internetdisconnected"));

    var corrected = $"Je souhaite effacer mes données. {Guid.NewGuid()}";
    await Field(page, "Message").FillAsync(corrected);
    await ModifyAsync(page);
    await Expect(Banner(page)).ToBeVisibleAsync();

    await page.UnrouteAsync(ModifyHandler);
    await ModifyAsync(page);

    await Expect(Banner(page)).ToBeHiddenAsync();
    await Expect(Dialog(page)).ToBeHiddenAsync();
    await Expect(Toast(page)).ToBeVisibleAsync();
    (await harness.CountOfRequestsAsync(corrected)).ShouldBe(1);
  }

  /// <summary>
  /// <b>Une demande close pendant la correction pose le bandeau, et rien d'autre</b> : la ligne n'est
  /// pas redessinée. ⚠️ Le 409 dit « close », <b>pas lequel des deux statuts</b> — la cellule Statut
  /// afficherait « En cours » à côté d'un crayon éteint. L'incohérence se résout au prochain
  /// chargement, et la cellule garde donc ce qu'elle affichait.
  /// <para>
  /// ⚠️ <b>Le bandeau porte ses propres mots</b>, et non ceux de l'échec technique : l'<c>Operator</c>
  /// apprend que la demande a été close, au lieu d'être invité à un second essai qu'aucune correction
  /// ne fera passer.
  /// </para>
  /// </summary>
  /// <remarks>
  /// Le statut se pose <b>dans le dos de l'écran</b>, la modale déjà ouverte : c'est le vrai service
  /// qui refuse, et non une réponse simulée.
  /// </remarks>
  [Fact]
  public async Task ShowsTheBannerAndLeavesTheRowAloneWhenTheRequestWasClosed()
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var request = await ARecordedRequestAsync(page);
    await OpenTheModificationAsync(request);

    await harness.SetStatusAsync(request.Message, RequestStatus.Completed);

    var corrected = $"Je souhaite effacer mes données. {Guid.NewGuid()}";
    await Field(page, "Message").FillAsync(corrected);
    await ModifyAsync(page);

    await Expect(ClosedBanner(page)).ToBeVisibleAsync();
    await Expect(Banner(page)).ToBeHiddenAsync();
    await Expect(Dialog(page)).ToBeVisibleAsync();
    await ExpectNothingSaidAsync(page);
    await Expect(Field(page, "Message")).ToHaveValueAsync(corrected);

    await Expect(RowOf(page, request.Email)).ToHaveCountAsync(1);
    await Expect(StatusOf(RowOf(page, request.Email))).ToHaveTextAsync("En cours");
    (await harness.CountOfRequestsAsync(corrected)).ShouldBe(0);
  }

  /// <summary>
  /// <b>Une demande supprimée depuis un autre onglet l'annonce, et sa ligne part du tableau</b> : il
  /// n'y a rien à réessayer, la modale se ferme, et le toast dit ce qui lui est arrivé. Le focus va au
  /// cadre du tableau : le crayon qui avait ouvert la modale est parti avec sa ligne.
  /// </summary>
  [Fact]
  public async Task RemovesTheRowWhenTheRequestWasDeletedElsewhere()
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var request = await ARecordedRequestAsync(page);
    await OpenTheModificationAsync(request);

    await harness.DeleteRequestAsync(request.Message);

    await Field(page, "Message").FillAsync($"Je souhaite effacer mes données. {Guid.NewGuid()}");
    await ModifyAsync(page);

    await Expect(Dialog(page)).ToBeHiddenAsync();
    await Expect(VanishedToast(page)).ToBeVisibleAsync();
    await Expect(RowOf(page, request.Email)).ToHaveCountAsync(0);
    await Expect(Frame(page)).ToBeFocusedAsync();
  }

  /// <summary>
  /// ⚠️ <b>Pendant l'envoi, « Modifier » est inerte et la modale ne se ferme pas</b> — ni par
  /// « Annuler », ni par la croix, ni par Échap, ni par un clic sur le fond : la réponse qui arrive
  /// trouve la saisie qu'elle concerne, et une correction ne s'enregistre jamais deux fois.
  /// </summary>
  [Fact]
  public async Task StaysOpenAndInertWhileSending()
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var request = await ARecordedRequestAsync(page);
    await OpenTheModificationAsync(request);

    var sent = 0;
    var release = new TaskCompletionSource();
    await page.RouteAsync(ModifyHandler, async route =>
    {
      Interlocked.Increment(ref sent);
      await release.Task;
      await route.ContinueAsync();
    });

    var corrected = $"Je souhaite effacer mes données. {Guid.NewGuid()}";
    await Field(page, "Message").FillAsync(corrected);
    await ModifyButton(page).DblClickAsync();

    await Expect(ModifyButton(page)).ToBeDisabledAsync();

    await page.Keyboard.PressAsync("Escape");
    await Dialog(page).GetByRole(AriaRole.Button, new() { Name = "Annuler", Exact = true }).ClickAsync();
    await Dialog(page).GetByRole(AriaRole.Button, new() { Name = "Fermer", Exact = true }).ClickAsync();
    await page.Mouse.ClickAsync(5, 5);

    await Expect(Dialog(page)).ToBeVisibleAsync();
    await Expect(Confirmation(page)).ToBeHiddenAsync();

    release.SetResult();

    await Expect(Dialog(page)).ToBeHiddenAsync();
    await Expect(Toast(page)).ToBeVisibleAsync();
    sent.ShouldBe(1);
    (await harness.CountOfRequestsAsync(corrected)).ShouldBe(1);
  }

  /// <summary>
  /// ⚠️ <b>Le toast ne dit rien du tout</b> — et pas seulement : rien de cette correction-là. Un toast
  /// vide est le seul qui prouve qu'aucun mot n'a été dit.
  /// </summary>
  /// <remarks>
  /// Il se désigne par son rôle <b>en sélecteur</b>, et non par <c>GetByRole</c> : un toast vide ne
  /// paraît pas dans l'arbre d'accessibilité, et c'est justement lui qu'il s'agit de lire.
  /// </remarks>
  private static Task ExpectNothingSaidAsync(IPage page)
  {
    return Expect(page.Locator("[role='status']")).ToHaveTextAsync(string.Empty);
  }

  /// <summary>Compte les envois au handler de la modification, sans les retenir.</summary>
  private static async Task<Func<int>> CountingTheSendsAsync(IPage page)
  {
    var sent = 0;

    await page.RouteAsync(ModifyHandler, route =>
    {
      Interlocked.Increment(ref sent);

      return route.ContinueAsync();
    });

    return () => sent;
  }

  private static async Task ExpectTheRefusalAsync(IPage page, string label, string refusal)
  {
    await Expect(Field(page, label)).ToHaveAccessibleDescriptionAsync(refusal);
    await Expect(Field(page, label)).ToHaveAttributeAsync("aria-invalid", "true");
  }

  /// <summary>Ouvre la modale sur cette demande, et attend qu'elle soit là.</summary>
  private static async Task OpenTheModificationAsync(ARequest request)
  {
    await PencilOf(request.Row).ClickAsync();
    await Expect(Dialog(request.Page)).ToBeVisibleAsync();
  }

  /// <summary>Le crayon d'une ligne, qui porte le même nom que la modale qu'il ouvre.</summary>
  private static ILocator PencilOf(ILocator row)
  {
    return row.GetByRole(AriaRole.Button, new() { Name = DialogTitle, Exact = true });
  }

  private static Task ModifyAsync(IPage page)
  {
    return ModifyButton(page).ClickAsync();
  }

  /// <summary>Le bouton primaire de la modale en mode modification.</summary>
  private static ILocator ModifyButton(IPage page)
  {
    return Dialog(page).GetByRole(AriaRole.Button, new() { Name = SubmitLabel, Exact = true });
  }

  /// <summary>Le bandeau d'échec de la modale, qui n'est là que si l'enregistrement a échoué.</summary>
  private static ILocator Banner(IPage page)
  {
    return Dialog(page).GetByRole(AriaRole.Alert).And(page.GetByText(Failure, new() { Exact = true }));
  }

  /// <summary>Le même bandeau, portant cette fois les mots d'une demande close pendant la correction.</summary>
  private static ILocator ClosedBanner(IPage page)
  {
    return Dialog(page).GetByRole(AriaRole.Alert).And(page.GetByText(ClosedFailure, new() { Exact = true }));
  }

  private static string Iso(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

  private static ILocator Dialog(IPage page)
  {
    return page.GetByRole(AriaRole.Dialog, new() { Name = DialogTitle, Exact = true, IncludeHidden = true });
  }

  private static ILocator Confirmation(IPage page)
  {
    return page.GetByRole(AriaRole.Alertdialog, new() { Name = "Abandonner la saisie ?", Exact = true, IncludeHidden = true });
  }

  private static ILocator Field(IPage page, string label)
  {
    return Dialog(page).GetByLabel(label, new() { Exact = true });
  }

  private static ILocator RowOf(IPage page, string email)
  {
    return page.GetByRole(AriaRole.Row).Filter(new() { HasText = email });
  }

  private static ILocator CellOf(ILocator row, int index)
  {
    return row.GetByRole(AriaRole.Cell).Nth(index);
  }

  /// <summary>La cellule de la date limite de réponse, cinquième de la ligne.</summary>
  private static ILocator DeadlineOf(ILocator row)
  {
    return CellOf(row, 4);
  }

  /// <summary>La cellule du statut, dixième de la ligne.</summary>
  private static ILocator StatusOf(ILocator row)
  {
    return CellOf(row, 9);
  }

  /// <summary>
  /// Attend que les lignes affichées du tableau se lisent dans cet ordre, chacune reconnue par son
  /// email. Une ligne cachée ne s'y trouve pas.
  /// </summary>
  private static Task ExpectOrderAsync(IPage page, params string[] emails)
  {
    return Expect(Rows(page)).ToHaveTextAsync(
      emails.Select(email => new Regex(Regex.Escape(email))).ToArray());
  }

  /// <summary>
  /// Les lignes affichées du corps du tableau, dans l'ordre du tableau : le dernier groupe de lignes
  /// est le corps, l'en-tête étant le premier.
  /// </summary>
  private static ILocator Rows(IPage page)
  {
    return Frame(page).GetByRole(AriaRole.Rowgroup).Last.GetByRole(AriaRole.Row);
  }

  private static ILocator Frame(IPage page)
  {
    return page.GetByRole(AriaRole.Region, new() { Name = "Liste des demandes", Exact = true });
  }

  /// <summary>Le message, lu dans le cadre du tableau : c'est là, à la place des lignes, qu'il se lit.</summary>
  private static ILocator NoMatchMessage(IPage page)
  {
    return Frame(page).GetByText(NoMatch, new() { Exact = true });
  }

  private static ILocator Search(IPage page)
  {
    return page.GetByRole(AriaRole.Searchbox, new() { Name = "Rechercher une demande", Exact = true });
  }

  private static ILocator SortMenu(IPage page)
  {
    return page.GetByRole(AriaRole.Combobox, new() { Name = "Trier par", Exact = true });
  }

  private static ILocator Toast(IPage page)
  {
    return page.GetByRole(AriaRole.Status).And(page.GetByText(Modified, new() { Exact = true }));
  }

  private static ILocator VanishedToast(IPage page)
  {
    return page.GetByRole(AriaRole.Status).And(page.GetByText(Vanished, new() { Exact = true }));
  }

  /// <summary>
  /// Enregistre une demande En cours, ouvre le tableau et rend de quoi la retrouver : sa ligne, son
  /// email unique et son message.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Le tableau est relu à chaque appel</b> : la seconde demande d'un scénario doit figurer sur
  /// la page où la première se lit déjà.
  /// </remarks>
  private async Task<ARequest> ARecordedRequestAsync(IPage page, string receivedOn = "2026-01-15")
  {
    var email = $"{Guid.NewGuid():N}@example.org";
    var message = await harness.RecordRequestAsync(
      lastName: RecordedLastName,
      firstName: RecordedFirstName,
      email: email,
      receivedOn: receivedOn);

    await page.GotoAsync("/demandes");

    var row = page.GetByRole(AriaRole.Row).Filter(new() { HasText = email });
    await Expect(row).ToHaveCountAsync(1);

    return new ARequest(page, row, email, message);
  }

  /// <summary>Une demande enregistrée, telle que le tableau la montre.</summary>
  private sealed record ARequest(IPage Page, ILocator Row, string Email, string Message);
}
