namespace MicroserviceRgpd.Core.Screenings;

/// <summary>
/// Ce que le rapport dit de <b>ce qu'il n'a pas regardé</b>. Quatre parties, et elle est une
/// <b>propriété de toute réponse</b> rendant un <see cref="Screening"/> ou une
/// <see cref="ScreenedColumn"/> — voir <see cref="ScreeningAnswer{T}"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Structurée, et non en prose, pour une seule raison : qu'elle soit testable.</b> La clause porte
/// deux dimensions de nature différente — les <b>sources</b> et les <b>catégories</b> — et en prose
/// l'une des deux disparaît dans une réécriture sans que rien ne casse. Une structure, elle, se
/// teste : c'est le seul mécanisme qui rende la décision vérifiable au lieu de la laisser à la
/// vigilance d'un relecteur.
/// </para>
/// <para>
/// ⚠️ <b>Mais la structure crée le problème que la prose n'avait pas, et l'inversion est le résultat
/// central.</b> Six classes de sources dans un tableau ne se lisent plus comme une illustration :
/// elles se lisent comme un <b>référentiel</b>, et un <c>Operator</c> qui a coché les six conclut
/// qu'il a fait le tour — on aurait reconstruit une promesse d'exhaustivité à l'endroit exact conçu
/// pour la refuser, et cette fois dans une forme testable, donc durable. D'où les <b>deux régimes de
/// clôture</b> : l'ensemble de ce qu'on n'a pas regardé est <b>infini</b> et ne s'énumère pas ;
/// l'ensemble de ce qui <b>a</b> été lu compte exactement un élément. La liste fermée change donc de
/// côté — <see cref="Perimeter"/> est fermé et vrai, <see cref="Beyond"/> est déclaré ouvert et ses
/// items ne sont que des exemples.
/// </para>
/// <para>
/// <b>Le texte est constant ; seuls les comptes sont calculés</b>, et ils sont attachés au périmètre
/// lu. Ce qui doit rester non énumérable est la liste des sources non regardées, pour ne pas se lire
/// comme un référentiel ; cela n'a jamais porté sur ce que le service peut compter de ce qu'il a bel
/// et bien lu.
/// </para>
/// <para>
/// ⚠️ <b>Aucun cas spécial au seuil zéro, et c'est un refus argumenté.</b> Un énoncé particulier
/// quand rien n'est signalé dirait implicitement que le rapport <b>non</b> vide, lui, va bien : la
/// réassurance serait rétablie d'un cran plus haut, là où elle est plus difficile à voir. <b>La
/// clause a la même force à zéro signalement qu'à neuf cents.</b> Ce qui tient ce cas est ailleurs :
/// un rapport n'est jamais littéralement vide — toutes les colonnes y sont, <c>Unflagged</c>
/// comprises et arbitrables — et <c>Unflagged</c> dit ce que le service n'a pas fait, jamais ce que
/// la colonne est.
/// </para>
/// <para>
/// ⚠️ <b>Elle ne prend pas le conteneur libre.</b> Une colonne <c>json</c>/<c>jsonb</c> devient une
/// <see cref="ScreenedColumn"/> <see cref="PersonalDataCategory.PersonalDataUncategorised"/> portant
/// le motif « conteneur libre : le contenu n'est pas lisible depuis le schéma ». Le partage est celui
/// de l'arbitrage : <b>une ligne se retient, une clause ne s'arbitre pas</b> — et une incomplétude
/// reléguée ici serait la seule que l'<c>Omission relue</c> ne relirait pas, faute de bouton.
/// </para>
/// <para>
/// <b>Gouvernance, et l'asymétrie est le fond.</b> <b>Ajouter</b> un exemple hors périmètre est une
/// <b>PR</b> : ça élargit la reconnaissance, c'est gratuit et ça ne trompe personne. <b>Retirer</b>
/// un exemple, ou toucher au périmètre lu, aux catégories hors de portée ou à la relation au
/// <c>Manifest</c>, est un <b>ADR</b> : ça rétrécit une reconnaissance déjà tenue à des
/// <c>Operator</c>, ce qui est exactement l'érosion que l'<c>Omission silencieuse</c> redoute.
/// </para>
/// </remarks>
public sealed class IncompletenessClause
{
  private IncompletenessClause(ReadPerimeter perimeter)
  {
    Perimeter = perimeter;
  }

  /// <summary>
  /// <b>Partie 1 — le périmètre lu, fermé.</b> Ce qui a été regardé compte exactement un élément :
  /// ce relevé. C'est la seule liste de la clause qu'on ait le droit de fermer, et c'est aussi celle
  /// qui porte les comptes.
  /// </summary>
  public ReadPerimeter Perimeter { get; }

  /// <summary>
  /// <b>Partie 2 — hors périmètre, ouvert.</b> Ses items sont des exemples et le disent ; l'ensemble
  /// qu'ils illustrent n'est pas énumérable.
  /// </summary>
  public BeyondPerimeter Beyond { get; } = BeyondPerimeter.Constant;

  /// <summary>
  /// <b>Partie 3 — les catégories hors de portée</b>, énumérées <b>nommément</b> et non énoncées en
  /// principe. Le générique — « certaines catégories ne sont pas atteignables » — est la phrase qu'on
  /// survole, et elle perd ce qui coûte.
  /// <para>
  /// ⚠️ <b>C'est le seul endroit du produit où se dit la seconde moitié de ce que la taxonomie
  /// affirme.</b> Les trois valeurs sont gardées au titre de ce qu'elles <i>sont</i> — la vérité
  /// étant « cette catégorie existe et je ne sais pas la voir » ; sans cette partie, la taxonomie
  /// affiche treize valeurs dont trois hors d'atteinte <b>sans le dire</b>, ce qui est la confusion
  /// qu'<c>Unflagged</c> a été nommée pour éviter, un étage plus haut.
  /// </para>
  /// <para>
  /// ⚠️ <b>Elle dit une limite de méthode, jamais un résultat de corpus.</b> Que des applications
  /// libres ne portent aucune colonne d'art. 10 n'autorise pas à écrire qu'une base client n'en porte
  /// pas.
  /// </para>
  /// </summary>
  public CategoriesBeyondReach BeyondReach { get; } = CategoriesBeyondReach.Constant;

  /// <summary>
  /// <b>Partie 4 — la relation au <c>Manifest</c>.</b> Elle est écrite ici et pas seulement au
  /// glossaire : la borne <c>Suggéré, jamais déclaré</c> lie le <b>code</b> et n'empêche que le pont
  /// technique. Rien en elle n'empêche un <c>Operator</c> pressé de lire le rapport comme son
  /// paysage, et c'est précisément le grief — une décision tenue dans le code et perdue dans l'usage.
  /// La renvoyer à l'écran l'aurait de plus laissée hors de toute réponse qui n'est pas l'écran.
  /// </summary>
  public string RelationToManifest { get; } =
    "Ce dépistage n'est pas le recensement de votre paysage de données. Le recensement est le "
    + "Manifest, et le Manifest se déclare à la main, système par système.";

  /// <summary>
  /// La clause d'un rapport : le texte constant, et les comptes de <b>ce</b> relevé.
  /// </summary>
  /// <remarks>
  /// <b>C'est le seul chemin de construction</b>, et il exige un rapport. Une clause détachée de tout
  /// relevé serait une clause dont les comptes ne veulent rien dire, et il n'y a aucun usage pour
  /// elle : la clause accompagne une réponse, elle n'existe pas seule.
  /// </remarks>
  /// <exception cref="ArgumentNullException"><paramref name="screening"/> est absent.</exception>
  public static IncompletenessClause For(Screening screening)
  {
    ArgumentNullException.ThrowIfNull(screening);

    return For(ScreeningCounts.Of(screening));
  }

  /// <summary>
  /// La clause d'un rapport <b>qu'on n'a pas chargé</b> : le même texte constant, et les mêmes
  /// comptes, obtenus de la base plutôt que de l'agrégat.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Elle existe pour l'écran d'une table, et elle n'affaiblit pas la règle.</b> La clause
  /// reste inconstruisible sans les comptes de <b>ce</b> relevé ; ce qui change est seulement qui les
  /// a calculés. Sans ce chemin, rendre une <see cref="ScreenedColumn"/> sans sa clause serait
  /// devenu le chemin économe, et la clause serait tombée là où elle est le plus nécessaire.
  /// </remarks>
  /// <exception cref="ArgumentNullException"><paramref name="counts"/> est absent.</exception>
  public static IncompletenessClause For(ScreeningCounts counts)
  {
    ArgumentNullException.ThrowIfNull(counts);

    return new IncompletenessClause(ReadPerimeter.Of(counts));
  }
}

/// <summary>
/// Ce que le dépistage <b>a</b> lu — la seule liste de la clause qui soit fermée, parce que c'est la
/// seule qui puisse l'être sans mentir.
/// </summary>
/// <param name="ReadAsSignal">Ce qui a été lu <b>comme signal</b>, c'est-à-dire comme indice de sens.</param>
/// <param name="ReadOnlyToFilter">
/// Ce qui n'a été lu <b>que pour écarter</b>. La séparation d'avec le signal n'est pas une nuance :
/// écrire « j'ai lu les types » ferait croire qu'un <c>varchar(10)</c> et un <c>date</c> sont deux
/// indices de qualité différente, alors qu'ils ne sont un indice ni l'un ni l'autre. C'est aussi ce
/// qui explique à l'<c>Operator</c> pourquoi une donnée cachée dans les <b>valeurs</b> d'une colonne
/// au nom neutre ne pouvait pas être vue.
/// <para>
/// ⚠️ <b>Elle décrit ce que le dépistage a lu du <c>ColumnListing</c>, et non ce que la ligne
/// retient.</b> Le relevé porte neuf champs par colonne, dont la table qu'une clé étrangère
/// référence ; une <see cref="ListedColumn"/>, elle, n'en garde que ceux dont l'arbitrage ou le
/// filtre a besoin — le schéma et la table, par exemple, vivent sur son <see cref="ColumnIdentity"/>
/// et non à côté. Lire cette liste comme l'inventaire des colonnes de la table de persistance ferait
/// conclure à tort que la clause promet un champ qui n'existe pas.
/// </para>
/// </param>
/// <param name="ColumnsRead">Combien de colonnes ce relevé porte.</param>
/// <param name="TablesRead">Combien de tables il couvre.</param>
/// <param name="ColumnsWithoutAComment">
/// Combien de ces colonnes n'ont aucun commentaire, ni au niveau de la colonne ni à celui de la
/// table. Le compte est là parce que l'absence est <b>massive</b> et souvent structurelle : plusieurs
/// SGBD n'en rendent aucun, et sur bien des relevés réels ce compte égale
/// <paramref name="ColumnsRead"/>.
/// </param>
public sealed record ReadPerimeter(
  IReadOnlyList<string> ReadAsSignal,
  IReadOnlyList<string> ReadOnlyToFilter,
  int ColumnsRead,
  int TablesRead,
  int ColumnsWithoutAComment)
{
  /// <summary>Ce qui ouvre la partie, en une phrase.</summary>
  public string Statement { get; } =
    "Ce dépistage n'a lu qu'un relevé de colonnes, celui que vous avez collé, et rien d'autre.";

  /// <summary>Ce qui accompagne la seconde liste, et qui dit pourquoi elle n'est pas la première.</summary>
  public string FilterStatement { get; } =
    "Lus seulement pour écarter, jamais comme indice de sens : la forme d'une colonne ne dit pas ce "
    + "qu'elle porte, et le service n'a jamais vu une seule valeur.";

  /// <summary>
  /// La condition que portent les commentaires, et elle n'est pas une précaution de style : plusieurs
  /// SGBD n'en rendent jamais, et sans elle la clause serait fausse sur la majorité des relevés
  /// réels. Le dialecte déclaré porte bien l'information, mais il vit sur le relevé et un
  /// <c>Operator</c> ne les rapproche pas.
  /// </summary>
  public const string CommentCondition = "lorsque le SGBD en rend et lorsqu'ils existent";

  /// <summary>Cette liste est-elle fermée ? <b>Oui</b>, et c'est la seule de la clause à l'être.</summary>
  public bool IsClosed => true;

  /// <summary>Le périmètre lu de ce rapport : le texte constant, et ses trois comptes.</summary>
  internal static ReadPerimeter Of(ScreeningCounts counts)
  {
    return new ReadPerimeter(
      [
        "les noms de tables",
        "les noms de colonnes",
        $"les commentaires de table, {CommentCondition}",
        $"les commentaires de colonne, {CommentCondition}",
      ],
      [
        "le type de la colonne",
        "sa nullabilité",
        "la table qu'elle référence",
      ],
      counts.Columns,
      counts.Tables,
      counts.ColumnsWithoutAComment);
  }
}

/// <summary>
/// Ce que le dépistage n'a pas regardé — <b>déclaré ouvert</b>, ses items n'étant que des exemples.
/// </summary>
/// <remarks>
/// ⚠️ <b><see cref="IsClosed"/> vaut faux, et ce n'est pas une décoration.</b> C'est ce qui empêche
/// un lecteur — humain ou test — de traiter <see cref="Examples"/> comme un référentiel qu'on
/// pourrait cocher jusqu'au bout.
/// </remarks>
public sealed record BeyondPerimeter(IReadOnlyList<string> Examples)
{
  /// <summary>Le texte constant de la partie 2.</summary>
  internal static BeyondPerimeter Constant { get; } = new(
  [
    "les autres bases du client",
    "les applications en SaaS, dont le CRM",
    "les tableurs partagés",
    "les journaux applicatifs",
    "les exports du service commercial",
    "les pièces jointes de messagerie",
    "les sauvegardes",
    "les fichiers plats",
  ]);

  /// <summary>Ce qui ouvre la partie, et qui déclare l'ouverture avant que les exemples n'arrivent.</summary>
  public string Statement { get; } =
    "Ce dépistage n'a regardé aucune autre source. En voici quelques-unes, à titre d'exemples :";

  /// <summary>
  /// Ce qui ferme la partie <b>sans la fermer</b>. Elle est la phrase qui empêche la liste de se lire
  /// comme un référentiel, et elle vaut autant que les items eux-mêmes.
  /// </summary>
  public string OpennessStatement { get; } =
    "Cette énumération est ouverte : ce sont des exemples, jamais un inventaire. Ce qui n'a pas été "
    + "regardé ne s'énumère pas, et toute liste qui prétendrait le faire mentirait.";

  /// <summary>Cette liste est-elle fermée ? <b>Non</b>, et elle ne peut pas l'être.</summary>
  public bool IsClosed => false;
}

/// <summary>
/// Les catégories que ce régime — lire un schéma, jamais une valeur — ne peut <b>structurellement</b>
/// pas atteindre. Ce n'est pas « le moteur est perfectible », c'est « aucun moteur lisant le seul
/// schéma ne verra jamais ceci », et ça porte précisément sur les catégories dont l'omission coûte le
/// plus cher.
/// </summary>
public sealed record CategoriesBeyondReach(IReadOnlyList<CategoryBeyondReach> Categories)
{
  /// <summary>Le texte constant de la partie 3.</summary>
  internal static CategoriesBeyondReach Constant { get; } = new(
  [
    new CategoryBeyondReach(
      PersonalDataCategory.HealthData,
      "Une donnée de santé peut n'exister que dans les valeurs d'une colonne au nom parfaitement "
      + "neutre. Le nom ne dit rien, le type ne dit rien : un dépistage de schéma ne la verra pas."),
    new CategoryBeyondReach(
      PersonalDataCategory.SpecialCategoryData,
      "La biométrie ne relève de l'art. 9 qu'« aux fins d'identifier une personne de manière "
      + "unique » : c'est une finalité, et aucun lecteur de schéma ne connaît la finalité d'un "
      + "traitement."),
    new CategoryBeyondReach(
      PersonalDataCategory.CriminalOffenceData,
      "L'art. 10 réserve ces traitements aux autorités publiques, et rien dans un nom de colonne ne "
      + "l'annonce de façon fiable."),
  ]);

  /// <summary>Ce qui ouvre la partie, et qui dit sur quel registre elle parle.</summary>
  public string Statement { get; } =
    "Trois catégories de la taxonomie restent hors de portée de ce dépistage. Elles y figurent au "
    + "titre de ce qu'elles sont : la limite est celle de la méthode, jamais celle de votre base.";

  /// <summary>Cette liste est-elle fermée ? <b>Oui</b> : elle énumère des valeurs d'une taxonomie qui l'est.</summary>
  public bool IsClosed => true;
}

/// <summary>
/// Une catégorie hors de portée, et <b>pourquoi</b> elle l'est. Le motif est attaché à la valeur
/// parce que les trois ne le sont pas pour la même raison — l'une cache la donnée, l'autre cache le
/// régime, la troisième cache le responsable.
/// </summary>
/// <param name="Category">La valeur de la taxonomie concernée.</param>
/// <param name="Reason">Ce qui la met hors de portée, en une phrase lue par un humain.</param>
public sealed record CategoryBeyondReach(PersonalDataCategory Category, string Reason);
