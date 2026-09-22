using MicroserviceRgpd.UseCases.Screenings;

namespace MicroserviceRgpd.UnitTests.UseCases.Screenings;

/// <summary>
/// L'avancement de l'arbitrage : un compte sur un total, et <b>deux restes qui ne se fondent pas</b>.
/// </summary>
public class ArbitrationProgressTests
{
  /// <summary>
  /// Le total est <b>toutes</b> les colonnes — signalées ou non — et le reste se partage entre les
  /// signalées et celles où rien n'a été vu, ces dernières étant celles du verrou.
  /// </summary>
  [Fact]
  public void CountsEveryColumnAndSplitsWhatRemainsBetweenFlaggedAndUnflagged()
  {
    var progress = ArbitrationProgress.Of(
      new ScreeningTally(Flagged: 10, Retained: 4, SetAside: 3, Awaiting: 30, RetainedOnUnflagged: 1),
      new UnfinishedScreening(UnreadUnflagged: 26, Awaiting: 30));

    progress.Columns.ShouldBe(37);
    progress.Settled.ShouldBe(7);
    progress.FlaggedAwaiting.ShouldBe(4);
    progress.UnflaggedAwaiting.ShouldBe(26);
    progress.Awaiting.ShouldBe(30);
    progress.IsComplete.ShouldBeFalse();
    progress.SettledPerMille.ShouldBe(189);
  }

  /// <summary>Plus rien n'attend : l'arbitrage est achevé, et la barre est pleine.</summary>
  [Fact]
  public void IsCompleteOnlyOnceNothingAwaits()
  {
    var progress = ArbitrationProgress.Of(
      new ScreeningTally(Flagged: 2, Retained: 2, SetAside: 3, Awaiting: 0, RetainedOnUnflagged: 0),
      new UnfinishedScreening(UnreadUnflagged: 0, Awaiting: 0));

    progress.IsComplete.ShouldBeTrue();
    progress.SettledPerMille.ShouldBe(1000);
  }

  /// <summary>
  /// ⚠️ <b>Un rapport sans colonne n'est pas « achevé »</b> : il n'y a rien dont l'export puisse
  /// dire qu'on l'a tranché, et la barre ne divise pas par zéro.
  /// </summary>
  [Fact]
  public void DoesNotCallAnEmptyReportComplete()
  {
    var progress = ArbitrationProgress.Of(
      new ScreeningTally(0, 0, 0, 0, 0),
      new UnfinishedScreening(UnreadUnflagged: 0, Awaiting: 0));

    progress.IsComplete.ShouldBeFalse();
    progress.SettledPerMille.ShouldBe(0);
  }
}
