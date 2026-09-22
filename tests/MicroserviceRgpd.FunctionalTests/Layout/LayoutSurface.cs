using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.FunctionalTests.Layout;

/// <summary>
/// Le <b>layout partagé</b> de la surface de l'<c>Operator</c> — la feuille de style et la police
/// que le service sert lui-même —, exercé par sa <b>seule frontière HTTP</b>, exactement ce que fait
/// un navigateur.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Le layout n'appartient ni au <c>Requests</c> ni au <c>Screening</c>.</b>
/// Il reçoit donc son harnais à lui plutôt que d'entrer dans les harnais de contexte existants —
/// écrire deux fois la même assertion, une par contexte, l'aurait dupliquée sans rien prouver de
/// plus, et aurait fait se croiser deux contextes que la carte tient pour disjoints. Ce harnais pose
/// l'état dont il a besoin par les mêmes formulaires qu'un humain remplirait.
/// </para>
/// <para>
/// ⚠️ <b>Aucune valeur de design n'est lue ici.</b> Pas une couleur, pas un rayon, pas une taille,
/// pas une graisse : une telle assertion lirait le contenu de la feuille, décrirait l'implémentation
/// et casserait à chaque retouche sans jamais rien attraper. Ce que ce harnais garde est que le
/// layout <b>arrive</b>, et qu'il arrive <b>du service</b>. Le rendu, lui, se vérifie à l'œil.
/// </para>
/// <para>
/// <b>La collection est partagée</b> : d'autres tests déposent dans la même base. Les assertions ne
/// portent donc sur aucun compte et sur aucun contenu de rapport — seulement sur des adresses qui
/// répondent.
/// </para>
/// </remarks>
internal sealed class LayoutSurface(CustomWebApplicationFactory<Program> factory)
{
  /// <summary>La feuille unique, servie par le service et référencée par tous les écrans.</summary>
  internal const string StyleSheet = "/css/operator.css";

  /// <summary>
  /// Le fichier de police, variable et en sous-ensemble latin — embarqué, jamais chargé chez un
  /// tiers : un service qui outille le RGPD ne fait pas fuiter l'adresse IP de ses utilisateurs vers
  /// un hébergeur de polices.
  /// </summary>
  internal const string Font = "/fonts/inter-latin-variable.woff2";

  /// <summary>
  /// Le module du tableau des demandes RGPD, servi par le service, que seul cet écran charge.
  /// </summary>
  internal const string BoardModule = "/js/requests-board.js";

  /// <summary>
  /// Le module du dépôt d'un relevé, qui ne fait que copier la requête affichée — et que seul cet
  /// écran charge.
  /// </summary>
  internal const string DepositModule = "/js/deposit.js";

  /// <summary>Le module de la qualification : le compteur de caractères, et rien d'autre.</summary>
  internal const string QualifyModule = "/js/qualify.js";

  /// <summary>
  /// Le module de l'écran d'une table : il poste les arbitrages sans recharger la page, et rien
  /// d'autre — sans lui, les formulaires postent et redirigent comme avant.
  /// </summary>
  internal const string ArbitrationModule = "/js/arbitration.js";

  /// <summary>
  /// <b>Les quatre points d'entrée</b> que la barre de navigation offre, et les seuls, <b>dans
  /// l'ordre où l'on rencontre les écrans</b> : la configuration, la détection, la qualification,
  /// puis le tableau des demandes. Il n'y a pas de cinquième lien vers l'historique des rapports de
  /// détection : il s'atteint depuis le rapport de détection courant.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Cette liste est RECOPIÉE À DESSEIN</b>, et il ne faut pas la faire pointer vers celle du
  /// layout — pas plus que <see cref="ServiceName"/> ou que le calcul de <see cref="EntryPointOf"/>.
  /// Un test qui lit la constante qu'il vérifie ne vérifie plus rien : il passerait encore le jour où
  /// un cinquième lien apparaît, le jour où la barre se met à mener ailleurs, ou le jour où l'ordre
  /// se défait. Ce qui est écrit ici est ce que la surface <b>doit</b> offrir, tenu séparément de ce
  /// qu'elle offre : cette liste se met à jour <b>à la main</b>.
  /// </remarks>
  internal static readonly IReadOnlyList<string> EntryPoints =
    ["/parametrage", "/detection", "/qualification", "/demandes"];

  /// <summary>Le nom du service, que la barre porte devant ses quatre liens.</summary>
  internal const string ServiceName = "Microservice RGPD";

  /// <summary>
  /// <b>Les quatre libellés que la BARRE porte</b>, dans l'ordre où l'on rencontre les écrans — et
  /// le premier <b>n'est pas</b> le nom que la carte de l'accueil porte pour le même écran. Le
  /// service a deux noms vivants pour le paramétrage : <c>Paramétrage</c> dans la barre, où le
  /// wordmark <see cref="ServiceName"/> le précède de quinze centimètres et rendait la forme
  /// pleine redondante, et la forme pleine sur la carte, où rien ne la précède.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Recopiés à dessein</b>, comme <see cref="Doorways"/> : c'est <b>le</b> garde du double
  /// nom. Sans lui, rien n'empêche les deux formes de se rejoindre en silence — ni le compilateur,
  /// qui verrait deux champs distincts porter la même chaîne, ni aucun autre test, puisque la barre
  /// n'était jusqu'ici éprouvée que sur ses adresses.
  /// </remarks>
  internal static readonly IReadOnlyList<string> NavigationLabels =
  [
    "Paramétrage",
    "Détection des données personnelles",
    "Qualification",
    "Tableau des demandes RGPD",
  ];

  /// <summary>
  /// <b>L'accueil</b> — la porte du service, à la racine. Ce n'est le point d'entrée d'aucun
  /// contexte : c'est du layout, au même titre que la barre.
  /// </summary>
  internal const string Doorstep = "/";

  /// <summary>
  /// <b>La phrase de présentation du seuil</b>, gelée mot pour mot : registre juridique, troisième
  /// personne, et une énumération d'articles <b>non contiguë</b> — la taxonomie du code est fermée à
  /// six droits et exclut l'art. 22, si bien qu'« articles 15 à 21 » promettrait un droit que le
  /// service refuse.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Recopiée à dessein</b>, comme <see cref="EntryPoints"/> : un test qui lirait la constante
  /// qu'il vérifie passerait encore le jour où la phrase se défait.
  /// </remarks>
  internal const string Presentation =
    "Ce service instruit les demandes par lesquelles une personne concernée exerce les six droits " +
    "prévus aux articles 15 à 18, 20 et 21 du RGPD. Il sert aussi, avant toute demande, à présumer " +
    "les données qu'un système détient.";

  /// <summary>
  /// <b>Les quatre portes de l'accueil</b>, dans l'ordre où l'on rencontre les écrans, chacune avec
  /// le nom qu'elle porte, la phrase qu'elle dit et l'adresse où elle mène.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Recopiées à dessein</b>, elles aussi. Les phrases sont gelées, et <b>sans un
  /// chiffre</b> : les trois premières s'adressent à l'Operator, en français simple.
  /// </para>
  /// <para>
  /// ⚠️ <b>La dernière n'est pas une phrase, délibérément</b> : « Consultation des demandes RGPD en
  /// cours » nomme ce qu'on lit derrière la porte, sans verbe. Le parallélisme ne doit pas être
  /// « rétabli ».
  /// </para>
  /// <para>
  /// ⚠️ <b>La phrase de la qualification dit que rien n'est enregistré</b>, et c'est ce qui la
  /// distingue des trois autres : elle est la seule à écarter un geste plutôt qu'à en annoncer un.
  /// Le service y fait une <b>proposition</b>, jamais une décision. Ce membre de phrase est gelé
  /// comme le reste — sans lui, on arriverait sur l'écran en croyant y déposer une demande.
  /// </para>
  /// </remarks>
  internal static readonly IReadOnlyList<(string Name, string Sentence, string Address)> Doorways =
  [
    (
      "Paramétrage du microservice RGPD",
      "Pour chacun des six droits RGPD, choisissez un canal : une adresse HTTP ou RabbitMQ. Rien " +
      "n'est obligatoire. Un droit sans canal reste « non configuré ».",
      "/parametrage"),
    (
      "Détection des données personnelles",
      "Scannez une base ou collez un schéma. Le service repère les colonnes qui peuvent contenir " +
      "des données personnelles, et vous validez chaque ligne.",
      "/detection"),
    (
      "Qualification",
      "Vous ne savez pas quel droit une personne exerce ? Collez son message, le service vous fait " +
      "une proposition. Rien n'est enregistré.",
      "/qualification"),
    (
      "Tableau des demandes RGPD",
      "Consultation des demandes RGPD en cours",
      "/demandes"),
  ];

  /// <summary>
  /// <b>Les six adresses que le contexte de détection a quittées</b>, et qui <b>meurent en 404, sans
  /// redirection</b>. Une redirection serait un second nom vivant pour la même chose : l'ancien mot
  /// survivrait dans les signets, les liens collés et les barres d'adresse, et la surface porterait
  /// deux vocabulaires au lieu d'un.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Cette liste est RECOPIÉE À DESSEIN</b>, comme <see cref="EntryPoints"/> : elle ne se
  /// dérive pas des adresses vivantes. Un préfixe calculé depuis l'ancien nom se serait tu le jour
  /// où une seule des six serait revenue à la vie.
  /// </para>
  /// <para>
  /// ⚠️ <b>C'est le seul endroit du dépôt où l'ancienne adresse doit rester écrite</b>, et c'est ce
  /// qui la rend éprouvable : un garde ne peut pas tenir qu'une adresse est morte sans la nommer.
  /// Le jour où le mot du contexte entre dans les termes retirés, ce fichier a besoin d'une
  /// exemption <b>ancrée sur son chemin</b>, sur le modèle de celles déjà écrites — pas d'un
  /// assouplissement du garde, et pas de la suppression de cette liste.
  /// </para>
  /// </remarks>
  internal static readonly IReadOnlyList<string> RetiredScreeningAddresses =
  [
    "/depistage",
    "/depistage/depot",
    "/depistage/table",
    "/depistage/historique",
    "/depistage/archive",
    "/depistage/archive/table",
  ];

  /// <summary>
  /// L'écran de la qualification, et <b>la racine de son point d'entrée</b> : l'ADR-0010 lui donne
  /// la quatrième entrée du panneau et sa carte à l'accueil, au troisième rang. Il se marque donc
  /// comme les trois autres, et il porte le cadre partagé comme tous les écrans.
  /// </summary>
  internal const string Qualification = "/qualification";

  /// <summary>Le tableau des demandes RGPD, et la racine de son point d'entrée.</summary>
  internal const string Board = "/demandes";

  private const string Parametrage = "/parametrage";

  /// <summary>
  /// <b>La seconde face du Paramétrage</b>, et non un cinquième point d'entrée : elle pend sous
  /// <c>/parametrage</c>, porte le même titre que la première, et c'est « Paramétrage » qui reste
  /// marqué dans le panneau quand on la lit — on n'a pas quitté le Paramétrage.
  /// </summary>
  private const string ParametrageRabbitMq = "/parametrage/rabbitmq";
  internal const string ScreeningDeposit = "/detection/depot";
  private const string Connection = "/detection/connexion";
  private const string Report = "/detection";
  private const string ScreeningTable = "/detection/table";
  private const string Export = "/detection/export";

  /// <summary>L'écran d'une table, à l'adresse exacte sous laquelle la liste des écrans le lit.</summary>
  internal static string ScreeningTableScreen => TableOf(ScreeningTable);
  private const string History = "/detection/historique";
  private const string Archive = "/detection/archive";
  private const string ArchivedTable = "/detection/archive/table";

  private const string Schema = "public";
  private const string Table = "adherents";

  private static readonly DateTimeOffset GeneratedOn = new(2026, 8, 10, 9, 30, 0, TimeSpan.Zero);

  /// <summary>
  /// Les redirections ne sont pas suivies : ce sont les écrans eux-mêmes qu'on lit, et une écriture
  /// qui rendrait sa page directement ferait d'un rechargement une seconde déclaration.
  /// </summary>
  private readonly HttpClient _client = factory.CreateClient(
    new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

  /// <summary>
  /// <b>Les adresses de la surface de l'<c>Operator</c></b>, l'accueil compris, l'état de chacune
  /// posé par le chemin que le domaine autorise — deux dépôts de relevé pour qu'il existe un
  /// rapport courant et un rapport archivé. Le Paramétrage et le tableau des demandes RGPD se lisent
  /// sans qu'aucun état n'ait à être posé.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Les écrans de <c>/dossiers</c> n'y sont plus</b> : ils sont retirés, et leurs adresses
  /// rendent un 404 — voir <c>NoRetiredBoardSurfaceRemains</c>.
  /// </remarks>
  internal async Task<IReadOnlyList<string>> ScreensAsync()
  {
    var archived = await ScreenTwiceAsync();

    return
    [
      Doorstep,
      Qualification,
      Board,
      Parametrage,
      ParametrageRabbitMq,
      .. ScreeningScreens(archived),
    ];
  }

  /// <summary>
  /// <b>Les sept écrans du contexte de détection</b>, et eux seuls — l'état posé par le seul chemin
  /// que le domaine autorise, deux dépôts de relevé pour qu'il existe un rapport courant et un
  /// rapport archivé.
  /// </summary>
  internal async Task<IReadOnlyList<string>> ScreeningScreensAsync()
  {
    return ScreeningScreens(await ScreenTwiceAsync());
  }

  /// <summary>Le corps d'une adresse qui doit répondre, et le refus d'une adresse qui ne répond pas.</summary>
  internal async Task<string> ReadAsync(string address)
  {
    var response = await _client.GetAsync(address);

    response.StatusCode.ShouldBe(HttpStatusCode.OK, $"L'adresse {address} doit répondre.");

    return await response.Content.ReadAsStringAsync();
  }

  /// <summary>La réponse brute — de quoi lire un en-tête, ou peser un fichier binaire.</summary>
  internal async Task<HttpResponseMessage> FetchAsync(string address)
  {
    return await _client.GetAsync(address);
  }

  /// <summary>
  /// Les adresses qu'une page rendue fait <b>chercher chez un tiers</b>. Ce qui est retenu est ce
  /// qui déclenche une requête — feuilles, scripts, images —, jamais une URL qui n'est que du texte
  /// affiché : l'adresse d'un <c>Adapter</c> se lit à l'écran sans que le navigateur n'aille nulle
  /// part.
  /// </summary>
  internal static IReadOnlyList<string> ThirdPartyResourcesIn(string rendered)
  {
    IReadOnlyList<string> fetched =
    [
      .. Regex.Matches(rendered, @"<link\b[^>]*\bhref=""([^""]+)""").Select(m => m.Groups[1].Value),
      .. Regex.Matches(rendered, @"<script\b[^>]*\bsrc=""([^""]+)""").Select(m => m.Groups[1].Value),
      .. Regex.Matches(rendered, @"<img\b[^>]*\bsrc=""([^""]+)""").Select(m => m.Groups[1].Value),
    ];

    return [.. fetched.Where(IsThirdParty)];
  }

  /// <summary>
  /// Les attributs de <b>chaque balise <c>script</c></b> d'une page rendue, une entrée par balise, qu'elle charge un fichier
  /// ou porte son code en ligne : un script en ligne s'exécute aussi bien qu'un script chargé.
  /// </summary>
  internal static IReadOnlyList<string> ScriptAttributesIn(string rendered)
  {
    return [.. Regex.Matches(rendered, @"<script\b([^>]*)>", RegexOptions.IgnoreCase).Select(m => m.Groups[1].Value)];
  }

  /// <summary>
  /// Les <b>deux régions de navigation</b> d'une page rendue, isolées de tout le reste et l'une de
  /// l'autre : le <b>panneau latéral</b>, qui porte les quatre points d'entrée et disparaît quand
  /// l'Operator le replie, et le <b>header</b>, qui porte ce qui ne doit jamais disparaître — le
  /// hamburger, le nom du service, la version.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Ce qui se lit dedans n'est jamais confondu avec ce que l'écran écrit dessous : le tableau des
  /// demandes RGPD porte le même nom dans le panneau et dans son titre, et une assertion sur la
  /// navigation qui lirait la page entière passerait pour de mauvaises raisons.
  /// </para>
  /// <para>
  /// ⚠️ <b>Le compte est vérifié, et pas seulement la présence</b> — comme pour l'élément de version.
  /// Le <b>layout</b> de l'écran — tout ce qui est hors du <c>main</c> — porte <b>exactement deux</b>
  /// <c>nav</c>, et chacun est reconnu <b>à sa classe</b>. Un troisième <c>nav</c> de layout posé un
  /// jour ferait échouer ce compte plutôt que de se faire lire à la place de l'un des deux, ce qui
  /// est la seule façon dont un tel ajout pouvait se retourner en silence. ⚠️ La navigation qu'un
  /// <b>écran</b> pose dans son <c>main</c> — les onglets du Paramétrage — n'en relève pas : elle
  /// décrit l'intérieur d'un écran, pas les points d'entrée du service.
  /// </para>
  /// <para>
  /// ⚠️ <b>Les deux régions se lisent SÉPARÉMENT, et jamais concaténées.</b> Le nom du service est
  /// « le premier lien du header » ; recoller les deux régions ferait du premier point d'entrée le
  /// premier lien, et <c>WordmarkIn</c> se mettrait à lire <c>Configuration</c> sans échouer.
  /// </para>
  /// </remarks>
  internal static (string Sidepanel, string Header) NavigationOf(string rendered)
  {
    // ⚠️ LE `main` EST RETIRÉ AVANT LE COMPTE, et c'est ce qui garde le compte VRAI plutôt que de le
    // desserrer : ce qui est compté ici est ce que LE LAYOUT pose, et le layout pose ses deux
    // régions HORS du `main`. Un écran qui porte sa propre navigation — les onglets du Paramétrage —
    // en pose une DEDANS, et elle ne relève pas de ce compte. Compter la page entière aurait
    // confondu les deux, et desserrer le compte à « au moins deux » aurait rendu muet le seul cas
    // que ce compte attrape : un troisième `nav` de layout posé un jour, qui se ferait lire à la
    // place de l'un des deux.
    var layout = Regex.Replace(rendered, @"<main\b[^>]*>.*?</main>", string.Empty, RegexOptions.Singleline);

    var regions = Regex.Matches(layout, @"<nav\b([^>]*)>(.*?)</nav>", RegexOptions.Singleline);

    regions.Count.ShouldBe(
      2,
      "Le layout de l'écran doit porter exactement deux régions de navigation — le panneau latéral et "
      + "le header : un `nav` de plus se ferait lire à la place de l'un des deux.");

    return (RegionOf(regions, "sidepanel"), RegionOf(regions, "header"));
  }

  /// <summary>
  /// <b>Le panneau latéral des points d'entrée</b> d'une page rendue. C'est lui qui porte la liste
  /// des quatre entrées et le marquage de l'écran courant.
  /// </summary>
  internal static string SidepanelIn(string rendered)
  {
    return NavigationOf(rendered).Sidepanel;
  }

  /// <summary>
  /// <b>Le header du service</b> d'une page rendue : le hamburger, le nom du service, la version.
  /// Il survit au repli du panneau, et c'est ce qui fait qu'un écran au panneau replié garde un
  /// chemin de retour.
  /// </summary>
  internal static string HeaderIn(string rendered)
  {
    return NavigationOf(rendered).Header;
  }

  /// <summary>
  /// La région reconnue à sa <b>classe</b> — comme un mot de la liste, et non comme la valeur exacte
  /// de l'attribut : une seconde classe posée un jour à côté ne la rendrait pas invisible aux tests.
  /// </summary>
  private static string RegionOf(MatchCollection regions, string name)
  {
    var found = regions.Where(region => Regex.IsMatch(
      region.Groups[1].Value,
      @"\bclass=""(?:[^""]*\s)?" + name + @"(?:\s[^""]*)?""")).ToList();

    found.Count.ShouldBe(
      1, $"L'écran doit porter exactement une région de navigation `{name}`.");

    return found[0].Groups[2].Value;
  }

  /// <summary>
  /// Le <b>nom du service</b> tel que la barre le porte : ce qu'on lit, et l'adresse où il mène. Il
  /// est le premier lien de la barre <b>sans en être une entrée</b> — c'est ce qui donne le retour à
  /// l'accueil depuis n'importe quel écran sans ajouter une quatrième entrée.
  /// </summary>
  internal static (string Address, string Text) WordmarkIn(string bar)
  {
    var wordmark = Regex.Match(bar, @"<a\b([^>]*)>(.*?)</a>", RegexOptions.Singleline);

    wordmark.Success.ShouldBeTrue("La barre ne porte aucun lien.");

    return (
      Regex.Match(wordmark.Groups[1].Value, @"\bhref=""([^""]*)""").Groups[1].Value,
      TextIn(wordmark.Groups[2].Value));
  }

  /// <summary>
  /// <b>La version du produit</b> telle que la barre la porte : le texte de chaque élément
  /// <c>.version</c>, dans l'ordre. Ce que la barre doit porter est <b>un seul</b> élément —
  /// la liste est rendue entière plutôt qu'un premier élément trouvé, pour que le compte se
  /// vérifie et non seulement la présence.
  /// </summary>
  internal static IReadOnlyList<string> VersionsIn(string bar)
  {
    return
    [
      .. Regex.Matches(bar, VersionElement, RegexOptions.Singleline)
        .Select(version => TextIn(version.Groups[1].Value)),
    ];
  }

  /// <summary>
  /// La barre <b>sans l'élément de version</b> — pour le balayage des chiffres. ⚠️ L'exception est
  /// <b>retirée</b>, pas tolérée : le balayage garde tout le reste de la barre au chiffre près, et
  /// c'est ce qui fait qu'un compteur glissé à côté de la version échouerait encore.
  /// </summary>
  internal static string WithoutTheVersion(string bar)
  {
    return Regex.Replace(bar, VersionElement, string.Empty, RegexOptions.Singleline);
  }

  /// <summary>
  /// La région <b>sans le tracé du hamburger</b> — pour le balayage des chiffres.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>C'est la SECONDE exception nommée au balayage, et elle est d'une autre nature que la
  /// version.</b> Celle-ci est un chiffre qu'on <b>lit</b> et qu'on ignore ; le tracé du hamburger
  /// n'est pas lu du tout — ses <c>viewBox="0 0 18 18"</c> et ses coordonnées de chemin sont des
  /// mesures de dessin, qu'aucun œil ne rencontre comme un nombre. La règle que le balayage garde
  /// est « aucun COMPTE dans la navigation » ; un chiffre de géométrie n'en est pas un.
  /// <para>
  /// Le retrait porte sur l'élément <c>svg</c> entier, et il est <b>étroit</b> : un compteur glissé
  /// à côté du glyphe, dans le <c>label</c> mais hors du <c>svg</c>, fait toujours échouer le test.
  /// </para>
  /// </remarks>
  internal static string WithoutTheBurgerGlyph(string bar)
  {
    return Regex.Replace(bar, @"<svg\b[^>]*>.*?</svg>", string.Empty, RegexOptions.Singleline);
  }

  /// <summary>
  /// <b>La version informationnelle de l'assemblage Web</b>, lue par réflexion sur l'attribut que le
  /// build y écrit — et non recopiée : ce que le test garde est que l'écran <b>montre ce que
  /// l'assemblage porte</b>, quelle que soit la valeur du jour. La règle « une seule source » se
  /// vérifie ainsi de bout en bout : la propriété de build, l'attribut, l'écran.
  /// </summary>
  internal static string InformationalVersionOfTheWebAssembly()
  {
    var attribute = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();

    attribute.ShouldNotBeNull("L'assemblage Web ne porte aucune version informationnelle.");

    return attribute.InformationalVersion;
  }

  /// <summary>
  /// L'élément de version, reconnu à sa <b>classe</b> — comme un mot de la liste, et non comme la
  /// valeur exacte de l'attribut : une seconde classe posée un jour à côté ne le rendrait pas
  /// invisible aux tests, ce qui ferait passer le balayage des chiffres pour de mauvaises raisons.
  /// </summary>
  private const string VersionElement =
    @"<span\b[^>]*\bclass=""(?:[^""]*\s)?version(?:\s[^""]*)?""[^>]*>(.*?)</span>";

  /// <summary>
  /// <b>La liste des points d'entrée</b>, isolée du nom du service qui la précède : la barre porte
  /// un lien de plus qu'elle n'a d'entrées, et confondre les deux ferait passer un cinquième point
  /// d'entrée pour le retour à l'accueil.
  /// </summary>
  internal static string EntryPointListIn(string bar)
  {
    var list = Regex.Match(bar, @"<ul\b[^>]*>(.*?)</ul>", RegexOptions.Singleline);

    list.Success.ShouldBeTrue("La barre ne porte aucune liste de points d'entrée.");

    return list.Groups[1].Value;
  }

  /// <summary>
  /// Le point d'entrée que la barre doit <b>marquer</b> sur un écran : un seul, <b>sauf sur
  /// l'accueil</b>, qui n'en relève d'aucun et n'en marque donc aucun — marquer une entrée là
  /// reviendrait à dire qu'on est déjà dans ce que la porte ouvre. ⚠️ <b>C'est désormais la seule
  /// exception, et elle ne s'éteindra pas</b> : l'accueil est la porte, il ne relèvera jamais d'une
  /// entrée. La qualification, elle, a reçu la sienne par l'ADR-0010 et se marque comme les trois
  /// autres.
  /// </summary>
  internal static IReadOnlyList<string> MarkedEntryPointsOn(string screen)
  {
    return screen == Doorstep ? [] : [EntryPointOf(screen)];
  }

  /// <summary>
  /// <b>Ce qu'un fragment rendu donne à LIRE</b> : le balisage retiré, les entités rendues à leur
  /// caractère, et les blancs du gabarit ramenés à un espace.
  /// </summary>
  /// <remarks>
  /// ⚠️ Sans cela, une phrase gelée ne serait comparable qu'à condition de tenir sur une seule ligne
  /// du gabarit, et une apostrophe — que l'encodeur écrit <c>&amp;#x27;</c> — ferait échouer la
  /// comparaison sur un texte que l'humain lit pourtant mot pour mot.
  /// </remarks>
  internal static string TextIn(string fragment)
  {
    var stripped = Regex.Replace(fragment, "<!--.*?-->", " ", RegexOptions.Singleline);

    stripped = Regex.Replace(stripped, "<[^>]*>", " ");

    return Regex.Replace(WebUtility.HtmlDecode(stripped), @"\s+", " ").Trim();
  }

  /// <summary>Le contenu du <c>main</c> d'une page rendue — l'écran lui-même, sans son layout.</summary>
  internal static string MainOf(string rendered)
  {
    var main = Regex.Match(rendered, @"<main\b[^>]*>(.*?)</main>", RegexOptions.Singleline);

    main.Success.ShouldBeTrue("La page rendue ne porte aucun main.");

    return main.Groups[1].Value;
  }

  /// <summary>
  /// Les <b>liens de bloc</b> d'un fragment rendu : chacun avec l'adresse où il mène, ses attributs
  /// et ce qu'il enveloppe.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Un lien IMBRIQUÉ ne ressort pas ici comme un lien de plus</b>, et c'est une propriété de
  /// la lecture, pas un oubli : la fermeture est prise <b>au plus court</b>, si bien que le lien
  /// intérieur est avalé dans le contenu du lien extérieur et que le compte reste le même. Un lien
  /// posé <b>à côté</b>, lui, ressort. Qui garde l'absence de cible secondaire doit donc regarder
  /// <b>aussi</b> ce que le contenu porte — le compte seul se tairait.
  /// </remarks>
  internal static IReadOnlyList<(string Address, string Attributes, string Contents)> LinkBlocksIn(string fragment)
  {
    return
    [
      .. Regex.Matches(fragment, @"<a\b([^>]*)>(.*?)</a>", RegexOptions.Singleline).Select(link =>
      (
        Address: Regex.Match(link.Groups[1].Value, @"\bhref=""([^""]*)""").Groups[1].Value,
        Attributes: link.Groups[1].Value,
        Contents: link.Groups[2].Value
      )),
    ];
  }

  /// <summary>
  /// Ce que le corps de la page porte <b>hors de la navigation, hors du layout et hors du
  /// <c>main</c></b>. Un <c>main</c> qui existe ne dit pas encore qu'il enveloppe : la question est
  /// de savoir ce qui est resté dehors, et la réponse doit être « rien ».
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Les balises ouvrantes retirées sont NOMMÉES par leur classe</b> — le layout et la colonne,
  /// et elles seules : un <c>div</c> quelconque laissé dehors par un écran reste donc visible au
  /// balayage. Les <b>fermantes</b>, elles, ne portent aucune classe et sont indistinguables ; elles
  /// sont retirées toutes. C'est la limite connue de ce balayage, et elle est étroite : ce qu'un
  /// <c>div</c> égaré porterait — du <b>texte</b>, un titre, un tableau — survit à ce retrait et fait
  /// toujours échouer le test. Seule une paire de balises rigoureusement vide passerait.
  /// </para>
  /// </remarks>
  internal static string OutsideTheMainOf(string rendered)
  {
    var body = Regex.Match(rendered, @"<body\b[^>]*>(.*?)</body>", RegexOptions.Singleline);

    body.Success.ShouldBeTrue("La page rendue ne porte aucun corps.");

    var main = Regex.Match(body.Groups[1].Value, @"<main\b[^>]*>.*?</main>", RegexOptions.Singleline);

    main.Success.ShouldBeTrue("Le corps de la page rendue ne porte aucun main.");

    var outside = body.Groups[1].Value.Remove(main.Index, main.Length);

    outside = Regex.Replace(outside, @"<nav\b[^>]*>.*?</nav>", string.Empty, RegexOptions.Singleline);

    // La case du repli : elle porte l'état du panneau latéral, et elle doit précéder le layout pour que le
    // sélecteur `:checked ~ .layout` l'atteigne. Elle n'a donc pas sa place dans le `main`.
    outside = Regex.Replace(outside, @"<input\b[^>]*\bclass=""sidepanel-toggle""[^>]*>", string.Empty);

    // Les deux enveloppes de disposition, nommées par leur classe — puis les fermantes, qui n'en
    // portent aucune. Voir la limite consignée ci-dessus.
    outside = Regex.Replace(outside, @"<div\b[^>]*\bclass=""(?:layout|column)""[^>]*>", string.Empty);
    outside = Regex.Replace(outside, @"</div>", string.Empty);

    return outside.Trim();
  }

  /// <summary>
  /// Les liens de la barre, dans l'ordre où elle les pose, chacun avec l'adresse qu'il mène et le
  /// <b>marquage de l'écran courant</b> qu'il porte ou non.
  /// </summary>
  internal static IReadOnlyList<(string Address, bool IsCurrent)> LinksIn(string bar)
  {
    return
    [
      .. Regex.Matches(bar, @"<a\b([^>]*)>").Select(link =>
      (
        Address: Regex.Match(link.Groups[1].Value, @"\bhref=""([^""]*)""").Groups[1].Value,
        IsCurrent: link.Groups[1].Value.Contains(@"aria-current=""page""", StringComparison.Ordinal)
      )),
    ];
  }

  /// <summary>
  /// Le point d'entrée <b>dont un écran relève</b>, lu sur sa seule adresse : tout ce qui pend sous
  /// la détection des données personnelles relève de <c>/detection</c>, et le Paramétrage, la
  /// qualification et le tableau des demandes RGPD de leur propre racine.
  /// </summary>
  internal static string EntryPointOf(string screen)
  {
    var path = screen.Split('?')[0];

    var entryPoint = EntryPoints.SingleOrDefault(
      candidate => path == candidate || path.StartsWith($"{candidate}/", StringComparison.Ordinal));

    entryPoint.ShouldNotBeNull($"L'écran {screen} ne relève d'aucun des quatre points d'entrée.");

    return entryPoint;
  }

  private static bool IsThirdParty(string address)
  {
    return address.StartsWith("//", StringComparison.Ordinal)
      || address.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
      || address.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
  }

  private static IReadOnlyList<string> ScreeningScreens(string archived)
  {
    return
    [
      ScreeningDeposit,
      // ⚠️ L'écran d'attente d'un scan n'est PAS ici, et son absence est délibérée : son adresse
      // porte l'identité d'un scan vivant, qui meurt avec le processus. Le harnais aurait dû faire
      // partir un vrai scan et le prendre en cours de route pour l'y mettre — c'est-à-dire faire
      // dépendre le test du LAYOUT d'une course entre deux fils.
      Connection,
      Report,
      TableOf(ScreeningTable),
      Export,
      History,
      $"{Archive}?screening={Uri.EscapeDataString(archived)}",
      TableOf(ArchivedTable, $"screening={Uri.EscapeDataString(archived)}&"),
    ];
  }

  private static string TableOf(string screen, string leading = "")
  {
    return $"{screen}?{leading}schema={Uri.EscapeDataString(Schema)}&table={Uri.EscapeDataString(Table)}";
  }

  /// <summary>
  /// Dépose deux relevés et rend le rapport que le second a archivé : c'est le seul chemin vers un
  /// rapport de détection archivé, puisque « archivé » n'est écrit nulle part et se recalcule à
  /// chaque rendu.
  /// </summary>
  private async Task<string> ScreenTwiceAsync()
  {
    await DepositAListingAsync();

    var archived = await CurrentScreeningAsync();

    await DepositAListingAsync();

    return archived;
  }

  private async Task DepositAListingAsync()
  {
    var fields = new List<KeyValuePair<string, string>>
    {
      new("__RequestVerificationToken", await TokenOfAsync(ScreeningDeposit)),
      new("Paste", Paste()),
    };

    var deposited = await _client.PostAsync(ScreeningDeposit, new FormUrlEncodedContent(fields));

    deposited.StatusCode.ShouldBe(
      HttpStatusCode.Redirect, "Le dépôt d'un relevé sincère doit mener au rapport qu'il produit.");
  }

  /// <summary>
  /// Le rapport que l'écran de la table rendait, lu <b>sur son formulaire</b> comme le fait un
  /// navigateur — le seul moyen de retenir l'identité d'un rapport avant qu'un second dépôt ne
  /// l'archive.
  /// </summary>
  private async Task<string> CurrentScreeningAsync()
  {
    var address = TableOf(ScreeningTable);
    var screening = Regex.Match(await ReadAsync(address), @"name=""Screening"" value=""([^""]+)""");

    screening.Success.ShouldBeTrue($"Le formulaire de {address} ne dit pas quel rapport il rendait.");

    return screening.Groups[1].Value;
  }

  /// <summary>Un collage sincère d'une seule colonne, dans la forme que la requête de relevé émet.</summary>
  private static string Paste()
  {
    var header = $$"""
      {"format":"{{ColumnListing.FormatVersion}}","dialecte":"postgresql","base":"galette_prod","genere_le":"{{GeneratedOn.ToString("O", CultureInfo.InvariantCulture)}}"}
      """;

    var column = $$"""
      {"schema":"{{Schema}}","table":"{{Table}}","colonne":"email","position":1,"type":"varchar(255)","nullable":true,"commentaire_colonne":null,"commentaire_table":null,"table_referencee":null}
      """;

    return string.Join('\n', header, column, """{"fin":true,"colonnes":1}""");
  }

  private async Task<string> TokenOfAsync(string address)
  {
    var token = Regex.Match(
      await ReadAsync(address),
      @"<input name=""__RequestVerificationToken""[^>]*value=""([^""]+)""");

    token.Success.ShouldBeTrue($"Le formulaire de {address} ne porte aucun jeton anti-rejeu.");

    return token.Groups[1].Value;
  }
}
