using MicroserviceRgpd.UseCases.Screenings.ReadScreeningHistory;

namespace MicroserviceRgpd.Web.Pages.Screenings;

/// <summary>
/// Ce dont le formulaire de suppression a besoin : le rapport qu'il vise, et ce que sa suppression
/// fait au rang du courant.
/// </summary>
/// <remarks>
/// ⚠️ <b>L'écran n'en porte qu'un</b>, celui du rapport ouvert dans le détail. Supprimer un autre
/// rapport, c'est d'abord l'ouvrir — et donc lire ce qu'on va supprimer.
/// </remarks>
/// <param name="Screening">Le rapport visé, tel que l'historique le nomme.</param>
/// <param name="IsCurrent">
/// Ce rapport est-il le courant ? Le formulaire le dit, parce que le supprimer <b>rend son rang au
/// précédent</b> — ce n'est pas la même conséquence que de supprimer un archivé.
/// </param>
public sealed record DeleteScreeningForm(ScreeningHeading Screening, bool IsCurrent);
