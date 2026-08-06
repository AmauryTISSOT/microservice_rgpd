using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.UseCases.Casework.ConfirmClaim;

/// <summary>
/// Un <c>Operator</c> <b>reprend à son compte</b> un droit qu'une <c>Qualification</c> avait
/// seulement proposé.
/// </summary>
/// <remarks>
/// <para>
/// <b>Le geste a lieu dans le dossier ouvert, pendant que le délai court.</b> Il n'existe aucun
/// vestibule où une demande attendrait sa confirmation avant d'entrer : le mois de l'art. 12.3 a
/// commencé à la réception, et une salle d'attente aurait fait passer pour « pas encore commencé »
/// un compteur déjà lancé.
/// </para>
/// <para>
/// <b>Il est signé, comme tout geste qui produit une issue.</b> Une machine ne reconnaît jamais un
/// droit toute seule ; c'est précisément ce que cette commande vient corriger, et la signer par
/// l'application aurait vidé le geste de son sens.
/// </para>
/// </remarks>
/// <param name="Case">Le dossier dans lequel la confirmation a lieu.</param>
/// <param name="Right">Le droit repris à son compte.</param>
/// <param name="SignedBy">
/// Le nom que l'<c>Operator</c> a saisi. Non authentifié — la preuve garde le nom <b>et</b> ce
/// régime.
/// </param>
public sealed record ConfirmClaimCommand(CaseId Case, DataSubjectRight Right, string? SignedBy)
  : ICommand<Result>;
