using MicroserviceRgpd.Core.Casework;

namespace MicroserviceRgpd.UseCases.Casework.DeclareMotivation;

/// <summary>
/// Un <c>Operator</c> écrit <b>après coup</b> ce qu'il a pesé de l'identité du demandeur, sur un
/// dossier qui le réclamait.
/// </summary>
/// <remarks>
/// <para>
/// <b>Elle existe pour que la réclamation puisse être satisfaite.</b> Une exigence qu'on ne peut pas
/// satisfaire cesse d'être lue : un bandeau permanent s'apprend à ne plus se voir, et la faiblesse
/// qu'il devait rendre visible redeviendrait invisible.
/// </para>
/// <para>
/// ⚠️ <b>Elle ne touche à aucun <c>Claim</c>.</b> Le droit garde l'<c>IdentityDeclaration</c> sous
/// laquelle il s'est ouvert : ce qu'on pèse aujourd'hui ne rend pas rétroactivement propre ce qui a
/// été fait hier sur la foi de rien. C'est très exactement ce que le gel de l'origine protège.
/// </para>
/// </remarks>
/// <param name="Case">Le dossier dont on pèse enfin l'identité.</param>
/// <param name="Motivation">Ce qu'on a pesé — méthode et détail pris ensemble.</param>
/// <param name="SignedBy">
/// Le nom que l'<c>Operator</c> a saisi. Non authentifié — la preuve garde le nom <b>et</b> ce
/// régime.
/// </param>
public sealed record DeclareMotivationCommand(CaseId Case, IdentityMotivation Motivation, string? SignedBy)
  : ICommand<Result>;
