namespace MicroserviceRgpd.Core.Screenings;

/// <summary>
/// <b>Un seul <c>Scan</c> en vol par déploiement</b>, et le moyen de retrouver celui qui court.
/// C'est un fait du <b>service</b>, jamais d'une session : deux <c>Operator</c> qui ouvrent la même
/// adresse d'attente voient le même écran et le même compte.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Le second lancement est refusé, pas mis en file.</b> Une file aurait fait partir sur la
/// base d'un tiers une lecture que plus personne n'attendait, et le refus ne nomme pas seulement le
/// refus : il nomme <b>le scan en cours</b>, sans quoi l'<c>Operator</c> ne saurait pas si le
/// service travaille pour lui ou pour quelqu'un d'autre.
/// </para>
/// <para>
/// ⚠️ <b>Le dernier scan est retenu même après sa fin, et c'est ce qui rend le <c>303</c>
/// possible.</b> L'écran d'attente doit pouvoir répondre « c'est fini, voici le rapport » à qui
/// arrive une seconde après la dernière ligne écrite. Un seul est retenu : le précédent s'efface
/// quand le suivant part.
/// </para>
/// <para>
/// ⚠️ <b>Rien de tout cela ne survit au processus.</b> Le service redémarré, il n'y a plus ni scan
/// en vol ni scan retenu — et l'écran d'attente d'hier doit le <b>dire</b> plutôt que rediriger en
/// silence. Reprendre là où le scan s'était arrêté est impossible par construction : la chaîne de
/// connexion n'a pas survécu.
/// </para>
/// </remarks>
public sealed class ScansInFlight
{
  private readonly Lock _turn = new();

  private ScanProgress? _last;

  /// <summary>
  /// Le scan qui court, ou <c>null</c> s'il n'y en a aucun. C'est lui que le refus d'un second
  /// lancement nomme.
  /// </summary>
  public ScanProgress? Running
  {
    get
    {
      lock (_turn)
      {
        return _last is { } last && !last.Snapshot.HasEnded ? last : null;
      }
    }
  }

  /// <summary>
  /// Prend la place, s'il n'y a personne. ⚠️ <b>La lecture et la prise sont un seul geste</b> : deux
  /// <c>Operator</c> qui cliquent à la même seconde auraient tous deux lu « libre » avant que l'un
  /// des deux n'écrive.
  /// </summary>
  /// <param name="starting">Le scan qui voudrait partir.</param>
  /// <param name="alreadyRunning">Celui qui court déjà, quand la place est prise.</param>
  /// <returns>Vrai si la place était libre et que ce scan la tient désormais.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="starting"/> est absent.</exception>
  public bool TryTakeOff(ScanProgress starting, out ScanProgress? alreadyRunning)
  {
    ArgumentNullException.ThrowIfNull(starting);

    lock (_turn)
    {
      if (_last is { } last && !last.Snapshot.HasEnded)
      {
        alreadyRunning = last;

        return false;
      }

      _last = starting;
      alreadyRunning = null;

      return true;
    }
  }

  /// <summary>
  /// Le scan que cette adresse nomme, s'il est encore connu du processus — <c>null</c> sinon, et
  /// c'est le cas « ce scan n'existe plus » que l'écran d'attente doit dire.
  /// </summary>
  /// <param name="id">L'identité lue dans l'adresse.</param>
  public ScanProgress? Find(ScanId id)
  {
    lock (_turn)
    {
      return _last is { } last && last.Id == id ? last : null;
    }
  }
}
