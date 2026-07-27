using MicroserviceRgpd.Web.Hello;

namespace MicroserviceRgpd.FunctionalTests.ApiEndpoints;

[Collection("Sequential")]
public class HelloGet(CustomWebApplicationFactory<Program> factory) : IClassFixture<CustomWebApplicationFactory<Program>>
{
  private readonly HttpClient _client = factory.CreateClient();

  [Fact]
  public async Task ReturnsHelloWorld()
  {
    var result = await _client.GetAndDeserializeAsync<HelloResponse>("/hello");

    result.Message.ShouldBe("Hello world");
  }
}
