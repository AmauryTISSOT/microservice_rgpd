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
  /// <inheritdoc />
  public override DateTimeOffset GetUtcNow() => instant;
}
