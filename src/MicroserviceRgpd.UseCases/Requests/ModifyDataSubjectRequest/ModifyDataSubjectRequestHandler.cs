using MicroserviceRgpd.Core.Requests;

namespace MicroserviceRgpd.UseCases.Requests.ModifyDataSubjectRequest;

/// <summary>
/// Confie la correction à <see cref="DataSubjectRequest.Modify"/> et enregistre ce qu'elle a touché —
/// ou rend « introuvable », le refus d'une demande close, ou <b>toutes</b> les raisons de refuser la
/// saisie, sans rien écrire.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>L'horloge est lue une seule fois.</b> « Aujourd'hui à Paris », qui borne la date de
/// réception, et l'instant de la modification se tirent du même instant : lus séparément, une
/// correction posée à minuit pile pourrait être jugée sur un jour et datée du suivant.
/// </para>
/// <para>
/// ⚠️ <b>L'enregistrement passe par le suivi des modifications, jamais par un <c>Update</c>
/// global.</b> Un <c>Update</c> marquerait les colonnes comme modifiées et émettrait un <c>UPDATE</c>
/// même pour une modification qui n'a rien changé — précisément ce que le domaine refuse de laisser
/// exister.
/// </para>
/// </remarks>
/// <param name="requests">Les demandes enregistrées.</param>
/// <param name="clock">L'horloge du service, qui date le geste.</param>
public sealed class ModifyDataSubjectRequestHandler(IRepository<DataSubjectRequest> requests, TimeProvider clock)
  : ICommandHandler<ModifyDataSubjectRequestCommand, Result>
{
  /// <inheritdoc />
  public async ValueTask<Result> Handle(ModifyDataSubjectRequestCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var request = await requests.GetByIdAsync(command.DataSubjectRequest, cancellationToken);

    if (request is null)
    {
      return Result.NotFound();
    }

    var now = clock.GetUtcNow();
    var modified = request.Modify(command.Entry, ParisCalendar.DateOf(now), now);

    if (!modified.IsSuccess)
    {
      return modified;
    }

    await requests.SaveChangesAsync(cancellationToken);

    return modified;
  }
}
