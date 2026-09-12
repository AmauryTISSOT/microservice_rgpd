using MicroserviceRgpd.Core.Requests;

namespace MicroserviceRgpd.UnitTests.Core.Requests;

/// <summary>
/// Le signalement de la date limite de réponse : <b>« Échéance proche »</b> de aujourd'hui à
/// aujourd'hui plus sept jours, bornes comprises, <b>« En retard »</b> avant aujourd'hui — et rien
/// pour une demande qui n'est plus <see cref="RequestStatus.InProgress"/> (ADR-0021).
/// </summary>
public class DeadlineSignalTests
{
  private static readonly DateOnly Today = new(2026, 3, 10);

  [Fact]
  public void ListsExactlyTwoSignals()
  {
    DeadlineSignal.List.Select(signal => signal.Name).ShouldBe(["DueSoon", "Overdue"], ignoreOrder: true);
  }

  [Theory]
  [InlineData("DueSoon", "Échéance proche")]
  [InlineData("Overdue", "En retard")]
  public void CarriesTheFrenchLabelOfEachSignal(string name, string frenchLabel)
  {
    DeadlineSignal.FromName(name).FrenchLabel.ShouldBe(frenchLabel);
  }

  /// <summary>
  /// ⚠️ <b>Le jour même de la date limite, la demande est en échéance proche, pas en retard</b> ; elle
  /// l'est jusqu'à sept jours plus tard, et ne l'est plus au huitième.
  /// </summary>
  [Theory]
  [InlineData(-30, "Overdue")]
  [InlineData(-1, "Overdue")]
  [InlineData(0, "DueSoon")]
  [InlineData(1, "DueSoon")]
  [InlineData(7, "DueSoon")]
  [InlineData(8, null)]
  [InlineData(30, null)]
  public void SignalsARequestInProgressByItsDeadlineAgainstToday(int daysFromToday, string? expected)
  {
    (DeadlineSignal.Of(RequestStatus.InProgress, Today.AddDays(daysFromToday), Today)?.Name).ShouldBe(expected);
  }

  /// <summary>Une demande Terminée ou Annulée n'est jamais signalée, fût-elle au-delà de sa date limite.</summary>
  [Theory]
  [InlineData("Completed", -1)]
  [InlineData("Completed", 0)]
  [InlineData("Cancelled", -1)]
  [InlineData("Cancelled", 3)]
  public void NeverSignalsARequestThatIsNoLongerInProgress(string status, int daysFromToday)
  {
    DeadlineSignal.Of(RequestStatus.FromName(status), Today.AddDays(daysFromToday), Today).ShouldBeNull();
  }

  [Fact]
  public void RefusesAMissingStatus()
  {
    Should.Throw<ArgumentNullException>(() => DeadlineSignal.Of(null!, Today, Today));
  }
}
