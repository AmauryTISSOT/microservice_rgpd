using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.UseCases.Requests.ReadDataSubjectRequestExecution;

/// <summary>
/// Le <b>récapitulatif d'une exécution</b> : le droit, la personne telle que le système hôte la
/// recevrait, l'adresse qu'il appellerait, et le premier motif de blocage s'il y en a un.
/// </summary>
/// <remarks>
/// ⚠️ <b>L'adresse est celle du Paramétrage, telle quelle</b> — query string comprise : c'est celle
/// que l'appel viserait. Seul le journal la retient sans query string ni fragment.
/// </remarks>
/// <param name="Right">Le droit invoqué.</param>
/// <param name="FirstName">Le prénom de la personne, ou <c>null</c>.</param>
/// <param name="LastName">Le nom de la personne, ou <c>null</c>.</param>
/// <param name="Email">L'email de la personne, ou <c>null</c>.</param>
/// <param name="Endpoint">L'adresse que le Paramétrage associe au droit, ou <c>null</c> s'il est « non configuré ».</param>
/// <param name="Block">Le premier motif de blocage, ou <c>null</c> quand la demande s'exécute.</param>
public sealed record DataSubjectRequestExecutionSummary(
  DataSubjectRight Right,
  FirstName? FirstName,
  LastName? LastName,
  EmailAddress? Email,
  EndpointUrl? Endpoint,
  ExecutionBlock? Block)
{
  /// <summary>Le récapitulatif de <paramref name="request"/>, sous le Paramétrage <paramref name="settings"/>.</summary>
  /// <exception cref="ArgumentNullException"><paramref name="request"/> ou <paramref name="settings"/> est absent.</exception>
  internal static DataSubjectRequestExecutionSummary Of(DataSubjectRequest request, Settings settings)
  {
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(settings);

    var endpoint = ServiceSettings.HttpAddressFor(settings, request.Right);

    return new DataSubjectRequestExecutionSummary(
      request.Right,
      request.FirstName,
      request.LastName,
      request.Email,
      endpoint,
      request.ExecutionBlockFacing(endpoint));
  }
}
