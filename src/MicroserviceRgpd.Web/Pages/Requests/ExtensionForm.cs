using MicroserviceRgpd.Core.Requests;

namespace MicroserviceRgpd.Web.Pages.Requests;

/// <summary>
/// <b>Les deux champs que l'<c>Operator</c> saisit</b> dans la modale de prolongation — <b>des
/// chaînes, et rien que des chaînes</b>, sous les clés mêmes du corps. Rien n'est trimé ni jugé ici :
/// c'est <see cref="DataSubjectRequest.Extend"/> qui en décide.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>La durée n'y est pas</b> : deux mois est une constante du domaine, pas un champ (ADR-0029).
/// </para>
/// <para>
/// ⚠️ <b>L'identifiant de la demande n'y entre pas</b> : ce n'est pas une donnée saisie. Le handler le
/// prend en paramètre, comme celui de la suppression et celui de l'exécution.
/// </para>
/// </remarks>
public sealed class ExtensionForm
{
  /// <summary>Le nom canonique du motif de prolongation — <c>Complexity</c> ou <c>NumberOfRequests</c>.</summary>
  public string? ExtensionGround { get; set; }

  /// <summary>Le texte qui dit le fait concret justifiant les deux mois.</summary>
  public string? ExtensionJustification { get; set; }

  /// <summary>La saisie, prête pour le domaine.</summary>
  public ExtensionEntry ToEntry() => new(ExtensionGround, ExtensionJustification);
}
