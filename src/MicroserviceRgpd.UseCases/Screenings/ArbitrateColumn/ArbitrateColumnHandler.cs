using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UseCases.Screenings.ArbitrateColumn;

/// <summary>
/// Porte sur une colonne du dépistage courant l'issue qu'un humain vient de rendre, <b>signée et
/// datée par le service</b>.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Il ne charge du rapport que la ligne qu'il écrit.</b> Voir
/// <see cref="CurrentScreeningColumnSpec"/> : un arbitrage écrit une ligne, et rematérialiser cinq
/// mille colonnes à chaque clic aurait rendu intenable la surface même que ce geste sert.
/// </para>
/// <para>
/// ⚠️ <b>La date vient d'ici, jamais du geste de l'humain.</b> L'horloge est injectée pour que la
/// date d'un acte se dicte en test, et pour aucune autre raison : rien de ce que l'<c>Operator</c>
/// envoie ne la touche.
/// </para>
/// <para>
/// ⚠️ <b>Un archivé n'est pas arbitrable, et c'est la lecture qui le tient</b> plutôt qu'un contrôle
/// à part : seul le courant est chargé, donc seul le courant est écrit. Sans cela un
/// <c>Operator</c> arbitre le mauvais rapport, ce qui est le mode de panne invoqué pour refuser au
/// <c>Screening</c> tout état d'archivage.
/// </para>
/// <para>
/// ⚠️ <b>Et le courant peut avoir changé pendant la lecture.</b> « Courant » se recalcule à
/// l'écriture : entre le rendu de l'écran et le clic, un collègue a pu déposer un relevé. Le geste
/// compare donc le rapport lu à celui qu'il s'apprête à écrire, et <b>refuse</b> plutôt que de
/// signer au nom de l'humain un rapport qu'il n'a pas vu.
/// </para>
/// <para>
/// <b>Rien ne descend nulle part.</b> Aucun <c>EvidenceLog</c> — ce contexte n'en a pas, son grain est le
/// déploiement — et surtout aucun pont vers le <c>Manifest</c> : ce qui est retenu ici le reste ici,
/// c'est la clause <c>Aucune modification vers le Manifest</c>.
/// </para>
/// </remarks>
/// <param name="screenings">Les rapports du déploiement. Seul le courant s'écrit.</param>
/// <param name="clock">L'horloge. C'est elle, et elle seule, qui date un arbitrage.</param>
public sealed class ArbitrateColumnHandler(IRepository<Screening> screenings, TimeProvider clock)
  : ICommandHandler<ArbitrateColumnCommand, Result>
{
  /// <summary>
  /// La fin de tout refus. ⚠️ <b>Sans elle, l'<c>Operator</c> ne sait pas si la colonne a bougé</b> —
  /// et une surface qui laisse un doute sur ce qu'elle a écrit se relit en base à la main.
  /// </summary>
  private const string NothingWasArbitrated =
    "Aucun arbitrage n'a été enregistré : la colonne attend toujours.";

  /// <inheritdoc />
  public async ValueTask<Result> Handle(
    ArbitrateColumnCommand command,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var current = await screenings.FirstOrDefaultAsync(
      new CurrentScreeningColumnSpec(command.Column), cancellationToken);

    if (current is null)
    {
      // Le déploiement n'a lancé aucun dépistage. Ce n'est pas une programmation fautive : un écran
      // affiché il y a une minute peut nommer un rapport qu'un autre geste vient de supprimer.
      return Result.NotFound();
    }

    if (current.Id != command.ReadScreening)
    {
      // ⚠️ Le rapport a changé SOUS LES YEUX de celui qui a cliqué — un collègue a déposé un relevé
      // pendant qu'il relisait. Écrire ici aurait posé sa signature sur un rapport qu'il n'a jamais
      // vu, motifs compris, et le seul indice en aurait été son propre nom. Le geste refuse : c'est
      // la seule branche de ce fichier où l'écriture était possible et où on s'en abstient.
      return Result.Conflict();
    }

    ScreenedColumn? arbitrated;

    try
    {
      // ⚠️ La ligne ne bouge pas si la signature refuse : l'exception sort de la fabrique de
      // l'arbitrage AVANT que la ligne ne la reçoive. Il n'existe donc, jusque dans le mode de
      // panne, ni `Retained` ni `SetAside` non signé.
      arbitrated = current.Arbitrate(
        command.Column, command.Ruling, command.SignedBy, clock.GetUtcNow());
    }
    catch (ArgumentException refusal)
    {
      return Result.Invalid(Refused(refusal));
    }

    if (arbitrated is null)
    {
      // Le courant ne porte pas cette colonne : une adresse mal recopiée, ou un second dépistage
      // sur une base d'où la colonne a disparu.
      return Result.NotFound();
    }

    await screenings.UpdateAsync(current, cancellationToken);

    return Result.Success();
  }

  /// <summary>
  /// Le refus du domaine, redit à l'humain qui a cliqué — <b>sous le nom du champ fautif</b>, et
  /// suivi de ce que le service a fait, c'est-à-dire rien.
  /// </summary>
  /// <remarks>
  /// <b>Le message vient du type du domaine, jamais d'une seconde rédaction.</b> Deux libellés pour
  /// une même règle finiraient par ne plus dire la même chose, et l'écran mentirait sur ce que le
  /// service accepte. Le nom du paramètre qu'<c>ArgumentException</c> accole est retiré : l'écran
  /// nomme déjà le champ à côté de sa case, et le redire en anglais entre parenthèses ferait lire à
  /// l'humain un mot du C#.
  /// </remarks>
  private static ValidationError Refused(ArgumentException refusal)
  {
    return new ValidationError
    {
      // ⚠️ Le champ est celui que l'exception nomme, et non « le signataire » d'office : un
      // formulaire forgé peut poster `Awaiting`, que le domaine refuse comme état — le porter sous
      // le champ du nom aurait affiché « ce n'est pas un arbitrage » à côté de la case du nom.
      Identifier = refusal.ParamName == "ruling"
        ? nameof(ArbitrateColumnCommand.Ruling)
        : nameof(ArbitrateColumnCommand.SignedBy),
      ErrorMessage = $"{refusal.Message.Split(" (Parameter")[0]} {NothingWasArbitrated}",
      Severity = ValidationSeverity.Error,
    };
  }
}
