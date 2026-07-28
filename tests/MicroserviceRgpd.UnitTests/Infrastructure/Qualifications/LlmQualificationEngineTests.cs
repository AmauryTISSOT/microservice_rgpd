using System.Net;
using System.Text.Json;
using MicroserviceRgpd.Core.Qualifications;
using MicroserviceRgpd.Infrastructure.Qualifications;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Qualifications;

/// <summary>
/// L'adaptateur qui fait du moteur LLM du sidecar un moteur du domaine, exercé <b>sans réseau et
/// sans GPU</b> : un <see cref="HttpMessageHandler"/> double tient lieu de sidecar, et aucun test de
/// la suite .NET n'appelle jamais Ollama — un test qui exige un GPU ne tourne jamais, et un test qui
/// ne tourne jamais ment.
/// <para>
/// Il rejoint le lexique <b>derrière le même port</b> et sous la même promesse : un avis, ou rien.
/// Ce qu'il a de plus — une confiance déclarée, une justification — fait partie de l'avis, et son
/// absence est donc une panne du moteur, jamais un avis affaibli.
/// </para>
/// </summary>
public class LlmQualificationEngineTests
{
  private const string ValidOpinion = """
    {"rights":["Erasure"],"confidence":"High","justification":"Le texte demande la suppression.",
     "engine":{"name":"llm","version":"qwen3:8b+prompt.1"}}
    """;

  private static readonly RightsRequestText Text =
    RightsRequestText.From("Supprimez toutes les données que vous avez sur moi.");

  [Fact]
  public async Task AsksTheLlmEndpointForTheTextAndNothingElse()
  {
    var sidecar = RespondingWith(ValidOpinion);

    await Engine(sidecar).QualifyAsync(Text, CancellationToken.None);

    sidecar.LastRequest!.Method.ShouldBe(HttpMethod.Post);
    sidecar.LastRequest.RequestUri!.AbsolutePath.ShouldBe("/opinions/llm");

    using var body = JsonDocument.Parse(sidecar.LastRequestBody!);
    body.RootElement.GetProperty("text").GetString().ShouldBe(Text.Value);
    body.RootElement.EnumerateObject().Count().ShouldBe(1);
  }

  [Fact]
  public async Task ReadsTheCanonicalNamesBackIntoTheTaxonomy()
  {
    var sidecar = RespondingWith("""
      {"rights":["Access","Portability"],"confidence":"Medium","justification":"Copie et transfert."}
      """);

    var opinion = await Engine(sidecar).QualifyAsync(Text, CancellationToken.None);

    opinion.Qualification.ShouldBe(Qualification.Of([DataSubjectRight.Access, DataSubjectRight.Portability]));
  }

  /// <summary>
  /// Les deux choses que ce moteur a de plus que le lexique arrivent jusqu'au domaine : la règle de
  /// corroboration lit l'une, l'opérateur humain lit l'autre.
  /// </summary>
  [Theory]
  [InlineData("High", DeclaredConfidence.High)]
  [InlineData("Medium", DeclaredConfidence.Medium)]
  [InlineData("Low", DeclaredConfidence.Low)]
  public async Task CarriesTheDeclaredConfidenceAndTheJustificationBackToTheDomain(
    string wire,
    DeclaredConfidence expected)
  {
    var sidecar = RespondingWith($$"""
      {"rights":["Erasure"],"confidence":"{{wire}}","justification":"Le texte demande la suppression."}
      """);

    var opinion = await Engine(sidecar).QualifyAsync(Text, CancellationToken.None);

    opinion.DeclaredConfidence.ShouldBe(expected);
    opinion.Justification.ShouldBe("Le texte demande la suppression.");
  }

  [Theory]
  [InlineData(HttpStatusCode.BadRequest)]
  [InlineData(HttpStatusCode.BadGateway)]
  [InlineData(HttpStatusCode.ServiceUnavailable)]
  [InlineData(HttpStatusCode.GatewayTimeout)]
  public async Task TreatsAnyNonSuccessAsAFailureOfTheEngine(HttpStatusCode status)
  {
    var sidecar = RespondingWith("""{"title":"Le serveur de modèles n'a pas rendu d'avis valide"}""", status);

    await Should.ThrowAsync<QualificationEngineFailure>(
      () => Engine(sidecar).QualifyAsync(Text, CancellationToken.None));
  }

  /// <summary>
  /// Les invariants sont re-portés de ce côté-ci de la frontière, comme pour le lexique — et deux
  /// écarts s'ajoutent, propres à ce moteur : un degré hors de l'échelle, qui ne se replie
  /// <b>jamais</b> sur « basse » puisque ce serait inventer une auto-évaluation que le modèle n'a
  /// pas rendue, et une justification absente, ce moteur ne rendant pas d'avis sans raison.
  /// </summary>
  [Theory]
  [InlineData("""{"rights":[],"confidence":"High","justification":"…"}""")]
  [InlineData("""{"rights":["OutOfScope","Erasure"],"confidence":"High","justification":"…"}""")]
  [InlineData("""{"rights":["Deletion"],"confidence":"High","justification":"…"}""")]
  [InlineData("""{"rights":null,"confidence":"High","justification":"…"}""")]
  [InlineData("""{"confidence":"High","justification":"…"}""")]
  [InlineData("""{"rights":["Erasure"],"justification":"…"}""")]
  [InlineData("""{"rights":["Erasure"],"confidence":null,"justification":"…"}""")]
  [InlineData("""{"rights":["Erasure"],"confidence":"Enorme","justification":"…"}""")]
  // Un ordinal n'est pas un degré : l'accepter ferait dépendre le contrat interne de l'ordre de
  // déclaration d'un `enum` que personne des deux côtés ne pense à tenir stable.
  [InlineData("""{"rights":["Erasure"],"confidence":2,"justification":"…"}""")]
  [InlineData("""{"rights":["Erasure"],"confidence":"High"}""")]
  [InlineData("""{"rights":["Erasure"],"confidence":"High","justification":"   "}""")]
  [InlineData("ceci n'est pas du JSON")]
  public async Task TreatsAnythingThatIsNotAValidOpinionAsAFailureOfTheEngine(string body)
  {
    var sidecar = RespondingWith(body);

    await Should.ThrowAsync<QualificationEngineFailure>(
      () => Engine(sidecar).QualifyAsync(Text, CancellationToken.None));
  }

  /// <summary>
  /// Le sidecar a renoncé à attendre son amont et le dit par un code à lui : <b>la lenteur arrive
  /// nommée</b>, plutôt que confondue avec un serveur éteint. C'est ce que l'ordre strict des deux
  /// échéances achète, et c'est de cette distinction que dépendra le code rendu à l'appelant le jour
  /// où le témoin tombera en même temps.
  /// </summary>
  [Fact]
  public async Task NamesTheDeadlineTheSidecarAlreadyGaveUpOnRatherThanACommonFailure()
  {
    var sidecar = RespondingWith(
      """{"title":"Le serveur de modèles n'a pas répondu dans l'échéance"}""",
      HttpStatusCode.GatewayTimeout);

    await Should.ThrowAsync<QualificationEngineDeadlineExceeded>(
      () => Engine(sidecar).QualifyAsync(Text, CancellationToken.None));
  }

  /// <summary>
  /// Les autres pannes de l'amont ne se déguisent pas en lenteur : un serveur éteint et un serveur
  /// lent ne se réparent pas au même endroit, et le code rendu à l'appelant suit cette distinction.
  /// </summary>
  [Theory]
  [InlineData(HttpStatusCode.BadGateway)]
  [InlineData(HttpStatusCode.ServiceUnavailable)]
  [InlineData(HttpStatusCode.InternalServerError)]
  public async Task NeverCallsAnythingButALateAnswerADeadline(HttpStatusCode status)
  {
    var sidecar = RespondingWith("""{"title":"panne"}""", status);

    var silence = await Should.ThrowAsync<QualificationEngineFailure>(
      () => Engine(sidecar).QualifyAsync(Text, CancellationToken.None));

    silence.ShouldNotBeOfType<QualificationEngineDeadlineExceeded>();
  }

  /// <summary>
  /// L'annulation de l'appelant atteint le sidecar, et ressort telle quelle : ce n'est pas une panne
  /// du moteur, c'est un appelant qui est parti — et le GPU sérialisant les appels, une génération
  /// orpheline bloquerait la file de celui qui est resté.
  /// </summary>
  [Fact]
  public async Task PropagatesTheCallersCancellationRatherThanCallingItAFailure()
  {
    using var cancellation = new CancellationTokenSource();
    await cancellation.CancelAsync();

    var sidecar = RespondingWith(ValidOpinion);

    await Should.ThrowAsync<OperationCanceledException>(
      () => Engine(sidecar).QualifyAsync(Text, cancellation.Token));
  }

  private static LlmQualificationEngine Engine(SidecarDouble sidecar)
  {
    return new LlmQualificationEngine(sidecar.Client());
  }

  private static SidecarDouble RespondingWith(string body, HttpStatusCode status = HttpStatusCode.OK)
  {
    return SidecarDouble.RespondingWith(body, status);
  }
}
