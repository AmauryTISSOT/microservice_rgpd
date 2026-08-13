using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UseCases.Screenings.ReadCurrentScreening;

/// <summary>
/// Rend le sommaire du rapport courant, <b>clause comprise</b> — ou rien du tout quand le
/// déploiement n'a lancé aucun dépistage.
/// </summary>
/// <remarks>
/// ⚠️ <b>La clause est attachée ici et nulle part ailleurs.</b> <see cref="ScreeningAnswer{T}"/> n'a
/// pas d'autre constructeur que celui qui l'exige : aucun chemin ne construit une réponse sans elle,
/// et le rendu n'a donc jamais à se rappeler de la joindre.
/// </remarks>
/// <param name="screenings">Les rapports du déploiement, en lecture seule.</param>
public sealed class ReadCurrentScreeningHandler(IReadRepository<Screening> screenings)
  : IQueryHandler<ReadCurrentScreeningQuery, ScreeningAnswer<ScreeningSummary>?>
{
  /// <inheritdoc />
  public async ValueTask<ScreeningAnswer<ScreeningSummary>?> Handle(
    ReadCurrentScreeningQuery query,
    CancellationToken cancellationToken)
  {
    var current = await screenings.FirstOrDefaultAsync(new CurrentScreeningSpec(), cancellationToken);

    if (current is null)
    {
      return null;
    }

    // Les comptes de la clause portent sur CE relevé — combien de colonnes, combien de tables,
    // combien sans le moindre commentaire. Ils ne sont pas figés à l'ingestion : ils se
    // recalculent, comme tout le reste de ce rendu.
    return new ScreeningAnswer<ScreeningSummary>(
      ScreeningSummary.Of(current),
      IncompletenessClause.For(current));
  }
}
