using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.Adapters;
using MicroserviceRgpd.Core.SharedKernel;

// Shouldly porte un type du même nom, réservé à ses propres tables de cas.
using Case = MicroserviceRgpd.Core.Casework.Case;

namespace MicroserviceRgpd.UnitTests.Core.Casework;

/// <summary>
/// La <b>clôture</b>, et la destruction du nominatif à l'instant même.
/// </summary>
/// <remarks>
/// <para>
/// <b>Deux exigences qui se tiennent debout ensemble.</b> La clôture ne propage rien et ne gèle
/// rien — un <c>Step</c> laissé <c>ToDo</c> le reste, et se lit comme l'oubli qu'il est — et elle
/// détruit tout ce qui nomme, sans délai de conservation « au cas où ».
/// </para>
/// <para>
/// <b>Ce qui survit se compte.</b> La méthode de vérification d'identité, les états déclarés, les
/// dates. Ce qui meurt raconte : les désignations, le détail de la motivation, les questions
/// ouvertes.
/// </para>
/// </remarks>
public class CaseClosureTests
{
  private static readonly DateTimeOffset Received = new(2026, 4, 10, 9, 0, 0, TimeSpan.Zero);

  private static readonly DateTimeOffset Closed = new(2026, 5, 4, 14, 0, 0, TimeSpan.Zero);

  /// <summary>Un dossier neuf est ouvert, sans cause ni date de clôture.</summary>
  [Fact]
  public void OpensWithoutAnyClosure()
  {
    var opened = ACase();

    opened.State.ShouldBe(CaseState.Open);
    opened.IsClosed.ShouldBeFalse();
    opened.ClosingCause.ShouldBeNull();
    opened.ClosedOn.ShouldBeNull();
  }

  /// <summary>
  /// <b>La clôture nomme sa cause et son instant.</b> Elle n'est jamais un simple passage d'état :
  /// le dossier clos doit dire par quoi il s'est clos, et quand.
  /// </summary>
  [Fact]
  public void ClosesUnderACauseAndAnInstant()
  {
    var opened = ACase();

    opened.Close(ClosingCause.Answered, Closed).ShouldBeTrue();

    opened.State.ShouldBe(CaseState.Closed);
    opened.IsClosed.ShouldBeTrue();
    opened.ClosingCause.ShouldBe(ClosingCause.Answered);
    opened.ClosedOn.ShouldBe(Closed);
  }

  /// <summary>L'instant entre en UTC, comme partout : deux clôtures se comparent.</summary>
  [Fact]
  public void NormalisesTheClosingInstantOntoUtc()
  {
    var opened = ACase();

    opened.Close(ClosingCause.Answered, new DateTimeOffset(2026, 5, 4, 16, 0, 0, TimeSpan.FromHours(2)));

    opened.ClosedOn!.Value.Offset.ShouldBe(TimeSpan.Zero);
    opened.ClosedOn.ShouldBe(new DateTimeOffset(2026, 5, 4, 14, 0, 0, TimeSpan.Zero));
  }

  /// <summary>
  /// <b>Un dossier ne se clôt qu'une fois.</b> Reclore réécrirait la cause et la date d'une clôture
  /// que quelqu'un a signée — et le nominatif détruit ne reviendrait pas pour autant.
  /// </summary>
  [Fact]
  public void ClosesOnceAndOnlyOnce()
  {
    var opened = ACase();

    opened.Close(ClosingCause.Answered, Closed).ShouldBeTrue();
    opened.Close(ClosingCause.Abandoned, Closed.AddDays(2)).ShouldBeFalse();

    opened.ClosingCause.ShouldBe(ClosingCause.Answered);
    opened.ClosedOn.ShouldBe(Closed);
  }

  /// <summary>
  /// <b>Le sac de désignations tombe à l'instant même.</b> C'est la seule identité qui circule, et
  /// rien ne justifie qu'elle survive au dossier — pas même « au cas où ».
  /// </summary>
  [Fact]
  public void DestroysTheDesignationsAtThatVeryInstant()
  {
    var opened = ACase();

    opened.Designations.ShouldNotBeEmpty();

    opened.Close(ClosingCause.Answered, Closed);

    opened.Designations.ShouldBeEmpty();
  }

  /// <summary>
  /// <b>La méthode survit, le détail meurt.</b> La première se compte et son lecteur est le
  /// contrôle ; le second nomme, et n'a aucune raison de survivre à la personne dont il parle.
  /// </summary>
  [Fact]
  public void KeepsTheVerificationMethodAndDestroysItsProse()
  {
    var opened = ACase(
      motivation: IdentityMotivation.Of(
        IdentityVerificationMethod.CallbackOnKnownContact,
        "Rappel au 06 12 34 56 78, Jean Dupont a confirmé sa date de naissance."));

    opened.Close(ClosingCause.Answered, Closed);

    opened.Motivation.ShouldNotBeNull();
    opened.Motivation!.Method.ShouldBe(IdentityVerificationMethod.CallbackOnKnownContact);
    opened.Motivation.Detail.ShouldBeNull();
  }

  /// <summary>Un dossier que personne n'a motivé se clôt sans qu'une motivation n'apparaisse.</summary>
  [Fact]
  public void InventsNoMotivationForACaseThatNeverHadOne()
  {
    var opened = ACase();

    opened.Close(ClosingCause.Answered, Closed);

    opened.Motivation.ShouldBeNull();
  }

  /// <summary>
  /// <b>Les questions ouvertes meurent avec le dossier.</b> Elles sont du texte qui meurt : ce
  /// qui manquait aujourd'hui n'a plus de lecteur demain, et l'<c>EvidenceLog</c> en garde le jour.
  /// </summary>
  [Fact]
  public void DestroysTheOpenQuestions()
  {
    var opened = ACase();

    opened.Ask(OpenQuestionSubject.Designation, Received.AddDays(2));
    opened.Questions.ShouldNotBeEmpty();

    opened.Close(ClosingCause.Answered, Closed);

    opened.Questions.ShouldBeEmpty();
  }

  /// <summary>
  /// <b>Les localisations tombent, elles aussi.</b> Elles portent des références opaques et les
  /// désignations que les réserves proposaient : c'est du nominatif, quoi qu'en dise leur forme.
  /// </summary>
  [Fact]
  public void DestroysWhatTheLocatingsHeld()
  {
    var opened = ACase(systems: [ASystem("crm")]);

    opened.LocateServed(
      DeclaredSystemId.From("crm"),
      LocateFindings.Nothing,
      Received.AddDays(1));

    opened.Locatings.ShouldNotBeEmpty();

    opened.Close(ClosingCause.Answered, Closed);

    opened.Locatings.ShouldBeEmpty();
  }

  /// <summary>
  /// <b>La clôture ne propage rien et ne gèle rien.</b> Un <c>Step</c> laissé <c>ToDo</c> reste
  /// <c>ToDo</c> dans le dossier clos, où il se lit comme un oubli. Le masquer d'un état rassurant
  /// ferait perdre la seule trace que l'<c>Omission silencieuse</c> laisse jamais.
  /// </summary>
  [Fact]
  public void LeavesAStepLeftToDoExactlyAsItWas()
  {
    var opened = ACase(systems: [ASystem("crm")], rights: [DataSubjectRight.Access]);

    opened.Close(ClosingCause.Answered, Closed);

    opened.Claims.Single().Steps.Single().State.ShouldBe(StepState.ToDo);
  }

  /// <summary>
  /// <b>La clôture ne répond à aucun <c>Claim</c>.</b> Répondre est un geste d'humain, nommé et
  /// daté ; clore un dossier ne peut pas valoir réponse sur six droits d'un seul clic.
  /// </summary>
  [Fact]
  public void AnswersNoClaimOfItsOwnAccord()
  {
    var opened = ACase(rights: [DataSubjectRight.Access]);

    opened.Close(ClosingCause.Answered, Closed);

    opened.Claims.Single().State.ShouldBe(ClaimState.Open);
  }

  /// <summary>
  /// <b>Les <c>Step</c> non terminaux se réclament, ils ne bloquent pas.</b> C'est ce que l'écran
  /// montre avant de laisser signer — et la clôture se laisse faire quand l'humain passe outre.
  /// </summary>
  [Fact]
  public void CountsTheStepsNoOneHasDeclared()
  {
    var opened = ACase(
      systems: [ASystem("crm"), ASystem("facturation")],
      rights: [DataSubjectRight.Access]);

    opened.StepsAwaitingADeclaration.Count.ShouldBe(2);

    opened.Declare(DataSubjectRight.Access, DeclaredSystemId.From("crm"), StepState.Untreated);

    var awaiting = opened.StepsAwaitingADeclaration.Single();

    awaiting.DeclaredSystem.ShouldBe(DeclaredSystemId.From("facturation"));
  }

  /// <summary>
  /// <c>Awaiting</c> <b>n'est pas terminal</b> : une promesse d'<c>Adapter</c> ne vaut pas
  /// déclaration humaine, et la clôture la réclame comme le reste.
  /// </summary>
  [Fact]
  public void StillClaimsAStepAnAdapterOnlyPromisedToAnswer()
  {
    var opened = ACase(systems: [ASystem("crm")], rights: [DataSubjectRight.Access]);

    opened.Declare(DataSubjectRight.Access, DeclaredSystemId.From("crm"), StepState.Awaiting);

    opened.StepsAwaitingADeclaration.ShouldNotBeEmpty();
  }

  /// <summary>
  /// <b>Les <c>Claim</c> ouverts se réclament aussi</b>, et de la même façon : l'écran les montre,
  /// la clôture ne les exige jamais.
  /// </summary>
  [Fact]
  public void CountsTheClaimsNoOneHasAnswered()
  {
    var opened = ACase(rights: [DataSubjectRight.Access, DataSubjectRight.Portability]);

    opened.ClaimsAwaitingAnOutcome.Count.ShouldBe(2);

    opened.Answer(DataSubjectRight.Access).ShouldBeTrue();

    opened.ClaimsAwaitingAnOutcome.Single().Right.ShouldBe(DataSubjectRight.Portability);
  }

  /// <summary>
  /// <b>Répondre atteste l'acte de répondre</b>, et rien de plus : un <c>Step</c> resté inatteint
  /// ne barre pas la route, et reste lisible à côté.
  /// </summary>
  [Fact]
  public void AnswersARightWhoseWorkNoOneEverDid()
  {
    var opened = ACase(systems: [ASystem("crm")], rights: [DataSubjectRight.Access]);

    opened.Answer(DataSubjectRight.Access).ShouldBeTrue();

    var claim = opened.Claims.Single();

    claim.State.ShouldBe(ClaimState.Answered);
    claim.Steps.Single().State.ShouldBe(StepState.ToDo);
  }

  /// <summary>
  /// Répondre deux fois n'est pas un fait nouveau : le second passage rend faux, pour que
  /// l'appelant n'écrive pas une seconde ligne de preuve identique.
  /// </summary>
  [Fact]
  public void AnswersARightOnceAndOnlyOnce()
  {
    var opened = ACase(rights: [DataSubjectRight.Access]);

    opened.Answer(DataSubjectRight.Access).ShouldBeTrue();
    opened.Answer(DataSubjectRight.Access).ShouldBeFalse();
  }

  /// <summary>Un droit que le dossier ne porte pas ne se répond pas — sans que rien ne casse.</summary>
  [Fact]
  public void RefusesToAnswerARightTheCaseDoesNotCarry()
  {
    var opened = ACase(rights: [DataSubjectRight.Access]);

    opened.Answer(DataSubjectRight.Portability).ShouldBeFalse();
  }

  /// <summary>
  /// <b>Un dossier clos ne se travaille plus.</b> Ses désignations n'existent plus : un
  /// <c>Locate</c> chercherait sous rien, et une réponse portée après coup daterait un travail
  /// qu'aucune donnée ne soutient plus.
  /// </summary>
  [Fact]
  public void RefusesEveryGestureOnceClosed()
  {
    var opened = ACase(systems: [ASystem("crm")], rights: [DataSubjectRight.Access]);

    opened.Close(ClosingCause.Answered, Closed);

    opened.Answer(DataSubjectRight.Access).ShouldBeFalse();
    opened.Declare(DataSubjectRight.Access, DeclaredSystemId.From("crm"), StepState.Done).ShouldBeFalse();
    opened.Ask(OpenQuestionSubject.Designation, Closed.AddDays(1)).ShouldBeFalse();
  }

  /// <summary>Une cause absente est une programmation fautive, pas un cas de bord à absorber.</summary>
  [Fact]
  public void RefusesAClosureThatNamesNoCause()
  {
    var opened = ACase();

    Should.Throw<ArgumentNullException>(() => opened.Close(null!, Closed));
  }

  private static Case ACase(
    IdentityMotivation? motivation = null,
    DeclaredSystem[]? systems = null,
    DataSubjectRight[]? rights = null)
  {
    return Case.Open(
      CaseId.Next(),
      IdentityDeclaration.Unverified,
      motivation,
      [Designation.Of(DesignationKind.Email, "jean.dupont@example.fr")],
      rights ?? [DataSubjectRight.Access],
      ClaimOrigin.Named,
      systems ?? [],
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
      new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.Zero));
  }
}
