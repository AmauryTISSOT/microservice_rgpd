using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.UseCases.Screenings.DeleteScreening;
using MicroserviceRgpd.UseCases.Screenings.ReadScreeningHistory;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MicroserviceRgpd.Web.Pages.Screenings;

/// <summary>
/// L'<b>historique</b> des dépistages du déploiement — et le <b>seul écran d'où l'on supprime</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>« Archivé » est un calcul refait à chaque affichage.</b> Le rapport le plus récemment lancé
/// est le courant ; tous les autres sont archivés par le seul fait qu'il existe, et rien n'a été
/// écrit pour cela. Supprimer le courant rend son rang au précédent au rendu suivant, sans qu'aucune
/// écriture n'ait lieu.
/// </para>
/// <para>
/// ⚠️ <b>La suppression n'a que cet écran, et c'est délibéré.</b> Elle efface un rapport, toutes ses
/// colonnes et tous leurs arbitrages, sans retour et sans trace : la garder loin des écrans où l'on
/// travaille est ce qui empêche qu'elle se pose entre deux arbitrages, d'un clic de trop dans une
/// table de cinq mille lignes.
/// </para>
/// <para>
/// ⚠️ <b>Aucune échéance ne supprime, et rien ne tourne.</b> Un rapport vit jusqu'à ce qu'un humain
/// pose ce geste-ci. Il n'y a ni purge, ni rétention, ni corbeille.
/// </para>
/// </remarks>
public class HistoryModel(IMediator mediator) : PageModel
{
  /// <summary>
  /// La clé sous laquelle une suppression laisse à cet écran ce qu'elle vient d'emporter.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Elle est écrite et lue par cet écran seul</b>, et la phrase est la moitié utile du geste :
  /// après une suppression, le rapport ne peut plus se décrire lui-même, et une liste plus courte
  /// d'une ligne ne dit pas laquelle est partie.
  /// </remarks>
  internal const string NoticeKey = "DeletionNotice";

  /// <summary>Les rapports archivés, et le courant qu'ils ne sont pas.</summary>
  public ScreeningHistory? History { get; private set; }

  /// <summary>
  /// Le rapport que le bouton cliqué désigne. <b>Il ne vit que sur le POST</b> : l'écran rend
  /// toujours l'historique entier.
  /// </summary>
  [BindProperty]
  public string? Screening { get; set; }

  /// <summary>
  /// Le nom de base retapé. ⚠️ <b>Le service le confronte à ce qu'il détient</b>, jamais à un champ
  /// caché du même formulaire.
  /// </summary>
  [BindProperty]
  public string? ConfirmedDatabase { get; set; }

  /// <summary>Ce qu'une suppression vient d'emporter. ⚠️ <b>La lecture consomme la phrase.</b></summary>
  public string? Notice => TempData[NoticeKey] as string;

  public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
  {
    return await ReadTheHistoryAsync(cancellationToken);
  }

  /// <summary>
  /// Supprime le rapport nommé — <b>rapport, colonnes et arbitrages</b> — puis revient à
  /// l'historique en disant ce qui est parti.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Le succès redirige, il ne rend pas la page.</b> Un rechargement rejouerait sinon la
  /// suppression : elle serait sans effet — le rapport n'existe plus — mais le navigateur
  /// redemanderait d'envoyer le formulaire, sur le seul geste de cette surface qu'on ne veut jamais
  /// voir proposé deux fois.
  /// </para>
  /// <para>
  /// ⚠️ <b>Le refus de confirmation vient du geste</b>, où la règle est écrite : l'écran le redit et
  /// n'en rédige aucun.
  /// </para>
  /// </remarks>
  public async Task<IActionResult> OnPostDeleteAsync(CancellationToken cancellationToken)
  {
    if (!Guid.TryParse(Screening, out var named) || named == Guid.Empty)
    {
      // Aucun bouton de cet écran ne poste cela : le formulaire n'est pas celui-ci. On ne rédige pas
      // un refus en français pour un formulaire forgé, mais le silence est exclu — la réponse du
      // succès est une redirection, et deux redirections identiques ne se distinguent pas.
      TempData[NoticeKey] = NothingWasDeleted;

      return RedirectToPage();
    }

    var deleted = await mediator.Send(
      new DeleteScreeningCommand(ScreeningId.From(named), ConfirmedDatabase), cancellationToken);

    if (deleted.Status == ResultStatus.NotFound)
    {
      // Deux écrans ouverts sur le même déploiement, et l'autre a supprimé celui-ci le premier.
      TempData[NoticeKey] =
        "Ce rapport n'existe plus : il a été supprimé ailleurs. Voici l'historique tel qu'il est.";

      return RedirectToPage();
    }

    if (!deleted.IsSuccess)
    {
      foreach (var refusal in deleted.ValidationErrors)
      {
        ModelState.AddModelError(refusal.Identifier, refusal.ErrorMessage);
      }

      // ⚠️ L'historique se relit AVANT d'être rendu : le refus s'affiche au-dessus de l'état réel du
      // déploiement, et non au-dessus de celui qu'il avait au chargement précédent.
      return await ReadTheHistoryAsync(cancellationToken);
    }

    TempData[NoticeKey] = WhatWasDeleted(deleted.Value);

    return RedirectToPage();
  }

  /// <summary>
  /// Ce que la suppression vient d'emporter, en toutes lettres — <b>et ce qu'elle a fait au rang du
  /// courant</b>.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>La seconde phrase est la moitié utile.</b> Supprimer le courant remet le rapport
  /// précédent au rang de courant : un <c>Operator</c> qui ne le sait pas rouvrira le dépistage et
  /// arbitrera un rapport qu'il croyait rangé.
  /// </remarks>
  private static string WhatWasDeleted(DeletedScreening deleted)
  {
    var gone =
      $"Le dépistage de « {deleted.Database} » lancé le "
      + $"{deleted.LaunchedOn.ToString("dd/MM/yyyy à HH:mm", System.Globalization.CultureInfo.GetCultureInfo("fr-FR"))} "
      + $"a été supprimé, avec ses {deleted.ColumnCount} colonnes et tous leurs arbitrages. Rien ne "
      + "les rétablit.";

    return deleted.WasCurrent
      ? $"{gone} C'était le dépistage courant : le plus récent de ceux qui restent l'est devenu, et "
        + "c'est désormais lui qui s'arbitre."
      : gone;
  }

  /// <summary>
  /// Ce que dit un renvoi quand le formulaire ne désignait aucun rapport. ⚠️ <b>Une seule phrase</b> :
  /// ces branches n'ont qu'une cause réelle — un formulaire qui n'est pas celui de cet écran — et les
  /// distinguer aurait dit à l'humain ce que son navigateur a mal fait.
  /// </summary>
  private const string NothingWasDeleted =
    "Aucune suppression n'a été enregistrée : le formulaire envoyé ne désignait pas de rapport.";

  private async Task<IActionResult> ReadTheHistoryAsync(CancellationToken cancellationToken)
  {
    History = await mediator.Send(new ReadScreeningHistoryQuery(), cancellationToken);

    return Page();
  }
}
