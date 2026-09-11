namespace MicroserviceRgpd.UseCases.Requests.ReadDataSubjectRequests;

/// <summary>
/// Relire <b>toutes les demandes enregistrées</b>, dans l'ordre par défaut du tableau : la date de
/// réception la plus récente d'abord, puis la date de création la plus récente.
/// </summary>
/// <remarks>
/// <b>Aucun paramètre.</b> Le tableau rend toutes les demandes, sans pagination ; la recherche et le
/// choix du tri se feront sur les lignes déjà rendues, sans revenir au serveur.
/// </remarks>
public sealed record ReadDataSubjectRequestsQuery : IQuery<IReadOnlyList<RecordedDataSubjectRequest>>;
