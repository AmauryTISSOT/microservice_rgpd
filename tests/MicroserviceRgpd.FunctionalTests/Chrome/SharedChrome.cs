using System.Net;

namespace MicroserviceRgpd.FunctionalTests.Chrome;

/// <summary>
/// Le chrome partagé des onze écrans de la surface : une feuille de style et une police que <b>le service sert
/// lui-même</b>, et aucune ressource tierce.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Le test le plus important de ce fichier est
/// <see cref="LoadsNothingFromAThirdPartyOnAnyScreen"/>.</b> Le reste garde que le chrome arrive ;
/// celui-là garde <b>d'où</b> il arrive. Un service qui outille le RGPD ne peut pas faire fuiter
/// l'adresse IP de ses utilisateurs vers un hébergeur de polices pour afficher une page — et une
/// feuille chargée chez un tiers le ferait à chaque écran, sans que rien ne se voie.
/// </para>
/// <para>
/// ⚠️ <b>Aucun de ces tests n'asserte une valeur de design</b>, et aucun ne doit jamais en asserter
/// une. Comparer des hexadécimaux reviendrait à recopier la feuille dans le test : ça décrirait
/// l'implémentation, ça casserait à chaque retouche, et ça n'attraperait jamais rien. Le changement
/// visuel se vérifie à l'œil ; c'est une conséquence assumée.
/// </para>
/// </remarks>
[Collection(WebCollection.Name)]
public class SharedChrome(CustomWebApplicationFactory<Program> factory)
{
  private readonly ChromeSurface _chrome = new(factory);

  /// <summary>
  /// <b>La feuille répond à sa route</b>, servie par le service comme une feuille de style — et non
  /// comme le corps d'une page d'erreur, qu'un navigateur refuserait d'appliquer.
  /// </summary>
  [Fact]
  public async Task ServesTheStyleSheetItself()
  {
    var served = await _chrome.FetchAsync(ChromeSurface.StyleSheet);

    served.StatusCode.ShouldBe(HttpStatusCode.OK, "La feuille de style doit être servie.");
    served.Content.Headers.ContentType?.MediaType.ShouldBe("text/css");
  }

  /// <summary>
  /// <b>La police répond à sa route</b>, et elle arrive du service. C'est l'unique fichier : un seul
  /// fichier variable porte toutes les graisses, en une requête au lieu de quatre.
  /// </summary>
  [Fact]
  public async Task ServesTheEmbeddedFontItself()
  {
    var served = await _chrome.FetchAsync(ChromeSurface.Font);

    served.StatusCode.ShouldBe(HttpStatusCode.OK, "La police doit être servie par le service.");
    served.Content.Headers.ContentType?.MediaType.ShouldBe("font/woff2");

    var file = await served.Content.ReadAsByteArrayAsync();
    file.Length.ShouldBeGreaterThan(0, "Une police vide ne rendrait aucun texte.");
  }

  /// <summary>
  /// <b>Chaque écran de la surface référence la feuille</b>, sans exception : elle est posée une
  /// fois dans le layout partagé, et c'est ce qui fait qu'un écran neuf l'aura sans que personne y
  /// pense.
  /// </summary>
  [Fact]
  public async Task ReferencesTheStyleSheetFromEveryScreen()
  {
    foreach (var screen in await _chrome.ScreensAsync())
    {
      var rendered = await _chrome.ReadAsync(screen);

      rendered.ShouldContain(
        ChromeSurface.StyleSheet,
        Case.Sensitive,
        $"L'écran {screen} ne référence pas la feuille de style.");
    }
  }

  /// <summary>
  /// ⚠️ <b>Aucun écran ne fait chercher quoi que ce soit chez un tiers</b> : ni police, ni feuille,
  /// ni script. Tout ce que la page demande au navigateur d'aller chercher vient du service.
  /// </summary>
  [Fact]
  public async Task LoadsNothingFromAThirdPartyOnAnyScreen()
  {
    foreach (var screen in await _chrome.ScreensAsync())
    {
      var thirdParty = ChromeSurface.ThirdPartyResourcesIn(await _chrome.ReadAsync(screen));

      thirdParty.ShouldBeEmpty(
        $"L'écran {screen} fait chercher {string.Join(", ", thirdParty)} chez un tiers.");
    }
  }

  /// <summary>
  /// <b>La feuille elle-même ne va rien chercher ailleurs.</b> Un <c>@@import</c> ou une police
  /// distante dans la feuille rouvrirait la fuite en un seul endroit, invisible depuis les écrans.
  /// </summary>
  [Fact]
  public async Task LoadsNothingFromAThirdPartyFromTheStyleSheetItself()
  {
    var sheet = await _chrome.ReadAsync(ChromeSurface.StyleSheet);

    sheet.ShouldNotContain("http://");
    sheet.ShouldNotContain("https://");
    sheet.ShouldNotContain("//fonts.");
  }

  /// <summary>
  /// <b>Le CSS a quitté le fichier de vue.</b> Il vit dans la feuille servie, en un seul endroit :
  /// une règle recopiée dans un écran échapperait aux tokens et ne se retrouverait plus.
  /// </summary>
  [Fact]
  public async Task CarriesNoStyleBlockInsideTheScreensThemselves()
  {
    foreach (var screen in await _chrome.ScreensAsync())
    {
      var rendered = await _chrome.ReadAsync(screen);

      rendered.ShouldNotContain(
        "<style", Case.Insensitive, $"L'écran {screen} porte encore du CSS dans sa vue.");
    }
  }
}
