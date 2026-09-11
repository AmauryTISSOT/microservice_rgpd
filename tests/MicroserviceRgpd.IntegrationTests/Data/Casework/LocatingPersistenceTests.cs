using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.Adapters;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.Infrastructure.Data;

// Shouldly porte un type du même nom, réservé à ses propres tables de cas.
using Case = MicroserviceRgpd.Core.Casework.Case;

namespace MicroserviceRgpd.IntegrationTests.Data.Casework;

/// <summary>
/// Ce qu'un <c>Locate</c> laisse en base, et ce qu'un dossier relu en rapporte.
/// </summary>
/// <remarks>
/// La base est réelle parce que rien d'autre ne prouve ce qui est en jeu : trois niveaux de types
/// possédés — la localisation, ses réserves, les désignations qu'elles proposent —, et le fait qu'un
/// dossier relu les porte <b>sans qu'aucun <c>Include</c> ne les ait demandés</b>. Une réserve qui ne
/// remonterait pas serait un doute que personne n'arbitrerait jamais.
/// </remarks>
[Collection(PostgreSqlCollection.Name)]
public class LocatingPersistenceTests(PostgreSqlFixture postgres)
{
  private static readonly DateTimeOffset Received = new(2026, 4, 1, 8, 0, 0, TimeSpan.Zero);
  private static readonly DateTimeOffset Asked = new(2026, 4, 12, 10, 0, 0, TimeSpan.Zero);
  private static readonly DeclaredSystemId Boutique = DeclaredSystemId.From("brocanto-boutique");
  private static readonly DeclaredSystemId Journal = DeclaredSystemId.From("brocanto-journal");

  /// <summary>
  /// La localisation fait l'aller-retour <b>entière</b> : son noyau certain, ses réserves avec leur
  /// prose de motif, et les désignations qu'elles proposent.
  /// </summary>
  [Fact]
  public async Task RoundTripsTheWholeLocatingThroughTheRootAlone()
  {
    var opened = ACase();

    opened.LocateServed(
      Boutique,
      LocateFindings.ReadFrom(
        new LocateOnTheWire(
          ["clients#1203"],
          [
            new ReservedOnTheWire(
              "clients#4417",
              "Deux comptes portent ce nom : celui-ci a été créé en 2019 et n'a jamais commandé.",
              [new DesignationOnTheWire("email", "Jean.Dupont@Example.fr")]),
          ]),
        Boutique),
      Asked);

    await SaveAsync(opened);

    var locating = (await RereadAsync(opened.Id)).LocatingIn(Boutique).ShouldNotBeNull();

    locating.LastOutcome.ShouldBe(AdapterOutcome.Served);
    locating.AskedAt.ShouldBe(Asked);
    locating.DesignationsAtCall.ShouldBe(1);
    locating.Certain.ShouldHaveSingleItem().Value.ShouldBe("clients#1203");

    var reserved = locating.Reserved.ShouldHaveSingleItem();

    reserved.Reference.Value.ShouldBe("clients#4417");
    reserved.Reason.ShouldBe(
      "Deux comptes portent ce nom : celui-ci a été créé en 2019 et n'a jamais commandé.");
    reserved.State.ShouldBe(ReservationState.Awaiting);
    reserved.Designations.ShouldHaveSingleItem().Value.ShouldBe("Jean.Dupont@Example.fr");
  }

  /// <summary>
  /// L'arbitrage d'un humain survit à l'aller-retour, <b>et le sac enrichi avec lui</b> : c'est ce
  /// sac-là que l'appel suivant portera.
  /// </summary>
  [Fact]
  public async Task RoundTripsTheArbitrationAndTheBagItEnriched()
  {
    var opened = ACase();

    opened.LocateServed(
      Boutique,
      LocateFindings.ReadFrom(
        new LocateOnTheWire(
          null,
          [
            new ReservedOnTheWire(
              "clients#4417",
              "Deux comptes portent ce nom.",
              [new DesignationOnTheWire("email", "Jean.Dupont@Example.fr")]),
          ]),
        Boutique),
      Asked);

    opened.Arbitrate(Boutique, OpaqueReference.Of("clients#4417"), ReservationState.Attached);

    await SaveAsync(opened);

    var reread = await RereadAsync(opened.Id);

    reread.LocatingIn(Boutique)!.Reserved.ShouldHaveSingleItem().State.ShouldBe(ReservationState.Attached);
    reread.HoldsAnAttachmentIn(Boutique).ShouldBeTrue();
    reread.Designations.Select(designation => designation.Value)
      .ShouldBe(["jean.dupont@example.fr", "Jean.Dupont@Example.fr"]);
  }

  /// <summary>
  /// Un différé garde son <b>échéance déclarée</b>, celle après laquelle le service repassera à la
  /// prochaine ouverture du dossier.
  /// </summary>
  [Fact]
  public async Task RoundTripsTheDeclaredDeadlineOfADeferredCall()
  {
    var opened = ACase();

    opened.LocateDeferred(Journal, Asked.AddHours(17), Asked);

    await SaveAsync(opened);

    var locating = (await RereadAsync(opened.Id)).LocatingIn(Journal).ShouldNotBeNull();

    locating.LastOutcome.ShouldBe(AdapterOutcome.Deferred);
    locating.DeclaredDeadline.ShouldBe(Asked.AddHours(17));
    locating.Certain.ShouldBeEmpty();
  }

  /// <summary>
  /// Une question ouverte garde <b>la date à laquelle elle a été posée</b>, et rien d'autre : aucune
  /// ancienneté n'est écrite, aucun seuil n'existe nulle part.
  /// </summary>
  [Fact]
  public async Task RoundTripsTheDayAnOpenQuestionWasAsked()
  {
    var opened = ACase();

    opened.Ask(OpenQuestionSubject.Designation, Asked);

    await SaveAsync(opened);

    var question = (await RereadAsync(opened.Id)).Questions.ShouldHaveSingleItem();

    question.Subject.ShouldBe(OpenQuestionSubject.Designation);
    question.AskedOn.ShouldBe(Asked);
  }

  /// <summary>
  /// <b>Un zéro s'écrit, et il se relit comme un zéro</b> — jamais comme une absence de localisation.
  /// « Appelé, rien trouvé » a une valeur de preuve que « pas appelé » n'a pas.
  /// </summary>
  [Fact]
  public async Task TellsAZeroApartFromASystemNobodyCalled()
  {
    var opened = ACase();

    opened.LocateServed(Boutique, LocateFindings.Nothing, Asked);

    await SaveAsync(opened);

    var reread = await RereadAsync(opened.Id);

    reread.LocatingIn(Boutique).ShouldNotBeNull().LastOutcome.ShouldBe(AdapterOutcome.Served);
    reread.LocatingIn(Boutique)!.HoldsAnAttachment.ShouldBeFalse();
    reread.LocatingIn(Journal).ShouldBeNull();
  }

  private static Case ACase()
  {
    return Case.Open(
      CaseId.Next(),
      IdentityDeclaration.ApplicationSession,
      motivation: null,
      [Designation.Of(DesignationKind.Email, "jean.dupont@example.fr")],
      [DataSubjectRight.Access],
      ClaimOrigin.Named,
      [],
      ReceptionDate.Declared(Received));
  }

  private async Task SaveAsync(Case opened)
  {
    await using var dbContext = postgres.NewDbContext();

    dbContext.Cases.Add(opened);

    await dbContext.SaveChangesAsync();
  }

  private async Task<Case> RereadAsync(CaseId id)
  {
    // Un contexte neuf : relire depuis celui qui a écrit ne prouverait que le suivi des
    // modifications, jamais l'aller-retour à travers les colonnes.
    await using var dbContext = postgres.NewDbContext();

    return (await dbContext.Cases.SingleOrDefaultAsync(one => one.Id == id)).ShouldNotBeNull();
  }
}
