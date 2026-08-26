namespace MicroserviceRgpd.FunctionalTests;

/// <summary>
/// L'horloge du service <b>déplacée d'un décalage</b> que le test pose. Elle dit l'heure réelle plus
/// ce décalage, et rien d'autre.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>C'est le seul levier qui existe sur la durée de vie des aperçus, et c'est délibéré.</b> Le
/// cache d'aperçus n'est pas une couture de test : il n'offre aucune méthode « fais-les expirer
/// maintenant », et les écrans le traversent par la frontière HTTP. Ce qu'un test tourne ici est
/// très exactement ce que le temps tourne en exploitation.
/// </para>
/// <para>
/// ⚠️ <b>Elle part de l'heure réelle, et non d'un instant gelé.</b> Le déploiement de test est
/// partagé par toute la collection, et « le rapport courant » est le plus grand
/// <c>LaunchedOn</c> : une horloge gelée dans le passé aurait fait naître des rapports plus anciens
/// que ceux des tests voisins, et le courant aurait cessé d'être celui qu'on vient de déposer.
/// </para>
/// <para>
/// ⚠️ <b>Le décalage se remet à zéro, et c'est à l'appelant de le faire.</b> Un test qui avance de
/// douze heures et repart sans reculer daterait tous les rapports suivants dans le futur — et le
/// premier test à revenir à l'heure réelle verrait son propre dépôt se faire doubler par celui du
/// précédent.
/// </para>
/// <para>
/// ⚠️ <b>Les minuteries et l'horodatage monotone restent ceux du système.</b> Le décalage porte sur
/// « quelle heure est-il », jamais sur « combien de temps a passé » : un <c>Task.Delay</c> déplacé
/// aurait fait durer douze heures un test qui avance de douze heures.
/// </remarks>
public sealed class AClockTheTestsAdvance : TimeProvider
{
  private readonly Lock _turn = new();

  private TimeSpan _ahead = TimeSpan.Zero;

  /// <summary>De combien l'horloge du service est en avance sur l'heure réelle.</summary>
  public TimeSpan Ahead
  {
    get
    {
      lock (_turn)
      {
        return _ahead;
      }
    }
  }

  /// <summary>Avance l'horloge du service — et elle n'y revient jamais toute seule.</summary>
  public void Advance(TimeSpan by)
  {
    lock (_turn)
    {
      _ahead += by;
    }
  }

  /// <summary>Ramène l'horloge du service à l'heure réelle.</summary>
  public void Reset()
  {
    lock (_turn)
    {
      _ahead = TimeSpan.Zero;
    }
  }

  /// <inheritdoc />
  public override DateTimeOffset GetUtcNow()
  {
    return System.GetUtcNow() + Ahead;
  }

  /// <inheritdoc />
  public override long GetTimestamp()
  {
    return System.GetTimestamp();
  }

  /// <inheritdoc />
  public override ITimer CreateTimer(
    TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
  {
    return System.CreateTimer(callback, state, dueTime, period);
  }

  /// <inheritdoc />
  public override TimeZoneInfo LocalTimeZone => System.LocalTimeZone;

  /// <inheritdoc />
  public override long TimestampFrequency => System.TimestampFrequency;
}
