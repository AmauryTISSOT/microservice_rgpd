using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using MicroserviceRgpd.Core.Requests;
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

  /// <summary>Le message de l'état vide de la recherche, recopié à dessein.</summary>
  private const string NoMatch = "Aucune demande ne correspond à votre recherche";

  /// <summary>Le texte d'aide de la recherche, recopié à dessein.</summary>
  private const string SearchHint = "Rechercher par email, nom ou prénom";

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
  /// serveur lui donne : les dates de réception et limite de réponse en <c>jj/mm/aaaa</c> — la date
  /// limite, antérieure à aujourd'hui, suivie de « En retard » —, « Oui », le droit avec une
  /// majuscule initiale et sans article du RGPD, « Opérateur », et le statut « En cours » d'une
  /// demande qui naît. La cellule d'actions se garde dans
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

    row[..7].ShouldBe([email, "Martin", "Jeanne", "15/01/2026", "15/02/2026 En retard", "Oui", "Droit à l'effacement"]);
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
    row[4].ShouldBe("30/04/2026 En retard");
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
    var (id, _) = await _surface.RecordAsync(new Dictionary<string, string> { ["email"] = email, ["receivedOn"] = "2026-01-15" });

    await _surface.SetResponseDeadlineAsync(id, new DateOnly(2026, 6, 3));

    (await RowWithAsync(email))[4].ShouldBe("03/06/2026 En retard");
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
    var (id, _) = await _surface.RecordAsync(new Dictionary<string, string> { ["email"] = email });

    if (stored is not null)
    {
      await _surface.SetStatusAsync(id, stored);
    }

    var cell = CellsMarkupWith(await BoardAsync(), email)[9];
    var badge = Regex.Match(cell, @"<span\b(?<attributes>[^>]*)>(?<text>[^<]*)</span>");

    badge.Success.ShouldBeTrue("Le statut n'est pas rendu en badge.");
    badge.Groups["attributes"].Value.ShouldContain(@"class=""status""", Case.Sensitive, "Le statut n'est pas rendu en badge.");
    badge.Groups["attributes"].Value.ShouldContain($@"data-status=""{status}""", Case.Sensitive, "Le badge ne porte pas le nom du statut.");
    LayoutSurface.TextIn(badge.Groups["text"].Value).ShouldBe(label);
  }

  /// <summary>
  /// <b>La date limite d'une demande En cours est signalée par rapport à aujourd'hui</b> — « En
  /// retard » la veille, « Échéance proche » le jour même et jusqu'à sept jours plus tard, rien au
  /// huitième. Le signalement porte son nom, que la feuille de style colore, et sa mention se lit
  /// après la date.
  /// </summary>
  /// <remarks>
  /// L'horloge du service est <b>avancée</b> jusqu'au prochain 10 h UTC — le même jour à Paris, l'été
  /// comme l'hiver, et loin de minuit —, et remise à l'heure réelle quoi qu'il arrive.
  /// </remarks>
  [Theory]
  [InlineData(-1, "Overdue", "En retard")]
  [InlineData(0, "DueSoon", "Échéance proche")]
  [InlineData(7, "DueSoon", "Échéance proche")]
  [InlineData(8, null, null)]
  public async Task SignalsTheDeadlineOfARequestInProgressAgainstToday(int daysFromToday, string? signal, string? mention)
  {
    await AtTheNextAsync(TimeSpan.FromHours(10), async () =>
    {
      var deadline = ParisCalendar.Today(factory.Clock).AddDays(daysFromToday);
      var email = $"{Guid.NewGuid():N}@example.org";
      var (id, _) = await _surface.RecordAsync(new Dictionary<string, string> { ["email"] = email });

      await _surface.SetResponseDeadlineAsync(id, deadline);

      var (attributes, text) = DeadlineCellWith(await BoardAsync(), email);
      var day = deadline.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

      if (signal is null)
      {
        attributes.ShouldNotContain("data-deadline-signal", Case.Sensitive, "Une date limite lointaine est signalée.");
        text.ShouldBe(day);
      }
      else
      {
        attributes.ShouldContain($@"data-deadline-signal=""{signal}""", Case.Sensitive, "La date limite ne porte pas son signalement.");
        text.ShouldBe($"{day} {mention}");
      }
    });
  }

  /// <summary>
  /// <b>Une demande Terminée ou Annulée n'est jamais signalée</b>, même au-delà de sa date limite :
  /// seule une demande En cours attend encore une réponse.
  /// </summary>
  [Theory]
  [InlineData("Completed")]
  [InlineData("Cancelled")]
  public async Task NeverSignalsARequestThatIsNoLongerInProgress(string status)
  {
    var email = $"{Guid.NewGuid():N}@example.org";
    var (id, _) = await _surface.RecordAsync(new Dictionary<string, string> { ["email"] = email, ["receivedOn"] = "2026-01-15" });

    await _surface.SetStatusAsync(id, status);

    var (attributes, text) = DeadlineCellWith(await BoardAsync(), email);

    attributes.ShouldNotContain("data-deadline-signal", Case.Sensitive, $"Une demande au statut {status} est signalée.");
    text.ShouldBe("15/02/2026");
  }

  /// <summary>
  /// ⚠️ <b>Seule la cellule de la date limite porte le signalement</b> : ni la ligne, ni aucune autre
  /// cellule ne s'en colore.
  /// </summary>
  [Fact]
  public async Task SignalsOnlyTheDeadlineCellNotTheRow()
  {
    var email = $"{Guid.NewGuid():N}@example.org";

    await CreateAsync(new() { ["email"] = email, ["receivedOn"] = "2026-01-15" });

    var row = RowMarkupWith(await BoardAsync(), email);
    var cells = Regex.Matches(row, @"<td\b[^>]*>").Select(cell => cell.Value).ToArray();

    Regex.Match(row, @"<tr\b[^>]*>").Value.ShouldNotContain("deadline", Case.Sensitive, "La ligne entière porte le signalement.");
    cells[4].ShouldContain(@"data-deadline-signal=""Overdue""", Case.Sensitive, "La date limite antérieure à aujourd'hui n'est pas signalée.");
    cells.Where((_, index) => index != 4).ShouldAllBe(
      cell => !cell.Contains("deadline", StringComparison.Ordinal),
      "Une autre cellule que la date limite porte le signalement.");
  }

  /// <summary>
  /// ⚠️ <b>« Aujourd'hui » est celui de Paris, pas celui de l'UTC.</b> À 23 h 30 UTC, il est déjà le
  /// lendemain à Paris, l'été comme l'hiver : une date limite tombée le jour UTC est déjà En retard.
  /// Lu en UTC, ce serait le jour même — « Échéance proche ».
  /// </summary>
  [Fact]
  public async Task ReadsTodayInParisWhileItIsStillTheDayBeforeInUtc()
  {
    await AtTheNextAsync(new TimeSpan(23, 30, 0), async () =>
    {
      var todayInUtc = DateOnly.FromDateTime(factory.Clock.GetUtcNow().UtcDateTime);
      ParisCalendar.Today(factory.Clock).ShouldBe(todayInUtc.AddDays(1), "Il n'est pas déjà le lendemain à Paris.");

      var email = $"{Guid.NewGuid():N}@example.org";
      var (id, _) = await _surface.RecordAsync(new Dictionary<string, string> { ["email"] = email });

      await _surface.SetResponseDeadlineAsync(id, todayInUtc);

      DeadlineCellWith(await BoardAsync(), email).Attributes
        .ShouldContain(@"data-deadline-signal=""Overdue""", Case.Sensitive, "« Aujourd'hui » n'est pas lu à Paris.");
    });
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
  /// <b>La barre de recherche est en haut à gauche du tableau</b> : entre le bouton de création et le
  /// tableau, un champ de recherche nommé pour qui ne le voit pas, dont le texte d'aide est
  /// « Rechercher par email, nom ou prénom », suivi de son bouton ✕ — un simple bouton, qui se nomme
  /// « Vider la recherche ». Ni l'un ni l'autre n'est dans un formulaire : rien ne part au serveur.
  /// </summary>
  [Fact]
  public async Task CarriesTheSearchBarAndItsClearButtonAboveTheTable()
  {
    var main = await BoardAsync();
    var search = SearchBarIn(main);

    main.IndexOf(search, StringComparison.Ordinal).ShouldBeGreaterThan(
      main.IndexOf(@"id=""create-request-open""", StringComparison.Ordinal), "La recherche n'est pas sous le bouton de création.");
    main.IndexOf(search, StringComparison.Ordinal).ShouldBeLessThan(
      main.IndexOf("<table", StringComparison.Ordinal), "La recherche n'est pas au-dessus du tableau.");

    var field = Regex.Matches(search, @"<input\b[^>]*>").ShouldHaveSingleItem().Value;
    field.ShouldContain(@"type=""search""", Case.Sensitive, "Le champ de recherche n'en est pas un.");
    field.ShouldContain(@$"placeholder=""{SearchHint}""", Case.Sensitive, "Le champ de recherche ne porte pas son texte d'aide.");
    field.ShouldContain(@"aria-label=""Rechercher une demande""", Case.Sensitive, "Le champ de recherche ne se nomme pas.");

    var clear = Regex.Matches(search, @"<button\b[^>]*>").ShouldHaveSingleItem().Value;
    clear.ShouldContain(@"type=""button""", Case.Sensitive, "Le bouton ✕ n'est pas un simple bouton.");
    clear.ShouldContain(@"aria-label=""Vider la recherche""", Case.Sensitive, "Le bouton ✕ ne se nomme pas.");
  }

  /// <summary>
  /// <b>L'état vide de la recherche est rendu, et caché</b> : c'est le script qui le montre quand la
  /// saisie écarte toutes les lignes, avec les mots que le serveur lui a donnés.
  /// </summary>
  [Fact]
  public async Task RendersTheEmptyStateOfTheSearchHidden()
  {
    await CreateAsync(new() { ["email"] = $"{Guid.NewGuid():N}@example.org" });

    Regex.IsMatch(ParagraphSaying(await BoardAsync(), NoMatch), @"\bhidden\b")
      .ShouldBeTrue($"« {NoMatch} » se lit avant toute recherche.");
  }

  /// <summary>
  /// <b>Chaque ligne porte le texte que la recherche parcourt</b> — l'email, le nom et le prénom, tels
  /// qu'enregistrés : c'est le script qui les normalise, comme il normalise la saisie.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Une valeur absente est vide, et non « — »</b> : le tiret est le libellé d'une cellule, pas
  /// un texte que la personne a donné. Chercher « — » ne doit trouver aucune demande sans email.
  /// </remarks>
  [Fact]
  public async Task CarriesTheSearchableTextOfEachRow()
  {
    var marker = Guid.NewGuid().ToString("N");
    var byName = $"Hélène-{marker}";

    await CreateAsync(new() { ["email"] = "", ["lastName"] = "Dupont", ["firstName"] = byName });

    var row = Regex.Matches(TableIn(await BoardAsync()), @"<tr\b(?<attributes>[^>]*)>(?<cells>.*?)</tr>", RegexOptions.Singleline)
      .Where(match => match.Groups["cells"].Value.Contains(marker, StringComparison.Ordinal))
      .ShouldHaveSingleItem()
      .Groups["attributes"].Value;

    row.ShouldContain(@"data-email=""""", Case.Sensitive, "L'email absent n'est pas cherché vide.");
    row.ShouldContain(@"data-last-name=""Dupont""", Case.Sensitive, "La ligne ne porte pas son nom à chercher.");
    WebUtility.HtmlDecode(Regex.Match(row, @"data-first-name=""([^""]*)""").Groups[1].Value)
      .ShouldBe(byName, "La ligne ne porte pas son prénom à chercher, tel qu'enregistré.");
  }

  /// <summary>
  /// <b>Le menu de tri est au-dessus du tableau</b>, après la recherche : un menu déroulant nommé pour
  /// qui ne le voit pas, qui propose « Date de réception la plus récente » — sélectionnée à
  /// l'ouverture, c'est l'ordre que le serveur a rendu — puis « Date de réception la plus
  /// ancienne ». Il n'est dans aucun formulaire : rien ne part au serveur.
  /// </summary>
  [Fact]
  public async Task CarriesTheSortMenuWithTheMostRecentReceptionSelected()
  {
    var main = await BoardAsync();
    var menu = Regex.Matches(main, @"<select\b(?<attributes>[^>]*)>(?<options>.*?)</select>", RegexOptions.Singleline)
      .ShouldHaveSingleItem("L'écran ne porte pas un menu de tri, un seul.");

    main.IndexOf(menu.Value, StringComparison.Ordinal).ShouldBeGreaterThan(
      main.IndexOf(SearchBarIn(main), StringComparison.Ordinal), "Le menu de tri ne suit pas la recherche.");
    main.IndexOf(menu.Value, StringComparison.Ordinal).ShouldBeLessThan(
      main.IndexOf("<table", StringComparison.Ordinal), "Le menu de tri n'est pas au-dessus du tableau.");

    var id = Regex.Match(menu.Groups["attributes"].Value, @"\bid=""([^""]+)""");
    id.Success.ShouldBeTrue("Le menu de tri n'a pas d'identifiant, que son libellé désignerait.");
    Regex.Matches(main, $@"<label\b[^>]*\bfor=""{Regex.Escape(id.Groups[1].Value)}""[^>]*>(.*?)</label>", RegexOptions.Singleline)
      .ShouldHaveSingleItem("Le menu de tri ne se nomme pas.")
      .Groups[1].Value.ShouldNotBeNullOrWhiteSpace("Le libellé du menu de tri est vide.");

    var options = Regex.Matches(menu.Groups["options"].Value, @"<option\b(?<attributes>[^>]*)>(?<text>[^<]*)</option>");

    options
      .Select(option => LayoutSurface.TextIn(option.Groups["text"].Value))
      .ShouldBe(
        ["Date de réception la plus récente", "Date de réception la plus ancienne"],
        "Le menu de tri ne propose pas ses deux options, dans l'ordre.");

    options
      .Where(option => Regex.IsMatch(option.Groups["attributes"].Value, @"\bselected\b"))
      .Select(option => LayoutSurface.TextIn(option.Groups["text"].Value))
      .ShouldBe(["Date de réception la plus récente"], "« Date de réception la plus récente » n'est pas sélectionnée à l'ouverture, ou pas seule.");
  }

  /// <summary>
  /// ⚠️ <b>Les en-têtes de colonnes ne se cliquent pas</b> : ni lien, ni bouton — seul le menu trie.
  /// </summary>
  [Fact]
  public async Task KeepsTheColumnHeadersUnclickable()
  {
    var head = Regex.Match(TableIn(await BoardAsync()), @"<thead\b[^>]*>(.*?)</thead>", RegexOptions.Singleline).Groups[1].Value;

    foreach (var clickable in new[] { "<a ", "<button" })
    {
      head.ShouldNotContain(clickable, Case.Insensitive, $"Un en-tête de colonne porte un {clickable}>.");
    }
  }

  /// <summary>
  /// <b>Chaque ligne porte ses clés de tri</b> : sa date de réception en ISO, et son instant
  /// d'enregistrement en ISO, en UTC. Le script les compare comme des textes : c'est pourquoi
  /// l'instant est toujours écrit à la même largeur, six décimales comprises — la microseconde que la
  /// table retient, et rien de plus fin.
  /// </summary>
  [Fact]
  public async Task CarriesTheSortKeysOfEachRow()
  {
    var email = $"{Guid.NewGuid():N}@example.org";
    var message = await CreateAsync(new() { ["email"] = email, ["receivedOn"] = "2026-02-03" });

    var row = Regex.Match(RowMarkupWith(await BoardAsync(), email), @"<tr\b([^>]*)>").Groups[1].Value;

    AttributeOf(row, "data-received-on").ShouldBe("2026-02-03");

    var createdAt = AttributeOf(row, "data-created-at");
    createdAt.ShouldMatch(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{6}Z$", "L'instant d'enregistrement n'est pas en ISO UTC, à largeur fixe.");

    var stored = (await _surface.RowOfAsync(message))["created_at"].ShouldBeOfType<DateTime>();
    DateTime.Parse(createdAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
      .ShouldBe(stored.ToUniversalTime(), "La ligne ne porte pas l'instant d'enregistrement de sa demande.");
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

  /// <summary>
  /// La cellule de la date limite de la ligne qui porte <paramref name="marker"/> : les attributs de sa
  /// balise, et son texte.
  /// </summary>
  private static (string Attributes, string Text) DeadlineCellWith(string main, string marker)
  {
    var cell = Regex.Matches(RowMarkupWith(main, marker), @"<td\b(?<attributes>[^>]*)>(?<content>.*?)</td>", RegexOptions.Singleline)[4];

    return (cell.Groups["attributes"].Value, LayoutSurface.TextIn(cell.Groups["content"].Value));
  }

  /// <summary>
  /// Avance l'horloge du service jusqu'à la prochaine <paramref name="timeOfDayInUtc"/> UTC, y joue
  /// <paramref name="act"/>, et la remet à l'heure réelle quoi qu'il arrive.
  /// </summary>
  private async Task AtTheNextAsync(TimeSpan timeOfDayInUtc, Func<Task> act)
  {
    var now = factory.Clock.GetUtcNow();
    var next = new DateTimeOffset(now.UtcDateTime.Date + timeOfDayInUtc, TimeSpan.Zero);
    if (next <= now)
    {
      next = next.AddDays(1);
    }

    factory.Clock.Advance(next - now);

    try
    {
      await act();
    }
    finally
    {
      factory.Clock.Reset();
    }
  }

  /// <summary>La valeur de l'attribut <paramref name="name"/>, décodée comme le navigateur la lit.</summary>
  private static string AttributeOf(string attributes, string name)
  {
    var attribute = Regex.Match(attributes, $@"\b{Regex.Escape(name)}=""([^""]*)""");

    attribute.Success.ShouldBeTrue($"La ligne ne porte pas « {name} ».");

    return WebUtility.HtmlDecode(attribute.Groups[1].Value);
  }

  /// <summary>L'élément qui porte le message de l'état vide, une fois : ses attributs.</summary>
  private static string EmptyStateIn(string main) => ParagraphSaying(main, NoRequestYet);

  /// <summary>Le paragraphe qui dit <paramref name="text"/>, une fois : ses attributs.</summary>
  private static string ParagraphSaying(string main, string text)
  {
    var paragraph = Regex.Matches(main, @"<p\b(?<attributes>[^>]*)>(?<text>[^<]*)</p>")
      .Where(paragraph => LayoutSurface.TextIn(paragraph.Groups["text"].Value) == text)
      .ShouldHaveSingleItem($"L'écran ne porte pas « {text} », une fois.");

    return paragraph.Groups["attributes"].Value;
  }

  /// <summary>La barre de recherche, entière — balise ouvrante comprise —, une fois.</summary>
  private static string SearchBarIn(string main) =>
    Regex.Matches(main, @"<div\b[^>]*\brole=""search""[^>]*>.*?</div>", RegexOptions.Singleline)
      .ShouldHaveSingleItem("L'écran ne porte pas une barre de recherche, une seule.")
      .Value;
}
