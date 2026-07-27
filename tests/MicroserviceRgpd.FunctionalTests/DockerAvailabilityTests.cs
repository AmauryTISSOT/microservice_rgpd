namespace MicroserviceRgpd.FunctionalTests;

[Collection(WebCollection.Name)]
public class DockerAvailabilityTests(CustomWebApplicationFactory<Program> factory)
{
  [Fact]
  public void Docker_ShouldBeRunning_ForFullFunctionalTestCoverage()
  {
    Assert.True(factory.UsesPostgres,
      "Docker is not running or is misconfigured. " +
      "Functional tests fall back to SQLite, which does not catch PostgreSQL-specific issues. " +
      "For full test coverage, please start Docker Desktop (https://www.docker.com/products/docker-desktop/) and re-run the tests.");
  }
}
