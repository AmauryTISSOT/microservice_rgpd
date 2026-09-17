using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.UseCases.Requests.ReadDataSubjectRequests;

namespace MicroserviceRgpd.UseCases.Requests.RecordDataSubjectRequest;

/// <summary>
/// Reçoit la demande par <see cref="DataSubjectRequest.Receive"/> et l'enregistre — ou rend, sans
/// rien écrire, <b>toutes</b> les raisons de la refuser.
/// </summary>
/// <remarks>
/// ⚠️ <b>L'horloge est lue une seule fois.</b> « Aujourd'hui à Paris », qui borne la date de
/// réception, et l'instant d'enregistrement se tirent du même instant : lus séparément, une demande
/// posée à minuit pile pourrait être jugée sur un jour et datée du suivant.
/// <para>
/// La demande rendue dit si elle s'exécute, face au Paramétrage lu après l'enregistrement : la ligne
/// du tableau offre ou éteint son exécution comme au chargement (ADR-0026).
/// </para>
/// </remarks>
/// <param name="requests">Les demandes enregistrées.</param>
/// <param name="settings">Le Paramétrage, en lecture seule.</param>
/// <param name="broker">Ce que ce déploiement déclare savoir publier.</param>
/// <param name="clock">L'horloge du service, qui date le geste.</param>
public sealed class RecordDataSubjectRequestHandler(
  IRepository<DataSubjectRequest> requests,
  IReadRepository<Settings> settings,
  IBrokerConnectionState broker,
  TimeProvider clock)
  : ICommandHandler<RecordDataSubjectRequestCommand, Result<RecordedDataSubjectRequest>>
{
  /// <inheritdoc />
  public async ValueTask<Result<RecordedDataSubjectRequest>> Handle(
    RecordDataSubjectRequestCommand command,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var now = clock.GetUtcNow();
    var received = DataSubjectRequest.Receive(command.Entry, ParisCalendar.DateOf(now), now);

    if (!received.IsSuccess)
    {
      return Result<RecordedDataSubjectRequest>.Invalid(received.ValidationErrors);
    }

    await requests.AddAsync(received.Value, cancellationToken);

    return RecordedDataSubjectRequest.Of(
      received.Value,
      await ServiceSettings.ReadAsync(settings, cancellationToken),
      broker.Current,
      ParisCalendar.DateOf(now));
  }
}
