namespace MicroserviceRgpd.Core.Screenings;

/// <summary>
/// Ce par quoi un <c>Scan</c> <b>part</b> : il reçoit le dialecte et la chaîne de connexion de
/// l'<c>Operator</c>, prend la place s'il n'y a personne, et rend l'identité de l'écran d'attente —
/// ou le refus, qui nomme le scan en cours.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Le scan est lancé par la <i>requête</i> de l'<c>Operator</c>, et par rien d'autre.</b> Ni
/// <c>IHostedService</c>, ni <c>BackgroundService</c>, ni minuterie : le garde
/// <c>NothingRunsInTheBackgroundTests</c> lit l'IL et les attraperait. Ce que ce port fait n'est pas
/// « faire tourner quelque chose » — c'est <b>ne pas attendre</b> une lecture que l'<c>Operator</c>
/// a demandée, et dont il vient regarder l'avancement réel.
/// </para>
/// <para>
/// ⚠️ <b>Le scan survit à la fin de la requête HTTP, donc il ouvre sa <i>propre</i> portée de
/// service.</b> Emprunter celle de la requête ferait disposer le <c>DbContext</c> au milieu de
/// l'écriture du rapport, et un onglet fermé aurait annulé un scan de quarante secondes lancé sur
/// la production d'un tiers.
/// </para>
/// <para>
/// ⚠️ <b>La chaîne de connexion est remise au port de scan, et à rien d'autre.</b> Elle ne passe par
/// aucun message dont un journal recopierait les propriétés, ne se pose sur aucun tag de trace, et
/// n'entre ni dans <see cref="ScanProgress"/> ni en base. Ce port est le dernier endroit du service
/// qui la voit avant la frontière.
/// </para>
/// </remarks>
public interface IScanLauncher
{
  /// <summary>
  /// Fait partir un scan, si aucun ne court.
  /// </summary>
  /// <param name="dialect">Le SGBD que l'<c>Operator</c> a choisi.</param>
  /// <param name="connectionString">Sa chaîne de connexion, qui ne ressort d'aucun côté.</param>
  ScanLaunch Launch(DatabaseDialect dialect, string connectionString);

  /// <summary>
  /// L'<c>Operator</c> reprend la main : la fin est posée sur l'avancement, et la <b>requête en
  /// cours</b> est coupée.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Elle coupe la requête, pas seulement la boucle.</b> Un abandon qui se contenterait de
  /// sortir de la boucle laisserait un <c>SELECT</c> courir sur la base d'un tiers après que
  /// l'<c>Operator</c> a dit d'arrêter — et le service ne saurait même pas qu'il le fait courir.
  /// C'est le port qui sait interrompre ; ce qui se tient ici est de lui en donner l'ordre.
  /// </para>
  /// <para>
  /// ⚠️ <b>Un geste annulé n'a aucune conséquence.</b> Abandonner un scan déjà fini, un scan qui
  /// n'est pas celui qui court, ou un scan que le processus ne connaît plus : les trois ne font
  /// rien, et aucun ne fait reculer la fin déjà posée.
  /// </para>
  /// </remarks>
  /// <param name="scan">L'identité lue dans l'adresse de l'écran d'attente.</param>
  /// <returns>Vrai si c'est bien ce scan-là qui courait, et qu'il vient d'être arrêté.</returns>
  bool Abandon(ScanId scan);
}

/// <summary>
/// Ce qu'un lancement rend : le scan est parti et voici où l'attendre, ou il est <b>refusé</b> et
/// voici celui qui court.
/// </summary>
/// <remarks>
/// ⚠️ <b>Le refus nomme le scan en cours, et ce n'est pas une politesse.</b> Un « réessayez plus
/// tard » laisserait l'<c>Operator</c> ignorer si le service travaille pour lui ou pour quelqu'un
/// d'autre — et il relancerait, sur la production d'un tiers, une lecture déjà en cours.
/// </remarks>
public sealed class ScanLaunch
{
  private ScanLaunch(ScanId? started, ScanProgress? alreadyRunning)
  {
    Started = started;
    AlreadyRunning = alreadyRunning;
  }

  /// <summary>L'identité du scan qui vient de partir, et l'adresse de son écran d'attente.</summary>
  public ScanId? Started { get; }

  /// <summary>Le scan qui courait déjà, quand ce lancement a été refusé.</summary>
  public ScanProgress? AlreadyRunning { get; }

  /// <summary>Le scan est-il parti ?</summary>
  public bool TookOff => Started is not null;

  /// <summary>La place était libre : ce scan la tient, et l'écran d'attente l'attend.</summary>
  public static ScanLaunch TakenOff(ScanId started)
  {
    return new ScanLaunch(started, alreadyRunning: null);
  }

  /// <summary>Un scan court déjà. Il est nommé, et le second lancement n'a pas eu lieu.</summary>
  /// <exception cref="ArgumentNullException"><paramref name="alreadyRunning"/> est absent — un refus sans le scan qu'il nomme n'est pas ce refus-là.</exception>
  public static ScanLaunch RefusedBecause(ScanProgress alreadyRunning)
  {
    ArgumentNullException.ThrowIfNull(alreadyRunning);

    return new ScanLaunch(started: null, alreadyRunning);
  }
}
