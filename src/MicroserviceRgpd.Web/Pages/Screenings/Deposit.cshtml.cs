using MicroserviceRgpd.UseCases.Screenings.DepositListing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MicroserviceRgpd.Web.Pages.Screenings;

/// <summary>
/// L'écran du <b>dépôt d'un relevé</b> : l'<c>Operator</c> y prend la requête de son SGBD, colle ce
/// qu'elle rend, et obtient son rapport de détection <b>d'un seul geste</b>.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Ce n'est pas une route publique, et il n'en existe aucune pour ce contexte.</b> Cet écran
/// appelle MediatR directement. Une API qui déposerait un relevé laisserait quelqu'un refaire chez
/// lui l'écran « ✅ base analysée », où la détection se présente comme un recensement complet.
/// </para>
/// <para>
/// <b>Le geste est synchrone, sans exception</b> : le moteur rend toutes les colonnes, le rapport
/// de détection est écrit, puis on redirige vers lui. Rien ne part en arrière-plan — une détection
/// interrompue rendrait un rapport de détection <b>vide et rassurant</b>, et l'<c>Operator</c>
/// sonderait un état plutôt que de lire un résultat.
/// </para>
/// <para>
/// ⚠️ <b>Le plafond d'octets est relevé sur ce geste seul.</b> Kestrel plafonne le service à 64 Kio,
/// ce qui vaut ~335 colonnes : le refus muet du transport serait sorti <b>avant</b> le refus lisible
/// du format, et un relevé tronqué n'aurait jamais pu être nommé comme tel. Le plafond global reste
/// en place pour les deux routes publiques qu'il protège.
/// </para>
/// <para>
/// ⚠️ <b>Le plafond du transport est posé au-dessus de celui du geste, et l'écart n'est pas du
/// confort.</b> C'est lui qui rend le refus de poids <b>lisible</b> : posés au même niveau, les deux
/// plafonds se déclencheraient au même octet, et celui qui sortirait serait le <c>413</c> nu du
/// transport — sans phrase, sans écran, sans « aucune colonne n'a été ingérée ». Deux raisons
/// imposent l'écart plutôt qu'une marge symbolique : le collage arrive <b>encodé en formulaire</b>,
/// où les accolades et les guillemets du pivot pèsent trois octets chacun (mesuré à ~1,45× sur une
/// ligne réelle) ; et un collage qui franchit le plafond du geste doit malgré tout <b>arriver</b>
/// pour se faire refuser en français.
/// </para>
/// <para>
/// <b>Le mot est <em>détection des données personnelles</em>, et ce qu'elle rend est un
/// <em>rapport de détection</em></b> — jamais <em>recensement</em>, <em>cartographie</em> ni
/// <em>scan</em> — dans tout ce que cet écran dit. ⚠️ L'interdit porte sur ce qui <b>nomme</b> :
/// « recensement » est banni comme nom de la chose, et le verbe <em>recenser</em> reste licite dans
/// la prose — c'est lui qui dit que l'<c>Operator</c> recense ses systèmes, et le service pas.
/// </para>
/// </remarks>
[RequestSizeLimit(DepositModel.TransportCeilingInBytes)]
[RequestFormLimits(
  ValueLengthLimit = int.MaxValue,
  MultipartBodyLengthLimit = DepositModel.TransportCeilingInBytes)]
public class DepositModel(IMediator mediator) : PageModel
{
  /// <summary>
  /// Ce que le transport laisse entrer sur ce seul geste, en octets — <b>le double du plafond du
  /// geste</b>, qui est celui qui refuse en français.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Ce n'est pas le plafond annoncé à l'<c>Operator</c></b>, et il ne doit jamais le devenir :
  /// celui-là est <see cref="DepositListingCommand.MaxPasteBytes"/>, il vaut 8 Mo, et c'est lui qui
  /// porte une phrase. Celui-ci n'existe que pour que le collage <b>arrive</b> — encodé en
  /// formulaire, où le pivot enfle d'environ moitié — jusqu'au geste qui saura le refuser lisiblement.
  /// </remarks>
  public const long TransportCeilingInBytes = 2 * DepositListingCommand.MaxPasteBytes;

  /// <summary>
  /// Le plafond du geste en mégaoctets, tel que l'écran l'annonce — <b>dérivé de la constante du
  /// geste, jamais réécrit</b> : un « 8 Mo » recopié dans le HTML aurait continué de s'afficher le
  /// jour où le plafond bouge, et l'écran aurait promis autre chose que ce que le service accepte.
  /// </summary>
  public static long CeilingInMegabytes => DepositListingCommand.MaxPasteBytes / 1024 / 1024;

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

    // Le dépôt mène au rapport de détection qu'il vient de produire. La redirection fait aussi qu'un
    // rechargement ne détecte pas deux fois — et un second rapport de détection coûte du travail
    // humain, les arbitrages du précédent n'étant jamais repris.
    return RedirectToPage("Report");
  }
}
