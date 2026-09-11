using Vogen;

namespace MicroserviceRgpd.Core.Requests;

/// <summary>
/// Le <b>message</b> d'une demande : son contenu tel qu'il a été reçu — le corps de l'email, la
/// transcription du courrier —, recopié par l'<c>Operator</c>. <b>Obligatoire</b>.
/// </summary>
/// <remarks>Le plafond se compte en unités UTF-16 (<c>.Length</c>), comme dans le navigateur.</remarks>
[ValueObject<string>]
public readonly partial struct RequestMessage
{
  /// <summary>Le plafond, en unités UTF-16.</summary>
  public const int MaxLength = 10_000;

  /// <summary>Les bordures sont retirées avant validation <b>et</b> avant stockage.</summary>
  private static string NormalizeInput(string? input) => input?.Trim() ?? string.Empty;

  private static Validation Validate(string value)
  {
    if (value.Length == 0)
    {
      return Validation.Invalid(DataSubjectRequestMessages.MessageMissing);
    }

    return value.Length > MaxLength
      ? Validation.Invalid(DataSubjectRequestMessages.MessageTooLong)
      : Validation.Ok;
  }
}
