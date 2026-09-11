namespace MicroserviceRgpd.Core.Requests;

/// <summary>
/// Le canal par lequel une <see cref="DataSubjectRequest"/> est arrivée : un <see cref="Email"/> ou
/// un <see cref="Letter"/>. Il ne change aucune autre règle.
/// </summary>
public sealed class Origin : SmartEnum<Origin>
{
  /// <summary>La demande est arrivée par email.</summary>
  public static readonly Origin Email = new(nameof(Email), 0, "Email");

  /// <summary>La demande est arrivée par courrier, et l'<c>Operator</c> la transcrit.</summary>
  public static readonly Origin Letter = new(nameof(Letter), 1, "Courrier");

  private Origin(string name, int value, string frenchLabel)
    : base(name, value)
  {
    FrenchLabel = frenchLabel;
  }

  /// <summary>Le libellé destiné à l'<c>Operator</c>. Le français reste hors des identifiants.</summary>
  public string FrenchLabel { get; }
}
