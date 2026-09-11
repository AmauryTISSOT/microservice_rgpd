using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.UseCases.Requests.ReadDataSubjectRequests;

/// <summary>
/// Une demande telle que le tableau la rend : ce qui s'affiche sur sa ligne, et rien de plus.
/// </summary>
/// <remarks>
/// ⚠️ <b>Le message n'y est pas.</b> Le tableau ne l'affiche pas : il se lira sur la fiche de la
/// demande.
/// </remarks>
/// <param name="Id">L'identité de la demande.</param>
/// <param name="Email">L'email de la personne, ou <c>null</c>.</param>
/// <param name="LastName">Le nom de la personne, ou <c>null</c>.</param>
/// <param name="FirstName">Le prénom de la personne, ou <c>null</c>.</param>
/// <param name="ReceivedOn">La date de réception, déclarée par l'<c>Operator</c>.</param>
/// <param name="IdentityVerified">L'attestation que l'identité a été vérifiée.</param>
/// <param name="Right">Le droit invoqué.</param>
/// <param name="CreatedBy">Qui a enregistré la demande.</param>
/// <param name="CreatedAt">L'instant d'enregistrement, en UTC.</param>
public sealed record RecordedDataSubjectRequest(
  DataSubjectRequestId Id,
  EmailAddress? Email,
  LastName? LastName,
  FirstName? FirstName,
  DateOnly ReceivedOn,
  bool IdentityVerified,
  DataSubjectRight Right,
  string CreatedBy,
  DateTimeOffset CreatedAt)
{
  /// <summary>La ligne d'une demande enregistrée.</summary>
  /// <exception cref="ArgumentNullException"><paramref name="request"/> est absente.</exception>
  public static RecordedDataSubjectRequest Of(DataSubjectRequest request)
  {
    ArgumentNullException.ThrowIfNull(request);

    return new RecordedDataSubjectRequest(
      request.Id,
      request.Email,
      request.LastName,
      request.FirstName,
      request.ReceivedOn,
      request.IdentityVerified,
      request.Right,
      request.CreatedBy,
      request.CreatedAt);
  }
}
