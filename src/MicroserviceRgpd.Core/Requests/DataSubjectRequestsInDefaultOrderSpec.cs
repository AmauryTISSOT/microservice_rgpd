namespace MicroserviceRgpd.Core.Requests;

/// <summary>
/// <b>Toutes</b> les demandes enregistrées, dans l'<b>ordre par défaut</b> du tableau : la date de
/// réception la plus récente d'abord, puis, pour deux demandes reçues le même jour, la plus récemment
/// enregistrée.
/// </summary>
/// <remarks>
/// <b>Elle n'est pas bornée.</b> Le tableau rend toutes les demandes, sans pagination : une demande
/// cachée derrière un bouton est une demande qu'on oublie de traiter.
/// </remarks>
public sealed class DataSubjectRequestsInDefaultOrderSpec : Specification<DataSubjectRequest>
{
  public DataSubjectRequestsInDefaultOrderSpec()
  {
    Query.OrderByDescending(request => request.ReceivedOn)
      .ThenByDescending(request => request.CreatedAt)
      .AsNoTracking();
  }
}
