using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using MicroserviceRgpd.FunctionalTests.Layout;

namespace MicroserviceRgpd.FunctionalTests.Requests;

/// <summary>
/// <b>Consulter les demandes enregistrées</b> : le tableau que le serveur rend sous le bouton
/// « Créer une demande », exercé par la <b>seule frontière HTTP</b> — <c>GET /demandes</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Le serveur rend toutes les lignes, et tous leurs libellés</b> : ce qui est gardé ici est le
/// HTML tel qu'il arrive au navigateur, avant qu'aucun script ne l'ait touché.
/// </para>
/// <para>
/// Chaque test crée ses propres demandes, <b>reconnaissables par une valeur unique</b> — un email ou
/// un nom qui porte un GUID —, et ne lit que leurs lignes : la base est partagée par toute la
/// collection, et ne se vide pas entre deux tests.
/// </para>
/// </remarks>
[Collection(WebCollection.Name)]
public class RequestConsultation(CustomWebApplicationFactory<Program> factory)
{
  /// <summary>Le message de l'état vide, recopié à dessein.</summary>
  private const string NoRequestYet = "Aucune demande pour le moment";

  private readonly RequestSurface _surface = new(factory);

  private readonly LayoutSurface _layout = new(factory);

  /// <summary>
  /// <b>Le tableau porte ses colonnes dans l'ordre</b> — Email, Nom, Prénom, Date de réception,
  /// Identité vérifiée, Type de droit, Date de création, Créé par —, puis une colonne d'actions
  /// <b>sans titre</b>.
  /// </summary>
  [Fact]
  public async Task CarriesItsColumnsInOrderThenAnUntitledActionsColumn()
  {
    var table = TableIn(await BoardAsync());

    Regex.Matches(Regex.Match(table, @"<thead\b[^>]*>(.*?)</thead>", RegexOptions.Singleline).Groups[1].Value,
        @"<th\b[^>]*>(.*?)</th>", RegexOptions.Singleline)
      .Select(heading => LayoutSurface.TextIn(heading.Groups[1].Value))
      .ShouldBe(
        ["Email", "Nom", "Prénom", "Date de réception", "Identité vérifiée", "Type de droit", "Date de création", "Créé par", ""],
        "Le tableau ne porte pas ses colonnes dans l'ordre, suivies d'une colonne d'actions sans titre.");
  }

  /// <summary>
  /// <b>Le tableau est sous le bouton « Créer une demande »</b>, dans le contenu de l'écran et hors
  /// des modales.
  /// </summary>
  [Fact]
  public async Task SitsUnderTheCreateButton()
  {
    var main = await BoardAsync();

    var button = main.IndexOf(@"id=""create-request-open""", StringComparison.Ordinal);
    var table = main.IndexOf("<table", StringComparison.Ordinal);

    button.ShouldBeGreaterThanOrEqualTo(0, "L'écran ne porte plus le bouton de création.");
    table.ShouldBeGreaterThan(button, "Le tableau n'est pas sous le bouton de création, hors des modales.");
  }

  /// <summary>
  /// <b>Une demande enregistrée se lit sur sa ligne</b>, chaque cellule sous le libellé que le
  /// serveur lui donne : la date de réception en <c>jj/mm/aaaa</c>, « Oui », le droit avec une
  /// majuscule initiale et sans article du RGPD, et « Opérateur ». La cellule d'actions se garde
  /// dans <see cref="CarriesThreeActionsOnEachRowNamedForAScreenReader"/>.
  /// </summary>
  [Fact]
  public async Task RendersARecordedRequestWithTheLabelsOfTheServer()
  {
    var marker = Guid.NewGuid().ToString("N");
    var email = $"jeanne.{marker}@example.org";

    await CreateAsync(new()
    {
      ["receivedOn"] = "2026-01-15",
      ["lastName"] = "Martin",
      ["firstName"] = "Jeanne",
      ["email"] = email,
      ["identityVerified"] = "true",
      ["right"] = "Erasure",
    });

    var row = await RowWithAsync(email);

    row[..6].ShouldBe([email, "Martin", "Jeanne", "15/01/2026", "Oui", "Droit à l'effacement"]);
    row[7].ShouldBe("Opérateur");
  }

  /// <summary>
  /// <b>Chaque ligne porte ses trois actions</b> dans sa dernière cellule — la poubelle, le crayon,
  /// l'œil —, des boutons en icônes que leur libellé accessible nomme pour qui ne les voit pas :
  /// « Supprimer la demande », « Modifier la demande », « Voir la fiche de la demande ».
  /// </summary>
  /// <remarks>
  /// Ce sont de simples boutons, qui ne soumettent rien et ne mènent nulle part : aucun formulaire
  /// ne les entoure, aucun lien ne les double. Ce qu'ils font au survol et au clic se vérifie dans
  /// un vrai navigateur.
  /// </remarks>
  [Fact]
  public async Task CarriesThreeActionsOnEachRowNamedForAScreenReader()
  {
    var first = $"{Guid.NewGuid():N}@example.org";
    var second = $"{Guid.NewGuid():N}@example.org";

    await CreateAsync(new() { ["email"] = first });
    await CreateAsync(new() { ["email"] = second });

    var main = await BoardAsync();

    foreach (var email in new[] { first, second })
    {
      var actions = Regex.Matches(RowMarkupWith(main, email), @"<td\b[^>]*>(.*?)</td>", RegexOptions.Singleline)[^1].Groups[1].Value;
      var buttons = Regex.Matches(actions, @"<button\b([^>]*)>", RegexOptions.Singleline).Select(button => button.Groups[1].Value).ToArray();

      buttons
        .Select(attributes => Regex.Match(attributes, @"aria-label=""([^""]*)""").Groups[1].Value)
        .ShouldBe(
          ["Supprimer la demande", "Modifier la demande", "Voir la fiche de la demande"],
          "La ligne ne porte pas ses trois actions, nommées dans l'ordre.");

      buttons.ShouldAllBe(
        attributes => attributes.Contains(@"type=""button""", StringComparison.Ordinal),
        "Une action de la ligne soumet quelque chose.");

      buttons
        .Select(attributes => Regex.Match(attributes, @"data-action=""([^""]*)""").Groups[1].Value)
        .ShouldBe(["delete", "edit", "view"], "Seule la poubelle doit se désigner comme la suppression.");

      actions.ShouldNotContain("<form", Case.Insensitive, "Une action de la ligne est dans un formulaire.");
      actions.ShouldNotContain("<a ", Case.Insensitive, "Une action de la ligne mène quelque part.");
    }
  }

  /// <summary>
  /// <b>Chaque ligne porte l'identifiant de sa demande et la phrase de sa suppression</b>, composée
  /// par le serveur : le prénom et le nom s'ils sont là, l'email entre parenthèses quand un nom
  /// l'accompagne, et sans parenthèses quand il est seul. Le module la recopie dans la confirmation.
  /// </summary>
  /// <remarks>
  /// <c>{m}</c> est remplacé par une valeur unique, qui fait retrouver la ligne.
  /// </remarks>
  [Theory]
  [InlineData("Martin", "Jeanne", "jeanne.{m}@example.org", "Jeanne Martin (jeanne.{m}@example.org)")]
  [InlineData("", "", "jeanne.{m}@example.org", "jeanne.{m}@example.org")]
  [InlineData("Martin-{m}", "Jeanne", "", "Jeanne Martin-{m}")]
  [InlineData("Martin", "", "jeanne.{m}@example.org", "Martin (jeanne.{m}@example.org)")]
  public async Task CarriesTheIdAndTheDeletionSentenceComposedByTheServer(
    string lastName, string firstName, string email, string whose)
  {
    var marker = Guid.NewGuid().ToString("N");
    string Marked(string value) => value.Replace("{m}", marker, StringComparison.Ordinal);

    var (id, _) = await _surface.RecordAsync(new Dictionary<string, string>
    {
      ["lastName"] = Marked(lastName),
      ["firstName"] = Marked(firstName),
      ["email"] = Marked(email),
    });

    var row = Regex.Match(RowMarkupWith(await BoardAsync(), marker), @"<tr\b([^>]*)>").Groups[1].Value;

    AttributeOf(row, "data-request-id").ShouldBe(id.ToString());
    AttributeOf(row, "data-deletion-confirmation").ShouldBe(
      $"La demande de {Marked(whose)} sera définitivement supprimée. Cette action est irréversible.");
  }

  /// <summary>
  /// <b>Une identité non vérifiée se lit « Non »</b>, et chaque droit sous son libellé, majuscule
  /// initiale comprise.
  /// </summary>
  [Theory]
  [InlineData("Access", "Droit d'accès")]
  [InlineData("Rectification", "Droit de rectification")]
  [InlineData("Restriction", "Droit à la limitation du traitement")]
  [InlineData("Portability", "Droit à la portabilité")]
  [InlineData("Objection", "Droit d'opposition")]
  public async Task RendersNoForAnUnverifiedIdentityAndTheLabelOfEachRight(string right, string label)
  {
    var email = $"{Guid.NewGuid():N}@example.org";

    await CreateAsync(new() { ["email"] = email, ["identityVerified"] = "false", ["right"] = right });

    var row = await RowWithAsync(email);

    row[4].ShouldBe("Non");
    row[5].ShouldBe(label);
  }

  /// <summary>
  /// <b>Un email, un nom ou un prénom absent se lit « — »</b> : une absence se distingue d'une
  /// cellule mal rendue.
  /// </summary>
  [Fact]
  public async Task RendersADashForAnAbsentEmailLastNameOrFirstName()
  {
    var byEmail = $"{Guid.NewGuid():N}@example.org";
    var byName = $"Martin-{Guid.NewGuid():N}";

    await CreateAsync(new() { ["email"] = byEmail, ["lastName"] = "", ["firstName"] = "" });
    await CreateAsync(new() { ["email"] = "", ["lastName"] = byName, ["firstName"] = "Jeanne" });

    var rows = RowsIn(await BoardAsync());

    rows.Where(cells => cells.Contains(byEmail)).ShouldHaveSingleItem()[..3].ShouldBe([byEmail, "—", "—"]);
    rows.Where(cells => cells.Contains(byName)).ShouldHaveSingleItem()[..3].ShouldBe(["—", byName, "Jeanne"]);
  }

  /// <summary>
  /// ⚠️ <b>La date de création se lit en <c>jj/mm/aaaa HH:mm</c>, à l'heure de Paris</b> — ni à
  /// celle de la machine, ni en UTC. L'attendu se calcule depuis l'instant relu en base, converti
  /// par le fuseau <c>Europe/Paris</c> : Paris n'est jamais à l'heure UTC, un rendu en UTC tomberait
  /// donc toujours à côté.
  /// </summary>
  [Fact]
  public async Task RendersTheCreationInstantInParisTime()
  {
    var email = $"{Guid.NewGuid():N}@example.org";
    var message = await CreateAsync(new() { ["email"] = email });

    var createdAt = (await _surface.RowOfAsync(message))["created_at"].ShouldBeOfType<DateTime>();
    var expected = TimeZoneInfo
      .ConvertTime(new DateTimeOffset(createdAt), TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris"))
      .ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    (await RowWithAsync(email))[6].ShouldBe(expected);
  }

  /// <summary>
  /// <b>Les lignes arrivent dans l'ordre par défaut</b> : la date de réception la plus récente
  /// d'abord, puis, pour deux demandes reçues le même jour, la plus récemment créée.
  /// </summary>
  /// <remarks>
  /// La demande reçue le plus tard est créée <b>entre</b> les deux autres : un tri sur la seule date
  /// de création la rendrait au milieu, un tri sur la seule date de réception laisserait le
  /// départage au hasard.
  /// </remarks>
  [Fact]
  public async Task RendersTheRowsByReceptionThenCreationMostRecentFirst()
  {
    var marker = Guid.NewGuid().ToString("N");

    await CreateAsync(new() { ["lastName"] = $"Ancienne-{marker}", ["firstName"] = "A", ["email"] = "", ["receivedOn"] = "2026-01-10" });
    await CreateAsync(new() { ["lastName"] = $"Recue-{marker}", ["firstName"] = "B", ["email"] = "", ["receivedOn"] = "2026-01-12" });
    await CreateAsync(new() { ["lastName"] = $"Recente-{marker}", ["firstName"] = "C", ["email"] = "", ["receivedOn"] = "2026-01-10" });

    RowsIn(await BoardAsync())
      .Select(cells => cells[1])
      .Where(lastName => lastName.EndsWith(marker, StringComparison.Ordinal))
      .ShouldBe([$"Recue-{marker}", $"Recente-{marker}", $"Ancienne-{marker}"]);
  }

  /// <summary>
  /// <b>Sans aucune demande, le serveur rend « Aucune demande pour le moment »</b>, et le tableau
  /// n'a aucune ligne.
  /// </summary>
  [Fact]
  public async Task SaysThereIsNoRequestYetWhenNoneIsRecorded()
  {
    await _surface.DeleteAllAsync();

    var main = await BoardAsync();

    RowsIn(main).ShouldBeEmpty();
    EmptyStateIn(main).ShouldNotContain("hidden", Case.Sensitive, "L'état vide est caché sur un tableau vide.");
  }

  /// <summary>
  /// <b>Dès qu'une demande est enregistrée, l'état vide est caché</b> : il reste rendu, pour que le
  /// script le montre après la suppression de la dernière ligne.
  /// </summary>
  [Fact]
  public async Task HidesTheEmptyStateOnceARequestIsRecorded()
  {
    await CreateAsync(new() { ["email"] = $"{Guid.NewGuid():N}@example.org" });

    Regex.IsMatch(EmptyStateIn(await BoardAsync()), @"\bhidden\b")
      .ShouldBeTrue("L'état vide se lit au-dessus de demandes enregistrées.");
  }

  /// <summary>
  /// Enregistre une demande valide, les valeurs de <paramref name="fields"/> posées par-dessus ; rend
  /// son message unique.
  /// </summary>
  private async Task<string> CreateAsync(Dictionary<string, string> fields)
  {
    var request = RequestSurface.AValidRequest();

    foreach (var (key, value) in fields)
    {
      request[key] = value;
    }

    var response = await _surface.CreateAsync(request);

    response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

    return request["message"];
  }

  /// <summary>Le contenu de l'écran, les modales retirées.</summary>
  private async Task<string> BoardAsync() =>
    Regex.Replace(
      LayoutSurface.MainOf(await _layout.ReadAsync(RequestSurface.Board)),
      @"<dialog\b.*?</dialog>",
      string.Empty,
      RegexOptions.Singleline);

  /// <summary>La ligne qui porte <paramref name="marker"/>, une seule.</summary>
  private async Task<string[]> RowWithAsync(string marker) =>
    RowsIn(await BoardAsync()).Where(cells => cells.Contains(marker)).ShouldHaveSingleItem();

  private static string TableIn(string main)
  {
    var table = Regex.Match(main, @"<table\b[^>]*>.*?</table>", RegexOptions.Singleline);

    table.Success.ShouldBeTrue("L'écran ne porte aucun tableau.");

    return table.Value;
  }

  /// <summary>Les lignes du corps du tableau, chacune comme le texte de ses cellules.</summary>
  private static IReadOnlyList<string[]> RowsIn(string main)
  {
    var body = Regex.Match(TableIn(main), @"<tbody\b[^>]*>(.*?)</tbody>", RegexOptions.Singleline);

    body.Success.ShouldBeTrue("Le tableau ne porte aucun corps.");

    return
    [
      .. Regex.Matches(body.Groups[1].Value, @"<tr\b[^>]*>(.*?)</tr>", RegexOptions.Singleline)
        .Select(row => Regex.Matches(row.Groups[1].Value, @"<t[dh]\b[^>]*>(.*?)</t[dh]>", RegexOptions.Singleline)
          .Select(cell => LayoutSurface.TextIn(cell.Groups[1].Value))
          .ToArray()),
    ];
  }

  /// <summary>La ligne qui porte <paramref name="marker"/>, une seule, telle que le serveur la rend.</summary>
  private static string RowMarkupWith(string main, string marker) =>
    Regex.Matches(TableIn(main), @"<tr\b[^>]*>.*?</tr>", RegexOptions.Singleline)
      .Where(row => row.Value.Contains(marker, StringComparison.Ordinal))
      .ShouldHaveSingleItem($"Le tableau ne porte pas la ligne de « {marker} », une fois.")
      .Value;

  /// <summary>La valeur de l'attribut <paramref name="name"/>, décodée comme le navigateur la lit.</summary>
  private static string AttributeOf(string attributes, string name)
  {
    var attribute = Regex.Match(attributes, $@"\b{Regex.Escape(name)}=""([^""]*)""");

    attribute.Success.ShouldBeTrue($"La ligne ne porte pas « {name} ».");

    return WebUtility.HtmlDecode(attribute.Groups[1].Value);
  }

  /// <summary>L'élément qui porte le message de l'état vide, une fois : ses attributs.</summary>
  private static string EmptyStateIn(string main)
  {
    var empty = Regex.Matches(main, @"<p\b(?<attributes>[^>]*)>(?<text>[^<]*)</p>")
      .Where(paragraph => LayoutSurface.TextIn(paragraph.Groups["text"].Value) == NoRequestYet)
      .ShouldHaveSingleItem($"L'écran ne porte pas « {NoRequestYet} », une fois.");

    return empty.Groups["attributes"].Value;
  }
}
