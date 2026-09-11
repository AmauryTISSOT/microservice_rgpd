namespace MicroserviceRgpd.Core.Requests;

/// <summary>
/// Où en est une <see cref="DataSubjectRequest"/> : <see cref="InProgress"/>,
/// <see cref="Completed"/> ou <see cref="Cancelled"/>. C'est <b>un état tenu par la demande</b>, et
/// non la trace d'un <c>Gesture</c> (ADR-0021).
/// </summary>
/// <remarks>
/// ⚠️ <b>Aucun <c>Gesture</c> ne fait encore changer le statut</b> : une demande naît
/// <see cref="InProgress"/> et y reste. <see cref="Completed"/> et <see cref="Cancelled"/> sont
/// déclarés sans être atteignables, pour que l'US qui terminera et annulera une demande n'ait pas à
/// migrer la colonne.
/// </remarks>
public sealed class RequestStatus : SmartEnum<RequestStatus>
{
  /// <summary>La demande attend encore une réponse. C'est le statut de toute demande qui naît.</summary>
  public static readonly RequestStatus InProgress = new(nameof(InProgress), 0, "En cours");

  /// <summary>Le responsable a répondu à la demande.</summary>
  public static readonly RequestStatus Completed = new(nameof(Completed), 1, "Terminée");

  /// <summary>
  /// Le responsable n'instruira pas la demande, et la conserve. Ne se confond pas avec la
  /// suppression : une demande annulée reste dans le service.
  /// </summary>
  public static readonly RequestStatus Cancelled = new(nameof(Cancelled), 2, "Annulée");

  private RequestStatus(string name, int value, string frenchLabel)
    : base(name, value)
  {
    FrenchLabel = frenchLabel;
  }

  /// <summary>Le libellé destiné à l'<c>Operator</c>. Le français reste hors des identifiants.</summary>
  public string FrenchLabel { get; }
}
