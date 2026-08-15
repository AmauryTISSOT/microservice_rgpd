using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UseCases.Screenings.ReadScreeningHistory;

/// <summary>
/// Rend l'historique du déploiement : les rapports archivés, du plus récent au plus ancien.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Il lit tout le déploiement pour en retrancher un.</b> C'est la seule façon juste : « le
/// courant » n'est pas une ligne qu'on reconnaît, c'est le maximum d'un lot. Le coût est celui de
/// quelques dizaines d'entêtes — aucune colonne n'est chargée, et un déploiement ne porte jamais
/// plus d'une poignée de rapports.
/// </para>
/// <para>
/// <b>Il rend une liste vide plutôt que rien</b> quand le déploiement n'a lancé qu'une détection, ou
/// aucun : « il n'y a pas d'historique » est une réponse, et l'écran la rend en toutes lettres.
/// </para>
/// </remarks>
/// <param name="screenings">Les rapports du déploiement, en lecture seule.</param>
public sealed class ReadScreeningHistoryHandler(IReadRepository<Screening> screenings)
  : IQueryHandler<ReadScreeningHistoryQuery, ScreeningHistory>
{
  /// <inheritdoc />
  public async ValueTask<ScreeningHistory> Handle(
    ReadScreeningHistoryQuery query,
    CancellationToken cancellationToken)
  {
    var deployment = await screenings.ListAsync(new ScreeningHistorySpec(), cancellationToken);

    return ScreeningHistory.Of(deployment);
  }
}
