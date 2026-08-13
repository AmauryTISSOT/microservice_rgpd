using MicroserviceRgpd.UseCases.Screenings.ReadScreeningHistory;

namespace MicroserviceRgpd.Web.Pages.Screenings;

/// <summary>
/// Ce dont le formulaire de suppression a besoin : le rapport qu'il vise, et de quoi le distinguer
/// des autres formulaires du même écran.
/// </summary>
/// <remarks>
/// ⚠️ <b>Il existe pour que le formulaire soit écrit une seule fois</b> — le courant et chaque
/// archivé le portent — et un formulaire irréversible recopié en deux endroits est un formulaire
/// dont l'une des deux copies perdra un jour sa confirmation.
/// </remarks>
/// <param name="Screening">Le rapport visé, tel que l'historique le nomme.</param>
/// <param name="Rank">
/// Le rang du formulaire dans l'écran. ⚠️ <b>Il coud les identifiants HTML, et non le nom de la
/// base</b> : un nom d'objet est recopié verbatim du SGBD, et une espace ou un point y casse en
/// silence l'association d'un label à son champ.
/// </param>
/// <param name="IsCurrent">
/// Ce rapport est-il le courant ? Le formulaire le dit, parce que le supprimer <b>rend son rang au
/// précédent</b> — ce n'est pas la même conséquence que de supprimer un archivé.
/// </param>
public sealed record DeleteScreeningForm(ScreeningHeading Screening, int Rank, bool IsCurrent);
