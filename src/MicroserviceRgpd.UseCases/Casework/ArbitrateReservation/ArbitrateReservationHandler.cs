using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.EvidenceLog;

namespace MicroserviceRgpd.UseCases.Casework.ArbitrateReservation;

/// <summary>
/// Porte sur une <c>Reservation</c> l'issue qu'un humain vient de rendre, <b>enrichit le sac</b> de
/// ce qu'elle proposait si elle est rattachée, et écrit la ligne de preuve qui le nomme.
/// </summary>
/// <remarks>
/// <para>
/// <b>L'ordre est : écrire le dossier, puis consigner</b> — comme partout ailleurs. Ce sont deux
/// écritures et non une transaction, l'<c>EvidenceLog</c> étant hors de l'agrégat, et les deux pannes ne
/// se valent pas : une ligne de preuve pour un arbitrage que le dossier ne porte pas est un faux ; un
/// arbitrage porté dont la ligne manque est <b>visible</b> à l'écran.
/// </para>
/// <para>
/// <b>La ligne de preuve porte le compte du sac APRÈS l'arbitrage</b>, et c'est ce qui rend lisible
/// « recherché sous 2 désignations, dont 1 ajoutée par arbitrage le 12/04 » sans qu'aucune valeur
/// n'ait survécu. Elle est donc forgée après l'écriture du dossier — l'inverse aurait compté un sac
/// que l'arbitrage n'avait pas encore enrichi.
/// </para>
/// <para>
/// ⚠️ <b>Rien ne repart appeler ici.</b> L'appel suivant portera la désignation neuve à la prochaine
/// ouverture du dossier — c'est le seul endroit où le service appelle, et le faire d'ici ferait
/// dépendre un aller-retour réseau du clic de quelqu'un qui vient seulement de dire ce qu'il pense.
/// </para>
/// </remarks>
/// <param name="cases">Le seul dépôt de ce contexte : les règles sont écrites une fois, sur la racine.</param>
/// <param name="evidenceLog">La matière de preuve, en ajout seul.</param>
/// <param name="clock">L'horloge, injectée pour que la date d'un acte se dicte en test.</param>
public sealed class ArbitrateReservationHandler(IRepository<Case> cases, IEvidenceLog evidenceLog, TimeProvider clock)
  : ICommandHandler<ArbitrateReservationCommand, Result>
{
  /// <inheritdoc />
  public async ValueTask<Result> Handle(ArbitrateReservationCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var opened = await cases.FirstOrDefaultAsync(new CaseByIdSpec(command.Case), cancellationToken);

    if (opened is null)
    {
      return Result.NotFound();
    }

    // La signature est éprouvée d'abord : rien du dossier ne doit bouger si le nom manque, et un
    // rattachement enrichit le sac — ce qui se relit ensuite dans tous les appels suivants.
    Signatory signatory;

    try
    {
      // Le régime accompagne le nom, et il est posé ici : la surface n'authentifie personne, et c'est
      // ce que la preuve doit garder pour ne pas être relue comme une identification.
      signatory = Signatory.Operator(command.SignedBy, SignerVerification.Unauthenticated);
    }
    catch (ArgumentException refusal)
    {
      return Result.Invalid(new ValidationError
      {
        Identifier = nameof(ArbitrateReservationCommand.SignedBy),
        // Le message des types du domaine porte le nom du paramètre entre parenthèses, façon
        // `ArgumentException` : il est retiré, l'écran nommant déjà le champ fautif à côté de sa case.
        ErrorMessage = refusal.Message.Split(" (Parameter")[0],
        Severity = ValidationSeverity.Error,
      });
    }

    if (opened.Arbitrate(command.DeclaredSystem, command.Reference, command.Ruling) is null)
    {
      // Le dossier ne porte pas cette réserve, ou elle est déjà tranchée. Ce n'est pas une
      // programmation fautive : un écran affiché il y a une minute peut nommer une réserve qu'un
      // autre geste vient d'arbitrer.
      return Result.NotFound();
    }

    await cases.UpdateAsync(opened, cancellationToken);

    await evidenceLog.AppendAsync(
      EvidenceLogEntry.ReservationArbitrated(
        command.Case,
        clock.GetUtcNow(),
        command.DeclaredSystem,
        command.Ruling,
        opened.Designations.Count,
        signatory),
      cancellationToken);

    return Result.Success();
  }
}
