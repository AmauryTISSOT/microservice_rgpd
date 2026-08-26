using System.Collections;
using System.Reflection;
using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.Infrastructure.Screenings.Scanning.MySql;
using MySqlConnector;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Screenings.Scanning;

/// <summary>
/// Ce que le dialecte MariaDB/MySQL rend quand rien ne répond. <b>Aucun serveur n'est monté</b> :
/// un port fermé est une panne de pilote parfaitement authentique, et c'est celle par laquelle le
/// port se fait traverser ou non.
/// </summary>
/// <remarks>
/// ⚠️ <b>C'est le seul endroit du filet où le vrai pilote court.</b> Le reste s'éprouve sur le
/// texte des requêtes et sur les fonctions de traduction ; ici, <c>MySqlConnector</c> ouvre pour de
/// vrai un socket qui se fait refuser, et l'on regarde ce qui franchit le port.
/// </remarks>
public class MySqlScanFailureTests
{
  /// <summary>
  /// Un port fermé sur la boucle locale : le refus est immédiat, et il n'y a rien à monter.
  /// ⚠️ Le compte porte un nom reconnaissable — c'est lui que
  /// <see cref="KeepsNoLockerHoldingTheConnectionStringAfterTheScan"/> cherchera dans les casiers du
  /// pilote.
  /// </summary>
  private const string NothingListensThere =
    "Server=127.0.0.1;Port=1;User ID=temoin-du-casier;Password=secret;Database=epreuve;"
    + "ConnectionTimeout=3";

  private static MySqlDialectScanner AScanner()
  {
    return new MySqlDialectScanner(new AClockStuckAt(new DateTimeOffset(2026, 8, 25, 12, 0, 0, TimeSpan.Zero)));
  }

  /// <summary>Le dialecte s'annonce, et c'est par lui que l'aiguillage le trouve.</summary>
  [Fact]
  public void AnswersForTheMariaDbAndMySqlFamily()
  {
    AScanner().Dialect.ShouldBe(DatabaseDialect.MySql);
  }

  /// <summary>
  /// ⚠️ <b>Aucune <c>MySqlException</c> ne traverse le port.</b> Un hôte injoignable est le cas
  /// <b>fréquent</b> du chemin connecté ; le rendre par une exception le rangerait parmi les pannes
  /// du service, alors que c'est une fin que l'écran d'attente sait dire.
  /// </summary>
  [Fact]
  public async Task LetsNoDriverExceptionCrossThePort()
  {
    // Une MySqlException qui traverserait ferait tomber ce test ici même, avant toute assertion.
    var outcome = await AScanner().ScanAsync(
      NothingListensThere,
      progress: null,
      CancellationToken.None);

    outcome.Ending.ShouldBe(ScanEnding.Failed);
    outcome.Failure!.Phase.ShouldBe(ScanPhase.Connecting);
    outcome.Failure.Family.ShouldBe(ScanFailureFamily.Network);
  }

  /// <summary>
  /// ⚠️ <b>Ni hôte, ni compte, ni message du pilote.</b> C'est par là que la chaîne de connexion se
  /// reconstituerait par morceaux — et l'échec ne sait porter que deux mots du service.
  /// </summary>
  [Fact]
  public async Task SaysNothingOfTheHostOrOfTheAccount()
  {
    var outcome = await AScanner().ScanAsync(
      NothingListensThere,
      progress: null,
      CancellationToken.None);

    var said = outcome.Failure!.Phase.FrenchLabel + " " + outcome.Failure.Family.FrenchLabel
      + " " + outcome.Failure.Family.Statement;

    said.ShouldNotContain("127.0.0.1");
    said.ShouldNotContain("temoin-du-casier");
    said.ShouldNotContain("secret");
  }

  /// <summary>
  /// Une chaîne que le pilote ne sait pas lire, ou qui ne nomme aucune base : c'est ce qui a été
  /// fourni au service, et l'<c>Operator</c> le corrige seul.
  /// </summary>
  [Theory]
  [InlineData("Server=127.0.0.1;User ID=lecteur")]
  [InlineData("Server=127.0.0.1;Database=epreuve;OptionQuiNExistePas=3")]
  [InlineData("n'importe quoi")]
  public async Task BlamesWhatWasSuppliedWhenTheStringNamesNothingToRead(string connectionString)
  {
    var outcome = await AScanner().ScanAsync(
      connectionString,
      progress: null,
      CancellationToken.None);

    outcome.Ending.ShouldBe(ScanEnding.Failed);
    outcome.Failure!.Phase.ShouldBe(ScanPhase.Connecting);
    outcome.Failure.Family.ShouldBe(ScanFailureFamily.Supplied);
  }

  /// <summary>
  /// Un jeton déjà annulé n'ouvre rien du tout : l'<c>Operator</c> qui a quitté l'écran avant que le
  /// scan ne démarre n'a pas à voir la base du client touchée.
  /// </summary>
  [Fact]
  public async Task TouchesNothingWhenTheCallerHasAlreadyLetGo()
  {
    using var abandon = new CancellationTokenSource();

    await abandon.CancelAsync();

    await Should.ThrowAsync<OperationCanceledException>(
      () => AScanner().ScanAsync(NothingListensThere, progress: null, abandon.Token));
  }

  /// <summary>
  /// ⚠️ <b>L'échec d'une table ne fait pas tomber le scan, et il ne laisse aucune case vide.</b>
  /// Chaque colonne de la table reçoit sa raison — « droits refusés » quand le serveur a rendu
  /// <c>1142</c>, qui est très exactement le cas du compte qui ne voit qu'une partie de la base.
  /// </summary>
  [Fact]
  public void TurnsARefusedTableIntoRefusedRightsOnEachOfItsColumns()
  {
    var columns = new[]
    {
      MySqlCatalogue.ToColumn("epreuve", "adherents", "courriel", 1, "varchar(255)", "varchar", "NO", "", "", ""),
      MySqlCatalogue.ToColumn("epreuve", "adherents", "solde", 2, "decimal(12,2)", "decimal", "YES", "", "", ""),
    };

    var previews = new Dictionary<ColumnIdentity, ColumnPreview>();

    MySqlDialectScanner.MarkAbsent(
      columns,
      MySqlFailures.ReasonFor(MySqlFailures.TableAccessDenied),
      previews);

    previews.Count.ShouldBe(2);
    previews.Values.ShouldAllBe(preview => preview.Absence == PreviewAbsenceReason.AccessDenied);
  }

  /// <summary>
  /// <b>Aucun casier ne garde de session ouverte après le scan.</b> Le pool est coupé, et cela
  /// s'observe : le pilote inscrit bien la chaîne à son registre, mais il ne lui attache
  /// <b>aucun</b> pool — donc aucune connexion authentifiée qui survivrait à l'écran.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Ce test lit une entrée interne du pilote, et c'est assumé.</b> C'est la seule surface où
  /// « rien ne survit à l'écran » s'observe autrement qu'en relisant l'option. Si une montée de
  /// version en change la forme, ce test doit être <b>ré-ancré</b>, pas supprimé : il est ce qui
  /// verrait un <c>Pooling</c> revenu à sa valeur par défaut.
  /// </remarks>
  [Fact]
  public async Task KeepsNoOpenSessionAfterTheScan()
  {
    await AScanner().ScanAsync(NothingListensThere, progress: null, CancellationToken.None);

    var locker = Lockers().SingleOrDefault(
      entry => entry.Key.Contains("temoin-du-casier", StringComparison.Ordinal));

    locker.Key.ShouldNotBeNull(
      "Le pilote n'a pas même inscrit la chaîne à son registre : la forme interne a changé, et ce "
      + "test doit être ré-ancré plutôt que cru sur parole.");

    locker.Value.ShouldBeNull(
      "Un pool est attaché à la chaîne du client : une connexion authentifiée — hôte, compte et mot "
      + "de passe — reste ouverte après que l'Operator a quitté l'écran. C'est Pooling=false qui "
      + "l'en empêche.");
  }

  /// <summary>
  /// <b>Ce que le pilote garde tout de même, écrit ici plutôt que découvert un jour de revue.</b>
  /// <c>MySqlConnector</c> range la chaîne de connexion <b>telle quelle</b> — mot de passe compris —
  /// comme <b>clé</b> d'un dictionnaire statique et dans un cache du dernier venu, et il le fait
  /// même quand <c>Pooling=false</c>. <c>ClearAllPools</c> ne l'en retire pas, et
  /// <c>MySqlDataSource</c> passe par le même registre.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Ce test épingle une limite, il ne l'approuve pas.</b> Le service ne peut rien y faire
  /// depuis l'API publique du pilote : la chaîne vit dans un statique jusqu'à la fin du processus.
  /// Ce qui en découle est borné — aucune session ouverte, aucune valeur lue, rien qui descende en
  /// base —, mais ce n'est pas rien, et personne ne doit pouvoir dire plus tard qu'on ne le savait
  /// pas. <b>Le jour où une version du pilote cesse de le faire, ce test rougit</b> : c'est le
  /// signal qu'il faut le remplacer par l'assertion que #307 voulait écrire — aucun casier ne garde
  /// la chaîne.
  /// </remarks>
  [Fact]
  public async Task PinsTheConnectionStringTheDriverCachesInSpiteOfEverything()
  {
    await AScanner().ScanAsync(NothingListensThere, progress: null, CancellationToken.None);
    await MySqlConnection.ClearAllPoolsAsync();

    Lockers().ShouldContain(
      locker => locker.Key.Contains("temoin-du-casier", StringComparison.Ordinal),
      "Le pilote ne cache plus la chaîne de connexion. C'est une bonne nouvelle : remplacez ce test "
      + "par l'assertion que #307 voulait — aucun casier ne garde la chaîne — et retirez la réserve "
      + "écrite sur MySqlConnectionSettings.");
  }

  /// <summary>
  /// Ce que <c>MySqlConnector</c> range dans son registre statique : la chaîne, et le pool qu'il lui
  /// attache — <c>null</c> quand il n'y en a pas.
  /// </summary>
  private static List<KeyValuePair<string, object?>> Lockers()
  {
    var pool = typeof(MySqlConnection).Assembly.GetType("MySqlConnector.Core.ConnectionPool")
      ?? throw new InvalidOperationException(
        "MySqlConnector.Core.ConnectionPool a disparu : le test qui observe les casiers du pilote "
        + "doit être ré-ancré sur la forme neuve, pas retiré.");

    var registry = pool.GetField("s_pools", BindingFlags.Static | BindingFlags.NonPublic)
      ?? throw new InvalidOperationException(
        "Le registre statique des pools de MySqlConnector a changé de nom : le test qui observe "
        + "les casiers du pilote doit être ré-ancré sur la forme neuve, pas retiré.");

    if (registry.GetValue(null) is not IDictionary lockers)
    {
      return [];
    }

    var read = new List<KeyValuePair<string, object?>>();

    foreach (DictionaryEntry entry in lockers)
    {
      read.Add(new KeyValuePair<string, object?>(entry.Key.ToString() ?? string.Empty, entry.Value));
    }

    return read;
  }
}
