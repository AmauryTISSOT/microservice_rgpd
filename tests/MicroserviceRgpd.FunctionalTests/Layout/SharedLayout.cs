using System.Net;
using System.Text.RegularExpressions;

namespace MicroserviceRgpd.FunctionalTests.Layout;

/// <summary>
/// Le layout partagé des douze écrans de la surface : une feuille de style et une police que <b>le service sert
/// lui-même</b>, et aucune ressource tierce.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Le test le plus important de ce fichier est
/// <see cref="LoadsNothingFromAThirdPartyOnAnyScreen"/>.</b> Le reste garde que le layout arrive ;
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
public class SharedLayout(CustomWebApplicationFactory<Program> factory)
{
  private readonly LayoutSurface _layout = new(factory);

  /// <summary>
  /// <b>La feuille répond à sa route</b>, servie par le service comme une feuille de style — et non
  /// comme le corps d'une page d'erreur, qu'un navigateur refuserait d'appliquer.
  /// </summary>
  [Fact]
  public async Task ServesTheStyleSheetItself()
  {
    var served = await _layout.FetchAsync(LayoutSurface.StyleSheet);

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
    var served = await _layout.FetchAsync(LayoutSurface.Font);

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
    foreach (var screen in await _layout.ScreensAsync())
    {
      var rendered = await _layout.ReadAsync(screen);

      rendered.ShouldContain(
        LayoutSurface.StyleSheet,
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
    foreach (var screen in await _layout.ScreensAsync())
    {
      var thirdParty = LayoutSurface.ThirdPartyResourcesIn(await _layout.ReadAsync(screen));

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
    var sheet = await _layout.ReadAsync(LayoutSurface.StyleSheet);

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
    foreach (var screen in await _layout.ScreensAsync())
    {
      var rendered = await _layout.ReadAsync(screen);

      rendered.ShouldNotContain(
        "<style", Case.Insensitive, $"L'écran {screen} porte encore du CSS dans sa vue.");
    }
  }

  /// <summary>
  /// <b>Un élément <c>main</c> enveloppe le contenu rendu</b>, sur chaque écran. Il est posé une
  /// fois dans le layout partagé : c'est ce qui fait qu'un écran neuf l'aura sans que personne y
  /// pense.
  /// </summary>
  /// <remarks>
  /// Ce qui est vérifié n'est pas qu'une balise <c>main</c> existe — elle pourrait exister vide, à
  /// côté du contenu — mais qu'<b>il ne reste rien dehors</b> une fois la barre et le <c>main</c>
  /// retirés du corps. C'est la seule formulation qui distingue « envelopper » de « figurer ».
  /// </remarks>
  [Fact]
  public async Task WrapsEveryScreenInAMainElement()
  {
    foreach (var screen in await _layout.ScreensAsync())
    {
      var outside = LayoutSurface.OutsideTheMainOf(await _layout.ReadAsync(screen));

      outside.ShouldBeEmpty(
        $"L'écran {screen} laisse du contenu hors de son main : {outside}");
    }
  }

  /// <summary>
  /// <b>La barre porte les quatre points d'entrée, sur chaque écran, et dans l'ordre décidé</b> —
  /// et c'est ce qui fait que quitter un dossier long ne demande plus de le dérouler jusqu'en bas :
  /// le <c>Manifest</c>, la détection, la qualification et le tableau des demandes RGPD s'atteignent
  /// de partout sans passer par un écran intermédiaire.
  /// </summary>
  [Fact]
  public async Task CarriesTheFourEntryPointsOnEveryScreen()
  {
    foreach (var screen in await _layout.ScreensAsync())
    {
      var panel = LayoutSurface.SidepanelIn(await _layout.ReadAsync(screen));

      // ⚠️ La liste est lue SEULE, et le panneau est lu séparément du header : le nom du service
      // mène à l'accueil sans être une entrée, et lire les deux régions ensemble ferait passer le
      // retour à l'accueil pour une cinquième entrée — l'inverse de ce que ce test garde.
      LayoutSurface.LinksIn(LayoutSurface.EntryPointListIn(panel))
        .Select(link => link.Address)
        .ShouldBe(LayoutSurface.EntryPoints, $"Le panneau de l'écran {screen} n'offre pas les quatre points d'entrée, et eux seuls.");
    }
  }

  /// <summary>
  /// <b>La barre porte ses quatre libellés mot pour mot</b>, et le premier est <b>plus court</b> que
  /// le nom que la carte de l'accueil donne au même écran : la barre dit <c>Paramétrage</c> là où
  /// la carte dit <c>Paramétrage du microservice RGPD</c>.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>C'est le garde du double nom, et il n'a de sens que lu avec
  /// <see cref="Doorstep.CarriesTheFourDoorwaysWordForWordAndInOrder"/>.</b> Les deux formes
  /// viennent désormais de deux champs distincts d'un même <c>EntryPoint</c> ; rien dans le code ne
  /// les empêche de se rejoindre, et la barre n'était jusqu'ici éprouvée que sur ses adresses. Un
  /// retour silencieux à la forme pleine dans la barre — celui-là même que le renommage du service
  /// a servi à défaire — ne se serait vu nulle part.
  /// </remarks>
  [Fact]
  public async Task CarriesTheFourNavigationLabelsWordForWordAndInOrder()
  {
    foreach (var screen in await _layout.ScreensAsync())
    {
      var panel = LayoutSurface.SidepanelIn(await _layout.ReadAsync(screen));

      // La liste est lue SEULE, pour la même raison qu'au-dessus : le wordmark est un lien de la
      // navigation sans être une entrée, et il porte précisément le mot dont ces libellés se
      // distinguent.
      LayoutSurface.LinkBlocksIn(LayoutSurface.EntryPointListIn(panel))
        .Select(link => LayoutSurface.TextIn(link.Contents))
        .ShouldBe(
          LayoutSurface.NavigationLabels,
          $"Le panneau de l'écran {screen} ne porte pas les quatre libellés arrêtés, dans l'ordre décidé.");
    }
  }

  /// <summary>
  /// <b>Le lien de l'écran courant est marqué</b>, et lui seul : sans cela, la barre dit où l'on
  /// peut aller sans jamais dire où l'on est. ⚠️ <b>Sauf sur l'accueil</b>, qui ne relève d'aucun
  /// des quatre points d'entrée et n'en marque donc aucun.
  /// </summary>
  [Fact]
  public async Task MarksTheLinkOfTheCurrentScreen()
  {
    foreach (var screen in await _layout.ScreensAsync())
    {
      var links = LayoutSurface.LinksIn(LayoutSurface.SidepanelIn(await _layout.ReadAsync(screen)));

      links.Where(link => link.IsCurrent)
        .Select(link => link.Address)
        .ShouldBe(LayoutSurface.MarkedEntryPointsOn(screen), $"Le panneau de l'écran {screen} ne marque pas le bon lien, ou en marque plusieurs.");
    }
  }

  /// <summary>
  /// <b>La barre porte le nom du service, et il mène à l'accueil</b> — depuis n'importe quel écran.
  /// C'est le retour à la porte, et il est obtenu <b>sans entrée de plus</b> : le texte inerte
  /// devient un lien plutôt qu'un lien de plus.
  /// </summary>
  [Fact]
  public async Task NamesTheServiceInTheBarAndLeadsBackToTheDoorstep()
  {
    foreach (var screen in await _layout.ScreensAsync())
    {
      // ⚠️ LE NOM DU SERVICE SE LIT DANS LE BANDEAU, ET C'EST LE POINT DE CE TEST DEPUIS LE PANNEAU :
      // le panneau latéral se replie, le header non. Un wordmark qui aurait suivi les entrées dans le
      // panneau aurait emporté le chemin du retour avec lui au premier repli.
      var bar = LayoutSurface.HeaderIn(await _layout.ReadAsync(screen));
      var wordmark = LayoutSurface.WordmarkIn(bar);

      wordmark.Text.ShouldBe(
        LayoutSurface.ServiceName, $"Le header de l'écran {screen} ne nomme pas le service.");

      wordmark.Address.ShouldBe(
        LayoutSurface.Doorstep, $"Le nom du service ne ramène pas à l'accueil depuis l'écran {screen}.");
    }
  }

  /// <summary>
  /// ⚠️ <b>AUCUN CHIFFRE DANS LA BARRE</b>, ni compteur ni badge. La règle des chiffres que le
  /// service applique — un « 0 demande en retard » se lit comme une mesure
  /// rassurante là où la phrase dit ce qu'elle est — vaut aussi pour une barre qu'on lit sur les
  /// douze écrans sans jamais l'ouvrir.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Une exception, nommée et étroite : la version du produit</b>, à l'extrémité droite de la
  /// barre — de même nature que les références d'articles RGPD sur l'accueil. Elle est <b>retirée</b>
  /// de la barre avant le balayage, et non tolérée par lui : tout autre chiffre, y compris un
  /// compteur glissé à côté d'elle, fait toujours échouer le test.
  /// </remarks>
  [Fact]
  public async Task CarriesNoTallyInTheBar()
  {
    foreach (var screen in await _layout.ScreensAsync())
    {
      // ⚠️ LES DEUX RÉGIONS SONT BALAYÉES, pas seulement celle qui porte l'exception. Ne balayer que
      // le header aurait laissé un compteur s'installer à côté d'un point d'entrée — l'endroit
      // même où un « 3 dossiers en retard » viendrait naturellement se poser.
      var navigation = LayoutSurface.NavigationOf(await _layout.ReadAsync(screen));

      foreach (var (region, name) in new[] { (navigation.Sidepanel, "panneau latéral"), (navigation.Header, "header") })
      {
        var swept = LayoutSurface.WithoutTheBurgerGlyph(LayoutSurface.WithoutTheVersion(region));

        Regex.IsMatch(swept, @"\d").ShouldBeFalse(
          $"Le {name} de l'écran {screen} porte un chiffre, donc un compte que personne n'a demandé.");
      }
    }
  }

  /// <summary>
  /// <b>La barre porte la version du produit, exactement une fois, sur chaque écran</b> — un texte
  /// nu de la forme <c>v0.1.0</c>, à lire et à ignorer : ni lien, ni infobulle, ni libellé.
  /// </summary>
  [Fact]
  public async Task CarriesExactlyOneVersionInTheBar()
  {
    foreach (var screen in await _layout.ScreensAsync())
    {
      var versions = await VersionsInTheBarOfAsync(screen);

      versions.Count.ShouldBe(1, $"La barre de l'écran {screen} doit porter la version du produit, une fois.");

      versions[0].ShouldMatch(
        @"^v\d+\.\d+\.\d+$", $"La version de la barre de l'écran {screen} n'a pas la forme attendue.");
    }
  }

  /// <summary>
  /// <b>La version affichée est celle de l'assemblage Web</b>, lue par réflexion dans le test et non
  /// figée en littéral : ce qui est gardé est qu'il n'y a <b>qu'une source</b> — la propriété de
  /// build, portée par l'attribut de l'assemblage, et rendue telle quelle à l'écran.
  /// </summary>
  [Fact]
  public async Task ShowsTheInformationalVersionOfTheWebAssembly()
  {
    var expected = $"v{LayoutSurface.InformationalVersionOfTheWebAssembly()}";

    foreach (var screen in await _layout.ScreensAsync())
    {
      var versions = await VersionsInTheBarOfAsync(screen);

      versions.ShouldBe([expected], $"La barre de l'écran {screen} ne montre pas la version de l'assemblage.");
    }
  }

  private async Task<IReadOnlyList<string>> VersionsInTheBarOfAsync(string screen)
  {
    return LayoutSurface.VersionsIn(LayoutSurface.HeaderIn(await _layout.ReadAsync(screen)));
  }

  /// <summary>
  /// <b>Les six adresses du contexte de détection portent le nom de l'écran qu'elles servent</b> :
  /// elles répondent sous <c>/detection</c>, et les six anciennes <b>meurent en 404</b>.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Aucune redirection n'est posée, et il ne faut pas en poser.</b> Une ancienne adresse qui
  /// mènerait encore quelque part serait un second nom vivant pour la même chose : elle survivrait
  /// dans les signets et les liens collés, et la surface porterait deux vocabulaires. C'est pourquoi
  /// le refus est vérifié comme un 404 sec — un 301 ou un 302 passerait un test qui se contenterait
  /// de « ne répond pas 200 ».
  /// </remarks>
  [Fact]
  public async Task RetiresTheSixFormerScreeningAddressesWithoutRedirecting()
  {
    foreach (var retired in LayoutSurface.RetiredScreeningAddresses)
    {
      var response = await _layout.FetchAsync(retired);

      // La redirection est nommée AVANT le 404, et non déduite de lui : un 301 échouerait de toute
      // façon sur le statut, mais avec un message qui parlerait d'un statut inattendu là où ce qui
      // s'est produit est qu'une ancienne adresse mène encore quelque part.
      ((int)response.StatusCode is >= 300 and <= 399).ShouldBeFalse(
        $"L'ancienne adresse {retired} redirige, et fait donc revivre l'ancien nom.");

      response.StatusCode.ShouldBe(
        HttpStatusCode.NotFound, $"L'ancienne adresse {retired} doit être morte.");
    }
  }

  /// <summary>
  /// <b>Et les six écrans répondent bien sous leur nouveau préfixe</b> — les six ensemble, sans quoi
  /// une seule adresse restée en arrière ferait parler à la surface deux vocabulaires à la fois.
  /// </summary>
  [Fact]
  public async Task ServesTheSixScreeningScreensUnderTheirNewPrefix()
  {
    var screens = await _layout.ScreeningScreensAsync();

    // Sept, écrit en clair, et non le compte de la liste des adresses retirées : les deux comptes
    // valent ce qu'ils valent par histoire et non par règle, et les dériver l'un de l'autre ferait
    // qu'en retirer une adresse affaiblirait les deux tests d'un coup.
    screens.Count.ShouldBe(7, "Le contexte de détection compte sept écrans.");

    foreach (var screen in screens)
    {
      // ⚠️ Comparaison PAR SEGMENTS, comme celle du surlignage de la barre : un préfixe de texte nu
      // aurait aussi accepté une adresse qui commence par les mêmes lettres sans relever du
      // contexte.
      LayoutSurface.EntryPointOf(screen).ShouldBe(
        "/detection", $"L'écran {screen} ne relève pas du point d'entrée du contexte.");

      await _layout.ReadAsync(screen);
    }
  }

  /// <summary>
  /// <b>Ni pied de page, ni lien d'évitement.</b> Le premier est un annuaire de liens marketing que
  /// le service n'a pas ; le second est une décision explicite du demandeur.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Conséquence consignée</b> : la barre se répète sur les douze écrans sans moyen de la
  /// sauter au clavier, ce qui est une régression d'accessibilité par rapport à l'état d'avant, où
  /// aucune barre n'existait. Aucun chantier d'accessibilité n'est ouvert ici.
  /// </remarks>
  [Fact]
  public async Task CarriesNeitherAFooterNorASkipLink()
  {
    foreach (var screen in await _layout.ScreensAsync())
    {
      var rendered = await _layout.ReadAsync(screen);

      rendered.ShouldNotContain("<footer", Case.Insensitive, $"L'écran {screen} porte un pied de page.");

      // Un lien d'évitement est un lien vers un fragment de la page elle-même, et la surface n'en
      // porte aucun : c'est ce qu'on cherche, plutôt que le nom d'une cible qu'il aurait pu prendre.
      Regex.IsMatch(rendered, @"<a\b[^>]*\bhref=""#").ShouldBeFalse(
        $"L'écran {screen} porte un lien d'évitement.");
    }
  }
}
