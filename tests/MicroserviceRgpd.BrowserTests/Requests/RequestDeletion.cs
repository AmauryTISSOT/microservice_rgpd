using System.Text.RegularExpressions;

namespace MicroserviceRgpd.BrowserTests.Requests;

/// <summary>
/// <b>L'<c>Operator</c> supprime une demande depuis la poubelle de sa ligne</b>, dans un vrai
/// navigateur : la confirmation s'ouvre avec la phrase de la ligne ; « Annuler », Échap et un clic
/// en dehors la ferment sans rien supprimer ; « Supprimer définitivement » la ferme, retire la ligne
/// sans rechargement, et le toast « Demande supprimée » le dit. Un échec la ferme aussi, mais la
/// ligne reste, et le toast dit « La suppression a échoué, veuillez réessayer. ».
/// </summary>
/// <remarks>
/// <para>
/// Chaque test enregistre sa propre demande <b>par le use case</b>, reconnaissable à son email
/// unique, et relit la base par son message unique : la base est partagée par toute la collection.
/// </para>
/// <para>
/// ⚠️ <b>Les échecs que le vrai service ne donne pas sur commande</b> — une erreur serveur, une
/// coupure réseau — <b>sont simulés</b> en interceptant l'envoi. Le reste atteint le vrai handler.
/// </para>
/// </remarks>
[Collection(BrowserCollection.Name)]
public class RequestDeletion(BrowserHarness harness)
{
  private const string ConfirmationTitle = "Supprimer la demande";

  private const string Deleted = "Demande supprimée";

  private const string Failure = "La suppression a échoué, veuillez réessayer.";

  private static readonly Regex DeleteHandler = new(@"/demandes\?handler=Delete$");

  /// <summary>Les trois façons de fermer la confirmation sans rien supprimer.</summary>
  public static TheoryData<string> ClosingModes { get; } = ["Annuler", "Échap", "le fond"];

  /// <summary>
  /// <b>La poubelle ouvre la confirmation avec la phrase de sa ligne</b> — celle que le serveur a
  /// composée pour cette demande —, et « Annuler » a le focus : une touche Entrée réflexe ne
  /// supprime rien.
  /// </summary>
  [Fact]
  public async Task OpensTheConfirmationWithTheSentenceOfTheRow()
  {
    await using var context = await harness.NewContextAsync();
    var email = UniqueEmail();
    await harness.RecordRequestAsync("Martin", "Jeanne", email);
    var page = await OnTheBoardAsync(context);

    await TrashOf(page, email).ClickAsync();

    await Expect(Confirmation(page)).ToBeVisibleAsync();
    await Expect(Confirmation(page)).ToHaveAccessibleDescriptionAsync(
      $"La demande de Jeanne Martin ({email}) sera définitivement supprimée. Cette action est irréversible.");
    await Expect(ConfirmationButton(page, "Annuler")).ToBeFocusedAsync();
  }

  /// <summary>
  /// <b>Chaque poubelle ouvre la confirmation de sa propre ligne</b> : la phrase de la précédente ne
  /// reste pas.
  /// </summary>
  [Fact]
  public async Task OpensTheConfirmationOfTheRowWhoseTrashWasClicked()
  {
    await using var context = await harness.NewContextAsync();
    var first = UniqueEmail();
    var second = UniqueEmail();
    await harness.RecordRequestAsync("", "", first);
    await harness.RecordRequestAsync("", "", second);
    var page = await OnTheBoardAsync(context);

    await TrashOf(page, first).ClickAsync();
    await ConfirmationButton(page, "Annuler").ClickAsync();
    await TrashOf(page, second).ClickAsync();

    await Expect(Confirmation(page)).ToHaveAccessibleDescriptionAsync(
      $"La demande de {second} sera définitivement supprimée. Cette action est irréversible.");
  }

  /// <summary>
  /// <b>« Annuler », Échap ou un clic en dehors ferment la confirmation sans rien supprimer</b> :
  /// aucune requête ne part, la ligne reste, la demande est en base.
  /// </summary>
  [Theory]
  [MemberData(nameof(ClosingModes))]
  public async Task ClosesWithoutDeletingAnything(string mode)
  {
    await using var context = await harness.NewContextAsync();
    var email = UniqueEmail();
    var message = await harness.RecordRequestAsync("Martin", "Jeanne", email);
    var page = await OnTheBoardAsync(context);
    await TrashOf(page, email).ClickAsync();
    await Expect(Confirmation(page)).ToBeVisibleAsync();

    var sent = new List<string>();
    page.Request += (_, request) => sent.Add($"{request.Method} {request.Url}");

    await CloseByAsync(page, mode);

    await Expect(Confirmation(page)).ToBeHiddenAsync();
    await Expect(RowOf(page, email)).ToHaveCountAsync(1);
    sent.ShouldBeEmpty("La fermeture a appelé le service.");
    (await harness.CountOfRequestsAsync(message)).ShouldBe(1);
  }

  /// <summary>
  /// <b>« Supprimer définitivement » supprime la demande</b> : la confirmation se ferme, la ligne
  /// est retirée sans rechargement, le toast « Demande supprimée » le dit — et après rechargement,
  /// la demande n'apparaît plus.
  /// </summary>
  [Fact]
  public async Task DeletesTheRequestForGood()
  {
    await using var context = await harness.NewContextAsync();
    var email = UniqueEmail();
    var message = await harness.RecordRequestAsync("Martin", "Jeanne", email);
    var page = await OnTheBoardAsync(context);

    // Un marqueur posé sur la fenêtre : un rechargement l'effacerait.
    await page.EvaluateAsync("() => { window.untouched = true; }");

    await TrashOf(page, email).ClickAsync();
    await ConfirmationButton(page, "Supprimer définitivement").ClickAsync();

    await Expect(Confirmation(page)).ToBeHiddenAsync();
    await Expect(RowOf(page, email)).ToHaveCountAsync(0);
    await Expect(Toast(page)).ToBeVisibleAsync();
    (await page.EvaluateAsync<bool>("() => window.untouched === true")).ShouldBeTrue("La suppression a rechargé la page.");
    (await harness.CountOfRequestsAsync(message)).ShouldBe(0);

    await page.ReloadAsync();

    await Expect(page.GetByRole(AriaRole.Table)).ToBeVisibleAsync();
    await Expect(RowOf(page, email)).ToHaveCountAsync(0);
  }

  /// <summary>
  /// <b>Une demande déjà supprimée ailleurs se lit comme une réussite</b> (ADR-0022) : ce que
  /// l'<c>Operator</c> voulait est acquis, et la ligne s'en va au lieu de rester impossible à
  /// supprimer.
  /// </summary>
  [Fact]
  public async Task RemovesTheRowOfARequestAlreadyDeletedElsewhere()
  {
    await using var context = await harness.NewContextAsync();
    var email = UniqueEmail();
    var message = await harness.RecordRequestAsync("Martin", "Jeanne", email);
    var page = await OnTheBoardAsync(context);
    await harness.DeleteRequestAsync(message);

    await TrashOf(page, email).ClickAsync();
    await ConfirmationButton(page, "Supprimer définitivement").ClickAsync();

    await Expect(Confirmation(page)).ToBeHiddenAsync();
    await Expect(RowOf(page, email)).ToHaveCountAsync(0);
    await Expect(Toast(page)).ToBeVisibleAsync();
  }

  /// <summary>
  /// <b>La dernière ligne supprimée, l'état vide paraît</b> — sans revenir au serveur pour en
  /// connaître les mots.
  /// </summary>
  [Fact]
  public async Task ShowsTheEmptyStateOnceTheLastRowIsDeleted()
  {
    await using var context = await harness.NewContextAsync();
    await harness.DeleteAllRequestsAsync();
    var email = UniqueEmail();
    await harness.RecordRequestAsync("Martin", "Jeanne", email);
    var page = await OnTheBoardAsync(context);
    var none = page.GetByText("Aucune demande pour le moment", new() { Exact = true });
    await Expect(none).ToBeHiddenAsync();

    await TrashOf(page, email).ClickAsync();
    await ConfirmationButton(page, "Supprimer définitivement").ClickAsync();

    await Expect(RowOf(page, email)).ToHaveCountAsync(0);
    await Expect(none).ToBeVisibleAsync();
  }

  /// <summary>Les échecs de l'envoi.</summary>
  public static TheoryData<string> Failures { get; } = ["une erreur serveur", "une coupure réseau", "un jeton anti-rejeu périmé"];

  /// <summary>
  /// <b>Un échec ferme la confirmation, et le toast le dit</b> ; la ligne reste en place, et rien
  /// n'est supprimé. Le jeton périmé n'est pas simulé : le cookie qui le porte est effacé, et c'est
  /// le vrai service qui refuse.
  /// </summary>
  [Theory]
  [MemberData(nameof(Failures))]
  public async Task ShowsTheFailureToastAndKeepsTheRowOnAFailure(string failure)
  {
    await using var context = await harness.NewContextAsync();
    var email = UniqueEmail();
    var message = await harness.RecordRequestAsync("Martin", "Jeanne", email);
    var page = await OnTheBoardAsync(context);

    switch (failure)
    {
      case "une erreur serveur":
        await page.RouteAsync(DeleteHandler, route => route.FulfillAsync(new() { Status = 500 }));
        break;
      case "une coupure réseau":
        await page.RouteAsync(DeleteHandler, route => route.AbortAsync("internetdisconnected"));
        break;
      default:
        await context.ClearCookiesAsync();
        break;
    }

    await TrashOf(page, email).ClickAsync();
    await ConfirmationButton(page, "Supprimer définitivement").ClickAsync();

    await Expect(Confirmation(page)).ToBeHiddenAsync();
    await Expect(Toast(page, Failure)).ToBeVisibleAsync();
    await Expect(RowOf(page, email)).ToHaveCountAsync(1);
    (await harness.CountOfRequestsAsync(message)).ShouldBe(1);
  }

  /// <summary>
  /// <b>Après un échec, l'<c>Operator</c> réessaie depuis la même poubelle</b> : la confirmation
  /// rouverte envoie de nouveau, et cette fois la ligne s'en va.
  /// </summary>
  [Fact]
  public async Task DeletesOnARetryAfterAFailure()
  {
    await using var context = await harness.NewContextAsync();
    var email = UniqueEmail();
    var message = await harness.RecordRequestAsync("Martin", "Jeanne", email);
    var page = await OnTheBoardAsync(context);
    await page.RouteAsync(DeleteHandler, route => route.FulfillAsync(new() { Status = 500 }));

    await TrashOf(page, email).ClickAsync();
    await ConfirmationButton(page, "Supprimer définitivement").ClickAsync();
    await Expect(Toast(page, Failure)).ToBeVisibleAsync();
    await page.UnrouteAsync(DeleteHandler);

    await TrashOf(page, email).ClickAsync();
    await ConfirmationButton(page, "Supprimer définitivement").ClickAsync();

    await Expect(Confirmation(page)).ToBeHiddenAsync();
    await Expect(RowOf(page, email)).ToHaveCountAsync(0);
    await Expect(Toast(page)).ToBeVisibleAsync();
    (await harness.CountOfRequestsAsync(message)).ShouldBe(0);
  }

  /// <summary>
  /// Ferme la confirmation par l'un des trois modes. Le clic sur le fond tombe <b>dans le coin de la
  /// fenêtre</b>, loin de la confirmation centrée.
  /// </summary>
  private static Task CloseByAsync(IPage page, string mode)
  {
    return mode switch
    {
      "Annuler" => ConfirmationButton(page, "Annuler").ClickAsync(),
      "Échap" => page.Keyboard.PressAsync("Escape"),
      "le fond" => page.Mouse.ClickAsync(5, 5),
      _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Mode de fermeture inconnu."),
    };
  }

  private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

  private static async Task<IPage> OnTheBoardAsync(IBrowserContext context)
  {
    var page = await context.NewPageAsync();
    await page.GotoAsync("/demandes");

    return page;
  }

  private static ILocator RowOf(IPage page, string email)
  {
    return page.GetByRole(AriaRole.Row).Filter(new() { HasText = email });
  }

  private static ILocator TrashOf(IPage page, string email)
  {
    return RowOf(page, email).GetByRole(AriaRole.Button, new() { Name = "Supprimer la demande", Exact = true });
  }

  private static ILocator Confirmation(IPage page)
  {
    return page.GetByRole(AriaRole.Alertdialog, new() { Name = ConfirmationTitle, Exact = true, IncludeHidden = true });
  }

  private static ILocator ConfirmationButton(IPage page, string name)
  {
    return Confirmation(page).GetByRole(AriaRole.Button, new() { Name = name, Exact = true });
  }

  private static ILocator Toast(IPage page, string words = Deleted)
  {
    return page.GetByRole(AriaRole.Status).And(page.GetByText(words, new() { Exact = true }));
  }
}
