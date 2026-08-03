using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.Web.Casework;

/// <summary>
/// Ce que l'application reçoit quand une demande est entrée.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ce que la réponse tait délibérément</b> : le sac de désignations en écho, les <c>Step</c> nés
/// du catalogue, et tout ce qui décrit le paysage du client. Un appelant qui saurait qu'un dossier
/// porte six <c>Step</c> apprendrait par la bande combien de systèmes son responsable de traitement
/// a déclarés — un dénombrement du paysage, que la règle des chiffres interdit d'afficher et qu'il
/// n'y a aucune raison de rendre sur le fil.
/// </para>
/// <para>
/// <b>Aucune URL de dossier n'est rendue</b>, et c'est délibéré : la seule surface d'instruction
/// est la GUI de l'<c>Operator</c>, et l'application du client n'y a rien à faire — un lien qu'elle
/// suivrait lui laisserait refaire chez elle l'écran « demande traitée » que le service refuse
/// d'offrir.
/// </para>
/// <para>
/// <b>Règle d'évolution</b> : ajouter un champ facultatif est rétro-compatible ; en retirer un, en
/// renommer un, ou durcir une contrainte d'entrée sont des <b>ruptures</b>.
/// </para>
/// </remarks>
/// <param name="CaseId">
/// L'identité que le service donne à ce dossier. <b>Toujours présente</b>, et déjà persistée quand
/// elle arrive : c'est ce qui en fait autre chose qu'une promesse.
/// </param>
/// <param name="ReceivedOn">
/// L'instant dont le service fait partir le délai de l'art. 12.3 sur ce canal — celui de l'appel.
/// Il est rendu parce qu'il est <b>déclaré par le canal</b> et non constaté : l'appelant doit
/// pouvoir lire ce que le service a retenu de sa demande.
/// </param>
/// <param name="Claims">
/// Les droits reconnus, sous leurs noms canoniques et dans l'ordre de la taxonomie. Un seul dossier
/// les porte tous.
/// </param>
public sealed record OpenCaseResponse(
  Guid CaseId,
  DateTimeOffset ReceivedOn,
  IReadOnlyCollection<DataSubjectRight> Claims)
{
  /// <summary>Projette sur le fil ce que le service a ouvert.</summary>
  public static OpenCaseResponse From(Case opened)
  {
    ArgumentNullException.ThrowIfNull(opened);

    return new OpenCaseResponse(
      opened.Id.Value,
      opened.ReceivedOn,
      [.. opened.Claims.Select(claim => claim.Right)]);
  }
}
