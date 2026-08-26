using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.Infrastructure.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MicroserviceRgpd.FunctionalTests.Screenings;

/// <summary>
/// De quoi coller un relevé et lire le rapport par leur <b>seule frontière HTTP</b> — exactement ce
/// que fait un navigateur, et le seul chemin qui existe vers un rapport de détection.
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
  /// <summary>Le sommaire du rapport de détection courant.</summary>
  internal const string Report = "/detection";

  /// <summary>L'écran du dépôt — le seul chemin par lequel un relevé entre.</summary>
  internal const string Deposit = "/detection/depot";

  /// <summary>
  /// L'écran d'<b>une</b> table du rapport courant. Le schéma et la table passent en paramètres de
  /// requête : un nom d'objet peut porter un point ou une barre oblique, que la base rend tels quels.
  /// </summary>
  internal const string Table = "/detection/table";

  /// <summary>L'historique : ce que le déploiement a lancé, et le seul écran qui supprime.</summary>
  internal const string History = "/detection/historique";

  /// <summary>Le sommaire d'<b>un</b> rapport de détection archivé, nommé en paramètre de requête.</summary>
  internal const string Archive = "/detection/archive";

  /// <summary>Une table d'un rapport de détection archivé.</summary>
  internal const string ArchivedTable = "/detection/archive/table";

  /// <summary>
  /// La <c>Cartographie</c> du rapport courant, en JSON.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>L'adresse annonce ce qu'elle rend, et c'est tout son intérêt</b> : elle se colle dans un
  /// courriel, s'ouvre d'un clic et se met en favori. Deux routes plutôt qu'une route et un
  /// paramètre de format — sans script, un menu n'existe pas.
  /// </remarks>
  internal const string MapAsJson = "/detection/cartographie.json";

  /// <summary>La <c>Cartographie</c> du rapport courant, en CSV.</summary>
  internal const string MapAsCsv = "/detection/cartographie.csv";

  private static readonly DateTimeOffset GeneratedOn = new(2026, 8, 10, 9, 30, 0, TimeSpan.Zero);

  /// <summary>
  /// Les redirections ne sont pas suivies : c'est la redirection elle-même qu'on vérifie. Un dépôt
  /// qui rendrait directement sa page ferait d'un rechargement un second rapport de détection — et
  /// un second rapport de détection ne reprend aucun arbitrage du premier.
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
  /// Arbitre une colonne <b>par le formulaire de l'écran de sa table</b>, exactement comme un clic
  /// sur l'un des deux boutons — jeton anti-rejeu compris.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Aucune date n'est postée, et il n'y a pas de paramètre pour en poster une.</b> C'est le
  /// point que ce ticket doit garder : l'instant vient du service. Un paramètre optionnel ici
  /// aurait permis d'écrire un test vert contre un formulaire qui date les arbitrages.
  /// </remarks>
  /// <param name="signedBy">
  /// Le nom saisi, ou <c>null</c> pour poster le formulaire <b>sans le champ</b> — ce que fait un
  /// navigateur d'un champ vide, et le seul moyen d'éprouver le refus d'un arbitrage non signé.
  /// </param>
  /// <param name="alsoPosted">
  /// Des champs que le formulaire de l'écran ne porte pas — le seul moyen d'éprouver ce qu'un
  /// formulaire <b>forgé</b> obtient, et notamment qu'une date postée à la main ne date rien.
  /// </param>
  /// <param name="screening">
  /// Le rapport que l'écran rendait, ou <c>null</c> pour <b>le lire sur la page</b> comme le fait un
  /// navigateur. Le poser à la main est ce qui permet d'éprouver le clic d'un <c>Operator</c> dont
  /// l'écran a vieilli sous lui.
  /// </param>
  internal async Task<HttpResponseMessage> ArbitrateAsync(
    string column,
    string ruling,
    string table = "adherents",
    string schema = "public",
    IEnumerable<KeyValuePair<string, string>>? alsoPosted = null,
    string? screening = null)
  {
    var address = TableOf(schema, table);
    var rendered = await ReadAsync(address);

    var fields = new List<KeyValuePair<string, string>>
    {
      new("__RequestVerificationToken", TokenIn(rendered, address)),
      new("Column", column),
      new("Ruling", ruling),
      new("Screening", screening ?? ScreeningIn(rendered, address)),
    };

    if (alsoPosted is not null)
    {
      fields.AddRange(alsoPosted);
    }

    return await _client.PostAsync(address, new FormUrlEncodedContent(fields));
  }

  /// <summary>
  /// Pose le <b>geste de lot</b> sur une table <b>par le formulaire de son écran</b>, exactement
  /// comme un clic sur l'un de ses deux boutons — jeton anti-rejeu compris.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Il n'y a aucun paramètre pour désigner des colonnes, et il ne doit jamais y en avoir.</b>
  /// Le geste nomme une table ; ce qu'il atteint dedans est décidé par le domaine. Un paramètre ici
  /// aurait permis d'écrire un test vert contre un formulaire par lequel un lot écarte des colonnes
  /// signalées.
  /// </remarks>
  /// <param name="screening">
  /// Le rapport que l'écran rendait, ou <c>null</c> pour <b>le lire sur la page</b> comme le fait un
  /// navigateur.
  /// </param>
  /// <param name="renderedFrom">
  /// La table dont on lit le formulaire, quand ce n'est pas celle qu'on poste. ⚠️ <b>C'est le seul
  /// moyen d'éprouver le clic d'un <c>Operator</c> dont l'écran nomme une table qu'un second
  /// rapport de détection vient d'emporter</b> : cet écran-là ne se rend plus, et son formulaire est
  /// inatteignable.
  /// </param>
  internal async Task<HttpResponseMessage> ArbitrateInBatchAsync(
    string ruling,
    string table = "adherents",
    string schema = "public",
    string? screening = null,
    string? renderedFrom = null)
  {
    var address = TableOf(schema, table);
    var rendered = await ReadAsync(TableOf(schema, renderedFrom ?? table));

    var fields = new List<KeyValuePair<string, string>>
    {
      new("__RequestVerificationToken", TokenIn(rendered, address)),
      new("Ruling", ruling),
      new("Screening", screening ?? ScreeningIn(rendered, address)),
    };

    return await _client.PostAsync($"{address}&handler=Batch", new FormUrlEncodedContent(fields));
  }

  /// <summary>L'adresse de l'écran d'une table, ses deux membres échappés comme le fait un lien.</summary>
  internal static string TableOf(string schema = "public", string table = "adherents")
  {
    return $"{Table}?schema={Uri.EscapeDataString(schema)}&table={Uri.EscapeDataString(table)}";
  }

  /// <summary>
  /// L'<b>ancre</b> d'une colonne, telle que l'écran d'une table la pose et telle que la
  /// redirection d'un arbitrage la vise.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Elle est écrite ici en toutes lettres, et non calculée par le code de production.</b>
  /// La recalculer avec la fonction qu'elle éprouve aurait rendu vert n'importe quel changement
  /// de forme : ce qu'un test garde d'une ancre, c'est très exactement qu'elle ne bouge pas.
  /// </remarks>
  internal static string AnchorOf(string column)
  {
    return $"colonne-{column}";
  }

  /// <summary>
  /// Le <b>bloc</b> d'une colonne sur l'écran d'une table : sa <b>fiche</b> si la détection l'a
  /// signalée, sa <b>ligne</b> sinon — ou <c>null</c> si l'écran ne la porte pas.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Un seul lecteur pour les deux temps de l'écran.</b> Les signalées se lisent en fiches
  /// dépliables et les <c>Unflagged</c> en lignes : un helper par forme aurait laissé chaque
  /// fichier de tests décider tout seul de ce qu'il regarde, et une colonne passée d'un temps à
  /// l'autre serait devenue introuvable sans qu'aucun test ne rougisse.
  /// </remarks>
  internal static string? BlockOf(string table, string column)
  {
    var anchor = Regex.Escape(AnchorOf(column));

    var card = Regex.Match(
      table, $@"<details[^>]*id=""{anchor}""[^>]*>.*?</details>", RegexOptions.Singleline);

    if (card.Success)
    {
      return card.Value;
    }

    var row = Regex.Match(
      table, $@"<tr[^>]*id=""{anchor}""[^>]*>.*?</tr>", RegexOptions.Singleline);

    return row.Success ? row.Value : null;
  }

  /// <summary>
  /// Combien de colonnes l'écran d'une table rend vraiment, <b>les deux temps confondus</b> — la
  /// mesure que « celle-ci est là » ne donne pas.
  /// </summary>
  internal static int ColumnCountOf(string table)
  {
    return Regex.Matches(table, @"id=""colonne-").Count;
  }

  /// <summary>L'adresse du sommaire d'un rapport de détection archivé.</summary>
  internal static string ArchiveOf(string screening)
  {
    return $"{Archive}?screening={Uri.EscapeDataString(screening)}";
  }

  /// <summary>L'adresse d'une table d'un rapport de détection archivé.</summary>
  internal static string ArchivedTableOf(
    string screening, string schema = "public", string table = "adherents")
  {
    return $"{ArchivedTable}?screening={Uri.EscapeDataString(screening)}"
      + $"&schema={Uri.EscapeDataString(schema)}&table={Uri.EscapeDataString(table)}";
  }

  /// <summary>
  /// Le rapport que l'écran courant rendait — lu sur le formulaire d'arbitrage de la table, comme le
  /// fait un navigateur. C'est le seul moyen de retenir l'identité d'un rapport <b>avant</b> qu'un
  /// second dépôt ne l'archive.
  /// </summary>
  internal async Task<string> CurrentScreeningAsync(
    string schema = "public", string table = "adherents")
  {
    var address = TableOf(schema, table);

    return ScreeningIn(await ReadAsync(address), address);
  }

  /// <summary>
  /// Supprime un rapport de détection <b>par le formulaire de l'historique</b>, jeton anti-rejeu
  /// compris.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Le nom de base se repose en clair</b>, exactement comme l'<c>Operator</c> le retape : il
  /// n'existe aucun champ caché qui le porterait, et c'est ce qui fait de la confirmation un juge
  /// plutôt qu'une cérémonie.
  /// </remarks>
  /// <param name="confirmedDatabase">
  /// Le nom retapé, ou <c>null</c> pour poster le formulaire <b>sans le champ</b> — ce que fait un
  /// navigateur d'un champ vide.
  /// </param>
  internal async Task<HttpResponseMessage> DeleteAsync(string screening, string? confirmedDatabase)
  {
    var fields = new List<KeyValuePair<string, string>>
    {
      new("__RequestVerificationToken", await AntiforgeryTokenOfAsync(History)),
      new("Screening", screening),
    };

    if (confirmedDatabase is not null)
    {
      fields.Add(new KeyValuePair<string, string>("ConfirmedDatabase", confirmedDatabase));
    }

    return await _client.PostAsync(
      $"{History}?handler=Delete", new FormUrlEncodedContent(fields));
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

  /// <summary>
  /// Fait de ce rapport un rapport <b>scanné</b>, et pose au besoin une raison d'absence d'aperçu
  /// sur des colonnes nommées.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>C'est la seule entorse de ce fichier à sa propre règle — « aucun rapport n'est posé en
  /// base à la main » —, et elle est bornée.</b> Aucun rapport n'est posé : le dépôt HTTP produit
  /// celui-ci en entier, avec son ingestion, son moteur et son écriture. Ce qui est amendé après
  /// coup est <b>l'origine</b>, et uniquement parce qu'<b>aucun chemin d'écriture ne rend encore un
  /// relevé scanné</b> : le dépôt écrit <c>Pasted</c>, et le scan arrive plus tard dans la même
  /// livraison.
  /// </para>
  /// <para>
  /// ⚠️ <b>Sans cette entorse, le témoin de la clause n'aurait pas de niveau 2</b> — celui qui
  /// regarde l'écran —, et le rappel écrit en dur dans deux <c>.cshtml</c> n'aurait aucun test du
  /// tout. Le jour où le dépôt par connexion existe, ce levier disparaît et les témoins passent par
  /// lui.
  /// </para>
  /// </remarks>
  internal async Task MakeItScannedAsync(
    string screening, params (string Column, PreviewAbsenceReason Reason)[] withoutAPreview)
  {
    using var scope = factory.Services.CreateScope();
    var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    var origin = await database.Database.ExecuteSqlRawAsync(
      "update screenings set listing_origin = {0} where id = {1}",
      ListingOrigin.Scanned.Name,
      Guid.Parse(screening));

    origin.ShouldBe(
      1,
      "Le levier n'a atteint aucun rapport : le témoin du chemin scanné serait vert sur un rapport "
      + "resté collé, ce qui est très exactement la correspondance inversée qu'il existe pour "
      + "attraper.");

    foreach (var (column, reason) in withoutAPreview)
    {
      var touched = await database.Database.ExecuteSqlRawAsync(
        "update screened_columns set preview_absence_reason = {0} "
        + "where screening_id = {1} and column_name = {2}",
        reason.Name,
        Guid.Parse(screening),
        column);

      touched.ShouldBe(
        1,
        $"Le levier n'a atteint aucune ligne pour « {column} » : le témoin serait vert sur un "
        + "rapport que rien n'a amendé.");
    }
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
    return TokenIn(await ReadAsync(address), address);
  }

  private static string TokenIn(string rendered, string address)
  {
    var token = Regex.Match(
      rendered,
      @"<input name=""__RequestVerificationToken""[^>]*value=""([^""]+)""");

    token.Success.ShouldBeTrue($"Le formulaire de {address} ne porte aucun jeton anti-rejeu.");

    return token.Groups[1].Value;
  }

  /// <summary>
  /// Le rapport que l'écran rendait, lu sur le formulaire lui-même — comme le fait un navigateur.
  /// </summary>
  private static string ScreeningIn(string rendered, string address)
  {
    var screening = Regex.Match(rendered, @"name=""Screening"" value=""([^""]+)""");

    screening.Success.ShouldBeTrue(
      $"Le formulaire de {address} ne dit pas quel rapport il rendait : un arbitrage posté depuis "
      + "un écran vieilli atterrirait alors sur un rapport que personne n'a lu.");

    return screening.Groups[1].Value;
  }
}
