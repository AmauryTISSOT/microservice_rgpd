using System.Net;
using System.Text;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Qualifications;

/// <summary>
/// Le sidecar réduit à ce qu'un adaptateur en voit : une réponse dictée, et la requête reçue.
/// </summary>
/// <remarks>
/// Partagé par les deux adaptateurs, parce que ce qu'il tient est exactement ce qu'ils ont en commun :
/// du HTTP. Chaque suite lui dicte ce que <b>son</b> moteur répond ; aucune n'a besoin d'un processus
/// Python, et aucune n'approche un GPU.
/// </remarks>
internal sealed class SidecarDouble(HttpStatusCode status, string body) : HttpMessageHandler
{
  public HttpRequestMessage? LastRequest { get; private set; }

  public string? LastRequestBody { get; private set; }

  /// <summary>Un sidecar qui répondra cela, et le client déjà branché dessus.</summary>
  public static SidecarDouble RespondingWith(string body, HttpStatusCode status = HttpStatusCode.OK)
  {
    return new SidecarDouble(status, body);
  }

  public HttpClient Client()
  {
    return new HttpClient(this) { BaseAddress = new Uri("http://qualification-sidecar") };
  }

  protected override async Task<HttpResponseMessage> SendAsync(
    HttpRequestMessage request,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    LastRequest = request;
    LastRequestBody = request.Content is null
      ? null
      : await request.Content.ReadAsStringAsync(cancellationToken);

    return new HttpResponseMessage(status)
    {
      Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };
  }
}
