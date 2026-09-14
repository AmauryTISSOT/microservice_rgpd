using System.Net;
using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.Infrastructure.Data;
using MicroserviceRgpd.TestDoubles.Ollama;
using Microsoft.EntityFrameworkCore;

namespace MicroserviceRgpd.FunctionalTests.Screenings;

/// <summary>
/// Drapeau A2 allumé, <b>l'<c>Operator</c> scanne une base</b> : le rapport vient d'A2, la phase
/// « détection » avance lot par lot, les aperçus s'affichent sans rien décider, une ligne A2
/// s'arbitre comme une autre — et une panne d'Ollama se dit sur l'écran d'attente.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Le moteur n'est pas doublé</b> : c'est le câblage réel qui choisit A2, sur l'artefact
/// embarqué. Seuls le scanner de base et Ollama le sont — le premier au port, le second au fil HTTP.
/// </para>
/// <para>
/// <b>L'hôte est partagé</b> par la collection A2, et il n'y tient qu'un scan à la fois : chaque test
/// fait finir le sien et remet Ollama en état avant de rendre la main.
/// </para>
/// </remarks>
[Collection(A2OnWebCollection.Name)]
public class A2OnTheScannedPath(A2OnWebApplicationFactory factory)
{
  private readonly ScanSurface _scan = new(factory);

  private readonly ScreeningSurface _screening = new(factory);

  public static TheoryData<OllamaFault> TheFaults =>
  [
    OllamaFault.Unreachable,
    OllamaFault.AnotherEncoder,
    OllamaFault.MalformedEmbedding,
    OllamaFault.FailingEmbedding,
  ];

  /// <summary>
  /// Un scan produit un rapport de détection d'A2 ; <b>le même relevé collé produit les mêmes
  /// lignes</b> — catégorie, degré et motif compris.
  /// </summary>
  [Fact]
  public async Task ProducesAnA2ReportWithTheLinesTheSamePastedListingProduces()
  {
    factory.Ollama.Forget();

    await _screening.DepositAndReadTheReportAsync(ScreeningSurface.Paste(
      ScreeningSurface.Column("email", table: "users", position: 1),
      ScreeningSurface.Column("first_name", table: "users", position: 2),
      ScreeningSurface.Column("created_at", table: "users", position: 3),
      ScreeningSurface.Column("price", table: "products", position: 1)));
    var pasted = await CurrentReportAsync();

    (await _scan.ScanAsync(TheScannedListing())).StatusCode.ShouldBe(HttpStatusCode.SeeOther);
    var scanned = await CurrentReportAsync();

    scanned.Origin.ShouldBe(ListingOrigin.Scanned);
    scanned.Engine.ShouldBe("a2-bge-m3-logreg");
    scanned.Lines.ShouldBe(pasted.Lines);
    scanned.Lines.ShouldContain(line => line.Contains("users.email") && line.Contains(nameof(RuleStrength.PrototypeProximity)));
  }

  /// <summary>
  /// La phase « détection » compte les colonnes encodées <b>lot par lot</b> : Ollama retenu après le
  /// premier lot, l'écran d'attente lit « colonne 64 sur 130 ».
  /// </summary>
  [Fact]
  public async Task AdvancesTheDetectionPhaseBatchByBatch()
  {
    factory.Ollama.Forget();
    factory.Scanner.Reset();
    factory.Scanner.Outcome = DatabaseScannerDouble.AListing(
      [.. Enumerable.Range(1, 130).Select(i => ("clients", $"champ_{i}"))]);
    factory.Ollama.HoldAfterBatches(1);

    var scanId = await _scan.LaunchAsync();

    try
    {
      var rendered = await UntilTheWaitingScreenShowsAsync(scanId, "colonne 64 sur 130");

      rendered.ShouldContain($"Phase : <strong>{ScanPhase.Detecting.FrenchLabel}</strong>");
      rendered.ShouldContain("class=\"progress\"");
    }
    finally
    {
      factory.Ollama.Release();
      await _scan.UntilItEndsAsync(scanId);
      factory.Ollama.Forget();
    }
  }

  /// <summary>
  /// ⚠️ <b>Les aperçus s'affichent, et ils ne décident de rien.</b> Le même relevé scanné sous deux
  /// jeux d'aperçus — dont l'un a tout d'une adresse électronique — rend les mêmes lignes, et l'écran
  /// de la table montre les valeurs lues à côté.
  /// </summary>
  [Fact]
  public async Task ShowsThePreviewsAndLetsThemChangeNothingInTheReport()
  {
    factory.Ollama.Forget();

    await _scan.ScanAsync(TheScannedListing());
    var withTheirOwnValues = await CurrentReportAsync();

    await _scan.ScanAsync(TheScannedListingWhereEveryValueLooksLike("jean@exemple.fr"));
    var withEmailsEverywhere = await CurrentReportAsync();

    withEmailsEverywhere.Lines.ShouldBe(withTheirOwnValues.Lines, "Un aperçu a changé ce qu'A2 a détecté.");

    var users = WebUtility.HtmlDecode(await _screening.ReadAsync(ScreeningSurface.TableOf(table: "users")));

    ScreeningSurface.BlockOf(users, "email").ShouldNotBeNull().ShouldContain("jean@exemple.fr");
  }

  /// <summary>
  /// Une ligne A2 s'arbitre <b>exactement comme une ligne du lexique</b> : seule, en <c>Retained</c>,
  /// puis par lot sur les <c>Unflagged</c> de la table ouverte, en <c>SetAside</c>.
  /// </summary>
  [Fact]
  public async Task ArbitratesAnA2LineAloneOrInBatchAsALexiconLine()
  {
    factory.Ollama.Forget();

    await _scan.ScanAsync(TheScannedListing());

    var retained = await _screening.ArbitrateAsync("email", ScreenedColumnState.Retained.Name, table: "users");

    retained.StatusCode.ShouldBe(HttpStatusCode.Redirect);

    var batched = await _screening.ArbitrateInBatchAsync(ScreenedColumnState.SetAside.Name, table: "users");

    batched.StatusCode.ShouldBe(HttpStatusCode.Redirect);

    var users = WebUtility.HtmlDecode(await _screening.ReadAsync(ScreeningSurface.TableOf(table: "users")));

    ScreeningSurface.BlockOf(users, "email").ShouldNotBeNull().ShouldContain("retenue");
    ScreeningSurface.BlockOf(users, "created_at").ShouldNotBeNull().ShouldContain("écartée");

    // Le lot ne touche jamais une colonne signalée : first_name, signalée par A2, attend toujours.
    ScreeningSurface.BlockOf(users, "first_name").ShouldNotBeNull().ShouldNotContain("écartée");
  }

  /// <summary>
  /// ⚠️ <b>Ollama en panne pendant la détection</b> : une fin d'échec nommée — phase « détection »,
  /// famille « moteur de détection indisponible » —, la relance sous la main, aucun
  /// <c>Screening</c> produit, le rapport courant et ses aperçus inchangés, et rien d'Ollama à l'écran.
  /// </summary>
  [Theory]
  [MemberData(nameof(TheFaults))]
  public async Task EndsWithANamedDetectionFailureWhenOllamaFails(OllamaFault fault)
  {
    factory.Ollama.Forget();
    await _scan.ScanAsync(TheScannedListing());
    var settled = await SettledStateAsync();

    string rendered;

    try
    {
      factory.Scanner.Reset();
      factory.Scanner.Outcome = TheScannedListing();
      factory.Ollama.Fault = fault;

      var scanId = await _scan.LaunchAsync();

      (await _scan.UntilItEndsAsync(scanId)).StatusCode.ShouldBe(
        HttpStatusCode.OK, "Une panne du moteur a mené à un rapport.");

      rendered = await _scan.WaitingScreenAsync(scanId);
    }
    finally
    {
      factory.Ollama.Forget();
    }

    rendered.ShouldContain("Scan échoué");
    rendered.ShouldContain($"en phase « {ScanPhase.Detecting.FrenchLabel} »");
    rendered.ShouldContain(ScanFailureFamily.EngineUnavailable.FrenchLabel);
    rendered.ShouldContain(ScanFailureFamily.EngineUnavailable.Statement);
    rendered.ShouldContain(ScanSurface.Connection);
    rendered.ShouldNotContain(ScanFailureFamily.Database.Statement);

    rendered.ShouldNotContain(OllamaDouble.Canary);
    rendered.ShouldNotContain("ollama", Case.Insensitive);
    rendered.ShouldNotContain(OllamaDouble.ForeignDigest[..12]);
    rendered.ShouldNotContain(A2Equivalence.EncoderDigest[..12]);

    (await SettledStateAsync()).ShouldBe(
      settled,
      "Une panne du moteur a laissé un objet : un rapport, une ligne d'historique, ou un jeu d'aperçus "
      + "qui a évincé celui du rapport courant.");
  }

  /// <summary>
  /// Le relevé scanné de ces tests : deux colonnes qu'A2 signale, une qu'il ne signale pas dans la
  /// même table, et une dans une autre table.
  /// </summary>
  private static ScanOutcome TheScannedListing()
  {
    return DatabaseScannerDouble.AListing(
      ("users", "email"), ("users", "first_name"), ("users", "created_at"), ("products", "price"));
  }

  /// <summary>Le même relevé, sous des aperçus qui portent tous la même valeur.</summary>
  private static ScanOutcome TheScannedListingWhereEveryValueLooksLike(string value)
  {
    var listed = TheScannedListing();

    return ScanOutcome.Listed(
      listed.Pivot!,
      listed.Previews.ToDictionary(
        entry => entry.Key,
        _ => ColumnPreview.Read([PreviewedValue.Of(value, value.Length)])));
  }

  /// <summary>
  /// Interroge l'écran d'attente jusqu'à ce qu'il porte <paramref name="expected"/> — on ne dort pas :
  /// la détection court sur un autre fil.
  /// </summary>
  private async Task<string> UntilTheWaitingScreenShowsAsync(string scanId, string expected)
  {
    var rendered = string.Empty;

    for (var attempt = 0; attempt < 200; attempt++)
    {
      rendered = await _scan.WaitingScreenAsync(scanId);

      if (rendered.Contains(expected, StringComparison.Ordinal))
      {
        return rendered;
      }

      await Task.Delay(25);
    }

    throw new InvalidOperationException($"L'écran d'attente n'a jamais porté « {expected} » :\n{rendered}");
  }

  /// <summary>Le rapport courant : son origine, son moteur, et chacune de ses lignes décrite.</summary>
  private async Task<Report> CurrentReportAsync()
  {
    using var scope = factory.Services.CreateScope();
    var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    var current = await database.Screenings.AsNoTracking()
      .Include(screening => screening.Columns)
      .OrderByDescending(screening => screening.LaunchedOn)
      .FirstAsync();

    return new Report(
      current.Origin,
      current.Engine.Name,
      [.. current.Columns
        .Select(line => $"{line.Identity.Table}.{line.Identity.Column} | {line.Category.Name} | {line.Strength?.Name} | {line.Reason}")
        .Order(StringComparer.Ordinal)]);
  }

  /// <summary>Ce qu'une fin d'échec ne doit rien changer : le compte des rapports, le courant, et ses aperçus.</summary>
  private async Task<SettledState> SettledStateAsync()
  {
    using var scope = factory.Services.CreateScope();
    var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    var reports = await database.Screenings.AsNoTracking().CountAsync();
    var current = await database.Screenings.AsNoTracking()
      .OrderByDescending(screening => screening.LaunchedOn)
      .Select(screening => screening.Id)
      .FirstAsync();

    var previews = factory.Services.GetRequiredService<ScanPreviews>();

    return new SettledState(reports, current, previews.Show(current).Previews.Count);
  }

  private sealed record Report(ListingOrigin Origin, string Engine, IReadOnlyList<string> Lines);

  private sealed record SettledState(int Reports, ScreeningId Current, int PreviewsOfCurrent);
}
