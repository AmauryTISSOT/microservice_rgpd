using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.UseCases.Requests.ReadDataSubjectRequestExecution;

/// <summary>
/// Le <b>récapitulatif d'une exécution</b> : le droit, la personne telle que le système hôte la
/// recevrait, <b>par où</b> la demande partirait, et le premier motif de blocage s'il y en a un.
/// </summary>
/// <remarks>
/// ⚠️ <b>Le canal est celui du Paramétrage, tel quel</b> — une adresse l'est query string comprise :
/// c'est celle que l'appel viserait. Seul le journal la retient sans query string ni fragment.
/// </remarks>
/// <param name="Right">Le droit invoqué.</param>
/// <param name="FirstName">Le prénom de la personne, ou <c>null</c>.</param>
/// <param name="LastName">Le nom de la personne, ou <c>null</c>.</param>
/// <param name="Email">L'email de la personne, ou <c>null</c>.</param>
/// <param name="Exercise">Le canal d'exercice que le Paramétrage associe au droit — une adresse, un routage, ou « non configuré ».</param>
/// <param name="Block">Le premier motif de blocage, ou <c>null</c> quand la demande s'exécute.</param>
public sealed record DataSubjectRequestExecutionSummary(
  DataSubjectRight Right,
  FirstName? FirstName,
  LastName? LastName,
  EmailAddress? Email,
  ExerciseChannel Exercise,
  ExecutionBlock? Block)
{
  /// <summary>Le récapitulatif de <paramref name="request"/>, sous le Paramétrage <paramref name="settings"/>.</summary>
  /// <exception cref="ArgumentNullException"><paramref name="request"/> ou <paramref name="settings"/> est absent.</exception>
  internal static DataSubjectRequestExecutionSummary Of(DataSubjectRequest request, Settings settings)
  {
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(settings);

    var channel = settings.ChannelFor(request.Right);

    return new DataSubjectRequestExecutionSummary(
      request.Right,
      request.FirstName,
      request.LastName,
      request.Email,
      channel,
      request.ExecutionBlockFacing(channel));
  }
}
