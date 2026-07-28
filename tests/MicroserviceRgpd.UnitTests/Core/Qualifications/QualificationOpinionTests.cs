using MicroserviceRgpd.Core.Qualifications;

namespace MicroserviceRgpd.UnitTests.Core.Qualifications;

/// <summary>
/// La confiance est facultative par le domaine, pas par concession : le lexique n'a aucun avis
/// sur sa propre fiabilité, et une confiance constante serait un mensonge typé.
/// </summary>
public class QualificationOpinionTests
{
  [Fact]
  public void CarriesTheQualificationItsEngineRendered()
  {
    var verdict = Qualification.Of([DataSubjectRight.Access, DataSubjectRight.Erasure]);

    new QualificationOpinion(verdict, AnEngine.Lexical).Qualification.ShouldBe(verdict);
  }

  [Fact]
  public void LeavesConfidenceAndJustificationAbsentWhenTheEngineDeclaresNeither()
  {
    var opinion = new QualificationOpinion(Qualification.Of([DataSubjectRight.Erasure]), AnEngine.Lexical);

    opinion.DeclaredConfidence.ShouldBeNull();
    opinion.Justification.ShouldBeNull();
  }

  [Fact]
  public void CarriesConfidenceAndJustificationWhenTheEngineDeclaresThem()
  {
    var opinion = new QualificationOpinion(
      Qualification.Of([DataSubjectRight.Access]),
      AnEngine.Reasoning,
      DeclaredConfidence.High,
      "Savoir ce qui est détenu : art. 15.");

    opinion.DeclaredConfidence.ShouldBe(DeclaredConfidence.High);
    opinion.Justification.ShouldBe("Savoir ce qui est détenu : art. 15.");
  }

  /// <summary>
  /// Deux avis portant le même verdict sont le même avis, quel que soit l'ordre dans lequel les
  /// droits ont été énoncés : sans quoi la règle de corroboration comparerait des références.
  /// </summary>
  [Fact]
  public void EqualsAnotherOpinionCarryingTheSameVerdictInAnyOrder()
  {
    var one = new QualificationOpinion(
      Qualification.Of([DataSubjectRight.Access, DataSubjectRight.Erasure]), AnEngine.Lexical);
    var other = new QualificationOpinion(
      Qualification.Of([DataSubjectRight.Erasure, DataSubjectRight.Access]), AnEngine.Lexical);

    one.ShouldBe(other);
  }

  /// <summary>
  /// L'échelle est ordinale : la corroboration n'en lit qu'un bit — haute ou non — mais
  /// l'ordre doit rester lisible pour la trace d'audit et la mesure de calibration à venir.
  /// </summary>
  [Fact]
  public void OrdersTheThreeDegreesOfDeclaredConfidence()
  {
    Enum.GetValues<DeclaredConfidence>().ShouldBe(
      [DeclaredConfidence.Low, DeclaredConfidence.Medium, DeclaredConfidence.High]);
  }
}
