using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UseCases.Screenings.ArbitrateTableInBatch;

/// <summary>
/// Pose sur la table ouverte du rapport de détection courant les <b>n arbitrages</b> du geste de
/// lot, chacun
/// signé du nom saisi et daté par le service.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Il ne charge du rapport que la table qu'il écrit.</b> Voir
/// <see cref="CurrentScreeningTableSpec"/> : le lot écrit quelques dizaines de lignes, et
/// rematérialiser cinq mille colonnes pour cela aurait alourdi la surface même que ce geste existe
/// pour rendre tenable.
/// </para>
/// <para>
/// ⚠️ <b>Ce qu'il atteint n'est décidé nulle part ici.</b> Le domaine le décide, une fois, sur la
/// ligne — <see cref="ScreenedColumn.IsWithinReachOfABatchGesture"/>. Un second tamis dans ce geste
/// aurait été une seconde rédaction de la règle, et deux rédactions d'une même règle finissent par ne
/// plus dire la même chose.
/// </para>
/// <para>
/// ⚠️ <b>La date vient d'ici, jamais du geste de l'humain</b>, et le lot en pose des dizaines d'un
/// coup. L'horloge est injectée pour que la date d'un acte se dicte en test, et pour aucune autre
/// raison.
/// </para>
/// <para>
/// ⚠️ <b>Un archivé n'est pas arbitrable, et c'est la lecture qui le tient</b> — seul le courant est
/// chargé, donc seul le courant est écrit — <b>et le courant peut avoir changé pendant la
/// lecture</b> : le geste compare le rapport lu à celui qu'il s'apprête à écrire, et refuse plutôt
/// que de signer d'un coup, au nom de l'humain, des dizaines de lignes qu'il n'a pas vues.
/// </para>
/// </remarks>
/// <param name="screenings">Les rapports du déploiement. Seul le courant s'écrit.</param>
/// <param name="clock">L'horloge. C'est elle, et elle seule, qui date un arbitrage.</param>
public sealed class ArbitrateTableInBatchHandler(IRepository<Screening> screenings, TimeProvider clock)
  : ICommandHandler<ArbitrateTableInBatchCommand, Result<BatchArbitration>>
{
  /// <summary>
  /// La fin de tout refus. ⚠️ <b>Sans elle, l'<c>Operator</c> ne sait pas si la table a bougé</b> —
  /// et un lot qui laisse un doute sur ce qu'il a écrit se relit en base à la main, colonne par
  /// colonne, ce qui est le contraire de ce qu'il achète.
  /// </summary>
  private const string NothingWasArbitrated =
    "Aucun arbitrage n'a été enregistré : les colonnes de cette table attendent toujours.";

  /// <inheritdoc />
  public async ValueTask<Result<BatchArbitration>> Handle(
    ArbitrateTableInBatchCommand command,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var current = await screenings.FirstOrDefaultAsync(
      new CurrentScreeningTableSpec(command.Table), cancellationToken);

    if (current is null)
    {
      // Le déploiement n'a lancé aucune détection. Ce n'est pas une programmation fautive : un écran
      // affiché il y a une minute peut nommer un rapport qu'un autre geste vient de supprimer.
      return Result<BatchArbitration>.NotFound();
    }

    if (current.Id != command.ReadScreening)
    {
      // ⚠️ Le rapport a changé SOUS LES YEUX de celui qui a cliqué — un collègue a déposé un relevé
      // pendant qu'il relisait. Écrire ici aurait posé sa signature d'un seul coup sur des dizaines
      // de lignes d'un rapport qu'il n'a jamais vu, motifs compris.
      return Result<BatchArbitration>.Conflict();
    }

    var inTheTable = current.ColumnsOf(command.Table);

    if (inTheTable.Count == 0)
    {
      // Le courant ne porte pas cette table : une adresse mal recopiée, ou une seconde détection sur
      // une base d'où la table a disparu. Le cas ne se confond avec aucun autre — un relevé ne nomme
      // jamais une table sans colonne.
      return Result<BatchArbitration>.NotFound();
    }

    // ⚠️ Il se lit AVANT le lot, sur la table telle qu'elle était. Le lot n'atteint aucune signalée,
    // si bien que ce compte ne bougera pas — mais le lire avant est ce qui le rend indépendant de
    // cette promesse plutôt que suspendu à elle.
    var flaggedStillAwaiting = inTheTable.Count(column =>
      column.IsFlagged && column.AwaitsAnArbitration);

    IReadOnlyList<ScreenedColumn> arbitrated;

    try
    {
      // ⚠️ Aucune ligne ne bouge si la signature refuse : le domaine lève avant d'écrire la première.
      // Un lot à moitié posé laisserait l'Operator devant un refus sans savoir où le geste s'est
      // arrêté.
      arbitrated = current.ArbitrateInBatch(
        command.Table, command.Ruling, command.SignedBy, clock.GetUtcNow());
    }
    catch (ArgumentException refusal)
    {
      return Result<BatchArbitration>.Invalid(Refused(refusal));
    }

    await screenings.UpdateAsync(current, cancellationToken);

    return Result<BatchArbitration>.Success(
      new BatchArbitration(arbitrated.Count, flaggedStillAwaiting));
  }

  /// <summary>
  /// Le refus du domaine, redit à l'humain qui a cliqué — <b>sous le nom du champ fautif</b>, et
  /// suivi de ce que le service a fait, c'est-à-dire rien.
  /// </summary>
  /// <remarks>
  /// <b>Le message vient du type du domaine, jamais d'une seconde rédaction.</b> Deux libellés pour
  /// une même règle finiraient par ne plus dire la même chose, et l'écran mentirait sur ce que le
  /// service accepte.
  /// </remarks>
  private static ValidationError Refused(ArgumentException refusal)
  {
    return new ValidationError
    {
      Identifier = refusal.ParamName == "ruling"
        ? nameof(ArbitrateTableInBatchCommand.Ruling)
        : nameof(ArbitrateTableInBatchCommand.SignedBy),
      ErrorMessage = $"{refusal.Message.Split(" (Parameter")[0]} {NothingWasArbitrated}",
      Severity = ValidationSeverity.Error,
    };
  }
}
