using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UseCases.Screenings.ReadArchivedScreening;

/// <summary>
/// Rend le sommaire d'un rapport archivé, <b>clause comprise</b> — ou rien du tout quand le rapport
/// nommé n'existe plus, ou quand c'est le courant.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Deux lectures, et la seconde n'est pas un luxe.</b> Le rapport nommé se charge avec ses
/// colonnes ; les entêtes du déploiement se chargent pour <b>calculer</b> s'il est archivé, parce
/// qu'un rapport ne voit pas ses frères — <see cref="Screening.IsArchivedAmong"/> lève plutôt que de
/// deviner, et c'est ce qui rend ce calcul sûr. La seconde lecture coûte quelques dizaines de
/// lignes sans colonne.
/// </para>
/// <para>
/// ⚠️ <b>Aucune écriture n'est possible depuis ce chemin</b>, et pas seulement parce qu'on n'en
/// écrit pas : ce qu'il rend est un type sans geste, et le geste d'arbitrage, lui, ne sait charger
/// que le courant.
/// </para>
/// </remarks>
/// <param name="screenings">Les rapports du déploiement, en lecture seule.</param>
public sealed class ReadArchivedScreeningHandler(IReadRepository<Screening> screenings)
  : IQueryHandler<ReadArchivedScreeningQuery, ScreeningAnswer<ArchivedScreeningReport>?>
{
  /// <inheritdoc />
  public async ValueTask<ScreeningAnswer<ArchivedScreeningReport>?> Handle(
    ReadArchivedScreeningQuery query,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    var deployment = await screenings.ListAsync(new ScreeningHistorySpec(), cancellationToken);

    // Le rapport nommé n'est plus là : un écran affiché il y a une minute peut nommer un rapport
    // qu'un geste vient de supprimer.
    if (!deployment.Any(screening => screening.Id == query.Screening))
    {
      return null;
    }

    var current = Screening.CurrentAmong(deployment);

    // C'est le courant : il a son écran, où il s'arbitre. Le servir ici l'aurait montré comme un
    // document figé à un Operator qui a du travail dessus.
    if (current!.Id == query.Screening)
    {
      return null;
    }

    var archived = await screenings.FirstOrDefaultAsync(
      new ScreeningByIdSpec(query.Screening), cancellationToken);

    // Supprimé entre les deux lectures. Rien ne verrouille un rapport pendant qu'on le lit, et rien
    // ne le doit : la suppression est un geste de l'Operator, immédiat et sans cérémonie.
    if (archived is null)
    {
      return null;
    }

    return new ScreeningAnswer<ArchivedScreeningReport>(
      ArchivedScreeningReport.Of(archived),
      IncompletenessClause.For(archived));
  }
}
