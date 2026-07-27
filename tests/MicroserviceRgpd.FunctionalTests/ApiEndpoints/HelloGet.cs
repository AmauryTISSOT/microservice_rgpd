using MicroserviceRgpd.Web.Hello;

namespace MicroserviceRgpd.FunctionalTests.ApiEndpoints;

[Collection(WebCollection.Name)]
public class HelloGet(CustomWebApplicationFactory<Program> factory)
{
  private readonly HttpClient _client = factory.CreateClient();

  [Fact]
  public async Task ReturnsHelloWorld()
  {
    var result = await _client.GetAndDeserializeAsync<HelloResponse>("/hello");

    result.Message.ShouldBe("Hello world");
  }
}
