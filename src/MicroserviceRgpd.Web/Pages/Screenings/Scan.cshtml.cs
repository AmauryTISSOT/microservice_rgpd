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
public class ScanModel(ScansInFlight inFlight) : PageModel
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
    // ⚠️ La contrainte de route ne dit que « c'est un GUID », et le GUID vide en est un. Le bâtir
    // sans précaution lèverait sur une adresse tapée à la main ou tronquée, et l'Operator recevrait
    // un 500 nu là où cet écran a une phrase à lui dire.
    var progress = Core.Screenings.ScanId.TryFrom(ScanId, out var scanId)
      ? inFlight.Find(scanId)
      : null;

    if (progress is null)
    {
      // Le processus a redémarré, ou un autre scan a pris la place. L'écran le DIT plutôt que de
      // rediriger en silence vers un rapport qui n'est peut-être pas celui qu'on attendait.
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

    return Page();
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
