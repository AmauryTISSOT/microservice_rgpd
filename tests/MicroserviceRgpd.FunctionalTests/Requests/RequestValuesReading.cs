using System.Net;
using System.Text.Json;
using NSwag.Generation;

namespace MicroserviceRgpd.FunctionalTests.Requests;

/// <summary>
/// <b>Lire les valeurs d'une demande</b>, exercé par la <b>seule frontière HTTP</b> : le handler
/// <c>GET /demandes?handler=Values</c> que le script de la modale appelle pour la pré-remplir,
/// frappé ici directement.
/// </summary>
/// <remarks>
/// ⚠️ <b>Lire n'est pas modifier</b> : le statut de la demande n'entre pas en compte, et aucune
/// trace n'est laissée. La base étant partagée sans purge, chaque demande garde son message unique
/// pour clé de retrouvage.
/// </remarks>
[Collection(WebCollection.Name)]
public class RequestValuesReading(CustomWebApplicationFactory<Program> factory)
{
  private readonly RequestSurface _surface = new(factory);

  /// <summary>
  /// <b>Les huit champs reviennent</b>, sous les clés mêmes du formulaire et aux valeurs
  /// enregistrées : de quoi rouvrir la modale sur cette demande sans rien réécrire.
  /// </summary>
  [Fact]
  public async Task AnswersTheEightFieldsOfARecordedRequest()
  {
    var fields = RequestSurface.AValidRequest();
    fields["origin"] = "Letter";
    fields["receivedOn"] = "2026-01-12";
    fields["lastName"] = "Martin";
    fields["firstName"] = "Jeanne";
    fields["email"] = "jeanne.martin@example.org";
    fields["identityVerified"] = "true";
    fields["right"] = "Erasure";

    var (id, message) = await _surface.RecordAsync(fields);

    var response = await _surface.ValuesOfAsync(id);

    response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

    var values = await ValuesOfAsync(response);

    values.Count.ShouldBe(8, "Le handler rend autre chose que les huit champs du formulaire.");
    values["origin"].GetString().ShouldBe("Letter");
    values["receivedOn"].GetString().ShouldBe("2026-01-12");
    values["lastName"].GetString().ShouldBe("Martin");
    values["firstName"].GetString().ShouldBe("Jeanne");
    values["email"].GetString().ShouldBe("jeanne.martin@example.org");
    values["identityVerified"].GetBoolean().ShouldBeTrue();
    values["message"].GetString().ShouldBe(message);
    values["right"].GetString().ShouldBe("Erasure");
  }

  /// <summary>
  /// <b>Un champ facultatif laissé vide revient vide</b>, et non absent : le formulaire a huit champs
  /// à remplir, qu'il y ait eu saisie ou non.
  /// </summary>
  [Fact]
  public async Task AnswersNothingForTheOptionalFieldsLeftEmpty()
  {
    // Identifiée par ses seuls nom et prénom : c'est l'email qui manque, là où la saisie de départ
    // est identifiée par son seul email et laisse le nom et le prénom vides.
    var fields = RequestSurface.AValidRequest();
    fields["email"] = "";
    fields["lastName"] = "Martin";
    fields["firstName"] = "Jeanne";

    var byNames = await ReadValuesOfAsync(await _surface.RecordAsync(fields));
    var byEmail = await ReadValuesOfAsync(await _surface.RecordAsync());

    byNames["email"].ValueKind.ShouldBe(JsonValueKind.Null);
    byEmail["lastName"].ValueKind.ShouldBe(JsonValueKind.Null);
    byEmail["firstName"].ValueKind.ShouldBe(JsonValueKind.Null);
  }

  /// <summary>
  /// ⚠️ <b>Rien n'est gardé</b> : la modale relit ces valeurs après chaque modification, et un cache
  /// de navigateur lui rendrait celles d'avant.
  /// </summary>
  [Fact]
  public async Task KeepsTheValuesOutOfEveryCache()
  {
    var (id, _) = await _surface.RecordAsync();

    var response = await _surface.ValuesOfAsync(id);

    response.Headers.CacheControl
      .ShouldNotBeNull("La réponse ne dit rien de sa mise en cache.")
      .NoStore.ShouldBeTrue("Les valeurs d'une demande sont mises en cache.");
  }

  /// <summary>
  /// ⚠️ <b>Une demande close se lit comme une autre</b> — 200. Refuser la lecture ferait de ce point
  /// de consultation le gardien d'une règle d'écriture, et fermerait tout futur écran de consultation.
  /// </summary>
  [Theory]
  [InlineData("Completed")]
  [InlineData("Cancelled")]
  public async Task AnswersTheValuesOfAClosedRequest(string status)
  {
    var (id, message) = await _surface.RecordAsync();
    await _surface.SetStatusAsync(id, status);

    var response = await _surface.ValuesOfAsync(id);

    response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    (await ValuesOfAsync(response))["message"].GetString().ShouldBe(message);
  }

  /// <summary>
  /// <b>Une demande qui n'existe plus rend 404</b> — un second onglet l'a supprimée entre-temps.
  /// </summary>
  [Fact]
  public async Task AnswersNotFoundForARequestThatDoesNotExist()
  {
    var (id, _) = await _surface.RecordAsync();
    (await _surface.DeleteAsync(id)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

    var response = await _surface.ValuesOfAsync(id);

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  /// <summary>
  /// <b>Un identifiant qui n'en est pas un est refusé</b> — 400, et non 404 : l'écran ne demande que
  /// les identifiants qu'il a rendus, et ce n'est pas une demande introuvable.
  /// </summary>
  [Theory]
  [InlineData("")]
  [InlineData("pas-un-guid")]
  [InlineData("00000000-0000-0000-0000-000000000000")]
  public async Task RefusesAnIdThatIsNotOne(string id)
  {
    var response = await _surface.ValuesOfAsync(id);

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  /// <summary>
  /// ⚠️ <b>La lecture des valeurs n'est pas une API</b> : le document Swagger ne publie ni sa route,
  /// ni son contrat.
  /// </summary>
  [Fact]
  public async Task TheApiDocumentDoesNotPublishTheValues()
  {
    using var scope = factory.Services.CreateScope();
    var document = await scope.ServiceProvider.GetRequiredService<IOpenApiDocumentGenerator>().GenerateAsync("v1");
    var published = document.ToJson();

    document.Paths.Keys.ShouldContain("/qualifications", "Le document ne publie plus rien : l'assertion suivante serait vide.");
    published.ShouldNotContain("handler=Values", Case.Insensitive, "Le document Swagger publie la lecture d'une demande.");
    published.ShouldNotContain("DataSubjectRequestValues", Case.Insensitive, "Le document Swagger publie la lecture d'une demande.");
  }

  /// <summary>Les valeurs de la demande enregistrée, la réponse ayant d'abord été reconnue 200.</summary>
  private async Task<IReadOnlyDictionary<string, JsonElement>> ReadValuesOfAsync((Guid Id, string Message) recorded)
  {
    var response = await _surface.ValuesOfAsync(recorded.Id);

    response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

    return await ValuesOfAsync(response);
  }

  /// <summary>Le corps de la réponse, champ par champ, sous les clés mêmes du formulaire.</summary>
  private static async Task<IReadOnlyDictionary<string, JsonElement>> ValuesOfAsync(HttpResponseMessage response)
  {
    response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");

    using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    return body.RootElement.EnumerateObject().ToDictionary(field => field.Name, field => field.Value.Clone());
  }
}
