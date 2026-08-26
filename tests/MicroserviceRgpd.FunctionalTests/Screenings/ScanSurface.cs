using System.Net;
using System.Text.RegularExpressions;
using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.FunctionalTests.Screenings;

/// <summary>
/// De quoi lancer un scan et suivre son écran d'attente par leur <b>seule frontière HTTP</b> —
/// exactement ce que fait un navigateur.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Aucun <c>ScanProgress</c> n'est fabriqué à la main.</b> Ce qu'on éprouve est très
/// exactement qu'un formulaire posté fait partir un scan, qu'il survit à la requête, et que
/// l'écran d'attente en rend compte : poser l'avancement dans le conteneur aurait sauté les trois.
/// </para>
/// <para>
/// ⚠️ <b>Les redirections ne sont pas suivies.</b> Le <c>303</c> de la fin <b>est</b> ce qui est
/// éprouvé, et un client qui le suivrait rendrait le rapport sans jamais montrer le code.
/// </para>
/// <para>
/// <b>Un seul scan par déploiement, et la fabrique est partagée</b> : chaque test attend la fin du
/// sien avant de rendre la main, faute de quoi le suivant se ferait refuser par un scan qui n'est
/// pas le sien.
/// </para>
/// </remarks>
internal sealed class ScanSurface(CustomWebApplicationFactory<Program> factory)
{
  /// <summary>L'écran de la voie connectée.</summary>
  internal const string Connection = "/detection/connexion";

  /// <summary>La racine des écrans d'attente ; l'identité du scan la complète.</summary>
  internal const string Scan = "/detection/scan";

  /// <summary>Une chaîne de connexion reconnaissable, qu'aucun journal ne doit contenir.</summary>
  /// <remarks>
  /// ⚠️ <b>Elle porte un mot de passe qui n'est <i>que</i> dans elle</b> : chercher « Host=… » dans
  /// les journaux aurait pu tomber sur la chaîne du conteneur de test, et le canari aurait rougi
  /// pour une fuite qui n'est pas celle qu'il garde.
  /// </remarks>
  internal const string ASecret =
    "Host=galette.exemple;Database=galette_prod;Username=lecteur;Password=canari-308-ne-doit-pas-fuiter";

  private readonly HttpClient _client = factory.CreateClient(
    new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

  internal HttpClient Client => _client;

  /// <summary>Un second client, qui est un second <c>Operator</c> : même service, autre session.</summary>
  internal HttpClient AnotherOperator => factory.CreateClient(
    new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

  /// <summary>L'adresse de l'écran d'attente d'un scan.</summary>
  internal static string WaitingFor(string scanId)
  {
    return $"{Scan}/{scanId}";
  }

  /// <summary>Lance un scan par le formulaire de la connexion, sans suivre sa redirection.</summary>
  internal async Task<HttpResponseMessage> ConnectAsync(
    string connectionString = ASecret,
    string dialect = "postgresql")
  {
    var fields = new List<KeyValuePair<string, string>>
    {
      new("__RequestVerificationToken", await TokenAsync()),
      new("Dialect", dialect),
      new("ConnectionString", connectionString),
    };

    return await _client.PostAsync(Connection, new FormUrlEncodedContent(fields));
  }

  /// <summary>
  /// Lance un scan et rend l'identité que la redirection nomme — c'est-à-dire l'adresse où
  /// l'<c>Operator</c> arrive.
  /// </summary>
  internal async Task<string> LaunchAsync(
    string connectionString = ASecret,
    string dialect = "postgresql")
  {
    var launched = await ConnectAsync(connectionString, dialect);

    launched.StatusCode.ShouldBe(
      HttpStatusCode.Redirect,
      "Un lancement valide mène à l'écran d'attente ; rendre la page ici ferait d'un rechargement "
      + "un second scan sur la base du client.");

    var address = launched.Headers.Location!.OriginalString;
    var scanId = Regex.Match(address, @"/detection/scan/([0-9a-fA-F-]{36})");

    scanId.Success.ShouldBeTrue(
      $"La redirection du lancement ne nomme aucun scan : « {address} ».");

    return scanId.Groups[1].Value;
  }

  /// <summary>Ce que l'écran d'attente rend, tel qu'un <c>Operator</c> le lit.</summary>
  internal async Task<string> WaitingScreenAsync(string scanId, HttpClient? asRead = null)
  {
    var response = await (asRead ?? _client).GetAsync(WaitingFor(scanId));

    response.StatusCode.ShouldBe(HttpStatusCode.OK);

    return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
  }

  /// <summary>
  /// Attend que l'écran d'attente cède la place, et rend sa réponse — le <c>303</c> du succès, ou
  /// l'écran de la fin sans rapport.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>On interroge l'écran, on ne dort pas.</b> Le scan court sur un autre fil : une attente
  /// d'une durée écrite en dur aurait été soit trop courte sur une machine chargée, soit une
  /// seconde perdue à chaque test.
  /// </remarks>
  internal async Task<HttpResponseMessage> UntilItEndsAsync(string scanId)
  {
    var address = WaitingFor(scanId);

    for (var attempt = 0; attempt < 200; attempt++)
    {
      var response = await _client.GetAsync(address);

      if (response.StatusCode != HttpStatusCode.OK)
      {
        return response;
      }

      var rendered = await response.Content.ReadAsStringAsync();

      if (!rendered.Contains("http-equiv=\"refresh\"", StringComparison.OrdinalIgnoreCase))
      {
        // L'écran ne se rafraîchit plus : le scan a fini sans produire de rapport, et c'est une fin.
        return response;
      }

      await Task.Delay(25);
    }

    throw new InvalidOperationException(
      $"Le scan {scanId} n'a pas fini : l'écran d'attente se rafraîchit encore.");
  }

  /// <summary>
  /// Fait courir un scan jusqu'au bout et rend l'identité du rapport que le déploiement porte
  /// désormais — le chemin heureux, en un appel.
  /// </summary>
  internal async Task<HttpResponseMessage> ScanAsync(ScanOutcome outcome)
  {
    factory.Scanner.Reset();
    factory.Scanner.Outcome = outcome;

    var scanId = await LaunchAsync();

    return await UntilItEndsAsync(scanId);
  }

  private async Task<string> TokenAsync()
  {
    var rendered = await _client.GetStringAsync(Connection);
    var token = Regex.Match(
      rendered,
      @"<input name=""__RequestVerificationToken""[^>]*value=""([^""]+)""");

    token.Success.ShouldBeTrue($"Le formulaire de {Connection} ne porte aucun jeton anti-rejeu.");

    return token.Groups[1].Value;
  }
}
