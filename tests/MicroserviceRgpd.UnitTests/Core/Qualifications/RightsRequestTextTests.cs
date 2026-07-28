using MicroserviceRgpd.Core.Qualifications;
using Vogen;

namespace MicroserviceRgpd.UnitTests.Core.Qualifications;

/// <summary>
/// Le texte reçu n'a aucun plancher de longueur au-delà de la non-vacuité : rejeter un texte
/// parce qu'il est court, ce serait confondre « je n'y reconnais aucun droit » avec
/// « ta requête est malformée ».
/// </summary>
public class RightsRequestTextTests
{
  [Fact]
  public void RefusesAnAbsentText()
  {
    Should.Throw<ValueObjectValidationException>(() => RightsRequestText.From(null!));
  }

  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  [InlineData("\t\r\n ")]
  public void RefusesATextEmptyOnceBordersAreTrimmed(string text)
  {
    Should.Throw<ValueObjectValidationException>(() => RightsRequestText.From(text));
  }

  [Fact]
  public void RefusesATextBeyondTenThousandCharacters()
  {
    Should.Throw<ValueObjectValidationException>(() => RightsRequestText.From(new string('a', 10_001)));
  }

  [Fact]
  public void AcceptsATextOfExactlyTenThousandCharacters()
  {
    RightsRequestText.From(new string('a', 10_000)).Value.Length.ShouldBe(10_000);
  }

  [Theory]
  [InlineData("a")]
  [InlineData("🙂")]
  public void AcceptsASingleCharacterText(string text)
  {
    RightsRequestText.From(text).Value.ShouldBe(text);
  }

  /// <summary>
  /// Le plafond se mesure sur le texte nettoyé, celui qui sera conservé : un texte de 10 000
  /// caractères entouré d'espaces reste recevable. La spec énonce les deux règles sans les
  /// ordonner ; c'est cet ordre-ci qui est retenu, et ce test le fixe.
  /// </summary>
  [Fact]
  public void MeasuresTheCapOnTheTrimmedTextNotOnItsBorders()
  {
    RightsRequestText.From("   " + new string('a', 10_000) + "   ").Value.Length.ShouldBe(10_000);
  }

  [Fact]
  public void TrimsTheBordersItValidatesOn()
  {
    RightsRequestText.From("  Droit d'accès.  ").Value.ShouldBe("Droit d'accès.");
  }
}
