using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.UseCases.Casework.ArbitrateReservation;
using MicroserviceRgpd.UseCases.Casework.ConfirmClaim;
using MicroserviceRgpd.UseCases.Casework.DeclareMotivation;
using MicroserviceRgpd.UseCases.Casework.DeclareStep;
using MicroserviceRgpd.UseCases.Casework.Deliver;
using MicroserviceRgpd.UseCases.Casework.Locate;
using MicroserviceRgpd.UseCases.Casework.Read;
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

  /// <summary>Le préfixe de liaison de la motivation, cité tel quel lorsqu'un champ est refusé.</summary>
  public const string MotivationPrefix = nameof(Motivation);

  /// <summary>Le préfixe de liaison du téléchargement, cité tel quel lorsqu'un champ est refusé.</summary>
  public const string DeliveryPrefix = nameof(Delivery);

  /// <summary>Le préfixe de liaison de la remise, cité tel quel lorsqu'un champ est refusé.</summary>
  public const string HandoverPrefix = nameof(Handover);

  /// <summary>Ce que l'humain saisit pour <b>télécharger</b> la remise d'un droit — le premier geste.</summary>
  [BindProperty]
  public DeliveryForm Delivery { get; set; } = new();

  /// <summary>
  /// Ce que l'humain saisit pour <b>déclarer remis</b> — le second geste, et le seul qui date la
  /// remise et détruise les pièces.
  /// </summary>
  [BindProperty]
  public HandoverForm Handover { get; set; } = new();

  /// <summary>Le préfixe de liaison de l'arbitrage, cité tel quel lorsqu'un champ est refusé.</summary>
  public const string ArbitrationPrefix = nameof(Arbitration);

  /// <summary>
  /// Ce que l'humain saisit pour <b>trancher une réserve</b> de <c>Locate</c>.
  /// </summary>
  /// <remarks>
  /// <b>Un formulaire à part, comme les trois autres.</b> Les gestes de cet écran ne portent pas la
  /// même chose, et les mêler aurait fait signer d'un clic un rattachement qu'on n'avait pas voulu —
  /// erreur irréversible, et portant sur la donnée d'un tiers.
  /// </remarks>
  [BindProperty]
  public ArbitrationForm Arbitration { get; set; } = new();

  /// <summary>
  /// Ce que l'humain saisit pour écrire <b>après coup</b> ce qu'il a pesé de l'identité du demandeur.
  /// </summary>
  [BindProperty]
  public MotivationForm Motivation { get; set; } = new();

  /// <summary>Le dossier tel qu'il se lit à cet instant.</summary>
  public CaseOnScreen? OnScreen { get; private set; }

  /// <summary>
  /// Affiche le dossier — et, <b>avant de l'afficher</b>, va désigner la personne dans les systèmes
  /// qui déclarent un <c>Adapter</c>.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>C'est ici, et nulle part ailleurs, que le service appelle.</b> L'ouverture du dossier est
  /// aussi le seul endroit où se fait la <b>relance</b> d'un <c>202</c> : jamais depuis la file, qui
  /// n'émet aucun appel, et jamais par une minuterie. Rien ne tourne, donc rien ne peut s'arrêter en
  /// silence — un processus de fond interrompu rendrait un écran <b>vide et rassurant</b>.
  /// </para>
  /// <para>
  /// <b>L'ordre des appels est signifiant sans jamais être bloquant.</b> On cherche la personne avant
  /// d'affirmer quoi que ce soit sur elle, mais rien n'attend l'autre : une panne d'<c>Adapter</c>
  /// laisse une ligne « non appelé » à l'écran, et l'<c>Operator</c> instruit le dossier comme avant.
  /// </para>
  /// </remarks>
  public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
  {
    await LocateAsync(id, cancellationToken);

    await ReadAsync(id, cancellationToken);

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
    var right = FormBoundary.ReadVocabulary<DataSubjectRight>(
      ModelState,
      ConfirmationPrefix,
      nameof(ConfirmationForm.Right),
      Confirmation.Right,
      DataSubjectRight.TryFromName,
      "n'est pas un droit de la taxonomie");

    if (right is not null)
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

  /// <summary>
  /// Un <c>Operator</c> écrit <b>après coup</b> ce qu'il a pesé de l'identité du demandeur.
  /// </summary>
  /// <remarks>
  /// <b>Le droit ne bouge pas.</b> Ce qui s'écrit ici dit ce qu'on a fini par peser ; il ne rend pas
  /// rétroactivement propre l'accès ouvert sur la foi de rien, dont l'origine reste figée.
  /// </remarks>
  public async Task<IActionResult> OnPostMotivateAsync(Guid id, CancellationToken cancellationToken)
  {
    var method = FormBoundary.ReadVocabulary<IdentityVerificationMethod>(
      ModelState,
      MotivationPrefix,
      nameof(MotivationForm.VerificationMethod),
      Motivation.VerificationMethod,
      IdentityVerificationMethod.TryFromName,
      "n'est pas une méthode du vocabulaire");

    var motivation = method is null
      ? null
      : FormBoundary.Declared(
        ModelState,
        MotivationPrefix,
        nameof(MotivationForm.Detail),
        () => IdentityMotivation.Of(method, Motivation.Detail));

    if (motivation is not null)
    {
      var written = await mediator.Send(
        new DeclareMotivationCommand(CaseId.From(id), motivation, Motivation.SignedBy),
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

      FormBoundary.Deposit(ModelState, MotivationPrefix, written.ValidationErrors);
    }

    await LoadAsync(id, cancellationToken);

    return OnScreen is null ? NotFound() : Page();
  }

  /// <summary>
  /// Un <c>Operator</c> <b>tranche une réserve</b> : cette ligne est celle de la personne, ou elle ne
  /// l'est pas.
  /// </summary>
  /// <remarks>
  /// <b>Le sac s'enrichit du même geste</b> quand la réserve est rattachée et qu'elle proposait une
  /// désignation neuve. L'appel suivant la portera — à la <b>prochaine ouverture</b> du dossier, que
  /// la redirection ci-dessous provoque : c'est le seul endroit où le service appelle.
  /// </remarks>
  public async Task<IActionResult> OnPostArbitrateAsync(Guid id, CancellationToken cancellationToken)
  {
    var ruling = FormBoundary.ReadVocabulary<ReservationState>(
      ModelState,
      ArbitrationPrefix,
      nameof(ArbitrationForm.Ruling),
      Arbitration.Ruling,
      ReservationState.TryFromName,
      "n'est pas un arbitrage");

    // Le vide entre comme vide plutôt que comme un nul : un formulaire forgé est refusé en le
    // nommant, jamais ignoré.
    var system = FormBoundary.Read(
      ModelState,
      ArbitrationPrefix,
      nameof(ArbitrationForm.DeclaredSystem),
      () => DeclaredSystemId.From(Arbitration.DeclaredSystem ?? string.Empty));

    var reference = FormBoundary.Declared(
      ModelState,
      ArbitrationPrefix,
      nameof(ArbitrationForm.Reference),
      () => OpaqueReference.Of(Arbitration.Reference));

    if (ruling is not null && system is not null && reference is not null)
    {
      var arbitrated = await mediator.Send(
        new ArbitrateReservationCommand(
          CaseId.From(id),
          system.Value,
          reference,
          ruling,
          Arbitration.SignedBy),
        cancellationToken);

      if (arbitrated.Status == ResultStatus.NotFound)
      {
        return NotFound();
      }

      if (arbitrated.IsSuccess)
      {
        // Une redirection après l'écriture : recharger la page ne ré-arbitre rien, et c'est elle qui
        // fait repartir l'appel sous le sac que cet arbitrage vient d'enrichir.
        return RedirectToPage(new { id });
      }

      FormBoundary.Deposit(ModelState, ArbitrationPrefix, arbitrated.ValidationErrors);
    }

    await LoadAsync(id, cancellationToken);

    return OnScreen is null ? NotFound() : Page();
  }

  /// <summary>
  /// <b>Premier geste</b> : l'<c>Operator</c> télécharge la remise d'un droit, pour l'ouvrir et voir
  /// ce qu'elle contient.
  /// </summary>
  /// <remarks>
  /// <b>Aucune redirection, et aucune trace.</b> Ce qui part est un fichier, pas une page ; rien
  /// n'est daté au <c>Ledger</c>, et rien n'est détruit. La preuve attend le second geste.
  /// </remarks>
  public async Task<IActionResult> OnPostTakeAsync(Guid id, CancellationToken cancellationToken)
  {
    var right = FormBoundary.ReadVocabulary<DataSubjectRight>(
      ModelState,
      DeliveryPrefix,
      nameof(DeliveryForm.Right),
      Delivery.Right,
      DataSubjectRight.TryFromName,
      "n'est pas un droit de la taxonomie");

    if (right is not null)
    {
      var taken = await mediator.Send(new TakeDeliveryCommand(CaseId.From(id), right), cancellationToken);

      if (taken.Status == ResultStatus.NotFound)
      {
        return NotFound();
      }

      if (taken.IsSuccess)
      {
        return File(taken.Value.Content, DeliveryPackage.ContentType, taken.Value.FileName);
      }

      FormBoundary.Deposit(ModelState, DeliveryPrefix, taken.ValidationErrors);
    }

    await LoadAsync(id, cancellationToken);

    return OnScreen is null ? NotFound() : Page();
  }

  /// <summary>
  /// <b>Second geste</b> : l'<c>Operator</c> affirme avoir rendu la réponse. Ce clic seul date la
  /// remise au <c>Ledger</c> et détruit les pièces.
  /// </summary>
  /// <remarks>
  /// <b>Le service ne remet rien à personne.</b> Aucun lien à jeton, aucun SMTP : ce qui est
  /// consigné est le constat signé d'un humain, et non un accusé de réception que personne n'a.
  /// </remarks>
  public async Task<IActionResult> OnPostHandoverAsync(Guid id, CancellationToken cancellationToken)
  {
    var right = FormBoundary.ReadVocabulary<DataSubjectRight>(
      ModelState,
      HandoverPrefix,
      nameof(HandoverForm.Right),
      Handover.Right,
      DataSubjectRight.TryFromName,
      "n'est pas un droit de la taxonomie");

    if (right is not null)
    {
      var declared = await mediator.Send(
        new DeclareHandoverCommand(CaseId.From(id), right, Handover.SignedBy),
        cancellationToken);

      if (declared.Status == ResultStatus.NotFound)
      {
        return NotFound();
      }

      if (declared.IsSuccess)
      {
        // Une redirection après l'écriture : recharger la page ne redéclare rien, et le formulaire
        // repart vide plutôt que de garder le nom du signataire précédent sous les yeux du suivant.
        return RedirectToPage(new { id });
      }

      FormBoundary.Deposit(ModelState, HandoverPrefix, declared.ValidationErrors);
    }

    await LoadAsync(id, cancellationToken);

    return OnScreen is null ? NotFound() : Page();
  }

  /// <summary>
  /// Va désigner la personne là où le catalogue déclare un <c>Adapter</c> capable de le faire.
  /// </summary>
  /// <remarks>
  /// <b>Un dossier introuvable n'est pas une panne ici</b> : l'affichage qui suit le dira, et c'est
  /// lui qui rend le <c>404</c>. Cette méthode ne décide de rien de ce que l'écran montre.
  /// </remarks>
  private async Task LocateAsync(Guid id, CancellationToken cancellationToken)
  {
    if (!CaseId.TryFrom(id, out var opened))
    {
      return;
    }

    await mediator.Send(new LocateCommand(opened), cancellationToken);
  }

  /// <summary>
  /// Va lire les données de la personne là où le dossier vient d'en rattacher.
  /// </summary>
  /// <remarks>
  /// <b>Après <c>Locate</c>, et la dépendance est réelle</b> : on ne lit que là où l'on a trouvé, et
  /// c'est l'appel qui précède qui vient de dire où. Rien n'attend pour autant — une panne de l'un
  /// n'empêche pas l'autre, et l'écran s'affiche dans tous les cas.
  /// </remarks>
  private async Task ReadAsync(Guid id, CancellationToken cancellationToken)
  {
    if (!CaseId.TryFrom(id, out var opened))
    {
      return;
    }

    await mediator.Send(new ReadCommand(opened), cancellationToken);
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
