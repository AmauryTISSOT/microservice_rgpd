using MicroserviceRgpd.Core.Qualifications;

namespace MicroserviceRgpd.UnitTests.Core.Qualifications;

/// <summary>
/// Le verdict porte ses deux invariants dans son type. Ils sont validés à la frontière du sidecar,
/// et re-portés ici : une qualification vide ou un hors périmètre accompagné ne peut pas exister
/// en mémoire, quelle que soit la porte par laquelle on entre.
/// </summary>
public class QualificationTests
{
  /// <summary>
  /// I1 — « aucun droit reconnu » est la valeur nommée <c>OutOfScope</c>, pas une absence à
  /// interpréter : un verdict ne peut donc pas se confondre avec une panne de moteur.
  /// </summary>
  [Fact]
  public void RefusesToBeEmpty()
  {
    Should.Throw<ArgumentException>(() => Qualification.Of([]));
  }

  /// <summary>I2 — le hors périmètre ne se combine jamais avec un droit.</summary>
  [Fact]
  public void RefusesOutOfScopeAlongsideARight()
  {
    Should.Throw<ArgumentException>(() =>
      Qualification.Of([DataSubjectRight.OutOfScope, DataSubjectRight.Erasure]));
  }

  [Fact]
  public void AcceptsOutOfScopeOnItsOwn()
  {
    Qualification.Of([DataSubjectRight.OutOfScope]).Rights.ShouldBe([DataSubjectRight.OutOfScope]);
  }

  [Fact]
  public void AcceptsSeveralRightsAtOnce()
  {
    var qualification = Qualification.Of([DataSubjectRight.Access, DataSubjectRight.Erasure]);

    qualification.Rights.ShouldBe([DataSubjectRight.Access, DataSubjectRight.Erasure], ignoreOrder: true);
  }

  [Fact]
  public void HoldsEachRightOnlyOnce()
  {
    Qualification.Of([DataSubjectRight.Access, DataSubjectRight.Access]).Rights.Count.ShouldBe(1);
  }

  /// <summary>
  /// La comparaison de deux verdicts est une égalité d'ensembles, insensible à l'ordre : c'est ce
  /// dont la règle de corroboration aura besoin pour décider d'une divergence.
  /// </summary>
  [Fact]
  public void EqualsAnotherQualificationHoldingTheSameRightsInAnyOrder()
  {
    var one = Qualification.Of([DataSubjectRight.Access, DataSubjectRight.Erasure]);
    var other = Qualification.Of([DataSubjectRight.Erasure, DataSubjectRight.Access]);

    one.ShouldBe(other);
    one.GetHashCode().ShouldBe(other.GetHashCode());
  }

  [Fact]
  public void DiffersFromAQualificationHoldingOtherRights()
  {
    var one = Qualification.Of([DataSubjectRight.Access, DataSubjectRight.Erasure]);
    var other = Qualification.Of([DataSubjectRight.Access]);

    one.ShouldNotBe(other);
  }

  [Fact]
  public void RefusesAnAbsentCollectionOfRights()
  {
    Should.Throw<ArgumentNullException>(() => Qualification.Of(null!));
  }
}
