using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.UseCases.Screenings.ReadArchivedScreeningTable;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MicroserviceRgpd.Web.Pages.Screenings;

/// <summary>
/// L'écran d'<b>une table d'un dépistage archivé</b> : toutes ses colonnes, dans l'ordre du relevé,
/// et ce qu'un humain en avait tranché — <b>sans un seul bouton</b>.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Aucun filtre, ici non plus.</b> Un archivé se lit <b>en entier</b> : les
/// <c>Unflagged</c> <b>sont</b> l'écran, et un rapport ancien dont on ne rendrait que les signalées
/// serait un rapport dont personne ne peut plus vérifier ce que le dépistage n'avait pas vu.
/// </para>
/// <para>
/// ⚠️ <b>Aucun formulaire du tout</b>, et c'est ce qui distingue cet écran de son jumeau : pas
/// d'arbitrage à l'unité, pas de geste de lot, pas de suppression — celle-ci n'a qu'un écran, et ce
/// n'est pas celui-ci.
/// </para>
/// <para>
/// <b>Le schéma, la table et le rapport passent en paramètres de requête, jamais dans le chemin</b> :
/// un nom d'objet peut porter un point ou une barre oblique, que la base rend tels quels.
/// </para>
/// </remarks>
public class ArchivedTableModel(IMediator mediator) : PageModel
{
  /// <summary>Le rapport archivé dont on ouvre une table.</summary>
  [BindProperty(SupportsGet = true)]
  public string? Screening { get; set; }

  /// <summary>Le schéma dont le relevé disait que la table venait.</summary>
  [BindProperty(SupportsGet = true)]
  public string? Schema { get; set; }

  /// <summary>La table, telle que le relevé la nommait.</summary>
  [BindProperty(SupportsGet = true)]
  public string? Table { get; set; }

  /// <summary>La table lue, et la clause qui l'accompagne obligatoirement.</summary>
  public ScreeningAnswer<ArchivedTable>? Answer { get; private set; }

  public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
  {
    if (!Guid.TryParse(Screening, out var named) || named == Guid.Empty)
    {
      return RedirectToPage("History");
    }

    // Une adresse sans ses deux membres ne désigne aucune table : on renvoie au rapport archivé, qui
    // les nomme toutes. Forger une identité sur un membre vide aurait levé au fond d'un domaine dont
    // la règle est qu'un triplet mal formé est une programmation fautive, jamais une saisie.
    if (string.IsNullOrWhiteSpace(Schema) || string.IsNullOrWhiteSpace(Table))
    {
      return RedirectToPage("Archive", new { Screening });
    }

    Answer = await mediator.Send(
      new ReadArchivedScreeningTableQuery(
        ScreeningId.From(named), new TableIdentity(Schema, Table)),
      cancellationToken);

    // Le rapport a été supprimé, il est redevenu le courant, ou il ne portait pas cette table. Le
    // sommaire de l'archivé la nommerait s'il l'avait ; et s'il n'existe plus, c'est lui qui renvoie
    // à l'historique.
    return Answer is null ? RedirectToPage("Archive", new { Screening }) : Page();
  }
}
