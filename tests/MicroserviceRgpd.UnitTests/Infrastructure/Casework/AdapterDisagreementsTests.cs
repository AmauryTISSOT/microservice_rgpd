using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.Adapters;
using MicroserviceRgpd.Infrastructure.Casework.Adapters;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Casework;

/// <summary>
/// Le désaccord <c>Manifest</c>/<c>Adapter</c>, signalé <b>une seule fois, au grain du
/// déploiement</b>.
/// </summary>
/// <remarks>
/// Une panne unique n'est pas N pannes : un secret périmé vaut pour tous les dossiers à la fois, et
/// le crier une fois par dossier ferait dépendre le volume du bruit du nombre de demandes en cours
/// — un chiffre qui n'en dit rien.
/// </remarks>
public class AdapterDisagreementsTests
{
  private static readonly DeclaredSystemId Shop = DeclaredSystemId.From("boutique");
  private static readonly DeclaredSystemId Log = DeclaredSystemId.From("journal");

  /// <summary>Trente-cinq dossiers touchés par la même panne ne font qu'une chose à réparer.</summary>
  [Fact]
  public void SaysTheSameDisagreementOnceAndNeverAgain()
  {
    var said = new RecordingLogger();
    var disagreements = new AdapterDisagreements(said);

    for (var attempt = 0; attempt < 35; attempt++)
    {
      disagreements.Signal(Shop, AdapterVerdict.SecretRefused);
    }

    said.Errors.Count.ShouldBe(1);
    said.Errors[0].ShouldContain("boutique");
  }

  /// <summary>
  /// <b>Le grain est la paire (système, refus).</b> Un <c>Adapter</c> dont le secret est rejeté et
  /// qui, une fois le secret réparé, ne servirait pas le système demandé, a deux choses à dire :
  /// taire la seconde derrière la première ferait réparer une panne pour en découvrir une autre.
  /// </summary>
  [Fact]
  public void KeepsTheTwoRefusalsAndTheTwoSystemsApart()
  {
    var said = new RecordingLogger();
    var disagreements = new AdapterDisagreements(said);

    disagreements.Signal(Shop, AdapterVerdict.SecretRefused);
    disagreements.Signal(Shop, AdapterVerdict.SystemNotServed);
    disagreements.Signal(Log, AdapterVerdict.SecretRefused);
    disagreements.Signal(Shop, AdapterVerdict.SecretRefused);

    said.Errors.Count.ShouldBe(3);
  }

  /// <summary>
  /// Un <c>Adapter</c> qui répond n'est pas en désaccord avec le <c>Manifest</c> : signaler un
  /// verdict servi ou différé est une programmation fautive, jamais un signal discret.
  /// </summary>
  [Theory]
  [InlineData(nameof(AdapterVerdict.Served))]
  [InlineData(nameof(AdapterVerdict.Deferred))]
  public void RefusesToSignalWhatIsNotARefusal(string verdict)
  {
    var disagreements = new AdapterDisagreements(new RecordingLogger());

    Should.Throw<ArgumentException>(() => disagreements.Signal(Shop, AdapterVerdict.FromName(verdict)));
  }

  /// <summary>Ce que l'exploitant aurait lu, gardé plutôt qu'écrit.</summary>
  private sealed class RecordingLogger : ILogger<AdapterDisagreements>
  {
    public List<string> Errors { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
      where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
      LogLevel logLevel,
      EventId eventId,
      TState state,
      Exception? exception,
      Func<TState, Exception?, string> formatter)
    {
      ArgumentNullException.ThrowIfNull(formatter);

      if (logLevel == LogLevel.Error)
      {
        Errors.Add(formatter(state, exception));
      }
    }
  }
}
