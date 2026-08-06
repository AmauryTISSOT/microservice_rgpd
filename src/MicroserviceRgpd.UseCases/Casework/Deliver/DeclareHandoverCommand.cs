using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.UseCases.Casework.Deliver;

/// <summary>
/// <b>Second geste</b> de la remise : l'<c>Operator</c> affirme avoir rendu la réponse à la
/// personne. Ce clic <b>seul</b> date la remise au <c>Ledger</c> et détruit les pièces.
/// </summary>
/// <remarks>
/// <para>
/// <b>La remise est une affirmation, jamais un transfert d'octets.</b> Le service ne remet rien à
/// personne : il ne connaît ni l'adresse de la personne, ni le canal dont l'<c>Operator</c> répond,
/// et un envoi automatique l'aurait rendu comptable d'une adresse qu'il n'a pas vérifiée. Ce que la
/// preuve garde est donc un constat signé — « quelqu'un a dit avoir rendu » —, et non un accusé de
/// réception que personne n'a.
/// </para>
/// <para>
/// <b>Elle est signée d'un nom, et le régime accompagne le nom.</b> Aucune machine n'affirme qu'une
/// réponse a été rendue à quelqu'un.
/// </para>
/// </remarks>
/// <param name="Case">Le dossier dont on déclare la remise.</param>
/// <param name="Right">Le droit remis, et lui seul — deux droits sont deux dates de remise.</param>
/// <param name="SignedBy">
/// Le nom que l'<c>Operator</c> a saisi. Non authentifié — la preuve garde le nom <b>et</b> ce
/// régime.
/// </param>
public sealed record DeclareHandoverCommand(CaseId Case, DataSubjectRight Right, string? SignedBy)
  : ICommand<Result>;
