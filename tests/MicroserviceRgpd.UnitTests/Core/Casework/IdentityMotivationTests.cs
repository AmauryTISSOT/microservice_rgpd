using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.UnitTests.Core.Casework;

/// <summary>
/// La motivation en <b>deux champs qui ne se confondent jamais</b> : une méthode qui se compte et
/// survit à la clôture, un détail en prose qui nomme et meurt avec le dossier.
/// <para>
/// Le test le plus important de ce fichier n'est pas celui d'un chemin heureux : c'est
/// <see cref="KeepsTheAdmissionApartFromTheAbsenceOfAnyAnswer"/>. « Aucune » et « personne n'a pesé »
/// se ressemblent à l'œil et ne disent pas la même chose au contrôle.
/// </para>
/// </summary>
public class IdentityMotivationTests
{
  /// <summary>
  /// <b><c>None</c> est une réponse ; l'absence de motivation n'en est pas une.</b> Confondre les
  /// deux ferait signer par défaut un aveu que personne n'a écrit — exactement ce qu'une date de
  /// réception tenue pour défaut ne doit jamais faire.
  /// </summary>
  [Fact]
  public void KeepsTheAdmissionApartFromTheAbsenceOfAnyAnswer()
  {
    var admitted = IdentityMotivation.Of(IdentityVerificationMethod.None, detail: null);

    admitted.Method.ShouldBe(IdentityVerificationMethod.None);

    // Il n'existe aucune façon de fabriquer une motivation sans méthode : l'absence de réponse
    // s'écrit par l'absence de l'objet, jamais par une méthode nulle rangée dedans.
    Should.Throw<ArgumentNullException>(() => IdentityMotivation.Of(null!, "quelque chose"));
  }

  /// <summary>
  /// Le détail est <b>accueilli sans être exigé</b> : exiger de la prose derrière chaque méthode
  /// ferait écrire une ligne de rien à chaque dépôt, et le détail qui compte se noierait dans les
  /// autres.
  /// </summary>
  [Fact]
  public void WelcomesTheProseWithoutEverDemandingIt()
  {
    IdentityMotivation.Of(IdentityVerificationMethod.PersonalRecognition, detail: null).Detail.ShouldBeNull();

    // Le vide entre comme une absence, jamais comme une chaîne vide : celle-ci se relirait comme un
    // détail qu'on aurait effacé.
    IdentityMotivation.Of(IdentityVerificationMethod.PersonalRecognition, "   ").Detail.ShouldBeNull();

    IdentityMotivation.Of(IdentityVerificationMethod.PersonalRecognition, "  Reconnue à l'accueil.  ")
      .Detail.ShouldBe("Reconnue à l'accueil.");
  }

  /// <summary>
  /// Le détail est borné et nettoyé comme tout texte déclaré : il descend dans une colonne, et le
  /// service ne l'échappera pour personne.
  /// </summary>
  [Fact]
  public void RefusesAProseThatIsOversizedOrCarriesAControlCharacter()
  {
    Should.Throw<ArgumentException>(() => IdentityMotivation.Of(
      IdentityVerificationMethod.None,
      new string('a', IdentityMotivation.MaxDetailLength + 1)));

    Should.Throw<ArgumentException>(() => IdentityMotivation.Of(
      IdentityVerificationMethod.None,
      "Rappelé leclient."));
  }

  /// <summary>
  /// <b>La règle de la demande est écrite une seule fois</b>, et lue par l'écran comme par le
  /// dossier : deux rédactions finiraient par ne plus dire la même chose, et l'écran mentirait sur ce
  /// que le dossier montre.
  /// </summary>
  [Theory]
  [InlineData(nameof(IdentityDeclaration.Unverified), nameof(DataSubjectRight.Access), true)]
  [InlineData(nameof(IdentityDeclaration.OperatorAttested), nameof(DataSubjectRight.Access), true)]
  [InlineData(nameof(IdentityDeclaration.ApplicationSession), nameof(DataSubjectRight.Access), false)]
  [InlineData(nameof(IdentityDeclaration.ChannelControl), nameof(DataSubjectRight.Access), false)]
  [InlineData(nameof(IdentityDeclaration.Unverified), nameof(DataSubjectRight.Erasure), false)]
  [InlineData(nameof(IdentityDeclaration.Unverified), nameof(DataSubjectRight.Portability), false)]
  public void DemandsAMotivationOnlyWhereDataCouldBeHandedToAnImpostor(
    string declaration,
    string right,
    bool demanded)
  {
    IdentityMotivation.IsDemandedBy(
      IdentityDeclaration.FromName(declaration),
      DataSubjectRight.FromName(right))
      .ShouldBe(demanded);
  }

  /// <summary>
  /// Le vocabulaire des méthodes est <b>fermé à quatre valeurs</b>, dont la laide. Il est fermé pour
  /// que le contrôle <b>dénombre</b> une pratique plutôt qu'il ne lise de la prose nominative.
  /// </summary>
  [Fact]
  public void NamesFourMethodsIncludingTheUglyOne()
  {
    IdentityVerificationMethod.List.OrderBy(method => method.Value).Select(method => method.Name).ShouldBe(
      ["AttributeCrosscheck", "PersonalRecognition", "CallbackOnKnownContact", "None"]);
  }

  /// <summary>
  /// Le vocabulaire des déclarations d'identité est <b>fermé à quatre valeurs</b>, dont
  /// <c>Unverified</c> : sans la valeur laide, l'opérateur pressé coche la valeur propre et le
  /// service fabrique un faux au lieu d'enregistrer un vide.
  /// </summary>
  [Fact]
  public void NamesFourIdentityDeclarationsIncludingTheOneNobodyVerified()
  {
    IdentityDeclaration.List.OrderBy(one => one.Value).Select(one => one.Name).ShouldBe(
      ["ApplicationSession", "ChannelControl", "OperatorAttested", "Unverified"]);
  }
}
