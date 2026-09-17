namespace MicroserviceRgpd.Core.Requests;

/// <summary>
/// Le <b>résultat d'une tentative d'exécution</b> : <see cref="Succeeded"/>,
/// <see cref="NonSuccessResponse"/>, <see cref="TimedOut"/>, <see cref="NetworkError"/>,
/// <see cref="SucceededButNotRecorded"/>, <see cref="Unroutable"/> ou <see cref="Rejected"/>
/// (ADR-0026, ADR-0028).
/// </summary>
/// <remarks>
/// <para>
/// Une valeur fermée, pour que le journal d'exécution se compte et se filtre sans interpréter du
/// texte. Stockée par son nom, comme les autres vocabulaires fermés du dépôt.
/// </para>
/// <para>
/// ⚠️ <b>Une seule valeur pour les deux canaux</b> (ADR-0028) : <see cref="Succeeded"/>,
/// <see cref="TimedOut"/>, <see cref="NetworkError"/> et <see cref="SucceededButNotRecorded"/> se
/// disent d'un appel comme d'une publication. <see cref="NonSuccessResponse"/> n'a de sens que sur
/// HTTP, où il y a une réponse ; <see cref="Unroutable"/> et <see cref="Rejected"/> n'en ont que
/// sur un bus, qui seul peut refuser un message ou n'avoir personne à qui le remettre.
/// </para>
/// </remarks>
public sealed class ExecutionOutcome : SmartEnum<ExecutionOutcome>
{
  /// <summary>
  /// La remise a abouti : le destinataire a répondu 2xx, ou le broker a accusé réception. La demande
  /// est passée à Terminée.
  /// </summary>
  public static readonly ExecutionOutcome Succeeded = new(nameof(Succeeded), 0, "Succès");

  /// <summary>Le système hôte a répondu, mais pas 2xx — une redirection comprise, qui n'est pas suivie.</summary>
  public static readonly ExecutionOutcome NonSuccessResponse = new(nameof(NonSuccessResponse), 1, "Réponse non 2xx");

  /// <summary>
  /// La remise n'a rien reçu dans le délai : pas de réponse dans <c>HostSystem:TimeoutSeconds</c>,
  /// ou pas de confirmation dans <c>RabbitMq:PublishTimeoutSeconds</c>.
  /// </summary>
  public static readonly ExecutionOutcome TimedOut = new(nameof(TimedOut), 2, "Délai dépassé");

  /// <summary>Le destinataire — le système hôte, ou le broker — n'a pas pu être joint.</summary>
  public static readonly ExecutionOutcome NetworkError = new(nameof(NetworkError), 3, "Erreur réseau");

  /// <summary>
  /// La remise a abouti, mais la demande n'a pas pu passer à Terminée : le droit est remis, et le
  /// service ne l'a pas enregistré.
  /// </summary>
  public static readonly ExecutionOutcome SucceededButNotRecorded = new(nameof(SucceededButNotRecorded), 4, "Succès non enregistré");

  /// <summary>
  /// Le message a été publié, et <b>aucune file ne l'a reçu</b> : le broker l'a rendu au service.
  /// ⚠️ Ce cas n'existe que parce que la publication pose le drapeau <c>mandatory</c> ; sans lui, le
  /// message disparaîtrait en silence et la demande passerait à Terminée (ADR-0028).
  /// </summary>
  public static readonly ExecutionOutcome Unroutable = new(nameof(Unroutable), 5, "Message non routable");

  /// <summary>Le broker a <b>refusé</b> la publication — un <c>nack</c>.</summary>
  public static readonly ExecutionOutcome Rejected = new(nameof(Rejected), 6, "Publication refusée par le broker");

  private ExecutionOutcome(string name, int value, string frenchLabel)
    : base(name, value)
  {
    FrenchLabel = frenchLabel;
  }

  /// <summary>Le libellé destiné à l'<c>Operator</c>. Le français reste hors des identifiants.</summary>
  public string FrenchLabel { get; }
}
