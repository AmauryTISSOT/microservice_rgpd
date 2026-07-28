using MicroserviceRgpd.Core.Qualifications;
using MicroserviceRgpd.UseCases.Qualifications.Qualify;

namespace MicroserviceRgpd.UnitTests.UseCases.Qualifications.Qualify;

/// <summary>
/// Le chemin de qualification tant qu'un seul moteur existe. Le service est alors <b>en permanence
/// diminué</b> : il rend le verdict du témoin, dit qu'il faut relire, lève le booléen de
/// dégradation, et se tait — c'est le repli spécifié, livré avant le mode nominal.
/// </summary>
public class QualifyHandlerTests
{
  private static readonly RightsRequestText Text =
    RightsRequestText.From("Supprimez toutes les données que vous avez sur moi.");

  private readonly IQualificationEngine _witness = Substitute.For<IQualificationEngine>();

  [Fact]
  public async Task RendersTheVerdictOfTheOnlyEngineThatExists()
  {
    var outcome = await HandleWithVerdictAsync(DataSubjectRight.Erasure);

    outcome.Qualification.Rights.ShouldBe([DataSubjectRight.Erasure]);
  }

  /// <summary>
  /// Le témoin n'est pas un moteur principal : quand il tient lieu de verdict, le service le dit.
  /// Taire la dégradation ferait passer le mode où le service se trompe le plus pour sa marche
  /// normale.
  /// </summary>
  [Fact]
  public async Task SaysTheServiceWasNotWholeWhenItRendered()
  {
    var outcome = await HandleWithVerdictAsync(DataSubjectRight.Access);

    outcome.Degraded.ShouldBeTrue();
  }

  /// <summary>
  /// Aucun contrôle indépendant n'a pu avoir lieu : « à relire » le dit exactement, et c'est le
  /// seul signal atteignable — « corroboré » est interdit à tout mode dégradé par le contrat.
  /// </summary>
  [Fact]
  public async Task ForcesTheReviewSignalToNeedsReview()
  {
    var outcome = await HandleWithVerdictAsync(DataSubjectRight.OutOfScope);

    outcome.ReviewSignal.ShouldBe(ReviewSignal.NeedsReview);
  }

  [Fact]
  public async Task EchoesTheCallerReferenceWithoutTouchingIt()
  {
    var outcome = await HandleWithVerdictAsync(DataSubjectRight.Access, callerReference: "  DSAR-8871 ");

    outcome.CallerReference.ShouldBe("  DSAR-8871 ");
  }

  /// <summary>
  /// L'identifiant est ordonné dans le temps — la version 7 n'est pas un détail d'implémentation
  /// libre : c'est ce qui évitera de fragmenter l'index de la trace d'audit.
  /// </summary>
  [Fact]
  public async Task ForgesATimeOrderedIdentifier()
  {
    var outcome = await HandleWithVerdictAsync(DataSubjectRight.Access);

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
    var first = await HandleWithVerdictAsync(DataSubjectRight.Access, callerReference: "DSAR-8871");
    var second = await HandleWithVerdictAsync(DataSubjectRight.Access, callerReference: "DSAR-8871");

    second.QualificationId.ShouldNotBe(first.QualificationId);
  }

  /// <summary>
  /// L'annulation de l'appelant atteint le moteur : un travail poursuivi pour quelqu'un qui est
  /// parti prend la place de celui qui est resté.
  /// </summary>
  [Fact]
  public async Task PassesTheCallersCancellationOnToTheEngine()
  {
    using var cancellation = new CancellationTokenSource();
    GiveTheWitnessAVerdict(DataSubjectRight.Access);

    await new QualifyHandler(_witness).Handle(new QualifyCommand(Text, null), cancellation.Token);

    await _witness.Received(1).QualifyAsync(Text, cancellation.Token);
  }

  private async Task<QualificationOutcome> HandleWithVerdictAsync(
    DataSubjectRight verdict,
    string? callerReference = null)
  {
    GiveTheWitnessAVerdict(verdict);

    var result = await new QualifyHandler(_witness)
      .Handle(new QualifyCommand(Text, callerReference), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();

    return result.Value;
  }

  private void GiveTheWitnessAVerdict(DataSubjectRight verdict)
  {
    var opinion = new QualificationOpinion(Qualification.Of([verdict]));

    _witness
      .QualifyAsync(Text, Arg.Any<CancellationToken>())
      .Returns(opinion);
  }
}
