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
}
