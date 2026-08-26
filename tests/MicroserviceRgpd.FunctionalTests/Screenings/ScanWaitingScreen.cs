using System.Net;
using MicroserviceRgpd.Infrastructure.Data;
using MicroserviceRgpd.Infrastructure.Screenings;
using Microsoft.EntityFrameworkCore;
using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.FunctionalTests.Screenings;

/// <summary>
/// L'écran d'attente d'un scan : ce qu'il montre pendant, ce qu'il refuse de montrer trop tôt, et
/// ce qu'il fait à la fin.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Le scan est ralenti par la doublure, jamais par une attente écrite dans le test.</b> C'est
/// la seule façon d'observer un écran d'attente : un scanner instantané ne laisse jamais voir
/// l'écran qu'il est censé remplir.
/// </para>
/// <para>
/// <b>Un seul scan en vol par déploiement, et la fabrique est partagée</b> : chaque test fait finir
/// le sien avant de rendre la main.
/// </para>
/// </remarks>
[Collection(WebCollection.Name)]
public class ScanWaitingScreen(CustomWebApplicationFactory<Program> factory)
{
  private readonly CustomWebApplicationFactory<Program> _factory = factory;

  /// <summary>
  /// ⚠️ <b>Avant que le catalogue ait répondu, aucune barre et aucun dénominateur.</b> « 0 sur 0 »
  /// se lit comme une mesure ; l'absence, elle, ne se lit pas du tout — et c'est exactement ce
  /// qu'il faut montrer quand on ne sait pas encore.
  /// </summary>
  [Fact]
  public async Task ShowsNoBarAndNoDenominatorBeforeTheCatalogueHasAnswered()
  {
    var surface = Slowed(steps: []);
    var scanId = await surface.LaunchAsync();

    try
    {
      var rendered = await surface.WaitingScreenAsync(scanId);

      rendered.ShouldContain(ScanPhase.Connecting.FrenchLabel);
      rendered.ShouldContain("Le catalogue n'a pas encore répondu");
      rendered.ShouldNotContain("class=\"progress\"");

      // ⚠️ CE QU'ON INTERDIT EST UN COMPTE, pas la préposition. Chercher « sur » tout court aurait
      // interdit à l'écran la moindre phrase française — « la lecture en cours sur la base » en est
      // une — alors que ce qui ne doit pas s'afficher est un dénominateur : deux nombres de part et
      // d'autre, dont aucun n'est honnête tant que le catalogue n'a pas répondu.
      System.Text.RegularExpressions.Regex
        .IsMatch(rendered, @"\d+\s+sur\s+\d+")
        .ShouldBeFalse("L'écran affiche un dénominateur avant que le catalogue ait répondu.");
    }
    finally
    {
      // ⚠️ La libération est dans un `finally`, et le drainage avec elle. Une assertion qui tombe
      // laisserait sinon la doublure retenue et le scan « en vol » : la fabrique est partagée par
      // toute la collection, et chaque test suivant échouerait à cause de celui-ci.
      Release();
      await surface.UntilItEndsAsync(scanId);
    }
  }

  /// <summary>
  /// Une fois le catalogue rendu, le compte est <b>réel</b> — « table 148 sur 312 » —, et jamais un
  /// pourcentage écrit.
  /// </summary>
  [Fact]
  public async Task CountsRealTablesOnceTheCatalogueHasAnswered()
  {
    var surface = Slowed(steps:
      [ScanStep.CatalogueRead(312), ScanStep.TableSampled(148, 312)]);

    var scanId = await surface.LaunchAsync();

    try
    {
      var rendered = await surface.WaitingScreenAsync(scanId);

      rendered.ShouldContain(ScanPhase.Sampling.FrenchLabel);
      rendered.ShouldContain("table 148 sur 312");
      rendered.ShouldContain("class=\"progress\"");
    }
    finally
    {
      // ⚠️ La libération est dans un `finally`, et le drainage avec elle. Une assertion qui tombe
      // laisserait sinon la doublure retenue et le scan « en vol » : la fabrique est partagée par
      // toute la collection, et chaque test suivant échouerait à cause de celui-ci.
      Release();
      await surface.UntilItEndsAsync(scanId);
    }
  }

  /// <summary>
  /// L'écran se rafraîchit tout seul, et <b>sans une ligne de JavaScript</b>.
  /// </summary>
  [Fact]
  public async Task RefreshesItselfWithoutAnyScript()
  {
    var surface = Slowed(steps: [ScanStep.CatalogueRead(12)]);
    var scanId = await surface.LaunchAsync();

    try
    {
      var rendered = await surface.WaitingScreenAsync(scanId);

      rendered.ShouldContain("http-equiv=\"refresh\"");
      rendered.ShouldNotContain("<script", Case.Insensitive);
    }
    finally
    {
      // ⚠️ La libération est dans un `finally`, et le drainage avec elle. Une assertion qui tombe
      // laisserait sinon la doublure retenue et le scan « en vol » : la fabrique est partagée par
      // toute la collection, et chaque test suivant échouerait à cause de celui-ci.
      Release();
      await surface.UntilItEndsAsync(scanId);
    }
  }

  /// <summary>
  /// <b>Deux <c>Operator</c> voient le même écran et le même compte.</b> L'avancement est un fait du
  /// déploiement, pas d'une session.
  /// </summary>
  [Fact]
  public async Task ShowsTheSameCountToASecondOperator()
  {
    var surface = Slowed(steps:
      [ScanStep.CatalogueRead(312), ScanStep.TableSampled(148, 312)]);

    var scanId = await surface.LaunchAsync();

    try
    {
      var mine = await surface.WaitingScreenAsync(scanId);
      var theirs = await surface.WaitingScreenAsync(scanId, surface.AnotherOperator);

      mine.ShouldContain("table 148 sur 312");
      theirs.ShouldContain("table 148 sur 312");
    }
    finally
    {
      // ⚠️ La libération est dans un `finally`, et le drainage avec elle. Une assertion qui tombe
      // laisserait sinon la doublure retenue et le scan « en vol » : la fabrique est partagée par
      // toute la collection, et chaque test suivant échouerait à cause de celui-ci.
      Release();
      await surface.UntilItEndsAsync(scanId);
    }
  }

  /// <summary>
  /// ⚠️ <b>Fermer l'onglet n'annule rien.</b> Le scan court hors de la requête de l'<c>Operator</c>,
  /// dans une portée de service à lui : emprunter le jeton d'annulation de la requête aurait fait
  /// qu'un onglet fermé coupe une lecture de quarante secondes sur la base d'un tiers.
  /// </summary>
  [Fact]
  public async Task KeepsScanningAfterTheOperatorsConnectionIsGone()
  {
    var surface = Slowed(steps: [ScanStep.CatalogueRead(3)]);
    var scanId = await surface.LaunchAsync();

    // L'Operator s'en va : ses requêtes en cours sont abandonnées, et son client disparaît.
    surface.Client.CancelPendingRequests();
    surface.Client.Dispose();

    Release();

    var ended = await new ScanSurface(_factory).UntilItEndsAsync(scanId);

    ended.StatusCode.ShouldBe(
      HttpStatusCode.SeeOther,
      "Le scan a été annulé avec la requête de l'Operator, alors qu'il ne le devait pas.");
    _factory.Scanner.Interrupted.ShouldBeFalse();
  }

  /// <summary>
  /// À la fin, un <c>303</c> vers le rapport — et non un rafraîchissement de plus. Le <c>303</c>
  /// <b>remplace</b> l'entrée d'historique : qui revient d'une page ne tombe pas sur « ce scan
  /// n'existe plus » à propos d'un scan parfaitement réussi.
  /// </summary>
  [Fact]
  public async Task AnswersWithASeeOtherTowardTheReportOnceTheScanIsDone()
  {
    var surface = new ScanSurface(_factory);

    var ended = await surface.ScanAsync(
      DatabaseScannerDouble.AListing(("adherents", "nom"), ("adherents", "courriel")));

    ended.StatusCode.ShouldBe(HttpStatusCode.SeeOther);
    ended.Headers.Location!.OriginalString.ShouldContain(ScreeningSurface.Report);
  }

  /// <summary>
  /// Le rapport que le scan produit est <b>scanné</b>, et il porte la clause de son origine ainsi
  /// que l'identité du moteur avec ses <c>formes actives</c>.
  /// </summary>
  [Fact]
  public async Task ProducesAScannedReportThatSaysSo()
  {
    var surface = new ScanSurface(_factory);

    await surface.ScanAsync(
      DatabaseScannerDouble.AListing(("adherents", "nom"), ("adherents", "date_naissance")));

    var report = WebUtility.HtmlDecode(
      await surface.Client.GetStringAsync(ScreeningSurface.Report));

    // ⚠️ La clause du chemin SCANNÉ, et non celle du collé : « le service n'a jamais vu une seule
    // valeur » serait faux ici, et c'est très exactement le mensonge qu'ADR-0013 existe pour
    // empêcher.
    report.ShouldContain($"n'a lu que {ColumnPreview.MaxValuesInWords} valeurs par colonne");
    report.ShouldNotContain("n'a jamais vu une seule valeur");

    // L'identité du moteur porte ses FORMES ACTIVES : les règles de forme ont tourné, parce que le
    // scan leur a apporté des aperçus.
    report.ShouldContain(RulesAndLexiconScreeningEngine.FormsVersion);
    report.ShouldNotContain(RulesAndLexiconScreeningEngine.InactiveForms);

    using var scope = _factory.Services.CreateScope();
    var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var current = await database.Screenings.AsNoTracking()
      .OrderByDescending(screening => screening.LaunchedOn)
      .FirstAsync();

    current.Origin.ShouldBe(ListingOrigin.Scanned);
  }

  /// <summary>
  /// ⚠️ <b>Le rapport courant part à l'archive au succès, et à ce moment-là seulement.</b>
  /// L'historique porte une ligne de plus.
  /// </summary>
  [Fact]
  public async Task ArchivesThePreviousReportOnSuccessAndAddsOneLineToTheHistory()
  {
    var surface = new ScanSurface(_factory);
    var screening = new ScreeningSurface(_factory);

    var before = LinesOf(await screening.Client.GetStringAsync(ScreeningSurface.History));

    await surface.ScanAsync(DatabaseScannerDouble.AListing(("adherents", "nom")));

    var after = LinesOf(await screening.Client.GetStringAsync(ScreeningSurface.History));

    after.ShouldBe(before + 1, "Le scan réussi n'a pas ajouté sa ligne à l'historique.");
  }

  /// <summary>
  /// ⚠️ <b>Une fin sans rapport n'archive rien.</b> Un scan tombé ne fait pas perdre à
  /// l'<c>Operator</c> le rapport qu'il avait sous les yeux.
  /// </summary>
  [Fact]
  public async Task ArchivesNothingWhenTheScanEndsWithoutAReport()
  {
    var surface = new ScanSurface(_factory);
    var screening = new ScreeningSurface(_factory);

    var before = LinesOf(await screening.Client.GetStringAsync(ScreeningSurface.History));

    await surface.ScanAsync(ScanOutcome.NoTable());

    var after = LinesOf(await screening.Client.GetStringAsync(ScreeningSurface.History));

    after.ShouldBe(before, "Une fin sans relevé a malgré tout archivé le rapport courant.");
  }

  /// <summary>
  /// ⚠️ <b>Un second lancement est refusé, et le refus NOMME le scan en cours.</b> Un « réessayez
  /// plus tard » laisserait l'<c>Operator</c> ignorer si le service travaille pour lui ou pour
  /// quelqu'un d'autre — et il relancerait, sur la production d'un tiers, une lecture déjà en cours.
  /// </summary>
  [Fact]
  public async Task RefusesASecondLaunchAndNamesTheScanAlreadyRunning()
  {
    var surface = Slowed(steps: [ScanStep.CatalogueRead(7)]);
    var scanId = await surface.LaunchAsync();

    try
    {
      var refused = await surface.ConnectAsync();

      refused.StatusCode.ShouldBe(HttpStatusCode.OK);

      var rendered = WebUtility.HtmlDecode(await refused.Content.ReadAsStringAsync());

      rendered.ShouldContain("Un scan est déjà en cours");
      rendered.ShouldContain(scanId, Case.Insensitive);

      // Le port n'a été appelé qu'une fois : le refus n'a rien lancé du tout.
      _factory.Scanner.CallCount.ShouldBe(1);
    }
    finally
    {
      // ⚠️ La libération est dans un `finally`, et le drainage avec elle. Une assertion qui tombe
      // laisserait sinon la doublure retenue et le scan « en vol » : la fabrique est partagée par
      // toute la collection, et chaque test suivant échouerait à cause de celui-ci.
      Release();
      await surface.UntilItEndsAsync(scanId);
    }
  }

  /// <summary>Une adresse de scan que le processus ne connaît plus le <b>dit</b>.</summary>
  /// <remarks>
  /// ⚠️ Rediriger en silence vers le rapport courant aurait laissé l'<c>Operator</c> croire que
  /// c'est celui de son scan.
  /// </remarks>
  [Fact]
  public async Task SaysThatAnUnknownScanNoLongerExists()
  {
    var surface = new ScanSurface(_factory);

    var rendered = await surface.WaitingScreenAsync(Guid.CreateVersion7().ToString());

    rendered.ShouldContain("Ce scan n'existe plus");
  }

  /// <summary>
  /// Une doublure qui prend son temps, et les pas qu'elle rapporte avant de se mettre à attendre.
  /// </summary>
  private ScanSurface Slowed(IReadOnlyList<ScanStep> steps)
  {
    _factory.Scanner.Reset();
    _factory.Scanner.Outcome = DatabaseScannerDouble.AListing(("adherents", "nom"));
    _factory.Scanner.Steps = steps;

    // ⚠️ Une RETENUE, et non un délai : le scanner attend jusqu'à ce que le test le libère, ce qui
    // rend l'écran observable sans faire dépendre l'assertion d'une course entre deux fils.
    _factory.Scanner.Hold();

    return new ScanSurface(_factory);
  }

  /// <summary>Laisse la doublure finir : le scan qui attendait rend sa fin tout de suite.</summary>
  private void Release()
  {
    _factory.Scanner.Release();
  }

  /// <summary>
  /// Combien de rapports <b>archivés</b> l'historique porte. ⚠️ Compter les <c>&lt;tr&gt;</c> aurait
  /// compté double : chaque rapport y tient sur deux lignes, dont une de repli.
  /// </summary>
  private static int LinesOf(string history)
  {
    return System.Text.RegularExpressions.Regex.Matches(history, "colspan=\"5\"").Count;
  }
}
