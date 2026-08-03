using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MicroserviceRgpd.Core.Qualifications;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MicroserviceRgpd.FunctionalTests.ApiEndpoints;

/// <summary>
/// Le service qualifie <b>sans moteur de verdict</b>, exerce de bout en bout jusqu au vrai
/// PostgreSQL. C est la promesse « sans GPU » gardee par le comportement, et non par une intention :
/// le role <c>Verdict</c> n est pourvu par rien, et l hote de ce test ne lui pose aucune doublure.
/// <para>
/// Ce qui est verifie est la <b>forme</b> de ce qu un appelant recoit — inchangee, sans champ ni
/// valeur nouvelle — et la ligne que l acte laisse derriere lui.
/// </para>
/// </summary>
[Collection(LlmOffWebCollection.Name)]
public class QualificationsPostWithoutLlm
{
  private readonly LlmOffWebApplicationFactory _factory;
  private readonly HttpClient _client;

  public QualificationsPostWithoutLlm(LlmOffWebApplicationFactory factory)
  {
    _factory = factory;
    _client = factory.CreateClient();

    factory.Witness.Reset();
    factory.AuditTrail.Reset();
  }

  /// <summary>
  /// L hote demarre bel et bien <b>eteint</b> : le role de verdict n est pourvu par rien dans le
  /// conteneur reel. Sans cette verification, tout ce qui suit passerait aussi bien contre un moteur
  /// allume mais injoignable — un LLM tombe rend la meme forme de reponse qu un LLM absent.
  /// </summary>
  [Fact]
  public void ProvidesNothingAtAllForTheVerdictRole()
  {
    using var scope = _factory.Services.CreateScope();

    scope.ServiceProvider.GetKeyedService<IQualificationEngine>(QualificationEngineRole.Verdict)
      .ShouldBeNull();
  }

  /// <summary>
  /// Le service repond, et c est l avis du lexique qui tient lieu de verdict. Aucune regle nouvelle
  /// n a ete ecrite pour cela : c est exactement le chemin que le domaine emprunte quand le moteur
  /// principal tombe.
  /// </summary>
  [Fact]
  public async Task RendersTheVerdictOfTheLexiconAloneMarkedDegraded()
  {
    _factory.Witness.Qualification = Qualification.Of([DataSubjectRight.Erasure]);

    var body = await QualifyAsync();

    body.GetProperty("rights").EnumerateArray().Select(right => right.GetString()).ShouldBe(["Erasure"]);
    body.GetProperty("degraded").GetBoolean().ShouldBeTrue();
    body.GetProperty("reviewSignal").GetString().ShouldBe("NeedsReview");
  }

  /// <summary>
  /// <b>Aucune justification, et surtout aucune phrase fabriquee</b> : le champ est absent. Le
  /// lexique ne justifie rien, et le service n invente pas pour lui — un texte que personne n a
  /// ecrit ne doit jamais atteindre un humain.
  /// </summary>
  [Fact]
  public async Task CarriesNoJustificationAtAllRatherThanAFabricatedOne()
  {
    _factory.Witness.Qualification = Qualification.Of([DataSubjectRight.Erasure]);

    var body = await QualifyAsync();

    body.TryGetProperty("justification", out _).ShouldBeFalse();
  }

  /// <summary>
  /// La forme rendue est celle d aujourd hui : les memes champs, ni plus ni moins. Une extinction du
  /// moteur ne casse aucune integration.
  /// </summary>
  [Fact]
  public async Task RendersTheSameShapeAsAServiceThatHasItsTwoEngines()
  {
    _factory.Witness.Qualification = Qualification.Of([DataSubjectRight.Access]);

    var body = await QualifyAsync(callerReference: "DSAR-4412");

    body.EnumerateObject().Select(field => field.Name)
      .ShouldBe(["qualificationId", "callerReference", "rights", "reviewSignal", "degraded"], ignoreOrder: true);
  }

  /// <summary>
  /// <b><c>Corroborated</c> est inatteignable</b> : aucun controle independant n a eu lieu, et aucun
  /// verdict ne doit se presenter comme corrobore. Le lexique le plus assure du monde n y change
  /// rien — il ne se corrobore pas lui-meme.
  /// </summary>
  [Fact]
  public async Task NeverPresentsAVerdictAsCorroborated()
  {
    _factory.Witness.Qualification = Qualification.Of([DataSubjectRight.Erasure]);
    _factory.Witness.DeclaredConfidence = DeclaredConfidence.High;

    var body = await QualifyAsync();

    body.GetProperty("reviewSignal").GetString().ShouldBe("NeedsReview");
    body.GetProperty("degraded").GetBoolean().ShouldBeTrue();
  }

  /// <summary>
  /// La ligne ecrite est celle d un moteur tombe : avis de verdict nul, latence nulle, et l avis du
  /// lexique conserve avec l identite du moteur qui l a rendu. <b>Aucune colonne nouvelle</b> ne les
  /// distingue, et la limite est assumee.
  /// </summary>
  [Fact]
  public async Task WritesARowCarryingTheLexiconOpinionAndNoVerdictAtAll()
  {
    _factory.Witness.Qualification = Qualification.Of([DataSubjectRight.Erasure]);
    _factory.Witness.Engine = new QualificationEngineIdentity("lexicon", "1.0.0");

    var body = await QualifyAsync(callerReference: "DSAR-4412");
    var qualificationId = body.GetProperty("qualificationId").GetGuid();

    using var scope = _factory.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    var row = await dbContext.QualificationAuditEntries
      .AsNoTracking()
      .SingleAsync(entry => entry.QualificationId == qualificationId);

    row.Rights.ShouldBe(["Erasure"]);
    row.ReviewSignal.ShouldBe("NeedsReview");
    row.CallerReference.ShouldBe("DSAR-4412");

    row.VerdictRights.ShouldBeNull();
    row.VerdictEngineName.ShouldBeNull();
    row.VerdictLatencyMs.ShouldBeNull();

    row.WitnessRights.ShouldBe(["Erasure"]);
    row.WitnessEngineName.ShouldBe("lexicon");
    row.WitnessEngineVersion.ShouldBe("1.0.0");
  }

  /// <summary>
  /// Le lexique est le seul moteur qui reste : muet, il ne reste rien a qualifier, et le service
  /// rend une panne plutot qu un verdict que personne n a prononce.
  /// </summary>
  [Fact]
  public async Task RendersAFailureWhenTheLexiconItselfHasNothingToSay()
  {
    _factory.Witness.Silence = new QualificationEngineFailure("Le moteur lexical a repondu 500.");

    var response = await _client.PostAsJsonAsync("/qualifications", new { text = "Supprimez mes donnees." });

    response.IsSuccessStatusCode.ShouldBeFalse();
  }

  private async Task<JsonElement> QualifyAsync(string? callerReference = null)
  {
    var response = await _client.PostAsJsonAsync(
      "/qualifications",
      new { text = "Supprimez mes donnees.", callerReference });

    response.StatusCode.ShouldBe(HttpStatusCode.OK);

    return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
  }
}
