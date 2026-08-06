using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.Ledger;

namespace MicroserviceRgpd.UseCases.Casework.DeclareExtension;

/// <summary>
/// Enregistre la prolongation déclarée, et la consigne sous le nom de l'humain qui l'a signée.
/// </summary>
/// <remarks>
/// <para>
/// <b>Il n'écrit à personne, et il n'y a rien à câbler pour cela.</b> Le service n'a aucun canal
/// vers la personne concernée : la prolongation, le refus et la remise sont les trois seules
/// communications qui lui sont dues, et toutes trois sont des actes humains déclarés au service.
/// </para>
/// <para>
/// <b>L'instant de la déclaration est lu une seule fois</b> et sert aux deux écritures : c'est de
/// lui que le dénominateur se recalculera à chaque affichage, et deux lectures d'horloge auraient
/// pu tomber de part et d'autre de l'échéance du mois.
/// </para>
/// <para>
/// ⚠️ <b>Il ne dit nulle part si l'échéance a bougé.</b> Ni au dossier, ni au <c>Ledger</c> : le
/// déplacement est un calcul refait à l'affichage, et l'écrire une fois pour toutes serait le
/// dénominateur persisté que ce contexte refuse.
/// </para>
/// </remarks>
/// <param name="cases">Le seul dépôt de ce contexte : les règles sont écrites une fois, sur la racine.</param>
/// <param name="ledger">La matière de preuve, en ajout seul.</param>
/// <param name="clock">L'horloge, injectée pour que la date d'une déclaration se dicte en test.</param>
public sealed class DeclareExtensionHandler(
  IRepository<Case> cases,
  ILedger ledger,
  TimeProvider clock)
  : ICommandHandler<DeclareExtensionCommand, Result>
{
  /// <inheritdoc />
  public async ValueTask<Result> Handle(DeclareExtensionCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var opened = await cases.FirstOrDefaultAsync(new CaseByIdSpec(command.Case), cancellationToken);

    if (opened is null)
    {
      return Result.NotFound();
    }

    var declaredAt = clock.GetUtcNow();

    // La déclaration est éprouvée d'abord : le motif et la date d'information sont ce que
    // l'art. 12.3 réclame, et le refus se dépose sous le nom du champ que l'écran affiche.
    ExtensionDeclaration declaration;

    try
    {
      declaration = ExtensionDeclaration.Of(command.Motive, command.InformedOn, declaredAt);
    }
    catch (ArgumentException refusal)
    {
      return Result.Invalid(Named(FieldOf(refusal), refusal));
    }

    // La ligne de preuve est forgée avant que rien ne bouge : c'est elle qui exige un nom, et une
    // prolongation sans signataire ne doit rien poser sur le dossier.
    LedgerEntry signed;

    try
    {
      signed = LedgerEntry.ExtensionDeclared(
        command.Case,
        declaredAt,
        declaration,
        Signatory.Operator(command.SignedBy, SignatureRegime.Unauthenticated));
    }
    catch (ArgumentException refusal)
    {
      return Result.Invalid(Named(nameof(DeclareExtensionCommand.SignedBy), refusal));
    }

    // Déjà prolongé, ou clos : ce n'est pas une programmation fautive, mais un écran affiché il y a
    // une minute. L'art. 12.3 n'ouvre qu'une prolongation, et une seconde réécrirait le motif et les
    // dates qu'un humain a signés.
    if (!opened.DeclareExtension(declaration))
    {
      return Result.NotFound();
    }

    await cases.UpdateAsync(opened, cancellationToken);

    await ledger.AppendAsync(signed, cancellationToken);

    return Result.Success();
  }

  /// <summary>
  /// Le champ de la commande que le refus d'<see cref="ExtensionDeclaration"/> doit nommer.
  /// </summary>
  /// <remarks>
  /// Le type du domaine lève sous le nom de <b>son</b> paramètre ; l'écran, lui, nomme ses cases.
  /// Sans cette correspondance, un « motif absent » s'afficherait à côté de la case de la date.
  /// </remarks>
  private static string FieldOf(ArgumentException refusal)
  {
    return refusal.ParamName == "informedOn"
      ? nameof(DeclareExtensionCommand.InformedOn)
      : nameof(DeclareExtensionCommand.Motive);
  }

  private static ValidationError Named(string field, ArgumentException refusal) => new()
  {
    Identifier = field,
    ErrorMessage = refusal.Message.Split(" (Parameter")[0],
    Severity = ValidationSeverity.Error,
  };
}
