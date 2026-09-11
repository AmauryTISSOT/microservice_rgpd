using System.Net;
using System.Text.RegularExpressions;
using MicroserviceRgpd.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
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
  private const string Board = "/demandes";

  private const string Create = "/demandes?handler=Create";

  private readonly HttpClient _client = factory.CreateClient(
    new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

  /// <summary>
  /// <b>Une demande valide, envoyée avec le jeton anti-rejeu, est enregistrée</b> : le serveur répond
  /// 201.
  /// </summary>
  [Fact]
  public async Task AnswersCreatedToAValidRequest()
  {
    var response = await CreateAsync(AValidRequest());

    response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
  }

  /// <summary>
  /// ⚠️ <b>Sans jeton anti-rejeu, rien n'est enregistré</b> : une page tierce qui ferait poster le
  /// navigateur de l'<c>Operator</c> n'atteint pas le handler.
  /// </summary>
  [Fact]
  public async Task RecordsNothingWithoutTheAntiforgeryToken()
  {
    var fields = AValidRequest();

    var response = await _client.PostAsync(Create, new FormUrlEncodedContent(fields));

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    (await CountOfAsync(fields["message"])).ShouldBe(0);
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

      var response = await CreateAsync(new Dictionary<string, string>
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

      var row = await RowOfAsync(message);

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
    var fields = AValidRequest();
    fields["lastName"] = lastName;
    fields["firstName"] = firstName;
    fields["email"] = email;

    var response = await CreateAsync(fields);

    response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

    var row = await RowOfAsync(fields["message"]);

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
    document.Paths.Keys.ShouldNotContain(path => path.StartsWith(Board, StringComparison.Ordinal));

    foreach (var word in new[] { "demandes", "DataSubjectRequest", "CreationForm", "receivedOn" })
    {
      published.ShouldNotContain(word, Case.Insensitive, $"Le document Swagger publie « {word} ».");
    }
  }

  /// <summary>Une saisie complète et valide, identifiée par son seul email.</summary>
  private static Dictionary<string, string> AValidRequest() => new()
  {
    ["origin"] = "Email",
    ["receivedOn"] = "2026-01-15",
    ["lastName"] = "",
    ["firstName"] = "",
    ["email"] = "jeanne.martin@example.org",
    ["identityVerified"] = "false",
    ["message"] = $"Je souhaite accéder à mes données. {Guid.NewGuid()}",
    ["right"] = "Access",
  };

  /// <summary>
  /// Envoie la saisie au handler de création, avec le jeton que la page rend — et le cookie qui va
  /// avec, que le client garde d'une requête à l'autre.
  /// </summary>
  private async Task<HttpResponseMessage> CreateAsync(IReadOnlyDictionary<string, string> fields)
  {
    return await _client.PostAsync(Create, new FormUrlEncodedContent(
    [
      new("__RequestVerificationToken", await AntiforgeryTokenAsync()),
      .. fields,
    ]));
  }

  /// <summary>
  /// La ligne de la demande, relue <b>telle que la table la porte</b> — colonne par colonne, sous
  /// leur nom SQL, sans passer par le modèle EF qui l'a écrite.
  /// </summary>
  private async Task<IReadOnlyDictionary<string, object?>> RowOfAsync(string message)
  {
    using var scope = factory.Services.CreateScope();
    var connection = scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.GetDbConnection();

    await connection.OpenAsync();

    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT * FROM data_subject_requests WHERE message = @message";

    var parameter = command.CreateParameter();
    parameter.ParameterName = "message";
    parameter.Value = message;
    command.Parameters.Add(parameter);

    await using var reader = await command.ExecuteReaderAsync();

    (await reader.ReadAsync()).ShouldBeTrue("Aucune ligne n'a été enregistrée pour cette demande.");

    var row = Enumerable.Range(0, reader.FieldCount).ToDictionary(
      reader.GetName,
      column => reader.IsDBNull(column) ? null : reader.GetValue(column));

    (await reader.ReadAsync()).ShouldBeFalse("La demande a été enregistrée plus d'une fois.");

    return row;
  }

  private async Task<int> CountOfAsync(string message)
  {
    using var scope = factory.Services.CreateScope();

    return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database
      .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM data_subject_requests WHERE message = {message}")
      .SingleAsync();
  }

  private async Task<string> AntiforgeryTokenAsync()
  {
    var response = await _client.GetAsync(Board);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);

    var token = Regex.Match(
      await response.Content.ReadAsStringAsync(),
      @"<input name=""__RequestVerificationToken""[^>]*value=""([^""]+)""");

    token.Success.ShouldBeTrue("Le tableau des demandes ne porte aucun jeton anti-rejeu.");

    return token.Groups[1].Value;
  }
}
