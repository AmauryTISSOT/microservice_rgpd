using System.Globalization;
using MicroserviceRgpd.Core.Requests;

namespace MicroserviceRgpd.BrowserTests.Requests;

/// <summary>
/// <b>La date limite signalée, dans un vrai navigateur</b> : en orange avec « Échéance proche », en
/// rouge et en gras avec « En retard ». Ce que le serveur rend — quelle demande est signalée, et
/// comment — se garde dans <c>RequestConsultation</c> ; ici, ce que la feuille de style en fait.
/// </summary>
/// <remarks>
/// Le service tourne sur l'heure réelle : la date limite est posée à trois jours d'aujourd'hui à
/// Paris, de part ou d'autre — assez loin des bornes pour qu'un minuit franchi pendant le test n'en
/// change pas le signalement.
/// </remarks>
[Collection(BrowserCollection.Name)]
public class DeadlineSignals(BrowserHarness harness)
{
  /// <summary>L'orange profond qui se lit en texte, recopié à dessein.</summary>
  private const string Orange = "rgb(121, 52, 0)";

  /// <summary>Le rouge du signalement, recopié à dessein.</summary>
  private const string Red = "rgb(179, 38, 30)";

  [Theory]
  [InlineData(3, "Échéance proche", Orange, "400")]
  [InlineData(-3, "En retard", Red, "700")]
  public async Task ShowsTheDeadlineInItsColourWithItsMention(int daysFromToday, string mention, string colour, string weight)
  {
    var email = $"{Guid.NewGuid():N}@example.org";
    var message = await harness.RecordRequestAsync(email: email);
    var deadline = ParisCalendar.Today(TimeProvider.System).AddDays(daysFromToday);

    await harness.SetResponseDeadlineAsync(message, deadline);

    await using var context = await harness.NewContextAsync();
    var page = await context.NewPageAsync();
    await page.GotoAsync("/demandes");

    var row = page.GetByRole(AriaRole.Row).Filter(new() { HasText = email });
    var cell = row.GetByRole(AriaRole.Cell).Nth(4);

    await Expect(cell).ToHaveTextAsync($"{deadline.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)} {mention}");
    await Expect(cell.GetByText(mention, new() { Exact = true })).ToBeVisibleAsync();
    await Expect(cell).ToHaveCSSAsync("color", colour);
    await Expect(cell).ToHaveCSSAsync("font-weight", weight);
    await Expect(row.GetByRole(AriaRole.Cell).Nth(3)).Not.ToHaveCSSAsync("color", colour);
  }
}
