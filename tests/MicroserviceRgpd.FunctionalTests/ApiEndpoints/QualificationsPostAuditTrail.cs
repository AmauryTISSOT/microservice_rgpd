using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MicroserviceRgpd.Core.Qualifications;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MicroserviceRgpd.FunctionalTests.ApiEndpoints;

/// <summary>
/// Ce que l acte de qualifier laisse derriere lui, exerce de bout en bout jusqu au vrai PostgreSQL.
/// <para>
/// <b>L ordre est : qualifier, ecrire, repondre.</b> Ces tests le verifient par ses deux
/// consequences visibles — une ligne existe deja quand la reponse arrive, et une ecriture qui
/// echoue rend <c>500</c>, jamais <c>200</c> degrade.
/// </para>
/// </summary>
[Collection(WebCollection.Name)]
public class QualificationsPostAuditTrail
{
  private readonly CustomWebApplicationFactory<Program> _factory;
  private readonly HttpClient _client;

  public QualificationsPostAuditTrail(CustomWebApplicationFactory<Program> factory)
  {
    _factory = factory;
    _client = factory.CreateClient();

    factory.Verdict.Reset();
    factory.Witness.Reset();
    factory.AuditTrail.Reset();
  }

  /// <summary>
  /// La ligne est deja ecrite quand l appelant recoit son identifiant : c est ce qui fait de cet
  /// identifiant autre chose qu une promesse.
  /// </summary>
  [Fact]
  public async Task HasAlreadyWrittenTheTraceWhenTheAnswerArrives()
  {
    _factory.Verdict.Qualification = Qualification.Of([DataSubjectRight.Erasure]);
    _factory.Witness.Qualification = Qualification.Of([DataSubjectRight.Erasure]);

    var response = await _client.PostAsJsonAsync(
      "/qualifications",
      new { text = "Supprimez mes donnees.", callerReference = "DSAR-4412" });

    response.StatusCode.ShouldBe(HttpStatusCode.OK);

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    var qualificationId = body.GetProperty("qualificationId").GetGuid();

    using var scope = _factory.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    var row = await dbContext.QualificationAuditEntries
      .AsNoTracking()
      .SingleAsync(entry => entry.QualificationId == qualificationId);

    row.Rights.ShouldBe(["Erasure"]);
    row.CallerReference.ShouldBe("DSAR-4412");
    row.VerdictEngineName.ShouldNotBeNullOrWhiteSpace();
    row.WitnessEngineName.ShouldNotBeNullOrWhiteSpace();
  }

  /// <summary>
  /// Une base indisponible est une <b>panne du service</b>, pas une qualification degradee : elle
  /// rend <c>500</c>, et aucun identifiant creux ne circule.
  /// </summary>
  [Fact]
  public async Task RendersFiveHundredWhenTheTraceCannotBeWritten()
  {
    _factory.Verdict.Qualification = Qualification.Of([DataSubjectRight.Erasure]);
    _factory.Witness.Qualification = Qualification.Of([DataSubjectRight.Erasure]);
    _factory.AuditTrail.Refusal = new InvalidOperationException("La base est indisponible.");

    var response = await _client.PostAsJsonAsync("/qualifications", new { text = "Supprimez mes donnees." });

    response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);

    // La panne se presente dans la meme forme que les autres refus, et porte le traceId : aucune
    // qualification n a eu lieu, il n y a donc rien a referencer dans l audit.
    response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
  }

  /// <summary>
  /// Et <b>y compris quand la qualification etait degradee</b> : la degradation d un moteur ne
  /// survit pas a une panne de base, et un repli lexical dont la trace ne s ecrit pas est un
  /// <c>500</c> — jamais un <c>200</c> portant un identifiant qui ne mene nulle part.
  /// </summary>
  [Fact]
  public async Task RendersFiveHundredWhenTheTraceCannotBeWrittenEvenForADegradedQualification()
  {
    _factory.Verdict.Silence = new QualificationEngineFailure("Le moteur LLM a repondu 503.");
    _factory.Witness.Qualification = Qualification.Of([DataSubjectRight.Erasure]);
    _factory.AuditTrail.Refusal = new InvalidOperationException("La base est indisponible.");

    var response = await _client.PostAsJsonAsync("/qualifications", new { text = "Supprimez mes donnees." });

    response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
  }

  /// <summary>
  /// <b>La trace enregistre les verdicts, jamais les tentatives.</b> Les deux moteurs muets n ont
  /// rien qualifie : il n y a aucun verdict dont repondre, donc aucune ligne.
  /// </summary>
  [Fact]
  public async Task LeavesNoRowWhenNeitherEngineRenderedAnOpinion()
  {
    _factory.Verdict.Silence = new QualificationEngineFailure("Le moteur LLM a repondu 504.");
    _factory.Witness.Silence = new QualificationEngineFailure("Le moteur lexical a repondu 500.");

    var before = await CountRowsAsync();

    var response = await _client.PostAsJsonAsync("/qualifications", new { text = "Supprimez mes donnees." });

    response.IsSuccessStatusCode.ShouldBeFalse();
    (await CountRowsAsync()).ShouldBe(before);
  }

  /// <summary>
  /// <b>Aucun <c>GET</c> n expose la trace.</b> Elle n est pas un agregat metier qu on relit : c est
  /// l ecrit d un acte, et l appelant repart avec son resultat plutot qu avec une adresse.
  /// </summary>
  [Fact]
  public async Task ExposesNoWayToReadTheTraceBack()
  {
    _factory.Verdict.Qualification = Qualification.Of([DataSubjectRight.Erasure]);
    _factory.Witness.Qualification = Qualification.Of([DataSubjectRight.Erasure]);

    var response = await _client.PostAsJsonAsync("/qualifications", new { text = "Supprimez mes donnees." });

    // Ni en-tete Location — un `Location` qui ne mene nulle part est un mensonge de contrat...
    response.Headers.Location.ShouldBeNull();

    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    var qualificationId = body.GetProperty("qualificationId").GetGuid();

    // ... ni route de relecture, sous l identifiant rendu comme ailleurs.
    (await _client.GetAsync($"/qualifications/{qualificationId}")).StatusCode
      .ShouldBe(HttpStatusCode.NotFound);
    // Le chemin de qualification existe, mais pour un `POST` seul : le lire n est pas une route
    // absente par oubli, c est un verbe que la ressource n offre pas.
    (await _client.GetAsync("/qualifications")).StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
  }

  private async Task<int> CountRowsAsync()
  {
    using var scope = _factory.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    return await dbContext.QualificationAuditEntries.AsNoTracking().CountAsync();
  }
}
