using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UseCases.Screenings.RunScan;

/// <summary>
/// Le geste du <c>Scan</c> : il fait lire la base par le port, ingère le pivot qu'elle rend, fait
/// détecter avec les aperçus, écrit le rapport <c>Scanné</c>, dépose les aperçus en mémoire — et
/// tient l'avancement à jour à chaque pas.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Ce n'est pas un message, et ce n'en sera pas un.</b> Le <c>LoggingBehavior</c> du dépôt
/// recopie <b>toutes</b> les propriétés de chaque requête dans le journal : une commande qui
/// porterait la chaîne de connexion l'écrirait à chaque lancement, et une réponse qui porterait les
/// aperçus recopierait des valeurs réelles du client. Le geste est donc une classe ordinaire,
/// appelée directement.
/// </para>
/// <para>
/// ⚠️ <b>Il court hors de la requête HTTP, dans une portée de service à lui.</b> L'onglet fermé ne
/// l'annule pas : c'est le lanceur qui lui ouvre cette portée, et le jeton qu'il reçoit n'est pas
/// celui de l'<c>Operator</c>.
/// </para>
/// <para>
/// ⚠️ <b>L'archivage du rapport courant est un effet du succès, et de lui seul.</b> Le rapport
/// courant est le dernier lancé : écrire le nouveau <i>est</i> archiver l'ancien. Les fins sans
/// relevé n'écrivent rien, donc n'archivent rien — un scan tombé ne fait pas perdre à
/// l'<c>Operator</c> le rapport qu'il avait sous les yeux.
/// </para>
/// <para>
/// ⚠️ <b>L'ordre de la fin n'est pas indifférent.</b> Les aperçus se déposent <b>avant</b> que
/// l'avancement ne dise « produit » : l'écran d'attente redirige sur cette annonce, et un rapport
/// atteint une milliseconde avant son jeu d'aperçus aurait montré sans aperçu des colonnes qui en
/// ont un.
/// </para>
/// </remarks>
/// <param name="scanner">Le port de lecture. Il est le dernier à voir la chaîne de connexion.</param>
/// <param name="engine">Le port de la détection, le même que sur le chemin collé.</param>
/// <param name="screenings">Le dépôt des rapports.</param>
/// <param name="previews">Le cache mémoire du jeu d'aperçus vivant.</param>
/// <param name="clock">L'horloge injectée. C'est elle qui décide quel rapport est le courant.</param>
public sealed class ScanGesture(
  IDatabaseScanner scanner,
  IScreeningEngine engine,
  IRepository<Screening> screenings,
  ScanPreviews previews,
  TimeProvider clock)
{
  /// <summary>
  /// Mène le scan de bout en bout, en tenant <paramref name="progress"/> à jour.
  /// </summary>
  /// <param name="progress">Le transitoire que l'écran d'attente lit, déjà en vol.</param>
  /// <param name="dialect">Le SGBD choisi par l'<c>Operator</c>.</param>
  /// <param name="connectionString">Sa chaîne de connexion, qui ne ressort d'aucun côté.</param>
  /// <param name="cancellationToken">Le jeton du <b>lanceur</b>, jamais celui de la requête.</param>
  /// <exception cref="ArgumentNullException"><paramref name="progress"/> est absent.</exception>
  public async Task RunAsync(
    ScanProgress progress,
    DatabaseDialect dialect,
    string connectionString,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(progress);

    var outcome = await scanner.ScanAsync(
      dialect,
      connectionString,
      new ProgressOf(progress),
      cancellationToken);

    if (outcome.Ending != ScanEnding.Listed)
    {
      progress.EndedWithoutAReport(outcome.Ending, outcome.Failure);

      return;
    }

    var ingested = ColumnListingIngestion.Ingest(outcome.Pivot);

    if (ingested.Refusal is not null)
    {
      // ⚠️ Ce pivot est le nôtre : un refus ici n'est pas une faute de l'Operator, il n'a rien collé.
      // Il est dit comme un échec du relevé plutôt que redit à l'écran en neuf cas de format, dont
      // aucun ne signifierait quoi que ce soit pour qui n'a rien écrit.
      progress.EndedWithoutAReport(
        ScanEnding.Failed,
        new ScanFailure(ScanPhase.Cataloguing, ScanFailureFamily.Database));

      return;
    }

    var listing = ingested.Listing!;

    // ⚠️ La détection est la phase que le scan n'atteint jamais : le port a rendu son relevé et s'est
    // tu. L'attente en couvre une de plus que le scan parce que l'Operator attend un rapport.
    progress.Detecting(listing.ColumnCount);

    var screened = await engine.ScreenAsync(listing, outcome.Previews, cancellationToken);

    var screening = Screening.Of(
      ScreeningId.Next(),
      listing.Database,
      listing.Dialect,
      // ⚠️ L'origine est posée par LE GESTE, comme sur le chemin collé : rien en aval ne distingue
      // les deux relevés, c'est le chemin d'entrée qui sait.
      ListingOrigin.Scanned,
      screened.Engine,
      listing.DeclaredColumnCount,
      screened.Columns,
      clock.GetUtcNow());

    await screenings.AddAsync(screening, cancellationToken);

    try
    {
      previews.Keep(screening.Id, outcome.Previews);
    }
    finally
    {
      // ⚠️ LE RAPPORT EST ÉCRIT : quoi qu'il arrive après cette ligne, la fin dit qu'il existe. Ce
      // qui casserait ici tomberait sinon dans le rattrapage du lanceur, qui écrirait « arrêté sans
      // produire de rapport » — et l'écran d'attente annoncerait à l'Operator que son rapport
      // courant n'a pas bougé, alors que l'ancien vient de partir à l'archive.
      progress.Produced(screening.Id);
    }
  }

  /// <summary>
  /// Le fil qui relie les pas du port à l'avancement, <b>sur le fil qui les rapporte</b>.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Ce n'est pas un <see cref="Progress{T}"/>, et ce n'est pas un détail.</b> Celui-là poste
  /// ses appels au pool : la barre serait en retard d'un pas sur ce que le scan a réellement fait, et
  /// le dernier pas d'une phase pourrait arriver après le premier de la suivante — l'écran aurait
  /// affiché « aperçus 312 sur 312 » alors que la détection courait déjà.
  /// </remarks>
  private sealed class ProgressOf(ScanProgress progress) : IProgress<ScanStep>
  {
    public void Report(ScanStep value)
    {
      progress.Record(value);
    }
  }
}
