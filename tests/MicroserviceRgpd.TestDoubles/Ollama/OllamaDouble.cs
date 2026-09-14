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
/// <para>
/// <b>Il sait tomber en panne</b>, par <see cref="Fault"/> : chaque panne laisse derrière elle un
/// texte d'Ollama reconnaissable — <see cref="Canary"/>, l'adresse, un digest étranger —, pour qu'un
/// test exige qu'aucun ne traverse.
/// </para>
/// </remarks>
public sealed class OllamaDouble : HttpMessageHandler
{
  /// <summary>Le fragment que toute panne jouée ici glisse dans ce qu'Ollama dit.</summary>
  public const string Canary = "ollama-canari-464";

  /// <summary>Le digest que <see cref="OllamaFault.AnotherEncoder"/> déclare servir.</summary>
  public const string ForeignDigest = "0badc0de4640f00d0badc0de4640f00d0badc0de4640f00d0badc0de4640f00d";

  private readonly List<string> _embedBodies = [];

  private readonly Lock _gate = new();

  private OllamaFault _fault;

  private TaskCompletionSource? _held;

  private int _holdAfter;

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

  /// <summary>
  /// La panne jouée. ⚠️ <b>Sur un hôte partagé, la remettre à <see cref="OllamaFault.None"/></b> —
  /// <see cref="Forget"/> le fait —, sans quoi le test suivant hériterait d'un Ollama en panne.
  /// </summary>
  public OllamaFault Fault
  {
    get
    {
      lock (_gate)
      {
        return _fault;
      }
    }

    set
    {
      lock (_gate)
      {
        _fault = value;
      }
    }
  }

  /// <summary>
  /// Retient <c>/api/embed</c> une fois <paramref name="batches"/> lots encodés, et jusqu'à
  /// <see cref="Release"/> : c'est ce qui rend observable une détection à mi-chemin.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Une retenue, et non un délai</b> : l'échéance du câblage court pendant qu'on attend, et
  /// un délai assez long pour observer l'écran serait assez long pour la faire tomber.
  /// </remarks>
  public void HoldAfterBatches(int batches)
  {
    lock (_gate)
    {
      _holdAfter = batches;
      _held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }
  }

  /// <summary>Libère les lots retenus, qui répondent tout de suite.</summary>
  public void Release()
  {
    lock (_gate)
    {
      _held?.TrySetResult();
      _held = null;
    }
  }

  /// <summary>Oublie les appels reçus, la panne jouée et la retenue — pour un hôte partagé entre plusieurs tests.</summary>
  public void Forget()
  {
    lock (_gate)
    {
      _embedBodies.Clear();
      _fault = OllamaFault.None;

      // ⚠️ La retenue est LIBÉRÉE avant d'être lâchée : un test tombé entre la retenue et la
      // libération laisserait sinon un scan garé pour toujours, et l'hôte partagé refuserait tout
      // lancement suivant.
      _held?.TrySetResult();
      _held = null;
    }
  }

  protected override async Task<HttpResponseMessage> SendAsync(
    HttpRequestMessage request,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    var fault = Fault;

    if (fault == OllamaFault.Unreachable)
    {
      // La forme de ce que SocketsHttpHandler rend vraiment : le message cite l'hôte et le port.
      throw new HttpRequestException(
        $"Connection refused ({request.RequestUri!.Authority}) — {Canary}",
        new IOException(Canary));
    }

    if (fault == OllamaFault.NeverAnswers)
    {
      await Task.Delay(Timeout.Infinite, cancellationToken);
    }

    if (fault == OllamaFault.StallsAfterHeaders)
    {
      return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new StallingStream()) };
    }

    if (request.RequestUri!.AbsolutePath == "/api/embed" && HeldNow() is { } held)
    {
      await held.WaitAsync(cancellationToken);
    }

    return request.RequestUri!.AbsolutePath switch
    {
      "/api/tags" when request.Method == HttpMethod.Get => Json(Tags(fault)),
      "/api/embed" when request.Method == HttpMethod.Post => Embedded(
        fault, await request.Content!.ReadAsStringAsync(cancellationToken)),
      _ => new HttpResponseMessage(HttpStatusCode.NotFound),
    };
  }

  /// <summary>La retenue qui s'applique au lot qui arrive, s'il vient après ceux qu'on laisse passer.</summary>
  private Task? HeldNow()
  {
    lock (_gate)
    {
      return _held is { } held && _embedBodies.Count >= _holdAfter ? held.Task : null;
    }
  }

  private static JsonObject Tags(OllamaFault fault)
  {
    return new JsonObject
    {
      ["models"] = new JsonArray(new JsonObject
      {
        ["name"] = A2Equivalence.EncoderTag,
        ["model"] = A2Equivalence.EncoderTag,
        ["digest"] = fault == OllamaFault.AnotherEncoder ? ForeignDigest : A2Equivalence.EncoderDigest,
      }),
    };
  }

  private HttpResponseMessage Embedded(OllamaFault fault, string body)
  {
    int batch;

    lock (_gate)
    {
      _embedBodies.Add(body);
      batch = _embedBodies.Count;
    }

    return fault switch
    {
      OllamaFault.MalformedEmbedding => new HttpResponseMessage(HttpStatusCode.OK)
      {
        Content = new StringContent($"<html>{Canary} : sortie tronquée", Encoding.UTF8, "application/json"),
      },
      OllamaFault.FailingEmbedding => new HttpResponseMessage(HttpStatusCode.InternalServerError)
      {
        Content = new StringContent(
          $$"""{"error":"{{Canary}} : model requires more system memory"}""", Encoding.UTF8, "application/json"),
      },
      _ => Json(Embed(body, oneShort: fault == OllamaFault.OneVectorShortOnTheSecondBatch && batch == 2)),
    };
  }

  private static JsonObject Embed(string body, bool oneShort)
  {
    using var request = JsonDocument.Parse(body);
    var embeddings = new JsonArray();

    foreach (var text in request.RootElement.GetProperty("input").EnumerateArray().Skip(oneShort ? 1 : 0))
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

  /// <summary>
  /// Un corps dont aucun octet n'arrive jamais — jusqu'à ce que quelqu'un renonce, comme le flux
  /// d'une connexion réelle : seule une lecture annulable rend la main.
  /// </summary>
  private sealed class StallingStream : Stream
  {
    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
      get => throw new NotSupportedException();
      set => throw new NotSupportedException();
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
      await Task.Delay(Timeout.Infinite, cancellationToken);

      return 0;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
      return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
      throw new NotSupportedException("Un corps qui ne vient jamais ne se lit pas de façon bloquante.");
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
  }
}
