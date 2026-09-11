using System.Globalization;
using System.Net;
using System.Text.Json;
using MicroserviceRgpd.Core.Requests;

namespace MicroserviceRgpd.FunctionalTests.Requests;

/// <summary>
/// <b>Le serveur fait foi</b> : une saisie que <see cref="DataSubjectRequest.Receive"/> refuse,
/// envoyée directement au handler <c>POST /demandes?handler=Create</c> — sans passer par la
/// validation du navigateur, qui n'est qu'un confort —, reçoit <b>400 <c>ValidationProblem</c></b>
/// et n'enregistre rien.
/// </summary>
/// <remarks>
/// <para>
/// Chaque refus s'écrit <c>clé : message</c> — la clé du corps sous laquelle l'écran placera le
/// message, et le message tel que le serveur l'a écrit une seule fois, dans
/// <see cref="DataSubjectRequestMessages"/>. La réponse doit porter <b>exactement</b> ces refus : ni
/// un de moins, ni un de plus.
/// </para>
/// <para>
/// ⚠️ <b>« Rien n'est enregistré » se compte sur toute la table</b>, avant et après : une saisie
/// refusée n'a souvent pas de message qui la distinguerait des autres.
/// </para>
/// </remarks>
[Collection(WebCollection.Name)]
public class RequestRefusal(CustomWebApplicationFactory<Program> factory)
{
  private readonly CreationHandler _handler = new(factory);

  /// <summary>
  /// <b>Ni email, ni nom et prénom ensemble</b> : le message d'identification va sous l'email et sous
  /// chacun des champs nom et prénom qui manquent — jamais sous celui qui est renseigné. Des espaces
  /// ne renseignent rien.
  /// </summary>
  [Theory]
  [InlineData("Martin", "", "", new[] { "email", "firstName" })]
  [InlineData("", "Jeanne", "", new[] { "email", "lastName" })]
  [InlineData("", "", "", new[] { "email", "lastName", "firstName" })]
  [InlineData("  ", "\t", " \n ", new[] { "email", "lastName", "firstName" })]
  [InlineData("Martin", "   ", "   ", new[] { "email", "firstName" })]
  public async Task RefusesAPersonIdentifiedNeitherByEmailNorByFullName(
    string lastName, string firstName, string email, string[] keys)
  {
    var fields = CreationHandler.AValidRequest();
    fields["lastName"] = lastName;
    fields["firstName"] = firstName;
    fields["email"] = email;

    var refusals = await RefusalsOfAsync(fields);

    refusals.ShouldBe(
      keys.Select(key => $"{key} : {DataSubjectRequestMessages.IdentificationMissing}"),
      ignoreOrder: true);
  }

  /// <summary>
  /// <b>Chaque champ qui enfreint sa règle est refusé sous sa clé</b>, avec le message que le serveur
  /// a écrit pour elle. Un champ absent du corps, vide ou fait d'espaces est un champ vide ; une
  /// date qui n'est pas au format ISO du fil est mal formée, et non absente.
  /// </summary>
  /// <param name="key">La clé du corps ; <paramref name="value"/> <c>null</c> la retire du corps.</param>
  /// <param name="value">La valeur envoyée.</param>
  /// <param name="message">Le seul refus attendu, sous <paramref name="key"/>.</param>
  [Theory]
  [InlineData("receivedOn", null, DataSubjectRequestMessages.ReceivedOnMissing)]
  [InlineData("receivedOn", "", DataSubjectRequestMessages.ReceivedOnMissing)]
  [InlineData("receivedOn", "   ", DataSubjectRequestMessages.ReceivedOnMissing)]
  [InlineData("receivedOn", "15/01/2026", DataSubjectRequestMessages.ReceivedOnMalformed)]
  [InlineData("receivedOn", "2026-1-15", DataSubjectRequestMessages.ReceivedOnMalformed)]
  [InlineData("receivedOn", "2026-02-30", DataSubjectRequestMessages.ReceivedOnMalformed)]
  [InlineData("receivedOn", "2026-01-15T00:00:00", DataSubjectRequestMessages.ReceivedOnMalformed)]
  [InlineData("receivedOn", "hier", DataSubjectRequestMessages.ReceivedOnMalformed)]
  [InlineData("email", "jeanne.martin@", DataSubjectRequestMessages.EmailInvalid)]
  [InlineData("email", "jeanne martin@example.org", DataSubjectRequestMessages.EmailInvalid)]
  [InlineData("email", "jeanne.martin.example.org", DataSubjectRequestMessages.EmailInvalid)]
  [InlineData("message", null, DataSubjectRequestMessages.MessageMissing)]
  [InlineData("message", "", DataSubjectRequestMessages.MessageMissing)]
  [InlineData("message", " \t\n ", DataSubjectRequestMessages.MessageMissing)]
  [InlineData("right", null, DataSubjectRequestMessages.RightMissing)]
  [InlineData("right", "", DataSubjectRequestMessages.RightMissing)]
  [InlineData("right", "   ", DataSubjectRequestMessages.RightMissing)]
  [InlineData("right", "OutOfScope", DataSubjectRequestMessages.RightMissing)]
  [InlineData("right", "Consent", DataSubjectRequestMessages.RightMissing)]
  public async Task RefusesEachFieldThatBreaksItsRule(string key, string? value, string message)
  {
    var fields = CreationHandler.AValidRequest();

    if (value is null)
    {
      fields.Remove(key);
    }
    else
    {
      fields[key] = value;
    }

    var refusals = await RefusalsOfAsync(fields);

    refusals.ShouldBe([$"{key} : {message}"]);
  }

  /// <summary>
  /// <b>Un caractère au-delà du plafond suffit</b> : le nom et le prénom à 101, l'email à 255, le
  /// message à 10 001. Le serveur ne tronque rien, il refuse.
  /// </summary>
  [Theory]
  [InlineData("lastName", 101, DataSubjectRequestMessages.LastNameTooLong)]
  [InlineData("firstName", 101, DataSubjectRequestMessages.FirstNameTooLong)]
  [InlineData("email", 255, DataSubjectRequestMessages.EmailInvalid)]
  [InlineData("message", 10_001, DataSubjectRequestMessages.MessageTooLong)]
  public async Task RefusesEachValueOverItsCeiling(string key, int length, string message)
  {
    var fields = CreationHandler.AValidRequest();
    fields[key] = key == "email"
      ? new string('j', length - "@example.org".Length) + "@example.org"
      : new string('a', length);

    var refusals = await RefusalsOfAsync(fields);

    refusals.ShouldBe([$"{key} : {message}"]);
  }

  /// <summary>
  /// ⚠️ <b>« Aujourd'hui » est celui de Paris, pas celui de l'UTC.</b> À 23 h 30 UTC, il est déjà le
  /// lendemain à Paris, l'été comme l'hiver : ce lendemain est une date de réception acceptée, et le
  /// jour d'après est refusé comme future.
  /// </summary>
  /// <remarks>
  /// L'horloge du service est <b>avancée</b> jusqu'au prochain 23 h 30 UTC, et remise à l'heure
  /// réelle quoi qu'il arrive.
  /// </remarks>
  [Fact]
  public async Task RefusesADateAfterTodayInParisWhileItIsStillEarlierInUtc()
  {
    var now = factory.Clock.GetUtcNow();
    var lateEvening = new DateTimeOffset(now.UtcDateTime.Date.AddHours(23).AddMinutes(30), TimeSpan.Zero);
    if (lateEvening <= now)
    {
      lateEvening = lateEvening.AddDays(1);
    }

    factory.Clock.Advance(lateEvening - now);

    try
    {
      var todayInParis = DateOnly.FromDateTime(lateEvening.UtcDateTime).AddDays(1);
      ParisCalendar.Today(factory.Clock).ShouldBe(todayInParis, "L'horloge du service n'a pas été avancée comme prévu.");

      var sameDay = CreationHandler.AValidRequest();
      sameDay["receivedOn"] = Iso(todayInParis);

      var accepted = await _handler.CreateAsync(sameDay);

      accepted.StatusCode.ShouldBe(HttpStatusCode.Created, await accepted.Content.ReadAsStringAsync());

      var nextDay = CreationHandler.AValidRequest();
      nextDay["receivedOn"] = Iso(todayInParis.AddDays(1));

      var refusals = await RefusalsOfAsync(nextDay);

      refusals.ShouldBe([$"receivedOn : {DataSubjectRequestMessages.ReceivedOnInTheFuture}"]);
    }
    finally
    {
      factory.Clock.Reset();
    }
  }

  /// <summary>
  /// <b>Une saisie qui cumule les fautes les reçoit toutes, en une seule réponse</b> — chacune sous
  /// sa clé : le serveur ne s'arrête pas à la première.
  /// </summary>
  [Fact]
  public async Task ReturnsEveryRefusalOfAnEntryThatBreaksSeveralRules()
  {
    var refusals = await RefusalsOfAsync(new Dictionary<string, string>
    {
      ["origin"] = "Letter",
      ["receivedOn"] = "31/12/2026",
      ["lastName"] = new string('a', 101),
      ["firstName"] = "  ",
      ["email"] = "",
      ["identityVerified"] = "true",
      ["message"] = "",
      ["right"] = "OutOfScope",
    });

    refusals.ShouldBe(
      [
        $"receivedOn : {DataSubjectRequestMessages.ReceivedOnMalformed}",
        $"lastName : {DataSubjectRequestMessages.LastNameTooLong}",
        $"firstName : {DataSubjectRequestMessages.IdentificationMissing}",
        $"email : {DataSubjectRequestMessages.IdentificationMissing}",
        $"message : {DataSubjectRequestMessages.MessageMissing}",
        $"right : {DataSubjectRequestMessages.RightMissing}",
      ],
      ignoreOrder: true);
  }

  /// <summary>
  /// ⚠️ <b>Une origine hors des deux canaux n'est pas une saisie, c'est un envoi forgé</b> : la modale
  /// ne propose que <c>Email</c> et <c>Letter</c>. Il est refusé sous <c>origin</c>, sans rien
  /// enregistrer.
  /// </summary>
  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("Phone")]
  public async Task RefusesAnOriginOutsideTheTwoChannels(string? origin)
  {
    var fields = CreationHandler.AValidRequest();

    if (origin is null)
    {
      fields.Remove("origin");
    }
    else
    {
      fields["origin"] = origin;
    }

    var refusals = await RefusalsOfAsync(fields);

    refusals.ShouldHaveSingleItem().ShouldStartWith("origin : ");
  }

  /// <summary>
  /// Envoie la saisie, vérifie qu'elle est refusée en <c>400 ValidationProblem</c> <b>sans que rien
  /// n'ait été enregistré</b>, et rend ses refus sous la forme <c>clé : message</c>.
  /// </summary>
  private async Task<string[]> RefusalsOfAsync(IReadOnlyDictionary<string, string> fields)
  {
    var before = await _handler.CountAsync();

    var response = await _handler.CreateAsync(fields);
    var body = await response.Content.ReadAsStringAsync();

    (await _handler.CountAsync()).ShouldBe(before, "Une saisie refusée a été enregistrée.");

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
    response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

    using var document = JsonDocument.Parse(body);

    document.RootElement.GetProperty("status").GetInt32().ShouldBe((int)HttpStatusCode.BadRequest);

    return
    [
      .. document.RootElement.GetProperty("errors").EnumerateObject().SelectMany(field =>
        field.Value.EnumerateArray().Select(message => $"{field.Name} : {message.GetString()}")),
    ];
  }

  private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
