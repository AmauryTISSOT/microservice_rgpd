using MicroserviceRgpd.UseCases.Screenings.DepositListing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MicroserviceRgpd.Web.Pages.Screenings;

/// <summary>
/// L'écran du <b>dépôt d'un relevé</b> : l'<c>Operator</c> y prend la requête de son SGBD, colle ce
/// qu'elle rend, et obtient son rapport <b>d'un seul geste</b>.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Ce n'est pas une route publique, et il n'en existe aucune pour ce contexte.</b> Cet écran
/// appelle MediatR directement. Une API qui déposerait un relevé laisserait quelqu'un refaire chez
/// lui l'écran « ✅ base analysée », où le dépistage se présente comme un recensement complet.
/// </para>
/// <para>
/// <b>Le geste est synchrone, sans exception</b> : le moteur rend toutes les colonnes, le rapport
/// est écrit, puis on redirige vers lui. Rien ne part en arrière-plan — un dépistage interrompu
/// rendrait un rapport <b>vide et rassurant</b>, et l'<c>Operator</c> sonderait un état plutôt que
/// de lire un résultat.
/// </para>
/// <para>
/// ⚠️ <b>Le plafond d'octets est relevé sur ce geste seul.</b> Kestrel plafonne le service à 64 Kio,
/// ce qui vaut ~335 colonnes : le refus muet du transport serait sorti <b>avant</b> le refus lisible
/// du format, et un relevé tronqué n'aurait jamais pu être nommé comme tel. 8 Mo est deux fois le
/// pire cas autorisé — 20 000 colonnes pèsent ~3,8 Mo —, si bien que le refus qui sort est toujours
/// celui du contrat de format. Le plafond global reste en place pour les deux routes publiques
/// qu'il protège.
/// </para>
/// <para>
/// <b>Le mot est <em>dépistage</em></b> — jamais <em>recensement</em>, <em>cartographie</em> ni
/// <em>scan</em> — dans tout ce que cet écran dit.
/// </para>
/// </remarks>
[RequestSizeLimit(DepositModel.PasteCeilingInBytes)]
[RequestFormLimits(
  ValueLengthLimit = int.MaxValue,
  MultipartBodyLengthLimit = DepositModel.PasteCeilingInBytes)]
public class DepositModel(IMediator mediator) : PageModel
{
  /// <summary>
  /// Le plafond d'octets de ce seul geste, en octets. ⚠️ <b>Il vaut deux fois le pire cas
  /// autorisé</b> : c'est ce qui garantit qu'un relevé au-delà du plafond de colonnes se fasse
  /// refuser <b>par son compte déclaré</b>, lisiblement, plutôt que par le transport, muettement.
  /// </summary>
  public const long PasteCeilingInBytes = 8L * 1024 * 1024;

  /// <summary>Le relevé collé, tel quel. Le service ne le découpe ni ne le complète.</summary>
  [BindProperty]
  public string? Paste { get; set; }

  /// <summary>Le SGBD dont l'<c>Operator</c> veut la requête. Il ne décide que du texte affiché.</summary>
  [BindProperty(SupportsGet = true)]
  public string? Dialect { get; set; }

  /// <summary>La requête que l'écran met sous les yeux de l'<c>Operator</c>.</summary>
  public ListingQuery Query => ListingQuery.For(Dialect);

  public void OnGet()
  {
    // Rien à charger : l'écran ne relit aucun rapport, il en fait naître un.
  }

  public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
  {
    var deposited = await mediator.Send(new DepositListingCommand(Paste), cancellationToken);

    if (!deposited.IsSuccess)
    {
      // Les neuf refus viennent du domaine, où le contrat de format est écrit — l'écran les
      // redit, il n'en rédige aucun. Deux rédactions pour une même règle finiraient par ne plus
      // dire la même chose, et l'écran mentirait sur ce que le service accepte.
      // ⚠️ La clé est celle du champ lié, SANS préfixe. Le refus porte sur « Paste », qui est le
      // nom du textarea : une clé préfixée aurait désigné un champ inexistant, et le message ne
      // s'affichait que parce que l'écran énumère tout le ModelState. Le premier
      // asp-validation-for posé sur le champ n'aurait alors rien montré, en silence.
      foreach (var refusal in deposited.ValidationErrors)
      {
        ModelState.AddModelError(refusal.Identifier, refusal.ErrorMessage);
      }

      return Page();
    }

    // Le dépôt mène au rapport qu'il vient de produire. La redirection fait aussi qu'un
    // rechargement ne dépiste pas deux fois — et un second dépistage coûte du travail humain, les
    // arbitrages du précédent n'étant jamais repris.
    return RedirectToPage("Report");
  }
}
