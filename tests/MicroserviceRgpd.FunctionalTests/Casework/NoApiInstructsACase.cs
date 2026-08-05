using System.Net;
using FastEndpoints;

namespace MicroserviceRgpd.FunctionalTests.Casework;

/// <summary>
/// <b>Aucune API n'instruit un <c>Case</c>.</b> La seule route publique qui le touche le fait
/// <b>entrer</b>, et rien d'autre : elle n'arbitre pas, ne motive pas, ne constate pas, ne clôt pas.
/// </summary>
/// <remarks>
/// <para>
/// Ce n'est pas une préférence d'architecture. Le seul chemin vers l'instruction est un écran que nous
/// écrivons, sans quoi l'incomplétude cesserait d'être visible par construction — et quelqu'un
/// finirait par refaire chez lui l'écran « ✅ demande traitée », où le recensement se présente comme
/// complet et où un système non traité ne laisse aucune trace.
/// </para>
/// <para>
/// La règle est gardée <b>deux fois</b> : par la liste des endpoints que l'application déclare — pour
/// qu'ajouter une route soit un geste visible dans ce fichier — et par le comportement HTTP des verbes
/// qu'on pourrait croire servis.
/// </para>
/// </remarks>
[Collection(WebCollection.Name)]
public class NoApiInstructsACase(CustomWebApplicationFactory<Program> factory)
{
  /// <summary>
  /// L'unique endpoint du contexte, écrit en toutes lettres. En ajouter un doit être un geste
  /// délibéré, discuté ici — pas une ligne de plus dans un dossier.
  /// </summary>
  private static readonly string[] TheOnlyEndpointThatTouchesACase = ["MicroserviceRgpd.Web.Casework.OpenCase"];

  private readonly HttpClient _client = factory.CreateClient();

  /// <summary>
  /// Le contexte ne déclare qu'un endpoint, et il fait <b>entrer</b> une demande. Aucun autre type de
  /// la couche web ne parle à un <c>Case</c> par une route.
  /// </summary>
  [Fact]
  public void DeclaresASingleEndpointForTheWholeContextAndItOnlyLetsARequestIn()
  {
    var endpoints = typeof(Program).Assembly.GetTypes()
      .Where(type => type.IsAssignableTo(typeof(IEndpoint)) && !type.IsAbstract)
      .Where(type => type.Namespace?.Contains("Casework", StringComparison.Ordinal) == true)
      .Select(type => type.FullName!)
      .Order(StringComparer.Ordinal)
      .ToArray();

    endpoints.ShouldBe(
      TheOnlyEndpointThatTouchesACase,
      "Une route publique de plus touche un Case. Le seul chemin vers l'instruction est la surface "
      + "de l'Operator : une API qui instruirait laisserait quelqu'un refaire chez lui l'écran "
      + "« ✅ demande traitée ».");
  }

  /// <summary>
  /// <b>Rien ne relit, ne modifie ni ne clôt un dossier par une route.</b> Pas de <c>GET</c> — aucune
  /// route publique ne relit un dossier —, pas de <c>PUT</c>, pas de <c>PATCH</c>, pas de
  /// <c>DELETE</c> : et l'absence se constate sur le fil, pas seulement dans une liste de types.
  /// </summary>
  [Theory]
  [InlineData("GET", "/cases")]
  [InlineData("PUT", "/cases")]
  [InlineData("PATCH", "/cases")]
  [InlineData("DELETE", "/cases")]
  [InlineData("GET", "/cases/018f0000-0000-7000-8000-000000000000")]
  [InlineData("PUT", "/cases/018f0000-0000-7000-8000-000000000000")]
  [InlineData("PATCH", "/cases/018f0000-0000-7000-8000-000000000000")]
  [InlineData("DELETE", "/cases/018f0000-0000-7000-8000-000000000000")]
  [InlineData("POST", "/cases/018f0000-0000-7000-8000-000000000000/steps")]
  [InlineData("POST", "/cases/018f0000-0000-7000-8000-000000000000/close")]
  public async Task ServesNoVerbThatWouldReadModifyOrCloseACase(string verb, string address)
  {
    var response = await _client.SendAsync(new HttpRequestMessage(new HttpMethod(verb), address));

    // Ni servi, ni refusé pour une autre raison : la route n'existe pas.
    response.StatusCode.ShouldBeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
  }

  /// <summary>
  /// L'instruction passe par la surface de l'<c>Operator</c>, qui est bien là : la règle ci-dessus
  /// serait vide de sens si aucun chemin n'existait — un service où personne ne peut instruire
  /// respecterait toutes les règles et ne servirait à rien.
  /// </summary>
  [Fact]
  public async Task LeavesTheOnlyPathToInstructionOpenOnTheOperatorsSurface()
  {
    (await _client.GetAsync("/dossiers")).StatusCode.ShouldBe(HttpStatusCode.OK);
  }
}
