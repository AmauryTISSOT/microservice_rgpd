using Ardalis.Result;
using Ardalis.Specification;
using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.EvidenceLog;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.UseCases.Casework.DeclareExtension;
using MicroserviceRgpd.UseCases.Casework.DestroyEvidenceLog;

// Shouldly porte un type du même nom, réservé à ses propres tables de cas.
using Case = MicroserviceRgpd.Core.Casework.Case;

namespace MicroserviceRgpd.UnitTests.UseCases.Casework;

/// <summary>
/// La prolongation de l'art. 12.3 et la destruction d'un <c>EvidenceLog</c> échu, <b>horloge dictée</b>.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>C'est ici, et nulle part ailleurs, que la frontière du mois se prouve.</b> Les tests
/// d'écran datent leurs dossiers par rapport à la machine qui les exécute ; ceux-ci dictent l'instant
/// du geste, et peuvent donc poser la déclaration <b>à une seconde</b> de part et d'autre de
/// l'échéance — l'endroit exact où un service se trompe.
/// </para>
/// <para>
/// <b>Le gestionnaire lit l'horloge une seule fois.</b> Deux lectures auraient pu tomber de part et
/// d'autre du mois, et le dénominateur affiché aurait alors dépendu de la lenteur d'une requête.
/// </para>
/// </remarks>
public class DeclareExtensionHandlerTests
{
  private static readonly DateTimeOffset Received = new(2026, 3, 2, 9, 0, 0, TimeSpan.Zero);

  private readonly IRepository<Case> _cases = Substitute.For<IRepository<Case>>();
  private readonly IEvidenceLog _evidenceLog = Substitute.For<IEvidenceLog>();
  private readonly IExpiredEvidenceLogs _expired = Substitute.For<IExpiredEvidenceLogs>();

  /// <summary>
  /// <b>Déclarée dans le mois, elle porte le dénominateur à trois mois</b> — et l'échéance se
  /// recalcule sur la réception, jamais sur le jour du clic : le délai dû à la personne ne dépend pas
  /// de l'agenda de l'<c>Operator</c>.
  /// </summary>
  [Fact]
  public async Task CarriesTheDeadlineToThreeMonthsWhenTheClockSaysTheMonthIsStillRunning()
  {
    var opened = ACase();

    var declared = await DeclaringAt(opened, Received.AddMonths(1).AddSeconds(-1));

    declared.IsSuccess.ShouldBeTrue();

    var deadline = StatutoryDeadline.Of(opened.Reception, opened.ExtensionDeclaration);

    deadline.Extended.ShouldBeTrue();
    deadline.On.ShouldBe(Received.AddMonths(3));
  }

  /// <summary>
  /// <b>Déclarée le jour de l'échéance, elle porte encore.</b> Le dernier jour du mois est dû, et le
  /// service ne le rogne pas d'une seconde : c'est la même frontière que le dépassement.
  /// </summary>
  [Fact]
  public async Task StillCarriesWhenTheClockStandsExactlyOnTheDueInstant()
  {
    var opened = ACase();

    await DeclaringAt(opened, Received.AddMonths(1));

    StatutoryDeadline.Of(opened.Reception, opened.ExtensionDeclaration).Extended.ShouldBeTrue();
  }

  /// <summary>
  /// ⚠️ <b>Une seconde plus tard, elle s'inscrit et ne déplace rien.</b> Le fait est gardé au dossier
  /// et à la preuve ; le dépassement déjà acquis reste dépassé. Refuser la déclaration aurait perdu
  /// le fait, la porter aurait blanchi le dépassement.
  /// </summary>
  [Fact]
  public async Task RecordsTheDeclarationWithoutMovingAnythingOneSecondAfterTheMonth()
  {
    var opened = ACase();

    var declared = await DeclaringAt(opened, Received.AddMonths(1).AddSeconds(1));

    declared.IsSuccess.ShouldBeTrue();

    opened.ExtensionDeclaration.ShouldNotBeNull();

    var deadline = StatutoryDeadline.Of(opened.Reception, opened.ExtensionDeclaration);

    deadline.Extended.ShouldBeFalse();
    deadline.On.ShouldBe(Received.AddMonths(1));

    // Et la preuve porte tout de même la ligne : c'est au contrôle de refaire le calcul.
    await _evidenceLog.Received(1).AppendAsync(
      Arg.Is<EvidenceLogEntry>(line => line.Fact == EvidenceLogFact.ExtensionDeclared),
      Arg.Any<CancellationToken>());
  }

  /// <summary>
  /// <b>La ligne de preuve est datée du geste, et porte la date d'information à part.</b> Les deux
  /// ne se confondent pas : l'écart entre elles est ce que le contrôle vient lire. ⚠️ <b>Aucune
  /// colonne ne dit si l'échéance a bougé</b> — c'est un calcul, refait à chaque affichage.
  /// </summary>
  [Fact]
  public async Task DatesTheProofOfTheGestureAndKeepsTheDayThePersonWasInformedApart()
  {
    var declaredAt = Received.AddDays(10);
    var informedOn = Received.AddDays(8);

    await DeclaringAt(ACase(), declaredAt, informedOn);

    await _evidenceLog.Received(1).AppendAsync(
      Arg.Is<EvidenceLogEntry>(line =>
        line.OccurredAt == declaredAt
        && line.InformedOn == informedOn
        && line.DeclaredDeadline == null
        && line.Signatory.Kind == SignatoryKind.Operator),
      Arg.Any<CancellationToken>());
  }

  /// <summary>
  /// <b>Sans nom, rien ne se pose sur le dossier.</b> La ligne de preuve est forgée avant que
  /// l'agrégat ne bouge : une prolongation sans signataire laisserait un dossier prolongé dont
  /// personne n'a répondu.
  /// </summary>
  [Fact]
  public async Task WritesNothingAtAllWhenNobodySignedTheDeclaration()
  {
    var opened = ACase();

    var declared = await DeclaringAt(opened, Received.AddDays(3), signedBy: "  ");

    declared.Status.ShouldBe(ResultStatus.Invalid);
    declared.ValidationErrors.ShouldContain(refusal => refusal.Identifier == "SignedBy");

    opened.ExtensionDeclaration.ShouldBeNull();

    await _cases.DidNotReceive().UpdateAsync(Arg.Any<Case>(), Arg.Any<CancellationToken>());
    await _evidenceLog.DidNotReceive().AppendAsync(Arg.Any<EvidenceLogEntry>(), Arg.Any<CancellationToken>());
  }

  /// <summary>
  /// <b>Chaque refus se dépose sous le nom de SON champ.</b> Le type du domaine lève sous le nom de
  /// son paramètre, l'écran nomme ses cases : sans cette correspondance, un « motif absent »
  /// s'afficherait à côté de la case de la date.
  /// </summary>
  [Fact]
  public async Task NamesEachRefusalUnderTheFieldTheScreenShows()
  {
    var withoutAMotive = await DeclaringAt(ACase(), Received.AddDays(3), motive: "   ");

    withoutAMotive.ValidationErrors.ShouldContain(refusal => refusal.Identifier == "Motive");

    // Une date d'information postérieure à la déclaration elle-même : l'Operator dit avoir informé
    // la personne après avoir déclaré l'avoir fait.
    var declaredAt = Received.AddDays(3);

    var informedLater = await DeclaringAt(ACase(), declaredAt, declaredAt.AddDays(1));

    informedLater.ValidationErrors.ShouldContain(refusal => refusal.Identifier == "InformedOn");
  }

  /// <summary>
  /// <b>La destruction d'un <c>EvidenceLog</c> échu ne consigne rien</b>, et c'est écrit ici : le seul
  /// endroit où le nom se serait écrit est la preuve qui disparaît.
  /// </summary>
  [Fact]
  public async Task DestroysAnExpiredEvidenceLogWithoutWritingASingleLineAboutIt()
  {
    var evidenceLogOf = CaseId.Next();
    var gesture = new DateTimeOffset(2031, 6, 1, 8, 0, 0, TimeSpan.Zero);

    _expired.DestroyAsync(evidenceLogOf, gesture, Arg.Any<CancellationToken>()).Returns(true);

    var destroyed = await new DestroyEvidenceLogHandler(_expired, new AClockStuckAt(gesture))
      .Handle(new DestroyEvidenceLogCommand(evidenceLogOf), CancellationToken.None);

    destroyed.IsSuccess.ShouldBeTrue();

    // ⚠️ L'instant du geste est celui de l'horloge, pas celui de l'écran d'où part le clic — et rien
    // n'est ajouté à la place de ce qui est parti.
    await _expired.Received(1).DestroyAsync(evidenceLogOf, gesture, Arg.Any<CancellationToken>());
    await _evidenceLog.DidNotReceive().AppendAsync(Arg.Any<EvidenceLogEntry>(), Arg.Any<CancellationToken>());
  }

  /// <summary>
  /// <b>Une preuve encore due ne se détruit pas</b>, et le refus se lit comme une adresse qui ne
  /// désigne rien : l'écran d'où venait le clic date d'avant.
  /// </summary>
  [Fact]
  public async Task RefusesToDestroyWhatTheClockSaysIsStillDue()
  {
    var gesture = new DateTimeOffset(2031, 6, 1, 8, 0, 0, TimeSpan.Zero);

    // Rien n'est stubé : l'adaptateur qui ne détruit pas rend false, et c'est cela qu'on lit.
    var destroyed = await new DestroyEvidenceLogHandler(_expired, new AClockStuckAt(gesture))
      .Handle(new DestroyEvidenceLogCommand(CaseId.Next()), CancellationToken.None);

    destroyed.Status.ShouldBe(ResultStatus.NotFound);
  }

  private async Task<Result> DeclaringAt(
    Case opened,
    DateTimeOffset declaredAt,
    DateTimeOffset? informedOn = null,
    string? motive = "Le prestataire de paie ne rend la main qu'au trimestre.",
    string? signedBy = "Camille Roy")
  {
    _cases.FirstOrDefaultAsync(Arg.Any<ISingleResultSpecification<Case>>(), Arg.Any<CancellationToken>())
      .Returns(opened);

    var handler = new DeclareExtensionHandler(_cases, _evidenceLog, new AClockStuckAt(declaredAt));

    return await handler.Handle(
      new DeclareExtensionCommand(
        opened.Id,
        motive,
        informedOn ?? declaredAt.AddDays(-1),
        signedBy),
      CancellationToken.None);
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
      Manifest.Empty,
      ReceptionDate.Declared(Received));
  }
}
