using System.Net;
using System.Text;
using MicroserviceRgpd.Infrastructure.Casework.Adapters;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Casework;

/// <summary>
/// Un <c>Adapter</c> de client, écrit comme le contrat le décrit : il compare le secret, puis
/// regarde s'il sert le <c>system_id</c> demandé — ou bien il ne compare rien, et c'est là tout
/// l'objet de la sonde.
/// </summary>
/// <remarks>
/// <para>
/// <b>Il répond par requête, et non par une réponse dictée d'avance.</b> Une vérification pose
/// <b>deux</b> questions à la même porte — l'une sous un secret faux, l'autre sous le vrai — et une
/// doublure qui répondrait la même chose aux deux n'aurait rien prouvé de ce qui les distingue.
/// </para>
/// <para>
/// L'ordre des deux gardes est celui du témoin, <c>temoin/adapter_rgpd.py</c> : le secret d'abord,
/// le système ensuite. C'est ce que le contrat demande, et c'est ce qui fait qu'un <c>401</c> ne dit
/// rien du système appelé.
/// </para>
/// </remarks>
/// <param name="expects">Le secret que cet <c>Adapter</c> attend, ou <c>null</c> s'il n'en compare aucun — il est alors nu.</param>
/// <param name="serves">Les <c>system_id</c> qu'il sert.</param>
internal sealed class AnAdapterOnTheWire(string? expects, params string[] serves) : HttpMessageHandler
{
  private readonly List<HttpRequestMessage> _asked = [];
  private readonly List<string?> _bodies = [];

  /// <summary>Un <c>Adapter</c> conforme au contrat : il garde sa porte, et sert ces systèmes.</summary>
  public static AnAdapterOnTheWire Guarding(string secret, params string[] serves)
  {
    return new AnAdapterOnTheWire(secret, serves);
  }

  /// <summary>
  /// Un <c>Adapter</c> <b>nu</b> : il sert tout ce qu'on lui demande, à qui le lui demande. C'est
  /// exactement l'application qu'un déploiement croit protégée par un secret et qui ne l'a jamais
  /// comparé.
  /// </summary>
  public static AnAdapterOnTheWire Naked()
  {
    return new AnAdapterOnTheWire(expects: null);
  }

  /// <summary>Tout ce qui est parti vers l'<c>Adapter</c>. Le compte est un fait du contrat : rien ne relance.</summary>
  public IReadOnlyList<HttpRequestMessage> Asked => _asked;

  /// <summary>Les corps envoyés, lus au passage — le contenu d'une requête ne se relit pas après coup.</summary>
  public IReadOnlyList<string?> Bodies => _bodies;

  /// <summary>Le client déjà branché sur cette doublure, tel que l'implémentation le recevra.</summary>
  public HttpClient Client()
  {
    // Aucune adresse de base : celle-ci est déclarée par système dans le Manifest, et l'appel la
    // porte en entier.
    return new HttpClient(this);
  }

  protected override async Task<HttpResponseMessage> SendAsync(
    HttpRequestMessage request,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    _asked.Add(request);
    _bodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));

    return new HttpResponseMessage(StatusFor(request))
    {
      // Un corps est servi même sur un refus : le contrat le laisse libre et non lu, et une sonde
      // qui n'irait bien que face à un corps vide n'aurait pas prouvé qu'elle ne le lit jamais.
      Content = new StringContent("{\"count\":12}", Encoding.UTF8, "application/json"),
    };
  }

  private HttpStatusCode StatusFor(HttpRequestMessage request)
  {
    var presented = request.Headers.TryGetValues(AdapterWire.SecretHeader, out var values)
      ? values.FirstOrDefault()
      : null;

    if (expects is not null && !string.Equals(presented, expects, StringComparison.Ordinal))
    {
      return HttpStatusCode.Unauthorized;
    }

    if (expects is null)
    {
      // Nu : il n'a rien comparé, et il sert. Il ne refuse pas davantage un système inconnu — un
      // Adapter qui ne garde pas sa porte ne garde généralement rien d'autre.
      return HttpStatusCode.OK;
    }

    var asked = System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query)[AdapterWire.SystemParameter];

    return serves.Contains(asked, StringComparer.Ordinal) ? HttpStatusCode.OK : HttpStatusCode.NotFound;
  }
}
