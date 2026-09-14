using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MicroserviceRgpd.TestDoubles.Ollama;

/// <summary>
/// Ollama réduit à ce que le moteur A2 en voit : <c>/api/tags</c>, qui déclare l'encodeur servi, et
/// <c>/api/embed</c>, qui rend un vecteur par texte. Posé <b>sur le fil HTTP</b>, sous le client que
/// le câblage réel construit.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Aucun test ne parle à un vrai Ollama.</b> Un test qui exigerait un encodeur tiré et servi ne
/// tournerait que sur le poste de qui l'a écrit, et un test qui ne tourne pas ment.
/// </para>
/// <para>
/// <b>Les vecteurs sont ceux du jeu figé <see cref="A2Equivalence"/></b>, dont les scores ont été
/// calculés en Python par <c>a2_model.py</c>. Un texte que le jeu ne connaît pas reçoit le vecteur
/// par défaut du jeu — une colonne sous le seuil —, si bien qu'un relevé quelconque se détecte sans
/// que chaque test ait à fabriquer ses vecteurs.
/// </para>
/// </remarks>
public sealed class OllamaDouble : HttpMessageHandler
{
  private readonly List<string> _embedBodies = [];

  private readonly Lock _gate = new();

  /// <summary>Les corps envoyés à <c>/api/embed</c>, dans l'ordre d'arrivée.</summary>
  public IReadOnlyList<string> EmbedBodies
  {
    get
    {
      lock (_gate)
      {
        return [.. _embedBodies];
      }
    }
  }

  /// <summary>Oublie les appels reçus — pour un hôte partagé entre plusieurs tests.</summary>
  public void Forget()
  {
    lock (_gate)
    {
      _embedBodies.Clear();
    }
  }

  protected override async Task<HttpResponseMessage> SendAsync(
    HttpRequestMessage request,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    return request.RequestUri!.AbsolutePath switch
    {
      "/api/tags" when request.Method == HttpMethod.Get => Json(Tags()),
      "/api/embed" when request.Method == HttpMethod.Post => Json(
        Embed(await request.Content!.ReadAsStringAsync(cancellationToken))),
      _ => new HttpResponseMessage(HttpStatusCode.NotFound),
    };
  }

  private static JsonObject Tags()
  {
    return new JsonObject
    {
      ["models"] = new JsonArray(new JsonObject
      {
        ["name"] = A2Equivalence.EncoderTag,
        ["model"] = A2Equivalence.EncoderTag,
        ["digest"] = A2Equivalence.EncoderDigest,
      }),
    };
  }

  private JsonObject Embed(string body)
  {
    lock (_gate)
    {
      _embedBodies.Add(body);
    }

    using var request = JsonDocument.Parse(body);
    var embeddings = new JsonArray();

    foreach (var text in request.RootElement.GetProperty("input").EnumerateArray())
    {
      embeddings.Add(new JsonArray([.. A2Equivalence.VectorOf(text.GetString()!).Select(x => (JsonNode?)x)]));
    }

    return new JsonObject { ["model"] = A2Equivalence.EncoderTag, ["embeddings"] = embeddings };
  }

  private static HttpResponseMessage Json(JsonObject body)
  {
    return new HttpResponseMessage(HttpStatusCode.OK)
    {
      Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
    };
  }
}
