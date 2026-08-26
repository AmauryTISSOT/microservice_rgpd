using System.Collections.Concurrent;

namespace MicroserviceRgpd.FunctionalTests;

/// <summary>
/// Tout ce que le service a journalisé pendant un test, texte rendu <b>et</b> valeurs des
/// propriétés — la seule façon d'éprouver qu'un secret n'y est pas.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Les valeurs des propriétés sont capturées, pas seulement le message rendu.</b> Le
/// <c>LoggingBehavior</c> du dépôt journalise en <i>structuré</i> : un secret passé en propriété
/// d'une requête n'apparaîtrait pas forcément dans le texte formaté, et un test qui ne lirait que
/// celui-ci serait vert sur une fuite complète.
/// </para>
/// <para>
/// <b>Il n'y a aucun filtre de niveau.</b> Un secret journalisé en <c>Trace</c> est un secret
/// journalisé : le collecteur prend tout ce que les catégories du service lui donnent.
/// </para>
/// </remarks>
public sealed class CapturedLogs : ILoggerProvider
{
  private readonly ConcurrentQueue<string> _written = new();

  /// <summary>Tout ce qui a été écrit depuis la dernière remise à zéro.</summary>
  public IReadOnlyList<string> Written => [.. _written];

  /// <summary>
  /// La fabrique est partagée par toute la collection : sans remise à zéro, un test lirait les
  /// journaux de tous ceux qui l'ont précédé.
  /// </summary>
  public void Clear()
  {
    _written.Clear();
  }

  /// <summary>Ce fragment apparaît-il quelque part dans les journaux ?</summary>
  public bool Mention(string fragment)
  {
    return _written.Any(line => line.Contains(fragment, StringComparison.OrdinalIgnoreCase));
  }

  public ILogger CreateLogger(string categoryName)
  {
    return new Collecting(_written, categoryName);
  }

  public void Dispose()
  {
    // Rien à libérer : la file vit aussi longtemps que la fabrique.
  }

  private sealed class Collecting(ConcurrentQueue<string> written, string category) : ILogger
  {
    public IDisposable? BeginScope<TState>(TState state)
      where TState : notnull
    {
      // La portée est retenue elle aussi : un secret posé en portée voyage avec chaque ligne.
      written.Enqueue($"{category} [portée] {state}");

      return null;
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
      LogLevel logLevel,
      EventId eventId,
      TState state,
      Exception? exception,
      Func<TState, Exception?, string> formatter)
    {
      ArgumentNullException.ThrowIfNull(formatter);

      var rendered = formatter(state, exception);

      // ⚠️ Le texte rendu ET les valeurs structurées : le LoggingBehavior recopie les propriétés
      // d'une requête en paires nommées, que le format d'un message ne montre pas forcément.
      var structured = state is IReadOnlyList<KeyValuePair<string, object?>> pairs
        ? string.Join(' ', pairs.Select(pair => $"{pair.Key}={pair.Value}"))
        : state?.ToString();

      written.Enqueue($"{category} {logLevel} {rendered} {structured} {exception}");
    }
  }
}
