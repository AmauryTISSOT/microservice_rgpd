using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.UseCases.Requests.ReadDataSubjectRequests;

namespace MicroserviceRgpd.UseCases.Requests.ModifyDataSubjectRequest;

/// <summary>
/// Confie la correction à <see cref="DataSubjectRequest.Modify"/>, enregistre ce qu'elle a touché et
/// rend la demande telle que le tableau la lit — ou rend « introuvable », le refus d'une demande
/// close, ou <b>toutes</b> les raisons de refuser la saisie, sans rien écrire.
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
/// <para>
/// ⚠️ <b>La demande rendue est relue en mémoire, jamais en base</b> : l'agrégat que le suivi des
/// modifications vient d'enregistrer porte déjà les nouvelles valeurs, et une correction qui n'a rien
/// changé les porte tout autant.
/// </para>
/// <para>
/// La demande rendue dit si elle s'exécute, face au Paramétrage lu avec la demande : une
/// correction qui ajoute l'email ou atteste l'identité rallume l'exécution sur la ligne (ADR-0026).
/// </para>
/// </remarks>
/// <param name="requests">Les demandes enregistrées.</param>
/// <param name="settings">Le Paramétrage, en lecture seule.</param>
/// <param name="clock">L'horloge du service, qui date le geste.</param>
public sealed class ModifyDataSubjectRequestHandler(
  IRepository<DataSubjectRequest> requests,
  IReadRepository<Settings> settings,
  TimeProvider clock)
  : ICommandHandler<ModifyDataSubjectRequestCommand, Result<RecordedDataSubjectRequest>>
{
  /// <inheritdoc />
  public async ValueTask<Result<RecordedDataSubjectRequest>> Handle(
    ModifyDataSubjectRequestCommand command,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var request = await requests.GetByIdAsync(command.DataSubjectRequest, cancellationToken);

    if (request is null)
    {
      return Result<RecordedDataSubjectRequest>.NotFound();
    }

    var current = await ServiceSettings.ReadAsync(settings, cancellationToken);
    var now = clock.GetUtcNow();
    var modified = request.Modify(command.Entry, ParisCalendar.DateOf(now), now);

    if (!modified.IsSuccess)
    {
      // ⚠️ Un refus ne rejoue pas la projection : `Map` ne transporte que le statut et les raisons, et
      // la demande n'est pas rendue ici. C'est ce qui garde le refus du domaine intact — `Invalid`
      // reste `Invalid`, `Conflict` reste `Conflict` — sans le réécrire d'un statut à l'autre.
      return modified.Map(_ => RecordedDataSubjectRequest.Of(request, current));
    }

    await requests.SaveChangesAsync(cancellationToken);

    return RecordedDataSubjectRequest.Of(request, current);
  }
}
