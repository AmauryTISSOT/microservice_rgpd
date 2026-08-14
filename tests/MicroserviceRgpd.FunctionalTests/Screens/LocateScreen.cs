using System.Net;
using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.EvidenceLog;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.FunctionalTests.Platform;
using MicroserviceRgpd.Infrastructure.Data;
using MicroserviceRgpd.Infrastructure.Data.Casework;
using Microsoft.EntityFrameworkCore;

// Shouldly porte un type du même nom, réservé à ses propres tables de cas. Le mot du glossaire
// l'emporte, et l'alias dit lequel des deux on lit.
using Case = MicroserviceRgpd.Core.Casework.Case;

namespace MicroserviceRgpd.FunctionalTests.Screens;

/// <summary>
/// <c>Locate</c> de bout en bout, par la <b>seule frontière HTTP</b> de l'écran du dossier : réserves,
/// arbitrage, sac enrichi, et l'appel suivant qui porte la désignation neuve.
/// </summary>
/// <remarks>
/// <para>
/// C'est le <b>cas dur du témoin</b>, rejoué en entier : deux « Jean Dupont » en base — que rien ne
/// départage — et un journal applicatif <b>sensible à la casse</b> là où la base ne l'est pas. Aucune
/// machine ne peut trancher cela ; c'est un humain qui le fait, nommé et daté, et c'est son
/// arbitrage — et lui seul — qui ouvre le second système.
/// </para>
/// <para>
/// L'<c>Adapter</c> est posé sur le <b>fil</b>, jamais sur le port du domaine : le corps qui part est
/// donc le vrai corps du contrat, et c'est en le relisant qu'on prouve que le sac s'est enrichi.
/// </para>
/// </remarks>
[Collection(WebCollection.Name)]
public class LocateScreen(CustomWebApplicationFactory<Program> factory)
{
  private static readonly DateTimeOffset DeclaredRecently = new(2026, 4, 1, 9, 0, 0, TimeSpan.Zero);

  private readonly OperatorSurface _surface = new(factory);

  /// <summary>
  /// <b>Le cas dur, de bout en bout.</b> Deux « Jean Dupont » en base font une <b>réserve motivée</b>
  /// que l'écran affiche <b>telle quelle</b> ; le journal, sensible à la casse, ne rend rien ; un
  /// humain tranche, nommé et daté ; le sac s'enrichit de l'adresse dans <b>sa</b> casse ; et l'appel
  /// suivant la porte, ce qui ouvre le journal.
  /// </summary>
  [Fact]
  public async Task CarriesAReserveThroughAnArbitrationIntoTheNextCall()
  {
    var opened = await AJeanDupontAsync();
    var address = OperatorSurface.AddressOf(opened);

    // 1. L'ouverture du dossier appelle les deux systèmes. La base hésite, et le dit en français.
    var screen = await _surface.ReadTextAsync(address);

    screen.ShouldContain("Deux comptes portent le nom « Jean Dupont »");
    screen.ShouldContain("clients#1203");
    screen.ShouldContain("clients#4417");

    // Le journal, lui, n'a rien rattaché : il ne connaît l'adresse que dans la casse où il l'a écrite.
    _surface.Client.ShouldNotBeNull();
    (await LocatingsOf(opened))
      .Single(locating => locating.DeclaredSystem == DeclaredSystemId.From(ABrocantoOnTheWire.Journal))
      .HoldsAnAttachment.ShouldBeFalse();

    // 2. Un humain tranche. C'est le SEUL chemin par lequel un rattachement se décide.
    var arbitrated = await _surface.ArbitrateAsync(
      opened,
      ABrocantoOnTheWire.Boutique,
      "clients#1203",
      nameof(ReservationState.Attached),
      "Claire Martin");

    arbitrated.StatusCode.ShouldBe(HttpStatusCode.Redirect);

    // 3. Le sac s'est enrichi de l'adresse que la réserve proposait, dans SA casse.
    var reread = await RereadAsync(opened);

    reread.Designations.Select(designation => designation.Value)
      .ShouldContain(ABrocantoOnTheWire.AsTheJournalWroteIt);

    // 4. L'appel suivant la porte — et c'est elle, et elle seule, qui ouvre le journal.
    await _surface.ReadTextAsync(address);

    factory.Adapter.BodiesSentTo(ABrocantoOnTheWire.Journal)
      .Last()
      .ShouldContain(ABrocantoOnTheWire.AsTheJournalWroteIt);

    (await LocatingsOf(opened))
      .Single(locating => locating.DeclaredSystem == DeclaredSystemId.From(ABrocantoOnTheWire.Journal))
      .Certain.ShouldHaveSingleItem().Value.ShouldBe("brocanto.log:2026-03");
  }

  /// <summary>
  /// <b>Aucun rattachement n'est décidé par le service.</b> Tant que personne n'a tranché, la réserve
  /// reste affichée et le dossier ne détient <b>rien</b> — ni par fusion à tort, ni par exclusion par
  /// prudence.
  /// </summary>
  [Fact]
  public async Task DecidesNoAttachmentOfItsOwn()
  {
    var opened = await AJeanDupontAsync();

    var screen = await _surface.ReadTextAsync(OperatorSurface.AddressOf(opened));

    screen.ShouldContain("Trancher cette réserve");
    screen.ShouldContain("C'est bien la personne");
    screen.ShouldContain("Ce n'est pas la personne");

    (await RereadAsync(opened)).HoldsAnyAttachment.ShouldBeFalse();
  }

  /// <summary>
  /// <b>Une réserve tranchée est signée et datée au <c>EvidenceLog</c>, et le compte du sac l'accompagne</b>
  /// — jamais ses valeurs, ni la prose de motif, ni la référence de l'application. Le contrôle lit
  /// « recherché sous 2 désignations, dont 1 ajoutée par arbitrage » sans qu'un seul nom lui survive.
  /// </summary>
  [Fact]
  public async Task WritesTheArbitrationInTheProofWithoutASingleDesignation()
  {
    var opened = await AJeanDupontAsync();

    await _surface.ReadTextAsync(OperatorSurface.AddressOf(opened));

    // Écarter ne verse rien au sac : la ligne dit le fait, et le compte n'a pas bougé.
    await _surface.ArbitrateAsync(
      opened,
      ABrocantoOnTheWire.Boutique,
      "clients#4417",
      nameof(ReservationState.SetAside),
      "Claire Martin");

    // Rattacher y verse la désignation que la réserve proposait, et le compte le dit.
    await _surface.ArbitrateAsync(
      opened,
      ABrocantoOnTheWire.Boutique,
      "clients#1203",
      nameof(ReservationState.Attached),
      "Claire Martin");

    var evidenceLog = await EvidenceLogOf(opened);

    evidenceLog.Single(line => line.Fact == nameof(EvidenceLogFact.ReservationSetAside)).DesignationCount.ShouldBe(1);

    var attached = evidenceLog.Single(line => line.Fact == nameof(EvidenceLogFact.ReservationAttached));

    attached.SignatoryName.ShouldBe("Claire Martin");
    attached.SignerVerification.ShouldBe(nameof(SignerVerification.Unauthenticated));
    attached.DeclaredSystem.ShouldBe(ABrocantoOnTheWire.Boutique);
    attached.DesignationCount.ShouldBe(2);

    // Aucune prose : le motif de la réserve est du texte qui MEURT, et il n'a aucune colonne ici.
    attached.Prose.ShouldBeNull();
  }

  /// <summary>
  /// <b>Six zéros ne se lisent pas « cette personne n'est pas chez nous ».</b> Quand tous les
  /// <c>Locate</c> rendent zéro, une <c>OpenQuestion</c> <b>datée</b> naît sur le dossier — et elle
  /// dit, en toutes lettres, que le délai de l'art. 12.3 continue de courir.
  /// </summary>
  [Fact]
  public async Task RaisesADatedQuestionWhenNothingAttachesAnywhere()
  {
    // Un sac sans nom : la base ne trouve rien, le journal non plus.
    var opened = await _surface.OpenAsync(
      ReceptionDate.Declared(DateTimeOffset.UtcNow.AddDays(-3)),
      ClaimOrigin.Named,
      motivation: null,
      [Designation.Of(DesignationKind.Reference, "inconnue-partout")],
      [DataSubjectRight.Access],
      OperatorSurface.ASystemServedByAnAdapter(ABrocantoOnTheWire.Boutique, "La boutique", DeclaredRecently),
      OperatorSurface.ASystemServedByAnAdapter(ABrocantoOnTheWire.Journal, "Le journal", DeclaredRecently));

    var screen = await _surface.ReadTextAsync(OperatorSurface.AddressOf(opened));

    screen.ShouldContain("Question ouverte — la désignation de la personne");
    screen.ShouldContain("continue de courir");

    // Aucune ancienneté n'est affichée : on montre la date, et l'Operator juge.
    screen.ShouldNotContain("sans réponse depuis");

    (await RereadAsync(opened)).Questions.ShouldHaveSingleItem()
      .Subject.ShouldBe(OpenQuestionSubject.Designation);

    (await EvidenceLogOf(opened)).ShouldContain(line => line.Fact == nameof(EvidenceLogFact.QuestionRaised));
  }

  /// <summary>
  /// <b>Un <c>Step</c> déclaré fait à zéro rattachement réclame un constat</b> ; celui d'un système où
  /// le <c>Locate</c> a rattaché quelque chose ne le réclame plus — le rattachement <b>est</b> le
  /// dénominateur que le constat devait fournir.
  /// </summary>
  [Fact]
  public async Task ClaimsAFindingOnlyWhereNothingWasAttached()
  {
    var opened = await AJeanDupontAsync();

    await _surface.ReadTextAsync(OperatorSurface.AddressOf(opened));

    // À zéro rattachement, le « fait » sans un mot est refusé.
    var refused = await _surface.DeclareAsync(
      opened,
      new Declaration(
        nameof(DataSubjectRight.Access),
        ABrocantoOnTheWire.Boutique,
        nameof(StepState.Done),
        Finding: string.Empty,
        SignedBy: "Claire Martin"));

    refused.StatusCode.ShouldBe(HttpStatusCode.OK);

    // Un humain rattache, et le constat cesse d'être réclamé : on sait désormais ce qu'on a trouvé.
    await _surface.ArbitrateAsync(
      opened,
      ABrocantoOnTheWire.Boutique,
      "clients#1203",
      nameof(ReservationState.Attached),
      "Claire Martin");

    var accepted = await _surface.DeclareAsync(
      opened,
      new Declaration(
        nameof(DataSubjectRight.Access),
        ABrocantoOnTheWire.Boutique,
        nameof(StepState.Done),
        Finding: string.Empty,
        SignedBy: "Claire Martin"));

    accepted.StatusCode.ShouldBe(HttpStatusCode.Redirect);
  }

  /// <summary>
  /// Un dossier dont le sac porte le nom du demandeur, et deux systèmes servis par l'<c>Adapter</c> du
  /// témoin.
  /// </summary>
  private async Task<CaseId> AJeanDupontAsync()
  {
    return await _surface.OpenAsync(
      ReceptionDate.Declared(DateTimeOffset.UtcNow.AddDays(-3)),
      ClaimOrigin.Named,
      motivation: null,
      [
        Designation.Of(DesignationKind.PersonName, "Jean Dupont"),
      ],
      [DataSubjectRight.Access],
      OperatorSurface.ASystemServedByAnAdapter(ABrocantoOnTheWire.Boutique, "La boutique", DeclaredRecently),
      OperatorSurface.ASystemServedByAnAdapter(ABrocantoOnTheWire.Journal, "Le journal", DeclaredRecently));
  }

  private async Task<IReadOnlyList<Locating>> LocatingsOf(CaseId opened)
  {
    return (await RereadAsync(opened)).Locatings;
  }

  private async Task<Case> RereadAsync(CaseId opened)
  {
    using var scope = factory.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    return (await dbContext.Cases.AsNoTracking().SingleAsync(one => one.Id == opened));
  }

  private async Task<IReadOnlyList<EvidenceLogRow>> EvidenceLogOf(CaseId opened)
  {
    using var scope = factory.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    return await dbContext.Set<EvidenceLogRow>()
      .AsNoTracking()
      .Where(row => row.CaseId == opened.Value)
      .ToListAsync();
  }
}
