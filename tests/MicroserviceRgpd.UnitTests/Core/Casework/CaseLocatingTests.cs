using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.Adapters;
using MicroserviceRgpd.Core.SharedKernel;

// Shouldly porte un type du même nom, réservé à ses propres tables de cas.
using Case = MicroserviceRgpd.Core.Casework.Case;

namespace MicroserviceRgpd.UnitTests.Core.Casework;

/// <summary>
/// Ce qu'un <c>Locate</c> laisse dans un dossier, et ce qu'un humain seul peut y trancher.
/// </summary>
/// <remarks>
/// Le fait cardinal : <b>aucun rattachement n'est décidé par le service</b>. Une réserve que personne
/// n'a tranchée n'est pas un rattachement — ni pour l'écran, ni pour le constat réclamé, ni pour la
/// question que six zéros font naître.
/// </remarks>
public class CaseLocatingTests
{
  private static readonly DateTimeOffset Opened = new(2026, 4, 10, 9, 0, 0, TimeSpan.Zero);
  private static readonly DeclaredSystemId Boutique = DeclaredSystemId.From("brocanto-boutique");
  private static readonly DeclaredSystemId Journal = DeclaredSystemId.From("brocanto-journal");

  /// <summary>
  /// Le noyau certain se lit dans le dossier, et il <b>détient un rattachement</b> : c'est cela qui
  /// lèvera l'exigence d'un constat sur ce système.
  /// </summary>
  [Fact]
  public void KeepsTheCertainCoreItWasServed()
  {
    var opened = ACase();

    opened.LocateServed(Boutique, Findings(certain: ["clients#1203"]), Opened);

    var locating = opened.LocatingIn(Boutique).ShouldNotBeNull();

    locating.LastOutcome.ShouldBe(AdapterOutcome.Served);
    locating.Certain.ShouldHaveSingleItem().Value.ShouldBe("clients#1203");
    locating.HoldsAnAttachment.ShouldBeTrue();
    opened.HoldsAnAttachmentIn(Boutique).ShouldBeTrue();
  }

  /// <summary>
  /// <b>Une réserve que personne n'a tranchée n'est pas un rattachement.</b> Compter comme trouvé ce
  /// que personne n'a regardé lèverait l'exigence du constat au moment précis où elle vaut le plus.
  /// </summary>
  [Fact]
  public void HoldsNoAttachmentWhileAReserveAwaitsAHuman()
  {
    var opened = ACase();

    opened.LocateServed(Boutique, Findings(reserved: [("clients#4417", "Deux comptes portent ce nom.", null)]), Opened);

    opened.HoldsAnAttachmentIn(Boutique).ShouldBeFalse();
    opened.HoldsAnyAttachment.ShouldBeFalse();
    opened.LocatingIn(Boutique)!.AwaitsAnArbitration.ShouldBeTrue();

    // Et l'exigence du constat tient : un « fait » à zéro rattachement se motive.
    opened.FindingIsDemandedBy(StepState.Done, Boutique).ShouldBeTrue();
  }

  /// <summary>
  /// <b>Une réserve rattachée par un humain verse ses désignations au sac</b>, et l'appel suivant les
  /// portera : confirmer une adresse trouvée en base ouvre le journal applicatif qui la cherchait sous
  /// une autre casse.
  /// </summary>
  [Fact]
  public void EnrichesTheBagWhenAHumanAttachesAReserve()
  {
    var opened = ACase();

    opened.LocateServed(
      Boutique,
      Findings(reserved: [("clients#4417", "Deux comptes portent ce nom.", "Jean.Dupont@Example.fr")]),
      Opened);

    opened.Designations.Count.ShouldBe(1);

    var arbitrated = opened.Arbitrate(Boutique, OpaqueReference.Of("clients#4417"), ReservationState.Attached);

    arbitrated.ShouldNotBeNull().State.ShouldBe(ReservationState.Attached);
    opened.Designations.Select(designation => designation.Value)
      .ShouldBe(["jean.dupont@example.fr", "Jean.Dupont@Example.fr"]);

    // Le rattachement est désormais détenu, et le constat n'est plus réclamé sur ce système.
    opened.HoldsAnAttachmentIn(Boutique).ShouldBeTrue();
    opened.FindingIsDemandedBy(StepState.Done, Boutique).ShouldBeFalse();
  }

  /// <summary>
  /// <b>Une réserve écartée n'enrichit rien</b>, et le fait reste écrit : un arbitrage rendu n'est pas
  /// une réserve disparue.
  /// </summary>
  [Fact]
  public void AddsNothingToTheBagWhenAHumanSetsAReserveAside()
  {
    var opened = ACase();

    opened.LocateServed(
      Boutique,
      Findings(reserved: [("clients#4417", "Un homonyme, créé en 2019.", "autre.jean@example.fr")]),
      Opened);

    opened.Arbitrate(Boutique, OpaqueReference.Of("clients#4417"), ReservationState.SetAside);

    opened.Designations.Count.ShouldBe(1);
    opened.HoldsAnAttachmentIn(Boutique).ShouldBeFalse();
    opened.LocatingIn(Boutique)!.Reserved.ShouldHaveSingleItem().State.ShouldBe(ReservationState.SetAside);
  }

  /// <summary>
  /// <b>Une réserve qui n'apporte aucune désignation reste locale et opaque</b> : la rattacher ne fait
  /// rien remonter au service, qui n'apprendra jamais que « #1203 » est une ligne d'une table héritée.
  /// </summary>
  [Fact]
  public void LearnsNothingFromAReserveThatProposesNothing()
  {
    var opened = ACase();

    opened.LocateServed(Boutique, Findings(reserved: [("#1203", "Une ligne d'une table héritée.", null)]), Opened);

    opened.Arbitrate(Boutique, OpaqueReference.Of("#1203"), ReservationState.Attached);

    opened.Designations.Count.ShouldBe(1);

    // Elle rattache tout de même : le dossier sait que cette ligne est celle de la personne, là-bas.
    opened.HoldsAnAttachmentIn(Boutique).ShouldBeTrue();
  }

  /// <summary>
  /// <b>Le premier arbitrage est le bon.</b> Un second ne réécrit pas le premier : la preuve garde
  /// déjà la date et le nom de celui qui a tranché.
  /// </summary>
  [Fact]
  public void NeverRewritesAnArbitrationSomebodyAlreadySigned()
  {
    var opened = ACase();

    opened.LocateServed(Boutique, Findings(reserved: [("clients#4417", "Un doute.", null)]), Opened);
    opened.Arbitrate(Boutique, OpaqueReference.Of("clients#4417"), ReservationState.Attached);

    opened.Arbitrate(Boutique, OpaqueReference.Of("clients#4417"), ReservationState.SetAside).ShouldBeNull();
    opened.LocatingIn(Boutique)!.Reserved.ShouldHaveSingleItem().State.ShouldBe(ReservationState.Attached);
  }

  /// <summary>
  /// Une réserve inconnue, ou un système jamais appelé, ne lèvent pas : un écran affiché il y a une
  /// minute peut nommer une réserve qu'un autre geste vient d'arbitrer.
  /// </summary>
  [Fact]
  public void SaysNoRatherThanThrowingOnAReserveTheCaseDoesNotCarry()
  {
    var opened = ACase();

    opened.Arbitrate(Boutique, OpaqueReference.Of("inconnue"), ReservationState.Attached).ShouldBeNull();

    opened.LocateServed(Boutique, LocateFindings.Nothing, Opened);

    opened.Arbitrate(Boutique, OpaqueReference.Of("inconnue"), ReservationState.Attached).ShouldBeNull();
  }

  /// <summary>
  /// <b>Un arbitrage rendu n'est jamais retiré par un appel suivant.</b> L'application peut changer
  /// d'avis d'un appel à l'autre ; l'issue qu'un humain a rendue est un fait daté, et la faire
  /// disparaître de l'écran ferait ré-arbitrer ce qui l'a déjà été.
  /// </summary>
  [Fact]
  public void KeepsAnArbitratedReserveThroughTheNextCall()
  {
    var opened = ACase();

    opened.LocateServed(Boutique, Findings(reserved: [("clients#4417", "Un doute.", null)]), Opened);
    opened.Arbitrate(Boutique, OpaqueReference.Of("clients#4417"), ReservationState.SetAside);

    opened.LocateServed(Boutique, Findings(certain: ["clients#1203"]), Opened.AddHours(1));

    var locating = opened.LocatingIn(Boutique)!;

    locating.Certain.ShouldHaveSingleItem().Value.ShouldBe("clients#1203");
    locating.Reserved.ShouldHaveSingleItem().State.ShouldBe(ReservationState.SetAside);
  }

  /// <summary>
  /// <b>Un différé ne fait bouger aucun rattachement</b> : l'<c>Adapter</c> n'a pas répondu à la
  /// question, il a dit quand il y répondrait. L'échéance est <b>déclarée</b>, et le service la garde
  /// pour repasser après elle.
  /// </summary>
  [Fact]
  public void KeepsTheDeclaredDeadlineOfADeferredCallWithoutTouchingTheAttachments()
  {
    var opened = ACase();

    opened.LocateDeferred(Journal, Opened.AddHours(18), Opened);

    var locating = opened.LocatingIn(Journal).ShouldNotBeNull();

    locating.LastOutcome.ShouldBe(AdapterOutcome.Deferred);
    locating.DeclaredDeadline.ShouldBe(Opened.AddHours(18));
    locating.HoldsAnAttachment.ShouldBeFalse();
    locating.Certain.ShouldBeEmpty();
  }

  /// <summary>
  /// Un refus ne fait rien avancer non plus, et seul un refus entre par cette porte : servir ou
  /// différer appartient au dossier, non à la preuve du transport.
  /// </summary>
  [Fact]
  public void TakesARefusalAndOnlyARefusal()
  {
    var opened = ACase();

    opened.LocateRefused(Boutique, AdapterOutcome.SystemNotServed, Opened);

    opened.LocatingIn(Boutique)!.LastOutcome.ShouldBe(AdapterOutcome.SystemNotServed);
    opened.HoldsAnyAttachment.ShouldBeFalse();

    Should.Throw<ArgumentException>(() => opened.LocateRefused(Journal, AdapterOutcome.Served, Opened));
  }

  /// <summary>
  /// <b>Le compte des désignations du dernier appel est gardé</b> : c'est lui qui dit qu'une réponse a
  /// répondu à une question plus étroite que celle qu'on pose aujourd'hui.
  /// </summary>
  [Fact]
  public void RemembersHowManyDesignationsTheLastCallCarried()
  {
    var opened = ACase();

    opened.LocateServed(
      Boutique,
      Findings(reserved: [("clients#4417", "Un doute.", "Jean.Dupont@Example.fr")]),
      Opened);

    opened.LocatingIn(Boutique)!.DesignationsAtCall.ShouldBe(1);

    opened.Arbitrate(Boutique, OpaqueReference.Of("clients#4417"), ReservationState.Attached);

    // Le sac a grossi ; la réponse d'hier vaut toujours pour la question d'hier, et pas pour celle-ci.
    opened.Designations.Count.ShouldBe(2);
    opened.LocatingIn(Boutique)!.DesignationsAtCall.ShouldBe(1);
  }

  /// <summary>
  /// <b>Une question ouverte ne se pose qu'une fois</b>, et elle porte la date de ce jour-là. La
  /// reposer à chaque passage en ferait un bruit quotidien, et réinitialiserait la seule chose que
  /// l'écran en dise.
  /// </summary>
  [Fact]
  public void AsksAnOpenQuestionOnlyOnce()
  {
    var opened = ACase();

    opened.Ask(OpenQuestionSubject.Designation, Opened).ShouldBeTrue();
    opened.Ask(OpenQuestionSubject.Designation, Opened.AddDays(3)).ShouldBeFalse();

    var question = opened.Questions.ShouldHaveSingleItem();

    question.Subject.ShouldBe(OpenQuestionSubject.Designation);
    question.AskedOn.ShouldBe(Opened);
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
      ReceptionDate.Declared(Opened));
  }

  private static LocateFindings Findings(
    string[]? certain = null,
    (string Reference, string Reason, string? Designation)[]? reserved = null)
  {
    return LocateFindings.ReadFrom(
      new LocateOnTheWire(
        certain,
        [.. (reserved ?? []).Select(one => new ReservedOnTheWire(
          one.Reference,
          one.Reason,
          one.Designation is null ? null : [new DesignationOnTheWire("email", one.Designation)]))]),
      Boutique);
  }
}
