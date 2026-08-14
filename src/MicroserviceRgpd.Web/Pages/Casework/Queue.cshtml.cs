using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.UseCases.Casework.DestroyEvidenceLog;
using MicroserviceRgpd.UseCases.Casework.ReadQueue;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MicroserviceRgpd.Web.Pages.Casework;

/// <summary>
/// La file : les dossiers ouverts, rangés par échéance, et — <b>à part</b> — les <c>EvidenceLog</c> dont
/// la conservation est échue. <b>C'est une requête, jamais un processus.</b>
/// </summary>
/// <remarks>
/// <para>
/// <b>Rien ne tourne derrière cet écran.</b> Aucun <c>IHostedService</c>, aucun
/// <c>BackgroundService</c>, aucun <c>cron</c>, aucune minuterie, aucun drapeau persisté
/// d'échéance : tout se recalcule à l'instant où l'<c>Operator</c> regarde. Un processus de fond
/// interrompu rendrait une file <b>vide et rassurante</b>, soit l'<c>Omission silencieuse</c> sous sa
/// forme la plus dangereuse. <b>C'est vrai de la destruction des <c>EvidenceLog</c> échus aussi</b> :
/// elle n'a lieu que sous le clic d'un humain.
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
/// ⚠️ <b>Elle n'offre aucun geste SUR UN DOSSIER.</b> Chaque ligne de dossier mène au dossier, et
/// c'est là que l'<c>Operator</c> agit : une action depuis la liste ferait signer quelqu'un sans
/// qu'il ait ouvert ce qu'il signe. Le <b>seul</b> geste de cet écran est la destruction d'un
/// <c>EvidenceLog</c> échu, qui n'a aucun dossier où vivre — le sien est clos depuis cinq ans —, et son
/// bouton est tenu <b>loin</b> des lignes de dossiers, dans une section qui lui est propre.
/// </para>
/// </remarks>
public class QueueModel(IMediator mediator) : PageModel
{
  /// <summary>Le préfixe de liaison de la destruction, cité tel quel lorsqu'un champ est refusé.</summary>
  public const string DestructionPrefix = nameof(Destruction);

  /// <summary>La file telle qu'elle se lit à cet instant.</summary>
  public OperatorQueue Queue { get; private set; } = new([], [], DateTimeOffset.MinValue);

  /// <summary>
  /// Ce que l'humain envoie pour <b>détruire un <c>EvidenceLog</c> échu</b> — un geste irréversible et
  /// sans trace.
  /// </summary>
  [BindProperty]
  public EvidenceLogDestructionForm Destruction { get; set; } = new();

  public async Task OnGetAsync(CancellationToken cancellationToken)
  {
    Queue = await mediator.Send(new ReadQueueQuery(), cancellationToken);
  }

  /// <summary>
  /// Détruit un <c>EvidenceLog</c> échu, en entier — et n'écrit rien à la place.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>La case à cocher est la parade, et elle est ici, dans l'écran.</b> C'est le second geste
  /// irréversible du dispositif après la clôture, et le seul qui ne laisse aucune trace de
  /// lui-même : la parade ne peut donc pas être de la donnée gardée quelque part, elle ne peut être
  /// que le clic délibéré de qui vient de lire ce qu'il détruit.
  /// </para>
  /// <para>
  /// <b>Aucun nom n'est demandé</b>, contrairement à tous les autres gestes du dispositif : il
  /// n'existe plus une ligne où l'écrire, le <c>EvidenceLog</c> détruit étant le seul endroit qui aurait
  /// pu le porter. Réclamer une signature pour ne l'écrire nulle part aurait été la façade d'une
  /// preuve.
  /// </para>
  /// <para>
  /// <b>Un refus du domaine ne s'affiche pas.</b> Une preuve encore due, un dossier inconnu, un
  /// <c>EvidenceLog</c> qu'un autre écran vient d'emporter : aucun de ces cas n'est une panne, et la file
  /// rechargée dit d'elle-même ce qui reste. C'est la seule chose vraie qu'on puisse afficher d'un
  /// geste qui ne se consigne pas.
  /// </para>
  /// </remarks>
  public async Task<IActionResult> OnPostDestroyEvidenceLogAsync(CancellationToken cancellationToken)
  {
    // Une adresse qui ne désigne aucun dossier n'est pas un formulaire mal rempli : c'est un envoi
    // forgé, et le domaine refuse le GUID vide comme il refuse le reste.
    if (!CaseId.TryFrom(Destruction.Case, out var ledgerOf))
    {
      return NotFound();
    }

    if (!Destruction.Confirmed)
    {
      ModelState.AddModelError(
        $"{DestructionPrefix}.{nameof(EvidenceLogDestructionForm.Confirmed)}",
        "La destruction emporte toute la preuve de ce dossier, ne se défait pas, et ne laisse "
        + "aucune trace d'elle-même. Cochez la case pour confirmer que c'est bien ce que vous "
        + "voulez faire.");

      await OnGetAsync(cancellationToken);

      return Page();
    }

    await mediator.Send(new DestroyEvidenceLogCommand(ledgerOf), cancellationToken);

    // Une redirection après l'écriture : recharger la page ne redétruit rien — et la file qui
    // revient est celle d'après la destruction, seule à pouvoir dire ce qui reste.
    return RedirectToPage();
  }
}

/// <summary>
/// Ce qu'un <c>Operator</c> envoie pour détruire un <c>EvidenceLog</c> échu : le dossier dont c'est la
/// preuve, et une confirmation délibérée.
/// </summary>
/// <remarks>
/// <b>Aucun champ de signature, et ce n'est pas un oubli.</b> Le geste ne laisse aucune trace de
/// lui-même : il n'existe plus une ligne où un nom pourrait s'écrire.
/// </remarks>
public sealed class EvidenceLogDestructionForm
{
  /// <summary>Le dossier dont la preuve est détruite. Il n'a plus une désignation depuis cinq ans.</summary>
  public Guid Case { get; set; }

  /// <summary>
  /// La <b>confirmation délibérée</b> du geste irréversible. Sans elle, rien n'est détruit.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Elle est éprouvée dans l'écran, jamais dans le domaine</b>, comme celle de la clôture :
  /// ce qu'elle protège est un humain contre son propre clic, et c'est une propriété de la surface.
  /// </remarks>
  public bool Confirmed { get; set; }
}
