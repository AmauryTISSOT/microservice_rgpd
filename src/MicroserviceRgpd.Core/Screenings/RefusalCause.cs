namespace MicroserviceRgpd.Core.Screenings;

/// <summary>
/// Pourquoi un collage a été refusé <b>en bloc</b>. Neuf cas, nommés un par un, et c'est le nombre
/// qui compte autant que la liste.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Un cas nommé, jamais un « format invalide ».</b> Un refus générique pousse l'<c>Operator</c>
/// à recommencer au hasard — il a fait un long trajet pour produire ce relevé, et rien dans « format
/// invalide » ne lui dit s'il doit rejouer sa requête, changer de schéma ou remonter son plafond.
/// Chaque cas porte donc <see cref="Expectation"/>, ce que le format attendait, et le refus qui le
/// cite porte en plus la ligne et ce qui s'y trouvait.
/// </para>
/// <para>
/// <b>Le type est un vocabulaire, pas un message.</b> Le rendu à l'écran n'est pas ici : ce qui vit
/// ici est ce qui rend le cas <b>distinguable</b> — d'un test, d'une surface, d'un journal. Une
/// phrase française rendue à l'<c>Operator</c> se compose à partir de ces trois pièces, jamais à la
/// place d'elles.
/// </para>
/// <para>
/// ⚠️ <b>Aucun de ces cas n'autorise une ingestion partielle</b>, et <see cref="EmptyListing"/> est
/// celui qu'on retire en premier quand on l'oublie : un pivot vide et parfaitement formé est ce que
/// rend une requête lancée contre le mauvais schéma, et l'accepter produirait un <c>Screening</c>
/// vide et rassurant.
/// </para>
/// </remarks>
public sealed class RefusalCause : SmartEnum<RefusalCause>
{
  /// <summary>Cas n° 1 — la ligne d'en-tête est absente ou illisible : rien ne déclare le format, le dialecte ni la base.</summary>
  public static readonly RefusalCause MissingHeader = new(
    nameof(MissingHeader),
    1,
    "en-tête absent ou illisible",
    "la première ligne du collage est l'en-tête, et elle déclare le format, le dialecte, la base et l'instant de génération");

  /// <summary>
  /// Cas n° 2 — la ligne de fin est absente. <b>Le cas nommé du ticket</b> : la troncature au
  /// presse-papier, celle que la ligne de fin existe pour rendre détectable.
  /// </summary>
  public static readonly RefusalCause MissingClosingLine = new(
    nameof(MissingClosingLine),
    2,
    "ligne de fin absente ou illisible",
    "la dernière ligne du collage est la ligne de fin, et elle porte le nombre de colonnes que la requête a produites");

  /// <summary>Cas n° 3 — le compte annoncé et les lignes reçues divergent, dans un sens ou dans l'autre.</summary>
  public static readonly RefusalCause CountMismatch = new(
    nameof(CountMismatch),
    3,
    "compte annoncé différent des lignes reçues",
    "la ligne de fin annonce autant de colonnes que le collage en porte");

  /// <summary>Cas n° 4 — une ligne de colonne ne s'analyse pas, ou il lui manque une des neuf clés.</summary>
  public static readonly RefusalCause UnreadableColumnLine = new(
    nameof(UnreadableColumnLine),
    4,
    "ligne de colonne illisible",
    "chaque ligne de colonne porte les neuf clés du pivot, valeurs vides comprises");

  /// <summary>
  /// Cas n° 5 — un trou dans les rangs d'une table : la troncature <b>au milieu</b>, invisible à un
  /// compte global quand deux morceaux collés se recouvrent mal. Les trois dialectes rendent des
  /// rangs contigus ; un trou n'est jamais légitime.
  /// </summary>
  public static readonly RefusalCause RankGap = new(
    nameof(RankGap),
    5,
    "trou dans les rangs d'une table",
    "les rangs des colonnes d'une même table se suivent sans trou ni répétition");

  /// <summary>Cas n° 6 — deux lignes portent le même triplet, signature de deux pivots collés bout à bout.</summary>
  public static readonly RefusalCause DuplicateColumn = new(
    nameof(DuplicateColumn),
    6,
    "colonne en double",
    "le triplet schéma, table, colonne ne désigne qu'une ligne du collage");

  /// <summary>
  /// Cas n° 7 — zéro colonne, en-tête et ligne de fin bien présents. Le moins intuitif, et le plus
  /// important après la troncature.
  /// </summary>
  public static readonly RefusalCause EmptyListing = new(
    nameof(EmptyListing),
    7,
    "relevé vide",
    "un relevé porte au moins une colonne : un relevé vide est ce que rend une requête lancée contre le mauvais schéma");

  /// <summary>Cas n° 8 — la version de format déclarée n'est pas celle que ce service sait lire.</summary>
  public static readonly RefusalCause UnknownFormatVersion = new(
    nameof(UnknownFormatVersion),
    8,
    "version de format inconnue",
    $"l'en-tête déclare le format « {ColumnListing.FormatVersion} »");

  /// <summary>
  /// Cas n° 9 — le plafond est franchi. ⚠️ <b>On refuse ; on ne tronque jamais</b> : tronquer en
  /// silence à N colonnes est l'<c>Omission silencieuse</c> sous sa forme la plus pure.
  /// </summary>
  public static readonly RefusalCause CeilingExceeded = new(
    nameof(CeilingExceeded),
    9,
    "plafond franchi",
    $"un relevé porte au plus {ColumnListing.MaxColumns} colonnes, et au-delà il est refusé plutôt que tronqué");

  private RefusalCause(string name, int value, string frenchLabel, string expectation)
    : base(name, value)
  {
    FrenchLabel = frenchLabel;
    Expectation = expectation;
  }

  /// <summary>Le libellé destiné à l'<c>Operator</c>. Le français reste hors des identifiants.</summary>
  public string FrenchLabel { get; }

  /// <summary>
  /// Ce que le format attendait, en une phrase. Elle est attachée au cas et non composée à l'écran :
  /// un refus qui ne dit pas ce qui était attendu fait recommencer au hasard.
  /// </summary>
  public string Expectation { get; }

  /// <summary>Le numéro du cas dans l'énumération de la résolution du format. C'est <see cref="SmartEnum{TEnum,TValue}.Value"/> lui-même.</summary>
  public int CaseNumber => Value;
}

/// <summary>
/// Un refus en bloc : le cas, la ligne du collage qui l'a déclenché, et ce qui s'y trouvait.
/// </summary>
/// <remarks>
/// <para>
/// <b>Il n'existe pas de refus partiel.</b> Un collage refusé ne rend <b>aucune</b> colonne — pas
/// même les lignes lisibles — parce qu'un <c>Screening</c> bâti sur 99,9 % d'un relevé se lirait
/// comme complet, et que l'<c>Omission relue</c> repose entièrement sur le fait que le rapport rend
/// <b>toutes</b> les colonnes du relevé.
/// </para>
/// <para>
/// ⚠️ <b><see cref="Observed"/> cite le collage, il ne le juge pas.</b> C'est ce qui a été lu à cet
/// endroit, en clair, pour que l'<c>Operator</c> reconnaisse sa propre ligne. Le service ne suppose
/// jamais ce qu'elle aurait dû être.
/// </para>
/// </remarks>
/// <param name="Cause">Lequel des neuf cas s'applique.</param>
/// <param name="LineNumber">
/// La ligne du collage, comptée à partir de 1 et sur le collage tel qu'il a été reçu. Elle est
/// toujours renseignée : même un collage vide a une première ligne, qui est celle où l'en-tête
/// manque.
/// </param>
/// <param name="Observed">Ce qui a été lu là où le format attendait autre chose.</param>
public sealed record ColumnListingRefusal(RefusalCause Cause, int LineNumber, string Observed);
