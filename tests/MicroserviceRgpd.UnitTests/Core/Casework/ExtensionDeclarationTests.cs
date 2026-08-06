using MicroserviceRgpd.Core.Casework;

namespace MicroserviceRgpd.UnitTests.Core.Casework;

/// <summary>
/// La <c>ExtensionDeclaration</c> : ce que l'<c>Operator</c> <b>déclare</b> lorsqu'il prolonge de
/// deux mois au titre de l'art. 12.3.
/// </summary>
/// <remarks>
/// <para>
/// Ce qui se garde ici est que <b>la charge probatoire est à celui qui prolonge</b>. Le service ne
/// prolonge rien et n'écrit à personne : il réclame un motif et la date à laquelle un humain a
/// informé la personne, et refuse la déclaration à qui n'a ni l'un ni l'autre.
/// </para>
/// <para>
/// ⚠️ <b>Le déplacement de l'échéance n'est pas ici.</b> C'est un calcul sur la date de la
/// déclaration, gardé par <see cref="StatutoryDeadlineTests"/> : cet objet ne porte aucune
/// propriété disant s'il a porté ou non.
/// </para>
/// </remarks>
public class ExtensionDeclarationTests
{
  private static readonly DateTimeOffset Declared = new(2026, 8, 20, 14, 0, 0, TimeSpan.Zero);

  /// <summary>Les trois faits déclarés se relisent tels quels : un motif, une date, une date.</summary>
  [Fact]
  public void KeepsTheMotiveAndBothDatesAsTheHumanDeclaredThem()
  {
    var extension = ExtensionDeclaration.Of(
      "Les systèmes du prestataire de paie ne rendent la main qu'au trimestre.",
      Declared.AddDays(-1),
      Declared);

    extension.Motive.ShouldBe("Les systèmes du prestataire de paie ne rendent la main qu'au trimestre.");
    extension.InformedOn.ShouldBe(Declared.AddDays(-1));
    extension.DeclaredOn.ShouldBe(Declared);
  }

  /// <summary>
  /// <b>Le motif est exigé.</b> Une prolongation sans motif serait un délai gagné sans raison écrite,
  /// et l'art. 12.3 met la raison à la charge de qui prolonge.
  /// </summary>
  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  public void RefusesToProlongWithoutSayingWhy(string? motive)
  {
    Should.Throw<ArgumentException>(() => ExtensionDeclaration.Of(motive, Declared.AddDays(-1), Declared))
      .Message.ShouldContain("motif");
  }

  /// <summary>
  /// <b>La date d'information ne peut pas être postérieure à la déclaration.</b> Elle dirait qu'on a
  /// informé la personne demain : c'est une frappe, et elle rendrait fausse la seule preuve que
  /// l'art. 12.3 réclame de l'<c>Operator</c>.
  /// </summary>
  [Fact]
  public void RefusesToHaveInformedThePersonTomorrow()
  {
    Should.Throw<ArgumentException>(
        () => ExtensionDeclaration.Of("Le prestataire est en congés.", Declared.AddDays(1), Declared))
      .Message.ShouldContain("informé");
  }

  /// <summary>
  /// Les deux dates sont ramenées en UTC : <c>timestamptz</c> ne conserve pas le décalage, et une
  /// heure locale ferait varier une échéance juridique d'un serveur à l'autre.
  /// </summary>
  [Fact]
  public void NormalisesBothDatesOntoUtc()
  {
    var informed = new DateTimeOffset(2026, 8, 19, 16, 0, 0, TimeSpan.FromHours(2));
    var declared = new DateTimeOffset(2026, 8, 20, 16, 0, 0, TimeSpan.FromHours(2));

    var extension = ExtensionDeclaration.Of("Le prestataire est en congés.", informed, declared);

    extension.InformedOn.Offset.ShouldBe(TimeSpan.Zero);
    extension.DeclaredOn.Offset.ShouldBe(TimeSpan.Zero);
    extension.DeclaredOn.ShouldBe(declared.ToUniversalTime());
  }

  /// <summary>
  /// <b>Elle ne porte aucune propriété disant si elle a déplacé l'échéance.</b> Le déplacement est
  /// un calcul sur la date de la déclaration ; une propriété écrite ici serait un dénominateur
  /// persisté, c'est-à-dire le drapeau que ce contexte refuse partout.
  /// </summary>
  [Fact]
  public void CarriesNoFlagSayingWhetherItMovedTheDeadline()
  {
    typeof(ExtensionDeclaration).GetProperties()
      .Select(property => property.Name)
      .ShouldBe(["Motive", "InformedOn", "DeclaredOn"], ignoreOrder: true);
  }
}
