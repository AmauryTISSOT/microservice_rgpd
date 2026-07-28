using System.Net;
using System.Text.Json;

namespace MicroserviceRgpd.FunctionalTests.Platform;

/// <summary>
/// Une erreur sortie de l'API n'a qu'une seule forme : <c>application/problem+json</c> conforme
/// RFC 9457, porteuse d'un <c>traceId</c>. La route inconnue est le cas le plus pur : l'erreur
/// est produite par la plateforme, avant toute application.
/// </summary>
[Collection(WebCollection.Name)]
public class ErrorResponseFormat(CustomWebApplicationFactory<Program> factory)
{
  private readonly HttpClient _client = factory.CreateClient();

  [Fact]
  public async Task RouteInconnueSortEnProblemJsonAvecTraceId()
  {
    var response = await _client.GetAsync("/route-qui-n-existe-pas");

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

    using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var problem = document.RootElement;

    problem.GetProperty("type").GetString().ShouldNotBeNullOrWhiteSpace();
    problem.GetProperty("title").GetString().ShouldNotBeNullOrWhiteSpace();
    problem.GetProperty("status").GetInt32().ShouldBe(404);
    problem.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
  }

  [Fact]
  public async Task MethodeNonSupporteeSortDansLaMemeForme()
  {
    var response = await _client.PostAsync("/hello", content: null);

    response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
    response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

    using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    document.RootElement.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
  }
}
