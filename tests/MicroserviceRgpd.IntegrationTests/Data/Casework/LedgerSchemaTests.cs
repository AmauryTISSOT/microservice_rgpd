using System.Data.Common;
using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.Ledger;
using MicroserviceRgpd.Infrastructure.Data;
using MicroserviceRgpd.Infrastructure.Data.Casework;

// Shouldly porte un type du même nom, réservé à ses propres tables de cas.
using Case = MicroserviceRgpd.Core.Casework.Case;
using Ledger = MicroserviceRgpd.Infrastructure.Data.Casework.Ledger;

namespace MicroserviceRgpd.IntegrationTests.Data.Casework;

/// <summary>
/// La forme que la migration a réellement donnée au <c>Ledger</c>, et ce qu'elle rend
/// <b>impossible</b>.
/// </summary>
/// <remarks>
/// <para>
/// « Anonyme par construction, jamais par expurgation » est une promesse qu'on ne peut pas tenir en
/// vérifiant ce qui a été écrit — il faudrait avoir tout écrit pour le savoir. Ces tests vérifient
/// donc qu'<b>aucune colonne n'existe</b> où une <c>Designation</c> ou un nom de personne concernée
/// pourrait atterrir, et ils portent sur le <b>schéma</b> plutôt que sur le modèle : c'est la
/// migration qui sera appliquée en production, et un modèle correct dont la migration diverge ne
/// protège de rien.
/// </para>
/// </remarks>
[Collection(PostgreSqlCollection.Name)]
public class LedgerSchemaTests(PostgreSqlFixture postgres)
{
  private static readonly DateTimeOffset Opened = new(2026, 8, 3, 14, 30, 0, TimeSpan.Zero);

  /// <summary>
  /// Huit colonnes, nommées une par une : ce qui n'a pas de colonne ne s'écrira pas. La liste est
  /// écrite en toutes lettres <b>pour que l'ajout d'une colonne soit un geste délibéré</b> — une
  /// colonne de prose libre glissée ici serait la porte par laquelle un nom finirait par passer.
  /// </summary>
  [Fact]
  public async Task NamesEightColumnsAndNotOneMoreWhereANameCouldLand()
  {
    var columns = await ColumnsAsync();

    columns.Keys.Order().ShouldBe(
    [
      "case_id",
      "designation_count",
      "entry_id",
      "fact",
      "identity_declaration",
      "occurred_at",
      "signatory_kind",
      "signatory_name",
    ]);
  }

  /// <summary>
  /// <b>Aucune <c>Designation</c> n'est stockable, dès la première ligne.</b> Ni valeur, ni nature,
  /// ni sac : la seule chose que le <c>Ledger</c> sait de la recherche est son <b>nombre</b>, et il
  /// est typé <c>integer</c> — un texte ne peut pas s'y ranger.
  /// </summary>
  [Fact]
  public async Task OffersNoColumnWhereADesignationCouldEverLand()
  {
    var columns = await ColumnsAsync();

    string[] forbidden = ["designation_value", "email", "phone", "subject", "designations"];

    columns.Keys.ShouldNotContain(
      name => forbidden.Any(word => name.Contains(word, StringComparison.OrdinalIgnoreCase)));

    columns["designation_count"].DataType.ShouldBe("integer");
  }

  /// <summary>
  /// <b>Le seul nom de personne que la table porte est celui de l'<c>Operator</c></b>, et il est
  /// facultatif — l'application appelant depuis une session authentifiée ne signe sous aucun nom,
  /// et la ligne le dit par son <c>signatory_kind</c> plutôt que par un nom vide.
  /// </summary>
  [Fact]
  public async Task CarriesTheOperatorsNameAndNoOtherPersonsName()
  {
    var columns = await ColumnsAsync();

    columns.Keys.Where(name => name.Contains("name", StringComparison.Ordinal))
      .ShouldBe(["signatory_name"]);

    columns["signatory_name"].Nullable.ShouldBeTrue();
    columns["signatory_kind"].Nullable.ShouldBeFalse();
  }

  /// <summary>
  /// Ce qui fait une preuve n'est jamais nul : le dossier, l'instant, le fait, le signataire.
  /// </summary>
  [Fact]
  public async Task LeavesNothingOptionalThatAProofIsMadeOf()
  {
    var columns = await ColumnsAsync();

    columns["entry_id"].Nullable.ShouldBeFalse();
    columns["case_id"].Nullable.ShouldBeFalse();
    columns["occurred_at"].Nullable.ShouldBeFalse();
    columns["fact"].Nullable.ShouldBeFalse();

    columns["occurred_at"].DataType.ShouldBe("timestamp with time zone");
  }

  /// <summary>
  /// <b>Aucune clé étrangère vers <c>cases</c>.</b> Ce n'est pas un oubli : le <c>Ledger</c> survit
  /// au dossier de cinq ans, et une contrainte référentielle rendrait la clôture impossible — ou,
  /// pire, emporterait la preuve avec le dossier qu'elle sert à défendre.
  /// </summary>
  [Fact]
  public async Task TiesTheProofToNoForeignKeyThatTheClosureWouldHaveToBreak()
  {
    var constraints = await ScalarListAsync(
      """
      select constraint_type
      from information_schema.table_constraints
      where table_name = 'ledger_entries' and constraint_type = 'FOREIGN KEY'
      """);

    constraints.ShouldBeEmpty();
  }

  /// <summary>
  /// La ligne fait l'aller-retour, et <b>elle n'a lu aucune ligne antérieure pour s'écrire</b> :
  /// deux ouvertures successives écrivent deux lignes qui ne se connaissent pas.
  /// </summary>
  [Fact]
  public async Task AppendsALineWithoutEverReadingTheOneBefore()
  {
    await using var dbContext = postgres.NewDbContext();

    var ledger = new Ledger(dbContext);
    var first = CaseId.Next();
    var second = CaseId.Next();

    await ledger.AppendAsync(
      LedgerEntry.CaseOpened(first, Opened, Signatory.Application, IdentityDeclaration.ApplicationSession, 2));
    await ledger.AppendAsync(
      LedgerEntry.CaseOpened(second, Opened, Signatory.Operator("Claire Berger"), IdentityDeclaration.Unverified, 0));

    await using var reread = postgres.NewDbContext();

    var lines = await reread.Set<LedgerRow>()
      .AsNoTracking()
      .Where(row => row.CaseId == first.Value || row.CaseId == second.Value)
      .ToListAsync();

    lines.Count.ShouldBe(2);

    var byApplication = lines.Single(row => row.CaseId == first.Value);
    byApplication.SignatoryKind.ShouldBe("Application");
    byApplication.SignatoryName.ShouldBeNull();
    byApplication.DesignationCount.ShouldBe(2);
    byApplication.Fact.ShouldBe("CaseOpened");

    var byOperator = lines.Single(row => row.CaseId == second.Value);
    byOperator.SignatoryKind.ShouldBe("Operator");
    byOperator.SignatoryName.ShouldBe("Claire Berger");
    byOperator.IdentityDeclaration.ShouldBe("Unverified");
  }

  /// <summary>
  /// <b>L'adaptateur n'expose qu'un ajout</b> — ni mise à jour, ni suppression ligne à ligne, ni
  /// relecture, pas même privée. Ce que le type ne sait pas faire, personne n'aura à jurer qu'il ne
  /// l'a pas fait.
  /// </summary>
  [Fact]
  public void OffersNoUpdateAndNoLineByLineDeletion()
  {
    typeof(Ledger)
      .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
                  | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static)
      .Where(method => method.DeclaringType == typeof(Ledger))
      .Select(method => method.Name)
      .ShouldBe(["AppendAsync", "RowOf"], ignoreOrder: true);

    // La ligne n'est jamais déclarée agrégat racine : le dépôt générique lui aurait rendu la mise à
    // jour et la suppression que la définition du Ledger ferme.
    typeof(LedgerRow).IsAssignableTo(typeof(IAggregateRoot)).ShouldBeFalse();

    // Et le contexte n'expose aucun `DbSet` du Ledger : il en existe un pour la trace d'audit, qui
    // n'a qu'un invariant d'écriture seule, mais un `DbSet` public rendrait ici `Remove` et
    // `Update` à quiconque tient le contexte — c'est-à-dire à tout le service.
    typeof(AppDbContext).GetProperties()
      .Select(property => property.PropertyType)
      .ShouldNotContain(typeof(DbSet<LedgerRow>));
  }

  /// <summary>
  /// <b>Il n'existe aucun dépôt de <c>Claim</c> ni de <c>Step</c>, ni aucun chemin d'écriture vers
  /// eux hors de la racine.</b> Ils sont <em>possédés</em> par le <c>Case</c> : EF Core refuse de
  /// les tenir pour des types interrogeables à part entière, et la règle cesse d'être une
  /// discipline pour devenir une impossibilité.
  /// </summary>
  [Fact]
  public void ReachesAClaimAndAStepThroughTheRootOrNotAtAll()
  {
    using var dbContext = postgres.NewDbContext();

    dbContext.Model.FindEntityType(typeof(Claim))!.IsOwned().ShouldBeTrue();
    dbContext.Model.FindEntityType(typeof(Step))!.IsOwned().ShouldBeTrue();
    dbContext.Model.FindEntityType(typeof(Designation))!.IsOwned().ShouldBeTrue();

    dbContext.Model.FindEntityType(typeof(Case))!.IsOwned().ShouldBeFalse();

    // Et aucun DbSet ne les expose : le seul dépôt de ce contexte est celui des Case.
    typeof(AppDbContext).GetProperties()
      .Select(property => property.PropertyType)
      .ShouldNotContain(type => type == typeof(DbSet<Claim>) || type == typeof(DbSet<Step>));
  }

  private async Task<Dictionary<string, ColumnShape>> ColumnsAsync()
  {
    await using var dbContext = postgres.NewDbContext();
    await using var command = await CommandAsync(
      dbContext,
      """
      select column_name, is_nullable, data_type, character_maximum_length
      from information_schema.columns
      where table_name = 'ledger_entries'
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
