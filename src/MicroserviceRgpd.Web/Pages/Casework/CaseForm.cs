using System.Globalization;
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
/// <b>Un seul champ de prose, et c'est celui du texte qui reste.</b> Le constat dit <i>pourquoi on
/// a déclaré cela</i>, n'est pas nominatif par nature, et survit dans l'<c>EvidenceLog</c>. Le
/// <b>texte qui meurt</b> — celui qui dit quelle ligne appartient à qui, et qui nomme donc des tiers
/// non demandeurs — n'a <b>aucun champ ici</b> : il se saisit ailleurs sur l'écran, vit sur le
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

  /// <summary>Le constat de l'<c>Operator</c>, en prose libre. <b>Texte qui reste</b> : il survit.</summary>
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
  /// Le détail, en prose libre et facultatif. <b>Texte qui meurt</b> : il nomme par nature, vit sur
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
/// écartée — et l'<c>EvidenceLog</c> en garde le fait, la date et le nom. Réclamer une prose ici ferait
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
public sealed class DeliveryDeclarationForm
{
  /// <summary>Le droit dont on déclare la remise, par son nom canonique anglais.</summary>
  public string? Right { get; set; }

  /// <summary>Le nom que l'<c>Operator</c> saisit pour signer. Sans authentification, et sans mémoire.</summary>
  public string? SignedBy { get; set; }
}

/// <summary>
/// Ce qu'un <c>Operator</c> saisit pour déclarer que le service a <b>répondu</b> sur un droit.
/// </summary>
/// <remarks>
/// <b>Deux champs, comme la confirmation, et pour la même raison.</b> Répondre est un fait ; le
/// contenu de la réponse est le paquet remis, qui a ses propres gestes. Réclamer une prose ici
/// ferait écrire une ligne de rien à chaque droit clos.
/// </remarks>
public sealed class ClaimOutcomeForm
{
  /// <summary>Le droit sur lequel le service déclare avoir répondu, par son nom canonique anglais.</summary>
  public string? Right { get; set; }

  /// <summary>Le nom que l'<c>Operator</c> saisit pour signer. Sans authentification, et sans mémoire.</summary>
  public string? SignedBy { get; set; }
}

/// <summary>
/// Ce qu'un <c>Operator</c> saisit pour <b>déclarer une prolongation</b> de deux mois au titre de
/// l'art. 12.3.
/// </summary>
/// <remarks>
/// <para>
/// <b>Le service ne prolonge rien et n'écrit à personne.</b> Il réclame ce que l'article met à la
/// charge de qui prolonge — un motif, et la date à laquelle la personne a été informée de la
/// prolongation et de ses motifs — et l'enregistre.
/// </para>
/// <para>
/// <b>Aucun champ ne dit si l'échéance a bougé</b>, et aucun ne pourrait : le déplacement est un
/// calcul fait sur la date de la déclaration, à l'instant où quelqu'un regarde.
/// </para>
/// </remarks>
public sealed class ExtensionForm
{
  /// <summary>
  /// Le motif, en prose libre. <b>Exigé</b> : l'art. 12.3 met la raison à la charge de qui prolonge.
  /// <b>Texte qui reste</b> — il survit au dossier.
  /// </summary>
  public string? Motive { get; set; }

  /// <summary>
  /// Le jour où l'<c>Operator</c> déclare avoir informé la personne, tel que le navigateur l'envoie.
  /// </summary>
  /// <remarks>
  /// <b>Il reste une chaîne jusqu'à la frontière</b>, comme la date de réception du dépôt : une date
  /// mal saisie doit se redire à l'humain sous le nom de <b>sa</b> case, et un <c>DateTime?</c> lié
  /// par le cadre aurait rendu un nul indiscernable d'une case vide.
  /// </remarks>
  public string? InformedOn { get; set; }

  /// <summary>Le nom que l'<c>Operator</c> saisit pour signer. Sans authentification, et sans mémoire.</summary>
  public string? SignedBy { get; set; }

  /// <summary>
  /// La date d'information, ou le refus déposé sous le nom de sa case.
  /// </summary>
  /// <remarks>
  /// <b>Elle est réclamée</b> — c'est la moitié de la charge probatoire de l'art. 12.3, et une
  /// prolongation sans elle ne prouverait que la moitié de ce que l'article exige. ⚠️ Une date
  /// <b>postérieure</b> n'est pas refusée ici mais par le domaine, qui la connaît : la frontière
  /// nomme, le type tranche.
  /// </remarks>
  /// <param name="modelState">L'endroit où les refus se déposent.</param>
  /// <param name="prefix">Le préfixe de liaison du formulaire, tel que la page l'a déclaré.</param>
  public DateTimeOffset? ReadInformedOn(ModelStateDictionary modelState, string prefix)
  {
    ArgumentNullException.ThrowIfNull(modelState);

    var field = $"{prefix}.{nameof(InformedOn)}";

    if (string.IsNullOrWhiteSpace(InformedOn))
    {
      modelState.AddModelError(
        field,
        "Le jour où vous avez informé la personne est exigé : l'art. 12.3 met cette preuve à votre "
        + "charge, et le service ne la constate pas à votre place.");

      return null;
    }

    if (!DateOnly.TryParse(InformedOn, CultureInfo.InvariantCulture, out var declared))
    {
      modelState.AddModelError(field, $"« {InformedOn} » n'est pas une date.");

      return null;
    }

    return new DateTimeOffset(declared.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
  }
}

/// <summary>
/// Ce qu'un <c>Operator</c> saisit pour <b>clore le dossier</b> — et détruire tout le nominatif à
/// l'instant même.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ce formulaire porte une case que peu d'autres portent.</b> Le geste est irréversible, et sa
/// parade est un geste <b>délibéré dans l'écran</b> plutôt que de la donnée gardée en réserve. La
/// case n'est ni pré-cochée ni mémorisée : elle est la seconde affirmation d'une personne qui vient
/// de lire ce qu'elle s'apprête à détruire. Le seul autre geste du dispositif à en porter une est
/// la destruction d'un <c>EvidenceLog</c> échu, sur le tableau des demandes RGPD.
/// </para>
/// <para>
/// <b>Le motif est un champ de prose, et c'est de la prose de <em>preuve</em>.</b> Il dit
/// <i>pourquoi on a décidé cela</i>, il n'est pas nominatif par nature, et il <b>survit</b> dans le
/// <c>EvidenceLog</c> quand tout le dossier tombe. C'est le seul champ de prose de cet écran dont
/// l'écriture soit parfois exigée — <c>Abandoned</c>, et lui seul.
/// </para>
/// </remarks>
public sealed class ClosingForm
{
  /// <summary>La cause de la clôture, par son nom canonique anglais. Vocabulaire fermé : elle se compte.</summary>
  public string? Cause { get; set; }

  /// <summary>
  /// Le motif, en prose libre. Exigé pour <c>Abandoned</c> seul, accueilli ailleurs. <b>Prose de
  /// preuve</b> : elle survit au dossier.
  /// </summary>
  public string? Motive { get; set; }

  /// <summary>Le nom que l'<c>Operator</c> saisit pour signer. Sans authentification, et sans mémoire.</summary>
  public string? SignedBy { get; set; }

  /// <summary>
  /// La <b>confirmation délibérée</b> du geste irréversible. Sans elle, rien n'est détruit.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Elle est éprouvée dans l'écran, jamais dans le domaine.</b> Ce qu'elle protège est un
  /// humain contre son propre clic : c'est une propriété de la surface, et la faire descendre dans
  /// la commande aurait fait porter au domaine une exigence d'ergonomie — puis, tôt ou tard, un
  /// appelant qui la coche pour lui-même.
  /// </remarks>
  public bool Confirmed { get; set; }
}

/// <summary>
/// Ce qu'un <c>Operator</c> déclare, une fois la frontière du domaine franchie.
/// </summary>
/// <param name="Right">Le droit au titre duquel le travail était dû.</param>
/// <param name="DeclaredSystem">Le système sur lequel il l'était.</param>
/// <param name="State">L'état déclaré.</param>
/// <param name="Finding">Le constat — texte qui reste, exigé par le type de la preuve.</param>
/// <param name="SignedBy">Le nom saisi, exigé par le type de la signature.</param>
public sealed record DeclaredFinding(
  DataSubjectRight Right,
  DeclaredSystemId DeclaredSystem,
  StepState State,
  string? Finding,
  string? SignedBy);
