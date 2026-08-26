using System.Net;
using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MicroserviceRgpd.FunctionalTests.Screenings;

/// <summary>
/// Les <b>fins anormales</b> d'un scan : l'abandon, le scan que le processus ne connaît plus,
/// l'échec nommé, le refus du second lancement, et les deux fins à zéro objet.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Aucune ne laisse d'objet.</b> Ni relevé, ni rapport, ni ligne d'historique, ni aperçu en
/// cache — et le rapport courant ne recule pas. C'est ce que chacun de ces tests vérifie
/// <b>en base</b>, et non seulement à l'écran : un écran qui dit « rien n'a été écrit » pendant
/// qu'une ligne s'écrit est très exactement le mensonge que ces tests existent pour empêcher.
/// </para>
/// <para>
/// ⚠️ <b>Un scan interrompu ne rend pas un écran « vide et rassurant ».</b> Il ne rend
/// <b>rien</b>, et il le <b>dit</b> : la phase où ça s'est arrêté, la famille de la cause, et la
/// relance sous la main.
/// </para>
/// <para>
/// <b>Un seul scan en vol par déploiement, et la fabrique est partagée</b> : chaque test fait finir
/// le sien avant de rendre la main.
/// </para>
/// </remarks>
[Collection(WebCollection.Name)]
public class ScanAbnormalEndings(CustomWebApplicationFactory<Program> factory)
{
  private readonly CustomWebApplicationFactory<Program> _factory = factory;

  /// <summary>
  /// Un message de pilote comme il en existe : il porte l'<b>hôte</b> et l'<b>utilisateur</b>, et
  /// c'est très exactement ce qu'aucun écran ne doit rendre.
  /// </summary>
  private const string AnInjectedDriverMessage =
    "28P01: authentification par mot de passe échouée pour l'utilisateur « lecteur » "
    + "(Host=galette.exemple, Database=galette_prod)";

  private const string AnInjectedHost = "galette.exemple";

  private const string AnInjectedUser = "lecteur";

  /// <summary>
  /// ⚠️ <b>L'abandon ne laisse rien, et l'annulation traverse le port.</b> La réponse mène à un
  /// écran de fin nommée ; aucun <c>Screening</c> neuf, aucune ligne d'historique, le rapport
  /// courant inchangé, les aperçus du rapport courant intacts — et le port a reçu l'annulation
  /// <b>avant la fin de sa lecture</b>.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Le port coupe la requête, pas seulement la boucle.</b> Sans cela, un <c>SELECT</c>
  /// continuerait de courir sur la base d'un tiers après que l'<c>Operator</c> a dit d'arrêter — et
  /// le service ne saurait même pas qu'il le fait courir. C'est <c>Interrupted</c> qui distingue
  /// une requête réellement coupée d'une réponse simplement ignorée.
  /// </remarks>
  [Fact]
  public async Task LeavesNothingBehindWhenTheOperatorAbandonsDuringTheSamplingPhase()
  {
    // Un rapport réussi d'abord : c'est LUI qui doit rester le courant, avec ses aperçus, quand le
    // scan suivant sera abandonné. Sans ce premier, « le rapport courant ne recule pas » ne
    // porterait sur rien.
    var settled = await SettledReportAsync();

    var surface = HeldDuringSampling();
    var scanId = await surface.LaunchAsync();

    await _factory.Scanner.Started;

    var abandoned = await surface.AbandonAsync(scanId);

    // ⚠️ Un 303, et non la page rendue sur le POST : sans lui, un rechargement du navigateur
    // reposterait l'abandon — sur un scan qui, entre-temps, peut être un AUTRE scan.
    abandoned.StatusCode.ShouldBe(HttpStatusCode.SeeOther);

    var rendered = await surface.WaitingScreenAsync(scanId);

    rendered.ShouldContain("Scan abandonné");
    rendered.ShouldContain(ScanPhase.Sampling.FrenchLabel);
    rendered.ShouldContain("Aucun rapport n'a été écrit");

    // L'écran ne bouge plus : il ne se rafraîchit pas indéfiniment sur un scan que plus personne
    // ne mène.
    rendered.ShouldNotContain("http-equiv=\"refresh\"", Case.Insensitive);

    _factory.Scanner.Interrupted.ShouldBeTrue(
      "Le port n'a pas reçu l'annulation : l'abandon n'a coupé que la boucle, et la lecture court "
      + "encore sur la base du client.");

    var after = await SettledStateAsync();

    after.ShouldBe(
      settled,
      "L'abandon a laissé un objet derrière lui : un rapport, une ligne d'historique, ou un jeu "
      + "d'aperçus qui a évincé celui du rapport courant.");
  }

  /// <summary>
  /// ⚠️ <b>Un geste annulé n'a aucune conséquence.</b> Abandonner un scan déjà fini ne fait pas
  /// reculer sa fin : le rapport vient d'être écrit, et dire « abandonné » par-dessus annoncerait à
  /// l'<c>Operator</c> le contraire de ce qui s'est passé.
  /// </summary>
  [Fact]
  public async Task DoesNothingWhenTheAbandonmentArrivesAfterTheScanIsDone()
  {
    var surface = HeldDuringSampling();
    var scanId = await surface.LaunchAsync();

    // ⚠️ Le jeton est pris PENDANT que le scan court, comme celui du navigateur qui a la page sous
    // les yeux. Un scan fini n'offre plus de bouton : le jeton ne se lirait plus nulle part, et le
    // test n'éprouverait alors que sa propre impossibilité à cliquer.
    var token = await surface.AbandonTokenAsync(scanId);

    _factory.Scanner.Release();

    (await surface.UntilItEndsAsync(scanId)).StatusCode.ShouldBe(HttpStatusCode.SeeOther);

    var late = await surface.AbandonAsync(scanId, token);

    late.StatusCode.ShouldBe(HttpStatusCode.SeeOther);

    // La fin ne recule pas : l'écran d'attente cède toujours la place au rapport que ce scan a
    // écrit, et ne dit nulle part qu'il a été abandonné.
    var after = await surface.Client.GetAsync(ScanSurface.WaitingFor(scanId));

    after.StatusCode.ShouldBe(HttpStatusCode.SeeOther);
    after.Headers.Location!.OriginalString.ShouldContain(ScreeningSurface.Report);
  }

  /// <summary>
  /// Une adresse d'attente d'hier, après un redémarrage : l'écran le <b>dit</b>, avec la relance
  /// sous la main — <b>jamais</b> une redirection muette.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Rediriger en silence vers le rapport courant</b> aurait laissé l'<c>Operator</c> prendre
  /// pour le relevé de son scan un rapport d'avant-hier.
  /// </remarks>
  [Fact]
  public async Task SaysThatAnUnknownScanNoLongerExistsAndOffersToLaunchAgain()
  {
    var surface = new ScanSurface(_factory);

    var response = await surface.Client.GetAsync(
      ScanSurface.WaitingFor(Guid.CreateVersion7().ToString()));

    response.StatusCode.ShouldBe(
      HttpStatusCode.OK,
      "Un scan inconnu a été rendu par une redirection, alors qu'il a une phrase à dire.");

    var rendered = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

    rendered.ShouldContain("Ce scan n'existe plus");
    rendered.ShouldContain("le service a redémarré");
    rendered.ShouldContain(ScanSurface.Connection);
  }

  /// <summary>
  /// Un échec à mi-parcours rend la <b>phase</b> et la <b>famille</b>, et rien de plus.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Ni le message du pilote, ni l'hôte, ni l'utilisateur.</b> C'est par là que la chaîne de
  /// connexion se reconstituerait par morceaux. La doublure injecte ici un message qui porte les
  /// trois : s'il ressort, c'est que quelque chose entre le port et l'écran le recopie.
  /// </para>
  /// <para>
  /// ⚠️ <b>Un relevé tombé à la table 300 sur 312 est perdu EN ENTIER.</b> Aucun rapport partiel
  /// n'est écrit : il se lirait comme complet, et rien sur l'écran ne dirait qu'il manque douze
  /// tables.
  /// </para>
  /// </remarks>
  [Fact]
  public async Task NamesThePhaseAndTheFamilyOfAFailureAndSaysNothingElse()
  {
    var settled = await SettledReportAsync();

    var surface = new ScanSurface(_factory);

    _factory.Scanner.Reset();
    _factory.Scanner.Steps = [ScanStep.CatalogueRead(312), ScanStep.TableSampled(300, 312)];

    // ⚠️ Aucun scanner réel ne lève : c'est la voie par laquelle on éprouve ce que le service fait
    // d'une panne QUI N'AURAIT PAS DÛ le traverser — et ce qu'il en laisse voir.
    _factory.Scanner.Breakdown = new InvalidOperationException(AnInjectedDriverMessage);

    var scanId = await surface.LaunchAsync();

    await surface.UntilItEndsAsync(scanId);

    var rendered = await surface.WaitingScreenAsync(scanId);

    rendered.ShouldContain("Scan échoué");
    rendered.ShouldContain(ScanPhase.Sampling.FrenchLabel);
    rendered.ShouldContain(ScanFailureFamily.Database.FrenchLabel);

    rendered.ShouldNotContain(AnInjectedDriverMessage);
    rendered.ShouldNotContain(AnInjectedHost);
    rendered.ShouldNotContain(AnInjectedUser);
    rendered.ShouldNotContain("28P01");

    (await SettledStateAsync()).ShouldBe(
      settled,
      "Un scan tombé à la table 300 sur 312 a laissé un objet : le relevé n'est pas perdu en "
      + "entier, et un rapport partiel se lirait comme complet.");
  }

  /// <summary>
  /// Pendant un scan, un second POST rend un écran qui <b>nomme le scan en cours</b> — son SGBD, sa
  /// phase, le lien vers son attente — <b>sans rien lancer</b>.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Un « réessayez plus tard » laisserait l'<c>Operator</c> ignorer si le service travaille
  /// pour lui ou pour quelqu'un d'autre</b> — et il relancerait, sur la production d'un tiers, une
  /// lecture déjà en cours.
  /// </remarks>
  [Fact]
  public async Task RefusesASecondLaunchWithAScreenThatNamesTheScanInFlight()
  {
    var surface = HeldDuringSampling();
    var scanId = await surface.LaunchAsync();

    try
    {
      var refused = await surface.ConnectAsync(dialect: DatabaseDialect.Sqlite.PivotName);

      refused.StatusCode.ShouldBe(HttpStatusCode.OK);

      var rendered = WebUtility.HtmlDecode(await refused.Content.ReadAsStringAsync());

      rendered.ShouldContain("Un scan est déjà en cours");
      rendered.ShouldContain(scanId, Case.Insensitive);

      // Le SGBD nommé est celui du scan QUI COURT — PostgreSQL —, et non celui que ce second
      // Operator vient de choisir : c'est le sien qui n'est pas parti.
      rendered.ShouldContain(DatabaseDialect.PostgreSql.FrenchLabel);
      rendered.ShouldContain(ScanPhase.Sampling.FrenchLabel);
      rendered.ShouldContain(ScanSurface.WaitingFor(scanId));

      // ⚠️ Le port n'a été appelé QU'UNE FOIS : le refus n'a rien lancé du tout, et la chaîne de
      // connexion de ce second Operator n'a été remise à personne.
      _factory.Scanner.CallCount.ShouldBe(1);
    }
    finally
    {
      // ⚠️ La libération est dans un `finally`, et le drainage avec elle. Une assertion qui tombe
      // laisserait sinon la doublure retenue et le scan « en vol » : la fabrique est partagée par
      // toute la collection, et chaque test suivant échouerait à cause de celui-ci.
      _factory.Scanner.Release();
      await surface.UntilItEndsAsync(scanId);
    }
  }

  /// <summary>
  /// Les <b>deux</b> fins à zéro objet rendent <b>deux écrans distincts</b>, et seule la seconde
  /// parle de demander un accès.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Les confondre enverrait l'<c>Operator</c> chercher une base vide quand il lui faut
  /// demander un accès.</b> La ligne de partage est la présence de la base au catalogue de
  /// schémas : dans un cas elle a répondu et n'a rien ; dans l'autre le compte ne la voit pas.
  /// </para>
  /// <para>
  /// ⚠️ <b>Ni l'une ni l'autre ne produit un rapport de zéro colonne.</b> Ce rapport-là ferait
  /// reculer le rapport courant et détruirait des jours d'arbitrage pour une connexion d'essai.
  /// </para>
  /// </remarks>
  [Fact]
  public async Task TellsTheTwoEmptyEndingsApartAndAsksForAnAccessOnTheSecondOnly()
  {
    var settled = await SettledReportAsync();
    var surface = new ScanSurface(_factory);

    var noTable = await EndingScreenOfAsync(surface, ScanOutcome.NoTable());
    var nothingVisible = await EndingScreenOfAsync(
      surface,
      ScanOutcome.DatabaseAbsentFromCatalogue());

    noTable.ShouldNotBe(
      nothingVisible,
      "Les deux fins à zéro objet rendent le même écran : l'Operator ira chercher une base vide "
      + "là où il lui faut demander un accès.");

    noTable.ShouldContain("ne porte aucune table");
    noTable.ShouldNotContain("Demandez un accès");

    nothingVisible.ShouldContain("ne voit aucune base");
    nothingVisible.ShouldContain("Demandez un accès");

    (await SettledStateAsync()).ShouldBe(
      settled,
      "Une fin à zéro objet a écrit un rapport de zéro colonne, et le rapport courant a reculé.");
  }

  /// <summary>
  /// ⚠️ <b>Les trois fins anormales partagent la même forme</b> — où le scan s'est arrêté, d'où
  /// vient la cause, et la relance sous la main — et ce test unique la tient sur les trois.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Un test par écran aurait laissé la forme dériver.</b> L'un aurait perdu la phase, l'autre
  /// aurait oublié de dire que le rapport courant n'a pas bougé, et un troisième aurait fini par
  /// proposer de « reprendre » un scan dont la chaîne de connexion n'a jamais survécu.
  /// </para>
  /// <para>
  /// ⚠️ <b>Aucune ne propose de reprendre.</b> La reprise est impossible <b>par construction</b> :
  /// la chaîne de connexion n'a pas survécu au scan, et le service n'a nulle part où l'avoir
  /// gardée. Un bouton « reprendre » aurait promis ce qu'aucune ligne du service ne sait faire.
  /// </para>
  /// </remarks>
  [Fact]
  public async Task GivesTheThreeAbnormalEndingsOneAndTheSameShape()
  {
    var surface = new ScanSurface(_factory);

    var screens = new List<(string Named, string Screen)>
    {
      ("le scan inconnu", await AnUnknownScanScreenAsync(surface)),
      ("l'abandon", await AnAbandonedScanScreenAsync()),
      ("l'échec", await AFailedScanScreenAsync(surface)),
    };

    foreach (var (named, screen) in screens)
    {
      screen.ShouldContain(
        "Où le scan s'est arrêté",
        Case.Sensitive,
        $"L'écran de {named} ne dit pas où le scan s'est arrêté.");

      screen.ShouldContain(
        "D'où vient la cause",
        Case.Sensitive,
        $"L'écran de {named} ne dit pas d'où vient la cause.");

      screen.ShouldContain(
        "Aucun rapport n'a été écrit",
        Case.Sensitive,
        $"L'écran de {named} ne dit pas que le rapport courant n'a pas bougé.");

      screen.ShouldContain(
        ScanSurface.Connection,
        Case.Sensitive,
        $"L'écran de {named} n'offre pas de relancer un scan.");

      screen.ShouldNotContain(
        "reprendre",
        Case.Insensitive,
        $"L'écran de {named} propose de reprendre le scan, ce qu'aucune ligne du service ne sait "
        + "faire : la chaîne de connexion n'a pas survécu.");
    }
  }

  /// <summary>L'écran que rend un scan abandonné pendant ses aperçus.</summary>
  private async Task<string> AnAbandonedScanScreenAsync()
  {
    var surface = HeldDuringSampling();
    var scanId = await surface.LaunchAsync();

    await _factory.Scanner.Started;
    await surface.AbandonAsync(scanId);

    return await surface.WaitingScreenAsync(scanId);
  }

  /// <summary>L'écran que rend un scan tombé sur une panne que le port n'aurait pas dû laisser passer.</summary>
  private async Task<string> AFailedScanScreenAsync(ScanSurface surface)
  {
    _factory.Scanner.Reset();
    _factory.Scanner.Steps = [ScanStep.CatalogueRead(9)];
    _factory.Scanner.Breakdown = new InvalidOperationException(AnInjectedDriverMessage);

    var scanId = await surface.LaunchAsync();

    await surface.UntilItEndsAsync(scanId);

    return await surface.WaitingScreenAsync(scanId);
  }

  /// <summary>L'écran que rend une adresse d'attente que le processus ne connaît plus.</summary>
  private static async Task<string> AnUnknownScanScreenAsync(ScanSurface surface)
  {
    return await surface.WaitingScreenAsync(Guid.CreateVersion7().ToString());
  }

  /// <summary>
  /// Fait courir un scan jusqu'à la fin qu'on lui dicte, et rend l'écran que son adresse d'attente
  /// affiche ensuite.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>L'écran se lit APRÈS la fin, sur la même adresse.</b> Le dernier scan est retenu même
  /// une fois fini — c'est ce qui laisse sa fin lisible à qui arrive une seconde après la dernière
  /// ligne écrite, plutôt que « ce scan n'existe plus » sur un scan qui vient de répondre.
  /// </remarks>
  private async Task<string> EndingScreenOfAsync(ScanSurface surface, ScanOutcome outcome)
  {
    _factory.Scanner.Reset();
    _factory.Scanner.Outcome = outcome;

    var scanId = await surface.LaunchAsync();

    await surface.UntilItEndsAsync(scanId);

    return await surface.WaitingScreenAsync(scanId);
  }

  /// <summary>
  /// Une doublure retenue <b>pendant la phase des aperçus</b> : le catalogue a répondu, une table a
  /// été prélevée, et le scan attend. C'est la fenêtre où l'abandon a quelque chose à couper.
  /// </summary>
  private ScanSurface HeldDuringSampling()
  {
    _factory.Scanner.Reset();
    _factory.Scanner.Outcome = DatabaseScannerDouble.AListing(("adherents", "nom"));
    _factory.Scanner.Steps = [ScanStep.CatalogueRead(312), ScanStep.TableSampled(148, 312)];
    _factory.Scanner.Hold();

    return new ScanSurface(_factory);
  }

  /// <summary>
  /// Fait aboutir un scan, et rend le rapport que le déploiement porte désormais — celui qui ne
  /// doit <b>pas</b> reculer quand la fin suivante est anormale.
  /// </summary>
  private async Task<SettledState> SettledReportAsync()
  {
    await new ScanSurface(_factory).ScanAsync(
      DatabaseScannerDouble.AListing(("adherents", "nom"), ("adherents", "courriel")));

    return await SettledStateAsync();
  }

  /// <summary>
  /// Ce qu'une fin anormale ne doit <b>rien</b> changer : le compte des rapports, l'identité du
  /// courant, et le jeu d'aperçus vivant.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Le cache d'aperçus se lit par le rapport qu'il accompagne.</b> Un scan abandonné qui y
  /// aurait déposé quoi que ce soit aurait évincé le jeu du rapport courant — et
  /// <see cref="ScanPreviews.Of"/> rendrait alors vide sur un rapport qui, lui, a bien des aperçus.
  /// </remarks>
  private async Task<SettledState> SettledStateAsync()
  {
    using var scope = _factory.Services.CreateScope();
    var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    var reports = await database.Screenings.AsNoTracking().CountAsync();
    var current = await database.Screenings.AsNoTracking()
      .OrderByDescending(screening => screening.LaunchedOn)
      .Select(screening => screening.Id)
      .FirstOrDefaultAsync();

    var previews = _factory.Services.GetRequiredService<ScanPreviews>();

    return new SettledState(reports, current, previews.Of(current).Count);
  }

  /// <summary>Ce qu'une fin anormale laisse strictement intact.</summary>
  private sealed record SettledState(int Reports, ScreeningId Current, int PreviewsOfCurrent);
}
