using System.Net;
using System.Text;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Casework;

/// <summary>
/// L'<c>Adapter</c> du client réduit à ce que le service en voit : une réponse dictée, et ce qui
/// est réellement parti sur le fil.
/// </summary>
/// <remarks>
/// <para>
/// <b>La couture est ici, sur le fil, et pas au-dessus.</b> L'en-tête de secret, le
/// <c>system_id</c> en paramètre, le <c>202</c> et son échéance <b>sont</b> le contrat : une
/// doublure posée sur le port du domaine les cacherait au-dessus de la couture, et les tests
/// n'auraient plus prouvé que le comportement de la doublure.
/// </para>
/// <para>
/// Aucun test .NET ne lance le témoin, n'ouvre de connexion MariaDB, ni n'approche un GPU : ce
/// gestionnaire est tout l'autre bout.
/// </para>
/// </remarks>
internal sealed class AdapterDouble(HttpStatusCode status, string? body) : HttpMessageHandler
{
  /// <summary>Ce que le service a demandé, dans l'ordre. Le compte est un fait du contrat : rien ne relance.</summary>
  private readonly List<HttpRequestMessage> _asked = [];

  /// <summary>Les corps envoyés, lus au passage — le contenu d'une requête ne se relit pas après coup.</summary>
  private readonly List<string?> _bodies = [];

  /// <summary>Un <c>Adapter</c> qui répondra cela.</summary>
  public static AdapterDouble RespondingWith(HttpStatusCode status, string? body = null)
  {
    return new AdapterDouble(status, body);
  }

  /// <summary>Tout ce qui est parti vers l'<c>Adapter</c>.</summary>
  public IReadOnlyList<HttpRequestMessage> Asked => _asked;

  /// <summary>La dernière requête reçue, ou une explosion : aucun test ne se contente d'une absence.</summary>
  public HttpRequestMessage LastRequest => _asked[^1];

  /// <summary>Le dernier corps reçu.</summary>
  public string? LastBody => _bodies[^1];

  /// <summary>Le client déjà branché sur cette doublure, tel que l'implémentation le recevra.</summary>
  public HttpClient Client()
  {
    // Aucune adresse de base : celle-ci est déclarée par système dans le Manifest, et l'appel la
    // porte en entier. Un client à adresse de base ferait passer le test là où la production
    // échouerait.
    return new HttpClient(this);
  }

  protected override async Task<HttpResponseMessage> SendAsync(
    HttpRequestMessage request,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    _asked.Add(request);
    _bodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));

    var response = new HttpResponseMessage(status);

    if (body is not null)
    {
      response.Content = new StringContent(body, Encoding.UTF8, "application/json");
    }

    return response;
  }
}
