using MicroserviceRgpd.Core.Qualifications;
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
      Qualification.Of([DataSubjectRight.Erasure]), DeclaredConfidence.High, "Suppression demandée."));
    var witness = new GatedEngine(new QualificationOpinion(Qualification.Of([DataSubjectRight.Erasure])));

    principal.AnswerOnce(witness.Called);
    witness.AnswerOnce(principal.Called);

    var handling = new QualifyHandler(principal, witness, NullLogger<QualifyHandler>.Instance)
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

  private QualifyHandler Handler()
  {
    return new QualifyHandler(_verdictEngine, _witness, NullLogger<QualifyHandler>.Instance);
  }

  private async Task<QualificationOutcome> HandleAsync(string? callerReference = null)
  {
    var result = await Handler().Handle(new QualifyCommand(Text, callerReference), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();

    return result.Value;
  }

  private void GiveThePrincipalEngine(DataSubjectRight verdict, DeclaredConfidence confidence)
  {
    _verdictEngine
      .QualifyAsync(Text, Arg.Any<CancellationToken>())
      .Returns(new QualificationOpinion(
        Qualification.Of([verdict]), confidence, "Le texte demande la suppression des données."));
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
      .Returns(new QualificationOpinion(Qualification.Of([verdict])));
  }

  private void GiveTheWitness(Exception failure)
  {
    _witness
      .QualifyAsync(Text, Arg.Any<CancellationToken>())
      .Returns(Task.FromException<QualificationOpinion>(failure));
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
