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
