using System.Net;
using System.Text.RegularExpressions;
using MicroserviceRgpd.TestDoubles.Ollama;

namespace MicroserviceRgpd.FunctionalTests.Screenings;

/// <summary>
/// Drapeau allumé, <b>A2 détecte un relevé collé</b> de bout en bout : dépôt, rapport, écran d'une
/// table, export — le moteur réel sur l'artefact embarqué, Ollama doublé au fil HTTP.
/// </summary>
/// <remarks>
/// ⚠️ <b>Le score ne doit apparaître nulle part.</b> Non calibré, il se lirait comme une probabilité :
/// ni l'écran, ni le motif, ni l'export ne le portent. Les scores que Python a calculés sur le jeu
/// figé sont cherchés en toutes lettres, sous les deux séparateurs décimaux.
/// </remarks>
[Collection(A2OnWebCollection.Name)]
public class TheScreeningEngineTheServiceResolvesWhenA2IsOn(A2OnWebApplicationFactory factory)
{
  private readonly ScreeningSurface _surface = new(factory);

  private static string APasteOfTheFrozenColumns()
  {
    return ScreeningSurface.Paste(
      ScreeningSurface.Column("email", table: "users", position: 1, columnComment: "adresse de contact"),
      ScreeningSurface.Column("first_name", table: "users", position: 2),
      ScreeningSurface.Column("price", table: "products", position: 1));
  }

  /// <summary>
  /// Le bandeau du rapport nomme A2 et sa version — l'artefact et l'encodeur —, et c'est bien Ollama
  /// qui a été appelé.
  /// </summary>
  [Fact]
  public async Task NamesA2AndItsVersionOnTheBannerOfTheReport()
  {
    factory.Ollama.Forget();

    var report = WebUtility.HtmlDecode(await _surface.DepositAndReadTheReportAsync(APasteOfTheFrozenColumns()));

    report.ShouldContain(
      $"a2-bge-m3-logreg, version modele-{A2Equivalence.ManifestSha256[..12]}+encodeur-{A2Equivalence.EncoderDigest[..12]}");
    factory.Ollama.EmbedBodies.ShouldNotBeEmpty();
  }

  /// <summary>
  /// L'écran de la table rend la ligne signalée avec sa catégorie, le degré d'A2 et son motif
  /// français, et la ligne sous le seuil comme une ligne où rien n'a été vu — sans motif.
  /// </summary>
  [Fact]
  public async Task RendersTheLinesA2FlaggedAndTheOnesItDidNot()
  {
    await _surface.DepositAndReadTheReportAsync(APasteOfTheFrozenColumns());

    var users = WebUtility.HtmlDecode(await _surface.ReadAsync(ScreeningSurface.TableOf(table: "users")));
    var email = ScreeningSurface.BlockOf(users, "email");

    email.ShouldNotBeNull();
    email.ShouldContain("coordonnées");
    email.ShouldContain("proximité d'un prototype");
    email.ShouldContain(
      "le nom « users.email » est proche du prototype « l'adresse électronique ou le numéro de "
      + "téléphone d'une personne » : coordonnées");

    var products = WebUtility.HtmlDecode(await _surface.ReadAsync(ScreeningSurface.TableOf(table: "products")));
    var price = ScreeningSurface.BlockOf(products, "price");

    price.ShouldNotBeNull();
    price.ShouldContain("Rien n'a été vu");
    price.ShouldNotContain("proximité d'un prototype");
  }

  /// <summary>⚠️ Aucun score — ni sur les écrans, ni dans l'un ou l'autre fichier de l'export.</summary>
  [Fact]
  public async Task ShowsNoScoreOnTheScreensNorInTheExport()
  {
    var report = await _surface.DepositAndReadTheReportAsync(APasteOfTheFrozenColumns());

    string[] surfaces =
    [
      report,
      await _surface.ReadAsync(ScreeningSurface.TableOf(table: "users")),
      await _surface.ReadAsync(ScreeningSurface.TableOf(table: "products")),
      await _surface.Client.GetStringAsync(ScreeningSurface.MapAsJson),
      await _surface.Client.GetStringAsync(ScreeningSurface.MapAsCsv),
    ];

    foreach (var surface in surfaces.Select(text => WebUtility.HtmlDecode(text)!))
    {
      foreach (var frozen in A2Equivalence.Columns)
      {
        var digits = frozen.Score.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)[2..];

        Regex.IsMatch(surface, $@"0[.,]{digits}").ShouldBeFalse(
          $"Le score de {frozen.Table}.{frozen.Column} apparaît en toutes lettres.");
      }

      surface.ShouldNotContain("score", Case.Insensitive);
    }
  }
}

/// <summary>
/// ⚠️ <b>Par défaut, la suite fonctionnelle détecte avec le vrai lexique.</b> L'hôte partagé ne dit
/// rien du drapeau, et c'est ce silence qu'on éprouve : il vaut « éteint ».
/// </summary>
[Collection(WebCollection.Name)]
public class TheScreeningEngineTheServiceResolvesByDefault(CustomWebApplicationFactory<Program> factory)
{
  private readonly ScreeningSurface _surface = new(factory);

  [Fact]
  public async Task NamesTheLexiconOnTheBannerOfTheReport()
  {
    var report = await _surface.DepositAndReadTheReportAsync(
      ScreeningSurface.Paste(ScreeningSurface.Column("email")));

    report.ShouldContain("regles-lexique-fr-en, version");
  }
}
