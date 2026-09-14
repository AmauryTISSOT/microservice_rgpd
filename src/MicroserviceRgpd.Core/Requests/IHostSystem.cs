using MicroserviceRgpd.Core.Configuration;

namespace MicroserviceRgpd.Core.Requests;

/// <summary>
/// Le <b>système hôte</b>, tel que <c>Requests</c> l'appelle pour faire appliquer un droit : un
/// <c>POST</c> JSON synchrone à l'adresse que le Paramétrage associe au droit (ADR-0026).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Il ne lève pas pour un échec de l'appel</b> : une réponse non 2xx, un délai dépassé ou une
/// erreur réseau sont des <see cref="HostSystemCall"/> comme les autres, que le journal d'exécution
/// retient. Seule une faute de programmation lève.
/// </para>
/// <para>
/// ⚠️ <b>Aucune nouvelle tentative</b> : un appel, une réponse. Aucun droit n'est appliqué sans le geste
/// de l'<c>Operator</c>.
/// </para>
/// </remarks>
public interface IHostSystem
{
  /// <summary>
  /// Demande au système hôte, à <paramref name="endpoint"/>, d'appliquer le droit que porte
  /// <paramref name="body"/>, et rend ce que l'appel a donné.
  /// </summary>
  Task<HostSystemCall> ApplyAsync(EndpointUrl endpoint, ExecutionBody body, CancellationToken cancellationToken);
}
