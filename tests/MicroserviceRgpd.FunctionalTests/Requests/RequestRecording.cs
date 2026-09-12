using System.Net;
using System.Text.RegularExpressions;
using MicroserviceRgpd.FunctionalTests.Layout;
using NSwag.Generation;

namespace MicroserviceRgpd.FunctionalTests.Requests;

/// <summary>
/// <b>Enregistrer une demande</b>, exercé par la <b>seule frontière HTTP</b> : le handler
/// <c>POST /demandes?handler=Create</c> que le script de la modale appellera, frappé ici directement,
/// jeton anti-rejeu compris — exactement ce que fera le navigateur.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>La persistance se vérifie ici, et nulle part ailleurs</b> : cette frontière écrit déjà dans
/// un vrai PostgreSQL, et la ligne relue en base est ce que le ticket promet. Il n'y a pas de test
/// d'intégration dédié.
/// </para>
/// <para>
/// Chaque demande porte un <b>message unique</b>, par lequel elle se retrouve en base : la base est
/// partagée par toute la collection, et ne se vide pas entre deux tests.
/// </para>
/// </remarks>
[Collection(WebCollection.Name)]
public class RequestRecording(CustomWebApplicationFactory<Program> factory)
{
  private readonly RequestSurface _surface = new(factory);

  /// <summary>
  /// <b>Une demande valide, envoyée avec le jeton anti-rejeu, est enregistrée</b> : le serveur répond
  /// 201.
  /// </summary>
  [Fact]
  public async Task AnswersCreatedToAValidRequest()
  {
    var response = await _surface.CreateAsync(RequestSurface.AValidRequest());

    response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
  }

  /// <summary>
  /// <b>Le 201 porte la ligne de la nouvelle demande</b>, en HTML, que le script insère dans le
  /// tableau : ses libellés — dont la date limite de réponse calculée et le statut « En cours » —,
  /// l'identifiant de la demande relue en base, et le badge sous le nom canonique du statut.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>C'est la ligne même que le tableau rendra</b>, au caractère près : une seule vue partielle
  /// rend les deux, et deux gabarits finiraient par diverger.
  /// </remarks>
  [Fact]
  public async Task AnswersCreatedWithTheRowOfTheNewRequest()
  {
    var email = $"{Guid.NewGuid():N}@example.org";
    var fields = RequestSurface.AValidRequest();
    fields["receivedOn"] = "2026-01-31";
    fields["lastName"] = "Martin";
    fields["firstName"] = "Jeanne";
    fields["email"] = email;
    fields["right"] = "Erasure";

    var response = await _surface.CreateAsync(fields);
    var body = await response.Content.ReadAsStringAsync();

    response.StatusCode.ShouldBe(HttpStatusCode.Created, body);
    response.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe("text/html");

    var row = body.Trim();
    Regex.Matches(row, @"<tr\b").Count.ShouldBe(1, "Le 201 ne porte pas une ligne, une seule.");

    var cells = Regex.Matches(row, @"<td\b[^>]*>(.*?)</td>", RegexOptions.Singleline)
      .Select(cell => LayoutSurface.TextIn(cell.Groups[1].Value))
      .ToArray();

    // La ligne du 201 est celle du tableau : la date limite, antérieure à aujourd'hui, y est signalée de même.
    cells[..7].ShouldBe([email, "Martin", "Jeanne", "31/01/2026", "28/02/2026 En retard", "Non", "Droit à l'effacement"]);
    cells[8..10].ShouldBe(["Opérateur", "En cours"]);
    row.ShouldContain(@"data-status=""InProgress""");
    row.ShouldContain(@"data-deadline-signal=""Overdue""", customMessage: "La ligne du 201 ne signale pas sa date limite.");
    row.ShouldContain(@"data-received-on=""2026-01-31""", customMessage: "La ligne ne porte pas sa date de réception ISO, qui la place.");

    var stored = await _surface.RowOfAsync(fields["message"]);

    stored["status"].ShouldBe("InProgress");
    stored["response_deadline"].ShouldBe(new DateOnly(2026, 2, 28));
    Regex.Match(row, @"data-request-id=""([^""]*)""").Groups[1].Value.ShouldBe(stored["id"]?.ToString());

    row.ShouldBe(await _surface.BoardRowWithAsync(email), "La ligne du 201 n'est pas celle que le tableau rend.");
  }

  /// <summary>
  /// ⚠️ <b>Sans jeton anti-rejeu, rien n'est enregistré</b> : une page tierce qui ferait poster le
  /// navigateur de l'<c>Operator</c> n'atteint pas le handler.
  /// </summary>
  [Fact]
  public async Task RecordsNothingWithoutTheAntiforgeryToken()
  {
    var fields = RequestSurface.AValidRequest();

    var response = await _surface.CreateWithoutTokenAsync(fields);

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    (await _surface.CountOfAsync(fields["message"])).ShouldBe(0);
  }

  /// <summary>
  /// <b>La ligne relue en base porte toute la demande</b> : les valeurs trimées, l'email dans sa
  /// casse, les vocabulaires fermés sous leur nom, signée <c>operator</c> et datée par l'horloge du
  /// service, en UTC.
  /// </summary>
  /// <remarks>
  /// ⚠️ L'horloge est <b>avancée</b> avant l'envoi : un instant d'enregistrement lu sur la machine
  /// plutôt que sur l'horloge du service tomberait hors de la fenêtre attendue.
  /// </remarks>
  [Fact]
  public async Task StoresTheWholeRequestTrimmedSignedAndDatedByTheServiceClock()
  {
    var ahead = TimeSpan.FromDays(40);
    var message = $"Merci d'effacer mon compte. {Guid.NewGuid()}";

    factory.Clock.Advance(ahead);

    try
    {
      var before = DateTimeOffset.UtcNow + ahead;

      var response = await _surface.CreateAsync(new Dictionary<string, string>
      {
        ["origin"] = "Letter",
        ["receivedOn"] = "2026-01-15",
        ["lastName"] = "  Martin  ",
        ["firstName"] = "\tJeanne ",
        ["email"] = "  Jeanne.MARTIN@Example.org ",
        ["identityVerified"] = "true",
        ["message"] = $"  {message}\n",
        ["right"] = "Erasure",
      });

      var after = DateTimeOffset.UtcNow + ahead;

      response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

      var row = await _surface.RowOfAsync(message);

      row["id"].ShouldBeOfType<Guid>().ShouldNotBe(Guid.Empty);
      row["origin"].ShouldBe("Letter");
      row["received_on"].ShouldBe(new DateOnly(2026, 1, 15));
      row["last_name"].ShouldBe("Martin");
      row["first_name"].ShouldBe("Jeanne");
      row["email"].ShouldBe("Jeanne.MARTIN@Example.org");
      row["identity_verified"].ShouldBe(true);
      row["message"].ShouldBe(message);
      row["data_subject_right"].ShouldBe("Erasure");
      row["created_by"].ShouldBe("operator");

      var createdAt = row["created_at"].ShouldBeOfType<DateTime>();
      createdAt.Kind.ShouldBe(DateTimeKind.Utc);
      new DateTimeOffset(createdAt).ShouldBeInRange(before, after);
    }
    finally
    {
      factory.Clock.Reset();
    }
  }

  /// <summary>
  /// <b>Une demande enregistrée est relue <c>InProgress</c></b>, sous le nom de la valeur — comme
  /// l'origine et le droit (ADR-0021).
  /// </summary>
  [Fact]
  public async Task StoresTheRequestInProgress()
  {
    var fields = RequestSurface.AValidRequest();

    var response = await _surface.CreateAsync(fields);

    response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
    (await _surface.RowOfAsync(fields["message"]))["status"].ShouldBe("InProgress");
  }

  /// <summary>
  /// <b>Une demande enregistrée est relue avec sa date limite de réponse</b>, calculée depuis la date
  /// de réception : un 31 janvier donne le 28 février, dernier jour du mois suivant (ADR-0021).
  /// </summary>
  [Fact]
  public async Task StoresTheResponseDeadlineComputedFromTheReceptionDate()
  {
    var fields = RequestSurface.AValidRequest();
    fields["receivedOn"] = "2026-01-31";

    var response = await _surface.CreateAsync(fields);

    response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
    (await _surface.RowOfAsync(fields["message"]))["response_deadline"].ShouldBe(new DateOnly(2026, 2, 28));
  }

  /// <summary>
  /// <b>Les trois façons d'identifier la personne sont acceptées</b> — un email seul ; un nom et un
  /// prénom sans email ; un email avec un nom partiel —, et un champ laissé vide est enregistré
  /// comme absent, non comme une chaîne vide.
  /// </summary>
  [Theory]
  [InlineData("", "", "jeanne.martin@example.org")]
  [InlineData("Martin", "Jeanne", "")]
  [InlineData("Martin", "", "jeanne.martin@example.org")]
  [InlineData("", "Jeanne", "jeanne.martin@example.org")]
  public async Task RecordsEachAcceptedIdentification(string lastName, string firstName, string email)
  {
    var fields = RequestSurface.AValidRequest();
    fields["lastName"] = lastName;
    fields["firstName"] = firstName;
    fields["email"] = email;

    var response = await _surface.CreateAsync(fields);

    response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

    var row = await _surface.RowOfAsync(fields["message"]);

    row["last_name"].ShouldBe(lastName.Length == 0 ? null : lastName);
    row["first_name"].ShouldBe(firstName.Length == 0 ? null : firstName);
    row["email"].ShouldBe(email.Length == 0 ? null : email);
  }

  /// <summary>
  /// ⚠️ <b>La création n'est pas une API</b> : le document Swagger — celui qui nourrit Swagger et
  /// Scalar — ne publie ni sa route, ni son contrat. Le handler est celui d'une page, appelé par son
  /// propre script.
  /// </summary>
  [Fact]
  public async Task TheApiDocumentDoesNotPublishTheCreation()
  {
    using var scope = factory.Services.CreateScope();
    var document = await scope.ServiceProvider.GetRequiredService<IOpenApiDocumentGenerator>().GenerateAsync("v1");
    var published = document.ToJson();

    document.Paths.Keys.ShouldContain("/qualifications", "Le document ne publie plus rien : les assertions suivantes seraient vides.");
    document.Paths.Keys.ShouldNotContain(path => path.StartsWith(RequestSurface.Board, StringComparison.Ordinal));

    foreach (var word in new[] { "demandes", "DataSubjectRequest", "RequestForm", "receivedOn" })
    {
      published.ShouldNotContain(word, Case.Insensitive, $"Le document Swagger publie « {word} ».");
    }
  }
}
