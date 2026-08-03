using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.SharedKernel;

// Shouldly porte un type du même nom, réservé à ses propres tables de cas. Le mot du glossaire
// l'emporte, et l'alias dit lequel des deux on lit.
using Case = MicroserviceRgpd.Core.Casework.Case;

namespace MicroserviceRgpd.UnitTests.Core.Casework;

/// <summary>
/// Ce que l'ouverture d'un dossier fixe, et ce qu'elle refuse de fixer.
/// <para>
/// Le test le plus important de ce fichier n'est pas celui d'un chemin heureux : c'est
/// <see cref="OpensASingleCaseWhateverTheNumberOfRights"/>. « Une demande, un <c>Case</c> » est la
/// frontière de l'agrégat, et elle se pose maintenant parce qu'elle ne se déplacera plus une fois
/// des dossiers écrits.
/// </para>
/// </summary>
public class CaseTests
{
  private static readonly DateTimeOffset Received = new(2026, 8, 1, 8, 0, 0, TimeSpan.Zero);

  /// <summary>
  /// <b>Une demande portant plusieurs droits donne un seul dossier.</b> La règle « lire avant
  /// d'effacer » traverse les <c>Claim</c>, et aucune frontière plus fine ne pourrait la tenir.
  /// </summary>
  [Fact]
  public void OpensASingleCaseWhateverTheNumberOfRights()
  {
    var opened = Open([DataSubjectRight.Access, DataSubjectRight.Erasure, DataSubjectRight.Portability]);

    opened.Claims.Count.ShouldBe(3);
    opened.Claims.Select(claim => claim.Right).ShouldBe(
      [DataSubjectRight.Access, DataSubjectRight.Erasure, DataSubjectRight.Portability]);
  }

  /// <summary>
  /// Deux fois le même droit est <b>une</b> réclamation : le service doit une réponse par droit, et
  /// non une par fois qu'on le lui a demandé.
  /// </summary>
  [Fact]
  public void FoldsARightClaimedTwiceIntoASingleClaim()
  {
    var opened = Open([DataSubjectRight.Access, DataSubjectRight.Access]);

    opened.Claims.Count.ShouldBe(1);
    opened.Claims[0].Right.ShouldBe(DataSubjectRight.Access);
  }

  /// <summary>
  /// L'ordre est celui de la taxonomie et non celui de la saisie : deux demandes portant les mêmes
  /// droits se relisent alors pareil, en base comme à l'écran.
  /// </summary>
  [Fact]
  public void RangesTheClaimsInTheOrderOfTheTaxonomyRatherThanOfWhatWasSent()
  {
    var opened = Open([DataSubjectRight.Objection, DataSubjectRight.Access, DataSubjectRight.Erasure]);

    opened.Claims.Select(claim => claim.Right).ShouldBe(
      [DataSubjectRight.Access, DataSubjectRight.Erasure, DataSubjectRight.Objection]);
  }

  /// <summary>
  /// <c>OutOfScope</c> n'est pas un droit réclamable : c'est le verdict qu'aucun ne l'a été. Une
  /// demande qui n'exerce aucun droit ouvre un dossier <b>sans aucun <c>Claim</c></b>, et se clora
  /// <c>NotApplicable</c> sous la signature d'un humain — jamais par la machine seule.
  /// </summary>
  [Fact]
  public void RefusesToClaimTheVerdictThatNoRightWasRecognised()
  {
    var refusal = Should.Throw<ArgumentException>(() => Open([DataSubjectRight.OutOfScope]));

    refusal.Message.ShouldContain("OutOfScope");
  }

  /// <summary>
  /// Une demande n'exerçant aucun droit <b>entre quand même</b>. Un dossier sans réclamation est un
  /// fait, jamais une saisie inachevée : le refuser à l'entrée laisserait un verdict orphelin.
  /// </summary>
  [Fact]
  public void OpensACaseThatClaimsNoRightAtAll()
  {
    var opened = Open([]);

    opened.Claims.ShouldBeEmpty();
  }

  /// <summary>
  /// <b>Un <c>Step</c> naît par (<c>Claim</c>, <c>DeclaredSystem</c>)</b> — le grain est le système,
  /// jamais la <c>Capability</c>.
  /// </summary>
  [Fact]
  public void GivesEachClaimOneStepPerDeclaredSystem()
  {
    var opened = Open(
      [DataSubjectRight.Access, DataSubjectRight.Erasure],
      ASystem("boutique"),
      ASystem("journal"),
      ASystem("compta-scellee"));

    foreach (var claim in opened.Claims)
    {
      claim.Steps.Select(step => step.DeclaredSystem).ShouldBe(
        [DeclaredSystemId.From("boutique"), DeclaredSystemId.From("compta-scellee"), DeclaredSystemId.From("journal")],
        ignoreOrder: true);
    }
  }

  /// <summary>
  /// Un catalogue vide ouvre un dossier sans aucun travail dû. C'est l'état d'un service qu'on
  /// vient d'installer, et l'écran doit pouvoir le dire plutôt que de refuser la demande.
  /// </summary>
  [Fact]
  public void OpensACaseOnAnEmptyManifestWithoutAnyStep()
  {
    var opened = Open([DataSubjectRight.Access]);

    opened.Claims[0].Steps.ShouldBeEmpty();
  }

  /// <summary>
  /// Les états de naissance : <c>Open</c> pour la réclamation, <c>ToDo</c> pour le travail dû.
  /// <c>ToDo</c> est délibérément le nom d'un travail <b>attendu</b> : resté tel dans un dossier
  /// clos, il se lira comme un oubli plutôt que comme un neutre rassurant.
  /// </summary>
  [Fact]
  public void BornsEveryClaimOpenAndEveryStepToDo()
  {
    var opened = Open([DataSubjectRight.Access], ASystem("boutique"));

    opened.Claims[0].State.ShouldBe(ClaimState.Open);
    opened.Claims[0].Steps[0].State.ShouldBe(StepState.ToDo);
  }

  /// <summary>
  /// Le dossier porte ce que le canal a déclaré de l'identité, <b>sans en juger la valeur</b>, et
  /// la date de réception telle qu'elle lui a été dite.
  /// </summary>
  [Fact]
  public void CarriesTheDeclaredIdentityAndTheDeclaredReceptionDate()
  {
    var opened = Open([DataSubjectRight.Access]);

    opened.IdentityDeclaration.ShouldBe(IdentityDeclaration.ApplicationSession);
    opened.ReceivedOn.ShouldBe(Received);
  }

  /// <summary>
  /// Le sac garde ce qu'on lui a donné, dans l'ordre où on le lui a donné, <b>sans qu'une
  /// désignation y figure deux fois</b> : compter deux fois la même mentirait au <c>Ledger</c> sur
  /// l'ampleur de la recherche.
  /// </summary>
  [Fact]
  public void KeepsTheBagInDeclaredOrderAndWithoutADuplicate()
  {
    var opened = Case.Open(
      CaseId.Next(),
      IdentityDeclaration.ApplicationSession,
      [
        Designation.Of(DesignationKind.Email, "jean.dupont@example.fr"),
        Designation.Of(DesignationKind.PersonName, "Jean Dupont"),
        Designation.Of(DesignationKind.Email, "jean.dupont@example.fr"),
      ],
      [DataSubjectRight.Access],
      Manifest.Empty,
      Received);

    opened.Designations.Count.ShouldBe(2);
    opened.Designations[0].Value.ShouldBe("jean.dupont@example.fr");
    opened.Designations[1].Kind.ShouldBe(DesignationKind.PersonName);
  }

  /// <summary>
  /// L'identifiant natif de l'application entre comme <b>une désignation parmi d'autres</b>. Rien
  /// dans le dossier ne le distingue : ni champ propre, ni rang, ni priorité de recherche.
  /// </summary>
  [Fact]
  public void GivesTheApplicationsOwnIdentifierNoAuthorityOverTheOthers()
  {
    var opened = Case.Open(
      CaseId.Next(),
      IdentityDeclaration.ApplicationSession,
      [
        Designation.Of(DesignationKind.Reference, "42"),
        Designation.Of(DesignationKind.Email, "jean.dupont@example.fr"),
      ],
      [DataSubjectRight.Access],
      Manifest.Empty,
      Received);

    // La seule chose qui distingue la référence native est sa nature, et elle est de même rang que
    // les trois autres : aucune propriété du dossier ne la nomme.
    typeof(Case).GetProperties()
      .Select(property => property.Name)
      .ShouldNotContain(name => name.Contains("SubjectId", StringComparison.OrdinalIgnoreCase));

    opened.Designations.Count.ShouldBe(2);
  }

  /// <summary>
  /// Les cinq états d'un <c>Step</c> et les trois d'un <c>Claim</c> existent, et rien de plus.
  /// <c>OutOfReach</c> et <c>Untreated</c> ne fusionnent pas : le premier est structurel et
  /// annonçable au premier jour, le second est un aveu constaté à la fin.
  /// </summary>
  [Fact]
  public void NamesFiveStatesForAStepAndThreeForAClaim()
  {
    // Rangés par leur valeur : `SmartEnum.List` trie par nom, et l'ordre du vocabulaire — celui
    // dans lequel un dossier progresse — se lirait autrement d'une lecture à l'autre.
    StepState.List.OrderBy(state => state.Value).Select(state => state.Name).ShouldBe(
      ["ToDo", "Awaiting", "Done", "OutOfReach", "Untreated"]);

    ClaimState.List.OrderBy(state => state.Value).Select(state => state.Name).ShouldBe(
      ["Open", "Answered", "Refused"]);
  }

  /// <summary>
  /// <b>Aucun chemin d'écriture vers un <c>Claim</c> ou un <c>Step</c> hors de la racine.</b> La
  /// règle tient par la forme des types — pas de constructeur public, pas de propriété qu'on puisse
  /// écrire de l'extérieur — plutôt que par la discipline de qui les manipule.
  /// </summary>
  [Fact]
  public void OffersNoWayToBuildOrWriteAClaimOrAStepFromOutsideTheRoot()
  {
    // La racine est unique : le dépôt générique étant contraint aux agrégats racines, ce qui n'en
    // est pas un n'a structurellement aucun dépôt.
    typeof(Case).IsAssignableTo(typeof(IAggregateRoot)).ShouldBeTrue();

    foreach (var inside in new[] { typeof(Claim), typeof(Step) })
    {
      inside.IsAssignableTo(typeof(IAggregateRoot)).ShouldBeFalse(
        $"{inside.Name} n'est pas une racine : un dépôt à lui rouvrirait le chemin d'écriture que "
        + "l'invariant « lire avant d'effacer » ferme.");


      inside.GetConstructors().ShouldBeEmpty(
        $"{inside.Name} se construit par le Case et par lui seul.");

      inside.GetProperties()
        .Where(property => property.SetMethod?.IsPublic == true)
        .ShouldBeEmpty($"{inside.Name} ne s'écrit que par le Case.");
    }
  }

  private static Case Open(DataSubjectRight[] rights, params DeclaredSystem[] systems)
  {
    return Case.Open(
      CaseId.Next(),
      IdentityDeclaration.ApplicationSession,
      [Designation.Of(DesignationKind.Email, "jean.dupont@example.fr")],
      rights,
      Manifest.Of(systems),
      Received);
  }

  private static DeclaredSystem ASystem(string id)
  {
    return DeclaredSystem.Declare(
      DeclaredSystemId.From(id),
      SystemLabel.From(id),
      SystemContents.From("Ce qu'il contient, dans les mots de qui l'a déclaré."),
      [],
      adapterAddress: null,
      new DateTimeOffset(2026, 7, 30, 9, 0, 0, TimeSpan.Zero));
  }
}
