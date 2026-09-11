using System.Net;
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
  private readonly CreationHandler _handler = new(factory);

  /// <summary>
  /// <b>Une demande valide, envoyée avec le jeton anti-rejeu, est enregistrée</b> : le serveur répond
  /// 201.
  /// </summary>
  [Fact]
  public async Task AnswersCreatedToAValidRequest()
  {
    var response = await _handler.CreateAsync(CreationHandler.AValidRequest());

    response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
  }

  /// <summary>
  /// ⚠️ <b>Sans jeton anti-rejeu, rien n'est enregistré</b> : une page tierce qui ferait poster le
  /// navigateur de l'<c>Operator</c> n'atteint pas le handler.
  /// </summary>
  [Fact]
  public async Task RecordsNothingWithoutTheAntiforgeryToken()
  {
    var fields = CreationHandler.AValidRequest();

    var response = await _handler.Client.PostAsync(CreationHandler.Create, new FormUrlEncodedContent(fields));

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    (await _handler.CountOfAsync(fields["message"])).ShouldBe(0);
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

      var response = await _handler.CreateAsync(new Dictionary<string, string>
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

      var row = await _handler.RowOfAsync(message);

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
    var fields = CreationHandler.AValidRequest();
    fields["lastName"] = lastName;
    fields["firstName"] = firstName;
    fields["email"] = email;

    var response = await _handler.CreateAsync(fields);

    response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

    var row = await _handler.RowOfAsync(fields["message"]);

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
    document.Paths.Keys.ShouldNotContain(path => path.StartsWith(CreationHandler.Board, StringComparison.Ordinal));

    foreach (var word in new[] { "demandes", "DataSubjectRequest", "CreationForm", "receivedOn" })
    {
      published.ShouldNotContain(word, Case.Insensitive, $"Le document Swagger publie « {word} ».");
    }
  }
}
