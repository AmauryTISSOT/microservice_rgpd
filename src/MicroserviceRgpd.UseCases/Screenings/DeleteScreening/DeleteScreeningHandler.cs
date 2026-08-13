using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UseCases.Screenings.DeleteScreening;

/// <summary>
/// Supprime le rapport nommé et, <b>avec lui, toutes ses colonnes</b>.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>La cascade est tenue par la base, et le geste ne charge pas les colonnes.</b> Un rapport
/// de vingt mille lignes se supprime par un <c>delete</c> sur sa seule ligne racine ; les
/// rematérialiser pour les effacer une par une aurait fait payer au geste le coût entier du rapport
/// pour n'écrire nulle part. Laisser des colonnes orphelines aurait fait survivre les arbitrages
/// d'un rapport qui n'existe plus — voir <c>ScreeningConfiguration</c>, où la cascade est déclarée.
/// </para>
/// <para>
/// ⚠️ <b>Le rang du rapport se lit AVANT la suppression, et il ne sert qu'à le dire.</b> Après le
/// geste, « était-ce le courant ? » n'a plus de réponse : le calcul porte sur un lot dont il vient
/// de sortir.
/// </para>
/// <para>
/// <b>Rien n'est différé.</b> Pas de marquage, pas de corbeille, pas de purge nocturne : le geste
/// supprime, et il a fini quand l'<c>Operator</c> relâche le bouton.
/// </para>
/// </remarks>
/// <param name="screenings">Les rapports du déploiement.</param>
public sealed class DeleteScreeningHandler(IRepository<Screening> screenings)
  : ICommandHandler<DeleteScreeningCommand, Result<DeletedScreening>>
{
  /// <inheritdoc />
  public async ValueTask<Result<DeletedScreening>> Handle(
    DeleteScreeningCommand command,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var deleted = await screenings.FirstOrDefaultAsync(
      new ScreeningHeaderByIdSpec(command.Screening), cancellationToken);

    if (deleted is null)
    {
      // Deux écrans ouverts sur le même déploiement, et l'autre a supprimé le premier. Ce n'est pas
      // une programmation fautive : c'est le geste concurrent le plus banal de cette surface.
      return Result<DeletedScreening>.NotFound();
    }

    // ⚠️ LA CONFRONTATION SE FAIT ICI, contre le nom que le service détient — jamais contre un champ
    // caché reposté, qui aurait fait de la confirmation une cérémonie sans juge. Et elle se fait
    // AVANT la lecture du lot : rien ne doit avoir lieu quand le geste ne va pas avoir lieu.
    if (!string.Equals(command.ConfirmedDatabase?.Trim(), deleted.Database, StringComparison.Ordinal))
    {
      return Result<DeletedScreening>.Invalid(NotConfirmed(deleted.Database));
    }

    // ⚠️ Le lot se lit pour le seul mot que l'écran a besoin de dire — « c'était le courant » — et
    // il se lit MAINTENANT, pendant que la question a encore une réponse.
    var deployment = await screenings.ListAsync(new ScreeningHistorySpec(), cancellationToken);

    var wasCurrent = deployment.Any(screening => screening.Id == command.Screening)
      && deleted.IsCurrentAmong(deployment);

    await screenings.DeleteAsync(deleted, cancellationToken);

    return new DeletedScreening(
      deleted.Database,
      deleted.LaunchedOn,
      deleted.DeclaredColumnCount,
      wasCurrent);
  }

  /// <summary>
  /// Le refus de la confirmation, <b>et ce que le service a fait, c'est-à-dire rien</b>.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Il redit le nom attendu.</b> Un refus qui dirait seulement « le nom ne correspond pas »
  /// ferait chercher à l'<c>Operator</c> lequel des deux il a mal tapé, sur un écran qui en porte
  /// plusieurs — et le seul geste raisonnable après ce refus est de retaper devant le bon nom.
  /// </remarks>
  private static ValidationError NotConfirmed(string database)
  {
    return new ValidationError
    {
      Identifier = nameof(DeleteScreeningCommand.ConfirmedDatabase),
      ErrorCode = "DeletionNotConfirmed",
      ErrorMessage =
        $"Rien n'a été supprimé : pour supprimer ce rapport, retapez le nom de sa base — "
        + $"« {database} ». La suppression emporte le rapport, toutes ses colonnes et tous leurs "
        + "arbitrages, et rien ne les rétablit.",
      Severity = ValidationSeverity.Error,
    };
  }
}
