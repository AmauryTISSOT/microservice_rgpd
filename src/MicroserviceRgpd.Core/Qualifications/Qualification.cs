namespace MicroserviceRgpd.Core.Qualifications;

/// <summary>
/// Le verdict rendu sur un <see cref="RightsRequestText"/> : l'ensemble des droits que le texte est
/// jugé exercer.
/// </summary>
/// <remarks>
/// Le type porte les deux invariants du domaine, validés à la frontière du sidecar et re-portés
/// ici pour qu'aucune porte ne les contourne :
/// <list type="bullet">
///   <item><b>I1</b> — l'ensemble n'est <b>jamais vide</b>. « Aucun droit reconnu » est la valeur
///   nommée <see cref="DataSubjectRight.OutOfScope"/>, pas une absence à interpréter : un verdict
///   ne peut donc pas se confondre avec une panne de moteur.</item>
///   <item><b>I2</b> — <see cref="DataSubjectRight.OutOfScope"/> est <b>exclusif</b> : il ne se
///   combine jamais avec un droit.</item>
/// </list>
/// L'égalité est une <b>égalité d'ensembles, insensible à l'ordre</b> — c'est ce dont la règle de
/// corroboration a besoin pour décider si deux moteurs divergent.
/// </remarks>
public sealed class Qualification : IEquatable<Qualification>
{
  private readonly HashSet<DataSubjectRight> _rights;

  private Qualification(HashSet<DataSubjectRight> rights)
  {
    _rights = rights;
  }

  /// <summary>Le verdict qui ne reconnaît aucun des six droits.</summary>
  public static Qualification OutOfScope { get; } = Of([DataSubjectRight.OutOfScope]);

  /// <summary>Les droits reconnus, chacun une seule fois, sans ordre signifiant.</summary>
  public IReadOnlyCollection<DataSubjectRight> Rights => _rights;

  /// <summary>
  /// Forge un verdict, ou refuse. Un ensemble vide ou un hors périmètre accompagné n'est pas un
  /// verdict faible : c'est une programmation fautive, et elle se présente comme telle.
  /// </summary>
  /// <exception cref="ArgumentNullException"><paramref name="rights"/> est absent.</exception>
  /// <exception cref="ArgumentException">I1 ou I2 est violé.</exception>
  public static Qualification Of(IEnumerable<DataSubjectRight> rights)
  {
    ArgumentNullException.ThrowIfNull(rights);

    var recognised = new HashSet<DataSubjectRight>(rights);

    if (recognised.Count == 0)
    {
      throw new ArgumentException(
        "Une qualification ne peut pas être vide : l'absence de droit reconnu s'écrit OutOfScope.",
        nameof(rights));
    }

    if (recognised.Count > 1 && recognised.Contains(DataSubjectRight.OutOfScope))
    {
      throw new ArgumentException(
        "OutOfScope est exclusif : il ne se combine jamais avec un droit.",
        nameof(rights));
    }

    return new Qualification(recognised);
  }

  /// <inheritdoc />
  public bool Equals(Qualification? other)
  {
    return other is not null && _rights.SetEquals(other._rights);
  }

  /// <inheritdoc />
  public override bool Equals(object? obj)
  {
    return Equals(obj as Qualification);
  }

  /// <inheritdoc />
  public override int GetHashCode()
  {
    // Le OU exclusif est insensible à l'ordre, comme l'égalité qu'il doit accompagner.
    return _rights.Aggregate(0, (hash, right) => hash ^ right.Value.GetHashCode());
  }

  /// <inheritdoc />
  public override string ToString()
  {
    return string.Join(", ", _rights.Select(right => right.Name).Order());
  }
}
