using System.Net;
using System.Text;
using System.Text.Json;
using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.FunctionalTests.Screenings;

/// <summary>
/// L'<b>export de la Cartographie</b> par sa seule frontière HTTP : le bouton et les deux liens sur
/// le rapport courant, puis les deux fichiers que les routes rendent.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Deux <c>GET</c> nus servis par des Razor Pages, jamais un point d'API.</b> Un lien collé
/// dans un courriel annonce ce qu'il rend ; un point d'API aurait des appelants qu'on ne voit pas —
/// <c>NoApiScreensADatabase</c> le refuse, et ce fichier vit à côté de lui.
/// </para>
/// <para>
/// <b>La collection est partagée</b> et « courant » est un calcul sur tout le déploiement : chaque
/// test dépose donc son propre relevé, et lit l'export du rapport que <b>son</b> dépôt vient de
/// rendre courant.
/// </para>
/// </remarks>
[Collection(WebCollection.Name)]
public class ScreeningMapExport(CustomWebApplicationFactory<Program> factory)
{
  private readonly ScreeningSurface _surface = new(factory);

  private static readonly byte[] Bom = Encoding.UTF8.GetPreamble();

  /// <summary>Dépose trois colonnes, en arbitre deux, et laisse la troisième attendre.</summary>
  private async Task ADepositedReportAsync()
  {
    await _surface.DepositAndReadTheReportAsync(
      ScreeningSurface.Paste(
        ScreeningSurface.Column("adr_l1", position: 1, columnComment: "adresse ; postale"),
        ScreeningSurface.Column("id_adh", position: 2),
        ScreeningSurface.Column("dt_naiss", position: 3)));

    await _surface.ArbitrateAsync("adr_l1", ScreenedColumnState.Retained.Name);
    await _surface.ArbitrateAsync("id_adh", ScreenedColumnState.SetAside.Name);
  }

  private static string CsvTextOf(byte[] content)
  {
    return Encoding.UTF8.GetString(content, Bom.Length, content.Length - Bom.Length);
  }

  // ─── L'écran ────────────────────────────────────────────────────────────────────────────────

  /// <summary>
  /// <b>Le bouton et les deux liens.</b> Deux liens sont la forme qui marche sans rien d'autre, et la
  /// seule qui se colle dans un courriel.
  /// </summary>
  [Fact]
  public async Task RendersTheButtonAndTheTwoLinks()
  {
    var report = WebUtility.HtmlDecode(
      await _surface.DepositAndReadTheReportAsync(
        ScreeningSurface.Paste(ScreeningSurface.Column("adr_l1"))));

    report.ShouldContain("Exporter la cartographie des données personnelles");
    report.ShouldContain(ScreeningSurface.MapAsCsv);
    report.ShouldContain(ScreeningSurface.MapAsJson);
  }

  // ─── Les deux routes ────────────────────────────────────────────────────────────────────────

  /// <summary>
  /// Les deux adresses répondent en <c>GET</c>, annoncent ce qu'elles rendent, et nomment le fichier
  /// qu'elles font enregistrer.
  /// </summary>
  [Fact]
  public async Task AnswersBothRoutesWithTheirContentTypeAndTheirFileName()
  {
    await ADepositedReportAsync();

    var csv = await _surface.Client.GetAsync(ScreeningSurface.MapAsCsv);

    csv.StatusCode.ShouldBe(HttpStatusCode.OK);
    csv.Content.Headers.ContentType?.ToString().ShouldBe("text/csv; charset=utf-8");
    csv.Content.Headers.ContentDisposition?.DispositionType.ShouldBe("attachment");

    var csvFileName = csv.Content.Headers.ContentDisposition?.FileName ?? string.Empty;

    csvFileName.ShouldContain("cartographie-galette-prod-");
    csvFileName.ShouldEndWith(".csv");

    var json = await _surface.Client.GetAsync(ScreeningSurface.MapAsJson);

    json.StatusCode.ShouldBe(HttpStatusCode.OK);
    json.Content.Headers.ContentType?.ToString().ShouldBe("application/json; charset=utf-8");
    (json.Content.Headers.ContentDisposition?.FileName ?? string.Empty).ShouldEndWith(".json");
  }

  /// <summary>
  /// ⚠️ <b>Une ligne par colonne, les non signalées et les écartées comprises.</b> Une cartographie
  /// réduite aux retenues serait le filtre que l'<c>Omission relue</c> interdit, déplacé du rapport
  /// vers l'export.
  /// </summary>
  [Fact]
  public async Task CarriesEveryColumnOfTheReportInBothFiles()
  {
    await ADepositedReportAsync();

    using var json = JsonDocument.Parse(
      await _surface.Client.GetByteArrayAsync(ScreeningSurface.MapAsJson));

    var rows = json.RootElement.GetProperty("colonnes");

    rows.GetArrayLength().ShouldBe(3);
    rows.EnumerateArray().Select(row => row.GetProperty("colonne").GetString())
      .ShouldBe(["adr_l1", "id_adh", "dt_naiss"]);
    rows.EnumerateArray().Select(row => row.GetProperty("etat").GetString())
      .ShouldBe(
        [
          ScreenedColumnState.Retained.FrenchLabel,
          ScreenedColumnState.SetAside.FrenchLabel,
          ScreenedColumnState.Awaiting.FrenchLabel,
        ]);

    var csv = CsvTextOf(await _surface.Client.GetByteArrayAsync(ScreeningSurface.MapAsCsv));

    csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length.ShouldBe(
      4, "L'en-tête, puis une ligne par colonne.");
  }

  /// <summary>
  /// ⚠️ <b>Le relevé est en tête, une seule fois</b>, et la phrase de l'inachevé compte ce qui
  /// attend encore — sans se confondre avec la clause d'incomplétude, qui n'est nulle part.
  /// </summary>
  [Fact]
  public async Task CarriesTheListingOnceAndCountsWhatIsStillWaiting()
  {
    await ADepositedReportAsync();

    using var json = JsonDocument.Parse(
      await _surface.Client.GetByteArrayAsync(ScreeningSurface.MapAsJson));

    var listing = json.RootElement.GetProperty("releve");

    listing.GetProperty("base").GetString().ShouldBe("galette_prod");
    listing.GetProperty("dialecte").GetString().ShouldBe("postgresql");
    listing.GetProperty("moteur").GetProperty("nom").GetString().ShouldNotBeNullOrWhiteSpace();
    listing.GetProperty("arbitrageInacheve").GetString()
      .ShouldBe("1 colonne de cette cartographie n'a pas encore été arbitrée.");

    foreach (var row in json.RootElement.GetProperty("colonnes").EnumerateArray())
    {
      row.TryGetProperty("base", out _).ShouldBeFalse();
      row.TryGetProperty("moteur", out _).ShouldBeFalse();
    }
  }

  /// <summary>La phrase disparaît quand plus rien n'attend : elle n'est pas écrite « 0 ».</summary>
  [Fact]
  public async Task SaysNothingOfTheUnfinishedOnceEveryColumnHasBeenRuledOn()
  {
    await _surface.DepositAndReadTheReportAsync(
      ScreeningSurface.Paste(ScreeningSurface.Column("adr_l1", position: 1)));

    await _surface.ArbitrateAsync("adr_l1", ScreenedColumnState.Retained.Name);

    using var json = JsonDocument.Parse(
      await _surface.Client.GetByteArrayAsync(ScreeningSurface.MapAsJson));

    json.RootElement.GetProperty("releve").TryGetProperty("arbitrageInacheve", out _)
      .ShouldBeFalse();
  }

  /// <summary>
  /// ⚠️ <b>Le CSV commence par le BOM, sépare par des points-virgules et cite selon la RFC 4180</b> —
  /// éprouvé sur un commentaire portant le séparateur.
  /// </summary>
  [Fact]
  public async Task OpensAsATableInASpreadsheet()
  {
    await ADepositedReportAsync();

    var content = await _surface.Client.GetByteArrayAsync(ScreeningSurface.MapAsCsv);

    content.Take(3).ShouldBe(Bom);

    var lines = CsvTextOf(content).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

    lines[0].ShouldStartWith("schema;table;colonne;position;type;nullable;");
    lines[0].ShouldEndWith(";etat;rendu_le");

    // Le commentaire porte un point-virgule : sans citation, il aurait fabriqué une case de plus.
    lines[1].ShouldContain("\"adresse ; postale\"");
    lines[1].ShouldContain(";oui;");
    lines[1].ShouldContain($";{ScreenedColumnState.Retained.FrenchLabel};");

    // ⚠️ Les horodatages du CSV perdent leur décalage — la seule forme qu'un tableur français
    // reconnaisse comme une date. Éprouvé sur la dernière case, seule à en porter un : le chercher
    // sur la ligne entière ferait rougir le test sur le « T » d'« adherents ».
    var renduLe = lines[1].Split(';')[^1];

    renduLe.ShouldMatch(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}$");
  }

  /// <summary>
  /// <b>L'export parle la taxonomie des prototypes</b>, dans les deux formats : le nom canonique et
  /// le libellé que l'écran rend — jamais une valeur retirée.
  /// </summary>
  [Fact]
  public async Task CarriesThePrototypeTaxonomyInBothFiles()
  {
    await _surface.DepositAndReadTheReportAsync(
      ScreeningSurface.Paste(
        ScreeningSurface.Column("derniere_connexion", position: 1),
        ScreeningSurface.Column("confession", position: 2)));

    using var json = JsonDocument.Parse(
      await _surface.Client.GetByteArrayAsync(ScreeningSurface.MapAsJson));

    json.RootElement.GetProperty("colonnes").EnumerateArray()
      .Select(row => (row.GetProperty("categorie").GetString(), row.GetProperty("categorieLibelle").GetString()))
      .ShouldBe(
      [
        (PersonalDataCategory.OnlineIdentifier.Name, "identifiant en ligne"),
        (PersonalDataCategory.DemographicData.Name, "données démographiques"),
      ]);

    var csv = CsvTextOf(await _surface.Client.GetByteArrayAsync(ScreeningSurface.MapAsCsv));

    csv.ShouldContain(";OnlineIdentifier;identifiant en ligne;");
    csv.ShouldContain(";DemographicData;données démographiques;");
    csv.ShouldNotContain("ConnectionData");
    csv.ShouldNotContain("SpecialCategoryData");
  }

  /// <summary>
  /// ⚠️ <b>Ni valeur lue, ni compte de valeurs, ni un mot de la clause d'incomplétude</b> — alors
  /// même que l'écran d'où l'on vient la porte en toutes lettres.
  /// </summary>
  [Fact]
  public async Task CarriesNoWordOfTheClauseThatEveryScreenStillShows()
  {
    var report = WebUtility.HtmlDecode(
      await _surface.DepositAndReadTheReportAsync(
        ScreeningSurface.Paste(ScreeningSurface.Column("adr_l1"))));

    // L'écran, lui, porte bien la clause : c'est la contrepartie qui borne le renversement.
    report.ShouldContain("Ce rapport de détection n'a lu qu'un relevé de colonnes");

    var files = new[]
    {
      Encoding.UTF8.GetString(await _surface.Client.GetByteArrayAsync(ScreeningSurface.MapAsJson)),
      CsvTextOf(await _surface.Client.GetByteArrayAsync(ScreeningSurface.MapAsCsv)),
    };

    foreach (var content in files)
    {
      content.ShouldNotContain("n'a lu qu'un relevé de colonnes");
      content.ShouldNotContain("n'a regardé aucune autre source");
      content.ShouldNotContain("Cette énumération est ouverte");
      content.ShouldNotContain("ne recense pas vos systèmes");
      content.ShouldNotContain("incomplet", Case.Insensitive);
      content.ShouldNotContain("aperçu", Case.Insensitive);
      content.ShouldNotContain("conforme", Case.Insensitive);
    }
  }

  /// <summary>
  /// Aucune détection n'a été lancée sur ce déploiement ? On ne rend pas un fichier vide : un CSV
  /// n'ayant qu'un en-tête se lirait comme « ce client n'a aucune donnée personnelle ».
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Un rapport existe toujours ici</b> — la collection est partagée et d'autres tests
  /// déposent : ce qui s'éprouve est donc que la route <b>ne se refuse pas</b> et n'invente pas de
  /// fichier vide, le renvoi vers le dépôt vivant dans le même chemin que celui du rapport.
  /// </remarks>
  [Fact]
  public async Task NeverRendersAFileWithoutARowsToPutInIt()
  {
    await ADepositedReportAsync();

    var csv = await _surface.Client.GetAsync(ScreeningSurface.MapAsCsv);

    csv.StatusCode.ShouldBe(HttpStatusCode.OK);
    CsvTextOf(await csv.Content.ReadAsByteArrayAsync())
      .Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length.ShouldBeGreaterThan(1);
  }

  /// <summary>
  /// ⚠️ <b>Sur un relevé SQLite, la base est un nom de fichier et rien d'autre.</b> Le SGBD ne
  /// connaît sa base que par son chemin, et le dossier parent est très exactement l'endroit où l'on
  /// écrit le nom du client : ce champ quitte le service, jusque dans le nom du fichier CSV.
  /// </summary>
  [Fact]
  public async Task CarriesNoPathWhenTheListingCameFromASqliteFile()
  {
    await _surface.DepositAndReadTheReportAsync(
      ScreeningSurface.Lines(
        ScreeningSurface.Header(
          dialect: "sqlite", database: "/srv/clients/mutuelle-des-cheminots/galette.sqlite"),
        ScreeningSurface.Column("adr_l1"),
        ScreeningSurface.ClosingLine(1)));

    using var json = JsonDocument.Parse(
      await _surface.Client.GetByteArrayAsync(ScreeningSurface.MapAsJson));

    var database = json.RootElement.GetProperty("releve").GetProperty("base").GetString()
      ?? string.Empty;

    database.ShouldBe("galette.sqlite");
    database.ShouldNotContain("/");
    database.ShouldNotContain("mutuelle-des-cheminots");

    // Et le nom du fichier, qui part par courriel, n'en porte pas davantage.
    var csv = await _surface.Client.GetAsync(ScreeningSurface.MapAsCsv);
    var fileName = csv.Content.Headers.ContentDisposition?.FileName ?? string.Empty;

    fileName.ShouldStartWith("cartographie-galette-sqlite-");
    fileName.ShouldNotContain("mutuelle");
  }

  /// <summary>
  /// ⚠️ <b>Les deux routes ne répondent qu'en <c>GET</c>.</b> Un <c>POST</c> qui passerait ferait de
  /// l'export un geste qui écrit, ce qu'il n'est pas.
  /// </summary>
  [Fact]
  public async Task RefusesAnythingButAGet()
  {
    await ADepositedReportAsync();

    foreach (var address in new[] { ScreeningSurface.MapAsCsv, ScreeningSurface.MapAsJson })
    {
      var posted = await _surface.Client.PostAsync(address, new StringContent(string.Empty));

      posted.StatusCode.ShouldBeOneOf(
        HttpStatusCode.MethodNotAllowed,
        HttpStatusCode.BadRequest,
        HttpStatusCode.NotFound);
    }
  }
}
