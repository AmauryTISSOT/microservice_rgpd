using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.FunctionalTests;

/// <summary>
/// Un scanner de base réduit à ce dont un test de contrat a besoin : une fin qu'on lui dicte, les
/// aperçus qu'on lui dicte, et ce qu'il a reçu.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>La doublure se pose sur le port, jamais sur des réponses enregistrées.</b> C'est le seul
/// point du contexte qui touche une base d'un tiers : aucun test fonctionnel n'ouvre de base réelle,
/// et aucun n'a besoin d'un conteneur pour éprouver un écran de scan.
/// </para>
/// <para>
/// ⚠️ <b>Elle sait rendre les <b>quatre</b> fins.</b> Une doublure qui ne saurait que réussir
/// laisserait les écrans d'échec, les deux fins à zéro objet et l'aperçu absent sans aucun test
/// fonctionnel — c'est-à-dire précisément ce que #309 devra éprouver.
/// </para>
/// </remarks>
public sealed class DatabaseScannerDouble : IDatabaseScanner
{
  private TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

  /// <summary>La fin que le scanner rendra au prochain appel.</summary>
  public ScanOutcome Outcome { get; set; } = ScanOutcome.NoTable();

  /// <summary>
  /// Ce qui empêche ce scanner de rendre une fin. ⚠️ Aucun scanner réel ne lève : c'est la voie par
  /// laquelle un test éprouve ce que le service fait d'une panne <b>qui n'aurait pas dû</b> le
  /// traverser.
  /// </summary>
  public Exception? Breakdown { get; set; }

  /// <summary>Le dialecte tel que le scanner l'a reçu.</summary>
  public DatabaseDialect? ReceivedDialect { get; private set; }

  /// <summary>
  /// La chaîne de connexion telle que le scanner l'a reçue — ce qui permet de vérifier ce que la
  /// frontière en a fait, et surtout qu'elle ne s'est posée nulle part ailleurs.
  /// </summary>
  public string? ReceivedConnectionString { get; private set; }

  /// <summary>Le nombre d'appels reçus depuis la dernière remise à zéro.</summary>
  public int CallCount { get; private set; }

  /// <summary>
  /// Ce que le scanner fait durer avant de répondre. Une seule chose l'exige : éprouver l'écran
  /// d'attente et l'annulation, qu'un scanner instantané ne laisse jamais observer.
  /// </summary>
  public TimeSpan Delay { get; set; } = TimeSpan.Zero;

  /// <summary>Les pas que le scanner rapportera pendant qu'il travaille.</summary>
  public IReadOnlyList<ScanStep> Steps { get; set; } = [];

  /// <summary>Signale que le scanner a commencé à travailler, avant même d'avoir répondu.</summary>
  public Task Started => _started.Task;

  /// <summary>
  /// Vrai si le travail de ce scanner a été interrompu par l'annulation de l'appelant. C'est la
  /// seule façon de distinguer une requête réellement coupée d'une réponse simplement ignorée.
  /// </summary>
  public bool Interrupted { get; private set; }

  /// <summary>Un relevé accepté par l'ingestion, et un aperçu par colonne.</summary>
  public static ScanOutcome AListing(params (string Table, string Column)[] columns)
  {
    var identities = columns
      .Select(column => ColumnIdentity.Of("public", column.Table, column.Column))
      .ToList();

    var pivot = string.Join(
      '\n',
      [
        "{\"format\":\"screening-pivot/1\",\"dialecte\":\"postgresql\",\"base\":\"galette_prod\","
        + "\"genere_le\":\"2026-08-26T12:00:00Z\"}",
        .. identities.Select((identity, rank) =>
          $"{{\"schema\":\"{identity.Schema}\",\"table\":\"{identity.Table}\","
          + $"\"colonne\":\"{identity.Column}\",\"position\":{Rank(identities, rank)},"
          + "\"type\":\"text\",\"nullable\":true,\"commentaire_colonne\":null,"
          + "\"commentaire_table\":null,\"table_referencee\":null}"),
        $"{{\"fin\":true,\"colonnes\":{identities.Count}}}",
      ]);

    return ScanOutcome.Listed(
      pivot,
      identities.ToDictionary(
        identity => identity,
        identity => ColumnPreview.Read([AValueFor(identity)])));
  }

  /// <summary>Un relevé dont une colonne nommée ne porte pas de valeurs mais une raison.</summary>
  public static ScanOutcome AListingWhere(
    string table,
    string column,
    PreviewAbsenceReason reason,
    params (string Table, string Column)[] columns)
  {
    var listed = AListing(columns);
    var previews = listed.Previews.ToDictionary(entry => entry.Key, entry => entry.Value);

    previews[ColumnIdentity.Of("public", table, column)] = ColumnPreview.Absent(reason);

    return ScanOutcome.Listed(listed.Pivot!, previews);
  }

  /// <summary>
  /// Ramène la doublure à son état de départ. La fabrique est partagée par toute la collection de
  /// tests : sans cela, un test qui omettrait de dicter sa fin hériterait de celle du précédent.
  /// </summary>
  public void Reset()
  {
    Outcome = ScanOutcome.NoTable();
    Breakdown = null;
    ReceivedDialect = null;
    ReceivedConnectionString = null;
    CallCount = 0;
    Delay = TimeSpan.Zero;
    Steps = [];
    Interrupted = false;
    _started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
  }

  public async Task<ScanOutcome> ScanAsync(
    DatabaseDialect dialect,
    string connectionString,
    IProgress<ScanStep>? progress = null,
    CancellationToken cancellationToken = default)
  {
    ReceivedDialect = dialect;
    ReceivedConnectionString = connectionString;
    CallCount++;
    _started.TrySetResult();

    foreach (var step in Steps)
    {
      progress?.Report(step);
    }

    if (Delay > TimeSpan.Zero)
    {
      try
      {
        await Task.Delay(Delay, cancellationToken);
      }
      catch (OperationCanceledException)
      {
        // Le travail en cours s'arrête vraiment : sous SQLite, l'annulation coupe la requête, pas
        // seulement la boucle. La doublure doit laisser observer la même chose.
        Interrupted = true;

        throw;
      }
    }

    if (Breakdown is not null)
    {
      throw Breakdown;
    }

    return Outcome;
  }

  /// <summary>
  /// Une valeur dictée, dont la longueur réelle se <b>mesure</b> plutôt que de s'écrire.
  /// </summary>
  /// <remarks>
  /// ⚠️ Une longueur écrite en dur ferait lever la doublure dès qu'un test nommerait une colonne
  /// plus longue que la phrase qui l'entoure — <c>date_naissance</c> suffit —, et c'est la fixture
  /// sur laquelle #308 et #309 bâtiront leurs écrans.
  /// </remarks>
  private static PreviewedValue AValueFor(ColumnIdentity identity)
  {
    var text = $"valeur de {identity.Column}";

    return PreviewedValue.Of(text, text.Length);
  }

  private static int Rank(IReadOnlyList<ColumnIdentity> identities, int index)
  {
    // Le rang se compte dans la table, pas dans le relevé : un saut de rang est l'un des neuf
    // refus, et une doublure qui numéroterait à la file les déclencherait tous.
    return identities.Take(index + 1).Count(other => other.Table == identities[index].Table);
  }
}
