namespace MicroserviceRgpd.Core.Requests;

/// <summary>
/// <b>« Aujourd'hui à Paris »</b>, calculé en un seul endroit. La date de réception d'une demande se
/// juge au jour du responsable, qui s'entend à l'heure de Paris : ni à celle de la machine qui
/// exécute le service, ni en UTC. Le handler s'en sert pour refuser une date future, et la page
/// pour rendre la valeur par défaut et le <c>max</c> du champ de date.
/// </summary>
/// <remarks>
/// ⚠️ <b>Le fuseau <c>Europe/Paris</c> doit se résoudre sur la plateforme.</b> Linux et macOS le
/// lisent dans <c>/usr/share/zoneinfo</c> ; Windows le traduit par ICU. Une image sans données de
/// fuseau — une image <i>chiseled</i> ou Alpine sans <c>tzdata</c> — ferait lever ce type à sa
/// première lecture, et c'est voulu : retomber en silence sur l'UTC décalerait le jour d'une heure
/// l'hiver, de deux l'été, et laisserait passer une date de réception future.
/// </remarks>
public static class ParisCalendar
{
  /// <summary>Le fuseau du responsable, heure d'été comprise.</summary>
  public static TimeZoneInfo TimeZone { get; } = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");

  /// <summary>Le jour qu'il est à Paris à l'instant que rend <paramref name="clock"/>.</summary>
  public static DateOnly Today(TimeProvider clock)
  {
    ArgumentNullException.ThrowIfNull(clock);

    return DateOf(clock.GetUtcNow());
  }

  /// <summary>
  /// Le jour qu'il est à Paris à <paramref name="instant"/> — pour qui a déjà lu l'horloge et doit
  /// dater deux choses du même instant.
  /// </summary>
  public static DateOnly DateOf(DateTimeOffset instant) =>
    DateOnly.FromDateTime(InParis(instant).DateTime);

  /// <summary>
  /// <paramref name="instant"/> tel que l'heure de Paris le lit — pour qui affiche l'heure d'un
  /// instant enregistré en UTC.
  /// </summary>
  public static DateTimeOffset InParis(DateTimeOffset instant) =>
    TimeZoneInfo.ConvertTime(instant, TimeZone);
}
