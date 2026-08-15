using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UseCases.Screenings.ReadArchivedScreeningTable;

/// <summary>
/// Rend une table d'un rapport archivé, <b>clause comprise</b> — ou rien du tout quand le rapport
/// n'existe plus, quand c'est le courant, ou quand il ne porte pas cette table.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Il ne charge pas le rapport</b>, exactement comme la lecture d'une table du courant :
/// l'entête arrive seul, les colonnes de la table par leur propre <c>DbSet</c>, et les comptes du
/// rapport entier par la base. Un archivé n'est pas plus petit qu'un courant.
/// </para>
/// <para>
/// <b>La clause est attachée ici, et elle porte sur le rapport entier.</b> Un archivé n'est pas
/// dispensé de dire ce qu'il n'a pas regardé — c'est même sur un rapport ancien que la tentation de
/// le lire comme un recensement est la plus forte.
/// </para>
/// </remarks>
/// <param name="screenings">Les rapports du déploiement, en lecture seule.</param>
/// <param name="columns">Les colonnes du rapport, lues par table.</param>
public sealed class ReadArchivedScreeningTableHandler(
  IReadRepository<Screening> screenings,
  IScreenedColumns columns)
  : IQueryHandler<ReadArchivedScreeningTableQuery, ScreeningAnswer<ArchivedTable>?>
{
  /// <inheritdoc />
  public async ValueTask<ScreeningAnswer<ArchivedTable>?> Handle(
    ReadArchivedScreeningTableQuery query,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    var deployment = await screenings.ListAsync(new ScreeningHistorySpec(), cancellationToken);

    var archived = deployment.FirstOrDefault(screening => screening.Id == query.Screening);

    // Le rapport n'est plus là, ou c'est le courant — qui se lit sur son propre écran, où il
    // s'arbitre. « Courant » reste un calcul sur le lot, demandé au domaine plutôt que refait ici.
    if (archived is null || !archived.IsArchivedAmong(deployment))
    {
      return null;
    }

    var read = await columns.OfTableAsync(query.Screening, query.Table, cancellationToken);

    // Ce rapport-là ne portait pas cette table : une adresse mal recopiée, ou une table qui
    // n'existait pas encore quand ce rapport de détection a été lancé. Une table vide portant la
    // clause aurait
    // fait passer l'un pour l'autre.
    if (read.Count == 0)
    {
      return null;
    }

    var counts = await columns.CountsOfAsync(query.Screening, cancellationToken);

    return new ScreeningAnswer<ArchivedTable>(
      ArchivedTable.Of(archived, query.Table, read, counts),
      IncompletenessClause.For(counts));
  }
}
