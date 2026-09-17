using MicroserviceRgpd.Core.Requests;

namespace MicroserviceRgpd.UseCases.Requests.ReadDataSubjectRequests;

/// <summary>
/// La <b>prolongation d'une demande</b>, telle que la lecture la rend : ce qui valait avant, et
/// pourquoi la date limite de réponse a été reportée de deux mois (ADR-0029).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Les quatre valeurs vont ensemble</b> : une demande est prolongée, ou elle ne l'est pas. Les
/// rendre en un seul objet nullable dit cette solidarité, là où quatre valeurs nullables côte à côte
/// laisseraient croire qu'une demande peut porter un motif sans date.
/// </para>
/// <para>
/// ⚠️ <b>La date limite initiale est une trace, pas une source</b> : la date qui fait foi partout
/// ailleurs reste la <c>ResponseDeadline</c> de la demande, qui porte déjà les deux mois.
/// </para>
/// </remarks>
/// <param name="InitialResponseDeadline">La date limite de réponse telle qu'elle valait avant la prolongation.</param>
/// <param name="Ground">Le motif de prolongation, avec son libellé français.</param>
/// <param name="Justification">Le texte qui dit le fait concret, tel que l'<c>Operator</c> l'a écrit.</param>
/// <param name="ExtendedAt">L'instant de la prolongation, en UTC.</param>
public sealed record RecordedExtension(
  DateOnly InitialResponseDeadline,
  ExtensionGround Ground,
  ExtensionJustification Justification,
  DateTimeOffset ExtendedAt)
{
  /// <summary>
  /// La prolongation de <paramref name="request"/>, ou <c>null</c> tant qu'elle n'a pas été
  /// prolongée.
  /// </summary>
  internal static RecordedExtension? Of(DataSubjectRequest request) =>
    request is
    {
      InitialResponseDeadline: { } deadline,
      ExtensionGround: { } ground,
      ExtensionJustification: { } justification,
      ExtendedAt: { } extendedAt,
    }
      ? new RecordedExtension(deadline, ground, justification, extendedAt)
      : null;
}
