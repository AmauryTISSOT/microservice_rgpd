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
  private readonly Lock _turn = new();

  private Interruptible? _interruptible;

  /// <inheritdoc />
  public ScanLaunch Launch(DatabaseDialect dialect, string connectionString)
  {
    ArgumentNullException.ThrowIfNull(dialect);
    ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

    var progress = ScanProgress.Starting(ScanId.Next(), dialect, clock.GetUtcNow());

    if (!inFlight.TryTakeOff(progress, out var alreadyRunning))
    {
      return ScanLaunch.RefusedBecause(alreadyRunning!);
    }

    // ⚠️ Le jeton est celui de PERSONNE d'autre. Il n'est ni celui de la requête — un onglet fermé
    // couperait une lecture de quarante secondes lancée sur la base d'un tiers —, ni celui de
    // l'hôte : c'est un jeton que seul l'abandon de l'Operator déclenche.
    var abandon = new CancellationTokenSource();

    lock (_turn)
    {
      _interruptible = new Interruptible(progress.Id, abandon);
    }

    // ⚠️ La place est prise AVANT que la tâche parte : la prendre après aurait laissé deux requêtes
    // simultanées lancer deux lectures sur la base du client avant que l'une des deux n'écrive.
    _ = Task.Run(
      () => RunAsync(progress, dialect, connectionString, abandon),
      CancellationToken.None);

    return ScanLaunch.TakenOff(progress.Id);
  }

  /// <inheritdoc />
  public void Abandon(ScanId scan)
  {
    if (inFlight.Running is not { } running || running.Id != scan)
    {
      // ⚠️ Un geste annulé n'a aucune conséquence : ce scan a déjà fini, ce n'est pas celui qui
      // court, ou le processus ne le connaît plus. Aucun des trois ne fait reculer quoi que ce soit.
      return;
    }

    // ⚠️ LA FIN EST POSÉE D'ABORD, et la requête coupée ensuite. Attendre que le port lève ferait
    // répondre à ce POST un écran d'attente qui se rafraîchit encore, à propos d'un scan que
    // l'Operator vient précisément d'arrêter.
    running.Abandon();

    Cut(scan);
  }

  /// <summary>
  /// Coupe la requête que ce scan a en cours, si c'est bien lui que le jeton retenu commande.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>L'ordre part <i>hors</i> du verrou.</b> <see cref="CancellationTokenSource.Cancel()"/>
  /// exécute les rappels d'annulation sur le fil qui l'appelle : les lancer sous le verrou aurait
  /// laissé le fil du scan, qui prend ce même verrou en rendant la main, attendre celui qui
  /// l'annule.
  /// </remarks>
  private void Cut(ScanId scan)
  {
    CancellationTokenSource? abandon;

    lock (_turn)
    {
      abandon = _interruptible is { } running && running.Scan == scan ? running.Abandon : null;
    }

    try
    {
      abandon?.Cancel();
    }
    catch (ObjectDisposedException)
    {
      // Le scan a fini entre la lecture du jeton et cet appel : il n'y a plus rien à couper, et la
      // fin qu'il a posée gagne de toute façon sur l'abandon.
    }
  }

  private async Task RunAsync(
    ScanProgress progress,
    DatabaseDialect dialect,
    string connectionString,
    CancellationTokenSource abandon)
  {
    try
    {
      // ⚠️ RIEN NE PART SI LA FIN EST DÉJÀ POSÉE, et cette ligne ferme une course étroite mais
      // réelle. Entre la prise de place et l'armement du jeton, ce scan est déjà TROUVABLE : un
      // abandon qui tombe là pose la fin, puis ne trouve aucun jeton à couper. Sans ce garde, la
      // lecture partirait ensuite sur la base d'un tiers — l'écran disant « scan abandonné »
      // pendant qu'un SELECT y court encore, ce qui est très exactement ce que l'abandon existe
      // pour empêcher. Un scan qui n'a jamais commencé n'a, lui, rien à interrompre.
      if (progress.Snapshot.HasEnded)
      {
        return;
      }

      // ⚠️ L'ouverture de la portée est DANS le rattrapage, et sa fermeture aussi. Le fournisseur
      // racine disposé — un arrêt d'hôte pendant un scan — ferait sinon échapper l'exception d'une
      // tâche que personne n'attend : elle serait perdue, le scan resterait « en vol » pour
      // toujours, et le déploiement entier refuserait tout lancement jusqu'au redémarrage.
      using var scope = scopes.CreateScope();

      var gesture = scope.ServiceProvider.GetRequiredService<ScanGesture>();

      await gesture.RunAsync(progress, dialect, connectionString, abandon.Token);
    }
    catch (OperationCanceledException)
    {
      // ⚠️ L'ABANDON N'EST PAS UN ÉCHEC, et il ne s'écrit pas au journal comme une panne. La fin est
      // déjà posée — Abandon la pose avant de couper —, et la redire ici ne changerait rien ; ce qui
      // compte est que cette exception ne devienne PAS un « scan échoué, famille : la base », qui
      // enverrait l'Operator chercher une panne là où il a lui-même arrêté la lecture.
    }
#pragma warning disable CA1031 // Personne n'attend cette tâche : ce qui n'est pas rattrapé ici est perdu.
    catch (Exception exception)
#pragma warning restore CA1031
    {
      // ⚠️ Le journal reçoit l'exception, jamais la chaîne de connexion ni ce qui a été lu. Un
      // pilote qui recopie l'adresse du serveur dans son message est le seul cas où quelque chose de
      // l'accès approche du journal, et c'est le port de scan qui l'arrête avant d'arriver ici.
      logger.LogError(exception, "Le scan {ScanId} s'est arrêté sur une erreur non prévue.", progress.Id);

      // ⚠️ « LA BASE », ET C'EST UN CHOIX PAR DÉFAUT, NON UNE LECTURE DE L'EXCEPTION. Ce rattrapage
      // ne voit que ce que le port N'A PAS su nommer : les familles vraies — le réseau, ce qui a été
      // fourni — sortent du port, qui seul sait ce qu'il a tenté. Ici, on ne sait pas, et il n'y a
      // pas de quatrième famille : en ajouter une « inconnue » ferait, à chaque panne neuve, une
      // valeur que du code déjà écrit ne saurait pas afficher. Reste la moins trompeuse des trois :
      // elle n'envoie l'Operator ni vérifier une chaîne qui n'est peut-être pas en cause, ni
      // soupçonner un réseau qui a répondu.
      progress.EndedWithoutAReport(
        ScanEnding.Failed,
        new ScanFailure(progress.Snapshot.Phase, ScanFailureFamily.Database));
    }
    finally
    {
      // ⚠️ LA RÉFÉRENCE EST LÂCHÉE SOUS LE VERROU, ET LA LIBÉRATION VIENT APRÈS. Un abandon qui
      // arriverait pendant ces deux lignes lit le jeton sous ce même verrou : il le trouve déjà
      // absent, et n'appelle donc jamais Cancel sur un objet libéré.
      lock (_turn)
      {
        if (_interruptible is { } running && running.Scan == progress.Id)
        {
          _interruptible = null;
        }
      }

      abandon.Dispose();
    }
  }

  /// <summary>
  /// Le scan que l'on sait encore <b>interrompre</b> : son identité, et le jeton qui coupe sa
  /// requête.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Les deux ne voyagent jamais l'un sans l'autre.</b> Séparés en deux champs, l'invariant
  /// « posés ensemble, lâchés ensemble » se tenait à la main sur trois sites — et il suffisait d'en
  /// oublier un pour couper la requête d'un <i>autre</i> scan que celui qu'on abandonne, ou pour
  /// appeler <c>Cancel</c> sur un jeton déjà libéré.
  /// </remarks>
  /// <param name="Scan">Le scan que ce jeton commande.</param>
  /// <param name="Abandon">Ce qui coupe sa requête en cours.</param>
  private sealed record Interruptible(ScanId Scan, CancellationTokenSource Abandon);
}
