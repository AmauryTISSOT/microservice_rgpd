using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.SharedKernel;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace MicroserviceRgpd.Web.Pages.Casework;

/// <summary>
/// Ce qu'un <c>Operator</c> saisit pour déclarer où en est un travail dû — <b>des chaînes, et rien
/// que des chaînes</b>, jusqu'à ce qu'elles franchissent la frontière du domaine.
/// </summary>
/// <remarks>
/// <para>
/// <b>Un seul champ de prose, et c'est celui de la prose de preuve.</b> Le constat dit <i>pourquoi on
/// a déclaré cela</i>, n'est pas nominatif par nature, et survit dans le <c>Ledger</c>. La prose de
/// <b>travail</b> — celle qui dit quelle ligne appartient à qui, et qui nomme donc des tiers non
/// demandeurs — n'a <b>aucun champ ici</b> : elle se saisit ailleurs sur l'écran, vit sur le
/// <c>Case</c> et meurt à la clôture. La règle tient par le <b>placement</b>, et non par la
/// discipline d'un <c>Operator</c> à qui l'on demanderait de s'auto-censurer dans un champ unique.
/// </para>
/// <para>
/// <b>Le nom est un champ du formulaire, et il n'est jamais mémorisé.</b> La surface n'authentifie
/// personne — choix de PoC assumé — et rien ne réutilise le nom d'une signature à la suivante : une
/// case pré-remplie ferait signer quelqu'un sans qu'il l'ait voulu.
/// </para>
/// </remarks>
public sealed class CaseForm
{
  /// <summary>Le droit au titre duquel le travail était dû, par son nom canonique anglais.</summary>
  public string? Right { get; set; }

  /// <summary>Le système sur lequel il était dû, par son identifiant.</summary>
  public string? DeclaredSystem { get; set; }

  /// <summary>
  /// L'état déclaré, par son nom canonique anglais. <c>Untreated</c> en fait partie : c'est l'aveu que
  /// personne ne l'a fait, et une liste qui l'omettrait ferait cocher une valeur propre à sa place.
  /// </summary>
  public string? State { get; set; }

  /// <summary>Le constat de l'<c>Operator</c>, en prose libre. <b>Prose de preuve</b> : elle survit.</summary>
  public string? Finding { get; set; }

  /// <summary>Le nom que l'<c>Operator</c> saisit pour signer. Sans authentification, et sans mémoire.</summary>
  public string? SignedBy { get; set; }

  /// <summary>
  /// Fait franchir au formulaire la frontière du domaine, ou <b>nomme à l'humain</b> ce qu'il a mal
  /// rempli, champ par champ.
  /// </summary>
  /// <remarks>
  /// Un mot que le vocabulaire fermé ignore n'est pas une saisie humaine — les listes sont closes —
  /// mais un formulaire forgé : il est refusé plutôt qu'ignoré, faute de quoi le service
  /// enregistrerait autre chose que ce qu'on croit lui avoir dit.
  /// </remarks>
  /// <param name="modelState">L'endroit où les refus se déposent, sous le nom du champ fautif.</param>
  /// <param name="prefix">Le préfixe de liaison du formulaire, tel que la page l'a déclaré.</param>
  /// <returns>Les valeurs du domaine, ou <c>null</c> si au moins un champ a été refusé.</returns>
  public DeclaredFinding? Read(ModelStateDictionary modelState, string prefix)
  {
    ArgumentNullException.ThrowIfNull(modelState);

    // Le vide entre comme vide plutôt que comme un nul : un `ArgumentNullException` de la couche
    // d'énumération serait une panne là où il s'agit d'un formulaire forgé, qu'on refuse en le nommant.
    if (!DataSubjectRight.TryFromName(Right ?? string.Empty, out var right))
    {
      modelState.AddModelError($"{prefix}.{nameof(Right)}", $"« {Right} » n'est pas un droit de la taxonomie.");
    }

    if (!StepState.TryFromName(State ?? string.Empty, out var state))
    {
      modelState.AddModelError($"{prefix}.{nameof(State)}", $"« {State} » n'est pas un état du travail dû.");
    }

    // Le vide entre comme vide plutôt que comme un nul, pour la même raison que ci-dessus.
    var system = FormBoundary.Read(
      modelState,
      prefix,
      nameof(DeclaredSystem),
      () => DeclaredSystemId.From(DeclaredSystem ?? string.Empty));

    if (!modelState.IsValid || right is null || state is null || system is null)
    {
      return null;
    }

    // Le constat et le nom ne sont pas relus ici : leurs règles vivent dans les types de la preuve,
    // et les redire ici ferait deux rédactions d'une même exigence — qui finiraient par différer.
    return new DeclaredFinding(right, system.Value, state, Finding, SignedBy);
  }

}

/// <summary>
/// Ce qu'un <c>Operator</c> saisit pour écrire <b>après coup</b> ce qu'il a pesé de l'identité du
/// demandeur — les mêmes deux champs qu'au dépôt, et pour les mêmes raisons.
/// </summary>
/// <remarks>
/// <b>La méthode n'a ici aucune option vide.</b> Au dépôt, le vide dit « personne n'a encore pesé » ;
/// ici, quelqu'un est en train de peser — c'est le geste même — et une case vide n'y voudrait rien
/// dire. <c>None</c> reste offerte : c'est la réponse de qui a regardé et n'a rien fait.
/// </remarks>
public sealed class MotivationForm
{
  /// <summary>La méthode, par son nom canonique anglais. Vocabulaire fermé : elle se compte et survit.</summary>
  public string? VerificationMethod { get; set; }

  /// <summary>
  /// Le détail, en prose libre et facultatif. <b>Prose de travail</b> : il nomme par nature, vit sur
  /// le dossier et meurt à sa clôture.
  /// </summary>
  public string? Detail { get; set; }

  /// <summary>Le nom que l'<c>Operator</c> saisit pour signer. Sans authentification, et sans mémoire.</summary>
  public string? SignedBy { get; set; }
}

/// <summary>
/// Ce qu'un <c>Operator</c> saisit pour <b>reprendre à son compte</b> un droit qu'une
/// <c>Qualification</c> avait seulement proposé.
/// </summary>
/// <remarks>
/// <b>Deux champs, et pas un de plus.</b> Aucune prose n'est réclamée : confirmer, c'est dire « oui,
/// ce droit-là ». Le fait, le droit, la date et le nom disent tout, et exiger un constat ferait
/// écrire une ligne de rien à chaque confirmation.
/// </remarks>
public sealed class ConfirmationForm
{
  /// <summary>Le droit repris à son compte, par son nom canonique anglais.</summary>
  public string? Right { get; set; }

  /// <summary>Le nom que l'<c>Operator</c> saisit pour signer. Sans authentification, et sans mémoire.</summary>
  public string? SignedBy { get; set; }
}

/// <summary>
/// Ce qu'un <c>Operator</c> saisit pour <b>trancher une réserve</b> de <c>Locate</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Aucun champ de prose, et c'est délibéré.</b> Le motif de la réserve est écrit par
/// l'application et lu par l'humain ; ce que l'humain rend est une <b>issue</b> — rattachée, ou
/// écartée — et le <c>Ledger</c> en garde le fait, la date et le nom. Réclamer une prose ici ferait
/// écrire une ligne de rien à chaque arbitrage, et le constat qui compte se noierait dans les autres.
/// </para>
/// <para>
/// ⚠️ <b>Il n'existe aucune valeur par défaut.</b> Les deux issues sont deux boutons distincts : une
/// liste où « rattachée » serait pré-sélectionnée ferait fusionner à tort d'un seul clic — et cette
/// erreur-là est irréversible, et porte sur la donnée d'un tiers.
/// </para>
/// </remarks>
public sealed class ArbitrationForm
{
  /// <summary>Le système qui a rendu la réserve, par son identifiant.</summary>
  public string? DeclaredSystem { get; set; }

  /// <summary>La référence opaque de la ligne, dans le vocabulaire de l'application.</summary>
  public string? Reference { get; set; }

  /// <summary>L'issue rendue, par son nom canonique anglais. Jamais <c>Awaiting</c>.</summary>
  public string? Ruling { get; set; }

  /// <summary>Le nom que l'<c>Operator</c> saisit pour signer. Sans authentification, et sans mémoire.</summary>
  public string? SignedBy { get; set; }
}

/// <summary>
/// Ce qu'un <c>Operator</c> saisit pour <b>télécharger</b> la remise d'un droit. Le premier geste.
/// </summary>
/// <remarks>
/// <b>Aucun nom, et c'est délibéré.</b> Ce geste n'affirme rien : il ouvre une archive pour la
/// vérifier. Réclamer une signature ici aurait fait signer un humain pour lire un fichier, et
/// mêlé la preuve d'une remise à la vérification qui la précède.
/// </remarks>
public sealed class DeliveryForm
{
  /// <summary>Le droit dont on prend la remise, par son nom canonique anglais.</summary>
  public string? Right { get; set; }
}

/// <summary>
/// Ce qu'un <c>Operator</c> saisit pour <b>déclarer remis</b>. Le second geste, et le seul qui date
/// quoi que ce soit.
/// </summary>
/// <remarks>
/// <para>
/// <b>Un formulaire à part du téléchargement.</b> Les deux gestes ne portent pas la même chose :
/// l'un vérifie, l'autre affirme — et cette affirmation détruit les pièces. Les mêler aurait fait
/// détruire d'un clic ce que quelqu'un venait seulement d'ouvrir.
/// </para>
/// <para>
/// <b>Aucun champ de prose.</b> Ce qui se déclare est un fait — la réponse a été rendue —, et le
/// canal par lequel elle l'a été n'est pas quelque chose que le service sait vérifier. Réclamer une
/// prose ferait écrire une ligne de rien à chaque remise.
/// </para>
/// </remarks>
public sealed class HandoverForm
{
  /// <summary>Le droit dont on déclare la remise, par son nom canonique anglais.</summary>
  public string? Right { get; set; }

  /// <summary>Le nom que l'<c>Operator</c> saisit pour signer. Sans authentification, et sans mémoire.</summary>
  public string? SignedBy { get; set; }
}

/// <summary>
/// Ce qu'un <c>Operator</c> déclare, une fois la frontière du domaine franchie.
/// </summary>
/// <param name="Right">Le droit au titre duquel le travail était dû.</param>
/// <param name="DeclaredSystem">Le système sur lequel il l'était.</param>
/// <param name="State">L'état déclaré.</param>
/// <param name="Finding">Le constat — prose de preuve, exigée par le type de la preuve.</param>
/// <param name="SignedBy">Le nom saisi, exigé par le type de la signature.</param>
public sealed record DeclaredFinding(
  DataSubjectRight Right,
  DeclaredSystemId DeclaredSystem,
  StepState State,
  string? Finding,
  string? SignedBy);
