namespace MicroserviceRgpd.ArchitectureTests;

/// <summary>
/// Le noyau partagé vaut par sa <b>petitesse</b> : un noyau qui grossit recouple deux contextes en
/// silence. La taxonomie y est seule parce qu'elle est seule à n'appartenir à aucun des deux — son
/// auteur est le RGPD. <c>Capability</c> est du <c>Casework</c> pur, <c>DeclaredConfidence</c> de la
/// <c>Qualification</c> pure : ni l'un ni l'autre n'entre.
/// <para>
/// La liste attendue est écrite en dur. C'est le but : y ajouter un nom doit demander un geste
/// délibéré, et ce geste est de niveau ADR — voir <c>docs/adr/0002</c>.
/// </para>
/// </summary>
public class SharedKernelTests
{
  private static readonly string[] TheTaxonomyAndItsProjection =
  [
    "MicroserviceRgpd.Core.SharedKernel.DataSubjectRight",
    "MicroserviceRgpd.Core.SharedKernel.DataSubjectRightJsonConverter",
  ];

  [Fact]
  public void HoldsTheTaxonomyAndNothingElse()
  {
    var inhabitants = ProductionAssembly.All
      .SelectMany(assembly => ContextInspector.TypesIn(ProductionAssembly.PathOf(assembly), ContextInspector.SharedKernel))
      .Order(StringComparer.Ordinal)
      .ToArray();

    inhabitants.ShouldBe(
      TheTaxonomyAndItsProjection,
      "Le noyau partagé n'est plus réduit à la taxonomie. Ce qui n'est pas vrai des deux côtés " +
      "sans exception n'y entre pas : sortez ce type vers le contexte qui le possède.");
  }

  /// <summary>
  /// La taxonomie ne peut pas être restée sous <c>Qualifications/</c> « aussi » : deux copies
  /// dériveraient, et le contrat public suivrait la mauvaise.
  /// </summary>
  [Fact]
  public void LeavesNothingOfTheTaxonomyBehindInQualification()
  {
    var strays = ProductionAssembly.All
      .SelectMany(assembly => ContextInspector.TypesIn(ProductionAssembly.PathOf(assembly), ContextInspector.Qualification))
      .Where(type => type.Contains("DataSubjectRight", StringComparison.Ordinal))
      .ToArray();

    strays.ShouldBeEmpty(
      "La taxonomie a déménagé dans le noyau partagé : " + string.Join(", ", strays) + " est resté derrière.");
  }
}
