using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.TestDoubles.Ollama;
using MicroserviceRgpd.UnitTests.Core.Screenings;
using Microsoft.Extensions.Logging;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Screenings;

/// <summary>
/// Les pannes d'Ollama, éprouvées <b>par le port</b> que le câblage rend : une famille unique
/// « moteur de détection indisponible », la cause fine au journal, et jamais un rapport.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Aucun texte d'Ollama ne traverse l'exception.</b> Chaque panne du double glisse un canari,
/// l'adresse ou un digest dans ce qu'Ollama dit ; l'exception qui sort du port n'en porte aucun, et
/// n'accroche aucune exception interne qui les recopierait.
/// </para>
/// <para>
/// ⚠️ <b>Le lexique ne répond jamais à la place d'A2.</b> Le port lève : il ne rend pas un rapport
/// d'un autre moteur, et c'est ce que chaque cas exige en exigeant l'exception.
/// </para>
/// </remarks>
public class A2ScreeningEngineFailureTests
{
  /// <summary>Une échéance assez courte pour qu'un Ollama muet la franchisse sans ralentir la suite.</summary>
  private const string AShortDeadline = "0.2";

  /// <summary>
  /// ⚠️ <b>L'échéance courte n'est posée que là où c'est elle qu'on éprouve.</b> Sous la charge de la
  /// suite entière, encoder 130 colonnes peut dépasser deux dixièmes de seconde : une panne de
  /// réponse illisible se serait lue comme une échéance dépassée.
  /// </summary>
  private static string DeadlineFor(OllamaFault fault)
  {
    return fault is OllamaFault.NeverAnswers or OllamaFault.StallsAfterHeaders ? AShortDeadline : "30";
  }

  public static TheoryData<OllamaFault, string> TheFaultsAndTheCauseTheLogNames => new()
  {
    { OllamaFault.Unreachable, "injoignable" },
    { OllamaFault.NeverAnswers, "échéance dépassée" },
    { OllamaFault.StallsAfterHeaders, "échéance dépassée" },
    { OllamaFault.AnotherEncoder, "encodeur non conforme" },
    { OllamaFault.MalformedEmbedding, "encodeur non conforme" },
    { OllamaFault.FailingEmbedding, "encodeur non conforme" },
    { OllamaFault.OneVectorShortOnTheSecondBatch, "encodeur non conforme" },
  };

  public static TheoryData<OllamaFault> TheFaults =>
    [.. Enum.GetValues<OllamaFault>().Where(fault => fault != OllamaFault.None)];

  /// <summary>
  /// ⚠️ <b>Un autre encodeur servi sous le même tag : aucun texte n'est encodé.</b> Ses vecteurs
  /// n'auraient jamais vu la régression, et les envoyer serait déjà lui confier les noms du client
  /// pour rien.
  /// </summary>
  [Fact]
  public async Task EncodesNothingWhenOllamaServesAnotherEncoder()
  {
    var ollama = new OllamaDouble { Fault = OllamaFault.AnotherEncoder };

    await Should.ThrowAsync<ScreeningEngineUnavailable>(
      () => AScreeningEngine.WiredToA2(ollama, new RecordedLogs()).ScreenAsync(AListing(3), IScreeningEngine.NoPreviews));

    ollama.EmbedBodies.ShouldBeEmpty();
  }

  /// <summary>
  /// Chaque panne sort <b>nommée</b>, dans la famille unique, <b>sans rapport</b> — pas même celui
  /// des lots déjà encodés — et sans rien d'Ollama dedans.
  /// </summary>
  [Theory]
  [MemberData(nameof(TheFaults))]
  public async Task FailsNamedWithoutAPartialReportNorAnyTextFromOllama(OllamaFault fault)
  {
    // 130 colonnes : trois lots, pour que la panne du second lot suive un lot rendu sans faute.
    var failure = await Should.ThrowAsync<ScreeningEngineUnavailable>(
      () => AScreeningEngine.WiredToA2(new OllamaDouble { Fault = fault }, new RecordedLogs(), DeadlineFor(fault))
        .ScreenAsync(AListing(130), IScreeningEngine.NoPreviews));

    failure.InnerException.ShouldBeNull("Une exception interne recopierait le message d'Ollama et son adresse.");
    failure.Message.ShouldContain(ScreeningEngineUnavailable.FrenchLabel, Case.Insensitive);
    failure.Message.ShouldNotContain(OllamaDouble.Canary);
    failure.Message.ShouldNotContain("ollama", Case.Insensitive);
    failure.Message.ShouldNotContain("http", Case.Insensitive);
    failure.Message.ShouldNotContain(OllamaDouble.ForeignDigest[..12]);
    failure.Message.ShouldNotContain(A2Equivalence.EncoderDigest[..12]);
  }

  /// <summary>
  /// ⚠️ <b>Le journal distingue les trois causes</b> — « injoignable », « échéance dépassée »,
  /// « encodeur non conforme » —, en propriété structurée : l'exploitant ne corrige pas la même
  /// chose selon la réponse.
  /// </summary>
  [Theory]
  [MemberData(nameof(TheFaultsAndTheCauseTheLogNames))]
  public async Task NamesTheCauseInTheLog(OllamaFault fault, string cause)
  {
    var logs = new RecordedLogs();

    await Should.ThrowAsync<ScreeningEngineUnavailable>(
      () => AScreeningEngine.WiredToA2(new OllamaDouble { Fault = fault }, logs, DeadlineFor(fault))
        .ScreenAsync(AListing(130), IScreeningEngine.NoPreviews));

    var logged = logs.Written.Where(entry => entry.Properties.ContainsKey("Cause")).ShouldHaveSingleItem();

    logged.Level.ShouldBe(LogLevel.Warning);
    logged.Properties["Cause"].ShouldBe(cause);
    logged.Rendered.ShouldContain(cause);
  }

  /// <summary>
  /// <b>L'appelant parti n'est pas un moteur en panne</b> : son annulation ressort comme une
  /// annulation, ni comptée ni journalisée comme une indisponibilité.
  /// </summary>
  [Fact]
  public async Task LetsTheCallersCancellationThroughAsACancellation()
  {
    var logs = new RecordedLogs();
    using var leaving = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

    var thrown = await Record.ExceptionAsync(
      () => AScreeningEngine.WiredToA2(new OllamaDouble { Fault = OllamaFault.NeverAnswers }, logs)
        .ScreenAsync(AListing(1), IScreeningEngine.NoPreviews, leaving.Token));

    thrown.ShouldBeAssignableTo<OperationCanceledException>();
    logs.Written.ShouldNotContain(entry => entry.Properties.ContainsKey("Cause"));
  }

  /// <summary>Un relevé collé de <paramref name="count"/> colonnes, toutes dans une table.</summary>
  private static ColumnListing AListing(int count)
  {
    var outcome = ColumnListingIngestion.Ingest(APivot.Paste(
      [.. Enumerable.Range(1, count).Select(i => APivot.Column($"champ_{i}", table: "clients", position: i))]));

    outcome.Refusal.ShouldBeNull();

    return outcome.Listing!;
  }
}
