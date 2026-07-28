using MicroserviceRgpd.Core.Qualifications;

namespace MicroserviceRgpd.UnitTests.Core.Qualifications;

/// <summary>
/// La confiance est facultative par le domaine, pas par concession : le lexique n'a aucun avis
/// sur sa propre fiabilité, et une confiance constante serait un mensonge typé.
/// </summary>
public class QualificationOpinionTests
{
  [Fact]
  public void CarriesTheRightsItRecognised()
  {
    var opinion = new QualificationOpinion([DataSubjectRight.Access, DataSubjectRight.Erasure]);

    opinion.Rights.ShouldBe([DataSubjectRight.Access, DataSubjectRight.Erasure]);
  }

  [Fact]
  public void LeavesConfidenceAndJustificationAbsentWhenTheEngineDeclaresNeither()
  {
    var opinion = new QualificationOpinion([DataSubjectRight.Erasure]);

    opinion.DeclaredConfidence.ShouldBeNull();
    opinion.Justification.ShouldBeNull();
  }

  [Fact]
  public void CarriesConfidenceAndJustificationWhenTheEngineDeclaresThem()
  {
    var opinion = new QualificationOpinion(
      [DataSubjectRight.Access],
      DeclaredConfidence.High,
      "Savoir ce qui est détenu : art. 15.");

    opinion.DeclaredConfidence.ShouldBe(DeclaredConfidence.High);
    opinion.Justification.ShouldBe("Savoir ce qui est détenu : art. 15.");
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
