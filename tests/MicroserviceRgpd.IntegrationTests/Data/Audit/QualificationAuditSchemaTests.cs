using System.Data.Common;
using MicroserviceRgpd.Infrastructure.Data;
using MicroserviceRgpd.Infrastructure.Data.Audit;

namespace MicroserviceRgpd.IntegrationTests.Data.Audit;

/// <summary>
/// La forme que la première migration du dépôt a réellement donnée à la base.
/// </summary>
/// <remarks>
/// Ces vérifications portent sur le schéma et non sur le modèle : c'est la migration qui sera
/// appliquée en production, et un modèle correct dont la migration diverge ne protège de rien.
/// </remarks>
[Collection(PostgreSqlCollection.Name)]
public class QualificationAuditSchemaTests(PostgreSqlFixture postgres)
{
  /// <summary>
  /// Une table unique, nommée en <c>snake_case</c> — déclaré explicitement dans la configuration
  /// d'entité, sans paquet de conventions dont la compatibilité n'est pas établie pour une table.
  /// </summary>
  [Fact]
  public async Task NamesEveryColumnInSnakeCaseOnASingleTable()
  {
    var columns = await ColumnsAsync();

    columns.Keys.Order().ShouldBe(
    [
      "caller_reference",
      "justification",
      "occurred_at",
      "qualification_id",
      "review_signal",
      "rights",
      "text",
      "total_latency_ms",
      "trace_id",
      "verdict_declared_confidence",
      "verdict_engine_name",
      "verdict_engine_version",
      "verdict_latency_ms",
      "verdict_rights",
      "witness_declared_confidence",
      "witness_engine_name",
      "witness_engine_version",
      "witness_latency_ms",
      "witness_rights",
    ]);
  }

  /// <summary>
  /// <b>Le verdict n'est jamais nul, les avis le sont.</b> C'est ce qui rend impossible une ligne
  /// sans verdict — donc une trace de tentative — et c'est aussi ce qui permet à la nullité des
  /// avis d'enregistrer la dégradation sans qu'aucune colonne ne la nomme.
  /// </summary>
  [Fact]
  public async Task LeavesTheVerdictColumnsRequiredAndTheOpinionColumnsNullable()
  {
    var columns = await ColumnsAsync();

    columns["qualification_id"].Nullable.ShouldBeFalse();
    columns["occurred_at"].Nullable.ShouldBeFalse();
    columns["text"].Nullable.ShouldBeFalse();
    columns["rights"].Nullable.ShouldBeFalse();
    columns["review_signal"].Nullable.ShouldBeFalse();
    columns["total_latency_ms"].Nullable.ShouldBeFalse();

    columns["verdict_rights"].Nullable.ShouldBeTrue();
    columns["verdict_declared_confidence"].Nullable.ShouldBeTrue();
    columns["verdict_engine_name"].Nullable.ShouldBeTrue();
    columns["verdict_engine_version"].Nullable.ShouldBeTrue();
    columns["witness_rights"].Nullable.ShouldBeTrue();
    columns["witness_declared_confidence"].Nullable.ShouldBeTrue();
    columns["witness_engine_name"].Nullable.ShouldBeTrue();
    columns["witness_engine_version"].Nullable.ShouldBeTrue();

    // Et rien qui nomme le mode dégradé : la nullité ci-dessus est son seul enregistrement.
    columns.Keys.ShouldNotContain(name => name.Contains("degrad", StringComparison.OrdinalIgnoreCase));
  }

  /// <summary>
  /// Les ensembles de droits sont des tableaux typés, et le texte porte le plafond du contrat
  /// public : la base cesse d'accepter ce que le contrat interdit.
  /// </summary>
  [Fact]
  public async Task TypesTheRightsAsArraysAndMirrorsThePublicCeilingOnTheText()
  {
    var columns = await ColumnsAsync();

    columns["rights"].DataType.ShouldBe("ARRAY");
    columns["verdict_rights"].DataType.ShouldBe("ARRAY");
    columns["witness_rights"].DataType.ShouldBe("ARRAY");

    columns["text"].MaxLength.ShouldBe(10_000);
    columns["caller_reference"].MaxLength.ShouldBe(64);
    columns["occurred_at"].DataType.ShouldBe("timestamp with time zone");
  }

  /// <summary>
  /// <b>Aucun index secondaire.</b> Rien ne lit cette table, aucune purge n'est prévue, et un index
  /// coûterait à chaque écriture — c'est-à-dire sur le chemin synchrone de chaque qualification.
  /// </summary>
  [Fact]
  public async Task CreatesNoIndexBeyondThePrimaryKey()
  {
    var indexes = await ScalarListAsync(
      "select indexname from pg_indexes where tablename = 'qualification_audit_entries'");

    indexes.ShouldBe(["pk_qualification_audit_entries"]);
  }

  /// <summary>
  /// <b>L'entité n'est jamais déclarée agrégat racine</b>, et ne passe donc jamais par le dépôt
  /// générique du gabarit : celui-ci reste intact pour un vrai agrégat, le jour où il en apparaîtra
  /// un. La contrainte du dépôt est vérifiée avec, faute de quoi on pourrait la retirer sans que
  /// rien ne le dise.
  /// </summary>
  [Fact]
  public void NeverDeclaresTheAuditRowAnAggregateRoot()
  {
    typeof(QualificationAuditRow).IsAssignableTo(typeof(IAggregateRoot)).ShouldBeFalse();

    typeof(EfRepository<>).GetGenericArguments()[0]
      .GetGenericParameterConstraints()
      .ShouldContain(typeof(IAggregateRoot));
  }

  private async Task<Dictionary<string, ColumnShape>> ColumnsAsync()
  {
    await using var dbContext = postgres.NewDbContext();
    await using var command = await CommandAsync(
      dbContext,
      """
      select column_name, is_nullable, data_type, character_maximum_length
      from information_schema.columns
      where table_name = 'qualification_audit_entries'
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
