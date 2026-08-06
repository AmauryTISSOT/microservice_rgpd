using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.Core.Casework;

/// <summary>
/// Ce que le service détient des pièces lues, <b>hors de l'agrégat</b> et pour une durée qui n'est
/// pas celle du dossier.
/// </summary>
/// <remarks>
/// <para>
/// <b>Il ne passe pas par le dépôt générique</b>, contraint aux agrégats racines : emprunter
/// celui-ci aurait déclaré agrégat ce qui est <b>hors de l'agrégat</b> par construction, et fait
/// dépendre l'effacement d'un contenu de la mise à jour d'une racine qui lui survit.
/// </para>
/// <para>
/// <b>Garder remplace.</b> Le triplet (dossier, droit, système) identifie la pièce : relire un
/// système ne l'empile pas, il la remplace — deux pièces pour la même paire seraient deux réponses
/// dues à la personne sur la même question.
/// </para>
/// <para>
/// ⚠️ <b>Aucune méthode ne lit un corps.</b> Ce port rend des <see cref="RetrievedData"/> entières
/// parce que la <c>Delivery</c> les tendra telles quelles à l'<c>Operator</c> ; rien du service
/// n'ouvre ce qu'elles portent, et il n'existe ici aucun geste qui le laisserait croire.
/// </para>
/// </remarks>
public interface IRetrievedData
{
  /// <summary>
  /// Garde la pièce qu'un <c>Read</c> vient de servir, en <b>remplaçant</b> celle que ce même
  /// (dossier, droit, système) portait.
  /// </summary>
  /// <param name="piece">La pièce, enveloppe et octets.</param>
  /// <param name="cancellationToken">L'annulation de l'écriture en cours.</param>
  /// <exception cref="ArgumentNullException"><paramref name="piece"/> est absent.</exception>
  Task KeepAsync(RetrievedData piece, CancellationToken cancellationToken = default);

  /// <summary>
  /// Les pièces détenues au titre d'un dossier, dans l'ordre du droit puis du système — jamais dans
  /// celui de leur arrivée, qui ferait dépendre un écran de l'ordre où des appels ont répondu.
  /// </summary>
  /// <param name="caseId">Le dossier dont on veut les pièces.</param>
  /// <param name="cancellationToken">L'annulation de la lecture en cours.</param>
  Task<IReadOnlyList<RetrievedData>> HeldForAsync(CaseId caseId, CancellationToken cancellationToken = default);

  /// <summary>
  /// <b>Détruit</b> les pièces détenues au titre d'un droit de ce dossier. C'est ce que le second
  /// geste de la remise fait, et rien d'autre ne les efface.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>Effacées, pas anonymisées</b> — et <b>sans réécrire le dossier</b> : c'est très exactement
  /// pourquoi ces pièces vivent hors de l'agrégat. Le séjour est inévitable, la lecture précédant
  /// l'effacement ; il doit être minimal.
  /// </para>
  /// <para>
  /// <b>Le grain est le droit, jamais le dossier.</b> Deux droits sont deux réponses et deux dates de
  /// remise : effacer le dossier entier à la première remise aurait détruit la réponse due sur le
  /// second droit avant que personne ne l'ait rendue.
  /// </para>
  /// <para>
  /// <b>Rien à détruire n'est pas une panne.</b> Un droit dont aucune lecture n'a rien rapporté se
  /// remet quand même — la <c>CoverSheet</c> seule est déjà une réponse.
  /// </para>
  /// </remarks>
  /// <param name="caseId">Le dossier dont on remet un droit.</param>
  /// <param name="right">Le droit remis, et lui seul.</param>
  /// <param name="cancellationToken">L'annulation de l'effacement en cours.</param>
  /// <exception cref="ArgumentNullException"><paramref name="right"/> est absent.</exception>
  Task DiscardAsync(CaseId caseId, DataSubjectRight right, CancellationToken cancellationToken = default);
}
