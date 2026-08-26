using MicroserviceRgpd.Core.Screenings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MicroserviceRgpd.Web.Pages.Screenings;

/// <summary>
/// L'écran d'attente d'un <c>Scan</c> : la phase où il en est, le <b>compte réel</b> de cette phase,
/// et rien d'inventé — jusqu'au <c>303</c> qui cède la place au rapport.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Aucune ligne de JavaScript.</b> Le rafraîchissement est un
/// <c>&lt;meta http-equiv="refresh"&gt;</c>, l'avancement est une largeur CSS, et le compte est du
/// texte. Tout ce que cet écran montre survit à un navigateur qui n'exécute rien.
/// </para>
/// <para>
/// ⚠️ <b>La fin est un <c>303</c>, pas un <c>meta refresh</c> de plus.</b> Un rafraîchissement qui
/// mènerait au rapport laisserait l'écran d'attente dans l'historique : l'<c>Operator</c> qui revient
/// d'une page tomberait sur « ce scan n'existe plus », à propos d'un scan qui a parfaitement réussi.
/// Le <c>303</c>, lui, remplace l'entrée d'historique.
/// </para>
/// <para>
/// ⚠️ <b>L'écran ne porte aucun état, et deux <c>Operator</c> y voient la même chose.</b>
/// L'avancement est un fait du déploiement, pas de la session : la seconde personne qui ouvre cette
/// adresse lit le même compte, à la même seconde, sans rien relancer.
/// </para>
/// <para>
/// ⚠️ <b>Fermer l'onglet n'annule rien.</b> Cette page ne fait que <i>lire</i> ; le scan court dans
/// une portée de service à lui, et il ne sait pas que quelqu'un le regardait.
/// </para>
/// </remarks>
/// <param name="inFlight">Le fait du déploiement, où l'on retrouve le scan que l'adresse nomme.</param>
/// <param name="launcher">Ce par quoi l'abandon coupe la requête en cours.</param>
public class ScanModel(ScansInFlight inFlight, IScanLauncher launcher) : PageModel
{
  /// <summary>Le délai du rafraîchissement, en secondes.</summary>
  /// <remarks>
  /// ⚠️ <b>Trois secondes, et la constante est lue par le HTML.</b> Un « 3 » recopié dans la balise
  /// aurait continué de s'afficher le jour où le délai bouge.
  /// </remarks>
  public const int RefreshInSeconds = 3;

  /// <summary>L'identité du scan, lue dans l'adresse.</summary>
  [BindProperty(SupportsGet = true)]
  public Guid ScanId { get; set; }

  /// <summary>Où en est ce scan, pris <b>d'un seul coup</b>, ou <c>null</c> s'il n'est plus connu.</summary>
  public ScanSnapshot? Snapshot { get; private set; }

  /// <summary>
  /// L'écran de la fin, quand ce scan en a une — ou celui du scan que le processus ne connaît plus.
  /// <c>null</c> tant qu'il court.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Toutes les fins passent par le même bloc</b>, y compris le scan inconnu, qui n'est
  /// pourtant la fin d'aucun scan que ce processus ait mené. C'est ce qui garantit qu'aucune d'elles
  /// n'oublie de dire où ça s'est arrêté, d'où vient la cause, et que le rapport courant n'a pas
  /// bougé.
  /// </remarks>
  public ScanEndingScreen? Ending { get; private set; }

  /// <summary>
  /// La largeur de la barre, en pour cent — <b>et il n'y en a une que si le compte est réel</b>.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>C'est le seul endroit où un pourcentage apparaît, et il ne s'affiche pas.</b> Il est la
  /// largeur d'un rectangle ; le chiffre montré à l'<c>Operator</c> reste « table 148 sur 312 ». Un
  /// pourcentage écrit se lit comme une mesure du <i>temps</i> restant, ce qu'il n'est pas.
  /// </remarks>
  public int BarWidth =>
    Snapshot is { Done: { } done, Total: { } total } && total > 0
      ? (int)Math.Round(done * 100d / total)
      : 0;

  /// <summary>Ce que la phase compte : la table pour les aperçus, la colonne pour la détection.</summary>
  /// <remarks>
  /// ⚠️ <b>Rien n'additionne les deux.</b> Additionner reviendrait à décider d'avance qu'une table
  /// « vaut » <i>n</i> colonnes, et le total global qui en sortirait serait un chiffre inventé.
  /// </remarks>
  public string Unit =>
    Snapshot?.Phase == ScanPhase.Detecting ? "colonne" : "table";

  public IActionResult OnGet()
  {
    var progress = Known();

    if (progress is null)
    {
      // Le processus a redémarré, ou un autre scan a pris la place. L'écran le DIT plutôt que de
      // rediriger en silence vers un rapport qui n'est peut-être pas celui qu'on attendait.
      Ending = ScanEndingScreen.NoLongerKnown();

      return Page();
    }

    Snapshot = progress.Snapshot;

    if (Snapshot.Ending == ScanEnding.Listed)
    {
      // ⚠️ 303, et vers le rapport COURANT : c'est celui que ce scan vient d'écrire, puisque le
      // courant est le dernier lancé. Le rapport ne s'atteint pas par un identifiant dans l'adresse,
      // et cet écran n'invente pas une route qui n'existe pas.
      return SeeOther(Url.Page("Report")!);
    }

    Ending = ScanEndingScreen.Of(Snapshot);

    return Page();
  }

  /// <summary>
  /// L'<c>Operator</c> abandonne ce scan : la fin est posée, et la <b>requête en cours</b> sur la
  /// base est coupée.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Un <c>form method="post"</c>, et pas une ligne de JavaScript.</b> Cet écran se rafraîchit
  /// par un <c>meta</c> ; un bouton qui aurait eu besoin d'un script n'aurait pas marché là où le
  /// reste de l'écran marche.
  /// </para>
  /// <para>
  /// ⚠️ <b>Un <c>303</c> vers ce même écran, et non la page rendue ici.</b> Sans lui, un
  /// rechargement du navigateur reposterait l'abandon — sur un scan qui, entre-temps, peut être un
  /// <b>autre</b> scan portant la place. Le geste ne se rejoue pas.
  /// </para>
  /// <para>
  /// ⚠️ <b>Un geste annulé n'a aucune conséquence.</b> Abandonner un scan déjà fini ou un scan qui
  /// n'est plus celui qui court ne fait rien du tout : la réponse est la même, et c'est l'écran de
  /// la fin que ce scan a réellement connue.
  /// </para>
  /// </remarks>
  public IActionResult OnPost()
  {
    if (Core.Screenings.ScanId.TryFrom(ScanId, out var scanId))
    {
      launcher.Abandon(scanId);
    }

    return SeeOther(Url.Page("Scan", new { scanId = ScanId })!);
  }

  /// <summary>
  /// Le scan que cette adresse nomme, s'il est encore connu du processus.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>La contrainte de route ne dit que « c'est un GUID », et le GUID vide en est un.</b> Le
  /// bâtir sans précaution lèverait sur une adresse tapée à la main ou tronquée, et l'<c>Operator</c>
  /// recevrait un 500 nu là où cet écran a une phrase à lui dire.
  /// </remarks>
  private ScanProgress? Known()
  {
    return Core.Screenings.ScanId.TryFrom(ScanId, out var scanId) ? inFlight.Find(scanId) : null;
  }

  /// <summary>
  /// La redirection <b>après une consultation</b>, qui remplace l'entrée d'historique plutôt que de
  /// s'y ajouter.
  /// </summary>
  private IActionResult SeeOther(string location)
  {
    Response.Headers.Location = location;

    return StatusCode(StatusCodes.Status303SeeOther);
  }
}
