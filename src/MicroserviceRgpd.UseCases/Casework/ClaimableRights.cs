using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.UseCases.Casework;

/// <summary>
/// <c>OutOfScope</c> n'est pas un droit réclamable, dit à l'appelant plutôt que levé sur lui.
/// </summary>
/// <remarks>
/// <para>
/// <b>La règle est déjà portée par <see cref="Core.Casework.Case"/>, qui lève.</b> La redite est
/// celle que le dépôt pratique déjà : le type protège le domaine en <b>levant</b>, la frontière
/// parle à l'appelant en lui <b>nommant</b> ce qu'il a mal rempli. Une taxonomie mal lue par une
/// application tierce n'est pas un accident de programmation du service.
/// </para>
/// <para>
/// ⚠️ <b>Ce n'est pas un refus de la demande.</b> Une demande n'exerçant aucun droit entre quand
/// même — elle ouvre un dossier sans aucun <c>Claim</c>, qui se clora <c>NotApplicable</c> sous la
/// signature d'un humain. Ce qui est refusé ici est de <b>réclamer</b> le verdict « aucun droit »
/// comme s'il en était un.
/// </para>
/// </remarks>
internal static class ClaimableRights
{
  /// <summary>Le mot que lit l'application qui a posté <c>OutOfScope</c> parmi des droits.</summary>
  internal const string Message =
    "OutOfScope n'est pas un droit réclamable : c'est le verdict qu'aucun droit n'a été reconnu. "
    + "Une demande n'exerçant aucun droit s'envoie sans aucun droit, et le dossier s'ouvrira quand "
    + "même.";

  /// <summary>
  /// L'écart, ou <c>null</c> si les droits sont réclamables — l'ensemble vide y compris, qui est
  /// une demande dont on ne reconnaît encore aucun droit et non une saisie inachevée.
  /// </summary>
  internal static ValidationError? Violation(IReadOnlyCollection<DataSubjectRight> rights, string identifier)
  {
    if (!rights.Contains(DataSubjectRight.OutOfScope))
    {
      return null;
    }

    return new ValidationError
    {
      Identifier = identifier,
      ErrorMessage = Message,
      Severity = ValidationSeverity.Error,
    };
  }
}
