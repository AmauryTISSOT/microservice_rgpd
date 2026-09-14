using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MicroserviceRgpd.TestDoubles.HostSystem;

/// <summary>
/// Un <b>système hôte factice</b>, qui écoute sur un port réel que l'OS attribue. Son adresse se pose
/// dans le Paramétrage, et c'est le <b>vrai</b> client du service qui l'appelle (ADR-0026).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Rien n'est remplacé dans le conteneur du service.</b> Une doublure du port prouverait la
/// présence de cette doublure ; ici, la méthode, l'en-tête, le corps sérialisé, le délai et les
/// redirections non suivies sont ceux que le système hôte d'un client verrait.
/// </para>
/// <para>
/// <b>Il enregistre chaque requête reçue</b>, et répond ce qu'on lui demande : un code
/// (<see cref="Answer"/>), après un retard (<see cref="Delay"/>), ou une redirection
/// (<see cref="RedirectTo"/>). Par défaut, il répond 200 tout de suite.
/// </para>
/// </remarks>
public sealed class HostSystemDouble : IAsyncDisposable
{
  private readonly List<HostSystemReception> _received = [];

  private readonly Lock _gate = new();

  private readonly WebApplication _app;

  private int _statusCode = StatusCodes.Status200OK;

  private TimeSpan _delay = TimeSpan.Zero;

  private string? _location;

  private HostSystemDouble(WebApplication app)
  {
    _app = app;
    _app.Run(ReceiveAsync);
  }

  /// <summary>L'adresse à laquelle il écoute, sans chemin.</summary>
  public Uri BaseAddress { get; private set; } = null!;

  /// <summary>Les requêtes reçues, dans l'ordre d'arrivée.</summary>
  public IReadOnlyList<HostSystemReception> Received
  {
    get
    {
      lock (_gate)
      {
        return [.. _received];
      }
    }
  }

  /// <summary>Démarre un système hôte factice sur un port libre.</summary>
  public static async Task<HostSystemDouble> StartAsync()
  {
    var builder = WebApplication.CreateSlimBuilder();
    builder.Logging.ClearProviders();

    // Le port 0 laisse l'OS choisir : deux suites qui tournent ensemble ne se disputent aucun port.
    builder.WebHost.UseUrls("http://127.0.0.1:0");

    var host = new HostSystemDouble(builder.Build());
    await host._app.StartAsync();

    var addresses = host._app.Services.GetRequiredService<IServer>().Features.GetRequiredFeature<IServerAddressesFeature>();
    host.BaseAddress = new Uri(addresses.Addresses.Single());

    return host;
  }

  /// <summary>L'adresse absolue de <paramref name="pathAndQuery"/> sur ce système hôte.</summary>
  public string AddressOf(string pathAndQuery) => new Uri(BaseAddress, pathAndQuery).ToString();

  /// <summary>Répond désormais <paramref name="statusCode"/>, sans corps.</summary>
  public void Answer(int statusCode)
  {
    lock (_gate)
    {
      _statusCode = statusCode;
      _location = null;
    }
  }

  /// <summary>Attend <paramref name="delay"/> avant de répondre.</summary>
  public void Delay(TimeSpan delay)
  {
    lock (_gate)
    {
      _delay = delay;
    }
  }

  /// <summary>Répond désormais <c>302 Found</c> vers <paramref name="location"/>.</summary>
  public void RedirectTo(string location)
  {
    lock (_gate)
    {
      _statusCode = StatusCodes.Status302Found;
      _location = location;
    }
  }

  /// <summary>Oublie les requêtes reçues, et répond de nouveau 200 tout de suite — pour un hôte partagé.</summary>
  public void Forget()
  {
    lock (_gate)
    {
      _received.Clear();
      _statusCode = StatusCodes.Status200OK;
      _delay = TimeSpan.Zero;
      _location = null;
    }
  }

  public async ValueTask DisposeAsync()
  {
    await _app.StopAsync();
    await _app.DisposeAsync();
  }

  private async Task ReceiveAsync(HttpContext context)
  {
    using var reader = new StreamReader(context.Request.Body);
    var body = await reader.ReadToEndAsync(context.RequestAborted);

    int statusCode;
    TimeSpan delay;
    string? location;

    lock (_gate)
    {
      _received.Add(new HostSystemReception(
        context.Request.Method,
        $"{context.Request.Path}{context.Request.QueryString}",
        context.Request.ContentType,
        context.Request.Headers.Authorization.ToString(),
        body));

      statusCode = _statusCode;
      delay = _delay;
      location = _location;
    }

    if (delay > TimeSpan.Zero)
    {
      await Task.Delay(delay, context.RequestAborted);
    }

    context.Response.StatusCode = statusCode;

    if (location is not null)
    {
      context.Response.Headers.Location = location;
    }
  }
}

/// <summary>Une requête reçue par le <see cref="HostSystemDouble"/>, telle qu'elle est arrivée.</summary>
/// <param name="Method">La méthode HTTP.</param>
/// <param name="PathAndQuery">Le chemin et la query string.</param>
/// <param name="ContentType">L'en-tête <c>Content-Type</c>, ou <c>null</c>.</param>
/// <param name="Authorization">L'en-tête <c>Authorization</c>, vide s'il est absent.</param>
/// <param name="Body">Le corps, en texte.</param>
public sealed record HostSystemReception(
  string Method,
  string PathAndQuery,
  string? ContentType,
  string Authorization,
  string Body);
