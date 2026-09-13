using System.Diagnostics;
using MicroserviceRgpd.Core.Qualifications;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.BrowserTests.Qualifications;

/// <summary>
/// <b>Le service lancé par le harnais qualifie avec les doublures du harnais</b>, et avec rien
/// d'autre : ce qu'un test dicte aux moteurs, l'écran de qualification le rend dans Chromium.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Ce ne sont pas des tests de l'écran de qualification</b> — les tests fonctionnels le
/// couvrent. Ils éprouvent le harnais : chaque réponse qu'un scénario navigateur peut avoir à dicter
/// (un droit, plusieurs, <c>OutOfScope</c>, un signal de relecture, le mode dégradé, le silence des
/// moteurs, la lenteur) atteint bien l'écran par le service sous test.
/// </para>
/// <para>
/// Les phrases de l'écran sont recopiées à dessein, comme ailleurs dans ce projet.
/// </para>
/// </remarks>
[Collection(BrowserCollection.Name)]
public class QualificationEngines
{
  private readonly BrowserHarness _harness;

  public QualificationEngines(BrowserHarness harness)
  {
    _harness = harness;

    harness.Verdict.Reset();
    harness.Lexicon.Reset();
  }

  /// <summary>
  /// <b>Le droit dicté paraît, avec la justification dictée</b>, et le texte saisi est celui que la
  /// doublure a reçu : c'est bien elle, et non un moteur réel, que le service a interrogée.
  /// </summary>
  [Fact]
  public async Task RendersTheRightAndTheJustificationTheDoublesDictate()
  {
    Dictate(DataSubjectRight.Erasure);
    _harness.Verdict.Justification = "La doublure du harnais réclame l'effacement.";
    await using var context = await _harness.NewContextAsync();
    var page = await context.NewPageAsync();

    await QualifyAsync(page, "Bonjour, je vous écris au sujet de mon compte.");

    await Expect(Verdict(page)).ToContainTextAsync("droit à l'effacement");
    await Expect(Verdict(page)).ToContainTextAsync("La doublure du harnais réclame l'effacement.");
    _harness.Verdict.ReceivedText.ShouldNotBeNull().Value.ShouldBe("Bonjour, je vous écris au sujet de mon compte.");
    _harness.Lexicon.CallCount.ShouldBe(1);
  }

  [Fact]
  public async Task RendersEveryRightWhenTheDoublesDictateSeveral()
  {
    Dictate(DataSubjectRight.Access, DataSubjectRight.Erasure);
    await using var context = await _harness.NewContextAsync();
    var page = await context.NewPageAsync();

    await QualifyAsync(page, "Une copie, puis l'effacement.");

    await Expect(Verdict(page)).ToContainTextAsync("droit d'accès, droit à l'effacement");
  }

  [Fact]
  public async Task RendersOutOfScopeWhenTheDoublesRecogniseNoRight()
  {
    _harness.Verdict.Qualification = Qualification.OutOfScope;
    _harness.Lexicon.Qualification = Qualification.OutOfScope;
    await using var context = await _harness.NewContextAsync();
    var page = await context.NewPageAsync();

    await QualifyAsync(page, "Quelles sont vos heures d'ouverture ?");

    await Expect(Verdict(page)).ToContainTextAsync("aucun droit reconnu");
  }

  /// <summary><b>Deux avis qui divergent donnent le signal de relecture « contestée ».</b></summary>
  [Fact]
  public async Task RendersTheReviewSignalTheDoublesProvoke()
  {
    _harness.Verdict.Qualification = Qualification.Of([DataSubjectRight.Erasure]);
    _harness.Lexicon.Qualification = Qualification.Of([DataSubjectRight.Access]);
    await using var context = await _harness.NewContextAsync();
    var page = await context.NewPageAsync();

    await QualifyAsync(page, "Supprimez, ou montrez-moi, je ne sais plus.");

    await Expect(Verdict(page)).ToContainTextAsync("Contestée");
  }

  /// <summary><b>Le moteur de verdict muet laisse le service non entier</b>, sans le bloquer.</summary>
  [Fact]
  public async Task RendersTheDegradedModeWhenTheVerdictDoubleFallsSilent()
  {
    Dictate(DataSubjectRight.Erasure);
    _harness.Verdict.Silence = new HttpRequestException("La doublure se tait.");
    await using var context = await _harness.NewContextAsync();
    var page = await context.NewPageAsync();

    await QualifyAsync(page, "Supprimez toutes mes données.");

    await Expect(Verdict(page)).ToContainTextAsync("Service non entier");
    await Expect(Verdict(page)).ToContainTextAsync("droit à l'effacement");
  }

  [Fact]
  public async Task SaysNothingCouldBeQualifiedWhenBothDoublesFallSilent()
  {
    _harness.Verdict.Silence = new HttpRequestException("La doublure de verdict se tait.");
    _harness.Lexicon.Silence = new HttpRequestException("La doublure du lexique se tait.");
    await using var context = await _harness.NewContextAsync();
    var page = await context.NewPageAsync();

    await QualifyAsync(page, "Supprimez toutes mes données.");

    await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Le service ne peut rien qualifier pour l'instant" }))
      .ToBeVisibleAsync();
  }

  /// <summary>
  /// <b>Une doublure lente fait attendre l'écran</b> : le verdict ne paraît pas avant que la doublure
  /// ait fini de faire durer sa réponse.
  /// </summary>
  /// <remarks>
  /// La durée se mesure plutôt que l'absence du verdict ne se guette : Playwright attend la fin de la
  /// navigation du formulaire avant de relire l'écran, et n'y verrait jamais l'attente.
  /// </remarks>
  [Fact]
  public async Task KeepsTheScreenWaitingWhileTheDoublesAreSlow()
  {
    Dictate(DataSubjectRight.Erasure);
    var delay = TimeSpan.FromSeconds(2);
    _harness.Verdict.Delay = delay;
    await using var context = await _harness.NewContextAsync();
    var page = await context.NewPageAsync();
    var waited = Stopwatch.StartNew();

    await QualifyAsync(page, "Supprimez toutes mes données.");

    await Expect(Verdict(page)).ToContainTextAsync("droit à l'effacement", new() { Timeout = 10_000 });
    waited.Elapsed.ShouldBeGreaterThanOrEqualTo(delay);
  }

  /// <summary>Les deux doublures s'accordent sur ces droits.</summary>
  private void Dictate(params DataSubjectRight[] rights)
  {
    _harness.Verdict.Qualification = Qualification.Of(rights);
    _harness.Lexicon.Qualification = Qualification.Of(rights);
  }

  private static async Task QualifyAsync(IPage page, string text)
  {
    await page.GotoAsync("/qualification");
    await page.GetByLabel("Le texte de la demande").FillAsync(text);
    await page.GetByRole(AriaRole.Button, new() { Name = "Qualifier", Exact = true }).ClickAsync();
  }

  /// <summary>La carte du verdict, sous son titre.</summary>
  private static ILocator Verdict(IPage page)
  {
    return page.Locator("div").Filter(new() { Has = page.GetByRole(AriaRole.Heading, new() { Name = "Le verdict", Exact = true }) }).Last;
  }
}
