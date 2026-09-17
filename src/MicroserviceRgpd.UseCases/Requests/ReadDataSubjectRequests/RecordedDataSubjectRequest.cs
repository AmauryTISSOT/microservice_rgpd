using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.UseCases.Requests.ReadDataSubjectRequests;

/// <summary>
/// Une demande telle que la lecture la rend : ce qui s'affiche sur sa ligne, <b>l'origine et le
/// message compris</b> — que le tableau, lui, n'affiche pas.
/// </summary>
/// <remarks>
/// ⚠️ <b>L'origine reste l'<see cref="Origin"/> du domaine</b>, et le message le
/// <see cref="RequestMessage"/> tel qu'il a été enregistré : la lecture ne recompose ni libellé ni
/// texte.
/// </remarks>
/// <param name="Id">L'identité de la demande.</param>
/// <param name="Origin">Le canal par lequel la demande est arrivée, avec son libellé français.</param>
/// <param name="Email">L'email de la personne, ou <c>null</c>.</param>
/// <param name="LastName">Le nom de la personne, ou <c>null</c>.</param>
/// <param name="FirstName">Le prénom de la personne, ou <c>null</c>.</param>
/// <param name="ReceivedOn">La date de réception, déclarée par l'<c>Operator</c>.</param>
/// <param name="ResponseDeadline">La date limite de réponse, telle que la demande la tient : jamais recalculée à la lecture.</param>
/// <param name="IdentityVerified">L'attestation que l'identité a été vérifiée.</param>
/// <param name="Message">Le contenu de la demande tel qu'il a été reçu — jamais absent (ADR-0019).</param>
/// <param name="Right">Le droit invoqué.</param>
/// <param name="CreatedBy">Qui a enregistré la demande.</param>
/// <param name="CreatedAt">L'instant d'enregistrement, en UTC.</param>
/// <param name="Status">Où en est la demande.</param>
/// <param name="Extension">
/// La prolongation de la demande, ou <c>null</c> tant qu'elle n'a pas été prolongée. ⚠️ <b>La date
/// limite rendue reste celle qui fait foi</b> : prolongée, elle porte déjà les deux mois. La
/// prolongation dit ce qui valait avant, et pourquoi (ADR-0029).
/// </param>
/// <param name="ExecutionBlock">
/// Le premier motif de blocage de l'exécution, ou <c>null</c> quand la demande s'exécute — calculé par
/// la demande face au canal d'exercice que le Paramétrage associe à son droit, <b>et à ce que le
/// déploiement sait publier</b> (ADR-0026, ADR-0027, ADR-0028).
/// </param>
public sealed record RecordedDataSubjectRequest(
  DataSubjectRequestId Id,
  Origin Origin,
  EmailAddress? Email,
  LastName? LastName,
  FirstName? FirstName,
  DateOnly ReceivedOn,
  DateOnly ResponseDeadline,
  bool IdentityVerified,
  RequestMessage Message,
  DataSubjectRight Right,
  string CreatedBy,
  DateTimeOffset CreatedAt,
  RequestStatus Status,
  RecordedExtension? Extension,
  ExecutionBlock? ExecutionBlock)
{
  /// <summary>
  /// La demande a-t-elle été prolongée ? ⚠️ <b>C'est la présence de la prolongation qui le dit</b>,
  /// et non une valeur de plus : ses quatre valeurs sont renseignées ensemble ou pas du tout.
  /// </summary>
  public bool Extended => Extension is not null;

  /// <summary>
  /// Ce que la lecture rend d'une demande enregistrée, sous le Paramétrage <paramref name="settings"/>
  /// et la connexion <paramref name="connection"/> que le déploiement déclare.
  /// </summary>
  /// <exception cref="ArgumentNullException"><paramref name="request"/>, <paramref name="settings"/> ou <paramref name="connection"/> est absent.</exception>
  internal static RecordedDataSubjectRequest Of(
    DataSubjectRequest request,
    Settings settings,
    BrokerConnection connection)
  {
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(settings);
    ArgumentNullException.ThrowIfNull(connection);

    return new RecordedDataSubjectRequest(
      request.Id,
      request.Origin,
      request.Email,
      request.LastName,
      request.FirstName,
      request.ReceivedOn,
      request.ResponseDeadline,
      request.IdentityVerified,
      request.Message,
      request.Right,
      request.CreatedBy,
      request.CreatedAt,
      request.Status,
      RecordedExtension.Of(request),
      request.ExecutionBlockFacing(settings.ChannelFor(request.Right), connection));
  }
}
