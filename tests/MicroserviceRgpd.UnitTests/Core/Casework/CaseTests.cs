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
  /// Le travail dû se range sous le <b>libellé</b> des systèmes — l'ordre sous lequel l'<c>Operator</c>
  /// cherche —, et non sous l'ordre où la base les rend : deux dossiers ouverts sur le même paysage
  /// se relisent dans le même ordre.
  /// </summary>
  [Fact]
  public void RangesTheDueWorkUnderTheLabelTheOperatorSearchesBy()
  {
    var opened = Open([DataSubjectRight.Access], ASystem("photos"), ASystem("boutique"), ASystem("journal"));

    opened.Claims[0].Steps.Select(step => step.DeclaredSystem.Value).ShouldBe(["boutique", "journal", "photos"]);
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
  /// Un dossier naît <b>ouvert</b>, et il n'existe aucun troisième état nommé « en retard » : le
  /// dépassement est un calcul fait à l'instant où l'<c>Operator</c> regarde.
  /// </summary>
  [Fact]
  public void BornsTheCaseOpenAndKnowsNoStateNamedLate()
  {
    Open([DataSubjectRight.Access]).State.ShouldBe(CaseState.Open);

    CaseState.List.OrderBy(state => state.Value).Select(state => state.Name).ShouldBe(["Open", "Closed"]);
  }

  /// <summary>
  /// <b>La racine porte l'état qu'un humain déclare, et aucune transition n'est interdite.</b>
  /// Ramener un <c>Done</c> à <c>Untreated</c> est un aveu : le refuser ferait choisir à
  /// l'<c>Operator</c> entre la vérité et le formulaire, et le service n'a jamais le droit de bloquer
  /// la trace la plus précieuse du dispositif.
  /// </summary>
  [Fact]
  public void CarriesEveryStateAHumanDeclaresIncludingTheOneThatAdmitsNobodyDidTheWork()
  {
    var opened = Open([DataSubjectRight.Access], ASystem("boutique"));

    opened.Declare(DataSubjectRight.Access, DeclaredSystemId.From("boutique"), StepState.Done).ShouldBeTrue();
    opened.Claims[0].Steps[0].State.ShouldBe(StepState.Done);

    opened.Declare(DataSubjectRight.Access, DeclaredSystemId.From("boutique"), StepState.Untreated).ShouldBeTrue();
    opened.Claims[0].Steps[0].State.ShouldBe(StepState.Untreated);
  }

  /// <summary>
  /// Un travail dû que le dossier ne porte pas se dit <b>faux</b>, sans rien changer. Ce n'est pas une
  /// programmation fautive : le <c>Manifest</c> vieillit exprès, et un système déclaré après
  /// l'ouverture n'a jamais eu de <c>Step</c> ici.
  /// </summary>
  [Fact]
  public void SaysSoWhenTheCaseNeverCarriedThatDueWork()
  {
    var opened = Open([DataSubjectRight.Access], ASystem("boutique"));

    opened.Declare(DataSubjectRight.Access, DeclaredSystemId.From("declare-apres"), StepState.Done).ShouldBeFalse();
    opened.Declare(DataSubjectRight.Erasure, DeclaredSystemId.From("boutique"), StepState.Done).ShouldBeFalse();

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
    opened.Reception.ShouldBe(ReceptionDate.Declared(Received));
  }

  /// <summary>
  /// Le sac garde ce qu'on lui a donné, dans l'ordre où on le lui a donné, <b>sans qu'une
  /// désignation y figure deux fois</b> : compter deux fois la même mentirait au <c>EvidenceLog</c> sur
  /// l'ampleur de la recherche.
  /// </summary>
  [Fact]
  public void KeepsTheBagInDeclaredOrderAndWithoutADuplicate()
  {
    var opened = Case.Open(
      CaseId.Next(),
      IdentityDeclaration.ApplicationSession,
      motivation: null,
      [
        Designation.Of(DesignationKind.Email, "jean.dupont@example.fr"),
        Designation.Of(DesignationKind.PersonName, "Jean Dupont"),
        Designation.Of(DesignationKind.Email, "jean.dupont@example.fr"),
      ],
      [DataSubjectRight.Access],
      ClaimOrigin.Named,
      [],
      ReceptionDate.Declared(Received));

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
      motivation: null,
      [
        Designation.Of(DesignationKind.Reference, "42"),
        Designation.Of(DesignationKind.Email, "jean.dupont@example.fr"),
      ],
      [DataSubjectRight.Access],
      ClaimOrigin.Named,
      [],
      ReceptionDate.Declared(Received));

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

  /// <summary>
  /// <b>Un <c>Claim</c> garde la porte sous laquelle il est né.</b> L'identité déclarée descend sur
  /// chaque droit à l'ouverture et s'y fige : c'est ce qui empêche une déclaration relevée en fin de
  /// dossier de réécrire la preuve d'hier.
  /// </summary>
  [Fact]
  public void FreezesOnEachClaimTheIdentityDeclarationInForceAtItsBirth()
  {
    var opened = Open(
      IdentityDeclaration.Unverified,
      null,
      ClaimOrigin.Named,
      [DataSubjectRight.Access, DataSubjectRight.Erasure]);

    opened.Claims.ShouldAllBe(claim => claim.IdentityAtOrigin == IdentityDeclaration.Unverified);
    opened.Claims.ShouldAllBe(claim => claim.Origin == ClaimOrigin.Named);
  }

  /// <summary>
  /// <b>Un <c>Claim</c> ne s'écrit que par la racine, l'origine comprise.</b> Il n'existe aucun
  /// chemin pour réécrire l'identité d'origine d'un droit déjà né — c'est la propriété qui rend le
  /// gel tenable, et non la seule discipline de qui manipule le dossier.
  /// </summary>
  [Fact]
  public void OffersNoWayToRewriteWhatAClaimFrozeAtItsBirth()
  {
    foreach (var name in new[] { nameof(Claim.Origin), nameof(Claim.IdentityAtOrigin), nameof(Claim.Confirmed) })
    {
      typeof(Claim).GetProperty(name)!.SetMethod?.IsPublic.ShouldNotBe(
        true,
        $"{name} est figé à la naissance du droit : le réécrire rendrait rétroactivement propre un "
        + "accès ouvert sur rien.");
    }
  }

  /// <summary>
  /// <b>Un <c>Access</c> ouvert sous une identité qui ne repose sur aucun contrôle du canal réclame
  /// une motivation</b> — et le dossier s'ouvre <b>quand même</b>. La faiblesse reste visible plutôt
  /// que contournée : la barrer aurait renvoyé la personne à son silence ou fait cocher n'importe
  /// quoi.
  /// </summary>
  [Fact]
  public void OpensTheCaseAndKeepsClaimingTheMotivationNobodyWrote()
  {
    var opened = Open(IdentityDeclaration.Unverified, null, ClaimOrigin.Named, [DataSubjectRight.Access]);

    opened.State.ShouldBe(CaseState.Open);
    opened.AwaitsAMotivation.ShouldBeTrue();
    opened.Claims[0].MotivationIsDemanded.ShouldBeTrue();
  }

  /// <summary>
  /// <c>None</c> est une <b>réponse</b>, et l'exigence est alors satisfaite. Sans la valeur laide,
  /// l'opérateur pressé cocherait la valeur propre — et le service enregistrerait un faux au lieu
  /// d'un aveu.
  /// </summary>
  [Fact]
  public void TakesTheAdmissionThatNothingWasDoneAsAnAnswerAndStopsClaiming()
  {
    var opened = Open(
      IdentityDeclaration.Unverified,
      IdentityMotivation.Of(IdentityVerificationMethod.None, detail: null),
      ClaimOrigin.Named,
      [DataSubjectRight.Access]);

    opened.AwaitsAMotivation.ShouldBeFalse();
    opened.Motivation!.Method.ShouldBe(IdentityVerificationMethod.None);
  }

  /// <summary>
  /// <b>L'exigence est étroite, et c'est délibéré.</b> Ce qu'on redoute est un accès accordé à un
  /// imposteur : la croiser avec les six autres droits ferait réclamer une motivation à chaque dépôt,
  /// et celle qui compte se noierait dans les autres.
  /// </summary>
  [Fact]
  public void ClaimsNoMotivationWhereNoDataWouldBeHandedToAnImpostor()
  {
    // Le même Unverified, mais sur un droit qui ne remet rien à personne.
    Open(IdentityDeclaration.Unverified, null, ClaimOrigin.Named, [DataSubjectRight.Erasure])
      .AwaitsAMotivation.ShouldBeFalse();

    // Le même Access, mais sous une identité qui repose sur un contrôle du canal.
    Open(IdentityDeclaration.ApplicationSession, null, ClaimOrigin.Named, [DataSubjectRight.Access])
      .AwaitsAMotivation.ShouldBeFalse();

    Open(IdentityDeclaration.ChannelControl, null, ClaimOrigin.Named, [DataSubjectRight.Access])
      .AwaitsAMotivation.ShouldBeFalse();

    // Attestée par l'opérateur repose, elle, sur ce qu'un humain a fait pour CE dossier : lui seul
    // peut dire quoi, et c'est ce qu'on lui demande.
    Open(IdentityDeclaration.OperatorAttested, null, ClaimOrigin.Named, [DataSubjectRight.Access])
      .AwaitsAMotivation.ShouldBeTrue();
  }

  /// <summary>
  /// <b>Un <c>Claim</c> <c>Proposed</c> naît non confirmé, et se confirme dans le dossier.</b> Il
  /// n'existe aucun état d'attente hors du <c>Case</c> : le dossier est ouvert, le délai court, et
  /// l'attente est visible pendant que le compteur tourne.
  /// </summary>
  [Fact]
  public void BornsAProposedClaimUnconfirmedAndConfirmsItInsideTheOpenCase()
  {
    var opened = Open(
      IdentityDeclaration.Unverified,
      null,
      ClaimOrigin.Proposed,
      [DataSubjectRight.Access]);

    // Le dossier est OUVERT, pas en attente : aucun vestibule n'existe.
    opened.State.ShouldBe(CaseState.Open);
    opened.Claims[0].AwaitsConfirmation.ShouldBeTrue();
    opened.Claims[0].State.ShouldBe(ClaimState.Open);

    opened.Confirm(DataSubjectRight.Access).ShouldBeTrue();

    opened.Claims[0].AwaitsConfirmation.ShouldBeFalse();

    // Un second geste ne change RIEN, et se dit faux : l'EvidenceLog consigne les faits qui changent
    // quelque chose, jamais leur répétition, et cette règle est tenue par l'appelant. Rendre vrai
    // ici lui ferait écrire une seconde ligne identique.
    opened.Confirm(DataSubjectRight.Access).ShouldBeFalse();
    opened.Claims[0].AwaitsConfirmation.ShouldBeFalse();
  }

  /// <summary>
  /// Un droit qui n'attendait rien ne se « confirme » pas : sans quoi la preuve porterait la
  /// confirmation d'un droit que la personne avait elle-même désigné.
  /// </summary>
  [Fact]
  public void ConfirmsNothingOnARightThatNeverAwaitedIt()
  {
    var opened = Open(IdentityDeclaration.Unverified, null, ClaimOrigin.Named, [DataSubjectRight.Access]);

    opened.Confirm(DataSubjectRight.Access).ShouldBeFalse();
  }

  /// <summary>
  /// <b>La réclamation d'une motivation peut être satisfaite après coup</b>, et elle doit pouvoir
  /// l'être : une exigence qu'on ne peut pas satisfaire cesse d'être lue, et le bandeau permanent
  /// qu'on apprend à ne plus voir rendrait la faiblesse invisible.
  /// </summary>
  [Fact]
  public void LetsSomebodyWeighTheIdentityAfterTheFactAndStopsClaiming()
  {
    var opened = Open(IdentityDeclaration.Unverified, null, ClaimOrigin.Named, [DataSubjectRight.Access]);

    opened.AwaitsAMotivation.ShouldBeTrue();

    opened.DeclareMotivation(
      IdentityMotivation.Of(IdentityVerificationMethod.CallbackOnKnownContact, "Rappelée au contrat."))
      .ShouldBeTrue();

    opened.AwaitsAMotivation.ShouldBeFalse();
    opened.Motivation!.Method.ShouldBe(IdentityVerificationMethod.CallbackOnKnownContact);
  }

  /// <summary>
  /// <b>Ce qu'on pèse aujourd'hui ne réécrit pas la porte sous laquelle le droit est né.</b> C'est
  /// tout le propos du gel : un accès ouvert sur la foi de rien ne devient pas rétroactivement propre
  /// parce que quelqu'un a fini par passer un coup de fil.
  /// </summary>
  [Fact]
  public void LeavesEachClaimsFrozenOriginUntouchedByAMotivationWrittenLater()
  {
    var opened = Open(IdentityDeclaration.Unverified, null, ClaimOrigin.Named, [DataSubjectRight.Access]);

    opened.DeclareMotivation(
      IdentityMotivation.Of(IdentityVerificationMethod.PersonalRecognition, detail: null));

    opened.Claims[0].IdentityAtOrigin.ShouldBe(IdentityDeclaration.Unverified);
    opened.Claims[0].MotivationIsDemanded.ShouldBeTrue(
      "Le droit continue de dire qu'il EXIGEAIT une motivation : c'est le dossier qui en porte une, "
      + "pas le droit qui cesse d'en avoir eu besoin.");
  }

  /// <summary>
  /// Une motivation déjà écrite ne s'écrase pas : la réécrire ferait réécrire ce que quelqu'un a
  /// signé, et rien n'était réclamé.
  /// </summary>
  [Fact]
  public void RefusesToOverwriteAMotivationSomebodyAlreadySigned()
  {
    var opened = Open(
      IdentityDeclaration.Unverified,
      IdentityMotivation.Of(IdentityVerificationMethod.None, detail: null),
      ClaimOrigin.Named,
      [DataSubjectRight.Access]);

    opened.DeclareMotivation(
      IdentityMotivation.Of(IdentityVerificationMethod.PersonalRecognition, detail: null))
      .ShouldBeFalse();

    opened.Motivation!.Method.ShouldBe(IdentityVerificationMethod.None);
  }

  /// <summary>
  /// Les origines qui portent <b>déjà</b> le fait d'un humain naissent confirmées : rien n'est
  /// réclamé à qui a lui-même désigné ou attesté le droit.
  /// </summary>
  [Fact]
  public void ClaimsNoConfirmationWhereAHumanAlreadyStandsBehindTheRight()
  {
    Open(IdentityDeclaration.Unverified, null, ClaimOrigin.Named, [DataSubjectRight.Access])
      .Claims[0].AwaitsConfirmation.ShouldBeFalse();

    Open(IdentityDeclaration.Unverified, null, ClaimOrigin.Attested, [DataSubjectRight.Access])
      .Claims[0].AwaitsConfirmation.ShouldBeFalse();
  }

  /// <summary>
  /// Un droit que le dossier ne porte pas se dit <b>faux</b>, sans rien changer : un écran affiché il
  /// y a une minute peut nommer un droit qu'un autre geste vient de changer, et ce n'est pas une
  /// programmation fautive.
  /// </summary>
  [Fact]
  public void SaysSoWhenTheCaseNeverCarriedTheRightSomeoneTriesToConfirm()
  {
    var opened = Open(IdentityDeclaration.Unverified, null, ClaimOrigin.Proposed, [DataSubjectRight.Access]);

    opened.Confirm(DataSubjectRight.Erasure).ShouldBeFalse();
    opened.Claims[0].AwaitsConfirmation.ShouldBeTrue();
  }

  /// <summary>
  /// Les trois origines d'un <c>Claim</c> existent, et rien de plus. <c>Proposed</c> n'est
  /// <b>pas</b> un quatrième <c>ClaimState</c> : la provenance d'un droit et l'état de la réponse
  /// due à la personne sont deux questions distinctes.
  /// </summary>
  [Fact]
  public void NamesThreeOriginsForAClaimAndAddsNoStateForThem()
  {
    ClaimOrigin.List.OrderBy(origin => origin.Value).Select(origin => origin.Name).ShouldBe(
      ["Named", "Attested", "Proposed"]);

    ClaimState.List.OrderBy(state => state.Value).Select(state => state.Name).ShouldBe(
      ["Open", "Answered", "Refused"]);
  }

  private static Case Open(DataSubjectRight[] rights, params DeclaredSystem[] systems)
  {
    return Open(IdentityDeclaration.ApplicationSession, null, ClaimOrigin.Named, rights, systems);
  }

  private static Case Open(
    IdentityDeclaration declaration,
    IdentityMotivation? motivation,
    ClaimOrigin origin,
    DataSubjectRight[] rights,
    params DeclaredSystem[] systems)
  {
    return Case.Open(
      CaseId.Next(),
      declaration,
      motivation,
      [Designation.Of(DesignationKind.Email, "jean.dupont@example.fr")],
      rights,
      origin,
      systems,
      ReceptionDate.Declared(Received));
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
