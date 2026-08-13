using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UseCases.Screenings.ReadScreeningTable;

/// <summary>
/// Rend une table du dépistage courant, <b>clause comprise</b> — ou rien du tout quand aucun
/// dépistage n'a été lancé, ou quand le courant ne porte pas cette table.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Il ne charge pas le rapport.</b> L'entête arrive seul, les colonnes de la table par leur
/// propre <c>DbSet</c>, et les comptes du rapport entier par la base : c'est exactement ce que la
/// seconde table de la persistance achète, et le seul chemin qui rende treize colonnes sans en
/// rematérialiser cinq mille à chaque rafraîchissement.
/// </para>
/// <para>
/// ⚠️ <b>La clause est attachée ici, et elle porte sur le rapport entier.</b> Une clause bornée à la
/// table ouverte aurait dit d'un écran de treize colonnes qu'il est le périmètre lu du dépistage.
/// </para>
/// </remarks>
/// <param name="screenings">Les rapports du déploiement, en lecture seule.</param>
/// <param name="columns">Les colonnes du rapport, lues par table.</param>
public sealed class ReadScreeningTableHandler(
  IReadRepository<Screening> screenings,
  IScreenedColumns columns)
  : IQueryHandler<ReadScreeningTableQuery, ScreeningAnswer<ScreenedTable>?>
{
  /// <inheritdoc />
  public async ValueTask<ScreeningAnswer<ScreenedTable>?> Handle(
    ReadScreeningTableQuery query,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    var current = await screenings.FirstOrDefaultAsync(
      new CurrentScreeningHeaderSpec(), cancellationToken);

    if (current is null)
    {
      return null;
    }

    var read = await columns.OfTableAsync(current.Id, query.Table, cancellationToken);

    // Le courant ne porte pas cette table : une adresse mal recopiée, ou un second dépistage sur une
    // base d'où la table a disparu. Une table vide portant la clause aurait fait passer l'un pour
    // l'autre.
    if (read.Count == 0)
    {
      return null;
    }

    var counts = await columns.CountsOfAsync(current.Id, cancellationToken);

    return new ScreeningAnswer<ScreenedTable>(
      ScreenedTable.Of(current, query.Table, read, counts),
      IncompletenessClause.For(counts));
  }
}
