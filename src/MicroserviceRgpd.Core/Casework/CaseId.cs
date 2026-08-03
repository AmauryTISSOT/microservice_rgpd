using Vogen;

namespace MicroserviceRgpd.Core.Casework;

/// <summary>
/// L'identité d'un <see cref="Case"/>, engendrée par le service au moment où le dossier s'ouvre.
/// </summary>
/// <remarks>
/// <para>
/// <b>Engendrée, jamais choisie</b> — à l'inverse du <see cref="DeclaredSystemId"/>. Personne
/// d'autre que le service n'a de mot pour ce dossier : l'application appelante a sa propre
/// référence, qu'elle garde, et l'<c>Operator</c> ne connaît le dossier qu'à l'écran.
/// </para>
/// <para>
/// <b>En version 7</b> : ordonnée dans le temps, donc sans fragmentation d'index à l'insertion, et
/// c'est le même choix que celui de l'identité d'une qualification.
/// </para>
/// </remarks>
[ValueObject<Guid>]
public readonly partial struct CaseId
{
  /// <summary>L'identité d'un dossier qui s'ouvre à l'instant.</summary>
  public static CaseId Next() => From(Guid.CreateVersion7());

  private static Validation Validate(Guid value)
  {
    return value == Guid.Empty
      ? Validation.Invalid("L'identifiant du Case est vide.")
      : Validation.Ok;
  }
}
