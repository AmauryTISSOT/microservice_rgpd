using System.Net.Http.Headers;
using System.Text;
using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.Requests;

namespace MicroserviceRgpd.Infrastructure.Requests;

/// <summary>
/// Le système hôte joint <b>par HTTP</b> : un <c>POST</c> <c>application/json</c> synchrone à l'adresse
/// du droit, dont le corps — le même que celui d'une publication, écrit une seule fois sur
/// <see cref="ExecutionWireBody"/> — porte exactement cinq clés (ADR-0026). Il est l'un des destinataires de
/// <see cref="HostSystemByChannel"/>, et <b>ne connaît que l'adresse</b> — jamais le genre du canal.
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
public sealed class HttpHostSystem(IHttpClientFactory clients, TimeProvider clock)
{
  /// <summary>
  /// Poste à <paramref name="endpoint"/> le droit que porte <paramref name="body"/>, et rend ce que
  /// l'appel a donné. ⚠️ <b>Il ne lève pas pour un échec de l'appel</b> : une réponse non 2xx, un
  /// délai dépassé ou une erreur réseau sont des <see cref="HostSystemCall"/> comme les autres.
  /// </summary>
  public async Task<HostSystemCall> ApplyAsync(EndpointUrl endpoint, ExecutionBody body, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(body);

    var client = clients.CreateClient(HostSystemServiceExtensions.ClientName);

    using var content = new StringContent(ExecutionWireBody.Of(body), Encoding.UTF8);
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
      return HostSystemCall.TimedOut(startedAt, clock.GetElapsedTime(started), client.Timeout);
    }
    catch (HttpRequestException)
    {
      return HostSystemCall.Unreachable(startedAt, clock.GetElapsedTime(started));
    }
  }
}
