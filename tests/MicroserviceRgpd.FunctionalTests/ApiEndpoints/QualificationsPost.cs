using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MicroserviceRgpd.Core.Qualifications;
using MicroserviceRgpd.UseCases.Qualifications.Qualify;

namespace MicroserviceRgpd.FunctionalTests.ApiEndpoints;

/// <summary>
/// Le contrat public de <c>POST /qualifications</c>, exercé de bout en bout — moteur substitué,
/// sidecar jamais appelé.
/// <para>
/// Ce que ces tests gardent n'est pas la qualité de la qualification, explicitement hors périmètre :
/// c'est la <b>forme</b> de ce qu'un appelant reçoit, et la promesse qu'elle ne changera pas sans
/// que quelqu'un l'ait décidé.
/// </para>
/// </summary>
[Collection(WebCollection.Name)]
public class QualificationsPost
{
  private readonly CustomWebApplicationFactory<Program> factory;
  private readonly HttpClient _client;

  public QualificationsPost(CustomWebApplicationFactory<Program> factory)
  {
    this.factory = factory;
    _client = factory.CreateClient();

    // xUnit construit la classe pour chaque test ; la fabrique, elle, est partagee par toute la
    // collection. La doublure repart donc d un etat connu, plutot que de celui du test precedent.
    factory.Witness.Reset();
  }

  [Fact]
  public async Task RendersTheQualificationInTheSameExchange()
  {
    factory.Witness.Verdict = Qualification.Of([DataSubjectRight.Access, DataSubjectRight.Erasure]);

    var body = await QualifyAsync(new { text = "Je veux une copie de mes données puis leur suppression." });

    body.GetProperty("qualificationId").GetGuid().ShouldNotBe(Guid.Empty);
    Rights(body).ShouldBe(["Access", "Erasure"], ignoreOrder: true);
  }

  /// <summary>
  /// Les deux axes sont <b>deux champs distincts, toujours présents</b> : un client les type sans
  /// point d'interrogation, et ne perd jamais « le service était-il entier ? » en lisant « avec
  /// quelle attention relire ? ».
  /// </summary>
  [Fact]
  public async Task AlwaysCarriesBothTheReviewSignalAndTheDegradationFlagAsDistinctFields()
  {
    var body = await QualifyAsync(new { text = "Supprimez mes données." });

    body.TryGetProperty("reviewSignal", out _).ShouldBeTrue();
    body.TryGetProperty("degraded", out _).ShouldBeTrue();
    body.GetProperty("degraded").ValueKind.ShouldBeOneOf(JsonValueKind.True, JsonValueKind.False);
  }

  /// <summary>
  /// Avec le seul lexique, le service est en permanence diminué : il le dit, force « à relire », et
  /// se tait. Lui fabriquer une justification serait mentir à l'opérateur au moment précis où il
  /// aurait le plus besoin de lire quelque chose.
  /// </summary>
  [Fact]
  public async Task SaysItIsDegradedAsksForReviewAndJustifiesNothing()
  {
    var body = await QualifyAsync(new { text = "Supprimez mes données." });

    body.GetProperty("degraded").GetBoolean().ShouldBeTrue();
    body.GetProperty("reviewSignal").GetString().ShouldBe("NeedsReview");
    body.TryGetProperty("justification", out _).ShouldBeFalse();
  }

  /// <summary>Aucun mode dégradé ne peut se présenter comme corroboré — contrainte de contrat.</summary>
  [Fact]
  public async Task NeverPresentsADegradedVerdictAsCorroborated()
  {
    var body = await QualifyAsync(new { text = "Je m'oppose à la prospection." });

    body.GetProperty("reviewSignal").GetString().ShouldNotBe("Corroborated");
  }

  [Fact]
  public async Task NeverRendersAnEmptySetOfRights()
  {
    factory.Witness.Verdict = Qualification.OutOfScope;

    var body = await QualifyAsync(new { text = "Bonjour, il fait beau." });

    Rights(body).ShouldBe(["OutOfScope"]);
  }

  /// <summary>
  /// Un seul caractère est un texte valide, et un emoji reçoit un verdict — « hors périmètre » —
  /// jamais un refus. Rejeter un texte parce qu'il est court, ce serait confondre « je n'y
  /// reconnais aucun droit » avec « ta requête est malformée ».
  /// </summary>
  [Theory]
  [InlineData("🙂")]
  [InlineData("?")]
  public async Task AcceptsAndQualifiesTheShortestTextThereIs(string text)
  {
    factory.Witness.Verdict = Qualification.OutOfScope;

    var body = await QualifyAsync(new { text });

    Rights(body).ShouldBe(["OutOfScope"]);
    factory.Witness.ReceivedText!.Value.Value.ShouldBe(text);
  }

  [Fact]
  public async Task AcceptsATextSittingExactlyOnTheCeiling()
  {
    var body = await QualifyAsync(new { text = new string('a', RightsRequestText.MaxLength) });

    body.GetProperty("qualificationId").GetGuid().ShouldNotBe(Guid.Empty);
  }

  [Fact]
  public async Task EchoesTheCallerReferenceVerbatimWithoutEverInterpretingIt()
  {
    const string Reference = "DSAR-8871/résumé:2";

    var body = await QualifyAsync(new { text = "Supprimez mes données.", callerReference = Reference });

    body.GetProperty("callerReference").GetString().ShouldBe(Reference);
  }

  /// <summary>Une référence absente est <b>absente</b> de la réponse, jamais présente et nulle.</summary>
  [Fact]
  public async Task OmitsTheCallerReferenceWhenNoneWasGiven()
  {
    var body = await QualifyAsync(new { text = "Supprimez mes données." });

    body.TryGetProperty("callerReference", out _).ShouldBeFalse();
  }

  /// <summary>
  /// Vide une fois nettoyée <b>vaut absente</b> : rendre une chaîne vide ferait croire à l'appelant
  /// qu'il a fourni quelque chose.
  /// </summary>
  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  public async Task TreatsACallerReferenceEmptyOnceTrimmedAsNoneAtAll(string reference)
  {
    var body = await QualifyAsync(new { text = "Supprimez mes données.", callerReference = reference });

    body.TryGetProperty("callerReference", out _).ShouldBeFalse();
  }

  /// <summary>
  /// Les bordures sont nettoyées, pas refusées : un retour chariot final est précisément ce que le
  /// nettoyage absorbe, et il ne doit pas être confondu avec un caractère de contrôle interdit.
  /// </summary>
  [Fact]
  public async Task CleansTheBordersOfACallerReferenceRatherThanRefusingThem()
  {
    var body = await QualifyAsync(new { text = "Supprimez mes données.", callerReference = "  DSAR-8871\n" });

    body.GetProperty("callerReference").GetString().ShouldBe("DSAR-8871");
  }

  /// <summary>
  /// La référence <b>n'est pas une clé d'idempotence</b> : deux requêtes qui la partagent sont deux
  /// qualifications, et l'API doit le montrer plutôt que de le documenter seule.
  /// </summary>
  [Fact]
  public async Task GivesTwoCallsSharingACallerReferenceTwoDistinctIdentifiers()
  {
    var payload = new { text = "Supprimez mes données.", callerReference = "DSAR-8871" };

    var first = await QualifyAsync(payload);
    var second = await QualifyAsync(payload);

    second.GetProperty("qualificationId").GetGuid()
      .ShouldNotBe(first.GetProperty("qualificationId").GetGuid());
  }

  [Fact]
  public async Task RefusesACallerReferenceBeyondTheCeiling()
  {
    var response = await PostAsync(new
    {
      text = "Supprimez mes données.",
      callerReference = new string('r', QualifyCommand.MaxCallerReferenceLength + 1),
    });

    await ShouldBeProblemDetailsAsync(response, HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task RefusesACallerReferenceCarryingAControlCharacter()
  {
    // La cloche ASCII, au milieu de la reference : elle survit au nettoyage des bordures,
    // et une reponse JSON n'est pas l'endroit ou l'echapper pour l'appelant.
    var response = await PostAsync(new
    {
      text = "Supprimez mes donnees.",
      callerReference = "DSAR-" + (char)7 + "8871",
    });

    await ShouldBeProblemDetailsAsync(response, HttpStatusCode.BadRequest);
  }

  /// <summary>Un texte absent, ou vide une fois ses bordures nettoyées, n'est pas une demande.</summary>
  [Theory]
  [InlineData("""{"callerReference":"DSAR-8871"}""")]
  [InlineData("""{"text":null}""")]
  [InlineData("""{"text":""}""")]
  [InlineData("""{"text":"   \t \n  "}""")]
  public async Task RefusesATextThatIsAbsentOrEmptyOnceTrimmed(string payload)
  {
    var response = await _client.PostAsync(
      "/qualifications",
      new StringContent(payload, Encoding.UTF8, "application/json"));

    await ShouldBeProblemDetailsAsync(response, HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task RefusesATextBeyondTheCeiling()
  {
    var response = await PostAsync(new { text = new string('a', RightsRequestText.MaxLength + 1) });

    await ShouldBeProblemDetailsAsync(response, HttpStatusCode.BadRequest);
  }

  /// <summary>
  /// Un corps que le service ne sait pas lire est refusé <b>dans la même forme</b> que le reste —
  /// sans quoi l'appelant aurait deux formes d'erreur à lire selon l'endroit où il s'est trompé —
  /// et sans qu'aucun moteur ne soit dérangé.
  /// </summary>
  [Fact]
  public async Task RefusesAMalformedBodyInTheSameShapeAsEverythingElse()
  {
    var response = await _client.PostAsync(
      "/qualifications",
      new StringContent("""{"text": "Supprimez""", Encoding.UTF8, "application/json"));

    await ShouldBeProblemDetailsAsync(response, HttpStatusCode.BadRequest);
    factory.Witness.CallCount.ShouldBe(0);
  }

  /// <summary>
  /// Il n'existe aucun <c>GET</c> sur la ressource : la qualification n'est pas une ressource qu'on
  /// relit, et la trace d'audit n'est atteignable par aucun appelant.
  /// </summary>
  [Fact]
  public async Task OffersNoWayToReadAQualificationBack()
  {
    var response = await _client.GetAsync("/qualifications");

    response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
  }

  private async Task<JsonElement> QualifyAsync(object payload)
  {
    var response = await PostAsync(payload);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);

    return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
  }

  private Task<HttpResponseMessage> PostAsync(object payload)
  {
    return _client.PostAsJsonAsync("/qualifications", payload);
  }

  private static string[] Rights(JsonElement body)
  {
    return [.. body.GetProperty("rights").EnumerateArray().Select(right => right.GetString()!)];
  }

  /// <summary>
  /// Aucune qualification n'a eu lieu : il n'y a rien à référencer dans l'audit, et l'identité qui
  /// vaut est celle du diagnostic.
  /// </summary>
  private static async Task ShouldBeProblemDetailsAsync(HttpResponseMessage response, HttpStatusCode expected)
  {
    response.StatusCode.ShouldBe(expected);
    response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

    using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    document.RootElement.GetProperty("status").GetInt32().ShouldBe((int)expected);
    document.RootElement.GetProperty("title").GetString().ShouldNotBeNullOrWhiteSpace();
    document.RootElement.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
  }
}
