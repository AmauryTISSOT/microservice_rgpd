using System.Net;
using FastEndpoints;

namespace MicroserviceRgpd.FunctionalTests.Screenings;

/// <summary>
/// <b>Aucune API ne détecte.</b> <c>Screening</c> ne déclare <b>aucune</b> route publique — pas même
/// une qui ferait entrer un relevé, là où <c>Casework</c> en garde une pour faire entrer une demande.
/// </summary>
/// <remarks>
/// <para>
/// Ce n'est pas une préférence d'architecture. Le seul chemin vers un rapport de détection est un
/// écran que
/// nous écrivons, sans quoi quelqu'un finirait par refaire chez lui l'écran « ✅ base analysée » —
/// où la détection se présente comme un recensement complet, et où une colonne non signalée ne
/// laisse aucune trace. C'est l'<c>Omission silencieuse</c> rétablie sans qu'une ligne de doctrine
/// n'ait été modifiée.
/// </para>
/// <para>
/// ⚠️ <b>Le contexte est plus strict que <c>Casework</c>, et c'est délibéré.</b> Là-bas, une route
/// fait <b>entrer</b> une demande, parce qu'une application tierce doit pouvoir en poster une. Ici
/// personne n'a de raison de poster un relevé : il n'existe qu'un seul acteur, l'<c>Operator</c>,
/// et il est déjà devant l'écran. La liste des endpoints de ce contexte est donc <b>vide</b>, écrite
/// en toutes lettres pour qu'en ajouter un soit un geste visible dans ce fichier.
/// </para>
/// <para>
/// La règle est gardée <b>deux fois</b> : par la liste des endpoints que l'application déclare, et
/// par le comportement HTTP des routes qu'on pourrait croire servies.
/// </para>
/// </remarks>
[Collection(WebCollection.Name)]
public class NoApiScreensADatabase(CustomWebApplicationFactory<Program> factory)
{
  private readonly HttpClient _client = factory.CreateClient();

  /// <summary>
  /// <b>Le contexte ne déclare aucun endpoint</b>, et la liste vide est écrite plutôt que déduite :
  /// le jour où quelqu'un en ajoute un, c'est ici qu'il faudra l'assumer.
  /// </summary>
  [Fact]
  public void DeclaresNoEndpointAtAllForTheWholeContext()
  {
    var endpoints = typeof(Program).Assembly.GetTypes()
      .Where(type => type.IsAssignableTo(typeof(IEndpoint)) && !type.IsAbstract)
      .Where(type => type.Namespace?.Contains("Screening", StringComparison.Ordinal) == true)
      .Select(type => type.FullName!)
      .Order(StringComparer.Ordinal)
      .ToArray();

    endpoints.ShouldBeEmpty(
      "Une route publique détecte. Le seul chemin vers un rapport de détection est la surface de "
      + "l'Operator : une API laisserait quelqu'un refaire chez lui l'écran « ✅ base analysée », où "
      + "une détection "
      + "se présente comme un recensement complet.");
  }

  /// <summary>
  /// <b>Rien ne dépose, ne relit, n'arbitre ni ne supprime un rapport de détection par une
  /// route</b>, et
  /// l'absence se constate sur le fil et non seulement dans une liste de types.
  /// </summary>
  [Theory]
  [InlineData("POST", "/screenings")]
  [InlineData("GET", "/screenings")]
  [InlineData("PUT", "/screenings")]
  [InlineData("DELETE", "/screenings")]
  [InlineData("GET", "/screenings/018f0000-0000-7000-8000-000000000000")]
  [InlineData("PATCH", "/screenings/018f0000-0000-7000-8000-000000000000")]
  [InlineData("DELETE", "/screenings/018f0000-0000-7000-8000-000000000000")]
  [InlineData("POST", "/screenings/018f0000-0000-7000-8000-000000000000/columns")]
  [InlineData("POST", "/listings")]
  [InlineData("POST", "/depistages")]
  public async Task ServesNoVerbThatWouldDepositReadOrArbitrateAScreening(string verb, string address)
  {
    var response = await _client.SendAsync(new HttpRequestMessage(new HttpMethod(verb), address));

    // Ni servie, ni refusée pour une autre raison : la route n'existe pas.
    response.StatusCode.ShouldBeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
  }

  /// <summary>
  /// ⚠️ <b>Et aucune route ne pré-remplit le <c>Manifest</c> depuis un rapport de détection.</b>
  /// C'est le geste
  /// que la clause <c>Aucune modification vers le Manifest</c> bannit nommément — un
  /// <c>Manifest</c> pré-rempli par une machine <b>se lirait comme complet</b>, ce qui est l'<c>Omission silencieuse</c> sous sa
  /// forme la plus dangereuse. La liste <em>Avoid</em> du glossaire dit d'elle-même qu'elle est le
  /// seul garde-fou qui accroche une revue de code sur une route de ce genre ; celui-ci la double.
  /// <para>
  /// <c>/manifest/prefill-from-screening</c> est éprouvé depuis que l'écran de révision
  /// <c>/manifest/{id}</c> a disparu avec le Manifest : le chemin n'est plus pris par
  /// <c>Casework</c>, et un 404 y dit bien qu'aucun pont n'existe. Ce que le pont aurait vraiment
  /// besoin d'ouvrir — une route qui <b>sort</b> les colonnes retenues — est éprouvé ci-dessous.
  /// </para>
  /// </summary>
  [Theory]
  [InlineData("POST", "/screenings/export")]
  [InlineData("GET", "/screenings/export")]
  [InlineData("GET", "/screenings/retained")]
  [InlineData("POST", "/screenings/prefill-manifest")]
  [InlineData("POST", "/manifest/prefill-from-screening")]
  public async Task ServesNoRouteThatWouldCarryADetectionAcrossIntoTheManifest(string verb, string address)
  {
    var response = await _client.SendAsync(new HttpRequestMessage(new HttpMethod(verb), address));

    response.StatusCode.ShouldBeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
  }

  /// <summary>
  /// La détection passe par la surface de l'<c>Operator</c>, qui est bien là : la règle ci-dessus
  /// serait vide de sens si aucun chemin n'existait — un service où personne ne peut détecter
  /// respecterait toutes les règles et ne servirait à rien.
  /// </summary>
  [Fact]
  public async Task LeavesTheOnlyPathToAScreeningOpenOnTheOperatorsSurface()
  {
    (await _client.GetAsync(ScreeningSurface.Deposit)).StatusCode.ShouldBe(HttpStatusCode.OK);
  }
}
