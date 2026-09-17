using Vogen;

namespace MicroserviceRgpd.Core.Requests;

/// <summary>
/// La <b>justification de la prolongation</b> : le texte par lequel l'<c>Operator</c> dit le fait
/// concret — quelle complexité, quel afflux — qui justifie les deux mois. <b>Obligatoire</b>
/// (ADR-0029).
/// </summary>
/// <remarks>
/// <para>
/// Le plafond se compte en unités UTF-16 (<c>.Length</c>), comme dans le navigateur, et comme celui
/// du <see cref="RequestMessage"/>.
/// </para>
/// <para>
/// ⚠️ <b>Le plafond est bien plus bas que celui du message</b> : la justification voyagera dans un
/// attribut HTML de <b>chaque</b> ligne du tableau, le jour où la fiche la montrera — pour qu'elle
/// la lise sans aller-retour réseau, comme elle lit déjà le message.
/// </para>
/// </remarks>
[ValueObject<string>]
public readonly partial struct ExtensionJustification
{
  /// <summary>Le plafond, en unités UTF-16.</summary>
  public const int MaxLength = 2_000;

  /// <summary>Les bordures sont retirées avant validation <b>et</b> avant stockage.</summary>
  private static string NormalizeInput(string? input) => input?.Trim() ?? string.Empty;

  private static Validation Validate(string value)
  {
    if (value.Length == 0)
    {
      return Validation.Invalid(DataSubjectRequestMessages.ExtensionJustificationMissing);
    }

    return value.Length > MaxLength
      ? Validation.Invalid(DataSubjectRequestMessages.ExtensionJustificationTooLong)
      : Validation.Ok;
  }
}
