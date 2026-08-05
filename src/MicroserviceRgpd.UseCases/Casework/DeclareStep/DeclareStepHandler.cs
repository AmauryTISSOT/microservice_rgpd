using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.Ledger;

namespace MicroserviceRgpd.UseCases.Casework.DeclareStep;

/// <summary>
/// Porte sur un <c>Step</c> l'état que l'<c>Operator</c> déclare, et écrit la ligne de preuve qui le
/// nomme.
/// </summary>
/// <remarks>
/// <para>
/// <b>L'ordre est : écrire le dossier, puis consigner</b> — comme à l'ouverture, et pour la même
/// raison. Ce sont deux écritures et non une transaction, le <c>Ledger</c> étant hors de l'agrégat ;
/// et les deux pannes ne se valent pas. Une ligne de preuve pour un état que le dossier ne porte pas
/// est un faux ; un état porté dont la ligne manque est un état <b>visible</b> à l'écran, que
/// l'<c>Operator</c> voit.
/// </para>
/// <para>
/// <b>Le service ne barre jamais la route.</b> Aucune transition n'est refusée, <c>Untreated</c>
/// compris : la faiblesse d'un dossier doit rester visible plutôt que contournée. Les deux seuls
/// refus possibles ici sont un nom vide et un constat vide — et ce ne sont pas des routes barrées,
/// mais la signature elle-même qui manque.
/// </para>
/// </remarks>
/// <param name="cases">Le seul dépôt de ce contexte : les règles sont écrites une fois, sur la racine.</param>
/// <param name="ledger">La matière de preuve, en ajout seul.</param>
/// <param name="clock">L'horloge, injectée pour que la date d'un acte se dicte en test.</param>
public sealed class DeclareStepHandler(IRepository<Case> cases, ILedger ledger, TimeProvider clock)
  : ICommandHandler<DeclareStepCommand, Result>
{
  /// <inheritdoc />
  public async ValueTask<Result> Handle(DeclareStepCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var opened = await cases.FirstOrDefaultAsync(new CaseByIdSpec(command.Case), cancellationToken);

    if (opened is null)
    {
      return Result.NotFound();
    }

    // La ligne de preuve est forgée d'abord : c'est elle qui exige un nom et un constat, et rien du
    // dossier ne doit bouger si la signature manque. La règle vit dans le type de la preuve, pas ici.
    var signed = Signed(command);

    if (!signed.IsSuccess)
    {
      return Result.Invalid(signed.ValidationErrors);
    }

    if (!opened.Declare(command.Right, command.DeclaredSystem, command.State))
    {
      // Le dossier ne porte pas ce travail dû. Ce n'est pas une programmation fautive : le Manifest
      // vieillit exprès, et un système déclaré après l'ouverture n'a jamais eu de Step ici.
      return Result.NotFound();
    }

    await cases.UpdateAsync(opened, cancellationToken);

    await ledger.AppendAsync(signed.Value, cancellationToken);

    return Result.Success();
  }

  /// <summary>
  /// La ligne de preuve, ou les refus nommés champ par champ. Les messages viennent des types du
  /// domaine : deux rédactions d'une même règle finiraient par ne plus dire la même chose.
  /// </summary>
  private Result<LedgerEntry> Signed(DeclareStepCommand command)
  {
    var refusals = new List<ValidationError>();
    Signatory? signatory = null;

    try
    {
      // Le régime accompagne le nom, et il est posé ici : la surface n'authentifie personne, et c'est
      // ce que la preuve doit garder pour ne pas être relue comme une identification.
      signatory = Signatory.Operator(command.SignedBy, SignatureRegime.Unauthenticated);
    }
    catch (ArgumentException refusal)
    {
      refusals.Add(Refusal(nameof(DeclareStepCommand.SignedBy), refusal.Message));
    }

    if (signatory is not null)
    {
      try
      {
        return LedgerEntry.StepDeclared(
          command.Case,
          clock.GetUtcNow(),
          command.DeclaredSystem,
          command.Right,
          command.State,
          command.Finding,
          signatory);
      }
      catch (ArgumentException refusal)
      {
        refusals.Add(Refusal(nameof(DeclareStepCommand.Finding), refusal.Message));
      }
    }

    return Result<LedgerEntry>.Invalid(refusals);
  }

  private static ValidationError Refusal(string field, string message)
  {
    return new ValidationError
    {
      Identifier = field,
      // Le message des types du domaine porte le nom du paramètre entre parenthèses, façon
      // `ArgumentException` : il est retiré, l'écran nommant déjà le champ fautif à côté de sa case.
      ErrorMessage = message.Split(" (Parameter")[0],
      Severity = ValidationSeverity.Error,
    };
  }
}
