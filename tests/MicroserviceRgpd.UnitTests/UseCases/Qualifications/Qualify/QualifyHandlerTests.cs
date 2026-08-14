using MicroserviceRgpd.Core.Qualifications;
using MicroserviceRgpd.Core.Qualifications.Audit;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.UseCases.Qualifications.Qualify;
using Microsoft.Extensions.Logging.Abstractions;

namespace MicroserviceRgpd.UnitTests.UseCases.Qualifications.Qualify;

/// <summary>
/// Le chemin de qualification en marche nominale : deux moteurs derrière le même port, appelés en
/// parallèle, et la règle du domaine qui confronte leurs avis.
/// <para>
/// Le handler ne décide de rien : il obtient deux avis, en confie la confrontation au domaine, et
/// forge une identité. Ce qui se vérifie ici est donc l'orchestration — le parallélisme, ce que
/// devient un moteur muet —, jamais la table de corroboration, qui se teste sans lui.
/// </para>
/// </summary>
public class QualifyHandlerTests
{
  private static readonly RightsRequestText Text =
    RightsRequestText.From("Supprimez toutes les données que vous avez sur moi.");

  private readonly IQualificationEngine _verdictEngine = Substitute.For<IQualificationEngine>();
  private readonly IQualificationEngine _witness = Substitute.For<IQualificationEngine>();
  private readonly RecordingAuditTrail _auditTrail = new();
  private readonly RecordingLogger _logger = new();

  /// <summary>
  /// Le témoin est <b>détecteur, jamais contributeur</b> en marche nominale : les droits rendus sont
  /// ceux du moteur principal, même quand le témoin en voit d'autres.
  /// </summary>
  [Fact]
  public async Task RendersTheVerdictOfThePrincipalEngineAndNeverThatOfTheWitness()
  {
    GiveThePrincipalEngine(DataSubjectRight.Erasure, DeclaredConfidence.High);
    GiveTheWitness(DataSubjectRight.Objection);

    var outcome = await HandleAsync();

    outcome.Qualification.ShouldBe(Qualification.Of([DataSubjectRight.Erasure]));
    outcome.ReviewSignal.ShouldBe(ReviewSignal.Contested);
    outcome.Degraded.ShouldBeFalse();
  }

  /// <summary>
  /// Les deux moteurs sont appelés <b>en parallèle</b> : le temps de réponse du service est celui du
  /// seul appel LLM, le lexique ne coûtant qu'une fraction de milliseconde. Chaque doublure attend
  /// ici que l'autre ait démarré — un enchaînement séquentiel n'en sortirait jamais.
  /// </summary>
  [Fact]
  public async Task AsksBothEnginesAtOnceRatherThanOneAfterTheOther()
  {
    var principal = new GatedEngine(new QualificationOpinion(
      Qualification.Of([DataSubjectRight.Erasure]),
      AnEngine.HoldingTheVerdict,
      DeclaredConfidence.High,
      "Suppression demandée."));
    var witness = new GatedEngine(
      new QualificationOpinion(Qualification.Of([DataSubjectRight.Erasure]), AnEngine.HoldingTheWitness));

    principal.AnswerOnce(witness.Called);
    witness.AnswerOnce(principal.Called);

    var handling = new QualifyHandler(
        witness, _auditTrail, TimeProvider.System, NullLogger<QualifyHandler>.Instance, principal)
      .Handle(new QualifyCommand(Text, null), CancellationToken.None)
      .AsTask();

    var outcome = await handling.WaitAsync(TimeSpan.FromSeconds(5));

    outcome.Value.ReviewSignal.ShouldBe(ReviewSignal.Corroborated);
  }

  /// <summary>
  /// La justification du moteur principal traverse le service jusqu'à l'opérateur : c'est le seul
  /// texte du parcours qu'un humain lira.
  /// </summary>
  [Fact]
  public async Task CarriesTheJustificationOfThePrincipalEngine()
  {
    GiveThePrincipalEngine(DataSubjectRight.Erasure, DeclaredConfidence.High);
    GiveTheWitness(DataSubjectRight.Erasure);

    var outcome = await HandleAsync();

    outcome.Justification.ShouldBe("Le texte demande la suppression des données.");
    outcome.ReviewSignal.ShouldBe(ReviewSignal.Corroborated);
  }

  /// <summary>
  /// Le repli lexical, dans sa forme définitive : quand le moteur principal ne rend aucun avis —
  /// <b>quelle qu'en soit la raison</b> —, le témoin produit le verdict, le signal vaut « à relire »,
  /// et la réponse est muette.
  /// </summary>
  [Fact]
  public async Task FallsBackOnTheWitnessWhenThePrincipalEngineRendersNoOpinion()
  {
    GiveThePrincipalEngine(new QualificationEngineFailure("Le moteur LLM a répondu 503."));
    GiveTheWitness(DataSubjectRight.Erasure);

    var outcome = await HandleAsync();

    outcome.Qualification.ShouldBe(Qualification.Of([DataSubjectRight.Erasure]));
    outcome.ReviewSignal.ShouldBe(ReviewSignal.NeedsReview);
    outcome.Degraded.ShouldBeTrue();
    outcome.Justification.ShouldBeNull();
  }

  /// <summary>
  /// <b>Un rôle qui n'est pourvu par rien</b> — le service tourne sans moteur de verdict. Ce que le
  /// handler apprend est qu'un rôle peut manquer, jamais qu'il s'agit d'un LLM : il ne l'interroge
  /// pas, et son avis entre comme un avis manquant, la forme même qu'il produit déjà pour un moteur
  /// muet. Le domaine ne reçoit aucune règle nouvelle, le repli existant fait tout le travail.
  /// </summary>
  [Fact]
  public async Task FallsBackOnTheWitnessWhenNoEngineHoldsTheVerdictRole()
  {
    GiveTheWitness(DataSubjectRight.Erasure);

    var outcome = await HandleAsync(HandlerWithoutAVerdictEngine());

    outcome.Qualification.ShouldBe(Qualification.Of([DataSubjectRight.Erasure]));
    outcome.ReviewSignal.ShouldBe(ReviewSignal.NeedsReview);
    outcome.Degraded.ShouldBeTrue();
    outcome.Justification.ShouldBeNull();
  }

  /// <summary>
  /// <b>Un choix délibéré n'est pas une panne.</b> Un rôle non pourvu ne fait journaliser aucun
  /// avertissement : les alertes d'un exploitant ne doivent pas se déclencher sur une décision de
  /// configuration. C'est la raison pour laquelle un moteur factice qui lèverait toujours a été
  /// écarté.
  /// </summary>
  [Fact]
  public async Task WarnsOfNoEngineFailureWhenTheVerdictRoleIsSimplyNotProvisioned()
  {
    GiveTheWitness(DataSubjectRight.Erasure);

    await HandleAsync(HandlerWithoutAVerdictEngine());

    _logger.Warnings.ShouldBeEmpty();
  }

  /// <summary>
  /// La ligne d'audit d'un service sans moteur de verdict est celle d'un moteur tombé : avis nul,
  /// latence nulle. Aucune colonne nouvelle ne les distingue, et la limite est assumée — dans un tel
  /// déploiement, l'absence de LLM est un fait de déploiement, pas un fait de qualification.
  /// </summary>
  [Fact]
  public async Task RecordsTheUnprovisionedVerdictRoleAsAMissingOpinionWithoutLatency()
  {
    GiveTheWitness(DataSubjectRight.Erasure);

    await HandleAsync(HandlerWithoutAVerdictEngine());

    var entry = _auditTrail.Entries.ShouldHaveSingleItem();
    entry.VerdictOpinion.ShouldBeNull();
    entry.VerdictLatency.ShouldBeNull();
    entry.LexiconOpinion!.Engine.ShouldBe(AnEngine.HoldingTheWitness);
  }

  /// <summary>
  /// Rôle non pourvu <b>et</b> témoin muet : il ne reste rien à qualifier, et la panne du témoin est
  /// la seule qu'il y ait à présenter.
  /// </summary>
  [Fact]
  public async Task PresentsTheFailureOfTheWitnessWhenItIsSilentAndNoEngineHoldsTheVerdictRole()
  {
    GiveTheWitness(new QualificationEngineFailure("Le moteur lexical a répondu 500."));

    var failure = await Should.ThrowAsync<QualificationEngineFailure>(
      () => HandleAsync(HandlerWithoutAVerdictEngine()));

    failure.Message.ShouldContain("500");
    _auditTrail.Entries.ShouldBeEmpty();
  }

  /// <summary>
  /// Symétrie non négociable : <b>un lexique mort ne doit pas s'éteindre en silence</b>. Le verdict
  /// est normal, mais il n'a reçu aucun contrôle, et le booléen de dégradation le dit.
  /// </summary>
  [Fact]
  public async Task RaisesTheDegradationFlagWhenItIsTheWitnessThatIsMissing()
  {
    GiveThePrincipalEngine(DataSubjectRight.Erasure, DeclaredConfidence.High);
    GiveTheWitness(new QualificationEngineFailure("Le moteur lexical a répondu 500."));

    var outcome = await HandleAsync();

    outcome.Qualification.ShouldBe(Qualification.Of([DataSubjectRight.Erasure]));
    outcome.Degraded.ShouldBeTrue();
    outcome.ReviewSignal.ShouldBe(ReviewSignal.NeedsReview);
  }

  /// <summary>
  /// Les deux moteurs muets, c'est une panne du service et non une qualification faible : le handler
  /// n'a rien à rendre, et il présente la panne du <b>moteur principal</b> — c'est elle qui décidera
  /// du code rendu à l'appelant.
  /// </summary>
  [Fact]
  public async Task PresentsTheFailureOfThePrincipalEngineWhenNeitherRenderedAnOpinion()
  {
    GiveThePrincipalEngine(new QualificationEngineFailure("Le moteur LLM a répondu 504."));
    GiveTheWitness(new QualificationEngineFailure("Le moteur lexical a répondu 500."));

    var failure = await Should.ThrowAsync<QualificationEngineFailure>(() => HandleAsync());

    failure.Message.ShouldContain("504");

    // La trace enregistre les verdicts, jamais les tentatives : une double panne n'a rien qualifié,
    // et il n'y a aucun verdict dont répondre.
    _auditTrail.Entries.ShouldBeEmpty();
  }

  /// <summary>
  /// Le dépassement d'échéance du moteur principal traverse <b>avec son type</b> : c'est lui, et lui
  /// seul, qui distingue le code rendu à l'appelant de celui d'une panne ordinaire.
  /// </summary>
  [Fact]
  public async Task KeepsTheDeadlineFailureOfThePrincipalEngineDistinctFromAnyOtherFailure()
  {
    GiveThePrincipalEngine(new QualificationEngineDeadlineExceeded("Le moteur LLM a dépassé son échéance."));
    GiveTheWitness(new QualificationEngineFailure("Le moteur lexical a répondu 500."));

    await Should.ThrowAsync<QualificationEngineDeadlineExceeded>(() => HandleAsync());
  }

  /// <summary>
  /// Un moteur peut se taire pour une raison que personne n'a prévue. Cela reste une double panne :
  /// le handler la <b>nomme</b>, plutôt que de laisser une cause inattendue ressortir en erreur
  /// interne, et il garde la cause d'origine attachée pour l'exploitant.
  /// </summary>
  [Fact]
  public async Task NamesTheDoubleFailureEvenWhenNeitherEngineFailedInAWayItKnows()
  {
    var unforeseen = new InvalidOperationException("Le socle HTTP n'a jamais laissé partir l'appel.");
    GiveThePrincipalEngine(unforeseen);
    GiveTheWitness(new TimeoutException("Le témoin non plus."));

    var failure = await Should.ThrowAsync<QualificationEngineFailure>(() => HandleAsync());

    failure.ShouldNotBeOfType<QualificationEngineDeadlineExceeded>();
    failure.InnerException.ShouldBeSameAs(unforeseen);
  }

  /// <summary>
  /// L'annulation de l'appelant n'est pas une panne de moteur : elle ressort telle quelle, et ne se
  /// déguise ni en verdict dégradé ni en repli. Un appelant parti ne reçoit rien.
  /// </summary>
  [Fact]
  public async Task PropagatesTheCallersCancellationRatherThanDegradingQuietly()
  {
    using var cancellation = new CancellationTokenSource();
    await cancellation.CancelAsync();

    _verdictEngine.QualifyAsync(Text, Arg.Any<CancellationToken>())
      .Returns(Task.FromCanceled<QualificationOpinion>(cancellation.Token));
    _witness.QualifyAsync(Text, Arg.Any<CancellationToken>())
      .Returns(Task.FromCanceled<QualificationOpinion>(cancellation.Token));

    await Should.ThrowAsync<OperationCanceledException>(
      () => Handler().Handle(new QualifyCommand(Text, null), cancellation.Token).AsTask());

    // Un appelant parti ne reçoit rien, et ne laisse rien : aucune ligne pour un acte qui n'a pas eu
    // lieu.
    _auditTrail.Entries.ShouldBeEmpty();
  }

  /// <summary>
  /// L'annulation de l'appelant atteint <b>les deux</b> moteurs : le GPU sérialisant les appels, un
  /// travail poursuivi pour quelqu'un qui est parti prend la place de celui qui est resté.
  /// </summary>
  [Fact]
  public async Task PassesTheCallersCancellationOnToBothEngines()
  {
    using var cancellation = new CancellationTokenSource();
    GiveThePrincipalEngine(DataSubjectRight.Access, DeclaredConfidence.High);
    GiveTheWitness(DataSubjectRight.Access);

    await Handler().Handle(new QualifyCommand(Text, null), cancellation.Token);

    await _verdictEngine.Received(1).QualifyAsync(Text, cancellation.Token);
    await _witness.Received(1).QualifyAsync(Text, cancellation.Token);
  }

  [Fact]
  public async Task EchoesTheCallerReferenceWithoutTouchingIt()
  {
    GiveThePrincipalEngine(DataSubjectRight.Access, DeclaredConfidence.High);
    GiveTheWitness(DataSubjectRight.Access);

    var outcome = await HandleAsync(callerReference: "  DSAR-8871 ");

    outcome.CallerReference.ShouldBe("  DSAR-8871 ");
  }

  /// <summary>
  /// L'identifiant est ordonné dans le temps — la version 7 n'est pas un détail d'implémentation
  /// libre : c'est ce qui évitera de fragmenter l'index de la trace d'audit.
  /// </summary>
  [Fact]
  public async Task ForgesATimeOrderedIdentifier()
  {
    GiveThePrincipalEngine(DataSubjectRight.Access, DeclaredConfidence.High);
    GiveTheWitness(DataSubjectRight.Access);

    var outcome = await HandleAsync();

    outcome.QualificationId.ShouldNotBe(Guid.Empty);
    outcome.QualificationId.Version.ShouldBe(7);
  }

  /// <summary>
  /// La référence appelante <b>n'est pas une clé d'idempotence</b> : deux appels qui la partagent
  /// sont deux qualifications distinctes, et deux identifiants distincts.
  /// </summary>
  [Fact]
  public async Task ForgesAFreshIdentifierForEachCallEvenUnderTheSameCallerReference()
  {
    GiveThePrincipalEngine(DataSubjectRight.Access, DeclaredConfidence.High);
    GiveTheWitness(DataSubjectRight.Access);

    var first = await HandleAsync(callerReference: "DSAR-8871");
    var second = await HandleAsync(callerReference: "DSAR-8871");

    second.QualificationId.ShouldNotBe(first.QualificationId);
  }

  /// <summary>
  /// La trace conserve le verdict <b>et ses prémisses</b> : les deux avis bruts, chacun avec le
  /// moteur qui l'a rendu. Enregistrer une conclusion sans ses prémisses ne permettrait de répondre
  /// de rien.
  /// </summary>
  [Fact]
  public async Task WritesTheVerdictAndBothRawOpinionsToTheTrace()
  {
    GiveThePrincipalEngine(DataSubjectRight.Erasure, DeclaredConfidence.High);
    GiveTheWitness(DataSubjectRight.Erasure);

    var outcome = await HandleAsync(callerReference: "DSAR-8871");

    var entry = _auditTrail.Entries.ShouldHaveSingleItem();
    entry.QualificationId.ShouldBe(outcome.QualificationId);
    entry.Text.ShouldBe(Text);
    entry.Qualification.ShouldBe(Qualification.Of([DataSubjectRight.Erasure]));
    entry.ReviewSignal.ShouldBe(ReviewSignal.Corroborated);
    entry.CallerReference.ShouldBe("DSAR-8871");

    entry.VerdictOpinion!.Engine.ShouldBe(AnEngine.HoldingTheVerdict);
    entry.VerdictOpinion.DeclaredConfidence.ShouldBe(DeclaredConfidence.High);
    entry.LexiconOpinion!.Engine.ShouldBe(AnEngine.HoldingTheWitness);
  }

  /// <summary>
  /// Un repli lexical et un lexique absent produisent <b>deux formes de ligne distinguables</b>, et
  /// c'est la nullité de l'avis manquant qui les distingue — jamais un champ nommant la dégradation.
  /// </summary>
  [Fact]
  public async Task RecordsTheFallbackByTheAbsenceOfTheVerdictOpinion()
  {
    GiveThePrincipalEngine(new QualificationEngineFailure("Le moteur LLM a répondu 503."));
    GiveTheWitness(DataSubjectRight.Erasure);

    await HandleAsync();

    var entry = _auditTrail.Entries.ShouldHaveSingleItem();
    entry.VerdictOpinion.ShouldBeNull();
    entry.VerdictLatency.ShouldBeNull();
    entry.LexiconOpinion.ShouldNotBeNull();
  }

  /// <summary>
  /// L'autre forme de dégradation : le verdict est normal, mais il n'a reçu aucun contrôle. Le
  /// booléen public recouvre les deux situations ; la trace les sépare.
  /// </summary>
  [Fact]
  public async Task RecordsTheMissingWitnessByTheAbsenceOfTheLexiconOpinion()
  {
    GiveThePrincipalEngine(DataSubjectRight.Erasure, DeclaredConfidence.High);
    GiveTheWitness(new QualificationEngineFailure("Le moteur lexical a répondu 500."));

    await HandleAsync();

    var entry = _auditTrail.Entries.ShouldHaveSingleItem();
    entry.LexiconOpinion.ShouldBeNull();
    entry.LexiconLatency.ShouldBeNull();
    entry.VerdictOpinion.ShouldNotBeNull();
  }

  /// <summary>
  /// <b>Qualifier, écrire, répondre.</b> Un échec d'écriture est une panne du service : il remonte,
  /// et rien n'est rendu à l'appelant.
  /// </summary>
  [Fact]
  public async Task FailsWhenTheTraceCannotBeWritten()
  {
    GiveThePrincipalEngine(DataSubjectRight.Erasure, DeclaredConfidence.High);
    GiveTheWitness(DataSubjectRight.Erasure);
    _auditTrail.Refuse(new InvalidOperationException("La base est indisponible."));

    await Should.ThrowAsync<InvalidOperationException>(() => HandleAsync());
  }

  /// <summary>
  /// Et il remonte <b>aussi quand la qualification était dégradée</b> : la dégradation d'un moteur ne
  /// survit pas à une panne de base, et aucun identifiant de qualification creux ne doit circuler,
  /// sous peine de vider de sens tous les autres.
  /// </summary>
  [Fact]
  public async Task FailsWhenTheTraceCannotBeWrittenEvenForADegradedQualification()
  {
    GiveThePrincipalEngine(new QualificationEngineFailure("Le moteur LLM a répondu 503."));
    GiveTheWitness(DataSubjectRight.Erasure);
    _auditTrail.Refuse(new InvalidOperationException("La base est indisponible."));

    await Should.ThrowAsync<InvalidOperationException>(() => HandleAsync());
  }

  private QualifyHandler Handler()
  {
    return new QualifyHandler(
      _witness, _auditTrail, TimeProvider.System, _logger, _verdictEngine);
  }

  /// <summary>Le handler tel que le construit un service où le rôle de verdict n'est pourvu par rien.</summary>
  private QualifyHandler HandlerWithoutAVerdictEngine()
  {
    return new QualifyHandler(_witness, _auditTrail, TimeProvider.System, _logger);
  }

  private async Task<QualificationOutcome> HandleAsync(string? callerReference = null)
  {
    return await HandleAsync(Handler(), callerReference);
  }

  private static async Task<QualificationOutcome> HandleAsync(QualifyHandler handler, string? callerReference = null)
  {
    var result = await handler.Handle(new QualifyCommand(Text, callerReference), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();

    return result.Value;
  }

  private void GiveThePrincipalEngine(DataSubjectRight verdict, DeclaredConfidence confidence)
  {
    _verdictEngine
      .QualifyAsync(Text, Arg.Any<CancellationToken>())
      .Returns(new QualificationOpinion(
        Qualification.Of([verdict]),
        AnEngine.HoldingTheVerdict,
        confidence,
        "Le texte demande la suppression des données."));
  }

  private void GiveThePrincipalEngine(Exception failure)
  {
    _verdictEngine
      .QualifyAsync(Text, Arg.Any<CancellationToken>())
      .Returns(Task.FromException<QualificationOpinion>(failure));
  }

  private void GiveTheWitness(DataSubjectRight verdict)
  {
    // Ni confiance, ni justification : le lexique n'en produit pas, et la doublure ne doit pas rendre
    // atteignable en test un état que le vrai moteur n'atteint jamais.
    _witness
      .QualifyAsync(Text, Arg.Any<CancellationToken>())
      .Returns(new QualificationOpinion(Qualification.Of([verdict]), AnEngine.HoldingTheWitness));
  }

  private void GiveTheWitness(Exception failure)
  {
    _witness
      .QualifyAsync(Text, Arg.Any<CancellationToken>())
      .Returns(Task.FromException<QualificationOpinion>(failure));
  }

  /// <summary>
  /// Une trace d'audit réduite à ce qu'un test du handler a besoin d'en savoir : ce qu'on lui a
  /// demandé d'écrire, et la panne qu'on lui dicte.
  /// </summary>
  private sealed class RecordingAuditTrail : IQualificationAuditTrail
  {
    private Exception? _refusal;

    /// <summary>Les entrées écrites, dans l'ordre. Vide dit qu'aucun acte n'a laissé de ligne.</summary>
    public List<QualificationAuditEntry> Entries { get; } = [];

    /// <summary>Fait échouer la prochaine écriture, comme le ferait une base indisponible.</summary>
    public void Refuse(Exception refusal) => _refusal = refusal;

    public Task RecordAsync(QualificationAuditEntry entry, CancellationToken cancellationToken = default)
    {
      if (_refusal is not null)
      {
        return Task.FromException(_refusal);
      }

      Entries.Add(entry);

      return Task.CompletedTask;
    }
  }

  /// <summary>
  /// Un journal réduit à la seule question qu'un test pose ici : le silence d'un moteur a-t-il fait
  /// journaliser un avertissement ? Un rôle non pourvu ne doit en produire aucun.
  /// </summary>
  private sealed class RecordingLogger : ILogger<QualifyHandler>
  {
    /// <summary>Les avertissements émis, dans l'ordre. Vide dit qu'aucun moteur n'a été porté disparu.</summary>
    public List<string> Warnings { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
      LogLevel logLevel,
      EventId eventId,
      TState state,
      Exception? exception,
      Func<TState, Exception?, string> formatter)
    {
      if (logLevel >= LogLevel.Warning)
      {
        Warnings.Add(formatter(state, exception));
      }
    }
  }

  /// <summary>
  /// Un moteur qui signale son appel et n'y répond qu'une fois l'autre parti : deux d'entre eux
  /// croisés ne se dénouent que si le handler les appelle vraiment en même temps.
  /// </summary>
  private sealed class GatedEngine(QualificationOpinion opinion) : IQualificationEngine
  {
    private readonly TaskCompletionSource _called = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Task _release = Task.CompletedTask;

    public Task Called => _called.Task;

    public void AnswerOnce(Task release)
    {
      _release = release;
    }

    public async Task<QualificationOpinion> QualifyAsync(
      RightsRequestText text,
      CancellationToken cancellationToken = default)
    {
      _called.TrySetResult();

      await _release;

      return opinion;
    }
  }
}
