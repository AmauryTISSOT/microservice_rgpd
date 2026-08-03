using Ardalis.Result;
using Ardalis.Specification;
using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.Ledger;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.UseCases.Casework.OpenCase;

// Shouldly porte un type du même nom, réservé à ses propres tables de cas.
using Case = MicroserviceRgpd.Core.Casework.Case;

namespace MicroserviceRgpd.UnitTests.UseCases.Casework;

/// <summary>
/// Ce que l'ouverture d'un dossier écrit, et dans quel ordre.
/// <para>
/// L'horloge est dictée : la date de réception est, sur ce canal, la seule chose que le service
/// produise lui-même, et un test qui la lirait sur la machine qui l'exécute ne prouverait rien de
/// l'endroit d'où elle vient.
/// </para>
/// </summary>
public class OpenCaseHandlerTests
{
  private static readonly DateTimeOffset Now = new(2026, 8, 3, 14, 30, 0, TimeSpan.Zero);

  private readonly AClockStuckAt _clock = new(Now);
  private readonly IReadRepository<DeclaredSystem> _manifest = Substitute.For<IReadRepository<DeclaredSystem>>();
  private readonly IRepository<Case> _cases = Substitute.For<IRepository<Case>>();
  private readonly ILedger _ledger = Substitute.For<ILedger>();

  /// <summary>
  /// <b>Le travail dû naît du catalogue</b> : un <c>Step</c> par (<c>Claim</c>,
  /// <c>DeclaredSystem</c>), et rien n'est deviné au-delà de ce qu'un humain a déclaré.
  /// </summary>
  [Fact]
  public async Task GivesEachClaimOneStepPerSystemTheManifestDeclaresRightNow()
  {
    TheManifestDeclares(ASystem("boutique"), ASystem("journal"));

    var opened = await OpenAsync([DataSubjectRight.Access, DataSubjectRight.Erasure]);

    opened.Value.Claims.Count.ShouldBe(2);
    opened.Value.Claims.ShouldAllBe(claim => claim.Steps.Count == 2);
  }

  /// <summary>
  /// Un catalogue vide ouvre un dossier <b>sans aucun travail dû</b>, plutôt que de refuser la
  /// demande : c'est l'état d'un service qu'on vient d'installer, et le mois court déjà.
  /// </summary>
  [Fact]
  public async Task OpensACaseEvenWhenNoSystemHasEverBeenDeclared()
  {
    TheManifestDeclares();

    var opened = await OpenAsync([DataSubjectRight.Access]);

    opened.IsSuccess.ShouldBeTrue();
    opened.Value.Claims[0].Steps.ShouldBeEmpty();
  }

  /// <summary>
  /// La date de réception vient de l'horloge du service : sur ce canal, la demande arrive à
  /// l'instant où elle est postée, et aucun champ ne permet d'en déclarer une autre.
  /// </summary>
  [Fact]
  public async Task DatesTheReceptionOnTheServiceClockOnThisChannel()
  {
    TheManifestDeclares();

    var opened = await OpenAsync([DataSubjectRight.Access]);

    opened.Value.ReceivedOn.ShouldBe(Now);
  }

  /// <summary>
  /// <b>Le dossier est écrit, puis la preuve.</b> Une ligne de preuve pour un dossier qui n'existe
  /// pas serait un faux ; un dossier dont la première ligne manque reste une ligne présente dans la
  /// file, que l'<c>Operator</c> voit.
  /// </summary>
  [Fact]
  public async Task WritesTheCaseThenItsFirstLineOfProof()
  {
    TheManifestDeclares();

    var opened = await OpenAsync([DataSubjectRight.Access]);

    await _cases.Received(1).AddAsync(
      Arg.Is<Case>(written => written.Id == opened.Value.Id),
      Arg.Any<CancellationToken>());

    await _ledger.Received(1).AppendAsync(
      Arg.Is<LedgerEntry>(line => line.Case == opened.Value.Id && line.Fact == LedgerFact.CaseOpened),
      Arg.Any<CancellationToken>());
  }

  /// <summary>
  /// <b>La première ligne compte les désignations, elle n'en écrit aucune.</b> « Recherché sous 2
  /// désignations » est une mesure de l'ampleur d'une recherche ; les deux valeurs seraient le sac
  /// lui-même, qui meurt à la clôture.
  /// </summary>
  [Fact]
  public async Task CountsTheDesignationsOnTheLedgerAndNeverNamesThem()
  {
    TheManifestDeclares();

    await OpenAsync(
      [DataSubjectRight.Access],
      Designation.Of(DesignationKind.Email, "jean.dupont@example.fr"),
      Designation.Of(DesignationKind.PersonName, "Jean Dupont"));

    await _ledger.Received(1).AppendAsync(
      Arg.Is<LedgerEntry>(line =>
        line.DesignationCount == 2
        && line.IdentityDeclaration == IdentityDeclaration.ApplicationSession
        && line.Signatory == Signatory.Application),
      Arg.Any<CancellationToken>());
  }

  /// <summary>
  /// <c>OutOfScope</c> ressort <b>nommé</b>, sous le nom du champ fautif, et sans qu'aucune
  /// exception n'ait traversé la frontière — <b>et rien n'est écrit</b>, ni dossier, ni preuve.
  /// </summary>
  [Fact]
  public async Task NamesTheUnclaimableVerdictAsAValidationErrorAndWritesNothing()
  {
    TheManifestDeclares();

    var opened = await OpenAsync([DataSubjectRight.Access, DataSubjectRight.OutOfScope]);

    opened.Status.ShouldBe(ResultStatus.Invalid);
    opened.ValidationErrors.ShouldContain(error => error.Identifier == "Rights");

    await _cases.DidNotReceive().AddAsync(Arg.Any<Case>(), Arg.Any<CancellationToken>());
    await _ledger.DidNotReceive().AppendAsync(Arg.Any<LedgerEntry>(), Arg.Any<CancellationToken>());
  }

  /// <summary>
  /// Une demande dont on ne reconnaît encore aucun droit <b>entre quand même</b> : le compteur de
  /// l'art. 12.3 court, et un vestibule où elle attendrait le laisserait courir hors du service.
  /// </summary>
  [Fact]
  public async Task OpensACaseThatClaimsNoRightYet()
  {
    TheManifestDeclares();

    var opened = await OpenAsync([]);

    opened.IsSuccess.ShouldBeTrue();
    opened.Value.Claims.ShouldBeEmpty();

    await _ledger.Received(1).AppendAsync(Arg.Any<LedgerEntry>(), Arg.Any<CancellationToken>());
  }

  private void TheManifestDeclares(params DeclaredSystem[] systems)
  {
    _manifest.ListAsync(Arg.Any<CancellationToken>()).Returns([.. systems]);
  }

  private async Task<Result<Case>> OpenAsync(
    DataSubjectRight[] rights,
    params Designation[] designations)
  {
    var handler = new OpenCaseHandler(_manifest, _cases, _ledger, _clock);

    return await handler.Handle(
      new OpenCaseCommand(
        IdentityDeclaration.ApplicationSession,
        designations,
        rights,
        Signatory.Application),
      CancellationToken.None);
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
