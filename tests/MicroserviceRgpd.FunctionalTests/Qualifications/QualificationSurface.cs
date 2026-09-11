using System.Net;
using System.Text.RegularExpressions;

namespace MicroserviceRgpd.FunctionalTests.Qualifications;

/// <summary>
/// De quoi coller un texte et lire le verdict par leur <b>seule frontière HTTP</b> — exactement ce
/// que fait un navigateur, et le seul chemin humain vers une qualification.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Le geste passe par le formulaire de l'écran</b>, jeton anti-rejeu compris : aucun
/// <c>PageModel</c> n'est instancié à la main, et aucune couture nouvelle n'est ouverte. C'est le
/// même montage que les harnais de la détection et du tableau des demandes.
/// </para>
/// <para>
/// ⚠️ <b>Les redirections ne sont pas suivies</b>, parce que c'est <b>l'absence</b> de redirection
/// qu'on éprouve : le <c>POST</c> rend la page portant le verdict, et une redirection ici voudrait
/// dire qu'une adresse de relecture existe — celle que ce contexte a refusée.
/// </para>
/// <para>
/// <b>Les moteurs sont doublés par la fabrique partagée</b>, sur le port de qualification : c'est
/// ainsi que les avis se dictent, comme le font déjà les tests de l'endpoint de qualification.
/// </para>
/// </remarks>
internal sealed class QualificationSurface(CustomWebApplicationFactory<Program> factory)
{
  /// <summary>L'écran, et le seul chemin par lequel un humain qualifie un texte.</summary>
  internal const string Screen = "/qualification";

  private readonly HttpClient _client = factory.CreateClient(
    new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

  /// <summary>
  /// La réponse brute d'une adresse, servie ou non — de quoi constater qu'une adresse <b>n'existe
  /// pas</b>, ce que <see cref="ReadAsync"/> ne saurait pas faire puisqu'il exige un 200.
  /// </summary>
  internal async Task<HttpResponseMessage> FetchAsync(string address)
  {
    return await _client.GetAsync(address);
  }

  /// <summary>
  /// Colle un texte et rend la réponse telle quelle — de quoi lire un statut et un en-tête.
  /// </summary>
  /// <param name="text">
  /// Le texte saisi, ou <c>null</c> pour poster le formulaire <b>sans le champ</b> — ce que fait un
  /// navigateur d'une zone de texte vide.
  /// </param>
  internal async Task<HttpResponseMessage> SubmitAsync(string? text)
  {
    var fields = new List<KeyValuePair<string, string>>
    {
      new("__RequestVerificationToken", await AntiforgeryTokenAsync()),
    };

    if (text is not null)
    {
      fields.Add(new KeyValuePair<string, string>("Text", text));
    }

    return await _client.PostAsync(Screen, new FormUrlEncodedContent(fields));
  }

  /// <summary>
  /// Colle un texte, exige que l'écran l'ait rendu <b>sans rediriger</b>, et rend la page telle
  /// qu'un <c>Operator</c> la lit — entités HTML résolues.
  /// </summary>
  /// <remarks>
  /// Le rendu est <b>décodé</b> parce que Razor encode l'apostrophe en <c>&amp;#x27;</c> : chercher
  /// « droit à l'effacement » dans le HTML brut échouerait sur un libellé pourtant présent, et le
  /// test se serait mis à parler d'encodage plutôt que de verdict.
  /// </remarks>
  internal async Task<string> QualifyAndReadAsync(string? text)
  {
    var qualified = await SubmitAsync(text);

    qualified.StatusCode.ShouldBe(
      HttpStatusCode.OK,
      "Le verdict se lit dans le même échange : l'écran ne redirige pas, et n'ouvre aucune adresse "
      + "de relecture.");

    qualified.Headers.Location.ShouldBeNull(
      "Un Location ferait de la trace d'audit une ressource métier exposée.");

    return WebUtility.HtmlDecode(await qualified.Content.ReadAsStringAsync());
  }

  /// <summary>Le corps d'une adresse qui doit répondre, entités résolues.</summary>
  internal async Task<string> ReadAsync(string address = Screen)
  {
    var response = await _client.GetAsync(address);

    response.StatusCode.ShouldBe(HttpStatusCode.OK, $"L'écran {address} doit se rendre.");

    return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
  }

  /// <summary>
  /// Ce que <b>l'écran lui-même</b> rend, le layout partagé retiré : c'est lui qu'on éprouve, et le
  /// panneau qui l'entoure appartient au harnais du layout.
  /// </summary>
  internal static string MainOf(string rendered)
  {
    var main = Regex.Match(rendered, "<main[^>]*>(.*?)</main>", RegexOptions.Singleline);

    main.Success.ShouldBeTrue("L'écran doit être rendu dans le main du layout partagé.");

    return main.Groups[1].Value;
  }

  /// <summary>
  /// Ce que le <b>dépliant</b> porte : le contenu de l'élément natif sous lequel l'écran range les
  /// internes des moteurs, et rien de la page qui l'entoure.
  /// </summary>
  /// <remarks>
  /// Le lire séparément est ce qui donne leur sens aux gardes : « les deux avis paraissent » ne dit
  /// rien tant qu'on ignore s'ils paraissent <b>sous le dépliant</b> ou en plein milieu du verdict,
  /// où l'ADR-0011 refuse qu'ils soient.
  /// <para>
  /// ⚠️ <b>L'écran seul, le layout retiré</b>, pour la même raison que <see cref="MainOf"/> : le
  /// jour où le panneau partagé se replierait par un dépliant plutôt que par sa case à cocher, ce
  /// harnais se serait mis à lire la navigation en croyant lire les avis.
  /// </para>
  /// </remarks>
  internal static string FoldOf(string rendered)
  {
    var fold = Regex.Match(
      MainOf(rendered), "<details[^>]*>(.*?)</details>", RegexOptions.Singleline);

    fold.Success.ShouldBeTrue(
      "L'écran doit porter un dépliant natif : c'est lui qui range les internes des moteurs sous le "
      + "verdict.");

    return fold.Groups[1].Value;
  }

  /// <summary>
  /// L'identifiant que l'écran met sous les yeux de l'<c>Operator</c>, lu <b>sur la page</b> — le
  /// seul endroit où il paraisse, faute d'adresse qui le porterait.
  /// </summary>
  internal static string IdentifierIn(string rendered)
  {
    var identifier = Regex.Match(
      rendered, @"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}");

    identifier.Success.ShouldBeTrue(
      "L'écran ne montre aucun identifiant de qualification : le verdict rendu ne serait alors "
      + "rattachable à aucune trace d'audit.");

    return identifier.Value;
  }

  private async Task<string> AntiforgeryTokenAsync()
  {
    var rendered = await ReadAsync();

    var token = Regex.Match(
      rendered, @"<input name=""__RequestVerificationToken""[^>]*value=""([^""]+)""");

    token.Success.ShouldBeTrue($"Le formulaire de {Screen} ne porte aucun jeton anti-rejeu.");

    return token.Groups[1].Value;
  }
}
