using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UnitTests.Core.Screenings;

/// <summary>
/// <b>Un seul scan en vol par déploiement</b>, et le moyen de retrouver celui qui court — ou celui
/// qui vient de finir.
/// </summary>
public class ScansInFlightTests
{
  private static readonly DateTimeOffset StartedOn =
    new(2026, 8, 26, 14, 5, 30, TimeSpan.Zero);

  /// <summary>La place est libre au départ : le premier scan la prend.</summary>
  [Fact]
  public void LetsTheFirstScanTakeOff()
  {
    var inFlight = new ScansInFlight();
    var first = AScan();

    inFlight.TryTakeOff(first, out var alreadyRunning).ShouldBeTrue();

    alreadyRunning.ShouldBeNull();
    inFlight.Running.ShouldBe(first);
  }

  /// <summary>
  /// ⚠️ <b>Le second est refusé, et le refus NOMME celui qui court.</b> Sans ce nom, l'<c>Operator</c>
  /// ne sait pas si le service travaille pour lui ou pour quelqu'un d'autre.
  /// </summary>
  [Fact]
  public void RefusesASecondScanAndNamesTheOneAlreadyRunning()
  {
    var inFlight = new ScansInFlight();
    var first = AScan();

    inFlight.TryTakeOff(first, out _);

    inFlight.TryTakeOff(AScan(), out var alreadyRunning).ShouldBeFalse();

    alreadyRunning.ShouldBe(first);
  }

  /// <summary>Le scan fini libère la place : une base injoignable ne bloque pas le déploiement.</summary>
  [Fact]
  public void LetsANewScanTakeOffOnceTheLastOneHasEnded()
  {
    var inFlight = new ScansInFlight();
    var first = AScan();

    inFlight.TryTakeOff(first, out _);
    first.EndedWithoutAReport(ScanEnding.NoTable);

    inFlight.Running.ShouldBeNull();
    inFlight.TryTakeOff(AScan(), out _).ShouldBeTrue();
  }

  /// <summary>
  /// ⚠️ <b>Le dernier scan reste retrouvable après sa fin, et c'est ce qui rend le <c>303</c>
  /// possible.</b> L'écran d'attente doit pouvoir répondre « c'est fini, voici le rapport » à qui
  /// arrive une seconde après la dernière ligne écrite.
  /// </summary>
  [Fact]
  public void StillFindsTheLastScanAfterItHasEnded()
  {
    var inFlight = new ScansInFlight();
    var scan = AScan();

    inFlight.TryTakeOff(scan, out _);
    scan.Produced(ScreeningId.Next());

    inFlight.Find(scan.Id).ShouldBe(scan);
  }

  /// <summary>Un seul est retenu : le précédent s'efface quand le suivant part.</summary>
  [Fact]
  public void ForgetsThePreviousScanWhenTheNextOneTakesOff()
  {
    var inFlight = new ScansInFlight();
    var first = AScan();

    inFlight.TryTakeOff(first, out _);
    first.Produced(ScreeningId.Next());
    inFlight.TryTakeOff(AScan(), out _);

    inFlight.Find(first.Id).ShouldBeNull();
  }

  /// <summary>Une identité que le processus ne connaît pas ne rend rien à inventer.</summary>
  [Fact]
  public void FindsNothingForAnUnknownScan()
  {
    new ScansInFlight().Find(ScanId.Next()).ShouldBeNull();
  }

  private static ScanProgress AScan()
  {
    return ScanProgress.Starting(ScanId.Next(), StartedOn);
  }
}
