namespace MicroserviceRgpd.Web.Pages.Shared;

/// <summary>Un point d'entrée : ce qu'on lit, où l'on arrive, et ce qu'on y fait.</summary>
/// <param name="NavigationLabel">
/// Le libellé <b>que la barre porte</b>, en français — c'est un texte destiné à l'humain.
/// </param>
/// <param name="DoorwayName">
/// Le nom <b>que la carte de l'accueil porte</b>, et que l'écran reprend en titre.
/// </param>
/// <param name="Address">L'adresse de l'écran d'entrée, et la racine de tout ce qui pend sous lui.</param>
/// <param name="Moment">
/// <b>Le moment du parcours</b> sous lequel l'accueil range la porte : avant toute demande, ou quand
/// une demande arrive. La barre l'ignore.
/// </param>
/// <param name="DoorwaySentence">
/// Ce que la <b>porte de l'accueil</b> dit, et que la barre ne dit pas : celle-ci n'a la
/// place que d'un nom. Les quatre textes vivent ici plutôt que dans le gabarit de l'accueil parce
/// que c'est le seul endroit qui tienne ensemble, <b>dans un seul ordre</b>, le nom, l'adresse et ce
/// qu'on trouve derrière — trois listes parallèles se seraient décalées d'un cran un jour.
/// </param>
/// <remarks>
/// ⚠️ <b>DEUX CHAMPS DE TEXTE, ET C'EST UN COÛT ASSUMÉ</b> — voir <c>ADR-0008</c>. Un écran sur les
/// quatre a <b>deux noms vivants</b> : la barre, où le wordmark le précède, n'a pas besoin de la
/// forme pleine que la carte porte seule. Les trois autres répètent la même chaîne dans les deux
/// champs, et cette répétition est <b>la forme normale</b> : un champ vide ou nul aurait fait de
/// « la barre reprend le nom de la carte » une règle implicite qu'aucun lecteur ne verrait.
/// </remarks>
internal sealed record EntryPoint(
  string NavigationLabel,
  string DoorwayName,
  string Address,
  Moment Moment,
  string DoorwaySentence)
{
  /// <summary>
  /// Si l'écran rendu relève de ce point d'entrée. La comparaison est faite <b>par segments</b> :
  /// la table d'arbitrage de la détection des données personnelles relève de <c>/detection</c> — ce
  /// que le préfixe de texte nu n'aurait pas su dire sans confondre aussi une adresse qui commence
  /// par les mêmes lettres.
  /// </summary>
  internal bool IsCurrent(PathString path)
  {
    return path.StartsWithSegments(Address, StringComparison.OrdinalIgnoreCase);
  }
}

/// <summary>
/// <b>Un moment du parcours</b>, sous lequel l'accueil groupe ses portes : un titre, et une phrase
/// qui dit ce qu'on y fait.
/// </summary>
/// <remarks>
/// ⚠️ <b>Le moment est porté par chaque point d'entrée, et non par une seconde liste</b> : l'accueil
/// groupe <see cref="Navigation.EntryPoints"/> dans son ordre, et une liste de groupes tenue à côté se
/// serait décalée d'un cran un jour.
/// </remarks>
internal sealed record Moment(string Title, string Sentence);

/// <summary>
/// <b>La barre de navigation de la surface de l'<c>Operator</c></b> : le nom du service, puis les
/// quatre points d'entrée du service, et le marquage de celui dont l'écran courant relève.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Quatre entrées, et le compte n'est plus une clause.</b> Ce que l'ADR-0006 tenait à trois
/// — « pas une quatrième » — reposait sur la lisibilité d'une ligne horizontale, et la ligne a
/// disparu avec la barre que l'ADR-0009 a retirée. L'ADR-0010 supplante donc cette clause : le
/// panneau porte ce qu'on y met, et une entrée se justifie par <b>ce qu'elle sert</b> — un contexte
/// borné sans surface —, pas par un compte. ⚠️ Le compte qui cesse d'être une clause ne devient pas
/// une invitation : l'historique des rapports de détection n'est toujours pas une entrée, il
/// s'atteint depuis le rapport de détection courant.
/// </para>
/// <para>
/// La clause « une barre à trois entrées se lit d'un coup d'œil » ne tient plus telle quelle, et
/// elle est érodée <b>en connaissance de cause</b> : <b>trois</b> des quatre libellés portent leur
/// forme pleine, et la barre <b>passera à la ligne</b> sur un écran étroit. Elle est en
/// <c>flex-wrap</c>, donc rien ne s'y déforme — c'est le prix accepté pour des noms qui disent ce
/// qu'ils mènent.
/// </para>
/// <para>
/// ⚠️ <b>Celui du paramétrage, lui, est court, et il est le seul</b> : l'écran se dit
/// <c>Paramétrage</c> ici et <c>Paramétrage du microservice RGPD</c> partout ailleurs — parce
/// que le wordmark <see cref="ServiceName"/> le précède <b>dans cette barre et nulle part
/// ailleurs</b>. Ce n'est donc pas un précédent pour raccourcir les trois autres : leur forme pleine
/// ne répète rien de ce qui les précède. Voir <c>ADR-0008</c> et, pour le nom, <c>ADR-0016</c>.
/// </para>
/// <para>
/// ⚠️ <b>Aucun compteur, aucun badge numérique</b>, et il ne doit jamais y en avoir. La règle des
/// chiffres que le service applique — un « 0 demande en retard » se lit comme une
/// mesure rassurante là où la phrase dit ce qu'elle est — vaut d'autant plus pour une barre qui se
/// répète sur tous les écrans sans qu'on l'ouvre jamais.
/// </para>
/// <para>
/// ⚠️ <b>Une seule exception, nommée et étroite : la version du produit</b> (<c>v0.1.0</c>), à
/// l'extrémité droite de la barre, portée par <see cref="ProductVersion"/> — de même nature que
/// les références d'articles RGPD sur l'accueil : un chiffre qui n'est ni un compte ni une mesure,
/// et qui se lit et s'ignore. Elle n'est <b>pas</b> une entrée : elle vit hors de
/// <see cref="EntryPoints"/>, qui reste à quatre.
/// </para>
/// </remarks>
internal static class Navigation
{
  /// <summary>
  /// Le nom du service, porté devant les quatre liens — et, depuis l'accueil, <b>le chemin du
  /// retour</b> : il est un lien vers la racine sur tous les écrans. C'est aussi le titre de
  /// l'accueil et la moitié droite du titre d'onglet de tous les écrans.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>C'est un NOM DE PRODUIT, pas une description</b>, et le changement de registre est
  /// délibéré — voir <c>ADR-0008</c>. « Droits des personnes concernées » disait ce que le service
  /// <b>fait</b> ; ce nom dit ce qu'il <b>est</b>. La capitale à <b>M</b>icroservice est ce qui le
  /// distingue du nom commun « microservice RGPD » que portent les phrases du domaine.
  /// </remarks>
  internal const string ServiceName = "Microservice RGPD";

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
  /// Les quatre points d'entrée, dans <b>l'ordre où l'on rencontre les écrans</b> : on configure,
  /// on détecte, on qualifie, on traite. ⚠️ <b>Ce n'est plus un ordre de mise en route</b> : rien
  /// n'oblige à qualifier un texte avant d'enregistrer une demande — une demande peut arriver déjà
  /// qualifiée, et elle s'enregistre sans qu'aucune qualification n'ait eu lieu. Ce que la
  /// position apprend et qu'aucun nom ne dit tient au premier rang seul : on y règle les adresses
  /// dont l'exercice des droits dépendra. Voir <c>ADR-0010</c> et <c>ADR-0016</c>.
  /// </summary>
  /// <summary>Le temps d'avant : on règle les canaux, on présume les données.</summary>
  internal static Moment BeforeAnyRequest { get; } =
    new("Avant toute demande", "On règle les canaux, on présume les données.");

  /// <summary>Le temps de la demande : on qualifie le texte, on suit la demande.</summary>
  internal static Moment WhenARequestArrives { get; } =
    new("Quand une demande arrive", "On qualifie le texte, on suit la demande.");

  internal static IReadOnlyList<EntryPoint> EntryPoints { get; } =
  [
    // ⚠️ LE SEUL ÉCRAN À DEUX NOMS, et c'est la conséquence directe du wordmark. « Paramétrage du
    // microservice RGPD » dans une barre qui dit déjà « Microservice RGPD » répétait le nom du
    // service à quinze centimètres de lui-même ; la carte, elle, n'a rien qui la précède et garde
    // donc la forme pleine — qui reste aussi le titre de l'écran. Le prix consigné : la redondance
    // survit sur l'accueil, où la barre surmonte les cartes. Voir ADR-0008.
    new(
      "Paramétrage",
      "Paramétrage du microservice RGPD",
      "/parametrage",
      BeforeAnyRequest,
      "Vous y associez à chacun des six droits RGPD le canal par lequel le service l'exercera : une " +
      "adresse HTTP, ou un routage RabbitMQ. Un droit sans l'un ni l'autre reste « non configuré », " +
      "et rien ne vous oblige à les renseigner tous."),
    new(
      "Détection des données personnelles",
      "Détection des données personnelles",
      "/detection",
      BeforeAnyRequest,
      // ⚠️ « JAMAIS UNE VALEUR » A CESSÉ D'ÊTRE VRAI, et la phrase a suivi. La voie connectée
      // prélève quelques valeurs par colonne pour affiner la détection : les laisser promises
      // absentes ici aurait fait de la carte d'accueil le seul endroit du service qui mente sur ce
      // que le scan lit. Ce qui reste vrai, et que la phrase dit, est qu'aucune valeur lue ne
      // survit au scan.
      "Vous y faites scanner une base par le service, ou vous collez un schéma vous-même. Il " +
      "signale les colonnes susceptibles de porter des données personnelles ; aucune valeur lue " +
      "n'est conservée, et vous tranchez ligne par ligne."),

    // ⚠️ LE TROISIÈME RANG PORTE LE SENS, et c'est le seul qui rende l'ordre lisible comme une
    // phrase — on configure, on détecte, on qualifie, on traite. Premier, l'écran précéderait la
    // configuration dont tout dépend ; dernier, il suivrait le traitement qu'il précède dans les
    // faits. Voir ADR-0010.
    // ⚠️ LA PHRASE DIT QU'AUCUNE DEMANDE N'EN DÉCOULE, et elle le dit parce que la carte est le seul
    // endroit où on le lit avant d'y arriver : l'écran qualifie et rend le verdict, il n'ouvre rien
    // et ne rattache rien. Le verbe est PROPOSER, jamais « décider » — le service dit, l'humain
    // tranche —, exactement comme la détection des données personnelles SIGNALE sans conclure.
    new(
      "Qualification",
      "Qualification",
      "/qualification",
      WhenARequestArrives,
      "Vous y collez le texte libre d'une demande, et le service propose les droits RGPD qu'elle " +
      "exerce. Aucune demande n'en découle : la proposition se lit ici, elle ne s'y dépose pas."),

    // ⚠️ PAS UNE PHRASE, ET C'EST DÉLIBÉRÉ : la carte nomme, sans verbe, ce à quoi l'écran sert, là
    // où les trois autres disent le geste qu'on y pose. Le parallélisme « Vous y + verbe » ne se
    // « rétablit » pas. ⚠️ L'écran ne liste encore aucune demande : la carte annonce la
    // consultation qu'il portera, et qu'un ticket suivant construit.
    // ⚠️ SON « RGPD » RESTE, et ce n'est pas une redondance avec le wordmark : ici le mot qualifie
    // LES DEMANDES — celles que le règlement régit —, là il nomme LE SERVICE. Le retirer effacerait
    // une qualification juridique, pas une répétition.
    new(
      "Tableau des demandes RGPD",
      "Tableau des demandes RGPD",
      "/demandes",
      WhenARequestArrives,
      "Consultation des demandes RGPD en cours"),
  ];
}
