using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.Web.Pages.Requests;

namespace MicroserviceRgpd.BrowserTests.Requests;

/// <summary>
/// <b>Le bouton « Exécuter la demande », dans un vrai navigateur</b> : actif sur une demande qui
/// s'exécute, éteint sinon, où son infobulle dit le premier motif de blocage (ADR-0026). Actif comme
/// éteint, son clic ouvre la confirmation — qui, éteint, dit le motif et refuse d'exécuter. Ce que la
/// confirmation fait ensuite appartient à <see cref="RequestExecution"/>.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Éteint, il porte <c>aria-disabled</c>, jamais <c>disabled</c></b>, comme le crayon : son
/// infobulle se montre au survol et au focus, qu'un bouton désactivé ne reçoit pas.
/// </para>
/// <para>
/// Les textes attendus se lisent sur les constantes du serveur — <see cref="RequestRow.ExecutionOffered"/>
/// et <see cref="ExecutionBlock"/>. Le Paramétrage est un singleton partagé par la collection : chaque
/// test part d'un service vierge et y revient.
/// </para>
/// </remarks>
[Collection(BrowserCollection.Name)]
public class ExecutionButton(BrowserHarness harness) : IAsyncLifetime
{
  public Task InitializeAsync() => harness.ForgetEveryEndpointAsync();

  public Task DisposeAsync() => harness.ForgetEveryEndpointAsync();

  /// <summary>
  /// <b>Sur une demande exécutable, le bouton est actif</b> : pas d'<c>aria-disabled</c>, et son
  /// infobulle dit son nom au survol.
  /// </summary>
  [Fact]
  public async Task OffersTheExecutionOfAnExecutableRequest()
  {
    await harness.ConfigureEndpointAsync(DataSubjectRight.Access);
    await using var context = await harness.NewContextAsync();
    var row = await RowOfARequestAsync(await context.NewPageAsync(), identityVerified: true);
    var button = ExecutionOf(row);

    await Expect(button).Not.ToHaveAttributeAsync("aria-disabled", "true");

    await button.HoverAsync();

    await Expect(row.GetByText(RequestRow.ExecutionOffered, new() { Exact = true })).ToBeVisibleAsync();
  }

  /// <summary>
  /// <b>Sur une demande qui ne s'exécute pas, le bouton est éteint et dit pourquoi</b> : le motif,
  /// caché tant qu'on ne le survole pas.
  /// </summary>
  [Theory]
  [InlineData(nameof(ExecutionBlock.IdentityNotVerified))]
  [InlineData(nameof(ExecutionBlock.RightNotConfigured))]
  public async Task DimsTheExecutionAndSaysTheBlockOnHover(string blockName)
  {
    var block = ExecutionBlock.FromName(blockName);

    if (block != ExecutionBlock.RightNotConfigured)
    {
      await harness.ConfigureEndpointAsync(DataSubjectRight.Access);
    }

    await using var context = await harness.NewContextAsync();
    var row = await RowOfARequestAsync(await context.NewPageAsync(), identityVerified: block != ExecutionBlock.IdentityNotVerified);
    var button = ExecutionOf(row);
    var explanation = row.GetByText(block.FrenchLabelFor(DataSubjectRight.Access), new() { Exact = true });

    await Expect(button).ToHaveAttributeAsync("aria-disabled", "true");
    await Expect(explanation).ToBeHiddenAsync();

    await button.HoverAsync();

    await Expect(explanation).ToBeVisibleAsync();
  }

  /// <summary>
  /// ⚠️ <b>Le motif s'atteint au clavier</b> : la tabulation passe de l'œil au bouton éteint, dont le
  /// focus visible montre l'infobulle — un bouton <c>disabled</c> ne recevrait pas ce focus.
  /// </summary>
  [Fact]
  public async Task ShowsTheBlockToTheKeyboardAlone()
  {
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var row = await RowOfARequestAsync(page, identityVerified: false);
    var explanation = row.GetByText(ExecutionBlock.IdentityNotVerified.FrenchLabelFor(DataSubjectRight.Access), new() { Exact = true });

    await row.GetByRole(AriaRole.Button, new() { Name = "Voir la fiche de la demande", Exact = true }).FocusAsync();
    await page.Keyboard.PressAsync("Tab");

    await Expect(ExecutionOf(row)).ToBeFocusedAsync();
    await Expect(explanation).ToBeVisibleAsync();
  }

  /// <summary>
  /// ⚠️ <b>Le clic sur le bouton éteint ouvre la confirmation, qui dit le motif et refuse d'exécuter</b> :
  /// le doigt et le lecteur d'écran, qui ne survolent pas, atteignent ainsi le motif que l'infobulle
  /// réserve à la souris. Le clic est forcé : Playwright tient un <c>aria-disabled</c> pour
  /// inatteignable, là où le navigateur délivre le clic.
  /// </summary>
  /// <remarks>
  /// La modale ne peut rien faire : « Exécuter » y est <c>disabled</c>, et aucune écriture n'est
  /// appelée. La flèche d'horloge se comporte à l'identique — voir <see cref="ExtensionButton"/>.
  /// </remarks>
  [Fact]
  public async Task OpensTheConfirmationSayingTheBlockWhenDimmed()
  {
    await harness.ConfigureEndpointAsync(DataSubjectRight.Access);
    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var row = await RowOfARequestAsync(page, identityVerified: false);

    var written = 0;
    page.Request += (_, request) =>
    {
      if (request.Url.Contains("handler=Execute", StringComparison.Ordinal))
      {
        Interlocked.Increment(ref written);
      }
    };

    await ExecutionOf(row).ClickAsync(new() { Force = true });

    var dialog = page.GetByRole(AriaRole.Alertdialog, new() { Name = ExecutionConfirmation.Title, Exact = true });

    await Expect(dialog).ToBeVisibleAsync();
    await Expect(dialog.GetByText(
      ExecutionBlock.IdentityNotVerified.FrenchLabelFor(DataSubjectRight.Access),
      new() { Exact = true })).ToBeVisibleAsync();
    await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = ExecutionConfirmation.Confirm, Exact = true }))
      .ToBeDisabledAsync();
    await Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/demandes$"));
    written.ShouldBe(0, "Le clic sur l'exécution éteinte a exécuté la demande.");
  }

  private static ILocator ExecutionOf(ILocator row) =>
    row.GetByRole(AriaRole.Button, new() { Name = RequestRow.ExecutionOffered, Exact = true });

  /// <summary>
  /// Enregistre une demande au droit d'accès, avec un email unique, ouvre le tableau et rend sa ligne.
  /// </summary>
  private async Task<ILocator> RowOfARequestAsync(IPage page, bool identityVerified)
  {
    var email = $"{Guid.NewGuid():N}@example.org";

    await harness.RecordRequestAsync(email: email, identityVerified: identityVerified, right: nameof(DataSubjectRight.Access));
    await page.GotoAsync("/demandes");

    var row = page.GetByRole(AriaRole.Row).Filter(new() { HasText = email });
    await Expect(row).ToHaveCountAsync(1);

    return row;
  }
}
