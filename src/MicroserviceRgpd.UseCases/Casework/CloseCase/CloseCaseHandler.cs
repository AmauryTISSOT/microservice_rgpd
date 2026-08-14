using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.EvidenceLog;

namespace MicroserviceRgpd.UseCases.Casework.CloseCase;

/// <summary>
/// Clôt le dossier, <b>détruit tout le nominatif</b> et consigne la clôture sous le nom de l'humain
/// qui l'a signée.
/// </summary>
/// <remarks>
/// <para>
/// <b>Tout ce qui nomme tombe en une seule écriture.</b> Les <c>Designations</c>, les
/// <c>Locatings</c> et ce qu'ils portaient, les <c>OpenQuestion</c>, le détail de la motivation :
/// tout vit <b>dans l'agrégat</b>, et un seul <c>UpdateAsync</c> les emporte ensemble. C'est la
/// raison pour laquelle ils y vivent — un nominatif éparpillé se serait détruit en plusieurs
/// écritures, dont l'une peut échouer.
/// </para>
/// <para>
/// <b>L'ordre est : écrire le dossier, consigner, puis détruire les pièces</b> — le même qu'à la
/// remise, et pour la même raison. Les <c>RetrievedData</c> vivent hors de l'agrégat, avec leur
/// durée de vie propre ; elles sont jetées en dernier, quand la preuve est déjà écrite. Une panne
/// entre les deux laisse des pièces détenues un moment de trop — visible, et réparable — plutôt
/// qu'un dossier clos dont rien ne dirait qu'il l'a été.
/// </para>
/// <para>
/// ⚠️ <b>Le <c>EvidenceLog</c> n'est pas touché par la clôture</b>, et continue de nommer
/// l'<c>Operator</c> : la preuve d'une procédure ne peut pas dépendre du consentement de qui l'a
/// instruite, et son effacement se refuse légitimement. Elle vivra cinq ans à compter d'ici.
/// </para>
/// <para>
/// ⚠️ <b>Aucun <c>Claim</c> n'est répondu, aucun <c>Step</c> n'est déclaré.</b> La clôture ne
/// propage rien : elle réclame — l'écran l'a fait avant de laisser signer — et se laisse faire.
/// </para>
/// </remarks>
/// <param name="cases">Le seul dépôt de ce contexte : les règles sont écrites une fois, sur la racine.</param>
/// <param name="retrieved">Les pièces détenues, hors de l'agrégat : c'est ici qu'elles meurent toutes.</param>
/// <param name="ledger">La matière de preuve, en ajout seul — elle survit au dossier de cinq ans.</param>
/// <param name="clock">L'horloge, injectée pour que la date d'une clôture se dicte en test.</param>
public sealed class CloseCaseHandler(
  IRepository<Case> cases,
  IRetrievedData retrieved,
  IEvidenceLog ledger,
  TimeProvider clock)
  : ICommandHandler<CloseCaseCommand, Result>
{
  /// <inheritdoc />
  public async ValueTask<Result> Handle(CloseCaseCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var opened = await cases.FirstOrDefaultAsync(new CaseByIdSpec(command.Case), cancellationToken);

    if (opened is null)
    {
      return Result.NotFound();
    }

    // Le motif est éprouvé à part de la signature pour que le refus se dépose sous le nom de SON
    // champ : un « motif manquant » affiché à côté de la case du nom ferait chercher au mauvais
    // endroit celui qui vient de saisir.
    string? motive;

    try
    {
      motive = EvidenceLogEntry.MotiveOrThrow(command.Motive, command.Cause);
    }
    catch (ArgumentException refusal)
    {
      return Result.Invalid(Named(nameof(CloseCaseCommand.Motive), refusal));
    }

    // La ligne de preuve est forgée avant que rien ne bouge : c'est elle qui exige un nom, et une
    // clôture sans signataire ne doit rien détruire du tout.
    EvidenceLogEntry signed;

    try
    {
      signed = EvidenceLogEntry.CaseClosed(
        command.Case,
        clock.GetUtcNow(),
        command.Cause,
        motive,
        Signatory.Operator(command.SignedBy, SignerVerification.Unauthenticated));
    }
    catch (ArgumentException refusal)
    {
      return Result.Invalid(Named(nameof(CloseCaseCommand.SignedBy), refusal));
    }

    // Déjà clos : ce n'est pas une programmation fautive, mais un écran affiché il y a une minute.
    // Reclore réécrirait la cause et la date qu'un humain a signées, sans rien rendre du détruit.
    if (!opened.Close(command.Cause, signed.OccurredAt))
    {
      return Result.NotFound();
    }

    // Une seule écriture, et tout le nominatif du dossier tombe avec elle.
    await cases.UpdateAsync(opened, cancellationToken);

    await ledger.AppendAsync(signed, cancellationToken);

    // En dernier, et au grain du dossier : ce sont les données de la personne telles que les
    // systèmes du client les ont rendues — les plus concentrées du dispositif, et les dernières à
    // pouvoir survivre à une clôture.
    await retrieved.DiscardAllAsync(command.Case, cancellationToken);

    return Result.Success();
  }

  /// <summary>
  /// Le refus d'un type du domaine, déposé sous le nom du champ que l'écran affiche.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Le message porte le nom du paramètre entre parenthèses, façon <c>ArgumentException</c> : il est
  /// retiré, l'écran nommant déjà le champ fautif à côté de sa case.
  /// </para>
  /// <para>
  /// <b>Elle est privée, et le reste.</b> Ce gestionnaire est le seul du dispositif à refuser sur
  /// <b>deux</b> champs distincts ; partout ailleurs — <c>AnswerClaimHandler</c> compris — un seul
  /// refus est possible et la <c>ValidationError</c> s'écrit sur place, comme le fait le dépôt
  /// depuis toujours. La remonter en utilitaire partagé pour un unique appelant à deux appels
  /// serait généraliser sur une paire.
  /// </para>
  /// </remarks>
  private static ValidationError Named(string field, ArgumentException refusal) => new()
  {
    Identifier = field,
    ErrorMessage = refusal.Message.Split(" (Parameter")[0],
    Severity = ValidationSeverity.Error,
  };
}
