using MicroserviceRgpd.UseCases.Casework.ReadQueue;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MicroserviceRgpd.Web.Pages.Casework;

/// <summary>
/// La file : les dossiers ouverts, rangés par échéance. <b>C'est une requête, jamais un
/// processus.</b>
/// </summary>
/// <remarks>
/// <para>
/// <b>Rien ne tourne derrière cet écran.</b> Aucun <c>IHostedService</c>, aucun
/// <c>BackgroundService</c>, aucun <c>cron</c>, aucune minuterie, aucun drapeau persisté
/// d'échéance : tout se recalcule à l'instant où l'<c>Operator</c> regarde. Un processus de fond
/// interrompu rendrait une file <b>vide et rassurante</b>, soit l'<c>Omission silencieuse</c> sous sa
/// forme la plus dangereuse.
/// </para>
/// <para>
/// <b>Ce n'est pas un tableau de bord, et il n'y a aucun nombre à regarder.</b> Ni total, ni taux, ni
/// « 2 dossiers en retard » : la règle des chiffres n'autorise que le dénombrement d'une chose
/// présente que le service détient lui-même, et le seul usage d'un total serait de se rassurer sans
/// lire les lignes. <c>0 dossier en retard</c> est impossible à produire ici.
/// </para>
/// <para>
/// <b>La file n'émet aucun appel.</b> La relance d'un <c>202</c> a lieu à l'ouverture d'un dossier ;
/// afficher une liste n'appelle pas un <c>Adapter</c> par ligne.
/// </para>
/// <para>
/// <b>Elle n'offre aucun geste.</b> Chaque ligne mène au dossier, et c'est là que l'<c>Operator</c>
/// agit : une action depuis la liste ferait signer quelqu'un sans qu'il ait ouvert ce qu'il signe.
/// </para>
/// </remarks>
public class QueueModel(IMediator mediator) : PageModel
{
  /// <summary>La file telle qu'elle se lit à cet instant.</summary>
  public OperatorQueue Queue { get; private set; } = new([], DateTimeOffset.MinValue);

  public async Task OnGetAsync(CancellationToken cancellationToken)
  {
    Queue = await mediator.Send(new ReadQueueQuery(), cancellationToken);
  }
}
