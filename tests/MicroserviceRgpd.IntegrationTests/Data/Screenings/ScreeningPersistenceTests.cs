using System.Data.Common;
using MicroserviceRgpd.Core.Screenings;
using Npgsql;

namespace MicroserviceRgpd.IntegrationTests.Data.Screenings;

/// <summary>
/// Le rapport et ses colonnes à travers la vraie base : ce qui s'écrit sur deux tables, ce qui se
/// lit <b>par table</b> sans charger le rapport, et ce que le rapport emporte avec lui.
/// </summary>
/// <remarks>
/// <para>
/// La base est réelle parce que rien d'autre ne prouve ce qui est en jeu : l'index unique qui fait
/// du triplet une identité dans un rapport, la contrainte qui interdit un arbitrage dépareillé, la
/// cascade qui n'y laisse pas cinq mille orphelines, et l'<b>absence</b> de toute colonne
/// d'archivage.
/// </para>
/// <para>
/// ⚠️ <b>Chaque test pose son propre rapport et ne lit que le sien.</b> Le conteneur est partagé par
/// toute la suite : compter les lignes de <c>screened_columns</c> sans borner au rapport du test
/// rendrait le vert dépendant de l'ordre d'exécution.
/// </para>
/// </remarks>
[Collection(PostgreSqlCollection.Name)]
public class ScreeningPersistenceTests(PostgreSqlFixture postgres)
{
  private static readonly DateTimeOffset LaunchedOn = new(2026, 8, 6, 9, 30, 0, TimeSpan.Zero);
  private static readonly DateTimeOffset RenderedOn = new(2026, 8, 7, 14, 5, 0, TimeSpan.Zero);
  private static readonly ScreeningEngineIdentity Engine = new("lexique-fr-en", "1.0.0");

  /// <summary>
  /// <b>Le rapport fait l'aller-retour entier</b> : ses colonnes arrivent avec lui, dans l'ordre du
  /// relevé, signalées et non signalées ensemble. Un rapport relu sans ses <c>Unflagged</c> serait
  /// un rapport dont l'omission a cessé d'être relisible.
  /// </summary>
  [Fact]
  public async Task RoundTripsTheReportAndEveryColumnOfTheListing()
  {
    var screening = AScreening(
      AFlaggedColumn("adr_l1", position: 2),
      ANothingSeenColumn("id_adh", position: 1));

    await SaveAsync(screening);

    var reread = await RereadAsync(screening.Id);

    reread.Database.ShouldBe("galette_prod");
    reread.Dialect.ShouldBe("postgresql");
    reread.Engine.ShouldBe(Engine);
    reread.LaunchedOn.ShouldBe(LaunchedOn);
    reread.DeclaredColumnCount.ShouldBe(2);
    reread.ColumnCount.ShouldBe(2);

    var flagged = reread.ColumnAt(ColumnIdentity.Of("public", "adherents", "adr_l1"));

    flagged.ShouldNotBeNull();
    flagged.Category.ShouldBe(PersonalDataCategory.ContactDetails);
    flagged.Strength.ShouldBe(RuleStrength.Morphological);
    flagged.Reason.ShouldBe("préfixe « adr » reconnu dans « adr_l1 »");
    flagged.Listed.Position.ShouldBe(2);
    flagged.Listed.DataType.ShouldBe("varchar(255)");
    flagged.Listed.IsNullable.ShouldBe(true);
    flagged.Listed.TableComment.ShouldBe("Les adhérents de l'association.");
    flagged.State.ShouldBe(ScreenedColumnState.Awaiting);

    var unflagged = reread.ColumnAt(ColumnIdentity.Of("public", "adherents", "id_adh"));

    unflagged.ShouldNotBeNull();
    unflagged.Category.ShouldBe(PersonalDataCategory.Unflagged);
    unflagged.Strength.ShouldBeNull();
    unflagged.Reason.ShouldBeNull();
  }

  /// <summary>
  /// <b>Deux tables, et le rapport n'en porte qu'une ligne</b> quel que soit le nombre de colonnes
  /// du relevé.
  /// </summary>
  [Fact]
  public async Task WritesOneRowForTheReportAndOnePerColumnOfTheListing()
  {
    var screening = AScreening(
      AFlaggedColumn("adr_l1", position: 1),
      ANothingSeenColumn("id_adh", position: 2),
      ANothingSeenColumn("date_crea", position: 3, table: "cotisations"));

    await SaveAsync(screening);

    var reports = await ScalarListAsync(
      $"select database_name from screenings where id = '{screening.Id.Value}'");

    reports.ShouldBe(["galette_prod"]);

    var columns = await ScalarListAsync(
      "select schema_name || '.' || table_name || '.' || column_name from screened_columns "
      + $"where screening_id = '{screening.Id.Value}' order by table_name, position");

    columns.ShouldBe(["public.adherents.adr_l1", "public.adherents.id_adh", "public.cotisations.date_crea"]);
  }

  /// <summary>
  /// <b>Les colonnes se lisent par table, sans charger le rapport</b> — et c'est toute la raison
  /// pour laquelle <c>ScreenedColumn</c> a son propre <c>DbSet</c>, contre le précédent de
  /// <c>Claim</c> et de <c>Step</c>. L'écran d'arbitrage ouvre une table à la fois ; charger
  /// l'agrégat entier pour treize colonnes aurait été le précédent respecté à la lettre et trahi
  /// en pratique.
  /// </summary>
  [Fact]
  public async Task ReadsTheColumnsOfOneTableWithoutLoadingTheWholeReport()
  {
    var screening = AScreening(
      AFlaggedColumn("adr_l1", position: 2),
      ANothingSeenColumn("id_adh", position: 1),
      ANothingSeenColumn("date_crea", position: 1, table: "cotisations"));

    await SaveAsync(screening);

    await using var dbContext = postgres.NewDbContext();

    var read = await dbContext.ScreenedColumns
      .Where(column =>
        EF.Property<ScreeningId>(column, "ScreeningId") == screening.Id
        && column.Listed.Identity.Schema == "public"
        && column.Listed.Identity.Table == "adherents")
      .OrderBy(column => column.Listed.Position)
      .ToListAsync();

    read.Select(column => column.Listed.Identity.Column).ShouldBe(["id_adh", "adr_l1"]);

    // ⚠️ Le rapport n'a pas été matérialisé : la lecture par table ne passe pas par la racine, et
    // c'est très exactement ce que la seconde table achète.
    dbContext.ChangeTracker.Entries<Screening>().ShouldBeEmpty();
  }

  /// <summary>
  /// <b>Le triplet identifie une colonne dans un rapport, et la base le tient</b> : deux lignes qui
  /// le partagent parleraient de la même colonne, et l'un des deux arbitrages écraserait l'autre.
  /// </summary>
  [Fact]
  public async Task LetsTheDatabaseHoldOneRowPerTripletWithinAReport()
  {
    var screening = AScreening(ANothingSeenColumn("id_adh", position: 1));

    await SaveAsync(screening);

    await using var dbContext = postgres.NewDbContext();

    // L'insertion se fait en SQL nu, et non par le modèle : ce qu'on veut prouver est que la *base*
    // refuse le doublon, y compris à qui contournerait EF Core.
    var refusal = await Should.ThrowAsync<PostgresException>(async () =>
    {
      await dbContext.Database.ExecuteSqlAsync(
        $"""
         insert into screened_columns
           (id, screening_id, schema_name, table_name, column_name, position, category)
         values
           ({Guid.CreateVersion7()}, {screening.Id.Value}, 'public', 'adherents', 'id_adh', 1, 'Unflagged')
         """);
    });

    // 23505 : violation d'unicité.
    refusal.SqlState.ShouldBe("23505");
    refusal.ConstraintName.ShouldBe("ux_screened_columns_identity");
  }

  /// <summary>
  /// <b>Le même triplet dans deux rapports de détection n'est pas un doublon</b> : relancer la
  /// détection sur la même base rend
  /// un rapport neuf qui porte les mêmes colonnes, et l'index unique est borné au rapport.
  /// </summary>
  [Fact]
  public async Task AcceptsTheSameTripletInTwoDistinctReports()
  {
    var first = AScreening(ANothingSeenColumn("id_adh", position: 1));
    var second = AScreening(ANothingSeenColumn("id_adh", position: 1));

    await SaveAsync(first);
    await SaveAsync(second);

    var rows = await ScalarListAsync(
      "select count(*)::text from screened_columns "
      + $"where screening_id in ('{first.Id.Value}', '{second.Id.Value}') and column_name = 'id_adh'");

    rows.ShouldBe(["2"]);
  }

  /// <summary>
  /// <b>L'arbitrage descend inline sur la ligne, avec sa signature</b>, et se relit tel quel. Il n'y
  /// a pas de troisième table : l'état et le nom de qui a tranché entrent ensemble ou pas du tout.
  /// </summary>
  [Fact]
  public async Task WritesTheArbitrationInlineWithItsSignature()
  {
    var screening = AScreening(AFlaggedColumn("adr_l1", position: 1));

    await SaveAsync(screening);

    await ArbitrateAsync(screening.Id, "adr_l1", ScreenedColumnState.Retained, RenderedOn);

    var written = await ScalarListAsync(
      "select state || '/' || (rendered_on = timestamptz '2026-08-07T14:05:00Z')::text "
      + "from screened_columns "
      + $"where screening_id = '{screening.Id.Value}' and column_name = 'adr_l1'");

    written.ShouldBe([$"{nameof(ScreenedColumnState.Retained)}/true"]);

    var reread = await RereadAsync(screening.Id);
    var column = reread.ColumnAt(ColumnIdentity.Of("public", "adherents", "adr_l1"));

    column.ShouldNotBeNull();
    column.State.ShouldBe(ScreenedColumnState.Retained);
    column.Arbitration.ShouldNotBeNull();
    column.Arbitration.RenderedOn.ShouldBe(RenderedOn);
    reread.RetainedCount.ShouldBe(1);
    reread.AwaitingCount.ShouldBe(0);
  }

  /// <summary>
  /// <b>« En attente » est l'absence des deux colonnes</b>, et non un mot écrit dans une colonne
  /// d'état. Un <c>Awaiting</c> stocké aurait pu se dissocier de la date qui n'existe pas.
  /// </summary>
  [Fact]
  public async Task LeavesTheTwoArbitrationColumnsNullWhileNobodyHasRuled()
  {
    var screening = AScreening(ANothingSeenColumn("id_adh", position: 1));

    await SaveAsync(screening);

    var written = await ScalarListAsync(
      "select coalesce(state, '∅') || '/' || coalesce(rendered_on::text, '∅') "
      + "from screened_columns "
      + $"where screening_id = '{screening.Id.Value}'");

    written.ShouldBe(["∅/∅"]);
  }

  /// <summary>
  /// ⚠️ <b>La date n'est pas facultative : la base refuse un état sans elle.</b> Le domaine ne
  /// sait pas poser l'un sans l'autre — les deux champs entrent ensemble dans un seul type — et la
  /// contrainte de contrôle le tient <b>aussi</b> contre qui écrirait en SQL nu. Sans elle, la
  /// promesse « il n'existe ni <c>Retained</c> ni <c>SetAside</c> sans date » ne vaudrait que tant
  /// que tout le monde passe par le domaine.
  /// </summary>
  [Fact]
  public async Task RefusesARulingThatCarriesNoDate()
  {
    var screening = AScreening(ANothingSeenColumn("id_adh", position: 1));

    await SaveAsync(screening);

    await using var dbContext = postgres.NewDbContext();

    var refusal = await Should.ThrowAsync<PostgresException>(async () =>
    {
      await dbContext.Database.ExecuteSqlAsync(
        $"""
         update screened_columns set state = 'Retained'
         where screening_id = {screening.Id.Value}
         """);
    });

    // 23514 : violation d'une contrainte de contrôle.
    refusal.SqlState.ShouldBe("23514");
    refusal.ConstraintName.ShouldBe("ck_screened_columns_arbitration");
  }

  /// <summary>
  /// <b>Le réarbitrage écrase l'état courant</b>, et n'ajoute pas une ligne. La trace <b>est</b>
  /// l'état courant seul : le coût est déclaré — la date de l'arbitrage remplacé est effacée.
  /// </summary>
  [Fact]
  public async Task OverwritesTheStandingArbitrationRatherThanKeepingAHistory()
  {
    var screening = AScreening(AFlaggedColumn("adr_l1", position: 1));

    await SaveAsync(screening);

    await ArbitrateAsync(screening.Id, "adr_l1", ScreenedColumnState.Retained, RenderedOn);
    await ArbitrateAsync(
      screening.Id, "adr_l1", ScreenedColumnState.SetAside, RenderedOn.AddDays(1));

    var written = await ScalarListAsync(
      "select state || '/' || (rendered_on = timestamptz '2026-08-08T14:05:00Z')::text "
      + "from screened_columns "
      + $"where screening_id = '{screening.Id.Value}' and column_name = 'adr_l1'");

    // Une seule ligne, portant la seconde issue : la première n'a laissé aucune trace nulle part.
    written.Count.ShouldBe(1);
    written[0].ShouldBe($"{nameof(ScreenedColumnState.SetAside)}/true");

    var reread = await RereadAsync(screening.Id);

    reread.SetAsideCount.ShouldBe(1);
    reread.RetainedCount.ShouldBe(0);
    reread.ColumnCount.ShouldBe(1);
  }

  /// <summary>
  /// <b>Le rapport emporte ses colonnes quand il s'en va.</b> Supprimer est un geste de
  /// l'<c>Operator</c>, irréversible et sans trace ; cinq mille lignes orphelines auraient fait
  /// survivre les arbitrages d'un rapport qui n'existe plus.
  /// </summary>
  [Fact]
  public async Task TakesEveryColumnWithItWhenTheReportGoes()
  {
    var screening = AScreening(
      AFlaggedColumn("adr_l1", position: 1),
      ANothingSeenColumn("id_adh", position: 2));

    await SaveAsync(screening);

    await ArbitrateAsync(screening.Id, "adr_l1", ScreenedColumnState.Retained, RenderedOn);

    (await ScalarListAsync(
      $"select count(*)::text from screened_columns where screening_id = '{screening.Id.Value}'"))
      .ShouldBe(["2"]);

    await using (var dbContext = postgres.NewDbContext())
    {
      dbContext.Screenings.Remove(
        await dbContext.Screenings.SingleAsync(one => one.Id == screening.Id));

      await dbContext.SaveChangesAsync();
    }

    (await ScalarListAsync(
      $"select count(*)::text from screened_columns where screening_id = '{screening.Id.Value}'"))
      .ShouldBe(["0"]);

    (await ScalarListAsync($"select count(*)::text from screenings where id = '{screening.Id.Value}'"))
      .ShouldBe(["0"]);
  }

  /// <summary>
  /// ⚠️ <b>Aucune colonne d'archivage nulle part</b> — ni drapeau, ni date, ni table à part.
  /// « Courant » est un calcul sur <c>launched_on</c> ; une colonne aurait rendu possibles deux
  /// courants après une transition ratée, et l'<c>Operator</c> aurait arbitré le mauvais rapport.
  /// C'est le seul test qui puisse le dire, parce qu'il interroge le schéma et non le modèle.
  /// </summary>
  [Fact]
  public async Task CarriesNoArchivalStateInEitherTable()
  {
    var columns = await ScalarListAsync(
      "select table_name || '.' || column_name from information_schema.columns "
      + "where table_name in ('screenings', 'screened_columns') order by 1");

    columns.ShouldNotBeEmpty();
    columns.ShouldNotContain(column => column.Contains("archiv", StringComparison.OrdinalIgnoreCase));
    columns.ShouldNotContain(column => column.Contains("current", StringComparison.OrdinalIgnoreCase));

    // Le rapport ne porte pas non plus d'avancement : « douze colonnes en attente » est un compte
    // sur la table fille, recalculé à chaque rendu, jamais un entier que quelqu'un doit penser à
    // remettre à jour.
    columns.ShouldNotContain(column => column.Contains("count", StringComparison.OrdinalIgnoreCase)
      && !column.Contains("declared_column_count", StringComparison.Ordinal));
  }

  /// <summary>
  /// <b>Le schéma des deux tables est celui qu'on a décidé</b> : les vocabulaires fermés descendent
  /// par leur nom et non par un entier, les plafonds sont ceux du domaine, et ce qui est facultatif
  /// l'est là où le domaine le dit.
  /// </summary>
  [Fact]
  public async Task DeclaresEachColumnWithTheTypeAndTheNullabilityThatWasDecided()
  {
    var shape = await ScalarListAsync(
      "select column_name || ' ' || data_type || coalesce('(' || character_maximum_length || ')', '')"
      + " || ' ' || is_nullable from information_schema.columns "
      + "where table_name = 'screened_columns' order by column_name");

    shape.ShouldContain("category character varying(64) NO");
    shape.ShouldContain("strength character varying(64) YES");
    shape.ShouldContain("reason character varying(2000) YES");
    shape.ShouldContain("state character varying(64) YES");
    shape.ShouldContain("rendered_on timestamp with time zone YES");

    // ⚠️ Aucune colonne ne porte qui a arbitré : ce contexte ne l'enregistre pas — ADR-0014.
    shape.ShouldNotContain(column => column.StartsWith("signed_by ", StringComparison.Ordinal));
    shape.ShouldContain("schema_name character varying(100) NO");
    shape.ShouldContain("table_name character varying(100) NO");
    shape.ShouldContain("column_name character varying(100) NO");
    shape.ShouldContain("position integer NO");
    shape.ShouldContain("screening_id uuid NO");

    var report = await ScalarListAsync(
      "select column_name || ' ' || data_type || coalesce('(' || character_maximum_length || ')', '')"
      + " || ' ' || is_nullable from information_schema.columns "
      + "where table_name = 'screenings' order by column_name");

    report.ShouldContain("database_name character varying(100) NO");
    report.ShouldContain("dialect character varying(64) NO");
    report.ShouldContain("declared_column_count integer NO");
    report.ShouldContain("launched_on timestamp with time zone NO");
    report.ShouldContain("engine_name text NO");
    report.ShouldContain("engine_version text NO");
  }

  /// <summary>
  /// <b>L'index unique porte ses colonnes dans cet ordre-là</b>, et l'ordre est tout l'argument :
  /// ses colonnes de tête sont exactement la lecture par table de l'écran d'arbitrage, ce qui est
  /// la raison pour laquelle il n'existe pas de troisième index sur
  /// <c>(screening_id, schema_name, table_name)</c>. Réordonner l'index laisserait cet argument
  /// écrit dans la migration et faux dans la base, sans qu'aucun autre test ne change de couleur.
  /// </summary>
  [Fact]
  public async Task LeadsTheUniqueIndexWithTheColumnsTheArbitrationScreenReads()
  {
    var indexes = await ScalarListAsync(
      "select indexdef from pg_indexes where tablename = 'screened_columns' order by indexname");

    indexes.ShouldContain(
      "CREATE UNIQUE INDEX ux_screened_columns_identity ON public.screened_columns "
      + "USING btree (screening_id, schema_name, table_name, column_name)");

    // ⚠️ Et rien d'autre que les trois index décidés : un index de plus se paierait sur chaque
    // dépôt, cinq mille lignes à la fois.
    indexes.Count.ShouldBe(3);
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

  private static ListedColumn AListedLine(string column, int position, string table)
  {
    return ListedColumn.Of(
      ColumnIdentity.Of("public", table, column),
      position,
      dataType: "varchar(255)",
      isNullable: true,
      columnComment: null,
      tableComment: "Les adhérents de l'association.");
  }

  private static ScreenedColumn AFlaggedColumn(string column, int position, string table = "adherents")
  {
    return ScreenedColumn.Flagged(
      AListedLine(column, position, table),
      PersonalDataCategory.ContactDetails,
      RuleStrength.Morphological,
      $"préfixe « adr » reconnu dans « {column} »");
  }

  private static ScreenedColumn ANothingSeenColumn(string column, int position, string table = "adherents")
  {
    return ScreenedColumn.NothingSeen(AListedLine(column, position, table));
  }

  private async Task SaveAsync(Screening screening)
  {
    await using var dbContext = postgres.NewDbContext();

    dbContext.Screenings.Add(screening);

    await dbContext.SaveChangesAsync();
  }

  /// <summary>
  /// Relit le rapport, arbitre une colonne, et écrit — <b>dans un seul contexte</b>, comme le fera
  /// le geste : l'écriture est un effet de l'arbitrage rendu par la racine, jamais une mise à jour
  /// que le test aurait faite à la main.
  /// </summary>
  private async Task ArbitrateAsync(
    ScreeningId id,
    string column,
    ScreenedColumnState ruling,
    DateTimeOffset renderedOn)
  {
    await using var dbContext = postgres.NewDbContext();

    // ⚠️ Les colonnes se demandent explicitement : elles ne sont pas possédées, et un rapport en
    // porte cinq mille. Le geste d'arbitrage, lui, n'aura pas à les charger — il écrira la ligne.
    var screening = await dbContext.Screenings
      .Include(one => one.Columns)
      .SingleAsync(one => one.Id == id);

    screening.Arbitrate(
      ColumnIdentity.Of("public", "adherents", column), ruling, renderedOn).ShouldNotBeNull();

    await dbContext.SaveChangesAsync();
  }

  private async Task<Screening> RereadAsync(ScreeningId id)
  {
    // Un contexte neuf : relire depuis celui qui a écrit ne prouverait que le suivi des
    // modifications, jamais l'aller-retour à travers les colonnes.
    await using var dbContext = postgres.NewDbContext();

    var reread = await dbContext.Screenings
      .Include(one => one.Columns)
      .SingleOrDefaultAsync(one => one.Id == id);

    return reread.ShouldNotBeNull();
  }

  private async Task<List<string>> ScalarListAsync(string sql)
  {
    await using var dbContext = postgres.NewDbContext();

    var connection = dbContext.Database.GetDbConnection();
    await connection.OpenAsync();

    await using DbCommand command = connection.CreateCommand();
    command.CommandText = sql;

    var values = new List<string>();

    await using var reader = await command.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
      values.Add(reader.GetString(0));
    }

    return values;
  }
}
