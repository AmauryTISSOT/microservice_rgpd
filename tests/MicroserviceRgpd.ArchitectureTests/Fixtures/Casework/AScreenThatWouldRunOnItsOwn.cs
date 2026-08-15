namespace MicroserviceRgpd.ArchitectureTests.Fixtures.Casework;

/// <summary>
/// Un témoin, et rien d'autre : il porte exactement ce que
/// <see cref="NothingRunsInTheBackgroundTests"/> doit refuser, pour qu'on sache que le garde
/// <b>mord</b>. Un garde qu'on n'a jamais vu rouge est un garde dont on ne sait rien.
/// </summary>
/// <remarks>
/// La minuterie est <b>cachée dans un corps de méthode</b>, à l'endroit précis qu'un test de
/// signatures ne verrait pas — c'est la dérive qu'on craint : un écran qui, un jour,
/// « rafraîchirait le tableau des demandes RGPD toutes les minutes ».
/// </remarks>
internal sealed class AScreenThatWouldRunOnItsOwn
{
  private readonly PeriodicTimer _refresh = new(TimeSpan.FromMinutes(1));

  internal void WouldRefreshTheQueueAllByItself()
  {
    using var reminder = new Timer(_ => { }, state: null, dueTime: 0, period: 60_000);

    _ = _refresh;
  }
}
