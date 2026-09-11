using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.SharedKernel;

// Shouldly porte un type du même nom, réservé à ses propres tables de cas.
using Case = MicroserviceRgpd.Core.Casework.Case;

namespace MicroserviceRgpd.UnitTests.Core.Casework;

/// <summary>
/// La <b>prolongation de l'art. 12.3</b> portée par le dossier : ce que l'<c>Operator</c> déclare,
/// et ce que le dossier en fait.
/// </summary>
/// <remarks>
/// <para>
/// Ce qui se garde ici est que le dossier <b>enregistre une déclaration</b>, sans jamais prolonger
/// quoi que ce soit lui-même ni écrire à la personne concernée : l'information est un acte humain,
/// seulement déclaré au service.
/// </para>
/// <para>
/// ⚠️ <b>Une déclaration tardive s'inscrit tout de même.</b> Le dossier ne la barre pas — le
/// dénominateur, lui, ne bouge pas, et c'est <see cref="StatutoryDeadlineTests"/> qui le garde.
/// </para>
/// </remarks>
public class CaseExtensionTests
{
  private static readonly DateTimeOffset Received = new(2026, 4, 10, 9, 0, 0, TimeSpan.Zero);

  /// <summary>Un dossier neuf ne porte aucune prolongation : personne n'en a déclaré.</summary>
  [Fact]
  public void OpensWithoutAnyExtension()
  {
    ACase().ExtensionDeclaration.ShouldBeNull();
  }

  /// <summary>La déclaration se pose sur le dossier, telle que l'humain l'a écrite.</summary>
  [Fact]
  public void KeepsTheExtensionTheOperatorDeclared()
  {
    var opened = ACase();

    var declared = AnExtension(Received.AddDays(20));

    opened.DeclareExtension(declared).ShouldBeTrue();

    opened.ExtensionDeclaration.ShouldBe(declared);
  }

  /// <summary>
  /// ⚠️ <b>Une déclaration tardive s'inscrit quand même.</b> Le fait est gardé : on enregistre un
  /// fait laid plutôt qu'on ne fabrique un faux, et le dépassement déjà acquis se lit à côté.
  /// </summary>
  [Fact]
  public void RecordsAnExtensionDeclaredLongAfterTheMonthRanOut()
  {
    var opened = ACase();

    var late = AnExtension(Received.AddDays(70));

    opened.DeclareExtension(late).ShouldBeTrue();

    opened.ExtensionDeclaration.ShouldBe(late);
    StatutoryDeadline.Of(opened.Reception, opened.ExtensionDeclaration).Extended.ShouldBeFalse();
  }

  /// <summary>
  /// <b>L'art. 12.3 n'ouvre qu'une prolongation.</b> Une seconde déclaration réécrirait le motif et
  /// les dates qu'un humain a signés, et ferait du délai une chose qu'on repousse à volonté.
  /// </summary>
  [Fact]
  public void ProlongsOnceAndOnlyOnce()
  {
    var opened = ACase();

    var first = AnExtension(Received.AddDays(20));

    opened.DeclareExtension(first).ShouldBeTrue();
    opened.DeclareExtension(AnExtension(Received.AddDays(25))).ShouldBeFalse();

    opened.ExtensionDeclaration.ShouldBe(first);
  }

  /// <summary>
  /// <b>Un dossier clos ne se prolonge plus.</b> Le délai qu'on prolongerait est éteint, et la
  /// déclaration daterait un geste que plus rien n'appelle.
  /// </summary>
  [Fact]
  public void RefusesToProlongAClosedCase()
  {
    var opened = ACase();

    opened.Close(ClosingCause.Answered, Received.AddDays(15));

    opened.DeclareExtension(AnExtension(Received.AddDays(20))).ShouldBeFalse();

    opened.ExtensionDeclaration.ShouldBeNull();
  }

  private static ExtensionDeclaration AnExtension(DateTimeOffset declaredOn)
  {
    return ExtensionDeclaration.Of(
      "Le prestataire de paie ne rend la main qu'au trimestre.",
      declaredOn.AddDays(-1),
      declaredOn);
  }

  private static Case ACase()
  {
    return Case.Open(
      CaseId.Next(),
      IdentityDeclaration.Unverified,
      motivation: null,
      [Designation.Of(DesignationKind.Email, "jean.dupont@example.fr")],
      [DataSubjectRight.Access],
      ClaimOrigin.Named,
      [],
      ReceptionDate.Declared(Received));
  }
}
