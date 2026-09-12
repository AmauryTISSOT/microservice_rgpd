using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.UseCases.Requests.ReadDataSubjectRequestValues;

/// <summary>
/// Les <b>huit valeurs saisies</b> d'une demande, telles qu'elles ont été enregistrées : ce qu'il
/// faut, et rien de plus, pour rouvrir le formulaire sur cette demande.
/// </summary>
/// <remarks>
/// ⚠️ <b>Ni date limite, ni statut, ni empreinte.</b> Ce ne sont pas des valeurs saisies : le
/// formulaire ne les montre pas et ne saurait pas quoi en faire.
/// </remarks>
/// <param name="Origin">Le canal par lequel la demande est arrivée.</param>
/// <param name="ReceivedOn">La date de réception, déclarée par l'<c>Operator</c>.</param>
/// <param name="LastName">Le nom de la personne, ou <c>null</c>.</param>
/// <param name="FirstName">Le prénom de la personne, ou <c>null</c>.</param>
/// <param name="Email">L'email de la personne, ou <c>null</c>.</param>
/// <param name="IdentityVerified">L'attestation que l'identité a été vérifiée.</param>
/// <param name="Message">Le contenu de la demande tel qu'il a été reçu.</param>
/// <param name="Right">Le droit invoqué.</param>
public sealed record DataSubjectRequestValues(
  Origin Origin,
  DateOnly ReceivedOn,
  LastName? LastName,
  FirstName? FirstName,
  EmailAddress? Email,
  bool IdentityVerified,
  RequestMessage Message,
  DataSubjectRight Right)
{
  /// <summary>Ce qu'on relit d'une demande enregistrée pour pré-remplir le formulaire.</summary>
  /// <exception cref="ArgumentNullException"><paramref name="request"/> est absente.</exception>
  internal static DataSubjectRequestValues Of(DataSubjectRequest request)
  {
    ArgumentNullException.ThrowIfNull(request);

    return new DataSubjectRequestValues(
      request.Origin,
      request.ReceivedOn,
      request.LastName,
      request.FirstName,
      request.Email,
      request.IdentityVerified,
      request.Message,
      request.Right);
  }
}
