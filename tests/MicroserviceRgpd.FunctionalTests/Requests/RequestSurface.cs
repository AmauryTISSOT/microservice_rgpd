using System.Net;
using System.Text.RegularExpressions;
using MicroserviceRgpd.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MicroserviceRgpd.FunctionalTests.Requests;

/// <summary>
/// Le handler <c>POST /demandes?handler=Create</c>, <b>frappé directement</b> : avec le jeton
/// anti-rejeu que la page rend et le cookie qui va avec — exactement ce que fait le navigateur —, et
/// la table <c>data_subject_requests</c> relue telle qu'il l'a laissée.
/// </summary>
internal sealed class RequestSurface(CustomWebApplicationFactory<Program> factory)
{
  internal const string Board = "/demandes";

  internal const string Create = "/demandes?handler=Create";

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
