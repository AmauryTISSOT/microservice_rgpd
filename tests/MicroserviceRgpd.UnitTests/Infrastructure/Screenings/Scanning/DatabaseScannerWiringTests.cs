using Microsoft.Extensions.DependencyInjection;
using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.Infrastructure.Screenings.Scanning;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Screenings.Scanning;

/// <summary>
/// Le câblage du port : ce qui se résout, et ce qui se passe quand on l'écrit deux fois.
/// </summary>
public class DatabaseScannerWiringTests
{
  [Fact]
  public void ResolvesAScannerThatKnowsSqlite()
  {
    using var provider = new ServiceCollection().AddDatabaseScanner().BuildServiceProvider();

    provider.GetRequiredService<IDatabaseScanner>().ShouldBeOfType<DatabaseScanner>();
  }

  /// <summary>
  /// ⚠️ <b>Un dialecte câblé ne lève pas, il rend une fin.</b> L'aiguillage ne rend pas un échec de
  /// scan pour un dialecte qu'il ne sait pas joindre — ce serait dire à l'<c>Operator</c> qu'un
  /// geste peut le sauver, alors que c'est un défaut de câblage. Rendre une fin est donc, très
  /// exactement, ce qui distingue un dialecte inscrit d'un dialecte oublié.
  /// </summary>
  [Fact]
  public async Task ResolvesAScannerThatKnowsPostgreSql()
  {
    using var provider = new ServiceCollection().AddDatabaseScanner().BuildServiceProvider();

    var outcome = await provider.GetRequiredService<IDatabaseScanner>()
      .ScanAsync(DatabaseDialect.PostgreSql, "Database=sans-hote");

    outcome.Ending.ShouldBe(ScanEnding.Failed);
  }

  /// <inheritdoc cref="ResolvesAScannerThatKnowsPostgreSql" />
  [Fact]
  public async Task ResolvesAScannerThatKnowsTheMariaDbAndMySqlFamily()
  {
    using var provider = new ServiceCollection().AddDatabaseScanner().BuildServiceProvider();

    var outcome = await provider.GetRequiredService<IDatabaseScanner>().ScanAsync(
      DatabaseDialect.MySql,
      "ceci n'est pas une chaîne de connexion",
      progress: null,
      CancellationToken.None);

    outcome.Ending.ShouldBe(ScanEnding.Failed);
    outcome.Failure!.Family.ShouldBe(ScanFailureFamily.Supplied);
  }

  /// <summary>
  /// Le dialecte qu'aucun pilote ne sert lève, et l'écran ne le voit jamais.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>L'aiguillage est monté à vide plutôt que résolu du conteneur.</b> Les trois dialectes du
  /// service ont désormais leur pilote : ce test n'a plus de dialecte orphelin à demander, et le
  /// seul moyen honnête d'éprouver le refus est de bâtir un aiguillage qui n'en sert aucun. Le
  /// câblage réel, lui, est tenu par les trois tests au-dessus.
  /// </remarks>
  [Fact]
  public async Task ThrowsForADialectNoDriverAnswersFor()
  {
    IDatabaseScanner orphaned = new DatabaseScanner([]);

    await Should.ThrowAsync<ArgumentOutOfRangeException>(
      () => orphaned.ScanAsync(
        DatabaseDialect.MySql,
        "Server=nulle-part",
        progress: null,
        CancellationToken.None));
  }

  /// <summary>
  /// ⚠️ <b>Un câblage écrit deux fois ne fait pas tomber le service.</b> L'aiguillage range ses
  /// dialectes par dialecte : un second scanner du même dialecte le ferait tomber à la première
  /// résolution — une panne au démarrage pour une inscription en double, ce qu'aucune autre
  /// inscription du dépôt ne punit.
  /// </summary>
  [Fact]
  public void SurvivesBeingWiredTwice()
  {
    using var provider = new ServiceCollection()
      .AddDatabaseScanner()
      .AddDatabaseScanner()
      .BuildServiceProvider();

    Should.NotThrow(() => provider.GetRequiredService<IDatabaseScanner>());
  }

  /// <summary>
  /// Un jeton déjà annulé n'ouvre rien du tout : l'<c>Operator</c> qui a quitté l'écran avant que le
  /// scan ne démarre n'a pas à voir sa base touchée.
  /// </summary>
  [Fact]
  public async Task TouchesNothingWhenTheCallerHasAlreadyLetGo()
  {
    using var provider = new ServiceCollection().AddDatabaseScanner().BuildServiceProvider();
    using var abandon = new CancellationTokenSource();

    await abandon.CancelAsync();

    await Should.ThrowAsync<OperationCanceledException>(
      () => provider.GetRequiredService<IDatabaseScanner>().ScanAsync(
        DatabaseDialect.Sqlite,
        "Data Source=nulle-part.db",
        progress: null,
        abandon.Token));
  }
}
