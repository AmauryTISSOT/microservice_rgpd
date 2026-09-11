using MicroserviceRgpd.Core.Casework;
using Vogen;

namespace MicroserviceRgpd.UnitTests.Core.Casework;

/// <summary>
/// Les quatre champs d'un <c>DeclaredSystem</c>, et ce que chacun refuse. Ils protègent le domaine
/// en <b>levant</b>.
/// </summary>
public class DeclaredSystemFieldsTests
{
  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  [InlineData("Export Agence")]
  [InlineData("export agence")]
  [InlineData("-export")]
  [InlineData("export/agence")]
  public void RefusesAnIdentifierThatWouldNotTravelSafelyToTheAdapter(string identifier)
  {
    Should.Throw<ValueObjectValidationException>(() => DeclaredSystemId.From(identifier));
  }

  [Theory]
  [InlineData("export-agence")]
  [InlineData("boutique")]
  [InlineData("reprise.2019")]
  [InlineData("compta_scellee")]
  [InlineData("2019")]
  public void AcceptsAnIdentifierAHumanWouldRecogniseInTheirOwnCode(string identifier)
  {
    DeclaredSystemId.From(identifier).Value.ShouldBe(identifier);
  }

  [Fact]
  public void CleansTheBordersOfAnIdentifierRatherThanRefusingThem()
  {
    DeclaredSystemId.From("  export-agence \n").Value.ShouldBe("export-agence");
  }

  [Fact]
  public void RefusesAnIdentifierBeyondTheCeiling()
  {
    Should.Throw<ValueObjectValidationException>(
      () => DeclaredSystemId.From(new string('a', DeclaredSystemId.MaxLength + 1)));
  }

  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  public void RefusesALabelThatNamesNothing(string label)
  {
    Should.Throw<ValueObjectValidationException>(() => SystemLabel.From(label));
  }

  /// <summary>
  /// Le champ « contient » est <b>obligatoire</b> : c'est avec ses mots que la <c>DeliveryLetter</c>
  /// nommera un système non couvert, et un système sans prose serait un système qu'elle ne saurait
  /// pas nommer.
  /// </summary>
  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  [InlineData("\t\n")]
  public void RefusesAnAbsentSentenceForWhatASystemContains(string prose)
  {
    Should.Throw<ValueObjectValidationException>(() => SystemContents.From(prose));
  }

  /// <summary>
  /// La prose se relit <b>telle quelle</b> : le service n'est pas l'auteur de ce texte, et un
  /// texte multiligne garde ses lignes.
  /// </summary>
  [Fact]
  public void GivesBackTheProseExactlyAsItWasWrittenLineBreaksIncluded()
  {
    const string Prose = "Les comptes clients et leurs commandes.\nLes messages du support.";

    SystemContents.From(Prose).Value.ShouldBe(Prose);
  }

  [Fact]
  public void RefusesProseCarryingAControlCharacter()
  {
    Should.Throw<ValueObjectValidationException>(
      () => SystemContents.From("Les comptes clients" + (char)7 + " et leurs commandes."));
  }

  [Theory]
  [InlineData("https://brocanto.example/rgpd")]
  [InlineData("http://localhost:5000/rgpd/adapter")]
  public void AcceptsAnAbsoluteHttpAddressForTheAdapterThatServesASystem(string address)
  {
    AdapterAddress.From(address).Value.ShouldBe(address);
  }

  [Theory]
  [InlineData("")]
  [InlineData("brocanto.example/rgpd")]
  [InlineData("/rgpd/adapter")]
  [InlineData("ftp://brocanto.example/rgpd")]
  public void RefusesAnAdapterAddressTheServiceCouldNotCall(string address)
  {
    Should.Throw<ValueObjectValidationException>(() => AdapterAddress.From(address));
  }

  /// <summary>
  /// <b>Le secret n'entre pas par la bande.</b> Une URL portant des identifiants d'usager est
  /// exactement le secret que le <c>Manifest</c> n'a aucun emplacement pour détenir.
  /// </summary>
  [Fact]
  public void RefusesAnAdapterAddressSmugglingCredentialsIntoTheManifest()
  {
    Should.Throw<ValueObjectValidationException>(
      () => AdapterAddress.From("https://service:s3cr3t@brocanto.example/rgpd"));
  }
}
