using Ardalis.Specification;
using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.Adapters;
using MicroserviceRgpd.Core.Casework.EvidenceLog;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.UseCases.Casework.CallAdapter;
using MicroserviceRgpd.UseCases.Casework.Read;

// Shouldly porte un type du même nom, réservé à ses propres tables de cas.
using Case = MicroserviceRgpd.Core.Casework.Case;

namespace MicroserviceRgpd.UnitTests.UseCases.Casework;

/// <summary>
/// Ce que l'appel de <c>Read</c> fait de la pièce qu'on lui rend, où il la range, et ce qu'il n'en
/// consigne pas.
/// </summary>
/// <remarks>
/// Quatre faits cardinaux : <b>on ne lit que là où l'on a rattaché</b> ; <b>le droit part et la
/// forme jamais</b> ; <b>la pièce est gardée hors de l'agrégat</b>, attribuée au couple (droit,
/// système) ; et <b>la preuve ne porte rien de la pièce</b> — ni son type, ni son nom, ni sa taille.
/// </remarks>
public class ReadHandlerTests
{
  private static readonly DateTimeOffset Now = new(2026, 4, 12, 10, 0, 0, TimeSpan.Zero);
  private static readonly DeclaredSystemId Boutique = DeclaredSystemId.From("brocanto-boutique");
  private static readonly DeclaredSystemId Journal = DeclaredSystemId.From("brocanto-journal");

  private static readonly byte[] SomeBytes = "nom;commande\nJean Dupont;CMD-1"u8.ToArray();

  private readonly IRepository<Case> _cases = Substitute.For<IRepository<Case>>();
  private readonly IReadRepository<DeclaredSystem> _manifest = Substitute.For<IReadRepository<DeclaredSystem>>();
  private readonly IAdapterCalls _calls = Substitute.For<IAdapterCalls>();
  private readonly IAdapterDisagreements _disagreements = Substitute.For<IAdapterDisagreements>();
  private readonly IRetrievedData _retrieved = Substitute.For<IRetrievedData>();
  private readonly IEvidenceLog _ledger = Substitute.For<IEvidenceLog>();

  /// <summary>L'instant que le service lira. Il se <b>dicte</b>, et il avance quand le test veut faire passer une échéance.</summary>
  private DateTimeOffset _now = Now;

  /// <summary>
  /// <b>Seuls les systèmes qui déclarent <c>Read</c>, une adresse, et où le dossier a rattaché sont
  /// appelés.</b> Demander les données d'une personne à une application qui vient de dire ne pas la
  /// connaître ferait remonter une pièce dont personne ne saurait de qui elle parle.
  /// </summary>
  [Fact]
  public async Task ReadsOnlyWhereItFoundSomeoneAndTheCapacityIsDeclared()
  {
    var opened = ACase();

    Attaches(opened, Boutique);
    Attaches(opened, Journal);

    TheManifestDeclares(
      AReadableSystem(Boutique),
      ASystem(Journal, AnAddress(), Capability.Locate),
      AReadableSystem(DeclaredSystemId.From("export-agence")));

    TheAdapterServes(SomeBytes);

    await ReadingIn(opened);

    await _calls.Received(1).ReadAsync(
      Arg.Is<AdapterCall>(call => call.DeclaredSystem == Boutique && call.Capability == Capability.Read),
      Arg.Any<DataSubjectRight>(),
      Arg.Any<CancellationToken>());

    await _calls.Received(1).ReadAsync(
      Arg.Any<AdapterCall>(), Arg.Any<DataSubjectRight>(), Arg.Any<CancellationToken>());
  }

  /// <summary>
  /// <b>Un système où seule une réserve attend n'est pas lu.</b> Ce qui manque est un regard, et lire
  /// avant qu'un humain n'ait tranché ferait rapatrier les données d'un homonyme.
  /// </summary>
  [Fact]
  public async Task WaitsForAHumanBeforeReadingWhereOnlyAReserveIsPending()
  {
    var opened = ACase();

    opened.LocateServed(
      Boutique,
      LocateFindings.ReadFrom(
        new LocateOnTheWire(
          null,
          [new ReservedOnTheWire("clients#4417", "Deux comptes portent ce nom.", null)]),
        Boutique),
      Now);

    TheManifestDeclares(AReadableSystem(Boutique));
    TheAdapterServes(SomeBytes);

    await ReadingIn(opened);

    await _calls.DidNotReceive().ReadAsync(
      Arg.Any<AdapterCall>(), Arg.Any<DataSubjectRight>(), Arg.Any<CancellationToken>());
  }

  /// <summary>
  /// <b>L'appel porte le droit au titre duquel on lit</b>, un appel par droit réclamé : le périmètre
  /// matériel de l'art. 20 est plus étroit que celui de l'art. 15, et il se décide chez le client.
  /// </summary>
  [Fact]
  public async Task CarriesTheRightOfTheClaimItReadsUnder()
  {
    var opened = ACase(DataSubjectRight.Access, DataSubjectRight.Erasure);

    Attaches(opened, Boutique);
    TheManifestDeclares(AReadableSystem(Boutique));
    TheAdapterServes(SomeBytes);

    await ReadingIn(opened);

    await _calls.Received(1).ReadAsync(
      Arg.Any<AdapterCall>(), DataSubjectRight.Access, Arg.Any<CancellationToken>());

    await _calls.Received(1).ReadAsync(
      Arg.Any<AdapterCall>(), DataSubjectRight.Erasure, Arg.Any<CancellationToken>());
  }

  /// <summary>
  /// <b>La pièce est gardée hors de l'agrégat, attribuée au couple (droit, système)</b>, et le dossier
  /// n'en retient que le fait. C'est la couture du fil : ce qui remonte de l'<c>Adapter</c> se
  /// retrouve, entier, chez qui le détiendra.
  /// </summary>
  [Fact]
  public async Task KeepsThePieceOutsideTheCaseAndAttributesItToTheRightSystem()
  {
    var opened = ACase();

    Attaches(opened, Boutique);
    TheManifestDeclares(AReadableSystem(Boutique));
    TheAdapterServes(SomeBytes, "text/csv", "brocanto-2026.csv");

    await ReadingIn(opened);

    var kept = Kept().ShouldHaveSingleItem();

    kept.Case.ShouldBe(opened.Id);
    kept.Right.ShouldBe(DataSubjectRight.Access);
    kept.DeclaredSystem.ShouldBe(Boutique);
    kept.Content.ShouldBe(SomeBytes);
    kept.Envelope.ContentType.ShouldBe("text/csv");
    kept.Envelope.FileName.ShouldBe("brocanto-2026.csv");
    kept.RetrievedAt.ShouldBe(Now);
    kept.IsEmpty.ShouldBeFalse();

    // Et le dossier, lui, ne garde que le fait de la lecture.
    opened.ReadingIn(DataSubjectRight.Access, Boutique)!.LastOutcome.ShouldBe(AdapterOutcome.Served);
  }

  /// <summary>
  /// <b>Corps vide et pièce absente sont deux déclarations différentes.</b> Un corps vide est une
  /// pièce détenue, sans octets : l'<c>Adapter</c> a cherché et n'a rien trouvé. Une pièce absente est
  /// un silence — personne n'a rien dit.
  /// </summary>
  [Fact]
  public async Task TellsAnEmptyPieceApartFromAnAbsentOne()
  {
    var opened = ACase();

    Attaches(opened, Boutique);
    TheManifestDeclares(AReadableSystem(Boutique));
    TheAdapterServes([]);

    await ReadingIn(opened);

    var kept = Kept().ShouldHaveSingleItem();

    kept.IsEmpty.ShouldBeTrue();
    kept.DeclaredSystem.ShouldBe(Boutique);
  }

  /// <summary>
  /// La preuve garde <b>le compte de ce sous quoi on a lu</b>, et rien de la pièce : ni son type, ni
  /// son nom, ni sa taille. Le <c>EvidenceLog</c> est lu par un contrôle ; il ne doit pas devenir un second
  /// endroit où les données de la personne transparaissent.
  /// </summary>
  [Fact]
  public async Task WritesTheSearchAndNothingOfThePiece()
  {
    var opened = ACase();

    Attaches(opened, Boutique);
    TheManifestDeclares(AReadableSystem(Boutique));
    TheAdapterServes(SomeBytes, "text/csv", "brocanto-2026.csv");

    await ReadingIn(opened);

    var written = Written().ShouldHaveSingleItem();

    written.Fact.ShouldBe(EvidenceLogFact.ReadServed);
    written.DeclaredSystem.ShouldBe(Boutique);
    written.Right.ShouldBe(DataSubjectRight.Access);
    written.DesignationCount.ShouldBe(1);
    written.Signatory.ShouldBe(Signatory.Application);
    written.Prose.ShouldBeNull();
  }

  /// <summary>
  /// <b>Une lecture servie ne repart pas.</b> Sa pièce est détenue, elle répond à la question posée
  /// sous le sac d'aujourd'hui, et repasser ferait rapatrier une seconde fois les données de
  /// quelqu'un — c'est-à-dire allonger le séjour que tout ce dispositif cherche à raccourcir.
  /// </summary>
  [Fact]
  public async Task NeverBringsBackTwiceThePieceItAlreadyHolds()
  {
    var opened = ACase();

    Attaches(opened, Boutique);
    TheManifestDeclares(AReadableSystem(Boutique));
    TheAdapterServes(SomeBytes);

    await ReadingIn(opened);
    _ledger.ClearReceivedCalls();
    _retrieved.ClearReceivedCalls();

    await ReadingIn(opened);

    await _calls.Received(1).ReadAsync(
      Arg.Any<AdapterCall>(), Arg.Any<DataSubjectRight>(), Arg.Any<CancellationToken>());

    Kept().ShouldBeEmpty();
    Written().ShouldBeEmpty();
  }

  /// <summary>
  /// <b>Un <c>202</c> sur <c>Read</c> porte son échéance déclarée, relue à l'ouverture du
  /// <c>Case</c></b> — et nulle part ailleurs : il n'existe ni compteur de tentatives, ni
  /// temporisation, ni abandon automatique.
  /// </summary>
  [Fact]
  public async Task ComesBackOnlyAfterTheDeadlineTheAdapterDeclared()
  {
    var opened = ACase();

    Attaches(opened, Boutique);
    TheManifestDeclares(AReadableSystem(Boutique));

    _calls.ReadAsync(Arg.Any<AdapterCall>(), Arg.Any<DataSubjectRight>(), Arg.Any<CancellationToken>())
      .Returns(AdapterAnswer<RetrievedPiece>.Deferring(Now.AddHours(6)));

    await ReadingIn(opened);

    var reading = opened.ReadingIn(DataSubjectRight.Access, Boutique)!;

    reading.LastOutcome.ShouldBe(AdapterOutcome.Deferred);
    reading.DeclaredDeadline.ShouldBe(Now.AddHours(6));

    var written = Written().ShouldHaveSingleItem();

    written.Fact.ShouldBe(EvidenceLogFact.ReadDeferred);
    written.DeclaredDeadline.ShouldBe(Now.AddHours(6));

    // Aucune pièce n'est détenue : différer n'est pas servir un corps vide.
    Kept().ShouldBeEmpty();

    // L'échéance n'est pas passée : on ne repasse pas.
    await ReadingIn(opened);
    await _calls.Received(1).ReadAsync(
      Arg.Any<AdapterCall>(), Arg.Any<DataSubjectRight>(), Arg.Any<CancellationToken>());

    // Elle l'est : on repasse, à l'ouverture du dossier et de nulle part ailleurs.
    _now = Now.AddHours(7);
    await ReadingIn(opened);
    await _calls.Received(2).ReadAsync(
      Arg.Any<AdapterCall>(), Arg.Any<DataSubjectRight>(), Arg.Any<CancellationToken>());
  }

  /// <summary>
  /// Un refus laisse une <b>tentative datée</b> — écrite par l'appel lui-même — et ne fait rien
  /// avancer du dossier. Repassé, il ne réécrit pas la même ligne : le verdict n'a pas changé.
  /// </summary>
  [Fact]
  public async Task WritesARefusalOnceAndHoldsNoPiece()
  {
    var opened = ACase();

    Attaches(opened, Boutique);
    TheManifestDeclares(AReadableSystem(Boutique));

    _calls.ReadAsync(Arg.Any<AdapterCall>(), Arg.Any<DataSubjectRight>(), Arg.Any<CancellationToken>())
      .Returns(AdapterAnswer<RetrievedPiece>.Refusing(AdapterOutcome.SystemNotServed));

    await ReadingIn(opened);
    await ReadingIn(opened);

    // Deux appels — un refus se répare ailleurs —, mais une seule ligne de preuve.
    await _calls.Received(2).ReadAsync(
      Arg.Any<AdapterCall>(), Arg.Any<DataSubjectRight>(), Arg.Any<CancellationToken>());

    Written().ShouldHaveSingleItem().Fact.ShouldBe(EvidenceLogFact.AdapterDidNotServeTheSystem);
    Kept().ShouldBeEmpty();

    // Le désaccord, lui, se signale à chaque fois : son grain est le déploiement, pas le dossier.
    _disagreements.Received(2).Signal(Boutique, AdapterOutcome.SystemNotServed);

    opened.ReadingIn(DataSubjectRight.Access, Boutique)!
      .LastOutcome.ShouldBe(AdapterOutcome.SystemNotServed);
  }

  /// <summary>
  /// <b>Une panne ne laisse pas une pièce vide, elle laisse un silence.</b> « Pas appelé » et
  /// « appelé, rien rendu » sont deux déclarations différentes. Les autres systèmes sont appelés quand
  /// même : rien ne barre la route.
  /// </summary>
  [Fact]
  public async Task LeavesNoReadingAndNoPieceWhenTheAdapterBreaksDown()
  {
    var opened = ACase();

    Attaches(opened, Boutique);
    Attaches(opened, Journal);
    TheManifestDeclares(AReadableSystem(Boutique), AReadableSystem(Journal));

    _calls.ReadAsync(
        Arg.Is<AdapterCall>(call => call.DeclaredSystem == Boutique),
        Arg.Any<DataSubjectRight>(),
        Arg.Any<CancellationToken>())
      .Returns<AdapterAnswer<RetrievedPiece>>(_ => throw new AdapterFailure("Muet."));

    _calls.ReadAsync(
        Arg.Is<AdapterCall>(call => call.DeclaredSystem == Journal),
        Arg.Any<DataSubjectRight>(),
        Arg.Any<CancellationToken>())
      .Returns(AdapterAnswer<RetrievedPiece>.Serving(APiece(SomeBytes)));

    var read = await ReadingIn(opened);

    read.IsSuccess.ShouldBeTrue();
    opened.ReadingIn(DataSubjectRight.Access, Boutique).ShouldBeNull();
    opened.ReadingIn(DataSubjectRight.Access, Journal).ShouldNotBeNull();

    Kept().ShouldHaveSingleItem().DeclaredSystem.ShouldBe(Journal);
  }

  /// <summary>
  /// <b>Un sac enrichi fait repartir la lecture</b> : la question n'est plus la même, et la pièce
  /// d'hier répondait à une question plus étroite. C'est ce qu'un arbitrage vient d'ouvrir.
  /// </summary>
  [Fact]
  public async Task ReadsAgainUnderTheDesignationAnArbitrationJustAdded()
  {
    var opened = ACase();

    opened.LocateServed(
      Boutique,
      LocateFindings.ReadFrom(
        new LocateOnTheWire(
          ["clients#1203"],
          [new ReservedOnTheWire(
            "clients#4417",
            "Deux comptes portent ce nom.",
            [new DesignationOnTheWire("email", "Jean.Dupont@Example.fr")])]),
        Boutique),
      Now);

    TheManifestDeclares(AReadableSystem(Boutique));
    TheAdapterServes(SomeBytes);

    await ReadingIn(opened);

    opened.Arbitrate(Boutique, OpaqueReference.Of("clients#4417"), ReservationState.Attached);

    await ReadingIn(opened);

    await _calls.Received(1).ReadAsync(
      Arg.Is<AdapterCall>(call =>
        call.Designations.Count == 2
        && call.Designations[1].Value == "Jean.Dupont@Example.fr"),
      Arg.Any<DataSubjectRight>(),
      Arg.Any<CancellationToken>());
  }

  /// <summary>Un dossier introuvable n'est pas une panne : c'est une adresse qui n'existe pas.</summary>
  [Fact]
  public async Task SaysNotFoundOnACaseThatDoesNotExist()
  {
    _cases.FirstOrDefaultAsync(Arg.Any<ISingleResultSpecification<Case>>(), Arg.Any<CancellationToken>())
      .Returns((Case?)null);

    var read = await Handler().Handle(new ReadCommand(CaseId.Next()), CancellationToken.None);

    read.Status.ShouldBe(Ardalis.Result.ResultStatus.NotFound);
  }

  private ReadHandler Handler()
  {
    return new ReadHandler(
      _cases,
      _manifest,
      new AdapterCallsForCase(_calls, _ledger, _disagreements, new AClockStuckAt(_now)),
      _retrieved,
      _ledger,
      new AClockStuckAt(_now));
  }

  private async Task<Ardalis.Result.Result> ReadingIn(Case opened)
  {
    _cases.FirstOrDefaultAsync(Arg.Any<ISingleResultSpecification<Case>>(), Arg.Any<CancellationToken>())
      .Returns(opened);

    return await Handler().Handle(new ReadCommand(opened.Id), CancellationToken.None);
  }

  private void TheManifestDeclares(params DeclaredSystem[] systems)
  {
    _manifest.ListAsync(Arg.Any<CancellationToken>()).Returns([.. systems]);
  }

  private void TheAdapterServes(byte[] content, string? contentType = null, string? fileName = null)
  {
    _calls.ReadAsync(Arg.Any<AdapterCall>(), Arg.Any<DataSubjectRight>(), Arg.Any<CancellationToken>())
      .Returns(AdapterAnswer<RetrievedPiece>.Serving(APiece(content, contentType, fileName)));
  }

  /// <summary>Toutes les pièces réellement gardées, dans l'ordre.</summary>
  private RetrievedData[] Kept()
  {
    return [.. _retrieved.ReceivedCalls()
      .Where(call => call.GetMethodInfo().Name == nameof(IRetrievedData.KeepAsync))
      .Select(call => (RetrievedData)call.GetArguments()[0]!)];
  }

  /// <summary>Toutes les lignes réellement écrites dans la preuve, dans l'ordre.</summary>
  private EvidenceLogEntry[] Written()
  {
    return [.. _ledger.ReceivedCalls()
      .Where(call => call.GetMethodInfo().Name == nameof(IEvidenceLog.AppendAsync))
      .Select(call => (EvidenceLogEntry)call.GetArguments()[0]!)];
  }

  private static RetrievedPiece APiece(byte[] content, string? contentType = null, string? fileName = null)
  {
    return new RetrievedPiece(TransportEnvelope.Of(contentType, fileName, Boutique), content);
  }

  /// <summary>Le dossier a rattaché quelqu'un dans ce système : c'est ce qui rend la lecture licite.</summary>
  private static void Attaches(Case opened, DeclaredSystemId declaredSystem)
  {
    opened.LocateServed(
      declaredSystem,
      LocateFindings.ReadFrom(new LocateOnTheWire([$"{declaredSystem.Value}#1203"], null), declaredSystem),
      Now);
  }

  private static Case ACase(params DataSubjectRight[] rights)
  {
    return Case.Open(
      CaseId.Next(),
      IdentityDeclaration.Unverified,
      motivation: null,
      [Designation.Of(DesignationKind.Email, "jean.dupont@example.fr")],
      rights.Length == 0 ? [DataSubjectRight.Access] : rights,
      ClaimOrigin.Named,
      Manifest.Empty,
      ReceptionDate.Declared(Now.AddDays(-3)));
  }

  private static DeclaredSystem AReadableSystem(DeclaredSystemId id)
  {
    return ASystem(id, AnAddress(), Capability.Locate, Capability.Read);
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
