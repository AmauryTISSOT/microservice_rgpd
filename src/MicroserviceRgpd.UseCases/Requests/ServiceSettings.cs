using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.UseCases.Requests;

/// <summary>
/// La lecture du Paramétrage <b>depuis <c>Requests</c></b>, conformiste en aval de
/// <c>Configuration</c> (ADR-0026) : le <see cref="Settings"/> tel qu'il est publié, sans traduction.
/// </summary>
internal static class ServiceSettings
{
  /// <summary>
  /// Le Paramétrage à cet instant — <b>vierge</b> tant qu'aucune ligne n'a été enregistrée, comme
  /// <c>Configuration</c> le lit lui-même : sa naissance est paresseuse.
  /// </summary>
  public static async Task<Settings> ReadAsync(IReadRepository<Settings> settings, CancellationToken cancellationToken)
  {
    var persisted = await settings.ListAsync(cancellationToken);

    return persisted.Count > 0 ? persisted[0] : Settings.Unconfigured();
  }

  /// <summary>
  /// L'adresse HTTP par laquelle <paramref name="right"/> s'exerce, ou <c>null</c> — <b>le seul canal
  /// que <c>Requests</c> sache exercer aujourd'hui</b>.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b><c>null</c> ne dit pas « non configuré »</b> : un droit réglé sur un routage RabbitMQ est
  /// configuré, et rend pourtant <c>null</c> ici, parce que le service ne sait pas encore publier
  /// (ADR-0027). C'est <b>le seul endroit</b> où <c>Requests</c> réduit un canal à l'espèce qu'il
  /// appelle ; deux lectures identiques à quelques lignes d'écart seraient une divergence en attente.
  /// </remarks>
  public static EndpointUrl? HttpAddressFor(Settings settings, DataSubjectRight right)
  {
    ArgumentNullException.ThrowIfNull(settings);

    return settings.ChannelFor(right) is ExerciseChannel.HttpEndpoint http ? http.Address : null;
  }
}
