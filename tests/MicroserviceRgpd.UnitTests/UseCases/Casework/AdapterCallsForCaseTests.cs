using System.Reflection;
using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.Adapters;
using MicroserviceRgpd.Core.Casework.EvidenceLog;
using MicroserviceRgpd.UseCases.Casework.CallAdapter;

// Shouldly porte un type du même nom, réservé à ses propres tables de cas.
using Case = MicroserviceRgpd.Core.Casework.Case;

namespace MicroserviceRgpd.UnitTests.UseCases.Casework;

/// <summary>
/// Ce qu'un appel d'<c>Adapter</c> laisse — et ne laisse pas — dans un dossier.
/// </summary>
/// <remarks>
/// Le fait cardinal : <b>un appel refusé n'est pas une affaire de <c>Case</c></b>. Rien n'y bouge ;
/// le <c>EvidenceLog</c> consigne la tentative datée, et le désaccord se signale une fois, au grain du
/// déploiement.
/// </remarks>
public class AdapterCallsForCaseTests
{
  private static readonly DateTimeOffset Now = new(2026, 8, 3, 14, 30, 0, TimeSpan.Zero);

  private readonly IEvidenceLog _ledger = Substitute.For<IEvidenceLog>();
  private readonly IAdapterDisagreements _disagreements = Substitute.For<IAdapterDisagreements>();
  private readonly IAdapterCalls _calls = Substitute.For<IAdapterCalls>();

  /// <summary>
  /// Un refus laisse une <b>tentative datée</b> dans la preuve : le fait qui dit lequel des deux
  /// refus c'était, le système appelé, et personne comme signataire — un appel sortant n'est le
  /// geste d'aucun humain nommé.
  /// </summary>
  [Theory]
  [InlineData(nameof(AdapterOutcome.SecretRefused), nameof(EvidenceLogFact.AdapterRefusedTheSecret))]
  [InlineData(nameof(AdapterOutcome.SystemNotServed), nameof(EvidenceLogFact.AdapterDidNotServeTheSystem))]
  public async Task WritesTheDatedAttemptWhenTheAdapterRefuses(string outcome, string expected)
  {
    var caseId = CaseId.Next();
    TheAdapterAnswers(AdapterAnswer<Found>.Refusing(AdapterOutcome.FromName(outcome)));

    await Calling().AskAsync<Found>(caseId, ALocate());

    var written = Written();

    written.Case.ShouldBe(caseId);
    written.Fact.ShouldBe(EvidenceLogFact.FromName(expected));
    written.OccurredAt.ShouldBe(Now);
    written.DeclaredSystem.ShouldBe(DeclaredSystemId.From("boutique"));
    written.Signatory.ShouldBe(Signatory.Application);
  }

  /// <summary>
  /// Un refus n'a <b>rien cherché</b> : ni désignation, ni même leur compte — un zéro se lirait
  /// comme une recherche menée sous rien.
  /// </summary>
  [Fact]
  public async Task CountsNoDesignationOnARefusalThatSearchedNothing()
  {
    TheAdapterAnswers(AdapterAnswer<Found>.Refusing(AdapterOutcome.SecretRefused));

    await Calling().AskAsync<Found>(CaseId.Next(), ALocate());

    var written = Written();

    written.DesignationCount.ShouldBeNull();
    written.IdentityDeclaration.ShouldBeNull();
  }

  /// <summary>Le désaccord est signalé, une fois par refus rendu — le grain du déploiement est tenu ailleurs.</summary>
  [Fact]
  public async Task SignalsTheDisagreementBesidesWritingTheProof()
  {
    TheAdapterAnswers(AdapterAnswer<Found>.Refusing(AdapterOutcome.SystemNotServed));

    await Calling().AskAsync<Found>(CaseId.Next(), ALocate());

    _disagreements.Received(1).Signal(DeclaredSystemId.From("boutique"), AdapterOutcome.SystemNotServed);
  }

  /// <summary>
  /// Servir et différer ne laissent <b>rien</b> ici : ce qu'ils deviennent appartient à qui a
  /// demandé l'appel, et le <c>EvidenceLog</c> ne consigne pas la mécanique d'un aller-retour.
  /// </summary>
  [Fact]
  public async Task LeavesNothingBehindWhenTheAdapterAnswers()
  {
    TheAdapterAnswers(AdapterAnswer<Found>.Deferring(Now.AddHours(6)));

    var answer = await Calling().AskAsync<Found>(CaseId.Next(), ALocate());

    answer.Outcome.ShouldBe(AdapterOutcome.Deferred);
    answer.DeclaredDeadline.ShouldBe(Now.AddHours(6));

    await _ledger.DidNotReceiveWithAnyArgs().AppendAsync(default!);
    _disagreements.DidNotReceiveWithAnyArgs().Signal(DeclaredSystemId.From("peu-importe"), AdapterOutcome.SecretRefused);
  }

  /// <summary>
  /// <b>Un appel refusé ne modifie rien dans le <c>Case</c></b>, et la règle tient par la
  /// <b>forme</b> : ce type ne reçoit aucun dépôt de dossier, seulement l'identité de celui au
  /// titre duquel l'appel part. Il n'a donc rien à faire avancer, et personne n'a à jurer qu'il ne
  /// l'a pas fait.
  /// </summary>
  [Fact]
  public void HasNoWayOfTouchingTheCaseItCallsFor()
  {
    var collaborators = typeof(AdapterCallsForCase)
      .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
      .Single()
      .GetParameters()
      .Select(parameter => parameter.ParameterType)
      .ToArray();

    collaborators.ShouldNotContain(typeof(IRepository<Case>));
    collaborators.ShouldNotContain(typeof(IReadRepository<Case>));
    collaborators.ShouldNotContain(typeof(Case));
  }

  private AdapterCallsForCase Calling()
  {
    return new AdapterCallsForCase(_calls, _ledger, _disagreements, new AClockStuckAt(Now));
  }

  private void TheAdapterAnswers(AdapterAnswer<Found> answer)
  {
    _calls.AskAsync<Found>(Arg.Any<AdapterCall>(), Arg.Any<CancellationToken>()).Returns(answer);
  }

  /// <summary>La ligne réellement écrite, ou l'échec du test s'il n'y en a pas exactement une.</summary>
  private EvidenceLogEntry Written()
  {
    return _ledger.ReceivedCalls()
      .Where(call => call.GetMethodInfo().Name == nameof(IEvidenceLog.AppendAsync))
      .Select(call => (EvidenceLogEntry)call.GetArguments()[0]!)
      .ToArray()
      .ShouldHaveSingleItem();
  }

  private static AdapterCall ALocate()
  {
    return new AdapterCall(
      AdapterAddress.From("https://brocanto.example.fr/rgpd"),
      DeclaredSystemId.From("boutique"),
      Capability.Locate,
      [Designation.Of(DesignationKind.Email, "helene.petit@example.fr")]);
  }

  /// <summary>Ce qu'une capacité rend, du seul point de vue de ce qui en est consigné : rien.</summary>
  private sealed record Found(int Count);
}
