using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.UseCases.Requests.ReadDataSubjectRequests;

namespace MicroserviceRgpd.UseCases.Requests.ExtendDataSubjectRequest;

/// <summary>
/// <b>Revérifie que la demande se prolonge</b>, confie la prolongation à
/// <see cref="DataSubjectRequest.Extend"/>, enregistre ce qu'elle a touché et rend la demande telle
/// que le tableau la lit — ou rend « introuvable », ou le motif de blocage, ou <b>toutes</b> les
/// raisons de refuser la saisie, sans rien écrire.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Le serveur fait foi, juste avant l'écriture</b>, comme pour l'exécution. Le tableau a pu être
/// rendu avant qu'un autre onglet ne close la demande ou ne la prolonge : un écran resté ouvert ne
/// doit pas pouvoir forcer un geste devenu impossible. Le motif se relit donc ici, sur la demande de
/// l'instant, et la ligne rendue avec le refus corrige l'écran obsolète (ADR-0029).
/// </para>
/// <para>
/// ⚠️ <b>Le blocage passe avant la saisie.</b> Une demande close refusée pour son motif ne fait pas
/// aussi la leçon sur une justification manquante : l'<c>Operator</c> lit ce qui l'arrête, et non ce
/// qu'il aurait dû écrire pour un geste qui n'aura pas lieu.
/// </para>
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
    var todayInParis = ParisCalendar.DateOf(now);

    if (request.ExtensionBlockFacing(todayInParis) is not null)
    {
      return new BlockedExtension(RecordedDataSubjectRequest.Of(request, current, broker.Current, todayInParis));
    }

    var extended = request.Extend(command.Entry, now);

    if (!extended.IsSuccess)
    {
      // ⚠️ Un refus ne rejoue pas la projection : `Map` ne transporte que le statut et les raisons,
      // et garde le refus du domaine intact.
      return extended.Map(_ => RecordedDataSubjectRequest.Of(request, current, broker.Current, todayInParis));
    }

    await requests.SaveChangesAsync(cancellationToken);

    return RecordedDataSubjectRequest.Of(request, current, broker.Current, todayInParis);
  }

  /// <summary>
  /// Un refus <b>qui rend la ligne</b> : <c>Conflict</c>, le motif en français pour seule erreur, et
  /// la demande à jour pour valeur — c'est par elle que l'écran obsolète se corrige.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Un seul statut pour les trois motifs</b> : la ligne rendue porte le motif, et c'est
  /// l'écran qui en tire son code — 409 pour ce qui est déjà joué, 422 pour une fenêtre fermée. Un
  /// <c>Invalid</c> se confondrait ici avec les refus de saisie, que la modale indexe par champ.
  /// </para>
  /// <para>
  /// ⚠️ <b>Il existe parce que les fabriques de refus d'<c>Ardalis.Result</c> ne portent pas de
  /// valeur</b>, comme pour l'exécution : seul le constructeur protégé pose les deux ensemble.
  /// </para>
  /// </remarks>
  private sealed class BlockedExtension : Result<RecordedDataSubjectRequest>
  {
    public BlockedExtension(RecordedDataSubjectRequest request)
      : base(ResultStatus.Conflict)
    {
      Value = request;
      Errors = [request.ExtensionBlock!.FrenchLabel];
    }
  }
}
