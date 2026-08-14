using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.EvidenceLog;

namespace MicroserviceRgpd.UseCases.Casework.DeclareMotivation;

/// <summary>
/// Porte sur le dossier la motivation qu'un humain vient d'écrire, et consigne <b>qu'il l'a écrite
/// ce jour-là</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>L'ordre est : écrire le dossier, puis consigner</b> — comme partout ailleurs, et pour la même
/// raison : le <c>EvidenceLog</c> est hors de l'agrégat, et les deux pannes ne se valent pas.
/// </para>
/// <para>
/// <b>Rien n'est écrit si rien n'était réclamé.</b> Le <c>EvidenceLog</c> consigne les faits qui changent
/// quelque chose, jamais leur répétition, et cette règle est tenue par l'appelant : écraser une
/// motivation déjà signée ferait réécrire ce que quelqu'un a affirmé, dans le seul dispositif dont
/// l'invariant est qu'on ne le réécrit pas.
/// </para>
/// </remarks>
/// <param name="cases">Le seul dépôt de ce contexte : les règles sont écrites une fois, sur la racine.</param>
/// <param name="ledger">La matière de preuve, en ajout seul.</param>
/// <param name="clock">L'horloge, injectée pour que la date d'un acte se dicte en test.</param>
public sealed class DeclareMotivationHandler(IRepository<Case> cases, IEvidenceLog ledger, TimeProvider clock)
  : ICommandHandler<DeclareMotivationCommand, Result>
{
  /// <inheritdoc />
  public async ValueTask<Result> Handle(DeclareMotivationCommand command, CancellationToken cancellationToken)
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
      signed = EvidenceLogEntry.MotivationDeclared(
        command.Case,
        clock.GetUtcNow(),
        command.Motivation.Method,
        // Le régime accompagne le nom, et il est posé ici : la surface n'authentifie personne.
        Signatory.Operator(command.SignedBy, SignerVerification.Unauthenticated));
    }
    catch (ArgumentException refusal)
    {
      return Result.Invalid(new ValidationError
      {
        Identifier = nameof(DeclareMotivationCommand.SignedBy),
        // Le message des types du domaine porte le nom du paramètre entre parenthèses, façon
        // `ArgumentException` : il est retiré, l'écran nommant déjà le champ fautif à côté de sa case.
        ErrorMessage = refusal.Message.Split(" (Parameter")[0],
        Severity = ValidationSeverity.Error,
      });
    }

    if (!opened.DeclareMotivation(command.Motivation))
    {
      // Rien n'était réclamé : un écran affiché il y a une minute peut montrer une demande qu'un
      // autre geste vient de satisfaire. Ce n'est pas une programmation fautive.
      return Result.NotFound();
    }

    await cases.UpdateAsync(opened, cancellationToken);

    await ledger.AppendAsync(signed, cancellationToken);

    return Result.Success();
  }
}
