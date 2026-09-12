namespace MicroserviceRgpd.UnitTests;

/// <summary>
/// Une horloge qui ne bouge pas, pour que la date d'un acte se <b>dicte</b> plutôt qu'elle ne se
/// lise sur la machine qui exécute le test.
/// </summary>
/// <remarks>
/// Écrite ici plutôt qu'empruntée à un paquet d'horloges de test : deux méthodes suffisent, et une
/// dépendance de plus pour deux méthodes se paierait à chaque montée de version.
/// </remarks>
/// <param name="instant">L'instant que cette horloge rendra, toujours le même.</param>
internal sealed class AClockStuckAt(DateTimeOffset instant) : TimeProvider
{
  /// <summary>
  /// Le nombre de lectures depuis sa naissance. C'est lui, et lui seul, qui distingue deux lectures
  /// d'un même instant d'une seule — un acte qui doit dater deux choses du même instant ne lit
  /// l'horloge qu'une fois, et un instant figé ne le dirait pas.
  /// </summary>
  public int Readings { get; private set; }

  /// <inheritdoc />
  public override DateTimeOffset GetUtcNow()
  {
    Readings++;

    return instant;
  }
}
