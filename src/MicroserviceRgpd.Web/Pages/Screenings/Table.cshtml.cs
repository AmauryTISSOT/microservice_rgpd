using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.UseCases.Screenings.ReadScreeningTable;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MicroserviceRgpd.Web.Pages.Screenings;

/// <summary>
/// L'écran d'<b>une table</b> du dépistage courant : toutes ses colonnes, dans l'ordre du relevé, et
/// la <c>Clause d'incomplétude</c> qui accompagne obligatoirement la réponse.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>La table est l'unité de travail, et cet écran est cette décision rendue.</b> Le commentaire
/// de table éclaire toutes ses colonnes, et le voisinage de <c>adr_l1</c>, <c>adr_l2</c>, <c>cp</c>,
/// <c>ville</c> ne se lit pas colonne isolée.
/// </para>
/// <para>
/// ⚠️ <b>Aucun filtre, et pas de bouton pour en poser un.</b> Les <c>Unflagged</c> <b>sont</b>
/// l'écran — 92,5 % du contenu réel d'un relevé — et un écran qui ne rendrait que les signalées
/// serait un écran où l'omission a cessé d'être relisible.
/// </para>
/// <para>
/// ⚠️ <b>Le schéma et la table passent en paramètres de requête, jamais dans le chemin.</b> Un nom
/// d'objet peut porter un point ou une barre oblique, que la base rend tels quels : les coudre dans
/// une adresse aurait fait dépendre la lecture d'une table de la façon dont son nom se découpe.
/// </para>
/// </remarks>
public class TableModel(IMediator mediator) : PageModel
{
  /// <summary>Le schéma dont le relevé dit que la table vient.</summary>
  [BindProperty(SupportsGet = true)]
  public string? Schema { get; set; }

  /// <summary>La table, telle que le relevé la nomme.</summary>
  [BindProperty(SupportsGet = true)]
  public string? Table { get; set; }

  /// <summary>La table lue, et la clause qui l'accompagne obligatoirement.</summary>
  public ScreeningAnswer<ScreenedTable>? Answer { get; private set; }

  public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
  {
    // ⚠️ Une adresse sans ses deux membres ne désigne aucune table : on renvoie au rapport, qui les
    // nomme toutes. Forger une identité sur un membre vide aurait levé au fond d'un domaine dont la
    // règle est qu'un triplet mal formé est une programmation fautive, jamais une saisie.
    if (string.IsNullOrWhiteSpace(Schema) || string.IsNullOrWhiteSpace(Table))
    {
      return RedirectToPage("Report");
    }

    Answer = await mediator.Send(
      new ReadScreeningTableQuery(new TableIdentity(Schema, Table)), cancellationToken);

    // Aucun dépistage courant, ou un courant qui ne porte pas cette table : le rapport la nommerait
    // s'il l'avait. Une table vide portant la clause aurait fait passer une adresse mal recopiée
    // pour une table réellement dépourvue de colonnes.
    return Answer is null ? RedirectToPage("Report") : Page();
  }
}
