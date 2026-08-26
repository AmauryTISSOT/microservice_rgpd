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
  /// <b>Partie 1 — le périmètre lu.</b> C'est la <b>seule des quatre parties qui varie</b> avec
  /// l'origine du relevé (ADR-0013), et c'est aussi celle qui porte les comptes.
  /// </summary>
  public ReadPerimeter Perimeter { get; }

  /// <summary>
  /// D'où venait le relevé que ce rapport a lu. <b>C'est ce qui fait varier la partie 1</b>, et rien
  /// d'autre : les trois autres parties sont les mêmes des deux côtés.
  /// </summary>
  /// <remarks>
  /// Elle est rendue ici plutôt que seulement au fond de <see cref="Perimeter"/> parce que ce que le
  /// service a lu se dit <b>aussi hors de la clause</b> — le rappel sous le compteur
  /// <c>Signalées</c>, en haut des deux écrans de rapport, est lu plus souvent qu'elle et doit varier
  /// avec elle.
  /// </remarks>
  public ListingOrigin Origin => Perimeter.Origin;

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
  /// glossaire : la borne <c>Aucune modification vers le Manifest</c> lie le <b>code</b> et n'empêche que le pont
  /// technique. Rien en elle n'empêche un <c>Operator</c> pressé de lire le rapport comme son
  /// paysage, et c'est précisément le grief — une décision tenue dans le code et perdue dans l'usage.
  /// La renvoyer à l'écran l'aurait de plus laissée hors de toute réponse qui n'est pas l'écran.
  /// <para>
  /// ⚠️ <b>Le texte est gelé par ADR-0006, et c'est une réécriture, pas un renommage.</b> L'ancienne
  /// phrase portait <b>les deux mots retirés à la fois</b>, et son renommage mécanique n'aurait rien
  /// voulu dire. Trois choses s'y tiennent et ne se défont pas en passant :
  /// </para>
  /// <para>
  /// ⚠️ <b>Le nom défini part, le verbe reste</b> — « ne recense pas… c'est vous qui les recensez ».
  /// Le défini d'identité (« le recensement <b>est</b> le <c>Manifest</c> ») conférait l'autorité même
  /// que l'<c>Omission silencieuse</c> redoute ; la forme verbale la retire sans supprimer le mot, et
  /// le témoin d'écran l'autorise mécaniquement — il ne bannit « recensement » que dans ce qui
  /// <b>nomme</b>, jamais dans la prose d'un paragraphe.
  /// </para>
  /// <para>
  /// <b>« à la main » est gardé mot pour mot</b>, délibérément : c'est ce qui dit que la liste ne se
  /// remplit pas toute seule. Et <b>le mot retenu pour la chose est « liste »</b>, par précédent — la
  /// lettre remise à la personne concernée (<c>DeliveryLetter</c>) écrit déjà « Cette liste… ne
  /// garantit pas qu'il n'en existe pas d'autres ». Le service tient le même langage des deux côtés.
  /// </para>
  /// <para>
  /// ⚠️ <b>L'identifiant <c>Manifest</c> en clair est retiré du texte</b> — non parce qu'un client le
  /// lirait, mais parce qu'ADR-0006 le fait désigner un écran qui ne porte plus ce nom : un
  /// <c>Operator</c> doit chercher « Configuration » dans la barre.
  /// </para>
  /// <para>
  /// ⚠️ <b>LE NOM CITÉ EST CELUI DE LA BARRE, PAS CELUI DE LA CARTE</b>, et l'écran en porte
  /// désormais deux — « Configuration » dans la barre, « Configuration du microservice RGPD » sur
  /// la carte de l'accueil et en titre (ADR-0008). Cette phrase décrit un <b>geste de
  /// navigation</b> : elle envoie l'<c>Operator</c> cliquer dans la barre, et le nom qu'il doit y
  /// reconnaître est celui qui y est écrit.
  /// </para>
  /// <para>
  /// ⚠️ <b>Ce texte est du domaine, mais il ne quitte pas la surface — et c'est ce qui borne le
  /// coût de la forme courte.</b> Il n'est rendu que par <c>_IncompletenessClause.cshtml</c>, inclus
  /// par les quatre écrans de détection, <b>qui portent tous la barre</b> : le mot cité est écrit à
  /// l'écran au moment où la phrase se lit. Il ne part ni dans une <c>DeliveryLetter</c> ni dans une
  /// charge d'API. Le jour où il partirait, la forme <b>pleine</b> devrait y revenir — c'est
  /// précisément le cas que la règle « aucune forme courte » de l'ADR-0006 visait.
  /// </para>
  /// </summary>
  public string RelationToManifest { get; } =
    "Ce rapport de détection ne recense pas vos systèmes : c'est vous qui les recensez, à la main, "
    + "système par système, dans « Configuration ». La liste que vous y tenez "
    + "ne garantit pas qu'il n'en existe pas d'autres.";

  /// <summary>
  /// La clause d'un rapport : le texte constant, et les comptes de <b>ce</b> relevé.
  /// </summary>
  /// <remarks>
  /// <b>C'est le seul chemin de construction</b>, et il exige un rapport. Une clause détachée de tout
  /// relevé serait une clause dont les comptes ne veulent rien dire, et il n'y a aucun usage pour
  /// elle : la clause accompagne une réponse, elle n'existe pas seule.
  /// </remarks>
  /// <remarks>
  /// ⚠️ <b>L'origine se lit sur l'agrégat, et <c>Screening.Origin</c> lève quand elle est
  /// absente.</b> C'est voulu : un rapport dont on ne sait pas s'il a été collé ou scanné rendrait
  /// ici l'une des deux clauses au hasard, et l'une des deux ment.
  /// </remarks>
  /// <exception cref="ArgumentNullException"><paramref name="screening"/> est absent.</exception>
  /// <exception cref="InvalidOperationException"><paramref name="screening"/> ne porte pas d'origine.</exception>
  public static IncompletenessClause For(Screening screening)
  {
    ArgumentNullException.ThrowIfNull(screening);

    return For(ScreeningCounts.Of(screening), screening.Origin);
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
  /// <param name="counts">Les comptes de ce relevé, obtenus de la base.</param>
  /// <param name="origin">
  /// D'où venait ce relevé. ⚠️ <b>Elle est un paramètre, et ne voyage pas dans
  /// <paramref name="counts"/></b> : cet objet s'appelle <em>les comptes du rapport</em>, y ranger
  /// une non-quantité lui ferait perdre ce que son nom dit — et la clause reçoit <b>deux</b> choses,
  /// ce qui reste lisible dans cette signature.
  /// </param>
  /// <exception cref="ArgumentNullException"><paramref name="counts"/> ou <paramref name="origin"/> est absent.</exception>
  /// <exception cref="ArgumentException"><paramref name="origin"/> est le cas nul : personne n'a dit d'où venait ce relevé.</exception>
  public static IncompletenessClause For(ScreeningCounts counts, ListingOrigin origin)
  {
    ArgumentNullException.ThrowIfNull(counts);

    return new IncompletenessClause(
      ReadPerimeter.Of(counts, ListingOrigin.KnownOrThrow(origin, nameof(origin))));
  }
}

/// <summary>
/// Ce que la détection <b>a</b> lu — la seule liste de la clause qui soit fermée, et la <b>seule des
/// quatre parties qui varie</b> avec l'origine du relevé.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Sur le chemin collé, pas un caractère ne bouge</b>, et c'est une décision et non un effet
/// de bord : la promesse la plus forte du produit — <i>le service n'a vu aucune valeur</i> — reste
/// <b>intégralement vraie</b> chez l'<c>Operator</c> qui colle, et on ne paie pas la vérité du
/// chemin neuf avec la sienne.
/// </para>
/// <para>
/// ⚠️ <b>La variance est portée par un type, jamais par un booléen</b> (ADR-0013). Un
/// <c>estScanné</c> aurait fait du collage le cas normal et de la connexion l'exception, quand les
/// deux chemins coexistent et qu'aucun ne remplace l'autre.
/// </para>
/// <para>
/// ⚠️ <b><see cref="IsClosed"/> ne dit pas la même chose des deux côtés, et c'est le point le plus
/// coûteux de la partie.</b> Ce qui a été regardé compte toujours exactement un élément — ce relevé
/// —, mais sur le chemin scanné <b>ce relevé est celui que le compte de connexion a présenté</b> :
/// la fermeture cesse d'être une propriété que le service tient lui-même, et rien dans la liste ne
/// pourrait le dire. C'est la seconde phrase de <see cref="Statement"/> qui le reconnaît, et c'est
/// pourquoi elle ne peut pas être coupée.
/// </para>
/// </remarks>
/// <param name="Origin">
/// D'où venait le relevé lu. C'est <b>le</b> fait dont tout le reste de la partie dépend.
/// </param>
/// <param name="ReadAsSignal">
/// Ce qui a été lu <b>comme signal</b>, c'est-à-dire comme indice de sens.
/// <para>
/// ⚠️ <b>Sur le chemin scanné, les valeurs prélevées y entrent, en cinquième puce.</b> Elles ont
/// servi à <b>deviner</b> et non à trier : une colonne <c>ref_3</c> dont le nom ne dit rien est
/// signalée parce que ses valeurs portent une clé d'IBAN. Les ranger dans
/// <paramref name="ReadOnlyToFilter"/> aurait été faux, et leur ouvrir une <b>troisième liste</b>
/// aurait contredit ce qui a été tranché du moteur — une règle de forme est un <c>Trigger</c> de
/// plus versé au même sac.
/// </para>
/// </param>
/// <param name="ReadOnlyToFilter">
/// Ce qui n'a été lu <b>que pour écarter</b>. La séparation d'avec le signal n'est pas une nuance :
/// écrire « j'ai lu les types » ferait croire qu'un <c>varchar(10)</c> et un <c>date</c> sont deux
/// indices de qualité différente, alors qu'ils ne sont un indice ni l'un ni l'autre. C'est aussi ce
/// qui explique à l'<c>Operator</c> pourquoi une donnée cachée dans les <b>valeurs</b> d'une colonne
/// au nom neutre ne pouvait pas être vue.
/// <para>
/// ⚠️ <b>Elle décrit ce que la détection a lu du <c>ColumnListing</c>, et non ce que la ligne
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
/// <param name="ColumnsWithoutAPreview">
/// Combien de colonnes n'ont aucun aperçu, <b>par famille</b> et <b>zéros compris</b>.
/// <para>
/// ⚠️ <b>Sur le chemin collé, elle vaut <c>null</c> — absente, jamais <see cref="PreviewAbsenceCounts.None"/>.</b>
/// Rendre « 0 par droits refusés » sur un relevé collé affirmerait qu'un prélèvement a eu lieu et
/// n'a rien refusé : une incomplétude inventée là où il n'y en a pas.
/// </para>
/// </param>
public sealed record ReadPerimeter(
  ListingOrigin Origin,
  IReadOnlyList<string> ReadAsSignal,
  IReadOnlyList<string> ReadOnlyToFilter,
  int ColumnsRead,
  int TablesRead,
  int ColumnsWithoutAComment,
  PreviewAbsenceCounts? ColumnsWithoutAPreview)
{
  /// <summary>D'où venait le relevé lu, et ce n'est jamais le cas nul.</summary>
  /// <remarks>
  /// ⚠️ <b>Le refus est ici et pas seulement dans <see cref="IncompletenessClause.For(ScreeningCounts, ListingOrigin)"/>.</b>
  /// Un record positionnel a un constructeur public : sans ce garde, <c>new ReadPerimeter(Unspecified, …)</c>
  /// rendrait en silence les phrases du <b>chemin collé</b> — « le service n'a jamais vu une seule
  /// valeur » — sur un relevé dont personne ne sait ce qu'il a lu, ce qui est très exactement la
  /// panne que le cas nul existe pour empêcher.
  /// </remarks>
  public ListingOrigin Origin { get; } = ListingOrigin.KnownOrThrow(Origin, nameof(Origin));

  /// <summary>
  /// Ce qui ouvre la partie, en une phrase sur le chemin collé et en <b>deux</b> sur le chemin
  /// scanné.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>La seconde phrase du chemin scanné ne peut pas être coupée : ici la queue est la
  /// preuve.</b> Sans elle, la première affirme qu'un relevé a été lu et laisse croire qu'il est
  /// celui de la base ; ce que le service tient est seulement ce qu'un compte de connexion lui a
  /// présenté, et lui seul sait ce qu'il a tu.
  /// </remarks>
  public string Statement =>
    Origin == ListingOrigin.Scanned
      ? "Ce rapport de détection n'a lu qu'un relevé de colonnes, celui que le compte de connexion a "
        + "présenté au service, et rien d'autre. Le service ne peut pas savoir si ce compte lui a "
        + "présenté toute la base."
      : "Ce rapport de détection n'a lu qu'un relevé de colonnes, celui que vous avez collé, et rien "
        + "d'autre.";

  /// <summary>Ce qui accompagne la seconde liste, et qui dit pourquoi elle n'est pas la première.</summary>
  /// <remarks>
  /// ⚠️ <b>Sur le chemin scanné, la queue tombe et rien ne la remplace</b> — traitement inverse de
  /// celui du rappel sous le compteur <c>Signalées</c>, et c'est délibéré : ici le début de phrase
  /// porte sa propre justification et la queue n'était qu'un ajout devenu faux.
  /// </remarks>
  public string FilterStatement =>
    Origin == ListingOrigin.Scanned
      ? "Lus seulement pour écarter, jamais comme indice de sens : la forme d'une colonne ne dit pas "
        + "ce qu'elle porte."
      : "Lus seulement pour écarter, jamais comme indice de sens : la forme d'une colonne ne dit pas "
        + "ce qu'elle porte, et le service n'a jamais vu une seule valeur.";

  /// <summary>
  /// La mise en garde du <b>premier venu</b> : ce que ces quelques valeurs ne prouvent pas.
  /// <b>Absente — pas vide — sur le chemin collé</b>, où il n'y a aucune valeur à qualifier.
  /// <para>
  /// ⚠️ <b>Absente aussi quand <see cref="NoPreviewSucceeded"/>.</b> « Ces cinq valeurs sont les
  /// cinq premières » ne se dit pas d'un relevé où aucune n'a été prélevée : la phrase affirmerait
  /// une lecture qui n'a pas eu lieu, dans la partie qui existe pour ne rien surestimer.
  /// </para>
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Sans elle, la cinquième puce devient dangereuse.</b> Une table de deux millions de
  /// courriels réels peut n'en montrer que cinq en <c>example.com</c> — jeux d'essai, comptes de
  /// démonstration, données d'amorçage —, et l'<c>Operator</c> qui les lit comme représentatives
  /// <b>écarte une colonne qu'il fallait retenir</b> : une omission causée par l'écran lui-même.
  /// </para>
  /// <para>
  /// <b>Elle est une propriété distincte plutôt qu'une phrase fondue dans la puce</b>, pour la raison
  /// qui structure toute la clause : qu'elle se teste. Et elle vit dans la partie 1 parce qu'elle est
  /// une limite de la <b>lecture</b>, valant pour tout le rapport — au contraire de la coupure à
  /// <see cref="ColumnPreview.MaxValueLength"/> caractères, qui porte sur <b>une</b> valeur précise
  /// et s'écrit à son contact.
  /// </para>
  /// </remarks>
  public string? FirstComeBias =>
    Origin == ListingOrigin.Scanned && !NoPreviewSucceeded
      ? $"Ces {ColumnPreview.MaxValuesInWords} valeurs sont les {ColumnPreview.MaxValuesInWords} "
        + "premières que la base a rendues, dans l'ordre où elle les a rendues : elles ne "
        + "représentent pas la colonne."
      : null;

  /// <summary>
  /// La condition que portent les commentaires, et elle n'est pas une précaution de style : plusieurs
  /// SGBD n'en rendent jamais, et sans elle la clause serait fausse sur la majorité des relevés
  /// réels. Le dialecte déclaré porte bien l'information, mais il vit sur le relevé et un
  /// <c>Operator</c> ne les rapproche pas.
  /// </summary>
  public const string CommentCondition = "lorsque le SGBD en rend et lorsqu'ils existent";

  /// <summary>
  /// Ce qui se dit <b>à l'échelle du rapport</b> quand aucun prélèvement n'a abouti nulle part.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>En plus de la raison portée par chaque ligne, jamais à sa place.</b> La raison par ligne
  /// dit pourquoi <i>cette</i> colonne n'a pas d'aperçu ; elle ne dit pas qu'aucune n'en a. Un
  /// <c>Operator</c> qui déroule un rapport de cinq mille lignes ne recompose pas ce fait, et c'est
  /// justement celui qui devrait le faire rescanner.
  /// </remarks>
  public const string NoPreviewSucceededStatement =
    "Aucune valeur n'a pu être prélevée sur ce relevé : pas une seule des colonnes lues n'a d'aperçu. "
    + "La détection s'y est faite sur les seuls noms, types et commentaires.";

  /// <summary>
  /// Aucun prélèvement n'a abouti sur ce relevé — toutes les colonnes lues portent une raison
  /// d'absence.
  /// </summary>
  /// <remarks>
  /// <b>Faux sur le chemin collé</b>, et pas par accident : rien n'y a été tenté, donc rien n'y a
  /// échoué. C'est la même décision que celle qui rend <see cref="ColumnsWithoutAPreview"/> absente
  /// plutôt que nulle.
  /// </remarks>
  public bool NoPreviewSucceeded =>
    ColumnsWithoutAPreview is { } absences && ColumnsRead > 0 && absences.Total == ColumnsRead;

  /// <summary>Cette liste est-elle fermée ? <b>Oui</b>, et c'est la seule de la clause à l'être.</summary>
  /// <remarks>
  /// ⚠️ <b>Elle ne dit pas la même chose des deux côtés</b> — voir le troisième avertissement du
  /// type. Sur le chemin scanné, ce que <see cref="Statement"/> reconnaît en seconde phrase est
  /// exactement ce que ce booléen ne peut plus promettre seul.
  /// </remarks>
  public bool IsClosed => true;

  /// <summary>Le périmètre lu de ce rapport : le texte que son origine commande, et ses comptes.</summary>
  internal static ReadPerimeter Of(ScreeningCounts counts, ListingOrigin origin)
  {
    var scanned = origin == ListingOrigin.Scanned;

    List<string> readAsSignal =
    [
      "les noms de tables",
      "les noms de colonnes",
      $"les commentaires de table, {CommentCondition}",
      $"les commentaires de colonne, {CommentCondition}",
    ];

    if (scanned)
    {
      readAsSignal.Add($"{ColumnPreview.MaxValuesInWords} valeurs au plus de chaque colonne");
    }

    return new ReadPerimeter(
      origin,
      readAsSignal,
      [
        "le type de la colonne",
        "sa nullabilité",
        "la table qu'elle référence",
      ],
      counts.Columns,
      counts.Tables,
      counts.ColumnsWithoutAComment,
      scanned ? counts.ColumnsWithoutAPreview : null);
  }
}

/// <summary>
/// Ce que la détection n'a pas regardé — <b>déclaré ouvert</b>, ses items n'étant que des exemples.
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
    "Ce rapport de détection n'a regardé aucune autre source. En voici quelques-unes, à titre "
    + "d'exemples :";

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
      "Aucune forme ne dit « donnée de santé ». Un IBAN porte une clé de contrôle, un courriel une "
      + "arobase ; un diagnostic est de la prose, et ce service reconnaît des formes, jamais des "
      + "entités nommées en contexte."),
    new CategoryBeyondReach(
      PersonalDataCategory.SpecialCategoryData,
      "La biométrie ne relève de l'art. 9 qu'« aux fins d'identifier une personne de manière "
      + "unique » : c'est une finalité, et une finalité ne se déclare nulle part dans une base — ni "
      + "dans un nom, ni dans un type, ni dans une valeur."),
    new CategoryBeyondReach(
      PersonalDataCategory.CriminalOffenceData,
      "L'art. 10 réserve ces traitements aux autorités publiques : c'est une qualité du responsable "
      + "du traitement, et rien dans une base ne l'annonce — la donnée d'une condamnation ressemble "
      + "à n'importe quel texte."),
  ]);

  /// <summary>Ce qui ouvre la partie, et qui dit sur quel registre elle parle.</summary>
  public string Statement { get; } =
    "Trois catégories de la taxonomie restent hors de portée de ce rapport de détection. Elles y "
    + "figurent au "
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
