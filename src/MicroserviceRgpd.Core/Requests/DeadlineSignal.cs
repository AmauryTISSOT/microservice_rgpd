namespace MicroserviceRgpd.Core.Requests;

/// <summary>
/// Le <b>signalement de la date limite de réponse</b> d'une demande <see cref="RequestStatus.InProgress"/> :
/// <see cref="DueSoon"/> ou <see cref="Overdue"/>, ou aucun (ADR-0021).
/// </summary>
/// <remarks>
/// ⚠️ <b>Un signalement n'est pas un statut</b> : rien ne l'enregistre. Il se lit à l'instant où l'on
/// regarde, depuis la date limite et « aujourd'hui » à Paris — voir <see cref="ParisCalendar"/>. La
/// règle n'est écrite qu'ici : le script de l'écran ne la recalcule pas.
/// </remarks>
public sealed class DeadlineSignal : SmartEnum<DeadlineSignal>
{
  /// <summary>La date limite tombe entre aujourd'hui et aujourd'hui plus <see cref="DueSoonWindowInDays"/> jours, bornes comprises.</summary>
  public static readonly DeadlineSignal DueSoon = new(nameof(DueSoon), 0, "Échéance proche");

  /// <summary>La date limite est antérieure à aujourd'hui.</summary>
  public static readonly DeadlineSignal Overdue = new(nameof(Overdue), 1, "En retard");

  /// <summary>Combien de jours après aujourd'hui une date limite est encore « proche ».</summary>
  public const int DueSoonWindowInDays = 7;

  private DeadlineSignal(string name, int value, string frenchLabel)
    : base(name, value)
  {
    FrenchLabel = frenchLabel;
  }

  /// <summary>Le libellé destiné à l'<c>Operator</c>. Le français reste hors des identifiants.</summary>
  public string FrenchLabel { get; }

  /// <summary>
  /// Le signalement d'une demande au statut <paramref name="status"/> dont la date limite est
  /// <paramref name="responseDeadline"/>, <paramref name="todayInParis"/> — ou <c>null</c> : une demande
  /// qui n'est plus en cours n'est jamais signalée.
  /// </summary>
  /// <remarks>Le jour même de la date limite, la demande est en échéance proche, pas en retard.</remarks>
  /// <exception cref="ArgumentNullException"><paramref name="status"/> est absent.</exception>
  public static DeadlineSignal? Of(RequestStatus status, DateOnly responseDeadline, DateOnly todayInParis)
  {
    ArgumentNullException.ThrowIfNull(status);

    if (status != RequestStatus.InProgress)
    {
      return null;
    }

    if (responseDeadline < todayInParis)
    {
      return Overdue;
    }

    return responseDeadline <= todayInParis.AddDays(DueSoonWindowInDays) ? DueSoon : null;
  }
}
