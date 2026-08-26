using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.Infrastructure.Data.Screenings;

namespace MicroserviceRgpd.IntegrationTests.Data.Screenings;

/// <summary>
/// La lecture d'une table et les comptes du rapport, <b>à travers la vraie base et sans charger le
/// rapport</b> — c'est ce que la seconde table achète, et rien d'autre ne le prouve.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Les comptes sont calculés par la base.</b> Les éprouver en mémoire n'aurait rien dit de ce
/// qui casse ici : un agrégat qui ne se traduit pas, un type possédé que le fournisseur ne sait pas
/// lire dans un prédicat, une conversion de vocabulaire fermé qui se perd.
/// </para>
/// <para>
/// ⚠️ <b>Chaque test pose son propre rapport et ne lit que le sien.</b> Le conteneur est partagé par
/// toute la suite : compter sans borner au rapport du test rendrait le vert dépendant de l'ordre
/// d'exécution — et c'est précisément la borne que ces comptes doivent tenir.
/// </para>
/// </remarks>
[Collection(PostgreSqlCollection.Name)]
public class ScreenedColumnsTests(PostgreSqlFixture postgres)
{
  private static readonly DateTimeOffset LaunchedOn = new(2026, 8, 6, 9, 30, 0, TimeSpan.Zero);
  private static readonly DateTimeOffset RenderedOn = new(2026, 8, 7, 14, 5, 0, TimeSpan.Zero);
  private static readonly ScreeningEngineIdentity Engine = new("lexique-fr-en", "1.0.0");

  /// <summary>
  /// ⚠️ <b>Toutes les colonnes de la table, dans l'ordre du relevé, et rien de la table voisine.</b>
  /// Les <c>Unflagged</c> sont là : les filtrer ici rétablirait l'<c>Omission silencieuse</c> un cran
  /// plus bas que l'écran, là où personne ne la relit.
  /// </summary>
  [Fact]
  public async Task RendersEveryColumnOfOneTableInTheOrderOfTheListing()
  {
    var screening = AScreening(
      AFlaggedColumn("adr_l1", position: 3),
      ANothingSeenColumn("id_adh", position: 1),
      ANothingSeenColumn("nom", position: 2),
      ANothingSeenColumn("montant", position: 1, table: "cotisations"));

    await SaveAsync(screening);

    var read = await ReadAsync(columns =>
      columns.OfTableAsync(screening.Id, new TableIdentity("public", "adherents")));

    read.Select(column => column.Identity.Column).ShouldBe(["id_adh", "nom", "adr_l1"]);
  }

  /// <summary>
  /// <b>Une table qu'un rapport ne porte pas rend la liste vide</b> — et c'est le geste, non la
  /// base, qui décide que cela ne se rend pas comme une table sans colonnes.
  /// </summary>
  [Fact]
  public async Task RendersNothingForATableTheReportDoesNotHold()
  {
    var screening = AScreening(ANothingSeenColumn("id_adh", position: 1));

    await SaveAsync(screening);

    var read = await ReadAsync(columns =>
      columns.OfTableAsync(screening.Id, new TableIdentity("public", "absente")));

    read.ShouldBeEmpty();
  }

  /// <summary>
  /// <b>Les neuf comptes du rapport, calculés par la base.</b> Ils portent sur le rapport
  /// <b>entier</b>, quelle que soit la table qu'on regarde.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b><c>RetainedOnUnflagged</c> est celui qu'on relit deux fois</b> : c'est la mesure directe
  /// de ce que l'<c>Omission relue</c> a rattrapé, et un compte qui l'aurait confondu avec les
  /// retenues aurait fait croire le mécanisme vivant alors que personne n'a rien relu.
  /// </remarks>
  [Fact]
  public async Task CountsTheWholeReportWithoutLoadingIt()
  {
    var screening = AScreening(
      AFlaggedColumn("adr_l1", position: 1),
      AFlaggedColumn("email", position: 2),
      ANothingSeenColumn("id_adh", position: 3),
      ANothingSeenColumn("nom", position: 4),
      ANothingSeenColumn("montant", position: 1, table: "cotisations"));

    await SaveAsync(screening);

    // Une signalée retenue, une non signalée retenue — l'Omission relue en acte —, et une non
    // signalée écartée. Il reste une signalée et une non signalée en attente.
    await ArbitrateAsync(screening.Id, "adherents", "adr_l1", ScreenedColumnState.Retained);
    await ArbitrateAsync(screening.Id, "adherents", "id_adh", ScreenedColumnState.Retained);
    await ArbitrateAsync(screening.Id, "cotisations", "montant", ScreenedColumnState.SetAside);

    var counts = await ReadAsync(columns => columns.CountsOfAsync(screening.Id));

    counts.Columns.ShouldBe(5);
    counts.Tables.ShouldBe(2);
    counts.Flagged.ShouldBe(2);
    counts.Retained.ShouldBe(2);
    counts.SetAside.ShouldBe(1);
    counts.Awaiting.ShouldBe(2);
    counts.RetainedOnUnflagged.ShouldBe(1);
    counts.UnreadUnflagged.ShouldBe(1);

    // Les lignes de ce relevé portent toutes un commentaire de table ; aucune n'est donc muette.
    counts.ColumnsWithoutAComment.ShouldBe(0);
  }

  /// <summary>
  /// <b>Le compte des colonnes muettes est celui de la clause</b> : ni commentaire de colonne, ni
  /// commentaire de table. L'absence est massive et souvent structurelle — plusieurs SGBD n'en
  /// rendent aucun — et c'est ce que la clause dit de <b>ce</b> relevé-ci.
  /// </summary>
  [Fact]
  public async Task CountsAColumnAsMuteOnlyWhenNeitherItNorItsTableCarriesAComment()
  {
    var screening = AScreening(
      ANothingSeenColumn("id_adh", position: 1, tableComment: null),
      ANothingSeenColumn("nom", position: 2, tableComment: null, columnComment: "Le nom de famille."),
      ANothingSeenColumn("montant", position: 1, table: "cotisations"));

    await SaveAsync(screening);

    var counts = await ReadAsync(columns => columns.CountsOfAsync(screening.Id));

    counts.ColumnsWithoutAComment.ShouldBe(1);
  }

  /// <summary>
  /// ⚠️ <b>Les comptes s'arrêtent au rapport qu'on leur nomme.</b> Un déploiement en porte plusieurs
  /// — un re-dépôt archive sans rien effacer — et un compte qui déborderait ferait lire à
  /// l'<c>Operator</c> le verrou d'un rapport qu'il ne regarde pas.
  /// </summary>
  [Fact]
  public async Task CountsOneReportAndNeverItsNeighbours()
  {
    var mine = AScreening(ANothingSeenColumn("id_adh", position: 1));
    var another = AScreening(
      ANothingSeenColumn("id_adh", position: 1),
      ANothingSeenColumn("nom", position: 2));

    await SaveAsync(mine);
    await SaveAsync(another);

    var counts = await ReadAsync(columns => columns.CountsOfAsync(mine.Id));

    counts.Columns.ShouldBe(1);
    counts.Tables.ShouldBe(1);
  }

  /// <summary>
  /// ⚠️ <b>Le rapport n'est pas matérialisé.</b> C'est toute la raison de la seconde table : ouvrir
  /// une table de treize colonnes ne doit pas ramener les cinq mille du rapport, ni sur la lecture,
  /// ni sur les comptes.
  /// </summary>
  [Fact]
  public async Task NeverMaterialisesTheReportItReadsFrom()
  {
    var screening = AScreening(
      AFlaggedColumn("adr_l1", position: 1),
      ANothingSeenColumn("montant", position: 1, table: "cotisations"));

    await SaveAsync(screening);

    await using var dbContext = postgres.NewDbContext();
    var columns = new ScreenedColumns(dbContext);

    await columns.OfTableAsync(screening.Id, new TableIdentity("public", "adherents"));
    await columns.CountsOfAsync(screening.Id);

    dbContext.ChangeTracker.Entries<Screening>().ShouldBeEmpty();
    dbContext.ChangeTracker.Entries<ScreenedColumn>().ShouldBeEmpty();
  }

  /// <summary>
  /// ⚠️ <b>Les deux producteurs des neuf comptes rendent les mêmes neuf nombres.</b> L'agrégat
  /// chargé sert le sommaire, la base sert l'écran d'une table, et rien dans un type de neuf entiers
  /// n'empêche les deux de diverger : retenues et écartées interverties d'un côté, l'
  /// <c>Operator</c> lit deux écrans qui se contredisent sans qu'aucun ne paraisse faux.
  /// </summary>
  [Fact]
  public async Task CountsTheSameNineNumbersFromTheDatabaseAsFromTheLoadedReport()
  {
    var screening = AScreening(
      AFlaggedColumn("adr_l1", position: 1),
      AFlaggedColumn("email", position: 2),
      ANothingSeenColumn("id_adh", position: 3),
      ANothingSeenColumn("nom", position: 4, tableComment: null),
      ANothingSeenColumn("montant", position: 1, table: "cotisations"));

    await SaveAsync(screening);

    await ArbitrateAsync(screening.Id, "adherents", "adr_l1", ScreenedColumnState.Retained);
    await ArbitrateAsync(screening.Id, "adherents", "id_adh", ScreenedColumnState.Retained);
    await ArbitrateAsync(screening.Id, "cotisations", "montant", ScreenedColumnState.SetAside);

    var fromTheDatabase = await ReadAsync(columns => columns.CountsOfAsync(screening.Id));

    await using var dbContext = postgres.NewDbContext();
    var loaded = await dbContext.Screenings
      .Include(one => one.Columns)
      .AsNoTracking()
      .SingleAsync(one => one.Id == screening.Id);

    // L'égalité porte sur le record entier : un compte ajouté un jour sans son équivalent SQL fera
    // rougir ce test, ce qu'une liste de neuf assertions recopiées n'aurait pas fait.
    fromTheDatabase.ShouldBe(ScreeningCounts.Of(loaded));
  }

  /// <summary>
  /// ⚠️ <b>Un rapport chargé sans ses colonnes refuse de rendre ses comptes.</b> Il en rendrait des
  /// zéros sincères et faux — et le pire d'entre eux ferait dire au verrou « toutes les colonnes ont
  /// été relues » sur un rapport que personne n'a ouvert.
  /// </summary>
  [Fact]
  public async Task RefusesToCountAReportLoadedWithoutItsColumns()
  {
    var screening = AScreening(
      AFlaggedColumn("email", position: 1),
      ANothingSeenColumn("id_adh", position: 2));

    await SaveAsync(screening);

    await using var dbContext = postgres.NewDbContext();
    var header = await dbContext.Screenings
      .AsNoTracking()
      .SingleAsync(one => one.Id == screening.Id);

    Should.Throw<InvalidOperationException>(() => ScreeningCounts.Of(header));
  }

  private static Screening AScreening(params ScreenedColumn[] columns)
  {
    return Screening.Of(
      ScreeningId.Next(),
      "galette_prod",
      "postgresql",
      Engine,
      columns.Length,
      columns,
      LaunchedOn);
  }

  private static ListedColumn AListedLine(
    string column,
    int position,
    string table,
    string? columnComment,
    string? tableComment)
  {
    return ListedColumn.Of(
      ColumnIdentity.Of("public", table, column),
      position,
      dataType: "varchar(255)",
      isNullable: true,
      columnComment: columnComment,
      tableComment: tableComment);
  }

  private static ScreenedColumn AFlaggedColumn(string column, int position, string table = "adherents")
  {
    return ScreenedColumn.Flagged(
      AListedLine(column, position, table, null, "Les adhérents de l'association."),
      PersonalDataCategory.ContactDetails,
      RuleStrength.Morphological,
      $"préfixe « adr » reconnu dans « {column} »");
  }

  private static ScreenedColumn ANothingSeenColumn(
    string column,
    int position,
    string table = "adherents",
    string? columnComment = null,
    string? tableComment = "Les adhérents de l'association.")
  {
    return ScreenedColumn.NothingSeen(
      AListedLine(column, position, table, columnComment, tableComment));
  }

  private async Task SaveAsync(Screening screening)
  {
    await using var dbContext = postgres.NewDbContext();

    dbContext.Screenings.Add(screening);

    await dbContext.SaveChangesAsync();
  }

  /// <summary>Relit le rapport, arbitre une colonne, et écrit — comme le fera le geste.</summary>
  private async Task ArbitrateAsync(
    ScreeningId id,
    string table,
    string column,
    ScreenedColumnState ruling)
  {
    await using var dbContext = postgres.NewDbContext();

    var screening = await dbContext.Screenings
      .Include(one => one.Columns)
      .SingleAsync(one => one.Id == id);

    screening.Arbitrate(
      ColumnIdentity.Of("public", table, column), ruling, RenderedOn).ShouldNotBeNull();

    await dbContext.SaveChangesAsync();
  }

  /// <summary>
  /// Une lecture dans un contexte neuf : relire depuis celui qui a écrit ne prouverait que le suivi
  /// des modifications, jamais l'aller-retour à travers la base.
  /// </summary>
  private async Task<T> ReadAsync<T>(Func<IScreenedColumns, Task<T>> read)
  {
    await using var dbContext = postgres.NewDbContext();

    return await read(new ScreenedColumns(dbContext));
  }
}
