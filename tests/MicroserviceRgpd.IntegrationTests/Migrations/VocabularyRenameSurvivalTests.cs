using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MicroserviceRgpd.Infrastructure.Data;
using Testcontainers.PostgreSql;

namespace MicroserviceRgpd.IntegrationTests.Migrations;

/// <summary>
/// <b>Le renommage du vocabulaire renomme ; il ne recrée pas.</b> Une base est montée au dernier
/// état d'<i>avant</i> le renommage, on y écrit des lignes avec les mots de cette date-là, puis on
/// joue la migration : les lignes doivent être encore là, aux nouveaux noms.
/// </summary>
/// <remarks>
/// <para>
/// L'échafaudage d'EF proposait un <c>DropTable</c> suivi d'un <c>CreateTable</c> — le type CLR
/// avait changé de nom en même temps que la table. Une telle migration <b>passe au vert</b> sur
/// toute suite qui monte un schéma neuf : la table finale a le bon nom, les bonnes colonnes, les
/// bons types. Elle n'aurait détruit que la preuve des dossiers déjà ouverts en production, et rien
/// dans la suite ne l'aurait dit. C'est ce trou-là que cette classe bouche, et c'est la seule
/// vérification du dépôt qui exige de la donnée <b>antérieure</b> à la migration qu'elle éprouve.
/// </para>
/// <para>
/// ⚠️ Elle monte son <b>propre</b> conteneur, et non celui de <see cref="PostgreSqlFixture"/> : ce
/// dernier est déjà migré jusqu'au bout quand le premier test le touche, donc il n'y a plus d'avant
/// à y écrire.
/// </para>
/// <para>
/// Ce fichier vit sous <c>Migrations/</c> parce qu'il <b>doit</b> nommer l'ancienne table et ses
/// anciennes colonnes — c'est précisément ce qu'il vérifie qu'on a renommé. C'est la même exception
/// assumée que pour le dossier de migrations du code source.
/// </para>
/// </remarks>
public class VocabularyRenameSurvivalTests : IAsyncLifetime
{
  /// <summary>La dernière migration écrite avec l'ancien vocabulaire.</summary>
  private const string BeforeTheRename = "20260810173953_CreateScreenings";

  private static readonly Guid EntryId = new("11111111-1111-1111-1111-111111111111");
  private static readonly Guid CaseId = new("22222222-2222-2222-2222-222222222222");
  private static readonly Guid QualificationId = new("33333333-3333-3333-3333-333333333333");

  private readonly PostgreSqlContainer _container =
    new PostgreSqlBuilder("postgres:18-alpine").Build();

  public async Task InitializeAsync()
  {
    await _container.StartAsync();

    await using var dbContext = NewDbContext();

    await dbContext.GetService<IMigrator>().MigrateAsync(BeforeTheRename);
    await WriteTheOldWordsAsync(dbContext);
    await dbContext.Database.MigrateAsync();
  }

  public Task DisposeAsync() => _container.DisposeAsync().AsTask();

  /// <summary>
  /// La ligne de preuve écrite avant le renommage se relit après, à l'identique et sous le nouveau
  /// nom de table.
  /// </summary>
  [Fact]
  public async Task KeepsTheProofWrittenBeforeTheTableWasRenamed()
  {
    await using var dbContext = NewDbContext();

    var entries = await ReadAsync<Guid>(
      dbContext, "SELECT entry_id AS \"Value\" FROM evidence_log_entries");

    entries.ShouldBe([EntryId]);

    var facts = await ReadAsync<string>(
      dbContext, "SELECT fact AS \"Value\" FROM evidence_log_entries");

    facts.ShouldBe(["CaseOpened"]);
  }

  /// <summary>La valeur écrite dans l'ancienne colonne se relit dans la nouvelle.</summary>
  [Fact]
  public async Task CarriesTheSignerVerificationAcrossItsRenaming()
  {
    await using var dbContext = NewDbContext();

    var verifications = await ReadAsync<string>(
      dbContext, "SELECT signer_verification AS \"Value\" FROM evidence_log_entries");

    verifications.ShouldBe(["Signed"]);
  }

  /// <summary>
  /// Les cinq membres d'audit changent de préfixe sans rien perdre — y compris le tableau de droits,
  /// dont un <c>DropColumn</c>/<c>AddColumn</c> aurait fait un <c>NULL</c> silencieux.
  /// </summary>
  [Fact]
  public async Task CarriesTheFiveLexiconColumnsAcrossTheirRenaming()
  {
    await using var dbContext = NewDbContext();

    var rights = await ReadAsync<string[]>(
      dbContext, "SELECT lexicon_rights AS \"Value\" FROM qualification_audit_entries");

    rights.Single().ShouldBe(["Access"]);

    var confidences = await ReadAsync<string>(
      dbContext, "SELECT lexicon_declared_confidence AS \"Value\" FROM qualification_audit_entries");

    confidences.ShouldBe(["High"]);

    var engines = await ReadAsync<string>(
      dbContext, "SELECT lexicon_engine_name AS \"Value\" FROM qualification_audit_entries");

    engines.ShouldBe(["mistral"]);

    var versions = await ReadAsync<string>(
      dbContext, "SELECT lexicon_engine_version AS \"Value\" FROM qualification_audit_entries");

    versions.ShouldBe(["7.1"]);

    var latencies = await ReadAsync<int>(
      dbContext, "SELECT lexicon_latency_ms AS \"Value\" FROM qualification_audit_entries");

    latencies.ShouldBe([42]);
  }

  /// <summary>
  /// La clé primaire et l'index portent les nouveaux noms — et ce sont les mêmes objets, puisque la
  /// ligne d'avant est toujours là pour en témoigner.
  /// </summary>
  [Fact]
  public async Task RenamesThePrimaryKeyAndTheIndexOnTheCaseIdentifier()
  {
    await using var dbContext = NewDbContext();

    var constraints = await ReadAsync<string>(
      dbContext,
      """
      SELECT conname AS "Value" FROM pg_constraint
      WHERE conrelid = 'evidence_log_entries'::regclass AND contype = 'p'
      """);

    constraints.ShouldBe(["pk_evidence_log_entries"]);

    var indexes = await ReadAsync<string>(
      dbContext,
      """
      SELECT indexname AS "Value" FROM pg_indexes
      WHERE tablename = 'evidence_log_entries' AND indexname <> 'pk_evidence_log_entries'
      """);

    indexes.ShouldBe(["ix_evidence_log_entries_case_id"]);
  }

  /// <summary>L'ancien nom de table ne répond plus.</summary>
  [Fact]
  public async Task LeavesNoTableUnderTheRetiredName()
  {
    await using var dbContext = NewDbContext();

    var found = await ReadAsync<string>(
      dbContext,
      """
      SELECT table_name AS "Value" FROM information_schema.tables
      WHERE table_schema = 'public' AND table_name = 'ledger_entries'
      """);

    found.ShouldBeEmpty();
  }

  /// <summary>
  /// Une ligne de preuve et une ligne d'audit, écrites avec les mots de leur date. Seules les
  /// colonnes obligatoires sont remplies, plus celles que le renommage touche.
  /// </summary>
  private static async Task WriteTheOldWordsAsync(AppDbContext dbContext)
  {
    await dbContext.Database.ExecuteSqlAsync(
      $"""
      INSERT INTO ledger_entries
        (entry_id, case_id, occurred_at, fact, signatory_kind, signature_regime)
      VALUES
        ({EntryId}, {CaseId}, TIMESTAMPTZ '2026-01-05 09:00:00+00', 'CaseOpened', 'Operator',
         'Signed');
      """);

    await dbContext.Database.ExecuteSqlAsync(
      $"""
      INSERT INTO qualification_audit_entries
        (qualification_id, occurred_at, text, rights, review_signal, total_latency_ms,
         witness_rights, witness_declared_confidence, witness_engine_name, witness_engine_version,
         witness_latency_ms)
      VALUES
        ({QualificationId}, TIMESTAMPTZ '2026-01-05 09:00:00+00', 'Je veux mes données',
         ARRAY['Access']::text[], 'None', 120,
         ARRAY['Access']::text[], 'High', 'mistral', '7.1', 42);
      """);
  }

  private static async Task<List<T>> ReadAsync<T>(AppDbContext dbContext, string sql) =>
    await dbContext.Database.SqlQueryRaw<T>(sql).ToListAsync();

  private AppDbContext NewDbContext() =>
    new(new DbContextOptionsBuilder<AppDbContext>()
      .UseNpgsql(_container.GetConnectionString())
      .Options);
}
