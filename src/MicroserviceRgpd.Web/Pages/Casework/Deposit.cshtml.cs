using MicroserviceRgpd.Core.Casework.Ledger;
using MicroserviceRgpd.UseCases.Casework.OpenCase;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MicroserviceRgpd.Web.Pages.Casework;

/// <summary>
/// L'écran du <b>dépôt manuel</b> : une demande arrivée par courriel entre dans le service plutôt
/// que de rester dans une boîte aux lettres.
/// </summary>
/// <remarks>
/// <para>
/// <b>Le mois de l'art. 12.3 court déjà quand cet écran s'ouvre.</b> C'est toute sa raison d'être :
/// la demande a été reçue avant, et tant qu'elle n'est pas ici le service ne peut rien prouver. Le
/// dépôt n'est donc jamais barré — aucun champ obligatoire hors la signature, aucune validation qui
/// renverrait l'<c>Operator</c> à sa boîte aux lettres.
/// </para>
/// <para>
/// <b>Le dossier ouvert a la même forme que celui du canal API</b>, sac de <c>Designations</c>
/// compris : deux formes voudraient dire deux chemins de recherche à maintenir, et l'un des deux
/// finirait par ne plus être celui qu'on croit. Ce qui diffère est ce que ce canal <b>sait de
/// plus</b> — la date de réception réelle, l'identité déclarée, la motivation — et non la forme de ce
/// qu'il produit.
/// </para>
/// <para>
/// ⚠️ <b>Aucun emplacement pour une pièce jointe.</b> Voir <see cref="DepositForm"/>, où l'absence
/// est une propriété du type.
/// </para>
/// </remarks>
public class DepositModel(IMediator mediator, TimeProvider clock) : PageModel
{
  /// <summary>Le préfixe de liaison du formulaire, cité tel quel lorsqu'un champ est refusé.</summary>
  public const string FormPrefix = nameof(Form);

  /// <summary>
  /// Le nombre de lignes de désignation offertes d'avance. Aucune n'est obligatoire, et les vides
  /// sont ignorées : c'est une commodité de saisie, jamais une exigence.
  /// </summary>
  public const int DesignationLines = 4;

  /// <summary>Ce que l'<c>Operator</c> transcrit du courriel qu'il a sous les yeux.</summary>
  [BindProperty]
  public DepositForm Form { get; set; } = new();

  public void OnGet()
  {
    // Rien à charger : l'écran ne relit aucun dossier, il en fait naître un.
  }

  public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
  {
    // L'instant du dépôt est lu une seule fois et sert deux fois : à borner la date de réception
    // déclarable, et à fonder le défaut. Le relire ferait dépendre l'un de l'autre d'un battement
    // d'horloge.
    var depositedAt = clock.GetUtcNow();

    var deposited = Form.Read(ModelState, FormPrefix, depositedAt);

    if (deposited is null)
    {
      return Page();
    }

    // La signature est forgée ici, où le régime se connaît : la surface n'authentifie personne, et
    // c'est ce que la preuve doit garder pour ne pas être relue comme une identification.
    Signatory signatory;

    try
    {
      signatory = Signatory.Operator(deposited.SignedBy, SignatureRegime.Unauthenticated);
    }
    catch (ArgumentException refusal)
    {
      ModelState.AddModelError(
        $"{FormPrefix}.{nameof(DepositForm.SignedBy)}",
        FormBoundary.Named(refusal));

      return Page();
    }

    var opened = await mediator.Send(
      new OpenCaseCommand(
        deposited.IdentityDeclaration,
        deposited.Motivation,
        deposited.Designations,
        deposited.Rights,
        deposited.Origin,
        deposited.Reception,
        signatory),
      cancellationToken);

    if (!opened.IsSuccess)
    {
      FormBoundary.Deposit(ModelState, FormPrefix, opened.ValidationErrors);

      return Page();
    }

    // Le dépôt mène au dossier qu'il vient d'ouvrir, et non à la file : c'est là que l'exigence non
    // satisfaite se lit — une motivation réclamée, un droit à confirmer — et l'y conduire est ce qui
    // rend la faiblesse visible plutôt que contournée. La redirection fait aussi qu'un rechargement
    // ne dépose pas deux fois.
    return RedirectToPage("Case", new { id = opened.Value.Id.Value });
  }
}
