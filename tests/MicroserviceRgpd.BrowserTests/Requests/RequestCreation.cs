using System.Text.RegularExpressions;

namespace MicroserviceRgpd.BrowserTests.Requests;

/// <summary>
/// <b>L'<c>Operator</c> crée une demande depuis la modale</b>, dans un vrai navigateur : une saisie
/// valide part au handler de la page, la modale se ferme, et le toast « Demande créée » le dit ; un
/// refus du serveur s'affiche sous ses champs ; tout autre échec pose un bandeau dans la modale, qui
/// reste ouverte avec la saisie intacte.
/// </summary>
/// <remarks>
/// <para>
/// Ce qui est enregistré se relit <b>en base</b>, par le message unique de chaque scénario : la base
/// est partagée par toute la collection.
/// </para>
/// <para>
/// ⚠️ <b>Les réponses que le vrai service ne donne pas sur commande</b> — un refus que le navigateur
/// aurait laissé passer, une erreur serveur, une coupure réseau — <b>sont simulées</b> en interceptant
/// l'envoi. Le reste atteint le vrai handler.
/// </para>
/// </remarks>
[Collection(BrowserCreationCollection.Name)]
public class RequestCreation(BrowserHarness harness)
{
  private const string DialogTitle = "Créer une nouvelle demande";

  private const string Created = "Demande créée";

  private const string Oldest = "Date de réception la plus ancienne";

  private const string NoMatch = "Aucune demande ne correspond à votre recherche";

  private const string Failure = "La demande n'a pas pu être enregistrée. Votre saisie est conservée : vous pouvez réessayer.";

  private static readonly Regex CreateHandler = new(@"/demandes\?handler=Create$");

  /// <summary>
  /// <b>Une saisie valide crée la demande</b> : la modale se ferme, le toast « Demande créée » paraît
  /// quelques secondes puis s'en va, et la demande est en base.
  /// </summary>
  [Fact]
  public async Task CreatesTheRequestAndSaysSoForAFewSeconds()
  {
    await using var context = await harness.NewContextAsync();
    await context.Clock.InstallAsync();
    var page = await OpenedAsync(context);
    var message = UniqueMessage();

    await FillAValidEntryAsync(page, message);
    await CreateAsync(page);

    await Expect(Dialog(page)).ToBeHiddenAsync();
    await Expect(Toast(page)).ToBeVisibleAsync();
    (await harness.CountOfRequestsAsync(message)).ShouldBe(1);

    await page.Clock.RunForAsync(4_000);
    await Expect(Toast(page)).ToBeVisibleAsync();

    await page.Clock.RunForAsync(1_500);
    await Expect(Toast(page)).ToBeHiddenAsync();
  }

  /// <summary>
  /// <b>La demande créée apparaît dans le tableau, sans rechargement</b> — avec sa date limite de
  /// réponse et le badge « En cours » —, le toast « Demande créée » le dit ; et sur un tableau qui
  /// était vide, « Aucune demande pour le moment » disparaît.
  /// </summary>
  [Fact]
  public async Task InsertsTheRowOfTheNewRequestWithoutReloadingAndDropsTheEmptyState()
  {
    await using var context = await harness.NewContextAsync();
    await harness.DeleteAllRequestsAsync();
    var page = await OpenedAsync(context);
    var none = page.GetByText("Aucune demande pour le moment", new() { Exact = true });
    await Expect(none).ToBeVisibleAsync();
    var email = UniqueEmail();

    // Un marqueur posé sur la fenêtre : un rechargement l'effacerait.
    await page.EvaluateAsync("() => { window.untouched = true; }");

    await FillAValidEntryAsync(page, UniqueMessage(), email);
    await Field(page, "Date de réception").FillAsync("2026-01-31");
    await CreateAsync(page);

    await Expect(Toast(page)).ToBeVisibleAsync();
    await Expect(RowOf(page, email)).ToBeVisibleAsync();
    // La date limite, antérieure à aujourd'hui, est signalée dans la ligne insérée comme dans le tableau.
    await Expect(RowOf(page, email).GetByRole(AriaRole.Cell, new() { Name = "28/02/2026 En retard", Exact = true })).ToBeVisibleAsync();
    await Expect(RowOf(page, email).GetByText("En cours", new() { Exact = true })).ToBeVisibleAsync();
    await Expect(none).ToBeHiddenAsync();
    (await page.EvaluateAsync<bool>("() => window.untouched === true")).ShouldBeTrue("La création a rechargé la page.");
  }

  /// <summary>
  /// <b>Avec « la plus récente » — le tri par défaut —, la ligne se place à sa date de réception</b>,
  /// puis à sa date de création : une demande reçue le même jour qu'une autre passe donc devant elle,
  /// puisqu'elle vient d'être créée.
  /// </summary>
  [Fact]
  public async Task PlacesTheRowInTheDefaultOrder()
  {
    await using var context = await harness.NewContextAsync();
    await harness.DeleteAllRequestsAsync();
    var recorded = UniqueEmail();
    await harness.RecordRequestAsync(email: recorded);
    var page = await context.NewPageAsync();
    await page.GotoAsync("/demandes");
    await Expect(SortMenu(page)).ToHaveValueAsync("newest");

    var later = await CreateReceivedOnAsync(page, "2026-01-20");
    var sameDay = await CreateReceivedOnAsync(page, "2026-01-15");
    var earlier = await CreateReceivedOnAsync(page, "2026-01-01");

    await ExpectOrderAsync(page, later, sameDay, recorded, earlier);
  }

  /// <summary>
  /// ⚠️ <b>Avec « la plus ancienne » sélectionnée, la ligne se place dans cet ordre-là</b>, et non
  /// dans l'ordre par défaut : la date de réception la plus ancienne d'abord, puis la date de
  /// création la plus ancienne — une demande reçue le même jour qu'une autre passe donc derrière
  /// elle, puisqu'elle vient d'être créée.
  /// </summary>
  [Fact]
  public async Task PlacesTheRowInTheSelectedOrderWhenTheOldestComesFirst()
  {
    await using var context = await harness.NewContextAsync();
    await harness.DeleteAllRequestsAsync();
    var recorded = UniqueEmail();
    await harness.RecordRequestAsync(email: recorded, receivedOn: "2026-01-15");
    var page = await context.NewPageAsync();
    await page.GotoAsync("/demandes");
    await SortMenu(page).SelectOptionAsync(new SelectOptionValue { Label = Oldest });

    var later = await CreateReceivedOnAsync(page, "2026-01-20");
    var sameDay = await CreateReceivedOnAsync(page, "2026-01-15");
    var earlier = await CreateReceivedOnAsync(page, "2026-01-01");

    await ExpectOrderAsync(page, earlier, recorded, sameDay, later);
  }

  /// <summary>
  /// ⚠️ <b>La recherche en cours s'applique à la nouvelle ligne</b> : celle qui ne lui correspond pas
  /// entre cachée, et « Aucune demande ne correspond à votre recherche » reste affiché ; celle qui lui
  /// correspond est visible, et chasse le message.
  /// </summary>
  /// <remarks>
  /// ⚠️ La recherche vidée à la fin, <b>la ligne cachée reparaît, et à sa place</b> : c'est ce qui
  /// distingue une ligne entrée cachée d'une ligne qui ne serait jamais entrée.
  /// </remarks>
  [Fact]
  public async Task ShowsTheNewRowOnlyWhenItMatchesTheSearch()
  {
    await using var context = await harness.NewContextAsync();
    await harness.DeleteAllRequestsAsync();
    var page = await context.NewPageAsync();
    await page.GotoAsync("/demandes");

    var marker = Guid.NewGuid().ToString("N");
    await Search(page).FillAsync(marker);

    var stranger = UniqueEmail();
    await CreateWithEmailAsync(page, stranger);

    await Expect(Toast(page)).ToBeVisibleAsync();
    await Expect(RowOf(page, stranger)).ToHaveCountAsync(0);
    await Expect(NoMatchMessage(page)).ToBeVisibleAsync();

    var sought = $"{marker}@example.org";
    await CreateWithEmailAsync(page, sought);

    await Expect(RowOf(page, sought)).ToBeVisibleAsync();
    await Expect(NoMatchMessage(page)).ToBeHiddenAsync();
    await Expect(RowOf(page, stranger)).ToHaveCountAsync(0);

    await Search(page).FillAsync(string.Empty);

    await ExpectOrderAsync(page, sought, stranger);
  }

  /// <summary>
  /// ⚠️ <b>« Créer » est désactivé pendant l'envoi</b> : un double clic ne part qu'une fois, et
  /// n'enregistre qu'une seule demande.
  /// </summary>
  [Fact]
  public async Task DisablesCreateWhileSendingSoADoubleClickRecordsOnce()
  {
    await using var context = await harness.NewContextAsync();
    var page = await OpenedAsync(context);
    var message = UniqueMessage();

    var sent = 0;
    var release = new TaskCompletionSource();
    await page.RouteAsync(CreateHandler, async route =>
    {
      Interlocked.Increment(ref sent);
      await release.Task;
      await route.ContinueAsync();
    });

    await FillAValidEntryAsync(page, message);
    await CreateButton(page).DblClickAsync();

    await Expect(CreateButton(page)).ToBeDisabledAsync();

    release.SetResult();

    await Expect(Toast(page)).ToBeVisibleAsync();
    sent.ShouldBe(1);
    (await harness.CountOfRequestsAsync(message)).ShouldBe(1);
  }

  /// <summary>
  /// <b>Un refus du serveur s'affiche comme un refus du navigateur</b> : sous chaque champ concerné,
  /// le focus sur le premier. La règle est ici une que le navigateur aurait laissée passer — la
  /// réponse est simulée.
  /// </summary>
  [Fact]
  public async Task ShowsTheServerRefusalsUnderTheirFields()
  {
    await using var context = await harness.NewContextAsync();
    var page = await OpenedAsync(context);

    await RefuseTheNameAndTheMessageAsync(page);

    await FillAValidEntryAsync(page, UniqueMessage());
    await Field(page, "Nom").FillAsync("Dupont");
    await CreateAsync(page);

    await Expect(Field(page, "Nom")).ToHaveAccessibleDescriptionAsync("Le nom ne peut pas dépasser 100 caractères.");
    await Expect(Field(page, "Nom")).ToHaveAttributeAsync("aria-invalid", "true");
    await Expect(Field(page, "Message")).ToHaveAccessibleDescriptionAsync("Le message ne peut pas dépasser 10 000 caractères.");
    await Expect(Field(page, "Message")).ToHaveAttributeAsync("aria-invalid", "true");
    await Expect(Field(page, "Nom")).ToBeFocusedAsync();

    await Expect(Dialog(page)).ToBeVisibleAsync();
    await Expect(Banner(page)).ToBeHiddenAsync();
    await Expect(Toast(page)).ToBeHiddenAsync();
    await Expect(CreateButton(page)).ToBeEnabledAsync();
  }

  /// <summary>
  /// ⚠️ <b>Un refus du serveur tient jusqu'à ce que son champ change</b>, comme un refus du navigateur
  /// tient jusqu'à sa correction : les règles du navigateur, qui avaient laissé passer la saisie, ne
  /// suffisent pas à le lever. Une frappe dans un autre champ le laisse en place.
  /// </summary>
  [Fact]
  public async Task KeepsAServerRefusalUntilItsFieldChanges()
  {
    await using var context = await harness.NewContextAsync();
    var page = await OpenedAsync(context);
    await RefuseTheNameAndTheMessageAsync(page);

    await FillAValidEntryAsync(page, UniqueMessage());
    await Field(page, "Nom").FillAsync("Dupont");
    await CreateAsync(page);
    await Expect(Field(page, "Nom")).ToHaveAttributeAsync("aria-invalid", "true");

    await Field(page, "Prénom").FillAsync("Jeanne");

    await Expect(Field(page, "Nom")).ToHaveAccessibleDescriptionAsync("Le nom ne peut pas dépasser 100 caractères.");
    await Expect(Field(page, "Message")).ToHaveAttributeAsync("aria-invalid", "true");

    await Field(page, "Nom").FillAsync("Martin");

    await Expect(Field(page, "Nom")).ToHaveAccessibleDescriptionAsync(new Regex("^$"));
    await Expect(Field(page, "Nom")).Not.ToHaveAttributeAsync("aria-invalid", "true");
    await Expect(Field(page, "Message")).ToHaveAttributeAsync("aria-invalid", "true");
  }

  /// <summary>
  /// ⚠️ <b>Pendant l'envoi, la modale ne se ferme pas</b> — ni par « Annuler », ni par la croix, ni
  /// par le fond : la réponse qui arrive trouve la saisie qu'elle concerne, et la modale se ferme
  /// sur la création.
  /// </summary>
  [Fact]
  public async Task StaysOpenWhileSending()
  {
    await using var context = await harness.NewContextAsync();
    var page = await OpenedAsync(context);
    var message = UniqueMessage();

    var release = new TaskCompletionSource();
    await page.RouteAsync(CreateHandler, async route =>
    {
      await release.Task;
      await route.ContinueAsync();
    });

    await FillAValidEntryAsync(page, message);
    await CreateAsync(page);
    await Expect(CreateButton(page)).ToBeDisabledAsync();

    await Dialog(page).GetByRole(AriaRole.Button, new() { Name = "Annuler", Exact = true }).ClickAsync();
    await Dialog(page).GetByRole(AriaRole.Button, new() { Name = "Fermer", Exact = true }).ClickAsync();
    await page.Mouse.ClickAsync(5, 5);

    await Expect(Dialog(page)).ToBeVisibleAsync();
    await Expect(page.GetByRole(AriaRole.Alertdialog)).ToBeHiddenAsync();

    release.SetResult();

    await Expect(Dialog(page)).ToBeHiddenAsync();
    await Expect(Toast(page)).ToBeVisibleAsync();
    (await harness.CountOfRequestsAsync(message)).ShouldBe(1);
  }

  /// <summary>Les échecs qui ne sont pas un refus de la saisie.</summary>
  public static TheoryData<string> Failures { get; } = ["une erreur serveur", "une coupure réseau", "un jeton anti-rejeu périmé"];

  /// <summary>
  /// <b>Tout autre échec pose un bandeau dans la modale</b>, qui reste ouverte avec la saisie
  /// intacte, et rien n'est enregistré. Le jeton périmé n'est pas simulé : le cookie qui le porte est
  /// effacé, et c'est le vrai service qui refuse.
  /// </summary>
  [Theory]
  [MemberData(nameof(Failures))]
  public async Task ShowsTheBannerAndKeepsTheEntryOnAnyOtherFailure(string failure)
  {
    await using var context = await harness.NewContextAsync();
    var page = await OpenedAsync(context);
    var message = UniqueMessage();

    switch (failure)
    {
      case "une erreur serveur":
        await page.RouteAsync(CreateHandler, route => route.FulfillAsync(new() { Status = 500 }));
        break;
      case "une coupure réseau":
        await page.RouteAsync(CreateHandler, route => route.AbortAsync("internetdisconnected"));
        break;
      default:
        await context.ClearCookiesAsync();
        break;
    }

    await FillAValidEntryAsync(page, message);
    await Field(page, "Nom").FillAsync("Dupont");
    await CreateAsync(page);

    await Expect(Banner(page)).ToBeVisibleAsync();
    await Expect(Dialog(page)).ToBeVisibleAsync();
    await Expect(Toast(page)).ToBeHiddenAsync();
    await Expect(CreateButton(page)).ToBeEnabledAsync();

    await Expect(Field(page, "Nom")).ToHaveValueAsync("Dupont");
    await Expect(Field(page, "Email")).ToHaveValueAsync("jeanne.martin@example.org");
    await Expect(Field(page, "Message")).ToHaveValueAsync(message);
    await Expect(Field(page, "Droits RGPD")).ToHaveValueAsync("Access");

    (await harness.CountOfRequestsAsync(message)).ShouldBe(0);
  }

  /// <summary>
  /// <b>Après un échec, l'<c>Operator</c> réessaie sans rien ressaisir</b> : la même saisie part, le
  /// bandeau s'en va, et la demande est créée.
  /// </summary>
  [Fact]
  public async Task CreatesOnRetryAfterAFailure()
  {
    await using var context = await harness.NewContextAsync();
    var page = await OpenedAsync(context);
    var message = UniqueMessage();

    await page.RouteAsync(CreateHandler, route => route.AbortAsync("internetdisconnected"));
    await FillAValidEntryAsync(page, message);
    await CreateAsync(page);
    await Expect(Banner(page)).ToBeVisibleAsync();

    await page.UnrouteAsync(CreateHandler);
    await CreateAsync(page);

    await Expect(Dialog(page)).ToBeHiddenAsync();
    await Expect(Toast(page)).ToBeVisibleAsync();
    (await harness.CountOfRequestsAsync(message)).ShouldBe(1);
  }

  /// <summary>
  /// <b>Après une création, la réouverture présente les valeurs par défaut</b> — sans refus, sans
  /// bandeau —, quoi qu'ait porté la saisie qui vient d'être enregistrée.
  /// </summary>
  [Fact]
  public async Task ReopensWithTheDefaultsAfterACreation()
  {
    await using var context = await harness.NewContextAsync();
    var page = await OpenedAsync(context);
    var today = await Field(page, "Date de réception").InputValueAsync();

    await Field(page, "Origine").SelectOptionAsync("Letter");
    await Field(page, "Date de réception").FillAsync("2026-01-15");
    await Field(page, "Nom").FillAsync("Martin");
    await Field(page, "Prénom").FillAsync("Jeanne");
    await Field(page, "Identité vérifiée").CheckAsync();
    await Field(page, "Message").FillAsync(UniqueMessage());
    await Field(page, "Droits RGPD").SelectOptionAsync("Erasure");
    await CreateAsync(page);
    await Expect(Dialog(page)).ToBeHiddenAsync();

    await OpenAsync(page);

    await Expect(Field(page, "Origine")).ToHaveValueAsync("Email");
    await Expect(Field(page, "Date de réception")).ToHaveValueAsync(today);
    await Expect(Field(page, "Nom")).ToHaveValueAsync(string.Empty);
    await Expect(Field(page, "Prénom")).ToHaveValueAsync(string.Empty);
    await Expect(Field(page, "Email")).ToHaveValueAsync(string.Empty);
    await Expect(Field(page, "Identité vérifiée")).Not.ToBeCheckedAsync();
    await Expect(Field(page, "Message")).ToHaveValueAsync(string.Empty);
    await Expect(Field(page, "Droits RGPD")).ToHaveValueAsync(string.Empty);
    await Expect(Banner(page)).ToBeHiddenAsync();
  }

  /// <summary>
  /// Le serveur refuse désormais toute création, au nom et au message — deux règles que le navigateur
  /// aurait laissé passer.
  /// </summary>
  private static Task RefuseTheNameAndTheMessageAsync(IPage page)
  {
    return page.RouteAsync(CreateHandler, route => route.FulfillAsync(new()
    {
      Status = 400,
      ContentType = "application/problem+json",
      Body = """
        {
          "title": "One or more validation errors occurred.",
          "status": 400,
          "errors": {
            "lastName": ["Le nom ne peut pas dépasser 100 caractères."],
            "message": ["Le message ne peut pas dépasser 10 000 caractères."]
          }
        }
        """,
    }));
  }

  private static string UniqueMessage() => $"Je souhaite accéder à mes données. {Guid.NewGuid()}";

  private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

  /// <summary>
  /// Crée depuis la modale une demande reçue ce jour-là, et attend sa ligne ; rend son email unique.
  /// </summary>
  private static async Task<string> CreateReceivedOnAsync(IPage page, string receivedOn)
  {
    var email = UniqueEmail();

    await OpenAsync(page);
    await FillAValidEntryAsync(page, UniqueMessage(), email);
    await Field(page, "Date de réception").FillAsync(receivedOn);
    await CreateAsync(page);
    await Expect(Dialog(page)).ToBeHiddenAsync();
    await Expect(RowOf(page, email)).ToBeVisibleAsync();

    return email;
  }

  /// <summary>
  /// Crée depuis la modale une demande portant cet email, et n'attend que la fermeture de la modale :
  /// la ligne peut entrer cachée, et une ligne cachée ne se distingue pas d'une ligne absente.
  /// </summary>
  private static async Task CreateWithEmailAsync(IPage page, string email)
  {
    await OpenAsync(page);
    await FillAValidEntryAsync(page, UniqueMessage(), email);
    await CreateAsync(page);
    await Expect(Dialog(page)).ToBeHiddenAsync();
  }

  private static async Task FillAValidEntryAsync(IPage page, string message, string email = "jeanne.martin@example.org")
  {
    await Field(page, "Email").FillAsync(email);
    await Field(page, "Message").FillAsync(message);
    await Field(page, "Droits RGPD").SelectOptionAsync("Access");
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

  private static ILocator RowOf(IPage page, string email)
  {
    return page.GetByRole(AriaRole.Row).Filter(new() { HasText = email });
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

  private static ILocator SortMenu(IPage page)
  {
    return page.GetByRole(AriaRole.Combobox, new() { Name = "Trier par", Exact = true });
  }

  private static ILocator Search(IPage page)
  {
    return page.GetByRole(AriaRole.Searchbox, new() { Name = "Rechercher une demande", Exact = true });
  }

  /// <summary>Le message, lu dans le cadre du tableau : c'est là, à la place des lignes, qu'il se lit.</summary>
  private static ILocator NoMatchMessage(IPage page)
  {
    return Frame(page).GetByText(NoMatch, new() { Exact = true });
  }

  private static ILocator Frame(IPage page)
  {
    return page.GetByRole(AriaRole.Region, new() { Name = "Liste des demandes", Exact = true });
  }

  /// <summary>
  /// Les lignes affichées du corps du tableau, dans l'ordre du tableau : le dernier groupe de lignes
  /// est le corps, l'en-tête étant le premier.
  /// </summary>
  private static ILocator Rows(IPage page)
  {
    return Frame(page).GetByRole(AriaRole.Rowgroup).Last.GetByRole(AriaRole.Row);
  }

  private static ILocator Toast(IPage page)
  {
    return page.GetByRole(AriaRole.Status).And(page.GetByText(Created, new() { Exact = true }));
  }

  private static ILocator Banner(IPage page)
  {
    return Dialog(page).GetByRole(AriaRole.Alert).And(page.GetByText(Failure, new() { Exact = true }));
  }
}
