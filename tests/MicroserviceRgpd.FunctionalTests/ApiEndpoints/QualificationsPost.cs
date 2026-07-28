using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MicroserviceRgpd.Core.Qualifications;
using MicroserviceRgpd.UseCases.Qualifications.Qualify;

namespace MicroserviceRgpd.FunctionalTests.ApiEndpoints;

/// <summary>
/// Le contrat public de <c>POST /qualifications</c>, exercé de bout en bout — les deux moteurs
/// substitués à la couture du port, sidecar jamais appelé, Ollama jamais approché.
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
    // collection. Les doublures repartent donc d un etat connu, plutot que de celui du test
    // precedent.
    factory.Verdict.Reset();
    factory.Witness.Reset();
  }

  [Fact]
  public async Task RendersTheQualificationInTheSameExchange()
  {
    BothEnginesSee(DataSubjectRight.Access, DataSubjectRight.Erasure);

    var body = await QualifyAsync(new { text = "Je veux une copie de mes données puis leur suppression." });

    body.GetProperty("qualificationId").GetGuid().ShouldNotBe(Guid.Empty);
    Rights(body).ShouldBe(["Access", "Erasure"], ignoreOrder: true);
  }

  /// <summary>
  /// Les deux moteurs sont appelés, et le verdict rendu est celui du moteur principal : le témoin
  /// est <b>détecteur, jamais contributeur</b> en marche nominale.
  /// </summary>
  [Fact]
  public async Task RendersTheVerdictOfThePrincipalEngineAndAsksBothForTheirOpinion()
  {
    factory.Verdict.Qualification = Qualification.Of([DataSubjectRight.Erasure]);
    factory.Witness.Qualification = Qualification.Of([DataSubjectRight.Objection]);

    var body = await QualifyAsync(new { text = "Supprimez mes données." });

    Rights(body).ShouldBe(["Erasure"]);
    factory.Verdict.CallCount.ShouldBe(1);
    factory.Witness.CallCount.ShouldBe(1);
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
  /// Le contrôle indépendant est favorable et la confiance haute : c'est tout ce que « corroboré »
  /// veut dire, et le service était entier pour le dire.
  /// </summary>
  [Fact]
  public async Task CallsAVerdictCorroboratedWhenTheIndependentControlAgreesUnderHighConfidence()
  {
    BothEnginesSee(DataSubjectRight.Erasure);

    var body = await QualifyAsync(new { text = "Supprimez mes données." });

    body.GetProperty("reviewSignal").GetString().ShouldBe("Corroborated");
    body.GetProperty("degraded").GetBoolean().ShouldBeFalse();
  }

  /// <summary>
  /// Un texte rédigé en anglais ressort contesté <b>sans qu'aucun détecteur de langue n'intervienne</b> :
  /// le désaccord des deux moteurs le signale seul. Un détecteur non mesuré rétablirait par la bande
  /// le plancher de longueur que le contrat a refusé.
  /// </summary>
  [Fact]
  public async Task LetsATextWrittenInEnglishComeOutContestedWithoutAnyLanguageDetector()
  {
    factory.Verdict.Qualification = Qualification.Of([DataSubjectRight.Erasure]);
    factory.Witness.Qualification = Qualification.OutOfScope;

    var body = await QualifyAsync(new { text = "Please delete all the data you hold about me." });

    body.GetProperty("reviewSignal").GetString().ShouldBe("Contested");
    body.GetProperty("degraded").GetBoolean().ShouldBeFalse();
    Rights(body).ShouldBe(["Erasure"]);
  }

  /// <summary>
  /// Une demande hors des sept valeurs — déréférencement, décision individuelle automatisée —
  /// retombe en hors périmètre <b>avec une justification lisible</b> : l'opérateur doit comprendre
  /// pourquoi la taxonomie fermée ne la couvre pas.
  /// </summary>
  [Theory]
  [InlineData("Je demande le déréférencement de cette page dans les résultats de recherche.")]
  [InlineData("Je conteste la décision automatisée qui a refusé mon dossier.")]
  public async Task FallsBackOnOutOfScopeWithAReadableJustificationForARightLeftOutOfTheTaxonomy(string text)
  {
    BothEnginesSee(DataSubjectRight.OutOfScope);
    factory.Verdict.Justification = "Aucun des six droits de la taxonomie n'est exercé par ce texte.";

    var body = await QualifyAsync(new { text });

    Rights(body).ShouldBe(["OutOfScope"]);
    body.GetProperty("justification").GetString()
      .ShouldBe("Aucun des six droits de la taxonomie n'est exercé par ce texte.");
  }

  /// <summary>
  /// Un verdict peu assuré parvient <b>signalé, jamais remplacé</b> : aucun seuil de confiance ne
  /// s'interpose entre le moteur et l'appelant, ni pour corriger le verdict, ni pour le refuser.
  /// </summary>
  [Theory]
  [InlineData(DeclaredConfidence.Low)]
  [InlineData(DeclaredConfidence.Medium)]
  public async Task SignalsALowlyConfidentVerdictWithoutEverReplacingIt(DeclaredConfidence confidence)
  {
    BothEnginesSee(DataSubjectRight.Erasure);
    factory.Verdict.DeclaredConfidence = confidence;

    var body = await QualifyAsync(new { text = "Supprimez mes données." });

    Rights(body).ShouldBe(["Erasure"]);
    body.GetProperty("reviewSignal").GetString().ShouldBe("NeedsReview");
  }

  /// <summary>
  /// Le repli lexical dans sa forme définitive : le moteur principal muet, le témoin produit le
  /// verdict, le signal vaut « à relire », et la réponse est <b>muette</b> — lui fabriquer une
  /// justification serait mentir à l'opérateur au moment précis où il aurait le plus besoin de lire
  /// quelque chose.
  /// </summary>
  [Fact]
  public async Task FallsBackOnTheWitnessAndStaysSilentWhenThePrincipalEngineRendersNothing()
  {
    factory.Verdict.Silence = new QualificationEngineFailure("Le moteur LLM a répondu 503.");
    factory.Witness.Qualification = Qualification.Of([DataSubjectRight.Erasure]);

    var body = await QualifyAsync(new { text = "Supprimez mes données." });

    Rights(body).ShouldBe(["Erasure"]);
    body.GetProperty("degraded").GetBoolean().ShouldBeTrue();
    body.GetProperty("reviewSignal").GetString().ShouldBe("NeedsReview");
    body.TryGetProperty("justification", out _).ShouldBeFalse();
  }

  /// <summary>
  /// Symétrie non négociable : <b>un lexique mort ne doit pas s'éteindre en silence</b>. Le verdict
  /// est normal — justification comprise —, mais il n'a reçu aucun contrôle indépendant.
  /// </summary>
  [Fact]
  public async Task RaisesTheDegradationFlagWhenItIsTheWitnessThatRendersNothing()
  {
    factory.Verdict.Qualification = Qualification.Of([DataSubjectRight.Erasure]);
    factory.Witness.Silence = new QualificationEngineFailure("Le moteur lexical a répondu 500.");

    var body = await QualifyAsync(new { text = "Supprimez mes données." });

    Rights(body).ShouldBe(["Erasure"]);
    body.GetProperty("degraded").GetBoolean().ShouldBeTrue();
    body.GetProperty("reviewSignal").GetString().ShouldBe("NeedsReview");
  }

  /// <summary>
  /// Aucun mode dégradé ne peut se présenter comme corroboré — contrainte de contrat, vérifiée
  /// <b>par les deux chemins</b> qui y mènent, et jusque sous une confiance haute.
  /// </summary>
  [Fact]
  public async Task NeverPresentsADegradedVerdictAsCorroborated()
  {
    factory.Verdict.Silence = new QualificationEngineFailure("Le moteur LLM a répondu 504.");
    var withoutThePrincipalEngine = await QualifyAsync(new { text = "Je m'oppose à la prospection." });

    factory.Verdict.Reset();
    factory.Verdict.Qualification = Qualification.Of([DataSubjectRight.Objection]);
    factory.Witness.Silence = new QualificationEngineFailure("Le moteur lexical a répondu 500.");
    var withoutTheWitness = await QualifyAsync(new { text = "Je m'oppose à la prospection." });

    withoutThePrincipalEngine.GetProperty("reviewSignal").GetString().ShouldNotBe("Corroborated");
    withoutTheWitness.GetProperty("reviewSignal").GetString().ShouldNotBe("Corroborated");
  }

  /// <summary>
  /// Trois choses circulent à l'intérieur du service et ne franchissent <b>jamais</b> cette
  /// frontière : les avis bruts de chaque moteur, la confiance déclarée, et l'identité des moteurs.
  /// Les publier graverait l'architecture dans le contrat public et inviterait l'appelant à
  /// recalculer, hors de tout test, la règle que le service tient pour lui.
  /// </summary>
  [Fact]
  public async Task NeverPublishesTheRawOpinionsTheDeclaredConfidenceOrTheEngines()
  {
    BothEnginesSee(DataSubjectRight.Erasure);

    var body = await QualifyAsync(new { text = "Supprimez mes données.", callerReference = "DSAR-8871" });

    body.EnumerateObject().Select(field => field.Name).ShouldBe(
      ["qualificationId", "callerReference", "rights", "reviewSignal", "degraded", "justification"],
      ignoreOrder: true);
  }

  [Fact]
  public async Task NeverRendersAnEmptySetOfRights()
  {
    BothEnginesSee(DataSubjectRight.OutOfScope);

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
    BothEnginesSee(DataSubjectRight.OutOfScope);

    var body = await QualifyAsync(new { text });

    Rights(body).ShouldBe(["OutOfScope"]);
    factory.Verdict.ReceivedText!.Value.Value.ShouldBe(text);
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
    factory.Verdict.CallCount.ShouldBe(0);
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

  /// <summary>Les deux moteurs voient la même chose : la marche nominale, et les moteurs d'accord.</summary>
  private void BothEnginesSee(params DataSubjectRight[] rights)
  {
    factory.Verdict.Qualification = Qualification.Of(rights);
    factory.Witness.Qualification = Qualification.Of(rights);
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
