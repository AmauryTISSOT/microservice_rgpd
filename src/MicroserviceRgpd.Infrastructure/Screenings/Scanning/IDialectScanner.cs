using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.Infrastructure.Screenings.Scanning;

/// <summary>
/// Ce qu'un dialecte sait faire seul : joindre sa base avec son propre pilote, lire son propre
/// catalogue, prélever ses propres valeurs. Un dialecte par pilote, et aucun ne connaît les autres.
/// </summary>
/// <remarks>
/// ⚠️ <b>Il n'écrit pas le pivot.</b> Trois dialectes qui sérialisent chacun leur en-tête, ce sont
/// trois occasions d'écrire le format de trois façons — et « un seul format pivot » ne survivrait
/// pas au troisième. La sérialisation vit une seule fois, dans <see cref="PivotWriter"/>.
/// </remarks>
internal interface IDialectScanner
{
  /// <summary>Le dialecte que ce scanner sait joindre. C'est par lui que l'aiguillage le trouve.</summary>
  DatabaseDialect Dialect { get; }

  /// <summary>Joint, relève, prélève, et rend l'une des quatre fins d'un scan.</summary>
  Task<ScanOutcome> ScanAsync(
    string connectionString,
    IProgress<ScanStep>? progress,
    CancellationToken cancellationToken);
}
