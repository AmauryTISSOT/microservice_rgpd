using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.EvidenceLog;

namespace MicroserviceRgpd.UseCases.Casework.ConfirmClaim;

/// <summary>
/// Porte sur un <c>Claim</c> la confirmation qu'un humain vient de donner, et écrit la ligne de
/// preuve qui le nomme.
/// </summary>
/// <remarks>
/// <b>L'ordre est : écrire le dossier, puis consigner</b> — comme à l'ouverture et à la déclaration,
/// et pour la même raison. Ce sont deux écritures et non une transaction, l'<c>EvidenceLog</c> étant hors
/// de l'agrégat, et les deux pannes ne se valent pas : une ligne de preuve pour une confirmation que
/// le dossier ne porte pas est un faux ; une confirmation portée dont la ligne manque est
/// <b>visible</b> à l'écran.
/// </remarks>
/// <param name="cases">Le seul dépôt de ce contexte : les règles sont écrites une fois, sur la racine.</param>
/// <param name="evidenceLog">La matière de preuve, en ajout seul.</param>
/// <param name="clock">L'horloge, injectée pour que la date d'un acte se dicte en test.</param>
public sealed class ConfirmClaimHandler(IRepository<Case> cases, IEvidenceLog evidenceLog, TimeProvider clock)
  : ICommandHandler<ConfirmClaimCommand, Result>
{
  /// <inheritdoc />
  public async ValueTask<Result> Handle(ConfirmClaimCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var opened = await cases.FirstOrDefaultAsync(new CaseByIdSpec(command.Case), cancellationToken);

    if (opened is null)
    {
      return Result.NotFound();
    }

    // La ligne de preuve est forgée d'abord : c'est elle qui exige un nom, et rien du dossier ne doit
    // bouger si la signature manque. La règle vit dans le type de la preuve, jamais ici.
    EvidenceLogEntry signed;

    try
    {
      signed = EvidenceLogEntry.ClaimConfirmed(
        command.Case,
        clock.GetUtcNow(),
        command.Right,
        // Le régime accompagne le nom, et il est posé ici : la surface n'authentifie personne, et
        // c'est ce que la preuve doit garder pour ne pas être relue comme une identification.
        Signatory.Operator(command.SignedBy, SignerVerification.Unauthenticated));
    }
    catch (ArgumentException refusal)
    {
      return Result.Invalid(new ValidationError
      {
        Identifier = nameof(ConfirmClaimCommand.SignedBy),
        // Le message des types du domaine porte le nom du paramètre entre parenthèses, façon
        // `ArgumentException` : il est retiré, l'écran nommant déjà le champ fautif à côté de sa case.
        ErrorMessage = refusal.Message.Split(" (Parameter")[0],
        Severity = ValidationSeverity.Error,
      });
    }

    if (!opened.Confirm(command.Right))
    {
      // Le dossier ne porte pas ce droit. Ce n'est pas une programmation fautive : un écran affiché
      // il y a une minute peut nommer un droit qu'un autre geste vient de changer.
      return Result.NotFound();
    }

    await cases.UpdateAsync(opened, cancellationToken);

    await evidenceLog.AppendAsync(signed, cancellationToken);

    return Result.Success();
  }
}
