using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.UseCases.Casework.ConfirmClaim;
using MicroserviceRgpd.UseCases.Casework.DeclareStep;
using MicroserviceRgpd.UseCases.Casework.ReadCase;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MicroserviceRgpd.Web.Pages.Casework;

/// <summary>
/// L'écran du dossier — <b>le seul endroit où l'<c>Operator</c> agit</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Tous les gestes ont lieu ici, et aucune API ne les porte.</b> Une route publique qui
/// instruirait un dossier permettrait à quelqu'un de refaire chez lui l'écran « ✅ demande
/// traitée », et l'incomplétude cesserait d'être visible par construction.
/// </para>
/// <para>
/// <b>Le bandeau nomme la plus ancienne déclaration du <c>Manifest</c> parmi les systèmes de ce
/// dossier.</b> C'est au moment où quelqu'un signe qu'une déclaration vieille doit lui être
/// rappelée : le catalogue vieillit exprès, et un écran qui ne le dirait pas laisserait signer sur la
/// foi d'un recensement dont personne ne se souvient de l'âge.
/// </para>
/// <para>
/// <b>Aucun total, aucun taux, aucun dénominateur.</b> Ni « 2 systèmes sur 6 », ni « 33 % » : un
/// dénominateur qui décrit le paysage du client donnerait à une déclaration faussable en silence
/// l'autorité d'un recensement.
/// </para>
/// </remarks>
public class CaseModel(IMediator mediator) : PageModel
{
  /// <summary>Le préfixe de liaison du formulaire, cité tel quel lorsqu'un champ est refusé.</summary>
  public const string FormPrefix = nameof(Form);

  /// <summary>Le préfixe de liaison de la confirmation, cité tel quel lorsqu'un champ est refusé.</summary>
  public const string ConfirmationPrefix = nameof(Confirmation);

  /// <summary>Ce que l'humain saisit pour déclarer où en est un travail dû.</summary>
  [BindProperty]
  public CaseForm Form { get; set; } = new();

  /// <summary>
  /// Ce que l'humain saisit pour <b>reprendre à son compte</b> un droit qu'une <c>Qualification</c>
  /// avait proposé.
  /// </summary>
  /// <remarks>
  /// <b>Un formulaire à part, et non un champ de plus sur le premier.</b> Les deux gestes ne portent
  /// pas la même chose : l'un déclare où en est un travail dû, l'autre dit d'où vient un droit. Les
  /// mêler aurait fait signer d'un clic une reconnaissance qu'on n'avait pas voulue.
  /// </remarks>
  [BindProperty]
  public ConfirmationForm Confirmation { get; set; } = new();

  /// <summary>Le dossier tel qu'il se lit à cet instant.</summary>
  public CaseOnScreen? OnScreen { get; private set; }

  public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
  {
    await LoadAsync(id, cancellationToken);

    // Une adresse qui ne désigne aucun dossier n'est pas un écran vide : elle n'existe pas.
    return OnScreen is null ? NotFound() : Page();
  }

  public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken cancellationToken)
  {
    var declared = Form.Read(ModelState, FormPrefix);

    if (declared is not null)
    {
      var written = await mediator.Send(
        new DeclareStepCommand(
          CaseId.From(id),
          declared.Right,
          declared.DeclaredSystem,
          declared.State,
          declared.Finding,
          declared.SignedBy),
        cancellationToken);

      if (written.Status == ResultStatus.NotFound)
      {
        return NotFound();
      }

      if (written.IsSuccess)
      {
        // Une redirection après l'écriture : recharger la page ne redéclare rien, et le formulaire
        // repart vide plutôt que de garder le nom du signataire précédent sous les yeux du suivant.
        return RedirectToPage(new { id });
      }

      FormBoundary.Deposit(ModelState, FormPrefix, written.ValidationErrors);
    }

    await LoadAsync(id, cancellationToken);

    return OnScreen is null ? NotFound() : Page();
  }

  /// <summary>
  /// Un <c>Operator</c> reprend à son compte un droit qu'une <c>Qualification</c> avait proposé.
  /// </summary>
  /// <remarks>
  /// <b>Le geste a lieu dans le dossier ouvert</b>, pendant que le délai court : il n'existe aucun
  /// vestibule où une demande attendrait sa confirmation avant d'entrer.
  /// </remarks>
  public async Task<IActionResult> OnPostConfirmAsync(Guid id, CancellationToken cancellationToken)
  {
    // Le vide entre comme vide plutôt que comme un nul : un formulaire dont le droit n'a pas été
    // envoyé est un formulaire forgé, qu'on refuse en le nommant.
    if (!DataSubjectRight.TryFromName(Confirmation.Right ?? string.Empty, out var right))
    {
      ModelState.AddModelError(
        $"{ConfirmationPrefix}.{nameof(ConfirmationForm.Right)}",
        $"« {Confirmation.Right} » n'est pas un droit de la taxonomie.");
    }
    else
    {
      var confirmed = await mediator.Send(
        new ConfirmClaimCommand(CaseId.From(id), right, Confirmation.SignedBy),
        cancellationToken);

      if (confirmed.Status == ResultStatus.NotFound)
      {
        return NotFound();
      }

      if (confirmed.IsSuccess)
      {
        // Une redirection après l'écriture : recharger la page ne reconfirme rien, et le formulaire
        // repart vide plutôt que de garder le nom du signataire précédent sous les yeux du suivant.
        return RedirectToPage(new { id });
      }

      FormBoundary.Deposit(ModelState, ConfirmationPrefix, confirmed.ValidationErrors);
    }

    await LoadAsync(id, cancellationToken);

    return OnScreen is null ? NotFound() : Page();
  }

  private async Task LoadAsync(Guid id, CancellationToken cancellationToken)
  {
    // Une adresse peut porter un GUID que le domaine refuse — le GUID vide, par exemple, qui ne
    // désigne aucun dossier. C'est une adresse qui n'existe pas, et non une panne du service : le
    // refus du type se traduit en « rien ici », comme pour un identifiant qui n'aurait jamais été
    // engendré.
    if (!CaseId.TryFrom(id, out var opened))
    {
      return;
    }

    OnScreen = await mediator.Send(new ReadCaseQuery(opened), cancellationToken);
  }
}
