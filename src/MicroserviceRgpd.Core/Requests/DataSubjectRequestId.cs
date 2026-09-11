using Vogen;

namespace MicroserviceRgpd.Core.Requests;

/// <summary>
/// L'identité d'une <see cref="DataSubjectRequest"/>, engendrée par le service à l'enregistrement.
/// <b>En version 7</b> : ordonnée dans le temps, donc sans fragmentation d'index à l'insertion.
/// </summary>
[ValueObject<Guid>]
public readonly partial struct DataSubjectRequestId
{
  /// <summary>L'identité d'une demande qui s'enregistre à l'instant.</summary>
  public static DataSubjectRequestId Next() => From(Guid.CreateVersion7());

  private static Validation Validate(Guid value)
  {
    return value == Guid.Empty
      ? Validation.Invalid("L'identifiant de la demande est vide.")
      : Validation.Ok;
  }
}
