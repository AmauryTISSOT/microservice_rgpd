using MicroserviceRgpd.Core.Casework;

namespace MicroserviceRgpd.UnitTests.Core.Casework;

/// <summary>
/// La forme unique sous laquelle on désigne une personne, quel que soit le canal.
/// </summary>
public class DesignationTests
{
  /// <summary>
  /// Quatre natures, et le mot canonique de chacune est celui du contrat d'<c>Adapter</c> — le C#
  /// n'impose au fil aucun mot que le contrat n'aurait pas choisi.
  /// </summary>
  [Fact]
  public void NamesFourKindsUnderTheTokensTheAdapterContractFixed()
  {
    DesignationKind.List.Select(kind => kind.Token).ShouldBe(["email", "name", "phone", "reference"]);
  }

  /// <summary>Le vocabulaire est <b>fermé</b> : un mot qu'il ignore n'est pas une nature qu'on devine.</summary>
  [Theory]
  [InlineData("email")]
  [InlineData("name")]
  [InlineData("phone")]
  [InlineData("reference")]
  public void ReadsBackEveryTokenItWrites(string token)
  {
    DesignationKind.FromToken(token)!.Token.ShouldBe(token);
  }

  [Theory]
  [InlineData("Email")]
  [InlineData("nom")]
  [InlineData("subject_id")]
  [InlineData(null)]
  public void RefusesAWordTheContractNeverFixed(string? token)
  {
    DesignationKind.FromToken(token).ShouldBeNull();
  }

  /// <summary>Les bordures sont retirées avant validation <b>et</b> avant stockage ; l'intérieur reste intact.</summary>
  [Fact]
  public void KeepsTheDeclaredValueAsItWasWrittenOnceItsBordersAreTrimmed()
  {
    var designation = Designation.Of(DesignationKind.PersonName, "  Jean Dupont  ");

    designation.Value.ShouldBe("Jean Dupont");
    designation.Kind.ShouldBe(DesignationKind.PersonName);
  }

  /// <summary>
  /// Deux désignations de même nature et de même valeur sont <b>la même</b> : c'est ce qui permet
  /// au sac de ne pas compter deux fois la même chose, et donc au <c>EvidenceLog</c> de ne pas mentir
  /// sur l'ampleur d'une recherche.
  /// </summary>
  [Fact]
  public void HoldsTwoIdenticalDesignationsForTheSameOne()
  {
    Designation.Of(DesignationKind.Email, "jean@example.fr")
      .ShouldBe(Designation.Of(DesignationKind.Email, "jean@example.fr"));

    // La casse, elle, distingue : le journal applicatif y est sensible là où la base ne l'est pas,
    // et fondre les deux ici priverait l'Operator de la désignation qui ouvre le second système.
    Designation.Of(DesignationKind.Email, "Jean@Example.fr")
      .ShouldNotBe(Designation.Of(DesignationKind.Email, "jean@example.fr"));
  }

  /// <summary>
  /// Une désignation vide ne pointe vers personne, et une recherche lancée sous elle rendrait un
  /// zéro qu'on lirait « cette personne n'est pas chez nous ».
  /// </summary>
  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  [InlineData(null)]
  public void RefusesADesignationThatPointsAtNobody(string? value)
  {
    Should.Throw<ArgumentException>(() => Designation.Of(DesignationKind.Email, value));
  }

  /// <summary>Une désignation part sur le fil vers un <c>Adapter</c> : elle n'y porte aucun caractère de contrôle.</summary>
  [Fact]
  public void RefusesAControlCharacterOnSomethingThatTravelsToAnAdapter()
  {
    Should.Throw<ArgumentException>(() => Designation.Of(DesignationKind.Reference, "4\t2"));
  }

  [Fact]
  public void RefusesAnOversizedValue()
  {
    Should.Throw<ArgumentException>(
      () => Designation.Of(DesignationKind.PersonName, new string('a', Designation.MaxValueLength + 1)));
  }
}
