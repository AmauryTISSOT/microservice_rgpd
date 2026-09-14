namespace MicroserviceRgpd.Core.Requests;

/// <summary>
/// Où en est une <see cref="DataSubjectRequest"/> : <see cref="InProgress"/>,
/// <see cref="Completed"/> ou <see cref="Cancelled"/>. C'est <b>un état tenu par la demande</b>, et
/// non la trace d'un <c>Gesture</c> (ADR-0021).
/// </summary>
/// <remarks>
/// Une demande naît <see cref="InProgress"/>, et passe à <see cref="Completed"/> quand son exécution
/// réussit (ADR-0026). ⚠️ <see cref="Cancelled"/> est déclaré sans être atteignable : annuler une
/// demande reste fermé.
/// </remarks>
public sealed class RequestStatus : SmartEnum<RequestStatus>
{
  /// <summary>La demande attend encore une réponse. C'est le statut de toute demande qui naît.</summary>
  public static readonly RequestStatus InProgress = new(nameof(InProgress), 0, "En cours");

  /// <summary>Le droit invoqué a été appliqué par le système hôte.</summary>
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
