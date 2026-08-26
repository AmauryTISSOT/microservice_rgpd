namespace MicroserviceRgpd.Core.Screenings;

/// <summary>
/// Ce qu'un <c>Scan</c> a rendu : un relevé et ses aperçus, l'une des deux fins nommées à zéro
/// objet, ou un échec à phase et famille nommées. Quatre fins, closes, et aucune n'est le silence.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Le relevé sort en <b>texte pivot</b>, jamais en <see cref="ColumnListing"/>.</b> La voie
/// connectée repasse par <see cref="ColumnListingIngestion.Ingest"/> comme le chemin collé : « un
/// seul format pivot » tient alors <b>par construction</b> plutôt que par relecture, et les neuf cas
/// de refus — <c>CountMismatch</c>, <c>DuplicateColumn</c>, <c>RankGap</c> compris — sont réutilisés
/// gratuitement. <see cref="ColumnListing"/> n'a d'ailleurs aucun constructeur public : c'est la
/// même clause, écrite dans le type.
/// </para>
/// <para>
/// ⚠️ <b>L'échec n'est pas une exception, et ce n'est pas un goût.</b> Un hôte injoignable est le cas
/// <b>fréquent</b> du chemin connecté ; le rendre par une exception le rangerait parmi les pannes du
/// service, alors que c'est une fin que l'écran d'attente sait dire. L'annulation, elle, reste une
/// <see cref="OperationCanceledException"/> : elle n'est pas une fin du scan, c'est l'appelant qui
/// reprend la main.
/// </para>
/// <para>
/// ⚠️ <b>Les deux fins à zéro objet sont deux, et la seconde seule appelle un geste.</b> Une base
/// <b>sans table</b> et une base <b>absente du catalogue de schémas</b> rendent toutes deux zéro
/// colonne ; les confondre enverrait l'<c>Operator</c> chercher une base vide quand il lui faut
/// demander un accès. Ni l'une ni l'autre ne produit un <c>Screening</c> de zéro colonne : ce
/// rapport-là ferait <b>reculer le rapport courant</b> et détruirait des jours d'arbitrage pour une
/// connexion d'essai.
/// </para>
/// </remarks>
public sealed class ScanOutcome
{
  private static readonly IReadOnlyDictionary<ColumnIdentity, ColumnPreview> NoPreview =
    new Dictionary<ColumnIdentity, ColumnPreview>();

  private ScanOutcome(
    ScanEnding ending,
    string? pivot,
    IReadOnlyDictionary<ColumnIdentity, ColumnPreview> previews,
    ScanFailure? failure)
  {
    Ending = ending;
    Pivot = pivot;
    Previews = previews;
    Failure = failure;
  }

  /// <summary>La fin que ce scan a connue. Toujours présente : il n'y a pas de cinquième forme.</summary>
  public ScanEnding Ending { get; }

  /// <summary>
  /// Le texte pivot du relevé, tel qu'il sera donné à <see cref="ColumnListingIngestion.Ingest"/>.
  /// Absent sur les trois autres fins.
  /// </summary>
  public string? Pivot { get; }

  /// <summary>
  /// Un <see cref="ColumnPreview"/> par colonne du relevé — jamais moins, jamais une case vide : une
  /// colonne sans valeur lisible porte une raison, et c'est <c>Un aperçu n'est jamais vide</c> tenu
  /// jusqu'ici. Vide sur les trois autres fins.
  /// </summary>
  public IReadOnlyDictionary<ColumnIdentity, ColumnPreview> Previews { get; }

  /// <summary>La phase et la famille de ce qui a fait tomber le scan. Absente sur les trois autres fins.</summary>
  public ScanFailure? Failure { get; }

  /// <summary>Le scan a relevé la base : un texte pivot, et un aperçu par colonne.</summary>
  public static ScanOutcome Listed(
    string pivot,
    IReadOnlyDictionary<ColumnIdentity, ColumnPreview> previews)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(pivot);
    ArgumentNullException.ThrowIfNull(previews);

    return new ScanOutcome(ScanEnding.Listed, pivot, previews, failure: null);
  }

  /// <summary>
  /// La base a répondu, et elle ne porte aucune table. Rien n'a raté ; il n'y a rien à relever.
  /// </summary>
  public static ScanOutcome NoTable()
  {
    return new ScanOutcome(ScanEnding.NoTable, pivot: null, NoPreview, failure: null);
  }

  /// <summary>
  /// La base est absente du catalogue de schémas : le compte de connexion ne voit rien. Le geste à
  /// poser est de demander un accès, et c'est la seule des deux fins à zéro objet qui en appelle un.
  /// </summary>
  public static ScanOutcome DatabaseAbsentFromCatalogue()
  {
    return new ScanOutcome(
      ScanEnding.DatabaseAbsentFromCatalogue,
      pivot: null,
      NoPreview,
      failure: null);
  }

  /// <summary>
  /// Le scan est tombé. La phase dit où, la famille dit de quel côté — et rien d'autre ne sort.
  /// </summary>
  public static ScanOutcome Failed(ScanPhase phase, ScanFailureFamily family)
  {
    return new ScanOutcome(
      ScanEnding.Failed,
      pivot: null,
      NoPreview,
      new ScanFailure(phase, family));
  }
}

/// <summary>
/// Les fins d'un <c>Scan</c>, closes : les <b>quatre</b> que la base rend, et <b>l'abandon</b>, que
/// l'<c>Operator</c> pose et que le port ne rend jamais.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>La ligne de partage est <see cref="ComesFromTheScanner"/>, et c'est elle que ce type
/// garde.</b> Une cinquième fin <i>venue de la base</i> serait une fin que les écrans déjà écrits ne
/// sauraient pas dire ; <see cref="Abandoned"/>, lui, ne sort d'aucune fabrique de
/// <see cref="ScanOutcome"/> — il n'existe pas de <c>ScanOutcome.Abandoned()</c>, et il n'en
/// existera pas.
/// </para>
/// <para>
/// ⚠️ <b>L'abandon est une fin, et non le silence.</b> Le laisser sans fin aurait fait rafraîchir
/// indéfiniment un écran d'attente que plus personne ne mène, et laissé <c>ScansInFlight</c> tenir
/// la place jusqu'au redémarrage du service.
/// </para>
/// </remarks>
public sealed class ScanEnding : SmartEnum<ScanEnding>
{
  public static readonly ScanEnding Listed = new(nameof(Listed), 1, "relevé");

  public static readonly ScanEnding NoTable = new(nameof(NoTable), 2, "base sans table");

  public static readonly ScanEnding DatabaseAbsentFromCatalogue = new(
    nameof(DatabaseAbsentFromCatalogue),
    3,
    "base absente du catalogue de schémas");

  public static readonly ScanEnding Failed = new(nameof(Failed), 4, "scan échoué");

  /// <summary>
  /// L'<c>Operator</c> a repris la main : la requête en cours a été coupée, et le scan s'est arrêté
  /// là où il en était. ⚠️ <b>La seule fin que la base ne rend pas.</b>
  /// </summary>
  public static readonly ScanEnding Abandoned = new(
    nameof(Abandoned),
    5,
    "scan abandonné",
    comesFromTheScanner: false);

  private ScanEnding(
    string name,
    int value,
    string frenchLabel,
    bool comesFromTheScanner = true)
    : base(name, value)
  {
    FrenchLabel = frenchLabel;
    ComesFromTheScanner = comesFromTheScanner;
  }

  /// <summary>Ce que l'écran nomme.</summary>
  public string FrenchLabel { get; }

  /// <summary>
  /// Cette fin sort-elle du port de scan ? ⚠️ <b>Faux pour le seul <see cref="Abandoned"/></b> — et
  /// c'est ce qui permet de compter les fins que la base rend sans compter les membres du type.
  /// </summary>
  public bool ComesFromTheScanner { get; }
}

/// <summary>
/// Ce qu'un scan tombé laisse dire : <b>où</b> il est tombé et <b>de quel côté</b> venait la cause.
/// Deux mots du service, et pas un caractère venu du pilote.
/// </summary>
public sealed record ScanFailure(ScanPhase Phase, ScanFailureFamily Family);
