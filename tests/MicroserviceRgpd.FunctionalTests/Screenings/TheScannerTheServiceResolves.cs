using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.FunctionalTests.Screenings;

/// <summary>
/// Le port de scan est <b>doublé</b> dans l'hôte de test, et la doublure sait jouer tout ce qu'une
/// base réelle ferait vivre à l'<c>Operator</c>.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Aucun test fonctionnel n'ouvre de base réelle.</b> Le scanner est le seul point du contexte
/// qui touche une base d'un tiers ; le doubler sur le port est ce qui permet d'éprouver les écrans
/// de scan sans conteneur, sans réseau, et sans qu'un test dépende de ce qu'un SGBD tiers a
/// justement dans le ventre ce jour-là.
/// </para>
/// <para>
/// Les écrans qui consommeront ces scénarios viennent avec #308 et #309. Ce qui est prouvé ici,
/// c'est que la doublure <b>est</b> celle que le service résout, et qu'elle sait déjà rendre les
/// quatre fins — sans quoi ces tickets découvriraient au dernier moment qu'ils n'ont rien pour
/// éprouver leurs écrans.
/// </para>
/// </remarks>
[Collection(WebCollection.Name)]
public class TheScannerTheServiceResolves(CustomWebApplicationFactory<Program> factory)
{
  private readonly CustomWebApplicationFactory<Program> _factory = factory;

  private IDatabaseScanner Resolved =>
    _factory.Services.GetRequiredService<IDatabaseScanner>();

  [Fact]
  public void IsTheDoubleAndNotTheDriver()
  {
    Resolved.ShouldBeSameAs(_factory.Scanner);
  }

  /// <summary>
  /// Un relevé dicté repasse par l'ingestion, comme un collage : c'est ce qui garantit qu'un test
  /// d'écran ne s'appuie jamais sur un pivot que le domaine refuserait.
  /// </summary>
  [Fact]
  public async Task RendersAPivotIngestionAccepts()
  {
    _factory.Scanner.Reset();
    _factory.Scanner.Outcome = DatabaseScannerDouble.AListing(
      ("abonne", "courriel"),
      ("abonne", "nom"),
      ("commande", "montant"));

    var outcome = await Resolved.ScanAsync(DatabaseDialect.PostgreSql, "Host=nulle-part");

    outcome.Ending.ShouldBe(ScanEnding.Listed);

    var ingested = ColumnListingIngestion.Ingest(outcome.Pivot);

    ingested.IsAccepted.ShouldBeTrue(ingested.Refusal?.Observed);
    ingested.Listing!.ColumnCount.ShouldBe(3);
    outcome.Previews.Count.ShouldBe(3);

    _factory.Scanner.ReceivedDialect.ShouldBe(DatabaseDialect.PostgreSql);
    _factory.Scanner.ReceivedConnectionString.ShouldBe("Host=nulle-part");
    _factory.Scanner.Reset();
  }

  /// <summary>Une colonne peut porter une raison plutôt que des valeurs, et l'écran devra la dire.</summary>
  [Fact]
  public async Task RendersAPreviewThatCarriesAReasonInsteadOfValues()
  {
    _factory.Scanner.Reset();
    _factory.Scanner.Outcome = DatabaseScannerDouble.AListingWhere(
      "abonne",
      "photo",
      PreviewAbsenceReason.UnsampleableType,
      ("abonne", "courriel"),
      ("abonne", "photo"));

    var outcome = await Resolved.ScanAsync(DatabaseDialect.Sqlite, "Data Source=galette.db");

    outcome.Previews[ColumnIdentity.Of("public", "abonne", "photo")]
      .Absence.ShouldBe(PreviewAbsenceReason.UnsampleableType);
    outcome.Previews[ColumnIdentity.Of("public", "abonne", "courriel")]
      .CarriesValues.ShouldBeTrue();
    _factory.Scanner.Reset();
  }

  /// <summary>
  /// ⚠️ <b>Les deux fins à zéro objet se dictent séparément.</b> Une doublure qui n'en connaîtrait
  /// qu'une laisserait l'écran qui les distingue sans aucun test.
  /// </summary>
  [Theory]
  [MemberData(nameof(TheTwoEmptyEndings))]
  public async Task RendersEachOfTheTwoEmptyEndings(ScanOutcome dictated, ScanEnding expected)
  {
    _factory.Scanner.Reset();
    _factory.Scanner.Outcome = dictated;

    var outcome = await Resolved.ScanAsync(DatabaseDialect.Sqlite, "Data Source=galette.db");

    outcome.Ending.ShouldBe(expected);
    outcome.Pivot.ShouldBeNull();
    _factory.Scanner.Reset();
  }

  public static TheoryData<ScanOutcome, ScanEnding> TheTwoEmptyEndings =>
    new()
    {
      { ScanOutcome.NoTable(), ScanEnding.NoTable },
      {
        ScanOutcome.DatabaseAbsentFromCatalogue(),
        ScanEnding.DatabaseAbsentFromCatalogue
      },
    };

  /// <summary>Un échec en cours de route, à phase et famille nommées, et rien d'autre.</summary>
  [Fact]
  public async Task RendersAFailureThatNamesItsPhaseAndItsFamily()
  {
    _factory.Scanner.Reset();
    _factory.Scanner.Outcome = ScanOutcome.Failed(ScanPhase.Sampling, ScanFailureFamily.Network);

    var outcome = await Resolved.ScanAsync(DatabaseDialect.MySql, "Server=nulle-part");

    outcome.Ending.ShouldBe(ScanEnding.Failed);
    outcome.Failure!.Phase.ShouldBe(ScanPhase.Sampling);
    outcome.Failure.Family.ShouldBe(ScanFailureFamily.Network);
    _factory.Scanner.Reset();
  }

  /// <summary>
  /// La doublure sait <b>faire durer</b> sa réponse, et l'annulation de l'appelant interrompt un
  /// travail en cours — ce qu'un scanner instantané ne laisserait jamais observer.
  /// </summary>
  [Fact]
  public async Task DelaysItsAnswerAndLetsTheCallerCutIt()
  {
    _factory.Scanner.Reset();
    _factory.Scanner.Delay = TimeSpan.FromSeconds(30);
    _factory.Scanner.Steps = [ScanStep.CatalogueRead(3)];

    using var abandon = new CancellationTokenSource();
    var record = new List<ScanStep>();

    var scanning = Resolved.ScanAsync(
      DatabaseDialect.Sqlite,
      "Data Source=galette.db",
      new SynchronousProgress(record.Add),
      abandon.Token);

    await _factory.Scanner.Started;
    await abandon.CancelAsync();

    await Should.ThrowAsync<OperationCanceledException>(() => scanning);

    _factory.Scanner.Interrupted.ShouldBeTrue();
    record.ShouldHaveSingleItem().Phase.ShouldBe(ScanPhase.Cataloguing);
    _factory.Scanner.Reset();
  }

  /// <summary>
  /// Rapporte les pas sur le fil qui les rapporte : <see cref="Progress{T}"/> les posterait sur le
  /// pool, et le test lirait sa liste avant qu'elle ne soit remplie.
  /// </summary>
  private sealed class SynchronousProgress(Action<ScanStep> onStep) : IProgress<ScanStep>
  {
    public void Report(ScanStep value) => onStep(value);
  }
}
