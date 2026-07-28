namespace MicroserviceRgpd.Core.Qualifications;

/// <summary>
/// Un moteur n'a pas rendu d'avis — ou en a rendu un que le domaine refuse.
/// </summary>
/// <remarks>
/// <para>
/// <b>Un avis invalide n'est pas un avis faible, c'est une panne du moteur.</b> Une liste vide, un
/// hors périmètre accompagné, une valeur étrangère à la taxonomie : rien de tout cela n'est une
/// qualification prudente qu'on pourrait rendre en la signalant. Le sidecar refuse déjà d'émettre
/// de tels avis ; ce type est la même règle, portée du côté .NET, pour que la frontière tienne même
/// si l'autre bout ment.
/// </para>
/// <para>
/// La panne est <b>nommée</b> plutôt que laissée à une exception de transport : c'est ce qui
/// permettra plus tard de décider quoi faire d'un moteur absent sans avoir à reconnaître un
/// <c>HttpRequestException</c> au milieu du domaine.
/// </para>
/// </remarks>
public sealed class QualificationEngineFailure : Exception
{
  /// <inheritdoc />
  public QualificationEngineFailure()
  {
  }

  /// <inheritdoc />
  public QualificationEngineFailure(string message)
    : base(message)
  {
  }

  /// <inheritdoc />
  public QualificationEngineFailure(string message, Exception innerException)
    : base(message, innerException)
  {
  }
}
