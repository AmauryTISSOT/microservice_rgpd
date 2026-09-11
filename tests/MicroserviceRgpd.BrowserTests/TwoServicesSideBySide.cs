namespace MicroserviceRgpd.BrowserTests;

/// <summary>
/// <b>Deux services démarrés côte à côte ne se disputent aucun port</b> : chacun reçoit le sien de
/// l'OS, et aucun ne retombe sur le port par défaut de Kestrel. C'est ce qui laisse tourner ensemble
/// deux suites navigateur, depuis deux worktrees.
/// </summary>
/// <remarks>
/// <para>
/// Ce test ne vérifie aucun écran, mais le harnais lui-même : il vit ici parce que
/// <c>ServiceOnARealPort</c> y vit, et n'ouvre aucun navigateur.
/// </para>
/// <para>
/// Les deux services partagent la base du harnais : ce test ne lit que leur adresse.
/// </para>
/// </remarks>
[Collection(BrowserCollection.Name)]
public class TwoServicesSideBySide(BrowserHarness harness)
{
  /// <summary>Le port sur lequel Kestrel écoute quand personne ne lui en donne, recopié à dessein.</summary>
  private const int KestrelDefaultPort = 5000;

  [Fact]
  public async Task EachListensOnItsOwnPortGivenByTheOs()
  {
    await using var first = new ServiceOnARealPort(harness.ConnectionString);
    await using var second = new ServiceOnARealPort(harness.ConnectionString);

    var firstAddress = first.Start();
    var secondAddress = second.Start();

    firstAddress.Port.ShouldNotBe(secondAddress.Port);
    firstAddress.Port.ShouldNotBe(KestrelDefaultPort);
    secondAddress.Port.ShouldNotBe(KestrelDefaultPort);
  }
}
