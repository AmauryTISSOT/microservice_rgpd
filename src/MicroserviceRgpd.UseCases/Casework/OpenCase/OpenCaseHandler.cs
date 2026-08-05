using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.Ledger;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.UseCases.Casework.OpenCase;

/// <summary>
/// Ouvre le dossier d'une demande, et écrit la première ligne de sa preuve.
/// </summary>
/// <remarks>
/// <para>
/// <b>Il lit le catalogue, et il ne le complète pas.</b> Le travail dû naît du <c>Manifest</c> tel
/// qu'il se lit à cet instant — un <c>Step</c> par (<c>Claim</c>, <c>DeclaredSystem</c>) — et un
/// catalogue vide ouvre un dossier sans aucun travail dû plutôt que de refuser la demande. C'est
/// l'<c>Omission silencieuse</c> assumée en face : ce qui n'a pas été déclaré n'entrera pas, et
/// c'est pourquoi la déclaration du <c>Manifest</c> est un prérequis et non une option.
/// </para>
/// <para>
/// <b>L'ordre est : ouvrir, puis consigner.</b> Ce sont deux écritures, et non une transaction : le
/// <c>Ledger</c> est <b>hors de l'agrégat</b> et son adaptateur écrit pour lui seul. L'ordre est
/// celui-ci parce que les deux pannes ne se valent pas — une ligne de preuve pour un dossier qui
/// n'existe pas est un faux, un dossier dont la première ligne manque est un dossier <b>présent</b>
/// dans la file, que l'<c>Operator</c> voit. On enregistre un fait laid plutôt qu'on ne fabrique un
/// faux, et l'échec de l'écriture remonte tel quel : il n'existe ici aucun repli qui rendrait un
/// identifiant de dossier à un appelant dont la demande n'a rien laissé.
/// </para>
/// </remarks>
/// <param name="manifest">Le paysage déclaré du client, en lecture seule.</param>
/// <param name="cases">
/// Le <b>seul</b> dépôt de ce contexte : il n'en existe aucun pour un <c>Claim</c> ni pour un
/// <c>Step</c>, et les règles sont écrites une fois sur la racine.
/// </param>
/// <param name="ledger">La matière de preuve, en ajout seul.</param>
/// <param name="clock">
/// L'horloge, injectée pour que la date d'un acte se dicte en test plutôt que d'être lue sur la
/// machine qui l'exécute.
/// </param>
public sealed class OpenCaseHandler(
  IReadRepository<DeclaredSystem> manifest,
  IRepository<Case> cases,
  ILedger ledger,
  TimeProvider clock)
  : ICommandHandler<OpenCaseCommand, Result<Case>>
{
  /// <inheritdoc />
  public async ValueTask<Result<Case>> Handle(OpenCaseCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var claimable = ClaimableRights.Violation(command.Rights, nameof(OpenCaseCommand.Rights));

    if (claimable is not null)
    {
      return Result<Case>.Invalid(claimable);
    }

    // La demande postée par l'application arrive à l'instant où elle est postée : sur ce canal, la
    // date de réception est celle de l'appel, et il n'existe aucun champ pour la déclarer autrement.
    // Elle est donc *déclarée* — par l'application, qui sait quand elle a reçu la demande — et jamais
    // tenue pour défaut : le défaut est l'affaire du dépôt manuel, où l'humain transcrit un courriel
    // reçu il y a un nombre de jours qu'il ignore.
    var reception = ReceptionDate.Declared(clock.GetUtcNow());

    var opened = Case.Open(
      CaseId.Next(),
      command.IdentityDeclaration,
      command.Designations,
      command.Rights,
      Manifest.Of(await manifest.ListAsync(cancellationToken)),
      reception);

    await cases.AddAsync(opened, cancellationToken);

    await ledger.AppendAsync(
      LedgerEntry.CaseOpened(
        opened.Id,
        reception.On,
        command.Signatory,
        command.IdentityDeclaration,
        // Le compte, jamais les valeurs : « recherché sous 2 désignations » est une mesure de
        // l'ampleur d'une recherche, les deux valeurs seraient le sac lui-même.
        opened.Designations.Count,
        reception),
      cancellationToken);

    return Result<Case>.Success(opened);
  }
}
