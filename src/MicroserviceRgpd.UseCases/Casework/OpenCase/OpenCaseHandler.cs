using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.EvidenceLog;
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
/// <c>EvidenceLog</c> est <b>hors de l'agrégat</b> et son adaptateur écrit pour lui seul. L'ordre
/// est celui-ci parce que les deux pannes ne se valent pas — une ligne de preuve pour un dossier
/// qui n'existe pas est un faux, un dossier dont la première ligne manque est un dossier
/// <b>présent</b> dans le tableau des demandes RGPD, que l'<c>Operator</c> voit. On enregistre un
/// fait laid plutôt qu'on ne fabrique un faux, et l'échec de l'écriture remonte tel quel : il
/// n'existe ici aucun repli qui rendrait un identifiant de dossier à un appelant dont la demande
/// n'a rien laissé.
/// </para>
/// </remarks>
/// <param name="manifest">Le paysage déclaré du client, en lecture seule.</param>
/// <param name="cases">
/// Le <b>seul</b> dépôt de ce contexte : il n'en existe aucun pour un <c>Claim</c> ni pour un
/// <c>Step</c>, et les règles sont écrites une fois sur la racine.
/// </param>
/// <param name="evidenceLog">La matière de preuve, en ajout seul.</param>
/// <param name="clock">
/// L'horloge, injectée pour que la date d'un acte se dicte en test plutôt que d'être lue sur la
/// machine qui l'exécute.
/// </param>
public sealed class OpenCaseHandler(
  IReadRepository<DeclaredSystem> manifest,
  IRepository<Case> cases,
  IEvidenceLog evidenceLog,
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

    // La date de réception vient du canal, entière : c'est lui qui sait si elle a été déclarée par
    // quelqu'un ou tenue pour défaut, et la choisir ici aurait exigé de savoir d'où l'on est appelé.
    var reception = command.Reception;

    var opened = Case.Open(
      CaseId.Next(),
      command.IdentityDeclaration,
      command.Motivation,
      command.Designations,
      command.Rights,
      command.Origin,
      Manifest.Of(await manifest.ListAsync(cancellationToken)),
      reception);

    await cases.AddAsync(opened, cancellationToken);

    await evidenceLog.AppendAsync(
      EvidenceLogEntry.CaseOpened(
        opened.Id,
        // L'instant du DÉPÔT, et non la date de réception : une demande transcrite d'une boîte aux
        // lettres a été reçue avant d'entrer ici, et dater la ligne de sa réception ferait dire à la
        // preuve que le service savait depuis ce jour-là. Le délai, lui, se compte sur `reception`,
        // que la même ligne porte à part.
        clock.GetUtcNow(),
        command.Signatory,
        command.IdentityDeclaration,
        // Le compte, jamais les valeurs : « recherché sous 2 désignations » est une mesure de
        // l'ampleur d'une recherche, les deux valeurs seraient le sac lui-même.
        opened.Designations.Count,
        reception,
        // La moitié qui se compte, et elle seule : le détail en prose reste sur le dossier et meurt
        // avec lui. Le contrôle juge ainsi la pratique sans qu'un seul nom lui survive.
        command.Motivation?.Method),
      cancellationToken);

    return Result<Case>.Success(opened);
  }
}
