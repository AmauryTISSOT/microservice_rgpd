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
  /// Date limite de réponse, Identité vérifiée, Type de droit, Date de création, Créé par, Statut —,
  /// puis une colonne d'actions <b>sans titre</b> : onze en tout.
  /// </summary>
  [Fact]
  public async Task CarriesItsColumnsInOrderThenAnUntitledActionsColumn()
  {
    var table = TableIn(await BoardAsync());

    Regex.Matches(Regex.Match(table, @"<thead\b[^>]*>(.*?)</thead>", RegexOptions.Singleline).Groups[1].Value,
        @"<th\b[^>]*>(.*?)</th>", RegexOptions.Singleline)
      .Select(heading => LayoutSurface.TextIn(heading.Groups[1].Value))
      .ShouldBe(
        [
          "Email", "Nom", "Prénom", "Date de réception", "Date limite de réponse", "Identité vérifiée",
          "Type de droit", "Date de création", "Créé par", "Statut", "",
        ],
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
  /// serveur lui donne : les dates de réception et limite de réponse en <c>jj/mm/aaaa</c>, « Oui »,
  /// le droit avec une majuscule initiale et sans article du RGPD, « Opérateur », et le statut
  /// « En cours » d'une demande qui naît. La cellule d'actions se garde dans
  /// <see cref="CarriesThreeActionsOnEachRowNamedForAScreenReader"/>.
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

    row[..7].ShouldBe([email, "Martin", "Jeanne", "15/01/2026", "15/02/2026", "Oui", "Droit à l'effacement"]);
    row[8..10].ShouldBe(["Opérateur", "En cours"]);
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
      var actions = CellsMarkupWith(main, email)[^1];
      var buttons = Regex.Matches(actions, @"<button\b([^>]*)>", RegexOptions.Singleline).Select(button => button.Groups[1].Value).ToArray();

      buttons
        .Select(attributes => Regex.Match(attributes, @"aria-label=""([^""]*)""").Groups[1].Value)
        .ShouldBe(
          ["Supprimer la demande", "Modifier la demande", "Voir la fiche de la demande"],
          "La ligne ne porte pas ses trois actions, nommées dans l'ordre.");

      buttons.ShouldAllBe(
        attributes => attributes.Contains(@"type=""button""", StringComparison.Ordinal),
        "Une action de la ligne soumet quelque chose.");

      actions.ShouldNotContain("<form", Case.Insensitive, "Une action de la ligne est dans un formulaire.");
      actions.ShouldNotContain("<a ", Case.Insensitive, "Une action de la ligne mène quelque part.");
    }
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

    row[5].ShouldBe("Non");
    row[6].ShouldBe(label);
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

    (await RowWithAsync(email))[7].ShouldBe(expected);
  }

  /// <summary>
  /// <b>La date limite de réponse d'une demande reçue un 31 mars se lit le 30 avril</b> : un mois
  /// plus tard, ramené au dernier jour du mois quand le 31 n'y existe pas (ADR-0021).
  /// </summary>
  [Fact]
  public async Task RendersTheResponseDeadlineOfARequestReceivedOnMarch31AsApril30()
  {
    var email = $"{Guid.NewGuid():N}@example.org";

    await CreateAsync(new() { ["email"] = email, ["receivedOn"] = "2026-03-31" });

    var row = await RowWithAsync(email);

    row[3].ShouldBe("31/03/2026");
    row[4].ShouldBe("30/04/2026");
  }

  /// <summary>
  /// ⚠️ <b>La date limite se lit telle que la demande la tient</b>, jamais recalculée depuis la date
  /// de réception à la lecture : posée en base à une valeur qu'aucun calcul ne donnerait, c'est
  /// elle qui s'affiche.
  /// </summary>
  [Fact]
  public async Task RendersTheResponseDeadlineAsHeldByTheRequestWithoutRecomputingIt()
  {
    var email = $"{Guid.NewGuid():N}@example.org";
    var message = await CreateAsync(new() { ["email"] = email, ["receivedOn"] = "2026-01-15" });

    await _surface.PutResponseDeadlineAsync(message, new DateOnly(2026, 6, 3));

    (await RowWithAsync(email))[4].ShouldBe("03/06/2026");
  }

  /// <summary>
  /// <b>Le statut se lit en badge, sous son libellé</b> — « En cours » pour la demande qui naît,
  /// « Terminée » ou « Annulée » pour une demande dont la base porte ce statut. Le badge porte le
  /// nom du statut, que la feuille de style colore : sa couleur ne se garde pas ici (ADR-0005).
  /// </summary>
  [Theory]
  [InlineData(null, "InProgress", "En cours")]
  [InlineData("Completed", "Completed", "Terminée")]
  [InlineData("Cancelled", "Cancelled", "Annulée")]
  public async Task RendersTheStatusAsABadgeUnderItsLabel(string? stored, string status, string label)
  {
    var email = $"{Guid.NewGuid():N}@example.org";
    var message = await CreateAsync(new() { ["email"] = email });

    if (stored is not null)
    {
      await _surface.PutStatusAsync(message, stored);
    }

    var cell = CellsMarkupWith(await BoardAsync(), email)[9];
    var badge = Regex.Match(cell, @"<span\b(?<attributes>[^>]*)>(?<text>[^<]*)</span>");

    badge.Success.ShouldBeTrue("Le statut n'est pas rendu en badge.");
    badge.Groups["attributes"].Value.ShouldContain(@"class=""status""", Case.Sensitive, "Le statut n'est pas rendu en badge.");
    badge.Groups["attributes"].Value.ShouldContain($@"data-status=""{status}""", Case.Sensitive, "Le badge ne porte pas le nom du statut.");
    LayoutSurface.TextIn(badge.Groups["text"].Value).ShouldBe(label);
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

  /// <summary>Les cellules de la ligne qui porte <paramref name="marker"/>, chacune telle que le serveur la rend.</summary>
  private static string[] CellsMarkupWith(string main, string marker) =>
  [
    .. Regex.Matches(RowMarkupWith(main, marker), @"<td\b[^>]*>(.*?)</td>", RegexOptions.Singleline)
      .Select(cell => cell.Groups[1].Value),
  ];

  /// <summary>L'élément qui porte le message de l'état vide, une fois : ses attributs.</summary>
  private static string EmptyStateIn(string main)
  {
    var empty = Regex.Matches(main, @"<p\b(?<attributes>[^>]*)>(?<text>[^<]*)</p>")
      .Where(paragraph => LayoutSurface.TextIn(paragraph.Groups["text"].Value) == NoRequestYet)
      .ShouldHaveSingleItem($"L'écran ne porte pas « {NoRequestYet} », une fois.");

    return empty.Groups["attributes"].Value;
  }
}
