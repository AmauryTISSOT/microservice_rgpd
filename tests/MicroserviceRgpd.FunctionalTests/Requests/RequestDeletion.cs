using System.Net;
using NSwag.Generation;

namespace MicroserviceRgpd.FunctionalTests.Requests;

/// <summary>
/// <b>Supprimer une demande</b>, exercé par la <b>seule frontière HTTP</b> : le handler
/// <c>POST /demandes?handler=Delete</c> que le script de la confirmation appelle, frappé ici
/// directement, jeton anti-rejeu compris — exactement ce que fait le navigateur.
/// </summary>
/// <remarks>
/// ⚠️ <b>La suppression ne laisse aucune trace</b> (ADR-0022) : ce qui se vérifie ici est que la
/// ligne a quitté la table, pas qu'une autre l'a remplacée.
/// </remarks>
[Collection(RequestsWebCollection.Name)]
public class RequestDeletion(CustomWebApplicationFactory<Program> factory)
{
  private readonly RequestSurface _surface = new(factory);

  /// <summary>
  /// <b>Une demande enregistrée se supprime</b> : le serveur répond 204, et la ligne n'est plus en
  /// base.
  /// </summary>
  [Fact]
  public async Task AnswersNoContentAndRemovesTheRow()
  {
    var (id, message) = await _surface.RecordAsync();

    var response = await _surface.DeleteAsync(id);

    response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
    (await _surface.CountOfAsync(message)).ShouldBe(0);
  }

  /// <summary>
  /// <b>Une demande qui n'existe plus rend 404</b> — un second onglet l'a déjà supprimée. L'écran le
  /// lit comme une réussite (ADR-0022).
  /// </summary>
  [Fact]
  public async Task AnswersNotFoundForARequestAlreadyDeleted()
  {
    var (id, _) = await _surface.RecordAsync();
    (await _surface.DeleteAsync(id)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

    var response = await _surface.DeleteAsync(id);

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  /// <summary>
  /// <b>Un identifiant qui n'en est pas un est refusé</b> — 400 : l'écran ne poste que ceux qu'il a
  /// rendus, et ce n'est pas une demande introuvable, que l'écran lirait comme une réussite.
  /// </summary>
  [Theory]
  [InlineData("")]
  [InlineData("pas-un-guid")]
  [InlineData("00000000-0000-0000-0000-000000000000")]
  public async Task RefusesAnIdThatIsNotOne(string id)
  {
    var response = await _surface.DeleteAsync(id);

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  /// <summary>
  /// ⚠️ <b>Sans jeton anti-rejeu, rien n'est supprimé</b> : une page tierce qui ferait poster le
  /// navigateur de l'<c>Operator</c> n'atteint pas le handler.
  /// </summary>
  [Fact]
  public async Task DeletesNothingWithoutTheAntiforgeryToken()
  {
    var (id, message) = await _surface.RecordAsync();

    var response = await _surface.DeleteWithoutTokenAsync(id);

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    (await _surface.CountOfAsync(message)).ShouldBe(1);
  }

  /// <summary>
  /// <b>Une demande se supprime quel que soit son statut</b> — posé en base, puisqu'aucun geste ne le
  /// fait encore changer.
  /// </summary>
  [Theory]
  [InlineData("InProgress")]
  [InlineData("Completed")]
  [InlineData("Cancelled")]
  public async Task DeletesARequestWhateverItsStatus(string status)
  {
    var (id, message) = await _surface.RecordAsync();
    await _surface.SetStatusAsync(id, status);

    var response = await _surface.DeleteAsync(id);

    response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
    (await _surface.CountOfAsync(message)).ShouldBe(0);
  }

  /// <summary>
  /// ⚠️ <b>La suppression n'est pas une API</b> : le document Swagger ne publie ni sa route, ni son
  /// contrat.
  /// </summary>
  [Fact]
  public async Task TheApiDocumentDoesNotPublishTheDeletion()
  {
    using var scope = factory.Services.CreateScope();
    var document = await scope.ServiceProvider.GetRequiredService<IOpenApiDocumentGenerator>().GenerateAsync("v1");
    var published = document.ToJson();

    document.Paths.Keys.ShouldContain("/qualifications", "Le document ne publie plus rien : l'assertion suivante serait vide.");
    published.ShouldNotContain("handler=Delete", Case.Insensitive, "Le document Swagger publie la suppression d'une demande.");
    published.ShouldNotContain("DeleteDataSubjectRequest", Case.Insensitive, "Le document Swagger publie la suppression d'une demande.");
  }
}
