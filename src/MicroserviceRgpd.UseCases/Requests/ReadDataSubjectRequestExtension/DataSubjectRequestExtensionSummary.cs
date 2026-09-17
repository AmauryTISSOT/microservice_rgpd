using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.UseCases.Requests.ReadDataSubjectRequestExtension;

/// <summary>
/// Le <b>récapitulatif d'une prolongation</b> : la demande telle que l'<c>Operator</c> la relit avant
/// de décider, la date limite <b>en vigueur</b>, et celle qui <b>en résultera</b> (ADR-0029).
/// </summary>
/// <remarks>
/// ⚠️ <b>Les deux dates viennent du serveur, toujours</b> : <c>Date.setMonth</c> ne fait pas le même
/// repli de fin de mois que <see cref="DateOnly.AddMonths"/> — un 31 décembre plus deux mois donne
/// 3 mars dans le navigateur, et 28 février dans le domaine. La modale annoncerait une date, le
/// serveur en écrirait une autre.
/// </remarks>
/// <param name="Right">Le droit invoqué.</param>
/// <param name="FirstName">Le prénom de la personne, ou <c>null</c>.</param>
/// <param name="LastName">Le nom de la personne, ou <c>null</c>.</param>
/// <param name="Email">L'email de la personne, ou <c>null</c>.</param>
/// <param name="ResponseDeadline">La date limite de réponse en vigueur — celle qui deviendra la date limite initiale.</param>
/// <param name="ExtendedResponseDeadline">La date limite de réponse qui résultera de la prolongation.</param>
/// <param name="Block">
/// Le premier motif de blocage, ou <c>null</c> quand la demande se prolonge. ⚠️ <b>La modale s'ouvre
/// quand même</b> : c'est le seul endroit où le motif se dit à un <c>Operator</c> tactile ou au
/// lecteur d'écran, et la confirmation y est éteinte.
/// </param>
public sealed record DataSubjectRequestExtensionSummary(
  DataSubjectRight Right,
  FirstName? FirstName,
  LastName? LastName,
  EmailAddress? Email,
  DateOnly ResponseDeadline,
  DateOnly ExtendedResponseDeadline,
  ExtensionBlock? Block)
{
  /// <summary>Le récapitulatif de <paramref name="request"/>, tel qu'il se lit <paramref name="todayInParis"/>.</summary>
  /// <remarks>
  /// ⚠️ <b>Les deux dates sont rendues même quand la demande est bloquée</b> : l'<c>Operator</c> lit
  /// ce que la prolongation aurait donné, et pourquoi elle n'aura pas lieu.
  /// </remarks>
  /// <param name="request">La demande.</param>
  /// <param name="todayInParis">Aujourd'hui à Paris — voir <see cref="ParisCalendar"/>.</param>
  /// <exception cref="ArgumentNullException"><paramref name="request"/> est absente.</exception>
  internal static DataSubjectRequestExtensionSummary Of(DataSubjectRequest request, DateOnly todayInParis)
  {
    ArgumentNullException.ThrowIfNull(request);

    return new DataSubjectRequestExtensionSummary(
      request.Right,
      request.FirstName,
      request.LastName,
      request.Email,
      request.ResponseDeadline,
      DataSubjectRequest.DeadlineExtendedFrom(request.ResponseDeadline),
      request.ExtensionBlockFacing(todayInParis));
  }
}
