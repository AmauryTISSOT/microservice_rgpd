namespace MicroserviceRgpd.Web.Pages.Shared;

/// <summary>Un point d'entrée : ce qu'on lit, où l'on arrive, et ce qu'on y fait.</summary>
/// <param name="Label">Le libellé, en français — c'est un texte destiné à l'humain.</param>
/// <param name="Address">L'adresse de l'écran d'entrée, et la racine de tout ce qui pend sous lui.</param>
/// <param name="DoorwaySentence">
/// La phrase que la <b>porte de l'accueil</b> dit, et que la barre ne dit pas : celle-ci n'a la
/// place que d'un nom. Les trois phrases vivent ici plutôt que dans le gabarit de l'accueil parce
/// que c'est le seul endroit qui tienne ensemble, <b>dans un seul ordre</b>, le nom, l'adresse et ce
/// qu'on trouve derrière — trois listes parallèles se seraient décalées d'un cran un jour.
/// </param>
internal sealed record ChromeEntryPoint(string Label, string Address, string DoorwaySentence)
{
  /// <summary>
  /// Si l'écran rendu relève de ce point d'entrée. La comparaison est faite <b>par segments</b> :
  /// un dossier relève du tableau des demandes RGPD, la reprise d'une déclaration du
  /// <c>Manifest</c>, et la table d'arbitrage de la détection des données personnelles, sous
  /// <c>/detection</c> — ce que le préfixe de texte nu n'aurait pas su dire sans confondre aussi une
  /// adresse qui commence par les mêmes lettres.
  /// </summary>
  internal bool IsCurrent(PathString path)
  {
    return path.StartsWithSegments(Address, StringComparison.OrdinalIgnoreCase);
  }
}

/// <summary>
/// <b>La barre de navigation de la surface de l'<c>Operator</c></b> : le nom du service, puis les
/// trois points d'entrée du service, et le marquage de celui dont l'écran courant relève.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Trois entrées, et pas une quatrième.</b> L'historique des rapports de détection n'en est
/// pas une : il s'atteint depuis le rapport de détection courant, et une barre à trois entrées se
/// parcourt moins qu'une barre à quatre.
/// </para>
/// <para>
/// La clause « une barre à trois entrées se lit d'un coup d'œil » ne tient plus telle quelle, et
/// elle est érodée <b>en connaissance de cause</b> : les trois libellés portent leur forme pleine,
/// et la barre <b>passera à la ligne</b> sur un écran étroit. Elle est en <c>flex-wrap</c>, donc
/// rien ne s'y déforme — c'est le prix accepté pour des noms qui disent ce qu'ils mènent.
/// </para>
/// <para>
/// ⚠️ <b>Aucun compteur, aucun badge numérique</b>, et il ne doit jamais y en avoir. La règle des
/// chiffres que le tableau des demandes RGPD applique — un « 0 dossier en retard » se lit comme une
/// mesure rassurante là où la phrase dit ce qu'elle est — vaut d'autant plus pour une barre qui se
/// répète sur tous les écrans sans qu'on l'ouvre jamais.
/// </para>
/// </remarks>
internal static class ChromeNavigation
{
  /// <summary>
  /// Le nom du service, porté devant les trois liens — et, depuis l'accueil, <b>le chemin du
  /// retour</b> : il est un lien vers la racine sur tous les écrans.
  /// </summary>
  internal const string ServiceName = "Droits des personnes concernées";

  /// <summary>L'adresse de l'accueil. Ce n'est pas un point d'entrée : c'est la porte.</summary>
  internal const string Doorstep = "/";

  /// <summary>
  /// <b>Ce que le service est</b>, dit au seuil en une phrase — registre juridique, troisième
  /// personne. Les cartes, elles, s'adressent à la deuxième personne à celui qui va cliquer : le
  /// partage est délibéré.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>L'énumération d'articles est NON CONTIGUË, et elle doit le rester</b> : la taxonomie de
  /// <c>DataSubjectRight</c> est fermée à six droits et exclut explicitement l'art. 22.
  /// « Articles 15 à 21 » promettrait un droit que le service refuse.
  /// </para>
  /// <para>
  /// ⚠️ Le verbe est <b>présumer</b>, jamais « établir » : la détection des données personnelles
  /// signale, elle ne conclut pas.
  /// </para>
  /// </remarks>
  internal const string Presentation =
    "Ce service instruit les demandes par lesquelles une personne concernée exerce les six droits " +
    "prévus aux articles 15 à 18, 20 et 21 du RGPD. Il sert aussi, avant toute demande, à présumer " +
    "les données qu'un système détient.";

  /// <summary>
  /// Les trois points d'entrée, dans <b>l'ordre de mise en route</b> : on configure, puis on
  /// détecte, puis on traite les demandes. La position apprend ce qu'aucun nom ne dit — que rien ne
  /// fonctionne dans le service tant que l'<c>Operator</c> n'a pas déclaré ses systèmes et les
  /// adresses de leurs <c>Adapter</c>.
  /// </summary>
  internal static IReadOnlyList<ChromeEntryPoint> EntryPoints { get; } =
  [
    new(
      "Configuration du microservice RGPD",
      "/manifest",
      "Vous y déclarez, à la main et un par un, les systèmes où vivent des données personnelles. " +
      "Le service ne connaît que ceux que vous y inscrivez, et rien ne garantit qu'il n'en existe " +
      "pas d'autres."),
    new(
      "Détection des données personnelles",
      "/detection",
      "Vous y collez un schéma de base de données, et le service signale les colonnes susceptibles " +
      "de porter des données personnelles. Il lit des noms de tables et de colonnes, jamais une " +
      "valeur ; vous tranchez, ligne par ligne."),

    // ⚠️ UNE SEULE PHRASE, ET C'EST DÉLIBÉRÉ. « Lire » est le seul des trois verbes qui ne soit pas
    // un geste, et la brièveté dit par sa forme qu'on ne pose rien sur cet écran. Le parallélisme ne
    // se « rétablit » pas : la seconde phrase qui l'aurait rétabli a été jugée sans apport.
    new(
      "Tableau des demandes RGPD",
      "/dossiers",
      "Vous y lisez les demandes RGPD en cours, rangées par échéance."),
  ];
}
