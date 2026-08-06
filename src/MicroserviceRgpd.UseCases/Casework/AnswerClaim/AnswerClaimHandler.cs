using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.Ledger;

namespace MicroserviceRgpd.UseCases.Casework.AnswerClaim;

/// <summary>
/// Porte l'issue sur le <c>Claim</c>, et la consigne sous le nom de qui l'a rendue.
/// </summary>
/// <remarks>
/// <b>Il ne détruit rien.</b> Répondre sur un droit n'efface aucune pièce : c'est la
/// <c>DeclareDelivery</c> qui détruit celles du droit remis, et la clôture qui détruit le reste.
/// Mêler les deux aurait fait disparaître un paquet que personne n'avait encore tendu.
/// </remarks>
/// <param name="cases">Le seul dépôt de ce contexte : les règles sont écrites une fois, sur la racine.</param>
/// <param name="ledger">La matière de preuve, en ajout seul.</param>
/// <param name="clock">L'horloge, injectée pour que la date d'une réponse se dicte en test.</param>
public sealed class AnswerClaimHandler(IRepository<Case> cases, ILedger ledger, TimeProvider clock)
  : ICommandHandler<AnswerClaimCommand, Result>
{
  /// <inheritdoc />
  public async ValueTask<Result> Handle(AnswerClaimCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var opened = await cases.FirstOrDefaultAsync(new CaseByIdSpec(command.Case), cancellationToken);

    if (opened is null)
    {
      return Result.NotFound();
    }

    // La ligne de preuve est forgée d'abord : c'est elle qui exige un nom, et rien ne doit bouger si
    // la signature manque. La règle vit dans le type de la preuve, jamais ici.
    LedgerEntry signed;

    try
    {
      signed = LedgerEntry.ClaimAnswered(
        command.Case,
        clock.GetUtcNow(),
        command.Right,
        Signatory.Operator(command.SignedBy, SignatureRegime.Unauthenticated));
    }
    catch (ArgumentException refusal)
    {
      return Result.Invalid(new ValidationError
      {
        Identifier = nameof(AnswerClaimCommand.SignedBy),
        // Le message des types du domaine porte le nom du paramètre entre parenthèses, façon
        // `ArgumentException` : il est retiré, l'écran nommant déjà le champ fautif à côté de sa case.
        ErrorMessage = refusal.Message.Split(" (Parameter")[0],
        Severity = ValidationSeverity.Error,
      });
    }

    // Le dossier est clos, il ne porte pas ce droit, ou une issue avait déjà été rendue. Aucun des
    // trois n'est une programmation fautive : un écran affiché il y a une minute peut nommer un
    // geste qu'un autre vient de faire.
    if (!opened.Answer(command.Right))
    {
      return Result.NotFound();
    }

    await cases.UpdateAsync(opened, cancellationToken);

    await ledger.AppendAsync(signed, cancellationToken);

    return Result.Success();
  }
}
