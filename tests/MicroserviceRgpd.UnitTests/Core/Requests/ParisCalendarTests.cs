using MicroserviceRgpd.Core.Requests;

namespace MicroserviceRgpd.UnitTests.Core.Requests;

/// <summary>
/// « Aujourd'hui » s'entend <b>à l'heure de Paris</b>, jamais à celle de la machine ni en UTC : la
/// date de réception d'une demande se juge au jour du responsable. Les bords de minuit sont ceux
/// où l'UTC et Paris ne tombent pas le même jour — une heure d'écart l'hiver, deux l'été.
/// </summary>
public class ParisCalendarTests
{
  /// <summary>L'hiver, Paris vit à UTC+1 : 23 h 30 UTC la veille est déjà 0 h 30 le lendemain.</summary>
  [Fact]
  public void TurnsTheDayAtParisMidnightInWinter()
  {
    ParisCalendar.Today(new AClockStuckAt(new DateTimeOffset(2026, 1, 14, 23, 30, 0, TimeSpan.Zero)))
      .ShouldBe(new DateOnly(2026, 1, 15));
    ParisCalendar.Today(new AClockStuckAt(new DateTimeOffset(2026, 1, 14, 22, 59, 0, TimeSpan.Zero)))
      .ShouldBe(new DateOnly(2026, 1, 14));
  }

  /// <summary>L'été, Paris vit à UTC+2 : 22 h 30 UTC est déjà le lendemain, 21 h 59 pas encore.</summary>
  [Fact]
  public void TurnsTheDayAtParisMidnightInSummer()
  {
    ParisCalendar.Today(new AClockStuckAt(new DateTimeOffset(2026, 7, 14, 23, 30, 0, TimeSpan.Zero)))
      .ShouldBe(new DateOnly(2026, 7, 15));
    ParisCalendar.Today(new AClockStuckAt(new DateTimeOffset(2026, 7, 14, 22, 30, 0, TimeSpan.Zero)))
      .ShouldBe(new DateOnly(2026, 7, 15));
    ParisCalendar.Today(new AClockStuckAt(new DateTimeOffset(2026, 7, 14, 21, 59, 0, TimeSpan.Zero)))
      .ShouldBe(new DateOnly(2026, 7, 14));
  }

  /// <summary>
  /// <b>Le fuseau se résout sur la plateforme qui exécute le service.</b> Sans données de fuseau,
  /// le calcul lèverait au premier appel plutôt que de retomber en silence sur l'UTC.
  /// </summary>
  [Fact]
  public void ResolvesEuropeParisOnThisPlatform()
  {
    ParisCalendar.TimeZone.BaseUtcOffset.ShouldBe(TimeSpan.FromHours(1));
    ParisCalendar.TimeZone.SupportsDaylightSavingTime.ShouldBeTrue();
  }
}
