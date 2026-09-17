using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.Web.Pages.Requests;

namespace MicroserviceRgpd.BrowserTests.Requests;

/// <summary>
/// <b>La flèche d'horloge « Prolonger le délai de réponse », dans un vrai navigateur</b> : active sur
/// une demande qui se prolonge, éteinte sinon, où son infobulle dit le premier motif de blocage
/// (ADR-0029). Active comme éteinte, son clic ouvre la confirmation — qui, éteinte, dit le motif et
/// refuse de prolonger. Ce que la confirmation fait ensuite appartient à
/// <see cref="RequestExtensionDialog"/>.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Éteinte, elle porte <c>aria-disabled</c>, jamais <c>disabled</c></b>, comme l'avion en
/// papier : son infobulle se montre au survol et au focus, qu'un bouton désactivé ne reçoit pas.
/// </para>
/// <para>
/// ⚠️ <b>Elle se comporte exactement comme l'exécution</b> — voir <see cref="ExecutionButton"/> : deux
/// boutons d'apparence identique ne font pas deux choses différentes sous le doigt.
/// </para>
/// <para>
/// Les textes attendus se lisent sur les constantes du serveur — <see cref="RequestRow.ExtensionOffered"/>,
/// <see cref="ExtensionBlock"/> et <see cref="ExtensionConfirmation"/>.
/// </para>
/// </remarks>
[Collection(BrowserCollection.Name)]
public class ExtensionButton(BrowserHarness harness)
{
  /// <summary>
  /// <b>Sur une demande prolongeable, la flèche est active</b> : pas d'<c>aria-disabled</c>, et son
  /// infobulle dit son nom au survol.
  /// </summary>
  [Fact]
  public async Task OffersTheExtensionOfAnExtendableRequest()
  {
    await using var context = await harness.NewContextAsync();
    var row = await RowOfARequestAsync(await context.NewPageAsync(), block: null);
    var button = ExtensionOf(row);

    await Expect(button).Not.ToHaveAttributeAsync("aria-disabled", "true");

    await button.HoverAsync();

    await Expect(button.GetByText(RequestRow.ExtensionOffered, new() { Exact = true })).ToBeVisibleAsync();
  }

  /// <summary>
  /// <b>Sur une demande qui ne se prolonge pas, la flèche est éteinte et dit pourquoi</b> : le motif,
  /// caché tant qu'on ne le survole pas.
  /// </summary>
  [Theory]
  [InlineData(nameof(ExtensionBlock.Closed))]
  [InlineData(nameof(ExtensionBlock.DeadlineElapsed))]
  public async Task DimsTheExtensionAndSaysTheBlockOnHover(string blockName)
  {
    var block = ExtensionBlock.FromName(blockName);

    await using var context = await harness.NewContextAsync();
    var row = await RowOfARequestAsync(await context.NewPageAsync(), block);
    var button = ExtensionOf(row);

    // ⚠️ L'infobulle se lit SUR LA FLÈCHE, et non dans la ligne : « Demande close » est aussi le motif
    // que l'avion en papier affiche, et deux infobulles de même texte y cohabitent.
    var explanation = button.GetByText(block.FrenchLabel, new() { Exact = true });

    await Expect(button).ToHaveAttributeAsync("aria-disabled", "true");
    await Expect(explanation).ToBeHiddenAsync();

    await button.HoverAsync();

    await Expect(explanation).ToBeVisibleAsync();
  }

  /// <summary>
  /// ⚠️ <b>Le motif s'atteint au clavier</b> : la tabulation passe du crayon à la flèche éteinte, dont
  /// le focus visible montre l'infobulle — un bouton <c>disabled</c> ne recevrait pas ce focus.
  /// </summary>
  [Fact]
  public async Task ShowsTheBlockToTheKeyboardAlone()
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var row = await RowOfARequestAsync(page, ExtensionBlock.DeadlineElapsed);
    var explanation = ExtensionOf(row).GetByText(ExtensionBlock.DeadlineElapsed.FrenchLabel, new() { Exact = true });

    await row.GetByRole(AriaRole.Button, new() { Name = "Modifier la demande", Exact = true }).FocusAsync();
    await page.Keyboard.PressAsync("Tab");

    await Expect(ExtensionOf(row)).ToBeFocusedAsync();
    await Expect(explanation).ToBeVisibleAsync();
  }

  /// <summary>
  /// ⚠️ <b>Le clic sur la flèche éteinte ouvre la confirmation, qui dit le motif et refuse de
  /// prolonger</b> : le doigt et le lecteur d'écran, qui ne survolent pas, atteignent ainsi le motif
  /// que l'infobulle réserve à la souris. Le clic est forcé : Playwright tient un
  /// <c>aria-disabled</c> pour inatteignable, là où le navigateur délivre le clic.
  /// </summary>
  [Fact]
  public async Task OpensTheConfirmationSayingTheBlockWhenDimmed()
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var row = await RowOfARequestAsync(page, ExtensionBlock.Closed);

    var written = 0;
    page.Request += (_, request) =>
    {
      if (request.Url.Contains("handler=Extend", StringComparison.Ordinal))
      {
        Interlocked.Increment(ref written);
      }
    };

    await ExtensionOf(row).ClickAsync(new() { Force = true });

    var dialog = page.GetByRole(AriaRole.Dialog, new() { Name = ExtensionConfirmation.Title, Exact = true });

    await Expect(dialog).ToBeVisibleAsync();
    await Expect(dialog.GetByText(ExtensionBlock.Closed.FrenchLabel, new() { Exact = true })).ToBeVisibleAsync();
    await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = ExtensionConfirmation.Confirm, Exact = true }))
      .ToBeDisabledAsync();
    await Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/demandes$"));
    written.ShouldBe(0, "Le clic sur la prolongation éteinte a prolongé la demande.");
  }

  /// <summary>
  /// La flèche d'horloge de cette ligne. ⚠️ <b>Son nom accessible ne change pas</b>, éteinte comme
  /// active : c'est son infobulle qui dit le motif — et c'est ce nom qui la retrouve ici.
  /// </summary>
  private static ILocator ExtensionOf(ILocator row) =>
    row.GetByRole(AriaRole.Button, new() { Name = RequestRow.ExtensionOffered, Exact = true });

  /// <summary>
  /// Enregistre une demande au droit d'accès, avec un email unique, la met dans l'état qui produit
  /// <paramref name="block"/> — ou, sans motif, dans un état prolongeable —, ouvre le tableau et rend
  /// sa ligne.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Les dates sont posées par rapport à aujourd'hui</b>, jamais en dur : la fenêtre de
  /// prolongation se juge sur le jour courant, qu'aucune horloge de test ne fige ici.
  /// </remarks>
  private async Task<ILocator> RowOfARequestAsync(IPage page, ExtensionBlock? block)
  {
    var email = $"{Guid.NewGuid():N}@example.org";
    var today = ParisCalendar.Today(TimeProvider.System);
    var message = await harness.RecordRequestAsync(email: email, right: nameof(DataSubjectRight.Access));

    await harness.SetResponseDeadlineAsync(
      message,
      block == ExtensionBlock.DeadlineElapsed ? today.AddDays(-1) : today.AddMonths(1));

    if (block == ExtensionBlock.Closed)
    {
      await harness.SetStatusAsync(message, RequestStatus.Completed);
    }

    await page.GotoAsync("/demandes");

    var row = page.GetByRole(AriaRole.Row).Filter(new() { HasText = email });
    await Expect(row).ToHaveCountAsync(1);

    return row;
  }
}
