using System.Globalization;
using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.Web.Pages.Requests;

namespace MicroserviceRgpd.BrowserTests.Requests;

/// <summary>
/// <b>L'œil d'une ligne ouvre la fiche de sa demande</b>, dans un vrai navigateur : la lecture à
/// l'écran de ce que le service en tient, ancrée au bord droit, en lecture seule — et <b>quatre
/// gestes la referment</b> : « Fermer », la croix, Échap, un clic sur le fond.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Aucune fermeture ne demande de confirmation</b> : contrairement à la modale de saisie, rien
/// n'est en jeu dans une lecture. Et après chacune, le focus revient sur l'œil de la ligne d'où l'on
/// vient : la navigation au clavier reprend là où elle s'était arrêtée.
/// </para>
/// <para>
/// <b>Et elle porte ce que la ligne dit de la demande</b> : les douze informations, dans ses
/// blocs — le message et l'origine compris, qu'aucune colonne ne montre. Les libellés que le serveur
/// rend, et eux seuls, se gardent dans <c>RequestsBoardScreen</c> ; ici, ce que l'œil d'une ligne y
/// verse.
/// </para>
/// <para>
/// ⚠️ <b>Chaque valeur se lit avec son libellé</b> : les scénarios apparient les intitulés et les
/// valeurs de la fiche, dans l'ordre — c'est l'appariement même qu'un lecteur d'écran annonce. Le
/// droit, le décompte, le statut et le message sortent des listes de définitions.
/// </para>
/// <para>
/// La fiche ne se cherche pas par son nom accessible — c'est le nom de la personne, qui change d'une
/// demande à l'autre —, mais par son bloc « La personne ». Chaque demande se retrouve par son
/// email unique, la base étant partagée par toute la collection.
/// </para>
/// </remarks>
[Collection(BrowserSheetCollection.Name)]
public class RequestSheet(BrowserHarness harness)
{
  /// <summary>Le nom accessible de l'œil, recopié à dessein.</summary>
  private const string Eye = "Voir la fiche de la demande";

  /// <summary>Le premier bloc de la fiche, à quoi elle se reconnaît, recopié à dessein.</summary>
  private const string FirstBlock = "La personne";

  /// <summary>Le titre du bloc de la prolongation, recopié à dessein.</summary>
  private const string ExtensionBlockTitle = "Prolongation";

  /// <summary>Le titre de la confirmation d'abandon : celle qui ne doit jamais se montrer ici.</summary>
  private const string ConfirmationTitle = "Abandonner la saisie ?";

  /// <summary>L'orange profond du signalement, recopié à dessein — comme dans <c>DeadlineSignals</c>.</summary>
  private const string Orange = "rgb(121, 52, 0)";

  /// <summary>Le rouge du signalement, recopié à dessein.</summary>
  private const string Red = "rgb(179, 38, 30)";

  /// <summary>Les quatre modes de fermeture de la fiche.</summary>
  public static TheoryData<string> ClosingModes { get; } = ["Fermer", "la croix", "Échap", "le fond"];

  /// <summary>
  /// <b>L'œil ouvre la fiche</b>, fermée jusque-là, avec ses blocs titrés — le délai, lui, se
  /// nomme pour le seul lecteur d'écran : l'encadré du décompte le dit déjà à qui le voit.
  /// </summary>
  [Fact]
  public async Task OpensTheSheetOnTheEyeOfTheRow()
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var row = await ARecordedRowAsync(page);

    await Expect(Sheet(page)).ToBeHiddenAsync();

    await EyeOf(row).ClickAsync();

    await Expect(Sheet(page)).ToBeVisibleAsync();

    foreach (var block in new[] { "Message", FirstBlock, "Historique" })
    {
      await Expect(Sheet(page).GetByRole(AriaRole.Heading, new() { Name = block, Exact = true })).ToBeVisibleAsync();
    }
  }

  /// <summary>
  /// <b>La fiche est ancrée au bord droit de la fenêtre, sur toute sa hauteur</b> : elle sort du
  /// bord, elle ne se pose pas au centre comme la modale de saisie.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Le bord droit est celui de la page, gouttière de défilement exclue</b> : la feuille de
  /// style la réserve en permanence, et aucune surface de l'écran ne va au-delà.
  /// </remarks>
  [Fact]
  public async Task AnchorsTheSheetToTheRightEdgeAtFullHeight()
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    await page.SetViewportSizeAsync(1280, 720);
    await OpenTheSheetAsync(await ARecordedRowAsync(page));

    var box = await Sheet(page).BoundingBoxAsync() ?? throw new InvalidOperationException("La fiche n'est pas affichée.");
    var width = await page.EvaluateAsync<float>("document.body.clientWidth");
    var height = await page.EvaluateAsync<float>("document.documentElement.clientHeight");
    box.Y.ShouldBe(0, 1, "La fiche ne part pas du haut de la fenêtre.");
    box.Height.ShouldBe(height, 1, "La fiche n'occupe pas toute la hauteur de la fenêtre.");
    (box.X + box.Width).ShouldBe(width, 1, "La fiche n'est pas ancrée au bord droit de la fenêtre.");
    box.X.ShouldBeGreaterThan(width / 2, "La fiche déborde sur la moitié gauche de l'écran.");
  }

  /// <summary>
  /// ⚠️ <b>La fiche s'ouvre instantanément</b> : aucun appel ne part au service. Tout ce qu'elle
  /// montre, la page l'a déjà.
  /// </summary>
  [Fact]
  public async Task OpensWithoutCallingTheService()
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var row = await ARecordedRowAsync(page);
    await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

    var sent = new List<string>();
    page.Request += (_, request) => sent.Add($"{request.Method} {request.Url}");

    await EyeOf(row).ClickAsync();

    await Expect(Sheet(page)).ToBeVisibleAsync();
    sent.ShouldBeEmpty("L'ouverture de la fiche a appelé le service.");
  }

  /// <summary>
  /// <b>Chacun des quatre gestes referme la fiche, sans confirmation</b> : rien n'est en jeu dans une
  /// lecture, et rien ne barre le passage.
  /// </summary>
  [Theory]
  [MemberData(nameof(ClosingModes))]
  public async Task ClosesWithoutAskingAnything(string mode)
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    await OpenTheSheetAsync(await ARecordedRowAsync(page));

    await CloseByAsync(page, mode);

    await Expect(Sheet(page)).ToBeHiddenAsync();
    await Expect(Confirmation(page)).ToBeHiddenAsync();
  }

  /// <summary>
  /// <b>Après chaque fermeture, le focus est sur l'œil de la ligne d'où l'on vient</b> : sans souris,
  /// la navigation reprend là où elle s'était arrêtée.
  /// </summary>
  [Theory]
  [MemberData(nameof(ClosingModes))]
  public async Task GivesTheFocusBackToTheEyeOfTheRow(string mode)
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var row = await ARecordedRowAsync(page);
    await OpenTheSheetAsync(row);

    await CloseByAsync(page, mode);

    await Expect(Sheet(page)).ToBeHiddenAsync();
    await Expect(EyeOf(row)).ToBeFocusedAsync();
  }

  /// <summary>
  /// ⚠️ <b>Cliquer une cellule de la ligne, hors des trois boutons, n'ouvre rien</b> : un email se
  /// sélectionne et se copie sans qu'une fiche s'ouvre.
  /// </summary>
  [Fact]
  public async Task OpensNothingOnAClickInACellOfTheRow()
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var row = await ARecordedRowAsync(page);

    await row.GetByRole(AriaRole.Cell).First.ClickAsync();

    await Expect(Sheet(page)).ToBeHiddenAsync();
  }

  /// <summary>
  /// <b>La fiche porte les douze informations d'une demande, dans l'ordre de ses blocs</b> — le
  /// délai, le message, la personne, l'historique —, chacune sous son libellé, et le droit invoqué
  /// au-dessus du titre, sous son libellé caché. Aucune valeur écrite par le script.
  /// </summary>
  /// <remarks>
  /// La demande est enregistrée avec <b>aucune</b> valeur par défaut de la modale : une origine, un
  /// droit, une identité vérifiée et un nom qui se distinguent tous du pré-remplissage. La date de
  /// création est l'instant de l'enregistrement, qu'aucun scénario ne connaît d'avance : elle se
  /// relit sur la cellule de la ligne, celle-là même dont la fiche la tient. La date limite, elle,
  /// tombe un mois après une réception fixée dans le passé : elle porte donc « En retard », et c'est
  /// bien la date limite qui est sous les yeux de l'<c>Operator</c> pendant qu'il lit le message.
  /// </remarks>
  [Fact]
  public async Task CarriesTheTwelveInformationsInTheOrderOfItsBlocks()
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var email = UniqueEmail();
    var message = await harness.RecordRequestAsync(
      lastName: "Dupont",
      firstName: "Jean",
      email: email,
      receivedOn: "2026-01-15",
      origin: Origin.Letter,
      identityVerified: true,
      right: "Erasure");

    var row = await OpenedRowOfAsync(page, email);
    await OpenTheSheetAsync(row);

    var createdAt = await row.Locator("td[data-field='createdAt']").TextContentAsync();
    (await FactsOfAsync(page)).ShouldBe(
    [
      "Date de réception : 15/01/2026",
      "Date limite de réponse : 15/02/2026 En retard",
      "Nom : Dupont",
      "Prénom : Jean",
      $"Email : {email}",
      "Identité vérifiée : Oui",
      $"Date de création : {createdAt}",
      "Créé par : Opérateur",
      "Origine : Courrier",
    ]);

    await Expect(Sheet(page).Locator(".sheet-right")).ToHaveTextAsync("Droit invoqué : Droit à l'effacement");

    await Expect(MessageOf(page)).ToHaveTextAsync(message);
    await Expect(StatusOf(page)).ToHaveTextAsync("En cours");
  }

  /// <summary>
  /// <b>Le titre de la fiche nomme la personne</b> — « Prénom Nom » —, et c'est lui qui la nomme
  /// pour qui ne la voit pas.
  /// </summary>
  [Fact]
  public async Task NamesThePersonInItsTitle()
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();

    await OpenTheSheetAsync(await ARecordedRowAsync(page));

    await Expect(TitleOf(page)).ToHaveTextAsync("Jeanne Martin");
    await Expect(Sheet(page)).ToHaveAccessibleNameAsync("Jeanne Martin");
  }

  /// <summary>
  /// <b>Quand le nom et le prénom manquent, le titre porte l'email seul</b> — et les deux cellules
  /// de la fiche portent « — » : leurs lignes ne disparaissent pas.
  /// </summary>
  [Fact]
  public async Task NamesThePersonByTheirEmailAloneWhenTheNameIsMissing()
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var email = UniqueEmail();
    await harness.RecordRequestAsync(email: email);

    await OpenTheSheetAsync(await OpenedRowOfAsync(page, email));

    await Expect(TitleOf(page)).ToHaveTextAsync(email);
    (await FactsOfAsync(page)).Skip(2).Take(3).ShouldBe(["Nom : —", "Prénom : —", $"Email : {email}"]);
  }

  /// <summary>
  /// ⚠️ <b>La fiche ne montre jamais l'identifiant technique de la demande</b> : il ne dit rien à
  /// l'<c>Operator</c>, et la ligne le porte pour la seule suppression.
  /// </summary>
  [Fact]
  public async Task NeverShowsTheTechnicalIdentifierOfTheRequest()
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var row = await ARecordedRowAsync(page);
    await OpenTheSheetAsync(row);

    var id = await row.GetAttributeAsync("data-request-id");
    id.ShouldNotBeNullOrWhiteSpace();
    (await Sheet(page).InnerHTMLAsync())
      .ShouldNotContain(id, Case.Insensitive, "La fiche montre l'identifiant technique de la demande.");
  }

  /// <summary>
  /// <b>Le message s'affiche tel qu'enregistré, ses retours à la ligne conservés</b> : c'est un
  /// texte que la personne a écrit, et la fiche le rend tel quel.
  /// </summary>
  [Fact]
  public async Task ShowsTheMessageAsRecordedWithItsLineBreaks()
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var email = UniqueEmail();
    var message = await harness.RecordRequestAsync(email: email);
    var overTwoLines = $"{message}\nEt une seconde ligne.";

    await harness.SetMessageAsync(message, overTwoLines);

    await OpenTheSheetAsync(await OpenedRowOfAsync(page, email));

    (await MessageOf(page).TextContentAsync()).ShouldBe(overTwoLines);
    await Expect(MessageOf(page)).ToHaveCSSAsync("white-space", "pre-wrap");
  }

  /// <summary>
  /// <b>Le statut s'affiche sous le même badge et le même mot que sur la ligne</b> : le nom canonique
  /// que la feuille de style colore vient de la cellule, cloné, et le script n'en écrit aucun mot.
  /// </summary>
  [Theory]
  [InlineData("InProgress", "En cours")]
  [InlineData("Completed", "Terminée")]
  [InlineData("Cancelled", "Annulée")]
  public async Task ShowsTheSameStatusBadgeAsTheRow(string name, string label)
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var email = UniqueEmail();
    var message = await harness.RecordRequestAsync(email: email);

    await harness.SetStatusAsync(message, RequestStatus.FromName(name));

    var row = await OpenedRowOfAsync(page, email);
    await OpenTheSheetAsync(row);

    await Expect(StatusOf(page)).ToHaveTextAsync(label);
    await Expect(StatusOf(page)).ToHaveAttributeAsync("data-status", name);
    await Expect(StatusOf(page)).ToHaveCSSAsync(
      "background-color",
      await BackgroundOfTheBadgeOfAsync(row));
  }

  /// <summary>
  /// <b>L'origine s'affiche sous son libellé français</b> — « Email » ou « Courrier » —, celui que le
  /// serveur a écrit sur la ligne : aucune colonne ne la montre, et le script ne dérive jamais un
  /// libellé d'un nom canonique.
  /// </summary>
  [Theory]
  [InlineData("Email", "Email")]
  [InlineData("Letter", "Courrier")]
  public async Task ShowsTheOriginUnderItsFrenchLabel(string name, string label)
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var email = UniqueEmail();
    await harness.RecordRequestAsync(email: email, origin: Origin.FromName(name));

    await OpenTheSheetAsync(await OpenedRowOfAsync(page, email));

    (await FactsOfAsync(page)).ShouldContain($"Origine : {label}");
  }

  /// <summary>
  /// <b>La date limite d'une demande En cours porte son signalement</b> — « En retard », « Échéance
  /// proche » —, sous les yeux de l'<c>Operator</c> pendant qu'il lit le message.
  /// </summary>
  /// <remarks>
  /// La date limite est posée à trois jours d'aujourd'hui à Paris, de part ou d'autre : assez loin
  /// des bornes pour qu'un minuit franchi pendant le test n'en change pas le signalement.
  /// </remarks>
  [Theory]
  [InlineData(3, "Échéance proche", Orange)]
  [InlineData(-3, "En retard", Red)]
  public async Task ShowsTheSignalOfTheDeadline(int daysFromToday, string mention, string colour)
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var email = UniqueEmail();
    var message = await harness.RecordRequestAsync(email: email);
    var deadline = ParisCalendar.Today(TimeProvider.System).AddDays(daysFromToday);

    await harness.SetResponseDeadlineAsync(message, deadline);

    await OpenTheSheetAsync(await OpenedRowOfAsync(page, email));

    (await FactsOfAsync(page))
      .ShouldContain($"Date limite de réponse : {deadline.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)} {mention}");
    await Expect(Sheet(page).GetByText(mention, new() { Exact = true })).ToHaveCSSAsync("color", colour);
    await Expect(CountdownLeadOf(page)).ToHaveCSSAsync("color", colour);
  }

  /// <summary>
  /// <b>Le décompte d'une demande En cours se lit en tête de la fiche</b> — les jours qui restent,
  /// et ce qu'ils comptent —, et <b>la barre dessine la part du délai écoulée</b> depuis la
  /// réception : ici, dix jours sur trente, le tiers.
  /// </summary>
  /// <remarks>
  /// La réception et la date limite sont posées autour d'aujourd'hui à Paris, à vingt jours de
  /// l'échéance : loin des bornes du signalement, qui colorerait le décompte.
  /// </remarks>
  [Fact]
  public async Task ShowsTheCountdownAndTheElapsedShareOfTheDeadline()
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var email = UniqueEmail();
    var today = ParisCalendar.Today(TimeProvider.System);
    var message = await harness.RecordRequestAsync(
      email: email,
      receivedOn: today.AddDays(-10).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

    await harness.SetResponseDeadlineAsync(message, today.AddDays(20));

    await OpenTheSheetAsync(await OpenedRowOfAsync(page, email));

    await Expect(CountdownLeadOf(page)).ToHaveTextAsync("20 jours");
    await Expect(Sheet(page).Locator("[data-field='countdownTail']")).ToHaveTextAsync("pour répondre");

    var share = await Sheet(page).Locator(".deadline-track span").EvaluateAsync<double>(
      "bar => bar.getBoundingClientRect().width / bar.parentElement.getBoundingClientRect().width");
    share.ShouldBe(1d / 3, 0.02, "La barre ne dessine pas le tiers du délai écoulé.");
  }

  /// <summary>
  /// ⚠️ <b>Une demande close n'affiche aucun signalement</b>, quelle que soit sa date limite : plus
  /// rien n'est dû.
  /// </summary>
  [Theory]
  [InlineData("Completed")]
  [InlineData("Cancelled")]
  public async Task ShowsNoSignalOnAClosedRequestHoweverLateItIs(string status)
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var email = UniqueEmail();
    var message = await harness.RecordRequestAsync(email: email);
    var deadline = ParisCalendar.Today(TimeProvider.System).AddDays(-30);

    await harness.SetResponseDeadlineAsync(message, deadline);
    await harness.SetStatusAsync(message, RequestStatus.FromName(status));

    await OpenTheSheetAsync(await OpenedRowOfAsync(page, email));

    (await FactsOfAsync(page))
      .ShouldContain($"Date limite de réponse : {deadline.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}");

    // ⚠️ NI DÉCOMPTE NI BARRE : plus aucun délai ne court.
    await Expect(CountdownLeadOf(page)).ToBeHiddenAsync();
    await Expect(Sheet(page).Locator(".deadline-track")).ToBeHiddenAsync();
    await Expect(StatusOf(page)).ToBeVisibleAsync();
  }

  /// <summary>
  /// <b>La fiche garde la même forme d'une demande à l'autre</b> : les dix intitulés de ses listes
  /// sont les mêmes, dans le même ordre, que la demande porte toutes ses valeurs ou presque aucune.
  /// </summary>
  [Fact]
  public async Task KeepsTheSameShapeFromOneRequestToTheNext()
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var bare = UniqueEmail();
    await harness.RecordRequestAsync(email: bare);

    await OpenTheSheetAsync(await ARecordedRowAsync(page));
    var shapeOfTheNamed = await LabelsOfAsync(page);
    await CloseByAsync(page, "Échap");
    await Expect(Sheet(page)).ToBeHiddenAsync();

    await OpenTheSheetAsync(await OpenedRowOfAsync(page, bare));

    (await LabelsOfAsync(page)).ShouldBe(shapeOfTheNamed);
  }

  /// <summary>
  /// <b>L'œil d'une demande qu'on vient de créer ouvre sa fiche, message et origine compris</b>, sans
  /// recharger la page : la ligne insérée porte ce que la fiche lui demande, comme celles du
  /// chargement.
  /// </summary>
  [Fact]
  public async Task CarriesWhatTheRowOfAJustCreatedRequestSays()
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    await page.GotoAsync("/demandes");
    var email = UniqueEmail();
    var message = $"Je souhaite accéder à mes données. {Guid.NewGuid()}";

    await CreateFromTheDialogAsync(page, email, message);

    await OpenTheSheetAsync(await OpenedRowOfAsync(page, email));

    await Expect(TitleOf(page)).ToHaveTextAsync(email);
    await Expect(MessageOf(page)).ToHaveTextAsync(message);
    (await FactsOfAsync(page)).ShouldContain("Origine : Courrier");
    (await FactsOfAsync(page)).ShouldContain($"Email : {email}");
  }

  /// <summary>
  /// <b>La fiche d'une demande prolongée montre le bloc « Prolongation » entier</b> — la date limite
  /// initiale, la date de la prolongation, le motif sous son libellé français et la justification —,
  /// et <b>celle d'une demande non prolongée ne le montre pas du tout</b> : ni titre, ni champ vide,
  /// ni tiret. Les deux demandes sont sur la même page, et rien n'est rechargé entre les deux.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>La justification se lit en entier</b>, dans le même bloc que le message de la demande :
  /// c'est l'autre texte long de la fiche.
  /// </remarks>
  [Fact]
  public async Task ShowsTheExtensionBlockOnAnExtendedRequestAndHidesItOnAnother()
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var extended = UniqueEmail();
    var untouched = UniqueEmail();
    var justification = ALongJustification();

    // ⚠️ LA DATE LIMITE EST POSÉE DANS UN MOIS : la fenêtre de prolongation se juge sur le jour
    // courant, et une date en dur finirait par la fermer — la demande ne se prolongerait plus.
    var deadline = ParisCalendar.Today(TimeProvider.System).AddMonths(1);

    await harness.SetResponseDeadlineAsync(
      await harness.RecordRequestAsync("Martin", "Jeanne", extended),
      deadline);
    await harness.RecordRequestAsync("Martin", "Jeanne", untouched);
    await page.GotoAsync("/demandes");

    var extendedRow = await RowOfAsync(page, extended);
    await ExtendFromTheDialogAsync(page, extendedRow, justification);
    await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

    var extendedAt = await extendedRow.GetAttributeAsync("data-sheet-extended-at");
    extendedAt.ShouldNotBeNull("La ligne prolongée ne porte pas la date de la prolongation.");
    extendedAt.ShouldMatch(@"^\d{2}/\d{2}/\d{4} \d{2}:\d{2}$", "La date de la prolongation n'est pas un instant en jj/mm/aaaa hh:mm.");

    // ⚠️ RIEN NE PART AU RÉSEAU : le bloc vient des `data-sheet-*` de la ligne, comme le reste.
    var sent = new List<string>();
    page.Request += (_, request) => sent.Add($"{request.Method} {request.Url}");

    await OpenTheSheetAsync(extendedRow);

    sent.ShouldBeEmpty("L'ouverture de la fiche d'une demande prolongée a appelé le service.");
    await Expect(ExtensionBlock(page)).ToBeVisibleAsync();
    (await FactsOfAsync(page)).ShouldContain(
      $"Date limite initiale : {deadline.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture)}");
    (await FactsOfAsync(page)).ShouldContain($"Date de la prolongation : {extendedAt}");
    (await FactsOfAsync(page)).ShouldContain($"{ExtensionConfirmation.GroundLabel} : {ExtensionGround.Complexity.FrenchLabel}");

    // ⚠️ LA JUSTIFICATION LA PLUS LONGUE QUE LE DOMAINE ACCEPTE S'AFFICHE EN ENTIER, comme le message.
    (await JustificationOf(page).TextContentAsync()).ShouldBe(justification);
    await Expect(JustificationOf(page)).ToHaveCSSAsync("white-space", "pre-wrap");
    await Expect(JustificationOf(page)).ToHaveCSSAsync("overflow-y", "auto");

    await CloseByAsync(page, "Échap");
    await Expect(Sheet(page)).ToBeHiddenAsync();

    // ⚠️ L'AUTRE DEMANDE DE LA MÊME PAGE : le bloc disparaît entier, titre compris.
    await OpenTheSheetAsync(await RowOfAsync(page, untouched));

    await Expect(ExtensionBlock(page)).ToBeHiddenAsync();
    await Expect(Sheet(page).GetByRole(AriaRole.Heading, new() { Name = ExtensionBlockTitle, Exact = true })).ToBeHiddenAsync();
    (await LabelsOfAsync(page)).ShouldNotContain("Date limite initiale");
    (await LabelsOfAsync(page)).ShouldNotContain(ExtensionConfirmation.GroundLabel);
    (await Sheet(page).InnerTextAsync()).ShouldNotContain(justification);
  }

  /// <summary>
  /// La justification <b>la plus longue que le domaine accepte</b> — 2 000 caractères —, sur plusieurs
  /// lignes et reconnaissable d'un scénario à l'autre : c'est ce texte-là qui voyage dans un attribut
  /// de la ligne, et que la fiche doit rendre en entier.
  /// </summary>
  private static string ALongJustification()
  {
    var opening = $"Les données sont réparties sur quatre systèmes. {Guid.NewGuid()}\n";

    return opening + new string('à', 2_000 - opening.Length);
  }

  /// <summary>Prolonge la demande de cette ligne depuis la modale, pour le motif de la complexité.</summary>
  private static async Task ExtendFromTheDialogAsync(IPage page, ILocator row, string justification)
  {
    await row.GetByRole(AriaRole.Button, new() { Name = RequestRow.ExtensionOffered, Exact = true }).ClickAsync();

    var dialog = page.GetByRole(AriaRole.Dialog, new() { Name = ExtensionConfirmation.Title, Exact = true });
    await dialog.GetByLabel(ExtensionConfirmation.GroundLabel, new() { Exact = true })
      .SelectOptionAsync(nameof(ExtensionGround.Complexity));
    await dialog.GetByLabel(ExtensionConfirmation.JustificationLabel, new() { Exact = true }).FillAsync(justification);
    await dialog.GetByRole(AriaRole.Button, new() { Name = ExtensionConfirmation.Confirm, Exact = true }).ClickAsync();

    await Expect(dialog).ToBeHiddenAsync();
  }

  /// <summary>Le bloc « Prolongation » de la fiche, montré sur la seule demande qui a été prolongée.</summary>
  private static ILocator ExtensionBlock(IPage page) => Sheet(page).Locator("[data-block='extension']");

  /// <summary>La justification, sous son libellé, hors de la liste de définitions.</summary>
  private static ILocator JustificationOf(IPage page) => Sheet(page).Locator("[data-field='extensionJustification']");

  /// <summary>Crée une demande depuis la modale, reçue par courrier, et attend sa ligne.</summary>
  private static async Task CreateFromTheDialogAsync(IPage page, string email, string message)
  {
    await page.GetByRole(AriaRole.Button, new() { Name = "Créer une demande", Exact = true }).ClickAsync();

    var dialog = page.GetByRole(AriaRole.Dialog, new() { Name = "Créer une nouvelle demande", Exact = true });
    await dialog.GetByRole(AriaRole.Radio, new() { Name = "Courrier", Exact = true }).CheckAsync();
    await dialog.GetByLabel("Email", new() { Exact = true }).And(dialog.Page.Locator(":not([type=radio])")).FillAsync(email);
    await dialog.GetByLabel("Message", new() { Exact = true }).FillAsync(message);
    await dialog.GetByLabel("Droits RGPD", new() { Exact = true }).SelectOptionAsync("Access");
    await dialog.GetByRole(AriaRole.Button, new() { Name = "Créer", Exact = true }).ClickAsync();

    await Expect(dialog).ToBeHiddenAsync();
  }

  /// <summary>
  /// Les valeurs de la fiche <b>appariées à leurs intitulés</b>, dans l'ordre : « intitulé : valeur ».
  /// C'est l'appariement qu'un lecteur d'écran annonce, et c'est lui que les scénarios jugent.
  /// </summary>
  private static async Task<IReadOnlyList<string>> FactsOfAsync(IPage page)
  {
    var labels = await LabelsOfAsync(page);
    var values = await Sheet(page).GetByRole(AriaRole.Definition).AllTextContentsAsync();

    values.Count.ShouldBe(labels.Count, "Un intitulé de la fiche est resté sans valeur, ou l'inverse.");

    return labels.Zip(values, (label, value) => $"{label} : {value}").ToList();
  }

  /// <summary>Les intitulés des listes de la fiche, dans l'ordre : sa forme, indépendante de ce qu'elle porte.</summary>
  private static async Task<IReadOnlyList<string>> LabelsOfAsync(IPage page)
  {
    return await Sheet(page).GetByRole(AriaRole.Term).AllTextContentsAsync();
  }

  /// <summary>Le titre de la fiche : le nom de la personne, et le nom accessible de la surface.</summary>
  private static ILocator TitleOf(IPage page)
  {
    return Sheet(page).GetByRole(AriaRole.Heading).First;
  }

  /// <summary>Le message, sous son libellé, hors de la liste de définitions.</summary>
  private static ILocator CountdownLeadOf(IPage page)
  {
    return Sheet(page).Locator("[data-field='countdownLead']");
  }

  private static ILocator MessageOf(IPage page)
  {
    return Sheet(page).Locator("[data-field='message']");
  }

  /// <summary>Le badge du statut, cloné depuis la cellule de la ligne.</summary>
  private static ILocator StatusOf(IPage page)
  {
    return Sheet(page).Locator("[data-field='status'] .status");
  }

  /// <summary>La teinte du badge de la ligne — celle que la fiche doit porter, puisque c'est le même badge.</summary>
  private static async Task<string> BackgroundOfTheBadgeOfAsync(ILocator row)
  {
    return await row.Locator("td[data-field='status'] .status")
      .EvaluateAsync<string>("badge => getComputedStyle(badge).backgroundColor");
  }

  private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

  /// <summary>
  /// La ligne de la demande qui porte cet email, <b>sur le tableau déjà ouvert</b> : c'est elle que
  /// le scénario de la demande qu'on vient de créer demande, et rien ne doit recharger la page.
  /// </summary>
  private static async Task<ILocator> RowOfAsync(IPage page, string email)
  {
    var row = page.GetByRole(AriaRole.Row).Filter(new() { HasText = email });
    await Expect(row).ToHaveCountAsync(1);

    return row;
  }

  /// <summary>Ouvre le tableau, et rend la ligne de la demande qui porte cet email.</summary>
  private static async Task<ILocator> OpenedRowOfAsync(IPage page, string email)
  {
    await page.GotoAsync("/demandes");

    return await RowOfAsync(page, email);
  }

  /// <summary>Ouvre la fiche de cette ligne, et attend qu'elle soit là.</summary>
  private static async Task OpenTheSheetAsync(ILocator row)
  {
    await EyeOf(row).ClickAsync();
    await Expect(Sheet(row.Page)).ToBeVisibleAsync();
    await WaitForTheSheetToSettleAsync(row.Page);
  }

  /// <summary>
  /// Attend que la fiche soit <b>arrivée</b> : le glissement de l'ouverture dure 150 ms, et une
  /// mesure prise en chemin ne dit rien de la place que la fiche prend.
  /// </summary>
  private static async Task WaitForTheSheetToSettleAsync(IPage page)
  {
    await Sheet(page).EvaluateAsync("sheet => Promise.all(sheet.getAnimations().map(slide => slide.finished))");
  }

  /// <summary>
  /// Ferme la fiche par l'un des quatre modes. Le clic sur le fond tombe <b>dans le coin haut gauche
  /// de la fenêtre</b>, loin de la fiche ancrée au bord droit.
  /// </summary>
  private static Task CloseByAsync(IPage page, string mode)
  {
    return mode switch
    {
      "Fermer" => CloseButtonOf(page).ClickAsync(),
      "la croix" => CrossOf(page).ClickAsync(),
      "Échap" => page.Keyboard.PressAsync("Escape"),
      "le fond" => page.Mouse.ClickAsync(5, 5),
      _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Mode de fermeture inconnu."),
    };
  }

  /// <summary>
  /// La sortie nommée du pied : des deux boutons « Fermer » de la fiche, c'est celui qui donne son
  /// libellé à lire — la croix, elle, ne se nomme que pour qui ne la voit pas.
  /// </summary>
  private static ILocator CloseButtonOf(IPage page)
  {
    return WaysOut(page).Filter(new() { HasText = "Fermer" });
  }

  /// <summary>La croix de la tête : le bouton « Fermer » qui ne donne rien à lire.</summary>
  private static ILocator CrossOf(IPage page)
  {
    return WaysOut(page).Filter(new() { HasNotText = "Fermer" });
  }

  private static ILocator WaysOut(IPage page)
  {
    return Sheet(page).GetByRole(AriaRole.Button, new() { Name = "Fermer", Exact = true });
  }

  /// <summary>
  /// La fiche : le seul <c>dialog</c> qui porte le bloc « La personne ». ⚠️ Elle ne se cherche
  /// pas par son nom accessible — c'est le nom de la personne, vide tant que rien ne l'y écrit.
  /// </summary>
  private static ILocator Sheet(IPage page)
  {
    return page
      .GetByRole(AriaRole.Dialog, new() { IncludeHidden = true })
      .Filter(new() { Has = page.GetByRole(AriaRole.Heading, new() { Name = FirstBlock, Exact = true, IncludeHidden = true }) });
  }

  private static ILocator Confirmation(IPage page)
  {
    return page.GetByRole(AriaRole.Alertdialog, new() { Name = ConfirmationTitle, Exact = true, IncludeHidden = true });
  }

  private static ILocator EyeOf(ILocator row)
  {
    return row.GetByRole(AriaRole.Button, new() { Name = Eye, Exact = true });
  }

  /// <summary>
  /// Enregistre une demande <b>par le use case</b>, ouvre le tableau, et rend sa ligne — celle qui
  /// porte son email unique.
  /// </summary>
  private async Task<ILocator> ARecordedRowAsync(IPage page)
  {
    var email = UniqueEmail();

    await harness.RecordRequestAsync(lastName: "Martin", firstName: "Jeanne", email: email);

    return await OpenedRowOfAsync(page, email);
  }
}
