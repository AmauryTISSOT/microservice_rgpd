using System.Globalization;
using System.Text.RegularExpressions;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.FunctionalTests.Layout;

/// <summary>
/// <b>La porte du service</b> : la racine répond un écran, et non un <c>404</c> en
/// <c>application/problem+json</c> à la figure de l'humain qui a tapé l'adresse.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>L'accueil ne relève ni du <c>Casework</c> ni du <c>Screening</c> : il est du layout</b>, au
/// même titre que la barre. Il est donc éprouvé dans le harnais du layout, par la <b>seule frontière
/// HTTP</b>, exactement ce que fait un navigateur — et le reste du layout le tient déjà pour un
/// écran comme les douze autres, puisqu'il entre dans <see cref="LayoutSurface.ScreensAsync"/>.
/// </para>
/// <para>
/// ⚠️ <b>Aucune assertion de ce fichier ne porte sur une valeur de design</b> : pas une couleur, pas
/// un rayon, pas une taille, pas une graisse. Ce qui est gardé est ce que l'écran <b>dit</b> et où il
/// <b>mène</b> ; le survol, le focus et la réorganisation en écran étroit se vérifient à l'œil.
/// </para>
/// </remarks>
[Collection(WebCollection.Name)]
public class Doorstep(CustomWebApplicationFactory<Program> factory)
{
  private readonly LayoutSurface _layout = new(factory);

  /// <summary>
  /// <b>La racine répond un écran</b>, rendu par le serveur et nommé — c'est toute la décision : le
  /// service a une porte.
  /// </summary>
  [Fact]
  public async Task AnswersAtTheRootWithAScreenThatNamesTheService()
  {
    var read = LayoutSurface.TextIn(
      LayoutSurface.MainOf(await _layout.ReadAsync(LayoutSurface.Doorstep)));

    read.ShouldContain(
      LayoutSurface.ServiceName, Case.Sensitive, "L'accueil ne porte pas le nom du service.");
  }

  /// <summary>
  /// <b>La phrase de présentation est celle qui a été arrêtée</b>, mot pour mot. Elle est gelée
  /// parce que chacun de ses mots a été pesé : le registre juridique, la troisième personne, et le
  /// verbe <b>présumer</b> — jamais « établir ».
  /// </summary>
  [Fact]
  public async Task CarriesThePresentationSentenceWordForWord()
  {
    var read = LayoutSurface.TextIn(
      LayoutSurface.MainOf(await _layout.ReadAsync(LayoutSurface.Doorstep)));

    read.ShouldContain(
      LayoutSurface.Presentation,
      Case.Sensitive,
      "La phrase de présentation de l'accueil n'est pas celle qui a été arrêtée.");
  }

  /// <summary>
  /// ⚠️ <b>Les articles cités sont exactement ceux que le code implémente.</b> L'énumération est
  /// <b>non contiguë</b> parce que la taxonomie est fermée à six droits et exclut l'art. 22 :
  /// « articles 15 à 21 » promettrait un droit que le service refuse, et la porte mentirait sur ce
  /// qui se trouve derrière elle.
  /// </summary>
  [Fact]
  public async Task CitesTheArticlesTheCodeActuallyImplements()
  {
    var read = LayoutSurface.TextIn(
      LayoutSurface.MainOf(await _layout.ReadAsync(LayoutSurface.Doorstep)));

    read.ShouldContain(LayoutSurface.Presentation, Case.Sensitive);

    ArticlesCitedIn(LayoutSurface.Presentation).ShouldBe(
      [.. DataSubjectRight.List.Where(right => right.Article is not null).Select(right => right.Article!.Value)],
      ignoreOrder: true,
      "La phrase du seuil ne cite pas les articles que la taxonomie ouvre : la porte promet un " +
      "droit que le service refuse, ou tait un droit qu'il instruit.");
  }

  /// <summary>
  /// <b>Les quatre portes portent les quatre noms et les quatre phrases arrêtés</b>, et elles les
  /// portent <b>dans l'ordre où l'on rencontre les écrans</b> — la position apprend ce qu'aucun nom
  /// ne dit.
  /// </summary>
  [Fact]
  public async Task CarriesTheFourDoorwaysWordForWordAndInOrder()
  {
    var main = LayoutSurface.MainOf(await _layout.ReadAsync(LayoutSurface.Doorstep));
    var doorways = LayoutSurface.LinkBlocksIn(main);

    doorways.Select(doorway => doorway.Address).ShouldBe(
      [.. LayoutSurface.Doorways.Select(expected => expected.Address)],
      "Les portes de l'accueil ne mènent pas aux quatre adresses, dans l'ordre décidé.");

    foreach (var (doorway, expected) in doorways.Zip(LayoutSurface.Doorways))
    {
      var read = LayoutSurface.TextIn(doorway.Contents);

      read.ShouldContain(
        expected.Name, Case.Sensitive, $"La porte {expected.Address} ne porte pas son nom.");

      read.ShouldContain(
        expected.Sentence, Case.Sensitive, $"La porte {expected.Address} ne porte pas sa phrase.");
    }
  }

  /// <summary>
  /// ⚠️ <b>La carte entière est un lien</b> : c'est une porte, on pousse la porte entière. Pas un
  /// <c>div</c> cliquable, pas de <c>role</c>, pas de <c>tabindex</c> — un lien est déjà un lien —,
  /// et <b>aucune cible secondaire</b> à l'intérieur : un second lien dans la carte ferait deux
  /// choses à pousser là où l'écran n'en montre qu'une.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>La cible secondaire se cherche des DEUX côtés, et le compte seul n'aurait rien vu.</b> Un
  /// lien posé <b>à côté</b> des quatre portes fait un cinquième lien ; un lien <b>imbriqué</b> dans
  /// une porte n'en fait aucun — la fermeture étant prise au plus court, il est avalé dans le
  /// contenu de la porte qui le porte. C'est pourquoi le contenu de chaque porte est fouillé en
  /// plus d'être compté.
  /// </remarks>
  [Fact]
  public async Task MakesEachWholeCardOneBlockLinkAndNothingElse()
  {
    var main = LayoutSurface.MainOf(await _layout.ReadAsync(LayoutSurface.Doorstep));
    var doorways = LayoutSurface.LinkBlocksIn(main);

    doorways.Count.ShouldBe(
      LayoutSurface.Doorways.Count,
      "L'accueil ne porte pas exactement les quatre liens de ses quatre portes : une cible " +
      "secondaire s'est glissée dans une carte, ou à côté.");

    foreach (var doorway in doorways)
    {
      // Le nom est un titre de niveau 2 et la phrase un paragraphe : la carte porte une hiérarchie,
      // et pas deux lignes de texte que rien ne distingue.
      Regex.IsMatch(doorway.Contents, @"<h2\b", RegexOptions.Singleline).ShouldBeTrue(
        $"La porte {doorway.Address} ne porte pas son nom en titre.");

      Regex.IsMatch(doorway.Contents, @"<p\b", RegexOptions.Singleline).ShouldBeTrue(
        $"La porte {doorway.Address} ne porte pas sa phrase en paragraphe.");

      Regex.IsMatch(doorway.Contents, @"<a\b", RegexOptions.Singleline).ShouldBeFalse(
        $"La porte {doorway.Address} porte un lien à l'intérieur : la carte a deux choses à " +
        "pousser là où l'écran n'en montre qu'une.");

      doorway.Attributes.ShouldNotContain(
        "role=", Case.Insensitive, $"La porte {doorway.Address} porte un role : un lien est déjà un lien.");

      doorway.Attributes.ShouldNotContain(
        "tabindex", Case.Insensitive, $"La porte {doorway.Address} porte un tabindex, que rien ne réclame.");
    }

    main.ShouldNotContain(
      "tabindex", Case.Insensitive, "L'accueil pose un ordre de tabulation à la main.");
  }

  /// <summary>
  /// ⚠️ <b>AUCUN CHIFFRE SUR L'ACCUEIL</b>, ni compteur ni badge : un « 0 dossier en retard » ne doit
  /// jamais rassurer à tort, et c'est d'autant plus vrai sur l'écran qu'on lit avant tous les autres.
  /// </summary>
  /// <remarks>
  /// La <b>seule</b> dérogation est étroite et nommée : les références d'articles du RGPD dans la
  /// phrase du seuil, qui ne sont ni un compteur ni une mesure du service. Elles sont retirées avant
  /// le balayage, plutôt que tolérées par une règle plus lâche qui aurait laissé passer un compte.
  /// </remarks>
  [Fact]
  public async Task CarriesNoTallyAnywhereOutsideTheArticlesOfTheRegulation()
  {
    // Le balayage porte sur ce qui se LIT, pas sur la source : le niveau d'un titre est un chiffre
    // du balisage, et le compter aurait rendu ce garde faux au premier `h2`.
    var read = LayoutSurface.TextIn(LayoutSurface.MainOf(await _layout.ReadAsync(LayoutSurface.Doorstep)));

    var swept = read.Replace(LayoutSurface.Presentation, string.Empty, StringComparison.Ordinal);

    Regex.IsMatch(swept, @"\d").ShouldBeFalse(
      "L'accueil porte un chiffre hors de la phrase du seuil, donc un compte que personne n'a demandé.");
  }

  /// <summary>
  /// <b>L'écran fonctionne sans JavaScript</b> : la porte se pousse au clavier et à la souris sans
  /// qu'une ligne de script ne soit servie ni exécutée.
  /// </summary>
  /// <remarks>
  /// L'absence de ressource tierce, elle, est gardée pour les quatorze écrans à la fois par
  /// <see cref="SharedLayout.LoadsNothingFromAThirdPartyOnAnyScreen"/>.
  /// </remarks>
  [Fact]
  public async Task RunsWithoutASingleLineOfScript()
  {
    var rendered = await _layout.ReadAsync(LayoutSurface.Doorstep);

    rendered.ShouldNotContain("<script", Case.Insensitive, "L'accueil sert du JavaScript.");
    rendered.ShouldNotContain("onclick", Case.Insensitive, "L'accueil câble un geste en JavaScript.");
  }

  /// <summary>
  /// Les articles du RGPD cités dans une phrase, <b>bornes des intervalles comprises</b> : « 15 à
  /// 18 » en vaut quatre. Sans cela, la comparaison avec la taxonomie ne dirait rien d'une plage.
  /// </summary>
  private static IReadOnlyList<int> ArticlesCitedIn(string sentence)
  {
    List<int> cited = [];

    foreach (Match range in Regex.Matches(sentence, @"(\d+)\s+à\s+(\d+)"))
    {
      var first = int.Parse(range.Groups[1].Value, CultureInfo.InvariantCulture);
      var last = int.Parse(range.Groups[2].Value, CultureInfo.InvariantCulture);

      cited.AddRange(Enumerable.Range(first, last - first + 1));
    }

    var single = Regex.Replace(sentence, @"\d+\s+à\s+\d+", string.Empty);

    cited.AddRange(
      Regex.Matches(single, @"\d+").Select(number => int.Parse(number.Value, CultureInfo.InvariantCulture)));

    return cited;
  }
}
