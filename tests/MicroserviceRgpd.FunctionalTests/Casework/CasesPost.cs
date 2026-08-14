using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Infrastructure.Data;
using MicroserviceRgpd.Infrastructure.Data.Casework;
using Microsoft.EntityFrameworkCore;

namespace MicroserviceRgpd.FunctionalTests.Casework;

/// <summary>
/// Le contrat public de <c>POST /cases</c>, exercé de bout en bout jusqu au vrai PostgreSQL.
/// <para>
/// Ce que ces tests gardent est la <b>forme</b> de la porte d entree — et surtout ce qu elle ne
/// porte pas : aucun emplacement pour une piece jointe, aucun champ d identite declaree, et un seul
/// dossier quels que soient les droits.
/// </para>
/// </summary>
[Collection(WebCollection.Name)]
public class CasesPost
{
  private readonly CustomWebApplicationFactory<Program> _factory;
  private readonly HttpClient _client;

  public CasesPost(CustomWebApplicationFactory<Program> factory)
  {
    _factory = factory;
    _client = factory.CreateClient();
  }

  /// <summary>
  /// Une demande entre, un dossier nait, et il est <b>deja relisible</b> quand la reponse arrive :
  /// c est ce qui fait de l identifiant rendu autre chose qu une promesse.
  /// </summary>
  [Fact]
  public async Task OpensACaseThatIsAlreadyReadableWhenTheAnswerArrives()
  {
    var body = await OpenAsync(new
    {
      designations = new[]
      {
        new { kind = "email", value = "jean.dupont@example.fr" },
        new { kind = "name", value = "Jean Dupont" },
      },
      rights = new[] { "Access" },
    });

    var caseId = body.GetProperty("caseId").GetGuid();

    caseId.ShouldNotBe(Guid.Empty);

    using var scope = _factory.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    var opened = await dbContext.Cases
      .AsNoTracking()
      .SingleAsync(one => one.Id == CaseId.From(caseId));

    opened.IdentityDeclaration.ShouldBe(IdentityDeclaration.ApplicationSession);
    opened.Designations.Count.ShouldBe(2);
    opened.Claims.Count.ShouldBe(1);
  }

  /// <summary>
  /// <b>Le dossier nait en portant <c>ApplicationSession</c> sans verification supplementaire de l
  /// appelant.</b> C est l application qui a authentifie la session ; le service enregistre cette
  /// declaration et n en juge jamais la valeur — et aucun champ de la requete ne permet d en
  /// declarer une autre.
  /// </summary>
  [Fact]
  public async Task CarriesApplicationSessionWithoutCheckingTheCallerAnyFurther()
  {
    // La valeur est envoyee quand meme : elle doit rester sans effet, faute de quoi un appelant
    // ferait enregistrer au service un faux qu il ne verifie pas.
    var body = await OpenAsync(new
    {
      designations = new[] { new { kind = "email", value = "sans.effet@example.fr" } },
      rights = new[] { "Access" },
      identityDeclaration = "ChannelControl",
    });

    var caseId = body.GetProperty("caseId").GetGuid();

    using var scope = _factory.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    var opened = await dbContext.Cases.AsNoTracking().SingleAsync(one => one.Id == CaseId.From(caseId));

    opened.IdentityDeclaration.ShouldBe(IdentityDeclaration.ApplicationSession);
  }

  /// <summary>
  /// <b>Une demande portant plusieurs droits donne un seul dossier.</b> La regle « lire avant d
  /// effacer » traverse les <c>Claim</c>, et aucune frontiere plus fine ne pourrait la tenir.
  /// </summary>
  [Fact]
  public async Task OpensASingleCaseWhenThreeRightsAreSent()
  {
    var body = await OpenAsync(new
    {
      designations = new[] { new { kind = "email", value = "trois.droits@example.fr" } },
      rights = new[] { "Erasure", "Access", "Portability" },
    });

    // Les droits ressortent dans l ordre de la taxonomie, et non dans celui de la saisie.
    body.GetProperty("claimedRights").EnumerateArray().Select(right => right.GetString())
      .ShouldBe(["Access", "Erasure", "Portability"]);

    using var scope = _factory.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    var opened = await dbContext.Cases
      .AsNoTracking()
      .SingleAsync(one => one.Id == CaseId.From(body.GetProperty("caseId").GetGuid()));

    opened.Claims.Count.ShouldBe(3);
  }

  /// <summary>
  /// <b>La premiere ligne du <c>EvidenceLog</c> est ecrite, et elle ne porte aucune designation</b> —
  /// seulement leur nombre. Anonyme par construction, des la premiere ligne.
  /// </summary>
  [Fact]
  public async Task WritesAFirstEvidenceLogLineThatCountsTheDesignationsAndNamesNone()
  {
    var body = await OpenAsync(new
    {
      designations = new[]
      {
        new { kind = "email", value = "au.ledger@example.fr" },
        new { kind = "reference", value = "1203" },
      },
      rights = new[] { "Access" },
    });

    var caseId = body.GetProperty("caseId").GetGuid();

    using var scope = _factory.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // `Set<T>()` : le contexte n expose aucun `DbSet` du EvidenceLog, pour que `Remove` et `Update` ne
    // soient a portee de personne.
    var line = await dbContext.Set<EvidenceLogRow>()
      .AsNoTracking()
      .SingleAsync(row => row.CaseId == caseId);

    line.Fact.ShouldBe("CaseOpened");
    line.DesignationCount.ShouldBe(2);
    line.IdentityDeclaration.ShouldBe("ApplicationSession");

    // Aucun humain n a signe : l application a appelé, et la ligne le dit plutot que de le taire.
    line.SignatoryKind.ShouldBe("Application");
    line.SignatoryName.ShouldBeNull();
  }

  /// <summary>
  /// L identifiant natif de l application entre comme <b>une designation de nature
  /// <c>reference</c></b>, sans champ a lui et sans autorite particuliere.
  /// </summary>
  [Fact]
  public async Task LetsTheApplicationsOwnIdentifierInAsOneDesignationAmongOthers()
  {
    var body = await OpenAsync(new
    {
      designations = new[] { new { kind = "reference", value = "clients.id=4412" } },
      rights = new[] { "Access" },
    });

    using var scope = _factory.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    var opened = await dbContext.Cases
      .AsNoTracking()
      .SingleAsync(one => one.Id == CaseId.From(body.GetProperty("caseId").GetGuid()));

    opened.Designations.Single().Kind.ShouldBe(DesignationKind.Reference);
  }

  /// <summary>
  /// Une demande dont on ne reconnait encore aucun droit <b>entre quand meme</b> : le compteur de l
  /// art. 12.3 court, et un vestibule ou elle attendrait le laisserait courir hors du service.
  /// </summary>
  [Fact]
  public async Task OpensACaseThatClaimsNoRightYet()
  {
    var body = await OpenAsync(new
    {
      designations = new[] { new { kind = "email", value = "aucun.droit@example.fr" } },
      rights = Array.Empty<string>(),
    });

    body.GetProperty("claimedRights").GetArrayLength().ShouldBe(0);
    body.GetProperty("caseId").GetGuid().ShouldNotBe(Guid.Empty);
  }

  /// <summary>
  /// <c>OutOfScope</c> est le verdict qu aucun droit n a ete reconnu, jamais un droit qu on reclame.
  /// Le refus est <b>nomme</b> a l appelant, sous le nom du champ fautif.
  /// </summary>
  [Fact]
  public async Task RefusesToClaimTheVerdictThatNoRightWasRecognised()
  {
    var response = await PostAsync(new
    {
      designations = new[] { new { kind = "email", value = "hors.perimetre@example.fr" } },
      rights = new[] { "OutOfScope" },
    });

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

    var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    problem.GetRawText().ShouldContain("OutOfScope");
  }

  /// <summary>Un mot que le vocabulaire ferme ignore est nomme a l appelant, jamais devine.</summary>
  [Theory]
  [InlineData("subject_id")]
  [InlineData("Email")]
  public async Task RefusesADesignationKindTheContractNeverFixed(string kind)
  {
    var response = await PostAsync(new
    {
      designations = new[] { new { kind, value = "peu importe" } },
      rights = new[] { "Access" },
    });

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  /// <summary>Un droit hors taxonomie est refuse : la taxonomie est fermee par decision.</summary>
  [Fact]
  public async Task RefusesARightThatIsNotInTheClosedTaxonomy()
  {
    var response = await PostAsync(new
    {
      designations = new[] { new { kind = "email", value = "droit.invente@example.fr" } },
      rights = new[] { "Deletion" },
    });

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  /// <summary>
  /// ⚠️ <b>Aucun emplacement pour une piece jointe n existe sur ce canal.</b> Aucune piece d
  /// identite n entre dans le service, tous canaux confondus (CEPD § 79) : un champ envoye n a
  /// aucun effet, et le multipart n est pas une forme que cette route accepte.
  /// </summary>
  [Fact]
  public async Task OffersNoPlaceWhereAnIdentityDocumentCouldEverEnter()
  {
    // Aucune propriete du contrat d entree ne pourrait porter un fichier, sous quelque nom que ce
    // soit : la regle tient par la forme du type plutot que par ce que l endpoint ignore.
    typeof(Web.Casework.OpenCaseRequest).GetProperties()
      .Select(property => property.Name.ToLowerInvariant())
      .ShouldNotContain(name =>
        name.Contains("attach") || name.Contains("file") || name.Contains("document")
        || name.Contains("upload") || name.Contains("proof"));

    using var multipart = new MultipartFormDataContent
    {
      { new StringContent("[]"), "designations" },
      { new ByteArrayContent(Encoding.UTF8.GetBytes("une piece d identite")), "attachment", "cni.jpg" },
    };

    var response = await _client.PostAsync("/cases", multipart);

    response.StatusCode.ShouldNotBe(HttpStatusCode.Created);
  }

  private async Task<JsonElement> OpenAsync(object request)
  {
    var response = await PostAsync(request);

    response.StatusCode.ShouldBe(HttpStatusCode.Created);

    // Aucun `Location` : aucune route publique ne relit un dossier, et un en-tete qui promettrait
    // une lecture qu aucune route n offre serait un mensonge de contrat.
    response.Headers.Location.ShouldBeNull();

    return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
  }

  private async Task<HttpResponseMessage> PostAsync(object request)
  {
    return await _client.PostAsJsonAsync("/cases", request);
  }
}
