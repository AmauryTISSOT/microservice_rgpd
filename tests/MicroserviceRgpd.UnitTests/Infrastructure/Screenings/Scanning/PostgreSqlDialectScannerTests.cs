using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.Infrastructure.Screenings.Scanning;
using MicroserviceRgpd.Infrastructure.Screenings.Scanning.PostgreSql;
using Npgsql;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Screenings.Scanning;

/// <summary>
/// Ce que le dialecte PostgreSQL fait de ce qui rate, et de ce qu'on lui donne — <b>sans conteneur</b>.
/// </summary>
/// <remarks>
/// ⚠️ <b>Aucun serveur n'est démarré ici.</b> Les faits par dialecte ont été mesurés hors du dépôt
/// (#275) ; ce qui s'éprouve dans le processus, c'est la frontière : la chaîne effective, la
/// traduction des pannes du pilote en fins nommées, et le fait que rien de ce que le pilote dit ne
/// traverse le port.
/// </remarks>
public class PostgreSqlDialectScannerTests
{
  private static readonly DateTimeOffset Noon = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

  /// <summary>
  /// La chaîne d'un hôte qui n'écoute pas, et qui le dit tout de suite : le port 1 de la boucle
  /// locale, avec un délai d'une seconde pour que l'échec ne se fasse pas attendre.
  /// </summary>
  private const string NoOneListening =
    "Host=127.0.0.1;Port=1;Database=epreuve;Username=personne;Password=rien;Timeout=1";

  /// <summary>
  /// ⚠️ <b>Le pool est coupé sur la chaîne effective.</b> Une connexion rendue au pool garderait une
  /// session ouverte sur la base du client après l'écran ; ici, la fin du scan est la fin de la
  /// connexion.
  /// </summary>
  [Fact]
  public void CutsThePoolOnTheConnectionStringItReallyUses()
  {
    PostgreSqlDialectScanner.TryPrepare(
      "Host=exemple.test;Database=epreuve;Pooling=true;Maximum Pool Size=50",
      out var prepared).ShouldBeTrue();

    var effective = new NpgsqlConnectionStringBuilder(prepared);

    effective.Pooling.ShouldBeFalse();
    effective.Multiplexing.ShouldBeFalse();
  }

  /// <summary>
  /// ⚠️ <b>Rien d'autre que le pool n'est retouché.</b> Ce que l'<c>Operator</c> a fourni — le port,
  /// le mode <c>SSL</c>, le délai — est ce que le service emploie : un scan qui réussirait sur des
  /// réglages que l'écran n'a pas montrés ne serait pas reproductible pour celui qui l'a lancé.
  /// </summary>
  [Fact]
  public void ChangesNothingElseOfWhatWasSupplied()
  {
    PostgreSqlDialectScanner.TryPrepare(
      "Host=exemple.test;Port=6543;Database=epreuve;Username=lecteur;SSL Mode=Require;Timeout=7",
      out var prepared).ShouldBeTrue();

    var effective = new NpgsqlConnectionStringBuilder(prepared);

    effective.Host.ShouldBe("exemple.test");
    effective.Port.ShouldBe(6543);
    effective.Database.ShouldBe("epreuve");
    effective.Username.ShouldBe("lecteur");
    effective.SslMode.ShouldBe(SslMode.Require);
    effective.Timeout.ShouldBe(7);
  }

  /// <summary>
  /// Une chaîne sans hôte, ou que le pilote ne sait même pas lire, est une fin nommée : rien de ce
  /// qu'elle contient ne ressort, et c'est le message du pilote qui porterait l'hôte et
  /// l'utilisateur.
  /// </summary>
  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  [InlineData("Database=epreuve;Username=lecteur")]
  [InlineData("Host=exemple.test;Port=pas-un-nombre")]
  [InlineData("ceci n'est pas une chaîne de connexion")]
  public void RefusesAConnectionStringItCannotUse(string connectionString)
  {
    PostgreSqlDialectScanner.TryPrepare(connectionString, out _).ShouldBeFalse();
  }

  /// <summary>
  /// ⚠️ <b>Une chaîne inutilisable est un échec de scan, pas une exception.</b> L'écran d'attente
  /// sait dire une fin nommée ; il ne sait pas dire une <c>ArgumentException</c>.
  /// </summary>
  [Fact]
  public async Task FailsOnAnUnusableConnectionStringWithoutThrowing()
  {
    var outcome = await ScannerUnderTest()
      .ScanAsync(DatabaseDialect.PostgreSql, "Database=epreuve");

    outcome.Ending.ShouldBe(ScanEnding.Failed);
    outcome.Failure!.Phase.ShouldBe(ScanPhase.Connecting);
    outcome.Failure.Family.ShouldBe(ScanFailureFamily.Supplied);
  }

  /// <summary>
  /// ⚠️ <b>Un hôte injoignable est le cas fréquent du chemin connecté, et c'est une fin.</b> Le
  /// rendre par une exception le rangerait parmi les pannes du service ; la famille
  /// <see cref="ScanFailureFamily.Network"/>, qui n'avait aucun emploi sous SQLite, trouve ici le
  /// sien.
  /// </summary>
  [Fact]
  public async Task SaysTheNetworkWhenNoOneAnswers()
  {
    var outcome = await ScannerUnderTest().ScanAsync(DatabaseDialect.PostgreSql, NoOneListening);

    outcome.Ending.ShouldBe(ScanEnding.Failed);
    outcome.Failure!.Phase.ShouldBe(ScanPhase.Connecting);
    outcome.Failure.Family.ShouldBe(ScanFailureFamily.Network);
    outcome.Pivot.ShouldBeNull();
    outcome.Previews.ShouldBeEmpty();
  }

  /// <summary>
  /// ⚠️ <b>Aucun mot du pilote ne traverse le port.</b> Ni l'hôte, ni le port, ni l'utilisateur, ni
  /// le message : c'est par là que la chaîne de connexion se reconstituerait par morceaux, dans un
  /// écran, puis dans un journal, puis dans un ticket.
  /// </summary>
  [Fact]
  public async Task LetsNoDriverWordCrossThePort()
  {
    var outcome = await ScannerUnderTest().ScanAsync(DatabaseDialect.PostgreSql, NoOneListening);

    var rendered = outcome.Failure!.Phase.FrenchLabel
      + outcome.Failure.Family.FrenchLabel
      + outcome.Failure.Family.Statement;

    rendered.ShouldNotContain("127.0.0.1", Case.Insensitive);
    rendered.ShouldNotContain("personne", Case.Insensitive);
    rendered.ShouldNotContain("epreuve", Case.Insensitive);
    rendered.ShouldNotContain("Npgsql", Case.Insensitive);
    rendered.ShouldNotContain("connection refused", Case.Insensitive);
  }

  /// <summary>
  /// Un jeton déjà annulé n'ouvre rien du tout : l'<c>Operator</c> qui a quitté l'écran avant que le
  /// scan ne démarre n'a pas à voir sa base touchée.
  /// </summary>
  [Fact]
  public async Task TouchesNothingWhenTheCallerHasAlreadyLetGo()
  {
    using var abandon = new CancellationTokenSource();

    await abandon.CancelAsync();

    await Should.ThrowAsync<OperationCanceledException>(
      () => ScannerUnderTest().ScanAsync(
        DatabaseDialect.PostgreSql,
        NoOneListening,
        progress: null,
        abandon.Token));
  }

  /// <summary>
  /// ⚠️ <b>Le tri se fait sur le <c>SQLSTATE</c>, jamais sur le texte du message.</b> Le message est
  /// localisé par <c>lc_messages</c> : un tri sur ses mots ne survivrait pas à une base configurée
  /// en français.
  /// </summary>
  /// <remarks>
  /// « Droits refusés » se range du côté de ce qui a été fourni au service parce que c'est le
  /// <b>geste</b> qui décide : un compte sans droit envoie l'<c>Operator</c> demander un accès,
  /// exactement comme un mot de passe faux l'envoie corriger sa chaîne.
  /// </remarks>
  [Theory]
  [InlineData("28P01")]
  [InlineData("28000")]
  [InlineData("3D000")]
  [InlineData("42501")]
  public void BlamesWhatWasSuppliedWhenTheServerRefusesTheAccount(string sqlState)
  {
    PostgreSqlFailures.FamilyOf(Answering(sqlState)).ShouldBe(ScanFailureFamily.Supplied);
  }

  /// <summary>Le serveur a parlé, et ce qu'il a dit est un échec de la base.</summary>
  [Theory]
  [InlineData("42P01")]
  [InlineData("53300")]
  [InlineData("XX000")]
  public void BlamesTheDatabaseWhenTheServerAnswersAnythingElse(string sqlState)
  {
    PostgreSqlFailures.FamilyOf(Answering(sqlState)).ShouldBe(ScanFailureFamily.Database);
  }

  /// <summary>
  /// Ce qui n'est pas une réponse du serveur est du réseau : la socket, le délai, la connexion
  /// tombée.
  /// </summary>
  [Fact]
  public void BlamesTheNetworkWhenTheServerNeverAnswered()
  {
    PostgreSqlFailures.FamilyOf(new NpgsqlException("la socket a lâché"))
      .ShouldBe(ScanFailureFamily.Network);
    PostgreSqlFailures.FamilyOf(new TimeoutException()).ShouldBe(ScanFailureFamily.Network);
  }

  /// <summary>
  /// ⚠️ <b>« Droits refusés » vit à part parce que c'est la seule des quatre raisons que
  /// l'<c>Operator</c> puisse corriger.</b> Elle l'envoie demander un accès, là où les autres ne lui
  /// font rien faire.
  /// </summary>
  [Fact]
  public void TellsAccessDeniedApartFromEveryOtherReadFailure()
  {
    PostgreSqlFailures.ReasonFor(Answering("42501"))
      .ShouldBe(PreviewAbsenceReason.AccessDenied);
    PostgreSqlFailures.ReasonFor(Answering("42P01"))
      .ShouldBe(PreviewAbsenceReason.ReadFailed);
    PostgreSqlFailures.ReasonFor(new NpgsqlException("la socket a lâché"))
      .ShouldBe(PreviewAbsenceReason.ReadFailed);
  }

  /// <summary>
  /// ⚠️ <b>Une requête coupée par notre propre annulation n'est pas un échec de scan.</b> Npgsql
  /// ouvre une seconde connexion pour demander au serveur de couper ; celui-ci répond
  /// <c>57014</c> sur la première, que le pilote relève parfois telle quelle. La laisser passer
  /// ferait d'un abandon volontaire un écran rouge.
  /// </summary>
  [Fact]
  public void RecognisesItsOwnCancellationComingBackFromTheServer()
  {
    PostgreSqlFailures.IsCancellation(Answering("57014")).ShouldBeTrue();
    PostgreSqlFailures.IsCancellation(Answering("42501")).ShouldBeFalse();
    PostgreSqlFailures.IsCancellation(new NpgsqlException("la socket a lâché")).ShouldBeFalse();
  }

  private static PostgresException Answering(string sqlState)
  {
    return new PostgresException(
      "le serveur a dit quelque chose, et ce quelque chose ne sort pas d'ici",
      "ERROR",
      "ERROR",
      sqlState);
  }

  private static DatabaseScanner ScannerUnderTest()
  {
    return new DatabaseScanner([new PostgreSqlDialectScanner(new AClockStuckAt(Noon))]);
  }
}
