using System.Diagnostics;
using System.Net;
using MicroserviceRgpd.Core.Qualifications;
using MicroserviceRgpd.Infrastructure.Qualifications;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Qualifications;

/// <summary>
/// L'instrumentation des deux appels sortants.
/// </summary>
/// <remarks>
/// <b>C'est le seul canal qui comptera les échecs</b> que la trace d'audit, par construction,
/// n'enregistrera jamais : celle-ci n'écrit que les verdicts rendus, si bien qu'un moteur muet, une
/// double panne ou une annulation n'y laissent rien. Savoir combien de fois le service a échoué est
/// une matière d'exploitation, et elle n'a que cette porte.
/// </remarks>
public class EngineTelemetryTests : IDisposable
{
  private const string ValidLlmOpinion = """
    {"rights":["Erasure"],"confidence":"High","justification":"Le texte demande la suppression."}
    """;

  private const string ValidLexiconOpinion = """{"rights":["Erasure"]}""";

  private static readonly RightsRequestText Text =
    RightsRequestText.From("Supprimez toutes les données que vous avez sur moi.");

  private readonly List<Activity> _recorded = [];
  private readonly ActivityListener _listener;

  /// <summary>
  /// La racine sous laquelle vivent les seules traces que ce test regarde. Un écouteur est
  /// <b>global au processus</b> : sans elle, les appels que d'autres suites font en même temps
  /// entreraient dans la liste, et les tests se mettraient à dépendre les uns des autres.
  /// </summary>
  private readonly Activity _root = new Activity(nameof(EngineTelemetryTests)).Start();

  public EngineTelemetryTests()
  {
    var root = _root.RootId;

    // Sans écouteur, une `ActivitySource` ne crée rien du tout : c'est cet abonnement qui joue ici
    // le rôle que tient l'exportateur OpenTelemetry en production.
    _listener = new ActivityListener
    {
      ShouldListenTo = source => source.Name == QualificationTelemetry.SourceName,
      Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
      ActivityStopped = activity =>
      {
        if (activity.RootId == root)
        {
          lock (_recorded)
          {
            _recorded.Add(activity);
          }
        }
      },
    };

    ActivitySource.AddActivityListener(_listener);
  }

  public void Dispose()
  {
    _listener.Dispose();
    _root.Dispose();
    GC.SuppressFinalize(this);
  }

  /// <summary>
  /// Les deux moteurs partagent une adresse et ne diffèrent que par leur chemin : la trace le
  /// <b>nomme</b>, là où une trace de transport seule obligerait à lire une URL pour savoir lequel
  /// des deux a parlé.
  /// </summary>
  [Fact]
  public async Task NamesTheEngineItWentToAskOnEverySuccessfulCall()
  {
    await new LlmQualificationEngine(Sidecar(ValidLlmOpinion)).QualifyAsync(Text, CancellationToken.None);
    await new LexiconQualificationEngine(Sidecar(ValidLexiconOpinion)).QualifyAsync(Text, CancellationToken.None);

    _recorded.Select(activity => activity.OperationName)
      .ShouldBe(["opinions/llm", "opinions/lexicon"]);
    _recorded.ShouldAllBe(activity => activity.Status == ActivityStatusCode.Unset);
  }

  /// <summary>
  /// Un moteur qui n'a pas rendu d'avis laisse une trace <b>en erreur</b> : c'est le seul endroit où
  /// cet échec se compte, et le repli lexical l'aura fait disparaître de la réponse.
  /// </summary>
  [Fact]
  public async Task MarksTheTraceInErrorWhenAnEngineRendersNoOpinion()
  {
    var engine = new LlmQualificationEngine(Sidecar("""{"title":"panne"}""", HttpStatusCode.BadGateway));

    await Should.ThrowAsync<QualificationEngineFailure>(() => engine.QualifyAsync(Text, CancellationToken.None));

    var failed = _recorded.ShouldHaveSingleItem();
    failed.Status.ShouldBe(ActivityStatusCode.Error);
    failed.GetTagItem("qualification.failure").ShouldBe("engine");
  }

  /// <summary>
  /// Le dépassement d'échéance se distingue dans la trace comme il se distingue partout ailleurs :
  /// un serveur lent et un serveur éteint ne se réparent pas au même endroit, et l'exploitant doit
  /// pouvoir les compter séparément.
  /// </summary>
  [Fact]
  public async Task TellsALateEngineApartFromABrokenOne()
  {
    var engine = new LlmQualificationEngine(Sidecar("""{"title":"trop lent"}""", HttpStatusCode.GatewayTimeout));

    await Should.ThrowAsync<QualificationEngineDeadlineExceeded>(
      () => engine.QualifyAsync(Text, CancellationToken.None));

    _recorded.ShouldHaveSingleItem().GetTagItem("qualification.failure").ShouldBe("deadline");
  }

  /// <summary>
  /// Un avis refusé <b>après</b> le transport — il manque la confiance que ce moteur promet toujours
  /// — est un échec du moteur exactement comme un serveur muet. Le compter autrement laisserait hors
  /// des métriques la panne que le sidecar est justement bâti pour rendre visible.
  /// </summary>
  [Fact]
  public async Task CountsAnOpinionRefusedAfterTheWireAsAFailureOfTheEngineToo()
  {
    var engine = new LlmQualificationEngine(Sidecar("""{"rights":["Erasure"],"justification":"…"}"""));

    await Should.ThrowAsync<QualificationEngineFailure>(() => engine.QualifyAsync(Text, CancellationToken.None));

    _recorded.ShouldHaveSingleItem().Status.ShouldBe(ActivityStatusCode.Error);
  }

  /// <summary>
  /// <b>Une annulation n'est pas un échec de moteur.</b> La compter comme telle gonflerait le seul
  /// chiffre d'exploitation dont le service dispose, avec des appelants qui sont simplement partis.
  /// </summary>
  [Fact]
  public async Task NeverCountsACallerWhoLeftAsAnEngineFailure()
  {
    using var cancellation = new CancellationTokenSource();
    await cancellation.CancelAsync();

    var engine = new LlmQualificationEngine(Sidecar(ValidLlmOpinion));

    await Should.ThrowAsync<OperationCanceledException>(() => engine.QualifyAsync(Text, cancellation.Token));

    _recorded.ShouldHaveSingleItem().Status.ShouldBe(ActivityStatusCode.Unset);
  }

  private static HttpClient Sidecar(string body, HttpStatusCode status = HttpStatusCode.OK)
  {
    return SidecarDouble.RespondingWith(body, status).Client();
  }
}
