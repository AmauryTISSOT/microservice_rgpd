using System.Net;
using System.Text.Json;
using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.FunctionalTests.Requests;

/// <summary>
/// <b>Publier vers un broker qui n'est pas là</b> : le déploiement déclare une connexion, et rien
/// n'écoute derrière elle (ADR-0028).
/// </summary>
/// <remarks>
/// ⚠️ <b>Le bouton n'est pas éteint pour autant.</b> Une connexion <c>Configured</c> dit ce que le
/// déploiement déclare, jamais ce qu'il atteint : aucun écran ne teste le réseau, et c'est
/// l'exécution — et elle seule — qui découvre l'échec. La demande reste alors En cours, le journal
/// dit l'erreur réseau, et l'<c>Operator</c> peut recommencer.
/// </remarks>
[Collection(ABrokerDeclaredButUnreachableWebCollection.Name)]
public class RequestPublicationToAnUnreachableBroker(ABrokerDeclaredButUnreachableWebApplicationFactory factory)
  : IAsyncLifetime
{
  private readonly RequestSurface _surface = new(factory);

  public Task InitializeAsync() => _surface.ForgetEveryChannelAsync();

  public Task DisposeAsync() => _surface.ForgetEveryChannelAsync();

  /// <summary>
  /// <b>Un broker injoignable rend 502</b>, la demande reste En cours, et le journal retient une
  /// erreur réseau, sans statut.
  /// </summary>
  [Fact]
  public async Task AnswersBadGatewayAndLeavesTheRequestInProgressWhenTheBrokerIsUnreachable()
  {
    await _surface.RouteAsync(
      DataSubjectRight.Access,
      new RabbitMqRouting(ExchangeName.From("rgpd.exercice"), RoutingKey.From("droit.acces")));

    var (id, message) = await _surface.RecordAsync(new Dictionary<string, string>
    {
      ["email"] = $"{Guid.NewGuid():N}@example.org",
      ["identityVerified"] = "true",
    });

    var response = await _surface.ExecuteAsync(id);
    var body = await response.Content.ReadAsStringAsync();

    response.StatusCode.ShouldBe(HttpStatusCode.BadGateway, body);

    using var problem = JsonDocument.Parse(body);
    problem.RootElement.GetProperty("detail").GetString()
      .ShouldBe("Le broker est injoignable. La demande reste En cours.");
    problem.RootElement.GetProperty("retryable").GetBoolean().ShouldBeTrue();

    (await _surface.RowOfAsync(message))["status"].ShouldBe("InProgress", "La demande a changé de statut.");

    var attempt = (await _surface.AttemptsOfAsync(id))
      .ShouldHaveSingleItem("L'échec n'a pas laissé une ligne de journal, une seule.");

    attempt["outcome"].ShouldBe("NetworkError");
    attempt["http_status"].ShouldBeNull();
    attempt["exercise"].ShouldBe("exchange rgpd.exercice, routing key droit.acces");
  }
}
