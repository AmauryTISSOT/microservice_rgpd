using System.Diagnostics;
using System.Net;
using System.Text;
using MicroserviceRgpd.Core.Qualifications;
using MicroserviceRgpd.Infrastructure.Qualifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Qualifications;

/// <summary>
/// Le pipeline de résilience des deux clients de moteur — <b>vérifié plutôt que supposé</b>.
/// </summary>
/// <remarks>
/// <para>
/// Les réglages du dépôt appliquent un pipeline standard à <b>tous</b> les clients HTTP, et ses
/// valeurs d'usine contredisent frontalement le contrat interne : trente secondes couperaient un
/// appel LLM légitime, et trois reprises mettraient quatre générations à la queue leu leu sur un GPU
/// qui sérialise, pour une seule requête entrante. La décision est donc de <b>remplacer</b> ce
/// pipeline sur les deux clients de moteur, et de le laisser en place partout ailleurs.
/// </para>
/// <para>
/// Cette suite reconstitue ce que les réglages du dépôt font — un pipeline standard par défaut sur
/// tout client — puis exerce les vraies inscriptions. Un seul écart avec la production : le délai
/// entre deux reprises du pipeline standard est raccourci, faute de quoi le témoin de contrôle
/// coûterait une dizaine de secondes à lui seul. Le nombre de reprises, lui, est celui d'usine —
/// c'est lui qu'on regarde.
/// </para>
/// </remarks>
public class EngineResiliencePipelineTests
{
  private static readonly RightsRequestText Text =
    RightsRequestText.From("Supprimez toutes les données que vous avez sur moi.");

  /// <summary>Un avis valide, pour les tests où ce n'est pas la réponse qui est regardée.</summary>
  private const string ValidLlmOpinion = """
    {"rights":["Erasure"],"confidence":"High","justification":"Le texte demande la suppression."}
    """;

  private const string ValidLexiconOpinion = """{"rights":["Erasure"]}""";

  /// <summary>
  /// Le seuil de déclenchement du disjoncteur standard se compte en centaines d'appels sur une
  /// fenêtre de trente secondes. Le dépasser franchement est la seule façon de prouver qu'il n'y en
  /// a aucun — ces appels ne quittent pas la mémoire, et ne coûtent rien.
  /// </summary>
  private const int WellBeyondAnyCircuitBreakerThreshold = 200;

  /// <summary>
  /// <b>Aucune reprise sur aucun des deux moteurs.</b> À température nulle avec seed fixe, rejouer
  /// est une opération nulle ; depuis qu'il existe un repli lexical la reprise est de surcroît
  /// dominée, et elle masquerait dans les métriques un problème de qualité du modèle qui doit rester
  /// visible.
  /// </summary>
  [Theory]
  [InlineData(QualificationEngineRole.Verdict)]
  [InlineData(QualificationEngineRole.Witness)]
  public async Task NeverReplaysAnEngineThatFailed(string role)
  {
    var sidecar = new CountingSidecar(HttpStatusCode.InternalServerError, "{}");
    using var services = Registered(sidecar);

    await Should.ThrowAsync<QualificationEngineFailure>(
      () => Engine(services, role).QualifyAsync(Text, CancellationToken.None));

    sidecar.Calls.ShouldBe(1);
  }

  /// <summary>
  /// Le pipeline explicite <b>remplace</b> celui que les réglages du dépôt posent par défaut : sans
  /// ce remplacement, l'échec ci-dessus aurait été rejoué trois fois — le témoin de contrôle le
  /// montre sur un client ordinaire, dans la même application.
  /// </summary>
  [Fact]
  public async Task LeavesTheDefaultPipelineInPlaceForEveryOtherClient()
  {
    var sidecar = new CountingSidecar(HttpStatusCode.InternalServerError, "{}");
    using var services = Registered(sidecar);

    var ordinary = services.GetRequiredService<IHttpClientFactory>().CreateClient(OrdinaryClient);

    using var response = await ordinary.GetAsync(new Uri("http://qualification-sidecar/n-importe-quoi"));

    // Le défaut d'usine reprend trois fois : quatre passages en tout. Ce qui est vérifié n'est pas
    // le chiffre pour lui-même, c'est qu'un client ordinaire a **gardé** ce que les moteurs perdent.
    sidecar.Calls.ShouldBeGreaterThan(1);
  }

  /// <summary>
  /// <b>Aucun disjoncteur sur le client LLM.</b> Il n'achèterait que de la latence pendant une panne
  /// lente de l'amont — or la latence n'est pas un critère de rejet —, c'est un seuil que personne
  /// n'a de quoi régler, et il rendrait le service non reproductible du point de vue de l'appelant.
  /// </summary>
  [Fact]
  public async Task NeverStopsCallingTheLlmEngineHoweverManyTimesItHasFailed()
  {
    var sidecar = new CountingSidecar(HttpStatusCode.InternalServerError, "{}");
    using var services = Registered(sidecar);

    for (var attempt = 0; attempt < WellBeyondAnyCircuitBreakerThreshold; attempt++)
    {
      await Should.ThrowAsync<QualificationEngineFailure>(
        () => Engine(services, QualificationEngineRole.Verdict).QualifyAsync(Text, CancellationToken.None));
    }

    // Un disjoncteur aurait cessé de laisser passer bien avant : les derniers appels n'auraient
    // jamais atteint le sidecar, et le compte serait resté en deçà.
    sidecar.Calls.ShouldBe(WellBeyondAnyCircuitBreakerThreshold);
  }

  /// <summary>
  /// Chaque moteur porte <b>son</b> échéance, et non une échéance partagée : celle du témoin est
  /// franchement plus courte, au point qu'il ne puisse jamais rallonger le temps de réponse du
  /// service, là où celle du verdict doit laisser une génération aboutir.
  /// </summary>
  [Fact]
  public async Task GivesTheWitnessADeadlineShortEnoughToNeverLengthenTheService()
  {
    var sidecar = new SlowSidecar(ValidLexiconOpinion);
    using var services = Registered(sidecar, llmDeadlineSeconds: 30, lexiconDeadlineSeconds: 0.2);

    var elapsed = Stopwatch.StartNew();

    await Should.ThrowAsync<QualificationEngineDeadlineExceeded>(
      () => Engine(services, QualificationEngineRole.Witness).QualifyAsync(Text, CancellationToken.None));

    // Le témoin a renoncé sur *sa* propre échéance ; celle du verdict, cent cinquante fois plus
    // longue, aurait laissé le service attendre.
    elapsed.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(10));
  }

  /// <summary>
  /// L'échéance dépassée <b>arrive nommée</b> plutôt que sous une annulation muette : c'est elle,
  /// et elle seule, qui distinguera plus tard le dépassement de l'indisponibilité en double panne.
  /// </summary>
  [Fact]
  public async Task NamesTheDeadlineItCutOnRatherThanRaisingABareCancellation()
  {
    var sidecar = new SlowSidecar(ValidLlmOpinion);
    using var services = Registered(sidecar, llmDeadlineSeconds: 0.2, lexiconDeadlineSeconds: 0.2);

    var tooSlow = await Should.ThrowAsync<QualificationEngineDeadlineExceeded>(
      () => Engine(services, QualificationEngineRole.Verdict).QualifyAsync(Text, CancellationToken.None));

    tooSlow.Message.ShouldContain("échéance");
  }

  /// <summary>
  /// L'échéance du pipeline ne doit pas se laisser devancer par celle, muette, que
  /// <see cref="HttpClient"/> porte d'usine : cent secondes couperaient l'appel LLM avant les cent
  /// cinquante que le contrat interne lui laisse, et le dépassement arriverait sans nom.
  /// </summary>
  [Theory]
  [InlineData(QualificationEngineRole.Verdict)]
  [InlineData(QualificationEngineRole.Witness)]
  public void LeavesTheDeadlineToThePipelineRatherThanToTheClient(string role)
  {
    using var services = Registered(new CountingSidecar(HttpStatusCode.OK, ValidLlmOpinion));

    var client = services.GetRequiredService<IHttpClientFactory>()
      .CreateClient(role == QualificationEngineRole.Verdict
        ? nameof(LlmQualificationEngine)
        : nameof(LexiconQualificationEngine));

    client.Timeout.ShouldBe(Timeout.InfiniteTimeSpan);
  }

  /// <summary>
  /// Une échéance absente de la configuration <b>arrête le démarrage</b>, plutôt que de se replier
  /// sur un chiffre codé en dur qui ferait exister deux vérités.
  /// </summary>
  [Theory]
  [InlineData("Qualification:LlmDeadlineSeconds")]
  [InlineData("Qualification:LexiconDeadlineSeconds")]
  public void RefusesToStartWithoutADeadline(string missing)
  {
    var settings = Settings(30, 5);
    settings.Remove(missing);

    var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

    Should.Throw<ArgumentException>(() => new ServiceCollection().AddQualificationEngines(configuration));
  }

  [Theory]
  [InlineData("zéro virgule cinq")]
  [InlineData("0")]
  [InlineData("-1")]
  public void RefusesADeadlineThatIsNotAStrictlyPositiveDuration(string raw)
  {
    var settings = Settings(30, 5);
    settings["Qualification:LlmDeadlineSeconds"] = raw;

    var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

    Should.Throw<ArgumentException>(() => new ServiceCollection().AddQualificationEngines(configuration));
  }

  /// <summary>Le nom du client ordinaire qui sert de témoin : n'importe qui, sauf un moteur.</summary>
  private const string OrdinaryClient = "un-client-comme-un-autre";

  private static IQualificationEngine Engine(IServiceProvider services, string role)
  {
    return services.GetRequiredKeyedService<IQualificationEngine>(role);
  }

  private static Dictionary<string, string?> Settings(double llm, double lexicon)
  {
    return new Dictionary<string, string?>
    {
      ["Qualification:SidecarBaseAddress"] = "http://qualification-sidecar",
      ["Qualification:LlmDeadlineSeconds"] = llm.ToString(System.Globalization.CultureInfo.InvariantCulture),
      ["Qualification:LexiconDeadlineSeconds"] = lexicon.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };
  }

  /// <summary>
  /// L'application telle que les réglages du dépôt la construisent — pipeline standard sur tout
  /// client — puis les deux moteurs par-dessus, exactement comme en production.
  /// </summary>
  private static ServiceProvider Registered(
    HttpMessageHandler sidecar,
    double llmDeadlineSeconds = 30,
    double lexiconDeadlineSeconds = 5)
  {
    var configuration = new ConfigurationBuilder()
      .AddInMemoryCollection(Settings(llmDeadlineSeconds, lexiconDeadlineSeconds))
      .Build();

    var services = new ServiceCollection();

    services.ConfigureHttpClientDefaults(http => http.AddStandardResilienceHandler(standard =>
    {
      // Le seul écart avec la production, et il ne porte que sur le temps d'attente entre deux
      // reprises : leur nombre reste celui d'usine, et c'est lui qu'on regarde.
      standard.Retry.Delay = TimeSpan.FromMilliseconds(1);
      standard.Retry.UseJitter = false;
    }));

    services.AddQualificationEngines(configuration);
    services.AddHttpClient(OrdinaryClient);

    // La doublure se pose *sous* les pipelines, là où vit le vrai transport : c'est ce qui permet de
    // compter ce que chaque pipeline laisse passer.
    services.AddHttpClient<LlmQualificationEngine>().ConfigurePrimaryHttpMessageHandler(() => sidecar);
    services.AddHttpClient<LexiconQualificationEngine>().ConfigurePrimaryHttpMessageHandler(() => sidecar);
    services.AddHttpClient(OrdinaryClient).ConfigurePrimaryHttpMessageHandler(() => sidecar);

    return services.BuildServiceProvider();
  }

  /// <summary>Un sidecar qui répond toujours la même chose, et compte combien de fois on le dérange.</summary>
  private sealed class CountingSidecar(HttpStatusCode status, string body) : HttpMessageHandler
  {
    private int _calls;

    public int Calls => Volatile.Read(ref _calls);

    protected override Task<HttpResponseMessage> SendAsync(
      HttpRequestMessage request,
      CancellationToken cancellationToken)
    {
      Interlocked.Increment(ref _calls);

      return Task.FromResult(new HttpResponseMessage(status)
      {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
      });
    }
  }

  /// <summary>Un sidecar qui ne répondra jamais assez tôt — le seul moyen d'observer une échéance.</summary>
  private sealed class SlowSidecar(string body) : HttpMessageHandler
  {
    protected override async Task<HttpResponseMessage> SendAsync(
      HttpRequestMessage request,
      CancellationToken cancellationToken)
    {
      await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

      return new HttpResponseMessage(HttpStatusCode.OK)
      {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
      };
    }
  }
}
