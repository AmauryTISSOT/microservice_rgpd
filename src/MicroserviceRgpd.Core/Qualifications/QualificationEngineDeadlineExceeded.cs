namespace MicroserviceRgpd.Core.Qualifications;

/// <summary>
/// Un moteur n'a pas rendu d'avis parce que son <b>échéance est passée</b>.
/// </summary>
/// <remarks>
/// <para>
/// C'est une <see cref="QualificationEngineFailure"/> comme une autre pour le repli : un moteur muet
/// est un moteur muet, et le domaine n'a pas à savoir pourquoi il s'est tu. Elle se distingue en un
/// seul point, et il n'arrive qu'au bout du monde : <b>quand les deux moteurs se taisent</b>, c'est
/// le mode de panne du moteur principal qui décide du code rendu à l'appelant — un dépassement
/// d'échéance, ou une indisponibilité.
/// </para>
/// <para>
/// <b>La lenteur arrive donc nommée</b> plutôt que subie. Elle l'est par les deux chemins qui y
/// mènent : le sidecar a lui-même renoncé à attendre son amont — et le dit par son propre code —, ou
/// bien l'échéance du client .NET est passée la première, ce que l'ordre strict des deux échéances
/// rend anormal et digne d'être vu.
/// </para>
/// </remarks>
public sealed class QualificationEngineDeadlineExceeded : QualificationEngineFailure
{
  /// <inheritdoc />
  public QualificationEngineDeadlineExceeded()
  {
  }

  /// <inheritdoc />
  public QualificationEngineDeadlineExceeded(string message)
    : base(message)
  {
  }

  /// <inheritdoc />
  public QualificationEngineDeadlineExceeded(string message, Exception innerException)
    : base(message, innerException)
  {
  }
}
