using System.Net;
using System.Net.Http.Json;
using FastEndpoints;
using NSwag.Generation;

namespace MicroserviceRgpd.FunctionalTests.Platform;

/// <summary>
/// <b>L'ancienne surface du tableau des dossiers est retirée</b> — ses trois écrans, sa route
/// publique et le contrat qu'elle publiait dans la documentation d'API. Le Tableau des demandes
/// RGPD vit désormais à <c>/demandes</c>, dans le contexte <c>Requests</c>.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Une ancienne adresse rend un 404, et rien d'autre</b> : ni une redirection vers
/// <c>/demandes</c>, qui ferait croire qu'un dossier d'hier y a trouvé sa suite, ni un écran
/// d'excuse. L'adresse n'existe plus, et le fil le dit.
/// </para>
/// <para>
/// L'absence est gardée <b>deux fois</b> : par le comportement HTTP des adresses qu'on pourrait
/// croire servies, et par le document Swagger que l'application publie, là où un endpoint oublié
/// se lirait encore comme un contrat.
/// </para>
/// </remarks>
[Collection(WebCollection.Name)]
public class NoRetiredBoardSurfaceRemains(CustomWebApplicationFactory<Program> factory)
{
  private readonly HttpClient _client = factory.CreateClient(
    new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

  /// <summary>
  /// <b>Les trois écrans du tableau des dossiers rendent un 404</b> — la file, le dépôt et le
  /// dossier, celui-ci sous une identité qui aurait été bien formée.
  /// </summary>
  [Theory]
  [InlineData("/dossiers")]
  [InlineData("/dossiers/depot")]
  [InlineData("/dossiers/018f0000-0000-7000-8000-000000000000")]
  public async Task TheRetiredBoardScreensAnswerNotFound(string address)
  {
    var response = await _client.GetAsync(address);

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound, $"L'adresse {address} répond encore.");
  }

  /// <summary>
  /// <b>Aucune route ne fait plus entrer une demande par <c>POST /cases</c></b>, et l'absence se
  /// constate sur le fil : un corps bien formé de l'ancien contrat n'y trouve personne.
  /// </summary>
  [Fact]
  public async Task NoRouteLetsARequestInThroughPostCasesAnymore()
  {
    var response = await _client.PostAsJsonAsync("/cases", new { designations = Array.Empty<object>(), rights = new[] { "Access" } });

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  /// <summary>
  /// ⚠️ <b>Le document Swagger ne publie plus ni la route, ni son contrat.</b> Le document est
  /// généré par l'application elle-même, hors de toute route : Swagger et Scalar ne sont montés
  /// qu'en développement, mais c'est ce même générateur qui les nourrit.
  /// </summary>
  [Fact]
  public async Task TheApiDocumentNoLongerPublishesThePostCasesContract()
  {
    using var scope = factory.Services.CreateScope();
    var document = await scope.ServiceProvider.GetRequiredService<IOpenApiDocumentGenerator>().GenerateAsync("v1");
    var published = document.ToJson();

    document.Paths.Keys.ShouldNotContain("/cases");
    document.Paths.Keys.ShouldContain("/qualifications", "Le document ne publie plus rien : l'assertion ci-dessus serait vide.");

    foreach (var word in new[] { "OpenCase", "DeclaredDesignation", "DeclaredSystem" })
    {
      published.ShouldNotContain(word, Case.Sensitive, $"Le document Swagger publie encore « {word} ».");
    }
  }
}
