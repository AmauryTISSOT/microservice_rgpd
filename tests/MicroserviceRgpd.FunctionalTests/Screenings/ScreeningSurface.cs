using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.FunctionalTests.Screenings;

/// <summary>
/// De quoi coller un relevé et lire le rapport par leur <b>seule frontière HTTP</b> — exactement ce
/// que fait un navigateur, et le seul chemin qui existe vers un dépistage.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Aucun rapport n'est posé en base à la main.</b> Le dépôt <em>est</em> ce qu'on éprouve :
/// écrire un <c>Screening</c> par le <c>DbContext</c> aurait sauté l'ingestion, le moteur et
/// l'écriture, c'est-à-dire les trois choses que cette tranche livre.
/// </para>
/// <para>
/// ⚠️ <b>Le moteur n'est pas doublé.</b> C'est le vrai <c>IScreeningEngine</c>, avec ses lexiques
/// gelés, qui répond ici — une doublure n'aurait prouvé que le comportement de la doublure, et le
/// budget du geste comme le contenu du rapport auraient cessé de vouloir dire quoi que ce soit.
/// </para>
/// <para>
/// <b>La collection est partagée</b>, et « courant » est un calcul sur tout le déploiement : chaque
/// test dépose donc son propre relevé et lit le rapport que <b>son</b> dépôt vient de rendre
/// courant.
/// </para>
/// </remarks>
internal sealed class ScreeningSurface(CustomWebApplicationFactory<Program> factory)
{
  /// <summary>Le rapport sommaire du dépistage courant.</summary>
  internal const string Report = "/depistage";

  /// <summary>L'écran du dépôt — le seul chemin par lequel un relevé entre.</summary>
  internal const string Deposit = "/depistage/depot";

  /// <summary>
  /// L'écran d'<b>une</b> table du rapport courant. Le schéma et la table passent en paramètres de
  /// requête : un nom d'objet peut porter un point ou une barre oblique, que la base rend tels quels.
  /// </summary>
  internal const string Table = "/depistage/table";

  private static readonly DateTimeOffset GeneratedOn = new(2026, 8, 10, 9, 30, 0, TimeSpan.Zero);

  /// <summary>
  /// Les redirections ne sont pas suivies : c'est la redirection elle-même qu'on vérifie. Un dépôt
  /// qui rendrait directement sa page ferait d'un rechargement un second dépistage — et un second
  /// dépistage ne reprend aucun arbitrage du premier.
  /// </summary>
  private readonly HttpClient _client = factory.CreateClient(
    new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

  internal HttpClient Client => _client;

  /// <summary>Colle un relevé et rend la réponse du dépôt, sans suivre sa redirection.</summary>
  internal async Task<HttpResponseMessage> DepositAsync(string paste)
  {
    var fields = new List<KeyValuePair<string, string>>
    {
      new("__RequestVerificationToken", await AntiforgeryTokenOfAsync(Deposit)),
      new("Paste", paste),
    };

    return await _client.PostAsync(Deposit, new FormUrlEncodedContent(fields));
  }

  /// <summary>
  /// Colle un relevé, exige qu'il ait été <b>refusé</b>, et rend le refus tel qu'un
  /// <c>Operator</c> le lit — entités HTML résolues.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Le refus se relit sur l'écran de dépôt, il ne redirige nulle part.</b> Un refus qui
  /// mènerait ailleurs ferait perdre le collage : l'<c>Operator</c> a fait un long trajet pour le
  /// produire, et le seul geste raisonnable après un refus est de corriger devant le texte qui
  /// l'explique.
  /// <para>
  /// Le rendu est <b>décodé</b> parce que Razor encode l'apostrophe en <c>&amp;#x27;</c> : chercher
  /// « aucune colonne n'a été ingérée » dans le HTML brut échouerait sur une phrase pourtant
  /// présente, et le test se serait mis à parler d'encodage plutôt que de refus.
  /// </para>
  /// </remarks>
  internal async Task<string> DepositAndReadTheRefusalAsync(string paste)
  {
    var refused = await DepositAsync(paste);

    refused.StatusCode.ShouldBe(
      HttpStatusCode.OK,
      "Un refus se relit sur l'écran de dépôt : il ne redirige pas, et il ne rend pas une erreur nue.");

    return WebUtility.HtmlDecode(await refused.Content.ReadAsStringAsync());
  }

  /// <summary>Colle un relevé, exige que le dépôt ait réussi, et rend le rapport qui en est sorti.</summary>
  internal async Task<string> DepositAndReadTheReportAsync(string paste)
  {
    var deposited = await DepositAsync(paste);

    deposited.StatusCode.ShouldBe(
      HttpStatusCode.Redirect,
      "Le dépôt d'un relevé sincère doit mener au rapport qu'il vient de produire.");

    return await ReadAsync(Report);
  }

  internal async Task<string> ReadAsync(string address)
  {
    var response = await _client.GetAsync(address);

    response.StatusCode.ShouldBe(HttpStatusCode.OK, $"L'écran {address} doit se rendre.");

    return await response.Content.ReadAsStringAsync();
  }

  /// <summary>
  /// La ligne d'en-tête d'un collage sincère. <paramref name="format"/> se choisit pour fabriquer le
  /// seul cas de refus qui porte sur la version déclarée.
  /// </summary>
  internal static string Header(
    string dialect = "postgresql",
    string database = "galette_prod",
    string? format = null)
  {
    return $$"""
      {"format":"{{format ?? ColumnListing.FormatVersion}}","dialecte":"{{dialect}}","base":"{{database}}","genere_le":"{{GeneratedOn.ToString("O", CultureInfo.InvariantCulture)}}"}
      """;
  }

  /// <summary>La ligne de fin, et le compte qu'elle annonce.</summary>
  internal static string ClosingLine(long declaredColumnCount)
  {
    return $$"""{"fin":true,"colonnes":{{declaredColumnCount.ToString(CultureInfo.InvariantCulture)}}}""";
  }

  /// <summary>
  /// Un collage assemblé ligne à ligne, sans qu'aucune ne soit posée d'office.
  /// </summary>
  /// <remarks>
  /// ⚠️ C'est ce qui permet de fabriquer les collages <b>amputés</b> — sans en-tête, sans ligne de
  /// fin — que <see cref="Paste(string[])"/> ne saurait pas produire, puisqu'il pose les deux.
  /// </remarks>
  internal static string Lines(params string[] lines)
  {
    return string.Join('\n', lines);
  }

  /// <summary>Une ligne de colonne, dans la forme que la requête de relevé émet.</summary>
  internal static string Column(
    string column,
    string table = "adherents",
    string schema = "public",
    int position = 1,
    string? dataType = "varchar(255)",
    string? columnComment = null,
    string? tableComment = null)
  {
    return new StringBuilder("{")
      .Append(Field("schema", schema)).Append(',')
      .Append(Field("table", table)).Append(',')
      .Append(Field("colonne", column)).Append(',')
      .Append($"\"position\":{position.ToString(CultureInfo.InvariantCulture)},")
      .Append(Field("type", dataType)).Append(',')
      .Append("\"nullable\":true,")
      .Append(Field("commentaire_colonne", columnComment)).Append(',')
      .Append(Field("commentaire_table", tableComment)).Append(',')
      .Append(Field("table_referencee", null))
      .Append('}')
      .ToString();
  }

  /// <summary>Un collage entier : l'en-tête, les lignes, et la ligne de fin qui les compte.</summary>
  internal static string Paste(params string[] columns)
  {
    return Paste(columns.Length, columns);
  }

  /// <summary>
  /// Un collage dont le compte annoncé et les lignes rendues se choisissent <b>séparément</b> —
  /// c'est le seul moyen de fabriquer la troncature que la ligne de fin existe pour attraper.
  /// </summary>
  internal static string Paste(int declaredColumnCount, params string[] columns)
  {
    return Lines([Header(), .. columns, ClosingLine(declaredColumnCount)]);
  }

  private static string Field(string key, string? value)
  {
    return value is null ? $"\"{key}\":null" : $"\"{key}\":\"{value}\"";
  }

  private async Task<string> AntiforgeryTokenOfAsync(string address)
  {
    var token = Regex.Match(
      await ReadAsync(address),
      @"<input name=""__RequestVerificationToken""[^>]*value=""([^""]+)""");

    token.Success.ShouldBeTrue($"Le formulaire de {address} ne porte aucun jeton anti-rejeu.");

    return token.Groups[1].Value;
  }
}
