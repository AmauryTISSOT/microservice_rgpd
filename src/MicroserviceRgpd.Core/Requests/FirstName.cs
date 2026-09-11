using Vogen;

namespace MicroserviceRgpd.Core.Requests;

/// <summary>
/// Le prénom de la personne qui exerce son droit. <b>Facultatif sur la demande</b> — l'absence s'y
/// dit par <c>null</c> —, mais jamais vide ici : un prénom qui existe a au moins un caractère.
/// </summary>
/// <remarks>Le plafond se compte en unités UTF-16 (<c>.Length</c>), comme dans le navigateur.</remarks>
[ValueObject<string>]
public readonly partial struct FirstName
{
  /// <summary>Le plafond, en unités UTF-16.</summary>
  public const int MaxLength = 100;

  /// <summary>Les bordures sont retirées avant validation <b>et</b> avant stockage.</summary>
  private static string NormalizeInput(string? input) => input?.Trim() ?? string.Empty;

  private static Validation Validate(string value)
  {
    if (value.Length == 0)
    {
      return Validation.Invalid("Un prénom absent se dit par son absence, pas par un prénom vide.");
    }

    return value.Length > MaxLength
      ? Validation.Invalid(DataSubjectRequestMessages.FirstNameTooLong)
      : Validation.Ok;
  }
}
