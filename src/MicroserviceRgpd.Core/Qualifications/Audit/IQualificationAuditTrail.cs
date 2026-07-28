namespace MicroserviceRgpd.Core.Qualifications.Audit;

/// <summary>
/// Le port par lequel le service écrit ce qu'il vient de qualifier — et par lequel il ne relit rien.
/// </summary>
/// <remarks>
/// <para>
/// <b>Une seule méthode, et aucune lecture.</b> Ce n'est pas une omission qu'on comblera : il
/// n'existe aucun <c>GET</c> qui expose la trace, et un dépôt offrirait des capacités de requête que
/// rien n'utilise. Un port en écriture seule dit la vérité du domaine — la trace est l'écrit d'un
/// acte, pas un agrégat qu'on instruit dans le temps.
/// </para>
/// <para>
/// <b>L'écriture est synchrone, et son échec est une panne du service.</b> L'ordre est : qualifier,
/// écrire, répondre. Une écriture qui échoue rend <c>500</c> — jamais <c>200</c> dégradé, y compris
/// lorsque la qualification l'était : la dégradation d'un moteur ne survit pas à une panne de base,
/// et un identifiant de qualification creux viderait de sens tous les autres.
/// </para>
/// </remarks>
public interface IQualificationAuditTrail
{
  /// <summary>
  /// Écrit l'entrée, ou échoue.
  /// </summary>
  /// <param name="entry">Ce que le service vient de qualifier, verdict et prémisses.</param>
  /// <param name="cancellationToken">
  /// L'annulation de l'appelant. Une annulation qui arrive ici laisse la trace non écrite, et donc
  /// aucune ligne : cohérent avec « la trace enregistre les verdicts, jamais les tentatives ».
  /// </param>
  Task RecordAsync(QualificationAuditEntry entry, CancellationToken cancellationToken = default);
}
