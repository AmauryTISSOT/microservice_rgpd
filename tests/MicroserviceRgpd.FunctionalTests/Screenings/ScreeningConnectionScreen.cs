using System.Net;
using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.Web.Pages.Screenings;

namespace MicroserviceRgpd.FunctionalTests.Screenings;

/// <summary>
/// L'écran de la <b>voie connectée</b> : ce qu'il dit avant qu'on tape quoi que ce soit, ce qu'il ne
/// propose pas, et ce qu'un lancement valide déclenche.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Les trois phrases sont éprouvées <i>littéralement</i>.</b> Ce ne sont pas des libellés
/// d'interface : ce sont les trois conséquences que l'<c>Operator</c> ne peut pas deviner, et une
/// reformulation qui les adoucirait passerait inaperçue d'un test qui ne chercherait qu'un mot-clé.
/// </para>
/// <para>
/// ⚠️ <b>Le dépôt collé n'est pas touché ici</b>, et ses tests non plus : la voie connectée s'ajoute
/// à côté de lui, elle ne le remplace pas.
/// </para>
/// </remarks>
[Collection(WebCollection.Name)]
public class ScreeningConnectionScreen(CustomWebApplicationFactory<Program> factory)
{
  private readonly CustomWebApplicationFactory<Program> _factory = factory;

  /// <summary>
  /// Les trois SGBD, par le nom que l'<c>Operator</c> lit — et par celui que le formulaire poste.
  /// </summary>
  [Fact]
  public async Task OffersTheThreeDialectsTheServiceKnowsHowToReach()
  {
    var rendered = await RenderedAsync();

    foreach (var dialect in DatabaseDialect.List)
    {
      rendered.ShouldContain(
        dialect.FrenchLabel,
        Case.Sensitive,
        $"L'écran de connexion ne propose pas {dialect.FrenchLabel}.");
      rendered.ShouldContain(
        $"value=\"{dialect.PivotName}\"",
        Case.Sensitive,
        $"Le formulaire ne sait pas poster {dialect.FrenchLabel}.");
    }
  }

  /// <summary>
  /// <b>La phrase des droits, mot pour mot.</b> Ce qu'elle dit et qu'aucun raccourci ne dit :
  /// que le service ne peut ni détecter ni signaler qu'une partie du schéma lui a été masquée.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>« Le service ne relève que ce que ce compte lui montre » ne suffit pas</b>, et c'est
  /// pourquoi le test compare la phrase entière : cette formulation-là se lit comme un truisme
  /// technique, dont personne ne tire qu'un rapport peut être entier et pourtant amputé d'un schéma.
  /// </remarks>
  [Fact]
  public async Task SaysWhatTheAccountMustHaveAndWhatTheServiceWillNotKnow()
  {
    var rendered = await RenderedAsync();

    rendered.ShouldContain(ConnectionModel.RightsSentence);

    rendered.ShouldContain(
      "il ne peut ni détecter ni signaler qu'une partie lui a été masquée",
      Case.Sensitive,
      "La phrase des droits a perdu ce qu'elle apporte : l'aveu que l'omission est INVISIBLE.");
  }

  /// <summary>
  /// La phrase des droits est <b>la même quel que soit le SGBD</b> : la propriété tient au fait que
  /// le service relève ce que le compte lui présente, pas au pilote.
  /// </summary>
  [Fact]
  public async Task SaysTheSameThingAboutRightsWhicheverDialectIsChosen()
  {
    var rendered = await RenderedAsync();

    // Une seule phrase, écrite une seule fois, sous les trois choix : il n'existe aucun chemin par
    // lequel elle varierait — et le test le prouve en n'en trouvant qu'une occurrence.
    var occurrences = rendered.Split(ConnectionModel.RightsSentence).Length - 1;

    occurrences.ShouldBe(
      1,
      "La phrase des droits est écrite plusieurs fois : deux rédactions finiraient par ne plus dire "
      + "la même chose selon le SGBD.");
  }

  /// <summary>Le sort de la chaîne : ni conservée, ni journalisée, ni rejouée.</summary>
  [Fact]
  public async Task SaysWhatBecomesOfTheConnectionString()
  {
    var rendered = await RenderedAsync();

    rendered.ShouldContain("ni conservée, ni journalisée, ni rejouée");
  }

  /// <summary>Ce qu'un lancement coûte : le rapport courant part à l'archive.</summary>
  [Fact]
  public async Task SaysThatLaunchingPushesTheCurrentReportToTheArchive()
  {
    var rendered = await RenderedAsync();

    rendered.ShouldContain("renvoie le rapport de détection courant à l'archive");
    rendered.ShouldContain(
      "pas repris",
      Case.Sensitive,
      "L'écran ne dit pas que les arbitrages ne sont pas repris : c'est le coût réel du geste.");
  }

  /// <summary>Sous SQLite, le fichier doit être joignable <b>par le service</b>.</summary>
  [Fact]
  public async Task SaysThatUnderSqliteTheFileMustBeReachableByTheServiceItself()
  {
    var rendered = await RenderedAsync();

    rendered.ShouldContain("SQLite");
    rendered.ShouldContain("joignable par le service");
  }

  /// <summary>
  /// ⚠️ <b>Aucun champ de téléversement, et il ne doit jamais y en avoir un.</b> Il ferait entrer
  /// une base entière — des valeurs réelles, en masse — dans un service dont la propriété première
  /// est de n'en conserver aucune.
  /// </summary>
  [Fact]
  public async Task OffersNoFileUploadAnywhere()
  {
    var rendered = await RenderedAsync();

    rendered.ShouldNotContain("type=\"file\"", Case.Insensitive);
    rendered.ShouldNotContain("multipart/form-data", Case.Insensitive);
  }

  /// <summary>
  /// La chaîne de connexion <b>ne revient pas dans le HTML</b>, y compris après un refus : un
  /// secret d'accès recopié dans une réponse se retrouve dans le cache du navigateur.
  /// </summary>
  [Fact]
  public async Task NeverWritesTheConnectionStringBackIntoTheScreen()
  {
    var surface = new ScanSurface(_factory);

    _factory.Scanner.Reset();
    _factory.Scanner.Outcome = DatabaseScannerDouble.AListing(("adherents", "nom"));
    _factory.Scanner.Hold();

    var scanId = await surface.LaunchAsync();

    try
    {
      // Un second lancement pendant que le premier court : c'est le chemin qui REND une page plutôt
      // que de rediriger, donc le seul où la chaîne pourrait ressortir.
      var refused = await surface.ConnectAsync();

      refused.StatusCode.ShouldBe(HttpStatusCode.OK);
      (await refused.Content.ReadAsStringAsync()).ShouldNotContain("canari-308");
    }
    finally
    {
      // ⚠️ La libération est dans un `finally` : une assertion qui tombe laisserait sinon la
      // doublure retenue et le scan « en vol », sur une fabrique partagée par toute la collection.
      _factory.Scanner.Release();
      await surface.UntilItEndsAsync(scanId);
    }
  }

  /// <summary>
  /// Un lancement valide <b>redirige</b> vers l'écran d'attente : rendre la page ici ferait d'un
  /// rechargement un second scan sur la base du client.
  /// </summary>
  [Fact]
  public async Task LeadsToTheWaitingScreenOfTheScanItJustStarted()
  {
    var surface = new ScanSurface(_factory);

    _factory.Scanner.Reset();
    _factory.Scanner.Outcome = DatabaseScannerDouble.AListing(("adherents", "nom"));

    var scanId = await surface.LaunchAsync();

    scanId.ShouldNotBeNullOrWhiteSpace();

    await surface.UntilItEndsAsync(scanId);
  }

  /// <summary>Le dialecte choisi est celui que le port reçoit, et il n'est pas deviné.</summary>
  [Fact]
  public async Task HandsTheChosenDialectAndTheStringToThePortAndToNothingElse()
  {
    var surface = new ScanSurface(_factory);

    _factory.Scanner.Reset();
    _factory.Scanner.Outcome = DatabaseScannerDouble.AListing(("adherents", "nom"));

    var scanId = await surface.LaunchAsync(dialect: DatabaseDialect.Sqlite.PivotName);

    await surface.UntilItEndsAsync(scanId);

    _factory.Scanner.ReceivedDialect.ShouldBe(DatabaseDialect.Sqlite);
    _factory.Scanner.ReceivedConnectionString.ShouldBe(ScanSurface.ASecret);
  }

  /// <summary>Une chaîne vide n'est pas un scan : le refus se lit sur l'écran, sans rien lancer.</summary>
  [Fact]
  public async Task RefusesAnEmptyConnectionStringWithoutReachingThePort()
  {
    var surface = new ScanSurface(_factory);

    _factory.Scanner.Reset();

    var refused = await surface.ConnectAsync(connectionString: string.Empty);

    refused.StatusCode.ShouldBe(HttpStatusCode.OK);
    (await refused.Content.ReadAsStringAsync()).ShouldContain("chaîne de connexion est vide");
    _factory.Scanner.CallCount.ShouldBe(0);
  }

  private async Task<string> RenderedAsync()
  {
    var rendered = await new ScanSurface(_factory).Client.GetStringAsync(ScanSurface.Connection);

    return WebUtility.HtmlDecode(rendered);
  }
}
