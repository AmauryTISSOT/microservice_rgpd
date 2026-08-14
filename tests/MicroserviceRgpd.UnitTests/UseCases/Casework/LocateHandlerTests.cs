using Ardalis.Specification;
using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.Adapters;
using MicroserviceRgpd.Core.Casework.EvidenceLog;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.UseCases.Casework.CallAdapter;
using MicroserviceRgpd.UseCases.Casework.Locate;

// Shouldly porte un type du même nom, réservé à ses propres tables de cas.
using Case = MicroserviceRgpd.Core.Casework.Case;

namespace MicroserviceRgpd.UnitTests.UseCases.Casework;

/// <summary>
/// Ce que l'appel de <c>Locate</c> fait, quand il repart, et ce qu'il consigne.
/// </summary>
/// <remarks>
/// Trois faits cardinaux : <b>il n'appelle que les systèmes qui déclarent la capacité</b> ; <b>il ne
/// consigne que ce qui change</b> — rouvrir un dossier relance les appels, et trente-cinq passages
/// rendant le même verdict n'ont aucun signataire ; et <b>tous les zéros font naître une question
/// datée</b>, qui n'arrête pas le délai.
/// </remarks>
public class LocateHandlerTests
{
  private static readonly DateTimeOffset Now = new(2026, 4, 12, 10, 0, 0, TimeSpan.Zero);
  private static readonly DeclaredSystemId Boutique = DeclaredSystemId.From("brocanto-boutique");
  private static readonly DeclaredSystemId Journal = DeclaredSystemId.From("brocanto-journal");

  private readonly IRepository<Case> _cases = Substitute.For<IRepository<Case>>();
  private readonly IReadRepository<DeclaredSystem> _manifest = Substitute.For<IReadRepository<DeclaredSystem>>();
  private readonly IAdapterCalls _calls = Substitute.For<IAdapterCalls>();
  private readonly IAdapterDisagreements _disagreements = Substitute.For<IAdapterDisagreements>();
  private readonly IEvidenceLog _evidenceLog = Substitute.For<IEvidenceLog>();
  /// <summary>
  /// L'instant que le service lira. Il se <b>dicte</b>, et il avance quand le test veut faire passer
  /// une échéance déclarée : c'est la seule chose qui fasse repartir un <c>202</c>, et la lire sur la
  /// machine qui exécute le test ne prouverait rien de l'endroit d'où elle vient.
  /// </summary>
  private DateTimeOffset _now = Now;

  /// <summary>
  /// <b>Seuls les systèmes qui déclarent <c>Locate</c> et une adresse sont appelés.</b> Un système
  /// sans <c>Adapter</c> reste pleinement légitime : il est recensé, et traité à la main.
  /// </summary>
  [Fact]
  public async Task CallsOnlyTheSystemsThatDeclareBothAnAddressAndTheCapability()
  {
    var opened = ACase();
    TheManifestDeclares(
      AServedSystem(Boutique),
      ASystem(Journal, adapterAddress: null, Capability.Locate),
      ASystem(DeclaredSystemId.From("export-agence"), AnAddress(), capabilities: []));

    TheAdapterServes(LocateFindings.Nothing);

    await LocatingIn(opened);

    await _calls.Received(1).AskAsync<LocateOnTheWire>(
      Arg.Is<AdapterCall>(call => call.DeclaredSystem == Boutique && call.Capability == Capability.Locate),
      Arg.Any<CancellationToken>());

    await _calls.Received(1).AskAsync<LocateOnTheWire>(Arg.Any<AdapterCall>(), Arg.Any<CancellationToken>());
  }

  /// <summary>
  /// L'appel porte le <b>sac de désignations du dossier</b>, et rien d'autre : c'est la seule
  /// identité qui circule.
  /// </summary>
  [Fact]
  public async Task CarriesTheBagOfDesignationsAndNothingElse()
  {
    var opened = ACase();
    TheManifestDeclares(AServedSystem(Boutique));
    TheAdapterServes(LocateFindings.Nothing);

    await LocatingIn(opened);

    await _calls.Received(1).AskAsync<LocateOnTheWire>(
      Arg.Is<AdapterCall>(call =>
        call.Designations.Count == 1
        && call.Designations[0].Value == "jean.dupont@example.fr"),
      Arg.Any<CancellationToken>());
  }

  /// <summary>
  /// Ce qui a été servi entre dans le dossier, et la preuve garde le <b>compte</b> de ce sous quoi on
  /// a cherché — jamais ce qu'on a trouvé : l'<c>EvidenceLog</c> mesure l'ampleur d'une recherche, il ne
  /// dénombre pas les données de la personne.
  /// </summary>
  [Fact]
  public async Task KeepsWhatWasServedAndCountsOnlyTheSearch()
  {
    var opened = ACase();
    TheManifestDeclares(AServedSystem(Boutique));
    TheAdapterServes(AFinding("clients#1203", "clients#4417", "Deux comptes portent ce nom."));

    await LocatingIn(opened);

    opened.LocatingIn(Boutique)!.Certain.ShouldHaveSingleItem().Value.ShouldBe("clients#1203");

    var written = Written().ShouldHaveSingleItem();

    written.Fact.ShouldBe(EvidenceLogFact.LocateServed);
    written.DeclaredSystem.ShouldBe(Boutique);
    written.DesignationCount.ShouldBe(1);
    written.Signatory.ShouldBe(Signatory.Application);
    written.Prose.ShouldBeNull();
  }

  /// <summary>
  /// <b>L'<c>EvidenceLog</c> ne consigne un appel que s'il rend un verdict différent du précédent.</b>
  /// Rouvrir un dossier relance les appels, et trente-cinq passages rendant le même « servi » n'ont
  /// aucun signataire — c'est un affichage qui les a déclenchés, non un humain.
  /// </summary>
  [Fact]
  public async Task WritesNothingWhenTheAdapterRepeatsTheSameVerdict()
  {
    var opened = ACase();
    TheManifestDeclares(AServedSystem(Boutique));
    TheAdapterServes(LocateFindings.Nothing);

    await LocatingIn(opened);
    _evidenceLog.ClearReceivedCalls();

    // Un second passage : le Locate est déjà servi sous ce sac, et rien ne repart.
    await LocatingIn(opened);

    await _calls.Received(1).AskAsync<LocateOnTheWire>(Arg.Any<AdapterCall>(), Arg.Any<CancellationToken>());
    Written().ShouldBeEmpty();
  }

  /// <summary>
  /// <b>La relance d'un <c>202</c> a lieu à l'ouverture du dossier, et seulement après l'échéance
  /// déclarée.</b> Avant elle, repasser ferait redire à l'<c>Adapter</c> ce qu'il vient de dire ; il
  /// n'existe ni compteur de tentatives, ni temporisation, ni abandon automatique.
  /// </summary>
  [Fact]
  public async Task ComesBackOnlyAfterTheDeadlineTheAdapterDeclared()
  {
    var opened = ACase();
    TheManifestDeclares(AServedSystem(Journal));
    _calls.AskAsync<LocateOnTheWire>(Arg.Any<AdapterCall>(), Arg.Any<CancellationToken>())
      .Returns(AdapterAnswer<LocateOnTheWire>.Deferring(Now.AddHours(6)));

    await LocatingIn(opened);

    var written = Written().ShouldHaveSingleItem();

    written.Fact.ShouldBe(EvidenceLogFact.LocateDeferred);
    written.DeclaredDeadline.ShouldBe(Now.AddHours(6));

    // L'échéance n'est pas passée : on ne repasse pas.
    await LocatingIn(opened);
    await _calls.Received(1).AskAsync<LocateOnTheWire>(Arg.Any<AdapterCall>(), Arg.Any<CancellationToken>());

    // Elle l'est : on repasse, à l'ouverture du dossier et de nulle part ailleurs.
    _now = Now.AddHours(7);
    await LocatingIn(opened);
    await _calls.Received(2).AskAsync<LocateOnTheWire>(Arg.Any<AdapterCall>(), Arg.Any<CancellationToken>());
  }

  /// <summary>
  /// <b>Un sac enrichi fait repartir l'appel, et l'appel suivant porte la désignation neuve.</b>
  /// C'est ce qu'un arbitrage vient d'ouvrir : la question n'est plus la même, et la réponse d'hier
  /// répondait à une question plus étroite.
  /// </summary>
  [Fact]
  public async Task CallsAgainUnderTheDesignationAnArbitrationJustAdded()
  {
    var opened = ACase();
    TheManifestDeclares(AServedSystem(Boutique));
    TheAdapterServes(AFinding(reserved: "clients#4417", reason: "Deux comptes portent ce nom.",
      proposes: "Jean.Dupont@Example.fr"));

    await LocatingIn(opened);

    opened.Arbitrate(Boutique, OpaqueReference.Of("clients#4417"), ReservationState.Attached);
    TheAdapterServes(AFinding("clients#4417"));

    await LocatingIn(opened);

    await _calls.Received(1).AskAsync<LocateOnTheWire>(
      Arg.Is<AdapterCall>(call =>
        call.Designations.Count == 2
        && call.Designations[1].Value == "Jean.Dupont@Example.fr"),
      Arg.Any<CancellationToken>());
  }

  /// <summary>
  /// <b>Tous les <c>Locate</c> à zéro font naître une <c>OpenQuestion</c> datée sur le dossier.</b>
  /// Six zéros ne doivent pas se lire « cette personne n'est pas chez nous » : la désignation
  /// insuffisante devient une question posée. ⚠️ Elle <b>n'arrête pas</b> le délai de l'art. 12.3.
  /// </summary>
  [Fact]
  public async Task RaisesADatedQuestionWhenEveryLocateRendersZero()
  {
    var opened = ACase();
    TheManifestDeclares(AServedSystem(Boutique), AServedSystem(Journal));
    TheAdapterServes(LocateFindings.Nothing);

    await LocatingIn(opened);

    var question = opened.Questions.ShouldHaveSingleItem();

    question.Subject.ShouldBe(OpenQuestionSubject.Designation);
    question.AskedOn.ShouldBe(Now);

    var raised = Written().Single(line => line.Fact == EvidenceLogFact.QuestionRaised);

    raised.DesignationCount.ShouldBe(1);
    raised.DeclaredSystem.ShouldBeNull();
    raised.Signatory.ShouldBe(Signatory.Application);

    // Et rien du dossier n'attend : l'échéance de l'art. 12.3 est la même qu'avant la question.
    StatutoryDeadline.Of(opened.Reception).On.ShouldBe(StatutoryDeadline.Of(opened.Reception).On);
  }

  /// <summary>
  /// <b>Un seul rattachement suffit à ne poser aucune question.</b> La question dit que la
  /// désignation ne suffit pas ; elle serait fausse dès qu'on a trouvé quelqu'un quelque part.
  /// </summary>
  [Fact]
  public async Task AsksNothingAsSoonAsOneSystemAttachedSomething()
  {
    var opened = ACase();
    TheManifestDeclares(AServedSystem(Boutique));
    TheAdapterServes(AFinding("clients#1203"));

    await LocatingIn(opened);

    opened.Questions.ShouldBeEmpty();
    Written().ShouldNotContain(line => line.Fact == EvidenceLogFact.QuestionRaised);
  }

  /// <summary>
  /// <b>Une réserve en attente n'est pas un zéro, et ne fait donc naître aucune question.</b>
  /// </summary>
  /// <remarks>
  /// Elle ne compte pour aucun rattachement — personne ne l'a tranchée —, mais quelque chose a bel
  /// et bien été trouvé sous les désignations qu'on avait : ce qui manque est un regard, pas une
  /// désignation de plus. Poser la question ici ferait afficher « aucun rattachement nulle part »
  /// juste au-dessus de la ligne que le système vient de rendre.
  /// </remarks>
  [Fact]
  public async Task AsksNothingWhileAReserveAwaitsAHuman()
  {
    var opened = ACase();
    TheManifestDeclares(AServedSystem(Boutique));
    TheAdapterServes(AFinding(reserved: "clients#4417", reason: "Deux comptes portent ce nom."));

    await LocatingIn(opened);

    opened.Questions.ShouldBeEmpty();
    Written().ShouldNotContain(line => line.Fact == EvidenceLogFact.QuestionRaised);
  }

  /// <summary>
  /// <b>Une question à laquelle le dossier a fini par répondre se retire de l'écran.</b>
  /// </summary>
  /// <remarks>
  /// La laisser en ferait un bandeau permanent, et un bandeau permanent s'apprend à ne plus se
  /// voir. Rien n'est perdu : le jour où elle s'est posée est au <c>EvidenceLog</c>, daté, et ce qui y a
  /// répondu porte sa propre ligne datée — le contrôle lit l'écart entre les deux.
  /// </remarks>
  [Fact]
  public async Task WithdrawsTheQuestionTheDaySomethingFinallyAttaches()
  {
    var opened = ACase();
    TheManifestDeclares(AServedSystem(Boutique));
    TheAdapterServes(AFinding());

    await LocatingIn(opened);

    opened.Questions.ShouldHaveSingleItem();

    // Le `Manifest` vieillit exprès : un système déclaré depuis, et jamais appelé, est rattrapé au
    // passage suivant — et lui trouve la personne.
    TheManifestDeclares(AServedSystem(Boutique), AServedSystem(Journal));
    TheAdapterServes(AFinding("journal.log:2026-03"));

    await LocatingIn(opened);

    opened.Questions.ShouldBeEmpty();

    // La preuve, elle, garde le jour où la question s'est posée.
    Written().ShouldContain(line => line.Fact == EvidenceLogFact.QuestionRaised);
  }

  /// <summary>
  /// <b>Un système qui n'a pas répondu empêche la question de naître.</b> Conclure « la désignation
  /// ne suffit pas » avant d'avoir entendu tout le monde ferait poser une question dont on ne sait
  /// pas encore si elle se pose.
  /// </summary>
  [Fact]
  public async Task WaitsForEverySystemToAnswerBeforeAsking()
  {
    var opened = ACase();
    TheManifestDeclares(AServedSystem(Boutique), AServedSystem(Journal));

    _calls.AskAsync<LocateOnTheWire>(
        Arg.Is<AdapterCall>(call => call.DeclaredSystem == Boutique),
        Arg.Any<CancellationToken>())
      .Returns(AdapterAnswer<LocateOnTheWire>.Serving(new LocateOnTheWire(null, null)));

    _calls.AskAsync<LocateOnTheWire>(
        Arg.Is<AdapterCall>(call => call.DeclaredSystem == Journal),
        Arg.Any<CancellationToken>())
      .Returns(AdapterAnswer<LocateOnTheWire>.Deferring(Now.AddHours(6)));

    await LocatingIn(opened);

    opened.Questions.ShouldBeEmpty();
  }

  /// <summary>
  /// <b>Une panne ne laisse pas un zéro, elle laisse un silence.</b> « Pas appelé » et « appelé, rien
  /// trouvé » sont deux déclarations différentes, et seule la seconde a une valeur de preuve. Les
  /// autres systèmes sont appelés quand même : rien ne barre la route.
  /// </summary>
  [Fact]
  public async Task LeavesASystemUnlocatedWhenItsAdapterBreaksDown()
  {
    var opened = ACase();
    TheManifestDeclares(AServedSystem(Boutique), AServedSystem(Journal));

    _calls.AskAsync<LocateOnTheWire>(
        Arg.Is<AdapterCall>(call => call.DeclaredSystem == Boutique),
        Arg.Any<CancellationToken>())
      .Returns<AdapterAnswer<LocateOnTheWire>>(_ => throw new AdapterFailure("Muet."));

    _calls.AskAsync<LocateOnTheWire>(
        Arg.Is<AdapterCall>(call => call.DeclaredSystem == Journal),
        Arg.Any<CancellationToken>())
      .Returns(AdapterAnswer<LocateOnTheWire>.Serving(new LocateOnTheWire(null, null)));

    var located = await LocatingIn(opened);

    located.IsSuccess.ShouldBeTrue();
    opened.LocatingIn(Boutique).ShouldBeNull();
    opened.LocatingIn(Journal).ShouldNotBeNull();

    // Et aucune question : on n'a pas entendu tout le monde.
    opened.Questions.ShouldBeEmpty();
  }

  /// <summary>
  /// <b>Un corps que le contrat ne prévoit pas est une panne</b>, pas un zéro : le service ne
  /// complète rien, et rien du dossier ne bouge.
  /// </summary>
  [Fact]
  public async Task LeavesASystemUnlocatedWhenItServesABodyTheContractDoesNotAllow()
  {
    var opened = ACase();
    TheManifestDeclares(AServedSystem(Boutique));

    _calls.AskAsync<LocateOnTheWire>(Arg.Any<AdapterCall>(), Arg.Any<CancellationToken>())
      .Returns(AdapterAnswer<LocateOnTheWire>.Serving(
        new LocateOnTheWire(null, [new ReservedOnTheWire("clients#4417", Reason: null, null)])));

    await LocatingIn(opened);

    opened.LocatingIn(Boutique).ShouldBeNull();
    Written().ShouldBeEmpty();
  }

  /// <summary>
  /// Un refus laisse une <b>tentative datée</b> — écrite par l'appel lui-même — et ne fait rien
  /// avancer du dossier. Repassé, il ne réécrit pas la même ligne : le verdict n'a pas changé.
  /// </summary>
  [Fact]
  public async Task WritesARefusalOnceAndNeverAdvancesTheCase()
  {
    var opened = ACase();
    TheManifestDeclares(AServedSystem(Boutique));
    _calls.AskAsync<LocateOnTheWire>(Arg.Any<AdapterCall>(), Arg.Any<CancellationToken>())
      .Returns(AdapterAnswer<LocateOnTheWire>.Refusing(AdapterOutcome.SystemNotServed));

    await LocatingIn(opened);
    await LocatingIn(opened);

    // Deux appels — un refus se répare ailleurs, et rouvrir le dossier est le geste par lequel on va
    // voir si ça l'a été —, mais une seule ligne de preuve.
    await _calls.Received(2).AskAsync<LocateOnTheWire>(Arg.Any<AdapterCall>(), Arg.Any<CancellationToken>());
    Written().ShouldHaveSingleItem().Fact.ShouldBe(EvidenceLogFact.AdapterDidNotServeTheSystem);

    // Le désaccord, lui, se signale à chaque fois : son grain est le déploiement, pas le dossier.
    _disagreements.Received(2).Signal(Boutique, AdapterOutcome.SystemNotServed);

    opened.HoldsAnyAttachment.ShouldBeFalse();
    opened.Questions.ShouldBeEmpty();
  }

  /// <summary>Un dossier introuvable n'est pas une panne : c'est une adresse qui n'existe pas.</summary>
  [Fact]
  public async Task SaysNotFoundOnACaseThatDoesNotExist()
  {
    _cases.FirstOrDefaultAsync(Arg.Any<ISingleResultSpecification<Case>>(), Arg.Any<CancellationToken>())
      .Returns((Case?)null);

    var located = await Handler().Handle(new LocateCommand(CaseId.Next()), CancellationToken.None);

    located.Status.ShouldBe(Ardalis.Result.ResultStatus.NotFound);
  }

  private LocateHandler Handler()
  {
    return new LocateHandler(
      _cases,
      _manifest,
      new AdapterCallsForCase(_calls, _evidenceLog, _disagreements, new AClockStuckAt(_now)),
      _evidenceLog,
      new AClockStuckAt(_now));
  }

  private async Task<Ardalis.Result.Result> LocatingIn(Case opened)
  {
    _cases.FirstOrDefaultAsync(Arg.Any<ISingleResultSpecification<Case>>(), Arg.Any<CancellationToken>())
      .Returns(opened);

    return await Handler().Handle(new LocateCommand(opened.Id), CancellationToken.None);
  }

  private void TheManifestDeclares(params DeclaredSystem[] systems)
  {
    _manifest.ListAsync(Arg.Any<CancellationToken>()).Returns([.. systems]);
  }

  private void TheAdapterServes(LocateFindings findings)
  {
    _calls.AskAsync<LocateOnTheWire>(Arg.Any<AdapterCall>(), Arg.Any<CancellationToken>())
      .Returns(AdapterAnswer<LocateOnTheWire>.Serving(OnTheWire(findings)));
  }

  /// <summary>Toutes les lignes réellement écrites dans la preuve, dans l'ordre.</summary>
  private EvidenceLogEntry[] Written()
  {
    return [.. _evidenceLog.ReceivedCalls()
      .Where(call => call.GetMethodInfo().Name == nameof(IEvidenceLog.AppendAsync))
      .Select(call => (EvidenceLogEntry)call.GetArguments()[0]!)];
  }

  private static LocateFindings AFinding(
    string? certain = null,
    string? reserved = null,
    string? reason = null,
    string? proposes = null)
  {
    return LocateFindings.ReadFrom(
      new LocateOnTheWire(
        certain is null ? null : [certain],
        reserved is null
          ? null
          : [new ReservedOnTheWire(
              reserved,
              reason ?? "Un doute que l'application ne sait pas lever.",
              proposes is null ? null : [new DesignationOnTheWire("email", proposes)])]),
      Boutique);
  }

  /// <summary>Ce que l'<c>Adapter</c> aurait écrit sur le fil pour rendre ces mêmes constatations.</summary>
  private static LocateOnTheWire OnTheWire(LocateFindings findings)
  {
    return new LocateOnTheWire(
      [.. findings.Certain.Select(reference => reference.Value)],
      [.. findings.Reserved.Select(reservation => new ReservedOnTheWire(
        reservation.Reference.Value,
        reservation.Reason,
        [.. reservation.Designations.Select(designation =>
          new DesignationOnTheWire(designation.Kind.Token, designation.Value))]))]);
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
      ReceptionDate.Declared(Now.AddDays(-3)));
  }

  private static DeclaredSystem AServedSystem(DeclaredSystemId id)
  {
    return ASystem(id, AnAddress(), Capability.Locate);
  }

  private static DeclaredSystem ASystem(
    DeclaredSystemId id,
    AdapterAddress? adapterAddress,
    params Capability[] capabilities)
  {
    return DeclaredSystem.Declare(
      id,
      SystemLabel.From($"Le système {id.Value}"),
      SystemContents.From("Ce qu'il contient, dans les mots de qui l'a déclaré."),
      capabilities,
      adapterAddress,
      Now.AddMonths(-2));
  }

  private static AdapterAddress AnAddress() => AdapterAddress.From("https://brocanto.example.fr/rgpd");
}
