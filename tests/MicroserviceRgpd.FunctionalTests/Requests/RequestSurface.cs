using System.Net;
using System.Text.RegularExpressions;
using MicroserviceRgpd.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MicroserviceRgpd.FunctionalTests.Requests;

/// <summary>
/// Les handlers de <c>/demandes</c> — <c>POST ?handler=Create</c>, <c>POST ?handler=Modify</c>,
/// <c>POST ?handler=Delete</c> et <c>GET ?handler=Values</c> —, <b>frappés directement</b> : avec le
/// jeton anti-rejeu que la page rend et le cookie qui va avec — exactement ce que fait le
/// navigateur —, et la table <c>data_subject_requests</c> relue telle qu'ils l'ont laissée.
/// </summary>
internal sealed class RequestSurface(CustomWebApplicationFactory<Program> factory)
{
  internal const string Board = "/demandes";

  internal const string Create = "/demandes?handler=Create";

  internal const string Modify = "/demandes?handler=Modify";

  internal const string Delete = "/demandes?handler=Delete";

  internal const string Values = "/demandes?handler=Values";

  private readonly HttpClient _client = factory.CreateClient(
    new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

  /// <summary>Une saisie complète et valide, identifiée par son seul email.</summary>
  internal static Dictionary<string, string> AValidRequest() => new()
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

  /// <summary>Envoie la saisie au handler de création, jeton anti-rejeu compris.</summary>
  internal async Task<HttpResponseMessage> CreateAsync(IReadOnlyDictionary<string, string> fields)
  {
    return await _client.PostAsync(Create, new FormUrlEncodedContent(
    [
      new("__RequestVerificationToken", await AntiforgeryTokenAsync()),
      .. fields,
    ]));
  }

  /// <summary>
  /// Envoie la saisie <b>sans</b> jeton anti-rejeu — ce que ferait une page tierce qui ferait poster
  /// le navigateur de l'<c>Operator</c>.
  /// </summary>
  internal async Task<HttpResponseMessage> CreateWithoutTokenAsync(IReadOnlyDictionary<string, string> fields)
  {
    return await _client.PostAsync(Create, new FormUrlEncodedContent(fields));
  }

  /// <summary>
  /// Envoie la correction au handler de modification, jeton anti-rejeu compris. L'identifiant de la
  /// demande va <b>en paramètre du handler</b>, comme pour la suppression : ce n'est pas une saisie.
  /// </summary>
  internal Task<HttpResponseMessage> ModifyAsync(Guid id, IReadOnlyDictionary<string, string> fields) =>
    ModifyAsync(id.ToString(), fields);

  /// <summary>Envoie la correction sous cet identifiant brut, jeton anti-rejeu compris.</summary>
  internal async Task<HttpResponseMessage> ModifyAsync(string id, IReadOnlyDictionary<string, string> fields)
  {
    return await _client.PostAsync(Modify, new FormUrlEncodedContent(
    [
      new("__RequestVerificationToken", await AntiforgeryTokenAsync()),
      new("id", id),
      .. fields,
    ]));
  }

  /// <summary>
  /// Envoie la correction <b>sans</b> jeton anti-rejeu — ce que ferait une page tierce qui ferait
  /// poster le navigateur de l'<c>Operator</c>.
  /// </summary>
  internal async Task<HttpResponseMessage> ModifyWithoutTokenAsync(
    Guid id,
    IReadOnlyDictionary<string, string> fields)
  {
    return await _client.PostAsync(Modify, new FormUrlEncodedContent(
    [
      new("id", id.ToString()),
      .. fields,
    ]));
  }

  /// <summary>
  /// Enregistre une demande valide — les valeurs de <paramref name="fields"/> posées par-dessus — et
  /// rend son identifiant, relu en base par son message unique.
  /// </summary>
  internal async Task<(Guid Id, string Message)> RecordAsync(IReadOnlyDictionary<string, string>? fields = null)
  {
    var request = AValidRequest();

    foreach (var (key, value) in fields ?? new Dictionary<string, string>())
    {
      request[key] = value;
    }

    var response = await CreateAsync(request);

    response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

    return ((await RowOfAsync(request["message"]))["id"].ShouldBeOfType<Guid>(), request["message"]);
  }

  /// <summary>Demande les valeurs saisies de la demande <paramref name="id"/>.</summary>
  internal Task<HttpResponseMessage> ValuesOfAsync(Guid id) => ValuesOfAsync(id.ToString());

  /// <summary>
  /// Demande les valeurs saisies sous cet identifiant brut. ⚠️ <b>Aucun jeton anti-rejeu</b> : c'est
  /// un GET, il ne change rien.
  /// </summary>
  internal async Task<HttpResponseMessage> ValuesOfAsync(string id) =>
    await _client.GetAsync($"{Values}&id={Uri.EscapeDataString(id)}");

  /// <summary>Demande la suppression de la demande <paramref name="id"/>, jeton anti-rejeu compris.</summary>
  internal Task<HttpResponseMessage> DeleteAsync(Guid id) => DeleteAsync(id.ToString());

  /// <summary>Demande la suppression sous cet identifiant brut, jeton anti-rejeu compris.</summary>
  internal async Task<HttpResponseMessage> DeleteAsync(string id)
  {
    return await _client.PostAsync(Delete, new FormUrlEncodedContent(
    [
      new("__RequestVerificationToken", await AntiforgeryTokenAsync()),
      new("id", id),
    ]));
  }

  /// <summary>Demande la suppression de la demande <paramref name="id"/> <b>sans</b> jeton anti-rejeu.</summary>
  internal async Task<HttpResponseMessage> DeleteWithoutTokenAsync(Guid id)
  {
    return await _client.PostAsync(Delete, new FormUrlEncodedContent([new("id", id.ToString())]));
  }

  /// <summary>
  /// Pose le statut de la demande <b>en base</b>, sous le nom de la valeur : aucun geste ne le fait
  /// encore changer.
  /// </summary>
  internal async Task SetStatusAsync(Guid id, string status)
  {
    using var scope = factory.Services.CreateScope();

    var updated = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database
      .ExecuteSqlAsync($"UPDATE data_subject_requests SET status = {status} WHERE id = {id}");

    updated.ShouldBe(1, "Le statut n'a été posé sur aucune demande.");
  }

  /// <summary>
  /// Rend la saisie avec <paramref name="key"/> posée à <paramref name="value"/>, ou retirée du corps
  /// si <paramref name="value"/> est <c>null</c>.
  /// </summary>
  internal static Dictionary<string, string> With(Dictionary<string, string> fields, string key, string? value)
  {
    if (value is null)
    {
      fields.Remove(key);
    }
    else
    {
      fields[key] = value;
    }

    return fields;
  }

  /// <summary>
  /// La ligne de la demande, relue <b>telle que la table la porte</b> — colonne par colonne, sous
  /// leur nom SQL, sans passer par le modèle EF qui l'a écrite.
  /// </summary>
  internal async Task<IReadOnlyDictionary<string, object?>> RowOfAsync(string message)
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

  /// <summary>Le nombre de demandes enregistrées sous ce message.</summary>
  internal async Task<int> CountOfAsync(string message) =>
    await CountAsync($"SELECT count(*)::int AS \"Value\" FROM data_subject_requests WHERE message = {message}");

  /// <summary>
  /// Le nombre de demandes enregistrées, <b>toutes confondues</b> — pour qui doit prouver que rien
  /// n'a été écrit par une saisie sans message qui la distingue.
  /// </summary>
  /// <remarks>
  /// ⚠️ Le compte ne vaut que parce que toute la collection <see cref="WebCollection"/> s'exécute en
  /// série : aucun test voisin n'enregistre de demande entre deux lectures.
  /// </remarks>
  internal async Task<int> CountAllAsync() =>
    await CountAsync($"SELECT count(*)::int AS \"Value\" FROM data_subject_requests");

  /// <summary>
  /// Retire <b>toutes</b> les demandes enregistrées — pour qui doit lire le tableau vide.
  /// </summary>
  /// <remarks>
  /// ⚠️ Ne vaut que parce que toute la collection <see cref="WebCollection"/> s'exécute en série, et
  /// qu'aucun test n'y compte sur une demande qu'il n'a pas créée lui-même.
  /// </remarks>
  internal async Task DeleteAllAsync()
  {
    using var scope = factory.Services.CreateScope();

    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database
      .ExecuteSqlAsync($"DELETE FROM data_subject_requests");
  }

  /// <summary>
  /// Pose la date limite de réponse de la demande <b>à même la table</b> — pour qui doit prouver
  /// qu'elle se lit telle qu'elle est tenue, sans être recalculée.
  /// </summary>
  internal async Task SetResponseDeadlineAsync(Guid id, DateOnly responseDeadline)
  {
    using var scope = factory.Services.CreateScope();

    var updated = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database
      .ExecuteSqlAsync($"UPDATE data_subject_requests SET response_deadline = {responseDeadline} WHERE id = {id}");

    updated.ShouldBe(1, "La date limite n'a été posée sur aucune demande.");
  }

  /// <summary>
  /// La ligne du tableau qui porte <paramref name="marker"/>, une seule, <b>telle que le serveur la
  /// rend</b> dans <c>GET /demandes</c> — de <c>&lt;tr</c> à <c>&lt;/tr&gt;</c>.
  /// </summary>
  internal async Task<string> BoardRowWithAsync(string marker)
  {
    var response = await _client.GetAsync(Board);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);

    var body = Regex.Match(await response.Content.ReadAsStringAsync(), @"<tbody\b[^>]*>(.*?)</tbody>", RegexOptions.Singleline);

    body.Success.ShouldBeTrue("Le tableau ne porte aucun corps.");

    return Regex.Matches(body.Groups[1].Value, @"<tr\b[^>]*>.*?</tr>", RegexOptions.Singleline)
      .Where(row => row.Value.Contains(marker, StringComparison.Ordinal))
      .ShouldHaveSingleItem($"Le tableau ne porte pas la ligne de « {marker} », une fois.")
      .Value;
  }

  private async Task<int> CountAsync(FormattableString query)
  {
    using var scope = factory.Services.CreateScope();

    return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database
      .SqlQuery<int>(query)
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
