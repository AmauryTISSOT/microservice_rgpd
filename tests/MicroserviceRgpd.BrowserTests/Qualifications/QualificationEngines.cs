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
[Collection(BrowserProposalCollection.Name)]
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

    await Expect(Screen(page)).ToContainTextAsync("droit à l'effacement");
    await Expect(Screen(page)).ToContainTextAsync("La doublure du harnais réclame l'effacement.");
    _harness.Verdict.ReceivedText.ShouldNotBeNull().Value.ShouldBe("Bonjour, je vous écris au sujet de mon compte.");
    _harness.Lexicon.CallCount.ShouldBe(1);
  }

  /// <summary><b>Plusieurs droits dictés paraissent tous</b>, dans l'ordre de la taxonomie.</summary>
  [Fact]
  public async Task RendersEveryRightWhenTheDoublesDictateSeveral()
  {
    Dictate(DataSubjectRight.Access, DataSubjectRight.Erasure);
    await using var context = await _harness.NewContextAsync();
    var page = await context.NewPageAsync();

    await QualifyAsync(page, "Une copie, puis l'effacement.");

    await Expect(Screen(page)).ToContainTextAsync("droit d'accès, droit à l'effacement");
  }

  /// <summary><b><c>OutOfScope</c> dicté paraît comme un verdict nommé.</b></summary>
  [Fact]
  public async Task RendersOutOfScopeWhenTheDoublesRecogniseNoRight()
  {
    _harness.Verdict.Qualification = Qualification.OutOfScope;
    _harness.Lexicon.Qualification = Qualification.OutOfScope;
    await using var context = await _harness.NewContextAsync();
    var page = await context.NewPageAsync();

    await QualifyAsync(page, "Quelles sont vos heures d'ouverture ?");

    await Expect(Screen(page)).ToContainTextAsync("aucun droit reconnu");
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

    await Expect(Screen(page)).ToContainTextAsync("Contestée");
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

    await Expect(Screen(page)).ToContainTextAsync("Service non entier");
    await Expect(Screen(page)).ToContainTextAsync("droit à l'effacement");
  }

  /// <summary><b>Les deux doublures muettes ne laissent rien à qualifier</b>, et l'écran le dit.</summary>
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
    _harness.Verdict.Delay = SlowAnswer;
    await using var context = await _harness.NewContextAsync();
    var page = await context.NewPageAsync();
    await EnterAsync(page, "Supprimez toutes mes données.");

    // Le chronomètre part au clic, et pas avant : le chargement de l'écran et la saisie ne comptent
    // pas dans l'attente.
    var waited = Stopwatch.StartNew();
    await Submit(page).ClickAsync();

    await Expect(Screen(page)).ToContainTextAsync("droit à l'effacement", new() { Timeout = 10_000 });
    _harness.Verdict.Started.IsCompleted.ShouldBeTrue();
    waited.Elapsed.ShouldBeGreaterThanOrEqualTo(SlowAnswer);
  }

  /// <summary>Ce que la doublure lente fait durer : assez pour se mesurer, assez peu pour la suite.</summary>
  private static readonly TimeSpan SlowAnswer = TimeSpan.FromSeconds(2);

  /// <summary>Les deux doublures s'accordent sur ces droits.</summary>
  private void Dictate(params DataSubjectRight[] rights)
  {
    _harness.Verdict.Qualification = Qualification.Of(rights);
    _harness.Lexicon.Qualification = Qualification.Of(rights);
  }

  private static async Task QualifyAsync(IPage page, string text)
  {
    await EnterAsync(page, text);
    await Submit(page).ClickAsync();
  }

  private static async Task EnterAsync(IPage page, string text)
  {
    await page.GotoAsync("/qualification");
    await page.GetByLabel("Le texte de la demande").FillAsync(text);
  }

  private static ILocator Submit(IPage page)
  {
    return page.GetByRole(AriaRole.Button, new() { Name = "Qualifier", Exact = true });
  }

  /// <summary>
  /// L'écran, lu par son rôle : la carte du verdict ne porte ni rôle ni nom accessible, et la lire
  /// par la forme du DOM irait contre la règle de ce projet. Les textes attendus ne paraissent nulle
  /// part ailleurs à l'écran que dans le verdict et le dépliant, tous deux tirés des doublures.
  /// </summary>
  private static ILocator Screen(IPage page)
  {
    return page.GetByRole(AriaRole.Main);
  }
}
