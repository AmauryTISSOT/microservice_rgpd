using System.Text.RegularExpressions;
using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.TestDoubles.HostSystem;
using MicroserviceRgpd.UseCases.Requests.ExecuteDataSubjectRequest;
using MicroserviceRgpd.UseCases.Requests.ReadDataSubjectRequestExecution;
using MicroserviceRgpd.Web.Pages.Requests;

namespace MicroserviceRgpd.BrowserTests.Requests;

/// <summary>
/// <b>L'<c>Operator</c> exécute une demande depuis l'avion en papier de sa ligne</b>, dans un vrai
/// navigateur : la confirmation récapitule ce que le système hôte recevra et à quelle adresse, « Annuler », la croix et Échap la
/// ferment sans appel, l'appel la bloque, et un succès la ferme, met la ligne à Terminée sans
/// rechargement et dit « Demande exécutée » (ADR-0026).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Le système hôte est un factice sur un port réel</b>, dont l'adresse est posée dans le
/// Paramétrage : c'est le vrai client du service qui l'appelle.
/// </para>
/// <para>
/// Les textes attendus se lisent sur les constantes du serveur — <see cref="ExecutionConfirmation"/>,
/// <see cref="ExecutionBlock"/>, <see cref="RequestStatus"/>. Le Paramétrage est un singleton partagé
/// par la collection : chaque test part d'un service vierge et y revient.
/// </para>
/// </remarks>
[Collection(BrowserCollection.Name)]
public class RequestExecution(BrowserHarness harness) : IAsyncLifetime
{
  private HostSystemDouble _host = null!;

  /// <summary>Les trois façons de renoncer.</summary>
  public static TheoryData<string> ClosingModes { get; } = ["Annuler", "la croix", "Échap"];

  public async Task InitializeAsync()
  {
    await harness.ForgetEveryEndpointAsync();
    _host = await HostSystemDouble.StartAsync();
  }

  public async Task DisposeAsync()
  {
    await _host.DisposeAsync();
    await harness.ForgetEveryEndpointAsync();
  }

  /// <summary>
  /// <b>La confirmation récapitule ce que le système hôte recevra et à quelle adresse</b> : son titre,
  /// les valeurs du récapitulatif sous leurs libellés, l'avertissement — et « Exécuter » offert, sans
  /// bandeau.
  /// </summary>
  [Fact]
  public async Task SummarizesWhatTheHostSystemWillReceiveAndWhere()
  {
    var address = _host.AddressOf("/rights/access?tenant=brocanto");
    await harness.ConfigureEndpointAsync(DataSubjectRight.Access, address);
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var email = UniqueEmail();
    var row = await RowOfAnExecutableRequestAsync(page, email);

    await ExecutionOf(row).ClickAsync();

    var expected = ExecutionConfirmation.Of(new DataSubjectRequestExecutionSummary(
      DataSubjectRight.Access,
      FirstName.From("Jeanne"),
      LastName.From("Martin"),
      EmailAddress.From(email),
      EndpointUrl.From(address),
      null));

    var dialog = Confirmation(page);

    await Expect(dialog).ToBeVisibleAsync();
    await Expect(dialog).ToHaveAccessibleDescriptionAsync(ExecutionConfirmation.Warning);
    await Expect(Fact(dialog, ExecutionConfirmation.RightLabel)).ToHaveTextAsync(expected.Right);
    await Expect(Fact(dialog, ExecutionConfirmation.FirstNameLabel)).ToHaveTextAsync(expected.FirstName);
    await Expect(Fact(dialog, ExecutionConfirmation.LastNameLabel)).ToHaveTextAsync(expected.LastName);
    await Expect(Fact(dialog, ExecutionConfirmation.EmailLabel)).ToHaveTextAsync(expected.Email);
    await Expect(Fact(dialog, ExecutionConfirmation.EndpointLabel)).ToHaveTextAsync(expected.Endpoint);
    await Expect(Button(page, ExecutionConfirmation.Confirm)).ToBeEnabledAsync();
    await Expect(dialog.GetByRole(AriaRole.Alert)).ToBeHiddenAsync();
    await Expect(Button(page, ExecutionConfirmation.Cancel)).ToBeFocusedAsync();
  }

  /// <summary>
  /// ⚠️ <b>Une demande devenue non exécutable depuis le chargement le dit dès l'ouverture</b> : l'adresse
  /// retirée dans le dos de l'écran, le bandeau porte le motif et « Exécuter » est éteint.
  /// </summary>
  [Fact]
  public async Task SaysTheBlockAtOnceWhenTheRequestNoLongerExecutes()
  {
    await harness.ConfigureEndpointAsync(DataSubjectRight.Access, _host.AddressOf("/rights/access"));
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var row = await RowOfAnExecutableRequestAsync(page, UniqueEmail());
    await harness.ForgetEveryEndpointAsync();

    await ExecutionOf(row).ClickAsync();

    await Expect(Confirmation(page).GetByRole(AriaRole.Alert))
      .ToHaveTextAsync(ExecutionBlock.NoEndpoint.FrenchLabelFor(DataSubjectRight.Access));
    await Expect(Button(page, ExecutionConfirmation.Confirm)).ToBeDisabledAsync();
  }

  /// <summary>
  /// <b>« Annuler », la croix et Échap ferment la confirmation sans aucun appel</b> : rien ne part au
  /// service, rien au système hôte, et la demande reste En cours.
  /// </summary>
  [Theory]
  [MemberData(nameof(ClosingModes))]
  public async Task ClosesWithoutAnyCall(string mode)
  {
    await harness.ConfigureEndpointAsync(DataSubjectRight.Access, _host.AddressOf("/rights/access"));
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var row = await RowOfAnExecutableRequestAsync(page, UniqueEmail());
    await ExecutionOf(row).ClickAsync();
    await Expect(Confirmation(page)).ToBeVisibleAsync();

    var sent = new List<string>();
    page.Request += (_, request) => sent.Add($"{request.Method} {request.Url}");

    await CloseByAsync(page, mode);

    await Expect(Confirmation(page)).ToBeHiddenAsync();
    await Expect(Status(row)).ToHaveTextAsync(RequestStatus.InProgress.FrenchLabel);
    sent.ShouldBeEmpty("La fermeture a appelé le service.");
    _host.Received.ShouldBeEmpty("La fermeture a appelé le système hôte.");
  }

  /// <summary>
  /// ⚠️ <b>Pendant l'appel, la confirmation est bloquée</b> : « Exécuter » porte <c>aria-busy</c> et son
  /// icône, et ni « Annuler », ni la croix, ni Échap ne la ferment. L'appel achevé, elle se ferme sur
  /// le succès.
  /// </summary>
  [Fact]
  public async Task StaysOpenAndBusyWhileTheCallRuns()
  {
    await harness.ConfigureEndpointAsync(DataSubjectRight.Access, _host.AddressOf("/rights/access"));
    _host.Delay(TimeSpan.FromSeconds(3));
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var row = await RowOfAnExecutableRequestAsync(page, UniqueEmail());
    await ExecutionOf(row).ClickAsync();

    var confirm = Button(page, ExecutionConfirmation.Confirm);
    await confirm.ClickAsync();

    await Expect(confirm).ToHaveAttributeAsync("aria-busy", "true");
    await Expect(confirm.Locator(".spinner")).ToBeVisibleAsync();

    foreach (var mode in ClosingModes)
    {
      await CloseByAsync(page, mode);
      await Expect(Confirmation(page)).ToBeVisibleAsync();
    }

    await Expect(confirm).ToHaveAttributeAsync("aria-busy", "true");
    await Expect(Confirmation(page)).ToBeHiddenAsync(new() { Timeout = 10_000 });
    _host.Received.ShouldHaveSingleItem("L'exécution n'a pas appelé le système hôte une fois, une seule.");
  }

  /// <summary>
  /// <b>Sur un succès, la confirmation se ferme, le toast dit « Demande exécutée »</b>, et la ligne passe
  /// à Terminée sans rechargement : le crayon éteint avec son motif, l'exécution éteinte avec
  /// « Demande close ».
  /// </summary>
  [Fact]
  public async Task CompletesTheRowAndSaysSoOnSuccess()
  {
    await harness.ConfigureEndpointAsync(DataSubjectRight.Access, _host.AddressOf("/rights/access"));
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var email = UniqueEmail();
    var row = await RowOfAnExecutableRequestAsync(page, email);

    // Un marqueur posé sur la fenêtre : un rechargement l'effacerait.
    await page.EvaluateAsync("() => { window.untouched = true; }");

    await ExecutionOf(row).ClickAsync();
    await Button(page, ExecutionConfirmation.Confirm).ClickAsync();

    await Expect(Confirmation(page)).ToBeHiddenAsync();
    await Expect(page.GetByRole(AriaRole.Status).And(page.GetByText(ExecutionConfirmation.Executed, new() { Exact = true })))
      .ToBeVisibleAsync();
    await Expect(Status(row)).ToHaveTextAsync(RequestStatus.Completed.FrenchLabel);
    await Expect(row.GetByRole(AriaRole.Button, new() { Name = "Modifier la demande", Exact = true }))
      .ToHaveAttributeAsync("aria-disabled", "true");
    await Expect(ExecutionOf(row)).ToHaveAttributeAsync("aria-disabled", "true");

    await ExecutionOf(row).HoverAsync();

    await Expect(row.GetByText(ExecutionBlock.Closed.FrenchLabelFor(DataSubjectRight.Access), new() { Exact = true }))
      .ToBeVisibleAsync();
    (await page.EvaluateAsync<bool>("() => window.untouched === true")).ShouldBeTrue("L'exécution a rechargé la page.");
    _host.Received.ShouldHaveSingleItem();
  }

  /// <summary>
  /// <b>Sur un échec, la confirmation reste ouverte et le bandeau dit pourquoi</b> — le <c>detail</c> du
  /// 502 —, la demande reste En cours, « Exécuter » n'est plus occupé. ⚠️ <b>Une nouvelle tentative réussie
  /// suit le chemin du succès</b> : la confirmation se ferme, le toast dit « Demande exécutée », la ligne passe à
  /// Terminée.
  /// </summary>
  [Fact]
  public async Task SaysTheFailureAndSucceedsOnANewAttempt()
  {
    await harness.ConfigureEndpointAsync(DataSubjectRight.Access, _host.AddressOf("/rights/access"));
    _host.Answer(503);
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var row = await RowOfAnExecutableRequestAsync(page, UniqueEmail());
    await page.EvaluateAsync("() => { window.untouched = true; }");
    await ExecutionOf(row).ClickAsync();

    var confirm = Button(page, ExecutionConfirmation.Confirm);
    await confirm.ClickAsync();

    await Expect(Confirmation(page).GetByRole(AriaRole.Alert)).ToHaveTextAsync(ExecutionFailure.NonSuccessResponse(503));
    await Expect(Confirmation(page)).ToBeVisibleAsync();
    await Expect(confirm).Not.ToHaveAttributeAsync("aria-busy", "true");
    await Expect(confirm.Locator(".spinner")).ToBeHiddenAsync();
    await Expect(confirm).ToBeEnabledAsync();
    await Expect(Status(row)).ToHaveTextAsync(RequestStatus.InProgress.FrenchLabel);

    _host.Answer(200);
    await confirm.ClickAsync();

    await Expect(Confirmation(page)).ToBeHiddenAsync();
    await Expect(page.GetByRole(AriaRole.Status).And(page.GetByText(ExecutionConfirmation.Executed, new() { Exact = true })))
      .ToBeVisibleAsync();
    await Expect(Status(row)).ToHaveTextAsync(RequestStatus.Completed.FrenchLabel);
    (await page.EvaluateAsync<bool>("() => window.untouched === true")).ShouldBeTrue("L'exécution a rechargé la page.");
    _host.Received.Count.ShouldBe(2, "L'échec puis la nouvelle tentative n'ont pas appelé le système hôte deux fois.");
  }

  /// <summary>
  /// <b>Après un échec, « Annuler », la croix et Échap ferment de nouveau la confirmation</b>, et la
  /// demande reste En cours.
  /// </summary>
  [Theory]
  [MemberData(nameof(ClosingModes))]
  public async Task ClosesAgainAfterAFailure(string mode)
  {
    await harness.ConfigureEndpointAsync(DataSubjectRight.Access, _host.AddressOf("/rights/access"));
    _host.Answer(503);
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var row = await RowOfAnExecutableRequestAsync(page, UniqueEmail());
    await ExecutionOf(row).ClickAsync();
    await Button(page, ExecutionConfirmation.Confirm).ClickAsync();
    await Expect(Confirmation(page).GetByRole(AriaRole.Alert)).ToBeVisibleAsync();

    await CloseByAsync(page, mode);

    await Expect(Confirmation(page)).ToBeHiddenAsync();
    await Expect(Status(row)).ToHaveTextAsync(RequestStatus.InProgress.FrenchLabel);
  }

  /// <summary>
  /// ⚠️ <b>Le serveur oppose un motif de blocage à ce que la confirmation offrait</b> : l'adresse retirée
  /// entre l'ouverture et le clic, la confirmation reste ouverte, le bandeau donne le motif, « Exécuter »
  /// s'éteint, et la ligne se met à jour sans rechargement — son exécution éteinte, la demande En cours.
  /// </summary>
  [Fact]
  public async Task SaysTheBlockAtConfirmationAndUpdatesTheRowWhenTheSettingsChanged()
  {
    await harness.ConfigureEndpointAsync(DataSubjectRight.Access, _host.AddressOf("/rights/access"));
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var row = await RowOfAnExecutableRequestAsync(page, UniqueEmail());
    await page.EvaluateAsync("() => { window.untouched = true; }");
    await ExecutionOf(row).ClickAsync();

    var confirm = Button(page, ExecutionConfirmation.Confirm);
    await Expect(confirm).ToBeEnabledAsync();
    await harness.ForgetEveryEndpointAsync();

    await confirm.ClickAsync();

    var block = ExecutionBlock.NoEndpoint.FrenchLabelFor(DataSubjectRight.Access);

    await Expect(Confirmation(page).GetByRole(AriaRole.Alert)).ToHaveTextAsync(block);
    await Expect(Confirmation(page)).ToBeVisibleAsync();
    await Expect(confirm).Not.ToHaveAttributeAsync("aria-busy", "true");
    await Expect(confirm).ToBeDisabledAsync();
    await Expect(ExecutionOf(row)).ToHaveAttributeAsync("aria-disabled", "true");
    await Expect(Status(row)).ToHaveTextAsync(RequestStatus.InProgress.FrenchLabel);
    (await page.EvaluateAsync<bool>("() => window.untouched === true")).ShouldBeTrue("Le blocage a rechargé la page.");
    _host.Received.ShouldBeEmpty("Le blocage a appelé le système hôte.");

    await CloseByAsync(page, "Annuler");

    await Expect(Confirmation(page)).ToBeHiddenAsync();
  }

  private static Task CloseByAsync(IPage page, string mode)
  {
    return mode switch
    {
      "Annuler" => Button(page, ExecutionConfirmation.Cancel).ClickAsync(),
      "la croix" => Button(page, "Fermer").ClickAsync(),
      "Échap" => page.Keyboard.PressAsync("Escape"),
      _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Mode de fermeture inconnu."),
    };
  }

  private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

  /// <summary>
  /// Enregistre une demande exécutable au droit d'accès — Jeanne Martin, identité vérifiée —, ouvre le
  /// tableau et rend sa ligne.
  /// </summary>
  private async Task<ILocator> RowOfAnExecutableRequestAsync(IPage page, string email)
  {
    await harness.RecordRequestAsync("Martin", "Jeanne", email, identityVerified: true, right: nameof(DataSubjectRight.Access));
    await page.GotoAsync("/demandes");

    var row = page.GetByRole(AriaRole.Row).Filter(new() { HasText = email });
    await Expect(row).ToHaveCountAsync(1);

    return row;
  }

  private static ILocator ExecutionOf(ILocator row) =>
    row.GetByRole(AriaRole.Button, new() { Name = RequestRow.ExecutionOffered, Exact = true });

  private static ILocator Status(ILocator row) => row.Locator("td[data-field=status]");

  private static ILocator Confirmation(IPage page) =>
    page.GetByRole(AriaRole.Alertdialog, new() { Name = ExecutionConfirmation.Title, Exact = true, IncludeHidden = true });

  private static ILocator Button(IPage page, string name) =>
    Confirmation(page).GetByRole(AriaRole.Button, new() { Name = name, Exact = true });

  /// <summary>La valeur que la confirmation montre sous le libellé <paramref name="label"/>.</summary>
  private static ILocator Fact(ILocator dialog, string label) =>
    dialog.Locator("dt").Filter(new() { HasTextRegex = new Regex($"^{Regex.Escape(label)}$") }).Locator("xpath=following-sibling::dd[1]");
}
