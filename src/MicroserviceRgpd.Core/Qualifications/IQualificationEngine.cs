namespace MicroserviceRgpd.Core.Qualifications;

/// <summary>
/// Le port par lequel le domaine demande son avis à un moteur de qualification.
/// </summary>
/// <remarks>
/// <para>
/// <b>Un seul port pour tous les moteurs</b>, et il ne nomme aucun d'eux. Le domaine ignore qu'un
/// avis vient d'un lexique déterministe, d'un LLM local ou d'un troisième moteur pas encore écrit ;
/// il ignore surtout qu'il y a un sidecar Python et du HTTP derrière — le transport est un détail
/// d'<c>Infrastructure</c>, et c'est ce port qui l'y maintient.
/// </para>
/// <para>
/// <b>Un avis, ou rien.</b> Une implémentation rend un <see cref="QualificationOpinion"/> qui
/// satisfait déjà tous les invariants du domaine, ou elle échoue. Il n'existe pas d'avis à moitié
/// valide : un avis mal formé n'est pas un avis faible, c'est une panne du moteur, et elle se
/// présente comme telle plutôt que de se déguiser en verdict.
/// </para>
/// </remarks>
public interface IQualificationEngine
{
  /// <summary>
  /// Demande au moteur son avis sur un texte.
  /// </summary>
  /// <param name="text">Le texte à qualifier, déjà validé par son type.</param>
  /// <param name="cancellationToken">
  /// L'annulation de l'appelant, propagée jusqu'au moteur : un travail poursuivi pour quelqu'un
  /// qui est parti occupe une place dans la file de celui qui est resté.
  /// </param>
  Task<QualificationOpinion> QualifyAsync(RightsRequestText text, CancellationToken cancellationToken = default);
}
