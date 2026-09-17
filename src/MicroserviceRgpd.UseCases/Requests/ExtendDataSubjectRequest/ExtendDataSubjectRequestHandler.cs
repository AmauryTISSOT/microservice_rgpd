using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.UseCases.Requests.ReadDataSubjectRequests;

namespace MicroserviceRgpd.UseCases.Requests.ExtendDataSubjectRequest;

/// <summary>
/// Confie la prolongation à <see cref="DataSubjectRequest.Extend"/>, enregistre ce qu'elle a touché et
/// rend la demande telle que le tableau la lit — ou rend « introuvable », ou <b>toutes</b> les raisons
/// de refuser la saisie, sans rien écrire.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>L'horloge est lue une seule fois.</b> « Aujourd'hui à Paris », contre lequel la fenêtre du
/// geste se jugera, et l'instant de la prolongation se tirent du même instant : lus séparément, un
/// geste posé à minuit pile pourrait être jugé sur un jour et daté du suivant.
/// </para>
/// <para>
/// ⚠️ <b>L'enregistrement passe par le suivi des modifications, jamais par un <c>Update</c>
/// global</b>, comme pour la correction : seules les colonnes que le geste a touchées partent.
/// </para>
/// <para>
/// La demande rendue est relue <b>en mémoire</b> : l'agrégat que le suivi des modifications vient
/// d'enregistrer porte déjà la nouvelle date limite et la mention de sa prolongation.
/// </para>
/// </remarks>
/// <param name="requests">Les demandes enregistrées.</param>
/// <param name="settings">Le Paramétrage, en lecture seule : la ligne rendue dit aussi si la demande s'exécute.</param>
/// <param name="broker">Ce que ce déploiement déclare savoir publier.</param>
/// <param name="clock">L'horloge du service, qui date le geste.</param>
public sealed class ExtendDataSubjectRequestHandler(
  IRepository<DataSubjectRequest> requests,
  IReadRepository<Settings> settings,
  IBrokerConnectionState broker,
  TimeProvider clock)
  : ICommandHandler<ExtendDataSubjectRequestCommand, Result<RecordedDataSubjectRequest>>
{
  /// <inheritdoc />
  public async ValueTask<Result<RecordedDataSubjectRequest>> Handle(
    ExtendDataSubjectRequestCommand command,
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
    var extended = request.Extend(command.Entry, ParisCalendar.DateOf(now), now);

    if (!extended.IsSuccess)
    {
      // ⚠️ Un refus ne rejoue pas la projection : `Map` ne transporte que le statut et les raisons,
      // et garde le refus du domaine intact.
      return extended.Map(_ => RecordedDataSubjectRequest.Of(request, current, broker.Current));
    }

    await requests.SaveChangesAsync(cancellationToken);

    return RecordedDataSubjectRequest.Of(request, current, broker.Current);
  }
}
