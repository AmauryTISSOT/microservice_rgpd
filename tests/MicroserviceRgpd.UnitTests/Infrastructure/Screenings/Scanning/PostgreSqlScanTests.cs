using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.Infrastructure.Screenings.Scanning.PostgreSql;
using Npgsql;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Screenings.Scanning;

/// <summary>
/// Les <b>décisions</b> d'un scan PostgreSQL, éprouvées sur une base qui répond ce qu'on lui fait
/// répondre. Aucun conteneur n'est démarré : les faits par dialecte ont été mesurés hors du dépôt
/// (#275), et ce qui se tient ici est ce que le service fait de ce que la base a dit.
/// </summary>
public class PostgreSqlScanTests
{
  private static readonly DateTimeOffset Noon = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

  /// <summary>
  /// ⚠️ <b>Une base absente du catalogue de schémas envoie l'<c>Operator</c> demander un accès.</b>
  /// C'est la seule des deux fins à zéro objet qui appelle un geste, et la confondre avec l'autre le
  /// ferait chercher une base vide quand il lui faut un droit.
  /// </summary>
  [Fact]
  public async Task SaysTheDatabaseIsAbsentFromTheSchemaCatalogue()
  {
    var outcome = await RunAsync(ABase.WithoutAnySchema());

    outcome.Ending.ShouldBe(ScanEnding.DatabaseAbsentFromCatalogue);
    outcome.Pivot.ShouldBeNull();
    outcome.Previews.ShouldBeEmpty();
  }

  /// <summary>
  /// ⚠️ <b>Une base sans table est une fin, pas un rapport de zéro colonne.</b> Un <c>Screening</c>
  /// vide ferait reculer le rapport courant et détruirait des jours d'arbitrage pour une connexion
  /// d'essai.
  /// </summary>
  [Fact]
  public async Task SaysTheDatabaseCarriesNoTable()
  {
    var outcome = await RunAsync(ABase.WithSchemasButNoTable());

    outcome.Ending.ShouldBe(ScanEnding.NoTable);
    outcome.Pivot.ShouldBeNull();
    outcome.Previews.ShouldBeEmpty();
  }

  /// <summary>
  /// ⚠️ <b>L'erreur de droits sur une table rend « droits refusés » sur ses colonnes, et le relevé
  /// reste entier.</b> Le schéma d'un client bouge, ses droits varient d'une table à l'autre ; si
  /// l'échec d'une table emportait le scan, un relevé de trois cents tables n'aboutirait jamais sur
  /// une base vivante. Et aucune <c>PostgresException</c> ne traverse le port.
  /// </summary>
  [Fact]
  public async Task GivesAccessDeniedToEveryColumnOfAForbiddenTableWithoutLosingTheRest()
  {
    var outcome = await RunAsync(ABase.OfTheFixture().Refusing("cotisations", "42501"));

    outcome.Ending.ShouldBe(ScanEnding.Listed);

    Preview(outcome, "public", "cotisations", "montant").Absence
      .ShouldBe(PreviewAbsenceReason.AccessDenied);
    Preview(outcome, "public", "cotisations", "adherent_id").Absence
      .ShouldBe(PreviewAbsenceReason.AccessDenied);
    Preview(outcome, "public", "adherents", "courriel").CarriesValues.ShouldBeTrue();
  }

  /// <summary>
  /// Toute autre panne de lecture se range dans la même famille : aucune n'offre à l'<c>Operator</c>
  /// un geste que les autres ne lui offrent pas.
  /// </summary>
  [Fact]
  public async Task GivesAReasonToATableThatFailsForAnyOtherCause()
  {
    var outcome = await RunAsync(ABase.OfTheFixture().Refusing("cotisations", "42P01"));

    Preview(outcome, "public", "cotisations", "montant").Absence
      .ShouldBe(PreviewAbsenceReason.ReadFailed);
  }

  /// <summary>
  /// ⚠️ <b>Le binaire s'écarte sur son type, avant toute requête.</b> Une colonne <c>bytea</c> n'est
  /// jamais interrogée : cinq valeurs binaires ne diraient rien à qui les regarde.
  /// </summary>
  [Fact]
  public async Task SaysTheTypeIsNotSampleableForBinaryColumnsAndNeverAsksForThem()
  {
    var database = ABase.OfTheFixture();

    var outcome = await RunAsync(database);

    Preview(outcome, "public", "adherents", "photo").Absence
      .ShouldBe(PreviewAbsenceReason.UnsampleableType);
    Preview(outcome, "public", "adherents", "signature").Absence
      .ShouldBe(PreviewAbsenceReason.UnsampleableType);

    database.Asked.ShouldNotContain("photo");
    database.Asked.ShouldNotContain("signature");
  }

  /// <summary>
  /// ⚠️ <b><c>uuid</c>, <c>jsonb</c> et une colonne de coordonnées sont prélevés.</b>
  /// <c>typcategory = 'U'</c> les aurait rangés avec <c>bytea</c> et exclus en silence.
  /// </summary>
  [Theory]
  [InlineData("jeton")]
  [InlineData("meta")]
  [InlineData("point_geo")]
  [InlineData("etiquettes")]
  public async Task SamplesWhatTheUserDefinedCategoryWouldHaveSwallowed(string column)
  {
    var database = ABase.OfTheFixture();

    await RunAsync(database);

    database.Asked.ShouldContain(column);
  }

  /// <summary>
  /// ⚠️ <b>Une lecture qui ne retourne rien reçoit sa raison, et la phrase ne dit pas que la table
  /// est vide.</b> Deux situations la produisent, indiscernables du dehors : la table est vide, ou
  /// elle est pleine et filtrée par une politique de sécurité au niveau ligne.
  /// </summary>
  [Fact]
  public async Task SaysNoValueWasReturnedWhenTheReadBringsBackNothing()
  {
    var outcome = await RunAsync(ABase.OfTheFixture().Silent("cotisations"));

    var preview = Preview(outcome, "public", "cotisations", "montant");

    preview.CarriesValues.ShouldBeFalse();
    preview.Absence.ShouldBe(PreviewAbsenceReason.NoValueReturned);
    preview.Absence!.Statement.ShouldNotContain("vide");
  }

  /// <summary>Chaque colonne du relevé reçoit un aperçu — aucune case n'est laissée vide.</summary>
  [Fact]
  public async Task GivesEveryListedColumnItsOwnPreview()
  {
    var outcome = await RunAsync(ABase.OfTheFixture());
    var listing = ColumnListingIngestion.Ingest(outcome.Pivot).Listing!;

    outcome.Previews.Count.ShouldBe(listing.ColumnCount);
    listing.Columns.ShouldAllBe(column => outcome.Previews.ContainsKey(column.Identity));
  }

  /// <summary>
  /// ⚠️ <b>Le catalogue apporte le dénominateur, et rien ne se compte avant lui.</b> Un total
  /// annoncé plus tôt serait le chiffre inventé que l'écran d'attente refuse. Le grain du
  /// prélèvement est la table : compter en colonnes ferait avancer la barre par bonds de largeur
  /// variable.
  /// </summary>
  [Fact]
  public async Task ReportsTheCatalogueBeforeItCountsAnything()
  {
    var record = new ARecordOfSteps();

    await RunAsync(ABase.OfTheFixture(), record);

    var steps = record.Steps;

    steps[0].Phase.ShouldBe(ScanPhase.Cataloguing);
    steps[0].Total.ShouldBe(3);
    steps.Skip(1).ShouldAllBe(step => step.Phase == ScanPhase.Sampling);
    steps.Skip(1).ShouldAllBe(step => step.Total == 3);
    steps[^1].Done.ShouldBe(3);
  }

  /// <summary>
  /// ⚠️ <b>L'annulation n'est pas une fin du scan.</b> L'<c>Operator</c> reprend la main ; un
  /// <see cref="ScanEnding.Failed"/> ferait d'un geste volontaire un écran rouge.
  /// </summary>
  [Fact]
  public async Task LetsTheCallerTakeBackTheHandWithoutCallingItAFailure()
  {
    using var abandon = new CancellationTokenSource();

    var database = ABase.OfTheFixture();
    var record = new ARecordOfSteps(step =>
    {
      if (step.Phase == ScanPhase.Cataloguing)
      {
        abandon.Cancel();
      }
    });

    await Should.ThrowAsync<OperationCanceledException>(
      () => PostgreSqlScan.RunAsync(database, new AClockStuckAt(Noon), record, abandon.Token));
  }

  /// <summary>
  /// ⚠️ <b>Une requête coupée par notre propre annulation remonte en abandon, jamais en échec de
  /// lecture.</b> Le serveur répond <c>57014</c> à la demande d'annulation que Npgsql lui a
  /// envoyée : la prendre pour une panne donnerait « lecture échouée » à des colonnes que personne
  /// n'a essayé de lire.
  /// </summary>
  [Fact]
  public async Task TurnsTheServerSideCancellationBackIntoAnAbandon()
  {
    using var abandon = new CancellationTokenSource();

    await abandon.CancelAsync();

    await Should.ThrowAsync<OperationCanceledException>(
      () => PostgreSqlScan.RunAsync(
        ABase.OfTheFixture().Refusing("adherents", "57014"),
        new AClockStuckAt(Noon),
        progress: null,
        CancellationToken.None));
  }

  /// <summary>
  /// ⚠️ <b>Un nom d'objet que le domaine refuse est une fin nommée, pas une exception.</b> Le schéma
  /// d'un client n'a pas à respecter ce que le service sait porter ; c'est au port de le dire, et
  /// sans recopier le nom fautif.
  /// </summary>
  [Fact]
  public async Task FailsWithoutThrowingWhenAnObjectNameIsOneTheDomainRefuses()
  {
    var outcome = await RunAsync(ABase.WhoseCatalogueTheDomainRefuses());

    outcome.Ending.ShouldBe(ScanEnding.Failed);
    outcome.Failure!.Phase.ShouldBe(ScanPhase.Cataloguing);
    outcome.Failure.Family.ShouldBe(ScanFailureFamily.Database);
  }

  /// <summary>
  /// ⚠️ <b>Une panne du catalogue est un échec de scan à phase nommée.</b> Le relevé n'a pas eu
  /// lieu : rendre un pivot partiel serait le relevé <b>silencieusement</b> amputé que le choix de
  /// <c>pg_catalog</c> existe pour empêcher.
  /// </summary>
  [Fact]
  public async Task FailsAtCataloguingWhenTheCatalogueItselfFails()
  {
    var outcome = await RunAsync(ABase.WhoseCatalogueFails());

    outcome.Ending.ShouldBe(ScanEnding.Failed);
    outcome.Failure!.Phase.ShouldBe(ScanPhase.Cataloguing);
    outcome.Failure.Family.ShouldBe(ScanFailureFamily.Network);
  }

  private static Task<ScanOutcome> RunAsync(ABase database, IProgress<ScanStep>? progress = null)
  {
    return PostgreSqlScan.RunAsync(
      database,
      new AClockStuckAt(Noon),
      progress,
      CancellationToken.None);
  }

  private static ColumnPreview Preview(
    ScanOutcome outcome,
    string schema,
    string table,
    string column)
  {
    return outcome.Previews[ColumnIdentity.Of(schema, table, column)];
  }

  /// <summary>
  /// Une base PostgreSQL qui répond ce qu'on lui fait répondre — et qui retient ce qu'on lui a
  /// demandé.
  /// </summary>
  private sealed class ABase : IPostgreSqlSession
  {
    private readonly Func<Task<PostgreSqlPresence>> _presence;
    private readonly Func<Task<List<CataloguedColumn>>> _catalogue;
    private readonly Dictionary<string, string> _refusals = [];
    private readonly HashSet<string> _silent = [];
    private readonly List<string> _asked = [];

    private ABase(
      Func<Task<PostgreSqlPresence>> presence,
      Func<Task<List<CataloguedColumn>>> catalogue)
    {
      _presence = presence;
      _catalogue = catalogue;
    }

    /// <summary>Les colonnes réellement demandées à la base. Le binaire ne doit jamais y figurer.</summary>
    public IReadOnlyList<string> Asked => _asked;

    internal static ABase OfTheFixture()
    {
      return new ABase(
        () => Task.FromResult(new PostgreSqlPresence("epreuve", 2)),
        () => PostgreSqlCatalogue.ReadAsync(
          APostgreSqlCatalogue.Reader(),
          CancellationToken.None));
    }

    internal static ABase WithoutAnySchema()
    {
      return new ABase(
        () => Task.FromResult(new PostgreSqlPresence("epreuve", 0)),
        () => throw new InvalidOperationException(
          "Le catalogue n'a pas à être lu quand aucun schéma applicatif n'existe."));
    }

    internal static ABase WithSchemasButNoTable()
    {
      return new ABase(
        () => Task.FromResult(new PostgreSqlPresence("epreuve", 1)),
        () => Task.FromResult(new List<CataloguedColumn>()));
    }

    internal static ABase WhoseCatalogueFails()
    {
      return new ABase(
        () => Task.FromResult(new PostgreSqlPresence("epreuve", 2)),
        () => throw new NpgsqlException("la connexion est tombée pendant le relevé"));
    }

    internal static ABase WhoseCatalogueTheDomainRefuses()
    {
      return new ABase(
        () => Task.FromResult(new PostgreSqlPresence("epreuve", 2)),

        // Un nom de table porteur d'un saut de ligne : PostgreSQL l'accepte, ColumnIdentity le refuse.
        () => PostgreSqlCatalogue.ReadAsync(
          APostgreSqlCatalogue.Reader(
            [["public", "adhe\nrents", "id", (short)1, "bigint", "int8", false, "", "", ""]]),
          CancellationToken.None));
    }

    /// <summary>Cette table refuse la lecture, avec le <c>SQLSTATE</c> donné.</summary>
    internal ABase Refusing(string table, string sqlState)
    {
      _refusals[table] = sqlState;

      return this;
    }

    /// <summary>Cette table lit sans rien retourner.</summary>
    internal ABase Silent(string table)
    {
      _silent.Add(table);

      return this;
    }

    public Task<PostgreSqlPresence> ReadPresenceAsync(CancellationToken cancellationToken)
    {
      return _presence();
    }

    public Task<List<CataloguedColumn>> ReadCatalogueAsync(CancellationToken cancellationToken)
    {
      return _catalogue();
    }

    public Task<IReadOnlyList<IReadOnlyList<PreviewedValue>>> SampleAsync(
      string schema,
      string table,
      IReadOnlyList<string> columns,
      CancellationToken cancellationToken)
    {
      _asked.AddRange(columns);

      if (_refusals.TryGetValue(table, out var sqlState))
      {
        throw new PostgresException("le serveur a dit non", "ERROR", "ERROR", sqlState);
      }

      var rows = _silent.Contains(table) ? 0 : ColumnPreview.MaxValues;

      return Task.FromResult<IReadOnlyList<IReadOnlyList<PreviewedValue>>>(
      [
        .. columns.Select(column => (IReadOnlyList<PreviewedValue>)
          [
            .. Enumerable.Range(1, rows).Select(row => Value($"{column}-{row}")),
          ]),
      ]);
    }

    private static PreviewedValue Value(string text)
    {
      return PreviewedValue.Of(text, text.Length);
    }
  }
}
