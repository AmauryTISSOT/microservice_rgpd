using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Screenings;

/// <summary>
/// Ce qu'un moteur câblé a journalisé : niveau, texte rendu et propriétés structurées — ce que
/// l'exploitant lit, et rien de plus.
/// </summary>
internal sealed class RecordedLogs : ILoggerProvider
{
  private readonly ConcurrentQueue<Entry> _written = new();

  /// <summary>Tout ce qui a été écrit, dans l'ordre.</summary>
  internal IReadOnlyList<Entry> Written => [.. _written];

  public ILogger CreateLogger(string categoryName)
  {
    return new Recording(_written);
  }

  public void Dispose()
  {
    // Rien à libérer : la file vit aussi longtemps que le test.
  }

  /// <summary>Une ligne de journal.</summary>
  internal sealed record Entry(LogLevel Level, string Rendered, IReadOnlyDictionary<string, object?> Properties);

  private sealed class Recording(ConcurrentQueue<Entry> written) : ILogger
  {
    public IDisposable? BeginScope<TState>(TState state)
      where TState : notnull
    {
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
      var properties = state is IReadOnlyList<KeyValuePair<string, object?>> pairs
        ? pairs.ToDictionary(pair => pair.Key, pair => pair.Value)
        : [];

      written.Enqueue(new Entry(logLevel, formatter(state, exception), properties));
    }
  }
}
