namespace MicroserviceRgpd.Core.Requests;

/// <summary>
/// Le <b>résultat d'une tentative d'exécution</b> : <see cref="Succeeded"/>,
/// <see cref="NonSuccessResponse"/>, <see cref="TimedOut"/>, <see cref="NetworkError"/> ou
/// <see cref="SucceededButNotRecorded"/> (ADR-0026).
/// </summary>
/// <remarks>
/// Une valeur fermée, pour que le journal d'exécution se compte et se filtre sans interpréter du
/// texte. Stockée par son nom, comme les autres vocabulaires fermés du dépôt.
/// </remarks>
public sealed class ExecutionOutcome : SmartEnum<ExecutionOutcome>
{
  /// <summary>Le système hôte a répondu 2xx : il a appliqué le droit, et la demande est passée à Terminée.</summary>
  public static readonly ExecutionOutcome Succeeded = new(nameof(Succeeded), 0, "Succès");

  /// <summary>Le système hôte a répondu, mais pas 2xx — une redirection comprise, qui n'est pas suivie.</summary>
  public static readonly ExecutionOutcome NonSuccessResponse = new(nameof(NonSuccessResponse), 1, "Réponse non 2xx");

  /// <summary>Le système hôte n'a pas répondu dans le délai <c>HostSystem:TimeoutSeconds</c>.</summary>
  public static readonly ExecutionOutcome TimedOut = new(nameof(TimedOut), 2, "Délai dépassé");

  /// <summary>Le système hôte n'a pas pu être joint.</summary>
  public static readonly ExecutionOutcome NetworkError = new(nameof(NetworkError), 3, "Erreur réseau");

  /// <summary>
  /// Le système hôte a répondu 2xx, mais la demande n'a pas pu passer à Terminée : le droit est
  /// appliqué, et le service ne l'a pas enregistré.
  /// </summary>
  public static readonly ExecutionOutcome SucceededButNotRecorded = new(nameof(SucceededButNotRecorded), 4, "Succès non enregistré");

  private ExecutionOutcome(string name, int value, string frenchLabel)
    : base(name, value)
  {
    FrenchLabel = frenchLabel;
  }

  /// <summary>Le libellé destiné à l'<c>Operator</c>. Le français reste hors des identifiants.</summary>
  public string FrenchLabel { get; }
}
