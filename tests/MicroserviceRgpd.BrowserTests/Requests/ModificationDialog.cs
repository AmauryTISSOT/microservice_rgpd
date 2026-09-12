using MicroserviceRgpd.Core.Requests;

namespace MicroserviceRgpd.BrowserTests.Requests;

/// <summary>
/// <b>Le crayon d'une demande, dans un vrai navigateur</b> : offert tant que la demande est En
/// cours, éteint sur une demande close — Terminée ou Annulée —, où il dit <b>pourquoi</b>.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Le crayon éteint porte <c>aria-disabled</c>, jamais <c>disabled</c></b> : l'infobulle des
/// boutons de ligne se montre au survol et au focus visible, qu'un bouton réellement désactivé ne
/// reçoit ni l'un ni l'autre. L'explication exigée ne s'afficherait alors jamais — ni à la souris,
/// ni au clavier.
/// </para>
/// <para>
/// Les demandes se créent <b>par le use case</b>, et leur statut se pose <b>à même la table</b> :
/// aucun <c>Gesture</c> ne sait encore clore une demande. Chacune se retrouve par son email unique,
/// la base étant partagée par toute la collection.
/// </para>
/// <para>
/// Ce que le crayon ouvre — la modale de modification et son formulaire — viendra ici même, avec
/// l'US qui le branche. Ce que le serveur rend se garde dans <c>RequestConsultation</c>.
/// </para>
/// </remarks>
[Collection(BrowserCollection.Name)]
public class ModificationDialog(BrowserHarness harness)
{
  /// <summary>Le nom accessible du crayon, le même quel que soit le statut de la demande.</summary>
  private const string Pencil = "Modifier la demande";

  /// <summary>Ce que le crayon éteint donne à lire, recopié à dessein.</summary>
  private const string Refusal = "Une demande close ne peut plus être modifiée";

  /// <summary>
  /// <b>Sur une demande En cours, le crayon est offert</b> : il ne porte pas <c>aria-disabled</c>, et
  /// son infobulle dit son nom, non un refus.
  /// </summary>
  [Fact]
  public async Task LeavesThePencilOfferedOnARequestInProgress()
  {
    await using var context = await harness.NewContextAsync();
    var row = await RowOfARequestAsync(context, status: null);
    var pencil = row.GetByRole(AriaRole.Button, new() { Name = Pencil, Exact = true });

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
    var pencil = row.GetByRole(AriaRole.Button, new() { Name = Pencil, Exact = true });
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
    var pencil = row.GetByRole(AriaRole.Button, new() { Name = Pencil, Exact = true });
    var explanation = row.GetByText(Refusal, new() { Exact = true });

    await row.GetByRole(AriaRole.Button, new() { Name = "Supprimer la demande", Exact = true }).FocusAsync();

    await Expect(explanation).ToBeHiddenAsync();

    await page.Keyboard.PressAsync("Tab");

    await Expect(pencil).ToBeFocusedAsync();
    await Expect(explanation).ToBeVisibleAsync();
  }

  private async Task<ILocator> RowOfARequestAsync(IBrowserContext context, RequestStatus? status)
  {
    return await RowOfARequestAsync(await context.NewPageAsync(), status);
  }

  /// <summary>
  /// Enregistre une demande, lui pose ce statut quand il en faut un autre qu'En cours, ouvre le
  /// tableau et rend sa ligne — celle qui porte son email unique.
  /// </summary>
  private async Task<ILocator> RowOfARequestAsync(IPage page, RequestStatus? status)
  {
    var email = $"{Guid.NewGuid():N}@example.org";
    var message = await harness.RecordRequestAsync(email: email);

    if (status is not null)
    {
      await harness.SetStatusAsync(message, status);
    }

    await page.GotoAsync("/demandes");

    var row = page.GetByRole(AriaRole.Row).Filter(new() { HasText = email });
    await Expect(row).ToHaveCountAsync(1);

    return row;
  }
}
