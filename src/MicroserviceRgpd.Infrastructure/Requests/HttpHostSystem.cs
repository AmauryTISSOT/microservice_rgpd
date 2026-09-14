using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.Requests;

namespace MicroserviceRgpd.Infrastructure.Requests;

/// <summary>
/// Le système hôte joint <b>par HTTP</b> : un <c>POST</c> <c>application/json</c> synchrone à l'adresse
/// du droit, dont le corps porte exactement cinq clés (ADR-0026).
/// </summary>
/// <remarks>
/// <para>
/// Le client est le client nommé <see cref="HostSystemServiceExtensions.ClientName"/> : sans
/// résilience, sans redirection suivie, sans en-tête d'authentification, et avec pour délai
/// <c>HostSystem:TimeoutSeconds</c> sur l'appel entier — voir <see cref="HostSystemServiceExtensions"/>.
/// </para>
/// <para>
/// ⚠️ <b>Il ne juge pas la réponse</b> : il rend son code, et <see cref="HostSystemCall"/> dit ce qu'il
/// vaut. Il ne lit pas non plus son corps, que le journal ne retient pas.
/// </para>
/// </remarks>
/// <param name="clients">La fabrique du client nommé.</param>
/// <param name="clock">L'horloge du service, qui date le départ de l'appel et en mesure la durée.</param>
public sealed class HttpHostSystem(IHttpClientFactory clients, TimeProvider clock) : IHostSystem
{
  /// <summary>
  /// Les clés du corps en camelCase, et <b>les nuls écrits</b> : un prénom ou un nom absent part à
  /// <c>null</c>, pour que les cinq clés soient toujours présentes.
  /// </summary>
  private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);

  /// <inheritdoc />
  public async Task<HostSystemCall> ApplyAsync(EndpointUrl endpoint, ExecutionBody body, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(body);

    var client = clients.CreateClient(HostSystemServiceExtensions.ClientName);

    using var content = new StringContent(JsonSerializer.Serialize(WireBody.Of(body), Wire), Encoding.UTF8);
    content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

    var startedAt = clock.GetUtcNow();
    var started = clock.GetTimestamp();

    try
    {
      using var response = await client.PostAsync(new Uri(endpoint.Value, UriKind.Absolute), content, cancellationToken);

      return HostSystemCall.Answered((int)response.StatusCode, startedAt, clock.GetElapsedTime(started));
    }
    catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
    {
      // Le délai du client se dit par une annulation que personne n'a demandée.
      return HostSystemCall.TimedOut(startedAt, clock.GetElapsedTime(started));
    }
    catch (HttpRequestException)
    {
      return HostSystemCall.Unreachable(startedAt, clock.GetElapsedTime(started));
    }
  }

  /// <summary>Le corps tel qu'il part : cinq clés, le droit sous son nom canonique, l'identifiant en texte.</summary>
  private sealed record WireBody(string RequestId, string Right, string Email, string? FirstName, string? LastName)
  {
    public static WireBody Of(ExecutionBody body) => new(
      body.RequestId.Value.ToString(),
      body.Right.Name,
      body.Email.Value,
      body.FirstName?.Value,
      body.LastName?.Value);
  }
}
