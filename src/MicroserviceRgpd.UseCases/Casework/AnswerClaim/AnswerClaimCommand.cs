using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.UseCases.Casework.AnswerClaim;

/// <summary>
/// L'<c>Operator</c> déclare que le service a <b>répondu</b> sur un droit.
/// </summary>
/// <remarks>
/// <para>
/// <b>Un geste par droit, et jamais un geste par dossier.</b> Chaque droit est une réponse due à la
/// personne : clore un dossier ne peut pas valoir réponse sur six droits d'un seul clic. C'est
/// pourquoi ce geste est distinct de la clôture, qui se contente de le <b>réclamer</b>.
/// </para>
/// <para>
/// <b>Il atteste l'acte de répondre, et rien de plus.</b> Des <c>Step</c> restés inatteints ne le
/// barrent pas et restent lisibles un par un : l'incomplétude reste visible là où elle est vraie,
/// plutôt que masquée par un état de haut niveau rassurant.
/// </para>
/// <para>
/// ⚠️ <b>Le refus d'un droit n'est pas ici.</b> <c>ClaimState.Refused</c> a une charge probatoire
/// propre — les mentions de l'art. 12.4 sont dues à la personne — et il aura son geste, avec la
/// motivation qu'il exige.
/// </para>
/// </remarks>
/// <param name="Case">Le dossier qui porte ce droit.</param>
/// <param name="Right">Le droit sur lequel le service déclare avoir répondu.</param>
/// <param name="SignedBy">
/// Le nom que l'<c>Operator</c> a saisi. Non authentifié — la preuve garde le nom <b>et</b> ce
/// régime.
/// </param>
public sealed record AnswerClaimCommand(CaseId Case, DataSubjectRight Right, string? SignedBy)
  : ICommand<Result>;
