using Vogen;

namespace MicroserviceRgpd.Core.Requests;

/// <summary>
/// L'identité d'une <see cref="ExecutionAttempt"/>, engendrée par le service à l'écriture. <b>En
/// version 7</b> : ordonnée dans le temps, comme celle d'une demande.
/// </summary>
[ValueObject<Guid>]
public readonly partial struct ExecutionAttemptId
{
  /// <summary>L'identité d'une tentative qui s'écrit à l'instant.</summary>
  public static ExecutionAttemptId Next() => From(Guid.CreateVersion7());

  private static Validation Validate(Guid value)
  {
    return value == Guid.Empty
      ? Validation.Invalid("L'identifiant de la tentative d'exécution est vide.")
      : Validation.Ok;
  }
}
