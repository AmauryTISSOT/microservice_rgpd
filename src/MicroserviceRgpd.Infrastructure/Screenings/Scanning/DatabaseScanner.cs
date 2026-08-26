using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.Infrastructure.Screenings.Scanning;

/// <summary>
/// L'implémentation du port : elle ne lit aucune base elle-même, elle aiguille vers le dialecte qui
/// sait le faire.
/// </summary>
/// <remarks>
/// ⚠️ <b>Un dialecte sans pilote câblé lève, il ne rend pas un échec de scan.</b> Un
/// <see cref="ScanOutcome.Failed"/> dit à l'<c>Operator</c> qu'un geste peut le sauver ; un dialecte
/// que le service ne sait pas joindre n'est pas une panne de sa base, c'est une erreur de
/// programmation, et elle doit s'entendre au démarrage plutôt que se déguiser en écran d'échec.
/// </remarks>
public sealed class DatabaseScanner : IDatabaseScanner
{
  private readonly IReadOnlyDictionary<DatabaseDialect, IDialectScanner> _dialects;

  internal DatabaseScanner(IEnumerable<IDialectScanner> dialects)
  {
    ArgumentNullException.ThrowIfNull(dialects);

    _dialects = dialects.ToDictionary(scanner => scanner.Dialect);
  }

  /// <inheritdoc />
  public Task<ScanOutcome> ScanAsync(
    DatabaseDialect dialect,
    string connectionString,
    IProgress<ScanStep>? progress = null,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(dialect);

    if (!_dialects.TryGetValue(dialect, out var scanner))
    {
      throw new ArgumentOutOfRangeException(
        nameof(dialect),
        $"Aucun pilote n'est câblé pour le dialecte {dialect.Name}.");
    }

    return scanner.ScanAsync(connectionString, progress, cancellationToken);
  }
}
