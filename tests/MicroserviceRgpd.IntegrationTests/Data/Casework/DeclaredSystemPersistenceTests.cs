using System.Data.Common;
using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Infrastructure.Data;

namespace MicroserviceRgpd.IntegrationTests.Data.Casework;

/// <summary>
/// La forme que la migration du <c>Manifest</c> a réellement donnée à la base, et l'aller-retour
/// d'un <c>DeclaredSystem</c> à travers elle.
/// </summary>
/// <remarks>
/// La base est réelle parce que rien d'autre ne prouve ce qui est en jeu : <c>text[]</c>,
/// <c>timestamptz</c>, la nullité colonne par colonne, l'unicité de l'identifiant et le fait qu'un
/// tableau de capacités <b>vide</b> se relit vide plutôt que nul.
/// </remarks>
[Collection(PostgreSqlCollection.Name)]
public class DeclaredSystemPersistenceTests(PostgreSqlFixture postgres)
{
  private static readonly DateTimeOffset Declared = new(2026, 7, 30, 9, 0, 0, TimeSpan.Zero);

  /// <summary>
  /// Le catalogue est à <b>gros grain</b> : six colonnes, dont aucune ne nomme une table, une
  /// colonne ou un champ du client, et aucune n'a d'emplacement pour un secret d'<c>Adapter</c>.
  /// </summary>
  [Fact]
  public async Task NamesSixColumnsInSnakeCaseAndNoneOfTheClientSchema()
  {
    var columns = await ColumnsAsync();

    columns.Keys.Order().ShouldBe(
      ["adapter_address", "capabilities", "contents", "declared_on", "id", "label"]);
  }

  /// <summary>
  /// <b>Il n'existe aucun emplacement pour le secret de l'<c>Adapter</c></b> — pas de colonne à
  /// laisser vide, pas de colonne à oublier de masquer dans un journal.
  /// </summary>
  [Fact]
  public async Task OffersNoColumnWhereAnAdapterSecretCouldEverLand()
  {
    var columns = await ColumnsAsync();

    string[] forbidden = ["secret", "token", "password", "credential", "key", "auth"];

    columns.Keys.ShouldNotContain(
      name => forbidden.Any(word => name.Contains(word, StringComparison.OrdinalIgnoreCase)));
  }

  /// <summary>
  /// L'adresse d'<c>Adapter</c> est la <b>seule</b> colonne facultative : un système sans capacité
  /// et sans adresse est le régime normal, mais un système sans prose « contient » ne serait pas
  /// nommable par la <c>CoverSheet</c>.
  /// </summary>
  [Fact]
  public async Task LeavesOnlyTheAdapterAddressOptional()
  {
    var columns = await ColumnsAsync();

    columns["adapter_address"].Nullable.ShouldBeTrue();

    columns["id"].Nullable.ShouldBeFalse();
    columns["label"].Nullable.ShouldBeFalse();
    columns["contents"].Nullable.ShouldBeFalse();
    columns["capabilities"].Nullable.ShouldBeFalse();
    columns["declared_on"].Nullable.ShouldBeFalse();
  }

  /// <summary>Les capacités sont un tableau typé, et la date porte son fuseau.</summary>
  [Fact]
  public async Task TypesTheCapabilitiesAsAnArrayAndTheDeclarationAsAnInstantWithItsOffset()
  {
    var columns = await ColumnsAsync();

    columns["capabilities"].DataType.ShouldBe("ARRAY");
    columns["declared_on"].DataType.ShouldBe("timestamp with time zone");

    columns["id"].MaxLength.ShouldBe(DeclaredSystemId.MaxLength);
    columns["label"].MaxLength.ShouldBe(SystemLabel.MaxLength);
    columns["contents"].MaxLength.ShouldBe(SystemContents.MaxLength);
    columns["adapter_address"].MaxLength.ShouldBe(AdapterAddress.MaxLength);
  }

  /// <summary>
  /// <b>Aucun index secondaire.</b> Le catalogue est un paysage de quelques systèmes qu'on lit d'un
  /// bloc ; la clé primaire couvre le seul accès ciblé qui existe.
  /// </summary>
  [Fact]
  public async Task CreatesNoIndexBeyondThePrimaryKey()
  {
    var indexes = await ScalarListAsync(
      "select indexname from pg_indexes where tablename = 'declared_systems'");

    indexes.ShouldBe(["pk_declared_systems"]);
  }

  /// <summary>
  /// Le niveau 0 fait l'aller-retour : aucune capacité, aucune adresse, et la prose relue mot pour
  /// mot. Un tableau vide se relit <b>vide</b>, jamais nul.
  /// </summary>
  [Fact]
  public async Task RoundTripsASystemDeclaredWithoutASingleCapabilityOrAdapter()
  {
    const string Prose = "L'export commercial transmis chaque mois à notre agence.\nUn fichier CSV.";

    await SaveAsync(DeclaredSystem.Declare(
      DeclaredSystemId.From("export-agence"),
      SystemLabel.From("L'export commercial mensuel"),
      SystemContents.From(Prose),
      [],
      adapterAddress: null,
      Declared));

    var reread = await RereadAsync("export-agence");

    reread.Capabilities.ShouldBeEmpty();
    reread.AdapterAddress.ShouldBeNull();
    reread.Contents.Value.ShouldBe(Prose);
    reread.Label.Value.ShouldBe("L'export commercial mensuel");
    reread.DeclaredOn.ShouldBe(Declared);
  }

  /// <summary>
  /// La comptabilité scellée : elle sait localiser et lire, elle ne pourra jamais effacer — et les
  /// deux capacités se relisent sous leurs noms canoniques anglais.
  /// </summary>
  [Fact]
  public async Task RoundTripsCapabilitiesUnderTheirCanonicalEnglishNames()
  {
    await SaveAsync(DeclaredSystem.Declare(
      DeclaredSystemId.From("compta-scellee"),
      SystemLabel.From("La comptabilité scellée"),
      SystemContents.From("Les écritures comptables, conservées dix ans."),
      [Capability.Locate, Capability.Read],
      AdapterAddress.From("https://brocanto.example/rgpd/compta"),
      Declared));

    var reread = await RereadAsync("compta-scellee");

    reread.Capabilities.ShouldBe([Capability.Locate, Capability.Read]);
    reread.AdapterAddress!.Value.Value.ShouldBe("https://brocanto.example/rgpd/compta");

    var stored = await ScalarListAsync(
      "select unnest(capabilities) from declared_systems where id = 'compta-scellee'");

    stored.ShouldBe(["Locate", "Read"]);
  }

  /// <summary>
  /// L'unicité de l'identifiant est tenue par la <b>base</b>, et pas seulement par la lecture
  /// préalable du gestionnaire : deux lignes le partageant feraient de tout appel sortant vers un
  /// <c>Adapter</c> une loterie.
  /// </summary>
  [Fact]
  public async Task RefusesTwoSystemsSharingTheIdentifierTheAdapterWillReceive()
  {
    await SaveAsync(ASystem("doublon"));

    await Should.ThrowAsync<DbUpdateException>(() => SaveAsync(ASystem("doublon")));
  }

  private static DeclaredSystem ASystem(string id)
  {
    return DeclaredSystem.Declare(
      DeclaredSystemId.From(id),
      SystemLabel.From("Un système déclaré"),
      SystemContents.From("Ce qu'il contient, dans les mots de qui l'a déclaré."),
      [],
      adapterAddress: null,
      Declared);
  }

  private async Task SaveAsync(DeclaredSystem system)
  {
    await using var dbContext = postgres.NewDbContext();

    dbContext.DeclaredSystems.Add(system);

    await dbContext.SaveChangesAsync();
  }

  private async Task<DeclaredSystem> RereadAsync(string id)
  {
    // Un contexte neuf : relire depuis celui qui a écrit ne prouverait que le suivi des
    // modifications, jamais l'aller-retour à travers les colonnes.
    await using var dbContext = postgres.NewDbContext();

    var reread = await dbContext.DeclaredSystems.SingleOrDefaultAsync(
      system => system.Id == DeclaredSystemId.From(id));

    return reread.ShouldNotBeNull();
  }

  private async Task<Dictionary<string, ColumnShape>> ColumnsAsync()
  {
    await using var dbContext = postgres.NewDbContext();
    await using var command = await CommandAsync(
      dbContext,
      """
      select column_name, is_nullable, data_type, character_maximum_length
      from information_schema.columns
      where table_name = 'declared_systems'
      """);

    var columns = new Dictionary<string, ColumnShape>(StringComparer.Ordinal);

    await using var reader = await command.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
      columns[reader.GetString(0)] = new ColumnShape(
        Nullable: reader.GetString(1) == "YES",
        DataType: reader.GetString(2),
        MaxLength: reader.IsDBNull(3) ? null : reader.GetInt32(3));
    }

    return columns;
  }

  private async Task<List<string>> ScalarListAsync(string sql)
  {
    await using var dbContext = postgres.NewDbContext();
    await using var command = await CommandAsync(dbContext, sql);

    var values = new List<string>();

    await using var reader = await command.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
      values.Add(reader.GetString(0));
    }

    return values;
  }

  private static async Task<DbCommand> CommandAsync(AppDbContext dbContext, string sql)
  {
    var connection = dbContext.Database.GetDbConnection();
    await connection.OpenAsync();

    var command = connection.CreateCommand();
    command.CommandText = sql;

    return command;
  }

  /// <summary>Ce qu'on demande à une colonne : sa nullité, son type, et sa borne s'il y en a une.</summary>
  private sealed record ColumnShape(bool Nullable, string DataType, int? MaxLength);
}
