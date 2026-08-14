using System.Net;
using System.Text.Json;

namespace MicroserviceRgpd.FunctionalTests.Platform;

/// <summary>
/// Une erreur sortie de l'API n'a qu'une seule forme : <c>application/problem+json</c> conforme
/// RFC 9457, porteuse d'un <c>traceId</c>. Les codes rendus par la plateforme, avant que la
/// moindre application ne soit atteinte, sont le cas le plus pur.
/// </summary>
[Collection(WebCollection.Name)]
public class ProblemDetailsErrorFormat(CustomWebApplicationFactory<Program> factory)
{
  private readonly HttpClient _client = factory.CreateClient();

  [Fact]
  public async Task UnknownRouteIsProblemDetails()
  {
    var response = await _client.GetAsync("/route-qui-n-existe-pas");

    await ShouldBeProblemDetailsAsync(response, HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task UnsupportedMethodIsTheSameShape()
  {
    var response = await _client.PostAsync(MethodNotAllowedTarget.Route, content: null);

    await ShouldBeProblemDetailsAsync(response, HttpStatusCode.MethodNotAllowed);
  }

  /// <summary>
  /// La forme unique est affirmée à un seul endroit : ajouter un cas doit être une ligne.
  /// </summary>
  private static async Task ShouldBeProblemDetailsAsync(HttpResponseMessage response, HttpStatusCode expected)
  {
    response.StatusCode.ShouldBe(expected);
    response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

    using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var problem = document.RootElement;

    problem.GetProperty("type").GetString().ShouldNotBeNullOrWhiteSpace();
    problem.GetProperty("title").GetString().ShouldNotBeNullOrWhiteSpace();
    problem.GetProperty("status").GetInt32().ShouldBe((int)expected);
    problem.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
  }
}
