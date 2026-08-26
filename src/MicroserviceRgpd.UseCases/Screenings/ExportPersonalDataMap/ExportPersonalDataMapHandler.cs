using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UseCases.Screenings.ExportPersonalDataMap;

/// <summary>
/// Calcule la cartographie du rapport courant à l'instant où l'<c>Operator</c> la demande — ou rien
/// du tout quand aucune détection n'a été lancée.
/// </summary>
/// <remarks>
/// <para>
/// <b>Le rapport est chargé avec toutes ses colonnes</b>, et c'est la seule lecture de ce contexte
/// qui en ait besoin en entier : le fichier porte une ligne par colonne, sans exception. La
/// spécification du rapport courant les inclut déjà, et <see cref="ScreeningCounts.Of"/> lève
/// bruyamment si jamais elle cessait de le faire.
/// </para>
/// <para>
/// ⚠️ <b>Il ne lit rien d'autre et n'écrit nulle part.</b> Aucune trace de l'export n'est posée : ce
/// serait un enregistrement dont personne n'a besoin, et le premier pas vers le destinataire machine
/// que la <c>Cartographie</c> refuse d'avoir.
/// </para>
/// </remarks>
/// <param name="screenings">Les rapports du déploiement, en lecture seule.</param>
/// <param name="clock">
/// L'horloge du service. ⚠️ <b>La date d'export est l'instant de la demande</b>, pas celle du
/// lancement : les deux vivent côte à côte dans l'en-tête JSON, et l'écart entre elles est
/// exactement ce que le destinataire doit voir.
/// </param>
public sealed class ExportPersonalDataMapHandler(
  IReadRepository<Screening> screenings,
  TimeProvider clock)
  : IQueryHandler<ExportPersonalDataMapQuery, PersonalDataMap?>
{
  /// <inheritdoc />
  public async ValueTask<PersonalDataMap?> Handle(
    ExportPersonalDataMapQuery query,
    CancellationToken cancellationToken)
  {
    var current = await screenings.FirstOrDefaultAsync(new CurrentScreeningSpec(), cancellationToken);

    return current is null
      ? null
      : PersonalDataMap.Of(current, clock.GetUtcNow());
  }
}
