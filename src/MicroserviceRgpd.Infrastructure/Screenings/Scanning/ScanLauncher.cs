using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.UseCases.Screenings.RunScan;

namespace MicroserviceRgpd.Infrastructure.Screenings.Scanning;

/// <summary>
/// Fait partir un <c>Scan</c> à la demande de l'<c>Operator</c>, et lui rend la main tout de suite.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Rien ne tourne en fond, et ce n'est pas une nuance de vocabulaire.</b> Aucune boucle
/// n'attend ici de travail à prendre : ce lanceur ne fait quelque chose que parce qu'une requête HTTP
/// vient de le lui demander, et il ne fera plus rien après. Le garde
/// <c>NothingRunsInTheBackgroundTests</c> lit l'IL du dépôt — pas de <c>BackgroundService</c>, pas de
/// minuterie, pas d'ordonnanceur —, et ce fichier est le seul du service qui rende la main avant
/// d'avoir fini.
/// </para>
/// <para>
/// ⚠️ <b>Une portée de service à lui, et le jeton d'annulation de personne.</b> Emprunter la portée
/// de la requête ferait disposer le <c>DbContext</c> au milieu de l'écriture du rapport ; emprunter
/// son jeton ferait qu'un onglet fermé annule une lecture de quarante secondes déjà lancée sur la
/// base d'un tiers.
/// </para>
/// <para>
/// ⚠️ <b>La chaîne de connexion ne va nulle part ailleurs.</b> Elle traverse cet objet jusqu'au
/// geste, et le geste jusqu'au port. Aucun journal ne l'écrit, aucun tag de trace ne la porte, et
/// <see cref="ScanProgress"/> n'a pas de champ où la mettre.
/// </para>
/// <para>
/// ⚠️ <b>Ce qui casse dans le geste se pose sur l'avancement, jamais dans le vide.</b> Une exception
/// non rattrapée dans une tâche que personne n'attend serait perdue, et le scan resterait « en vol »
/// pour toujours : le déploiement entier refuserait tout nouveau lancement jusqu'au redémarrage.
/// </para>
/// </remarks>
/// <param name="scopes">De quoi ouvrir une portée qui survivra à la requête.</param>
/// <param name="inFlight">Le fait du déploiement : un seul scan à la fois.</param>
/// <param name="clock">L'horloge injectée, qui date le lancement.</param>
/// <param name="logger">Le journal, où l'on écrit ce qui casse — et rien de ce que l'on a lu.</param>
public sealed class ScanLauncher(
  IServiceScopeFactory scopes,
  ScansInFlight inFlight,
  TimeProvider clock,
  ILogger<ScanLauncher> logger)
  : IScanLauncher
{
  /// <inheritdoc />
  public ScanLaunch Launch(DatabaseDialect dialect, string connectionString)
  {
    ArgumentNullException.ThrowIfNull(dialect);
    ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

    var progress = ScanProgress.Starting(ScanId.Next(), clock.GetUtcNow());

    if (!inFlight.TryTakeOff(progress, out var alreadyRunning))
    {
      return ScanLaunch.RefusedBecause(alreadyRunning!);
    }

    // ⚠️ La place est prise AVANT que la tâche parte : la prendre après aurait laissé deux requêtes
    // simultanées lancer deux lectures sur la base du client avant que l'une des deux n'écrive.
    _ = Task.Run(() => RunAsync(progress, dialect, connectionString), CancellationToken.None);

    return ScanLaunch.TakenOff(progress.Id);
  }

  private async Task RunAsync(
    ScanProgress progress,
    DatabaseDialect dialect,
    string connectionString)
  {
    try
    {
      // ⚠️ L'ouverture de la portée est DANS le rattrapage, et sa fermeture aussi. Le fournisseur
      // racine disposé — un arrêt d'hôte pendant un scan — ferait sinon échapper l'exception d'une
      // tâche que personne n'attend : elle serait perdue, le scan resterait « en vol » pour
      // toujours, et le déploiement entier refuserait tout lancement jusqu'au redémarrage.
      using var scope = scopes.CreateScope();

      var gesture = scope.ServiceProvider.GetRequiredService<ScanGesture>();

      await gesture.RunAsync(progress, dialect, connectionString, CancellationToken.None);
    }
#pragma warning disable CA1031 // Personne n'attend cette tâche : ce qui n'est pas rattrapé ici est perdu.
    catch (Exception exception)
#pragma warning restore CA1031
    {
      // ⚠️ Le journal reçoit l'exception, jamais la chaîne de connexion ni ce qui a été lu. Un
      // pilote qui recopie l'adresse du serveur dans son message est le seul cas où quelque chose de
      // l'accès approche du journal, et c'est le port de scan qui l'arrête avant d'arriver ici.
      logger.LogError(exception, "Le scan {ScanId} s'est arrêté sur une erreur non prévue.", progress.Id);

      progress.EndedWithoutAReport(
        ScanEnding.Failed,
        new ScanFailure(progress.Snapshot.Phase, ScanFailureFamily.Database));
    }
  }
}
