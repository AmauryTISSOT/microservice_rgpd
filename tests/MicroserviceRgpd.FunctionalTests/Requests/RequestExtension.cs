using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.Web.Pages.Requests;

namespace MicroserviceRgpd.FunctionalTests.Requests;

/// <summary>
/// <b>Prolonger une demande</b>, exercé par la <b>seule frontière HTTP</b> : le handler
/// <c>POST /demandes?handler=Extend</c> que le script de la modale appelle, et le
/// <c>GET /demandes?handler=Extension</c> qui rend le récapitulatif — frappés ici directement, jeton
/// anti-rejeu compris (ADR-0029).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>La persistance se vérifie ici</b> : cette frontière écrit dans un vrai PostgreSQL, et les
/// quatre colonnes relues <b>en SQL brut</b> — sans repasser par le modèle qui les a écrites — sont ce
/// que le ticket promet.
/// </para>
/// <para>
/// ⚠️ <b>Les textes attendus se lisent sur les constantes du serveur</b> —
/// <see cref="ExtensionConfirmation"/>, <see cref="RequestRow"/> : une phrase retouchée là l'est ici.
/// </para>
/// <para>
/// Chaque demande porte un <b>message unique</b>, par lequel elle se retrouve en base : la base est
/// partagée par toute la collection, et ne se vide pas entre deux tests.
/// </para>
/// </remarks>
[Collection(WebCollection.Name)]
public class RequestExtension(CustomWebApplicationFactory<Program> factory)
{
  private readonly RequestSurface _surface = new(factory);

  /// <summary>
  /// <b>Une prolongation valide est enregistrée</b> : le serveur répond 200, et la demande relue en
  /// base porte <b>les quatre colonnes</b> — la date limite initiale recopiée, le motif sous son nom
  /// canonique, la justification élaguée, et l'instant du geste lu sur l'horloge du service. La date
  /// limite en vigueur, elle, a pris ses deux mois.
  /// </summary>
  [Fact]
  public async Task AnswersOkAndStoresTheFourExtensionColumns()
  {
    var ahead = TimeSpan.FromDays(3);
    var (id, message) = await _surface.RecordAsync(new Dictionary<string, string> { ["receivedOn"] = "2026-01-15" });

    factory.Clock.Advance(ahead);

    try
    {
      var before = DateTimeOffset.UtcNow + ahead;

      var response = await _surface.ExtendAsync(id, new Dictionary<string, string>
      {
        [DataSubjectRequestField.ExtensionGround] = nameof(ExtensionGround.NumberOfRequests),
        [DataSubjectRequestField.ExtensionJustification] = "  Le service a reçu quatre cents demandes ce mois-ci.  ",
      });

      var after = DateTimeOffset.UtcNow + ahead;

      response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

      var row = await _surface.RowOfAsync(message);

      row["initial_response_deadline"].ShouldBe(new DateOnly(2026, 2, 15));
      row["response_deadline"].ShouldBe(new DateOnly(2026, 4, 15));
      row["extension_ground"].ShouldBe(nameof(ExtensionGround.NumberOfRequests));
      row["extension_justification"].ShouldBe("Le service a reçu quatre cents demandes ce mois-ci.");
      new DateTimeOffset(row["extended_at"].ShouldBeOfType<DateTime>(), TimeSpan.Zero)
        .ShouldBeInRange(before, after);

      // ⚠️ Le geste ne change ni le statut, ni l'empreinte de modification : ce n'est pas une correction.
      row["status"].ShouldBe(nameof(RequestStatus.InProgress));
      row["modified_at"].ShouldBeNull();
      row["modified_by"].ShouldBeNull();
    }
    finally
    {
      factory.Clock.Advance(-ahead);
    }
  }

  /// <summary>
  /// <b>Le 200 porte la ligne à jour</b>, rendue par la vue partielle du tableau : la nouvelle date
  /// limite, et la mention « Prolongée » <b>dans la cellule de la date limite</b> — pas une colonne de
  /// plus.
  /// </summary>
  [Fact]
  public async Task AnswersWithTheRowCarryingTheNewDeadlineAndTheExtendedMention()
  {
    var (id, _) = await _surface.RecordAsync(new Dictionary<string, string> { ["receivedOn"] = "2026-01-15" });

    var response = await _surface.ExtendAsync(id);
    var row = await response.Content.ReadAsStringAsync();

    response.StatusCode.ShouldBe(HttpStatusCode.OK, row);
    response.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe("text/html");

    Regex.Matches(row, @"<tr\b").Count.ShouldBe(1, "Le 200 ne porte pas une ligne, une seule.");
    Regex.Match(row, @"data-request-id=""([^""]*)""").Groups[1].Value.ShouldBe(id.ToString());

    var deadline = DeadlineCellOf(row);

    deadline.ShouldContain("15/04/2026", Case.Sensitive, "La cellule ne porte pas la nouvelle date limite.");
    deadline.ShouldContain(RequestRow.Extended, Case.Sensitive, "La cellule ne porte pas la mention « Prolongée ».");

    RequestSurface.FieldNamesIn(row).ShouldBe(
      [.. RequestSurface.RowFields, RequestSurface.UnnamedActionsCell],
      "La prolongation a ajouté une colonne au tableau.");
  }

  /// <summary>
  /// <b>La ligne du tableau dit la prolongation elle aussi</b>, au rechargement : le même gabarit, la
  /// même mention. Et une demande <b>non</b> prolongée ne la porte pas.
  /// </summary>
  [Fact]
  public async Task CarriesTheExtendedMentionOnTheBoardOnlyOnAnExtendedRequest()
  {
    var (id, message) = await _surface.RecordAsync(new Dictionary<string, string> { ["receivedOn"] = "2026-01-15" });

    DeadlineCellOf(await _surface.BoardRowWithAsync(message))
      .ShouldNotContain(RequestRow.Extended, Case.Sensitive, "Une demande non prolongée porte la mention.");

    (await _surface.ExtendAsync(id)).StatusCode.ShouldBe(HttpStatusCode.OK);

    DeadlineCellOf(await _surface.BoardRowWithAsync(message))
      .ShouldContain(RequestRow.Extended, Case.Sensitive, "Une demande prolongée ne porte pas la mention.");
  }

  /// <summary>
  /// ⚠️ <b>Le repli de fin de mois est celui du domaine, pas celui du navigateur</b> : un 31 décembre
  /// prolongé de deux mois donne le 28 février — et non le 3 mars, que <c>Date.setMonth</c> rendrait.
  /// La date limite initiale, elle, reste intacte.
  /// </summary>
  [Fact]
  public async Task FoldsTheEndOfMonthAsTheDomainDoes()
  {
    var (id, message) = await _surface.RecordAsync();

    await _surface.SetResponseDeadlineAsync(id, new DateOnly(2026, 12, 31));

    (await _surface.ExtendAsync(id)).StatusCode.ShouldBe(HttpStatusCode.OK);

    var row = await _surface.RowOfAsync(message);

    row["initial_response_deadline"].ShouldBe(new DateOnly(2026, 12, 31));
    row["response_deadline"].ShouldBe(new DateOnly(2027, 2, 28));
  }

  /// <summary>
  /// <b>Le récapitulatif annonce exactement ce que le serveur écrira</b> : la date limite en vigueur,
  /// celle qui en résultera, et l'avertissement <b>daté de la date limite initiale</b> — celle qui vaut
  /// encore. Le tout en libellés, déjà écrits par le serveur.
  /// </summary>
  [Fact]
  public async Task ReadsTheSummaryTheExtensionWillHonour()
  {
    var (id, message) = await _surface.RecordAsync(new Dictionary<string, string>
    {
      ["receivedOn"] = "2026-01-15",
      ["lastName"] = "Martin",
      ["firstName"] = "Jeanne",
      ["email"] = "jeanne.martin@example.org",
      ["right"] = nameof(Core.SharedKernel.DataSubjectRight.Erasure),
    });

    var response = await _surface.ExtensionOfAsync(id);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    response.Headers.CacheControl.ShouldNotBeNull().NoStore.ShouldBeTrue("Le récapitulatif est gardé en cache.");

    using var summary = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var fields = summary.RootElement;

    fields.GetProperty("right").GetString().ShouldBe(
      $"Droit à l'effacement (art. {Core.SharedKernel.DataSubjectRight.Erasure.Article})");
    fields.GetProperty("firstName").GetString().ShouldBe("Jeanne");
    fields.GetProperty("lastName").GetString().ShouldBe("Martin");
    fields.GetProperty("email").GetString().ShouldBe("jeanne.martin@example.org");
    fields.GetProperty("currentDeadline").GetString().ShouldBe("15/02/2026");
    fields.GetProperty("resultingDeadline").GetString().ShouldBe("15/04/2026");
    fields.GetProperty("warning").GetString().ShouldBe(
      string.Format(System.Globalization.CultureInfo.InvariantCulture, ExtensionConfirmation.WarningFormat, "15/02/2026"));

    // ⚠️ LIRE N'EST PAS PROLONGER : rien n'a été écrit.
    (await _surface.RowOfAsync(message))["extended_at"].ShouldBeNull();
  }

  /// <summary>
  /// <b>Le récapitulatif annonce la date que le serveur écrira, au jour près</b> — y compris au repli
  /// de fin de mois, que le navigateur ne saurait pas faire.
  /// </summary>
  [Fact]
  public async Task AnnouncesExactlyTheDeadlineTheServerWrites()
  {
    var (id, message) = await _surface.RecordAsync();

    await _surface.SetResponseDeadlineAsync(id, new DateOnly(2026, 12, 31));

    using var summary = JsonDocument.Parse(await (await _surface.ExtensionOfAsync(id)).Content.ReadAsStringAsync());
    var announced = summary.RootElement.GetProperty("resultingDeadline").GetString();

    (await _surface.ExtendAsync(id)).StatusCode.ShouldBe(HttpStatusCode.OK);

    var written = (DateOnly)(await _surface.RowOfAsync(message))["response_deadline"]!;

    announced.ShouldBe(written.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture));
  }

  /// <summary>
  /// ⚠️ <b>Un identifiant illisible rend 400, jamais 404</b> : ce n'est pas une demande introuvable,
  /// mais un envoi que l'écran n'a pas composé. Des deux côtés, et rien n'est écrit.
  /// </summary>
  [Theory]
  [InlineData("pas-un-guid")]
  [InlineData("")]
  public async Task RefusesAnUnreadableIdentifier(string id)
  {
    (await _surface.ExtendAsync(id)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    (await _surface.ExtensionOfAsync(id)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  /// <summary><b>Une demande qui n'existe pas rend 404</b>, des deux côtés.</summary>
  [Fact]
  public async Task RefusesAnUnknownRequest()
  {
    var unknown = Guid.CreateVersion7().ToString();

    (await _surface.ExtendAsync(unknown)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    (await _surface.ExtensionOfAsync(unknown)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  /// <summary>
  /// ⚠️ <b>Sans jeton anti-rejeu, rien n'est prolongé</b> : le handler est celui de la page, et une
  /// page tierce ne doit pas pouvoir le faire poster par le navigateur de l'<c>Operator</c>.
  /// </summary>
  [Fact]
  public async Task ExtendsNothingWithoutTheAntiforgeryToken()
  {
    var (id, message) = await _surface.RecordAsync();

    var response = await _surface.ExtendWithoutTokenAsync(id);

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    (await _surface.RowOfAsync(message))["extended_at"].ShouldBeNull();
  }

  /// <summary>La cellule de la date limite de réponse d'une ligne rendue.</summary>
  private static string DeadlineCellOf(string row) =>
    Regex.Match(row, @"<td\b[^>]*data-field=""responseDeadline""[^>]*>(?<cell>.*?)</td>", RegexOptions.Singleline)
      .Groups["cell"].Value;
}
