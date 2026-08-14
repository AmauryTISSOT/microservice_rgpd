namespace MicroserviceRgpd.Core.Casework.EvidenceLog;

/// <summary>
/// Les <c>EvidenceLog</c> dont la conservation est <b>échue</b>, et leur destruction — <b>entière, et
/// jamais ligne à ligne</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>C'est un type à part d'<see cref="IEvidenceLog"/>, et c'est tout le propos.</b> L'ajout seul reste
/// ce qu'il est : rien de ce qui écrit la preuve ne sait la relire ni l'effacer. Ce que ce type-ci
/// sait faire — voir ce qui est échu, et détruire un dossier de preuve entier — est <b>la seule
/// suppression du dispositif</b>, et elle est écrite dans un fichier qu'on ouvre exprès.
/// </para>
/// <para>
/// ⚠️ <b>Rien ici ne tourne, et rien ne purge.</b> <see cref="ListAsync"/> rend ce qu'un humain doit
/// voir ; <see cref="DestroyAsync"/> n'est appelée que par son clic. Il n'existe aucun balayage,
/// aucune minuterie, aucun <c>cron</c> : une purge de fond interrompue rendrait un écran <b>vide et
/// rassurant</b>, et personne ne saurait dire si elle a tourné.
/// </para>
/// <para>
/// ⚠️ <b>La destruction ne laisse aucune trace d'elle-même.</b> Un <c>EvidenceLog</c> détruit ne peut pas
/// consigner sa propre destruction, et rien n'est écrit ailleurs pour lui : on ne prouvera jamais
/// avoir purgé. C'est un coût assumé — l'alternative serait un second étage d'anonymisation, qui
/// rouvrirait l'expurgation que la définition de l'<c>EvidenceLog</c> ferme.
/// </para>
/// </remarks>
public interface IExpiredEvidenceLogs
{
  /// <summary>
  /// Les <c>EvidenceLog</c> échus à cet instant : un dossier clos depuis plus de
  /// <see cref="EvidenceLogRetention.Years"/> ans, dont la preuve est encore là.
  /// </summary>
  /// <remarks>
  /// <b>Un <c>EvidenceLog</c> déjà détruit n'y figure plus</b>, son dossier clos eût-il survécu : la
  /// ligne est celle de la preuve, et elle disparaît avec elle. C'est ainsi que le geste se voit
  /// avoir été fait, faute de pouvoir se consigner.
  /// </remarks>
  /// <param name="observedAt">L'instant où l'<c>Operator</c> regarde — jamais lu sur une horloge d'ici.</param>
  /// <param name="cancellationToken">L'annulation de l'échange en cours.</param>
  Task<IReadOnlyList<ExpiredEvidenceLog>> ListAsync(
    DateTimeOffset observedAt,
    CancellationToken cancellationToken = default);

  /// <summary>
  /// Détruit <b>en entier</b> l'<c>EvidenceLog</c> d'un dossier, si et seulement si sa conservation est
  /// échue à cet instant.
  /// </summary>
  /// <remarks>
  /// <b>L'échéance est éprouvée ici, et non crue sur parole.</b> L'écran de la file d'où part le
  /// clic a pu être affiché il y a une heure comme il y a un an ; ce qui est irréversible ne se
  /// décide pas sur une page vieille d'une minute.
  /// </remarks>
  /// <param name="evidenceLogOf">Le dossier dont la preuve est détruite.</param>
  /// <param name="observedAt">L'instant du geste, sur lequel l'échéance est éprouvée.</param>
  /// <param name="cancellationToken">L'annulation de l'échange en cours.</param>
  /// <returns><c>true</c> si une preuve échue vient d'être détruite ; <c>false</c> s'il n'y avait rien à détruire.</returns>
  Task<bool> DestroyAsync(
    CaseId evidenceLogOf,
    DateTimeOffset observedAt,
    CancellationToken cancellationToken = default);
}

/// <summary>
/// Un <c>EvidenceLog</c> échu, tel que l'écran de la file le montre : un dossier, la date de sa clôture,
/// et le jour où sa preuve a cessé d'être due.
/// </summary>
/// <remarks>
/// <b>Ni personne, ni droit, ni délai</b> — et c'est pourquoi cette ligne ne peut pas vivre dans le
/// tableau des <c>Case</c>. Le dossier qu'elle nomme est clos depuis cinq ans : il n'a plus une
/// désignation, et son délai de l'art. 12.3 est éteint depuis longtemps.
/// </remarks>
/// <param name="Case">Le dossier dont c'est la preuve — la seule façon de la désigner.</param>
/// <param name="ClosedOn">Le jour de la clôture, d'où court la conservation.</param>
/// <param name="ExpiredOn">Le jour où elle a cessé d'être due. Un fait, jamais un compte de jours écoulés.</param>
public sealed record ExpiredEvidenceLog(CaseId Case, DateTimeOffset ClosedOn, DateTimeOffset ExpiredOn);
