using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.Core.Requests;

/// <summary>
/// <b>Ce qui part au système hôte</b> quand une demande s'exécute : exactement cinq valeurs —
/// l'identifiant de la demande, le droit, l'email, le prénom et le nom (ADR-0026).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>L'identifiant est une clé d'idempotence</b> : l'hôte doit traiter deux appels portant le même
/// comme un seul. C'est ce qui couvre la double exécution concurrente.
/// </para>
/// <para>
/// Le prénom et le nom peuvent manquer ; l'email, jamais — une demande sans email ne s'exécute pas.
/// </para>
/// </remarks>
/// <param name="RequestId">La demande exécutée.</param>
/// <param name="Right">Le droit invoqué.</param>
/// <param name="Email">L'email de la personne.</param>
/// <param name="FirstName">Le prénom de la personne, ou <c>null</c>.</param>
/// <param name="LastName">Le nom de la personne, ou <c>null</c>.</param>
public sealed record ExecutionBody(
  DataSubjectRequestId RequestId,
  DataSubjectRight Right,
  EmailAddress Email,
  FirstName? FirstName,
  LastName? LastName)
{
  /// <summary>Le corps de l'exécution de <paramref name="request"/>.</summary>
  /// <exception cref="ArgumentNullException"><paramref name="request"/> est absente.</exception>
  /// <exception cref="InvalidOperationException">La demande ne porte pas d'email : elle ne s'exécute pas.</exception>
  public static ExecutionBody Of(DataSubjectRequest request)
  {
    ArgumentNullException.ThrowIfNull(request);

    if (request.Email is not { } email)
    {
      throw new InvalidOperationException("Une demande sans email ne s'exécute pas : son corps d'exécution n'existe pas.");
    }

    return new ExecutionBody(request.Id, request.Right, email, request.FirstName, request.LastName);
  }
}
