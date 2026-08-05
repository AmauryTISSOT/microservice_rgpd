using System.Data.Common;
using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.Infrastructure.Data;
using Npgsql;

// Shouldly porte un type du même nom, réservé à ses propres tables de cas.
using Case = MicroserviceRgpd.Core.Casework.Case;

namespace MicroserviceRgpd.IntegrationTests.Data.Casework;

/// <summary>
/// L'agrégat à travers la vraie base : ce qui s'écrit, ce qui se relit, et ce que la racine emporte
/// avec elle.
/// </summary>
/// <remarks>
/// La base est réelle parce que rien d'autre ne prouve ce qui est en jeu : la clé composite d'un
/// <c>Claim</c> — un droit au plus par dossier —, celle d'un <c>Step</c> — un par (<c>Claim</c>,
/// <c>DeclaredSystem</c>) —, et le fait qu'un dossier relu porte son travail dû sans qu'aucun
/// <c>Include</c> ne l'ait demandé.
/// </remarks>
[Collection(PostgreSqlCollection.Name)]
public class CasePersistenceTests(PostgreSqlFixture postgres)
{
  private static readonly DateTimeOffset Received = new(2026, 8, 1, 8, 0, 0, TimeSpan.Zero);
  private static readonly DateTimeOffset Declared = new(2026, 7, 30, 9, 0, 0, TimeSpan.Zero);

  /// <summary>
  /// Le dossier fait l'aller-retour <b>entier</b> : ses <c>Claim</c>, leurs <c>Step</c> et son sac
  /// arrivent avec la racine — un dossier relu sans son travail dû serait un dossier dont
  /// l'incomplétude ne se voit pas.
  /// </summary>
  [Fact]
  public async Task RoundTripsTheWholeCaseThroughItsRootAlone()
  {
    var opened = Open(
      [DataSubjectRight.Access, DataSubjectRight.Erasure],
      [
        Designation.Of(DesignationKind.Email, "jean.dupont@example.fr"),
        Designation.Of(DesignationKind.Reference, "1203"),
      ],
      ASystem("boutique"),
      ASystem("journal"));

    await SaveAsync(opened);

    var reread = await RereadAsync(opened.Id);

    reread.IdentityDeclaration.ShouldBe(IdentityDeclaration.ApplicationSession);
    reread.Reception.ShouldBe(ReceptionDate.Declared(Received));

    reread.Claims.Select(claim => claim.Right).ShouldBe(
      [DataSubjectRight.Access, DataSubjectRight.Erasure], ignoreOrder: true);

    reread.Claims.ShouldAllBe(claim => claim.State == ClaimState.Open);

    foreach (var claim in reread.Claims)
    {
      claim.Steps.Select(step => step.DeclaredSystem).ShouldBe(
        [DeclaredSystemId.From("boutique"), DeclaredSystemId.From("journal")], ignoreOrder: true);

      claim.Steps.ShouldAllBe(step => step.State == StepState.ToDo);
    }

    reread.Designations.Select(designation => (designation.Kind.Token, designation.Value)).ShouldBe(
      [("email", "jean.dupont@example.fr"), ("reference", "1203")], ignoreOrder: true);
  }

  /// <summary>
  /// <b>Une demande portant plusieurs droits donne un seul dossier</b>, et la base ne connaît qu'une
  /// ligne dans <c>cases</c> pour les deux réclamations.
  /// </summary>
  [Fact]
  public async Task WritesASingleRowInCasesWhateverTheNumberOfRights()
  {
    var opened = Open(
      [DataSubjectRight.Access, DataSubjectRight.Erasure, DataSubjectRight.Portability],
      [Designation.Of(DesignationKind.Email, "trois.droits@example.fr")]);

    await SaveAsync(opened);

    var rows = await ScalarListAsync(
      $"select id::text from cases where id = '{opened.Id.Value}'");

    rows.Count.ShouldBe(1);

    var claims = await ScalarListAsync(
      $"select data_subject_right from case_claims where case_id = '{opened.Id.Value}' order by data_subject_right");

    claims.ShouldBe(["Access", "Erasure", "Portability"]);
  }

  /// <summary>
  /// Un <c>Step</c> naît par (<c>Claim</c>, <c>DeclaredSystem</c>), et <b>la base tient la paire</b> :
  /// deux fois le même travail dû sur le même système est impossible, pas seulement improbable.
  /// </summary>
  [Fact]
  public async Task LetsTheDatabaseHoldOneStepPerClaimAndSystem()
  {
    var opened = Open(
      [DataSubjectRight.Access],
      [Designation.Of(DesignationKind.Email, "une.paire@example.fr")],
      ASystem("boutique"));

    await SaveAsync(opened);

    await using var dbContext = postgres.NewDbContext();

    // L'insertion se fait en SQL nu, et non par le modèle : ce qu'on veut prouver est que la
    // *base* refuse la paire en double, y compris à qui contournerait EF Core.
    var refusal = await Should.ThrowAsync<PostgresException>(async () =>
    {
      await dbContext.Database.ExecuteSqlAsync(
        $"""
         insert into case_steps (case_id, data_subject_right, declared_system_id, state)
         values ({opened.Id.Value}, 'Access', 'boutique', 'ToDo')
         """);
    });

    // 23505 : violation d'unicité.
    refusal.SqlState.ShouldBe("23505");
    refusal.ConstraintName.ShouldBe("pk_case_steps");
  }

  /// <summary>
  /// <b>La racine emporte tout ce qui est à elle.</b> C'est ce sur quoi la clôture s'appuiera :
  /// détruire le nominatif d'un dossier ne demandera pas d'aller chercher trois tables à la main.
  /// </summary>
  [Fact]
  public async Task TakesItsClaimsStepsAndBagWithItWhenTheRootGoes()
  {
    var opened = Open(
      [DataSubjectRight.Access],
      [Designation.Of(DesignationKind.PersonName, "Jean Dupont")],
      ASystem("boutique"));

    await SaveAsync(opened);

    await using var dbContext = postgres.NewDbContext();

    dbContext.Cases.Remove(await dbContext.Cases.SingleAsync(one => one.Id == opened.Id));

    await dbContext.SaveChangesAsync();

    (await ScalarListAsync($"select state from case_claims where case_id = '{opened.Id.Value}'")).ShouldBeEmpty();
    (await ScalarListAsync($"select state from case_steps where case_id = '{opened.Id.Value}'")).ShouldBeEmpty();
    (await ScalarListAsync($"select value from case_designations where case_id = '{opened.Id.Value}'")).ShouldBeEmpty();
  }

  /// <summary>
  /// Le dossier se relit par le <b>seul dépôt de ce contexte</b> — celui des <c>Case</c>. Il n'en
  /// existe aucun pour un <c>Claim</c> ni pour un <c>Step</c>, et il ne peut pas en exister : le
  /// dépôt générique est contraint aux agrégats racines, et eux n'en sont pas.
  /// </summary>
  [Fact]
  public async Task ReadsTheCaseThroughTheOnlyRepositoryThisContextHas()
  {
    var opened = Open(
      [DataSubjectRight.Access],
      [Designation.Of(DesignationKind.Email, "par.le.depot@example.fr")],
      ASystem("boutique"));

    await SaveAsync(opened);

    await using var dbContext = postgres.NewDbContext();

    var cases = new EfRepository<Case>(dbContext);

    var reread = await cases.FirstOrDefaultAsync(new CaseByIdSpec(opened.Id));

    reread.ShouldNotBeNull();
    reread.Claims[0].Steps[0].DeclaredSystem.ShouldBe(DeclaredSystemId.From("boutique"));
  }

  /// <summary>
  /// <b>La date de réception et son régime tiennent en deux colonnes, jamais une.</b> Une date nue
  /// serait indiscernable d'une date affirmée par un humain, et le défaut cesserait d'être visible
  /// <em>comme</em> un défaut là où il compte le plus : en base, où il survit à l'écran qui l'a montré.
  /// </summary>
  [Fact]
  public async Task WritesTheReceptionDateAndItsRegimeInTwoDistinctColumns()
  {
    var defaulted = Case.Open(
      CaseId.Next(),
      IdentityDeclaration.Unverified,
      [Designation.Of(DesignationKind.Email, "sans.date@example.fr")],
      [DataSubjectRight.Access],
      Manifest.Empty,
      ReceptionDate.Defaulted(Received));

    await SaveAsync(defaulted);

    var regimes = await ScalarListAsync(
      $"select reception_is_default::text from cases where id = '{defaulted.Id.Value}'");

    regimes.ShouldBe(["true"]);

    var reread = await RereadAsync(defaulted.Id);

    reread.Reception.IsDefault.ShouldBeTrue();
    reread.Reception.On.ShouldBe(Received.AddDays(-ReceptionDate.DaysHeldAlreadyRunByDefault));
  }

  /// <summary>
  /// <b>Un dossier naît <c>Open</c> en base</b>, et l'état y est écrit par son nom : la file s'appuie
  /// sur lui plutôt que de lister « tous les dossiers », pour qu'un dossier clos n'y réapparaisse jamais
  /// le jour où la clôture existera.
  /// </summary>
  [Fact]
  public async Task WritesTheStateByItsNameAndBornsItOpen()
  {
    var opened = Open(
      [DataSubjectRight.Access],
      [Designation.Of(DesignationKind.Email, "etat.ouvert@example.fr")]);

    await SaveAsync(opened);

    var states = await ScalarListAsync($"select state from cases where id = '{opened.Id.Value}'");

    states.ShouldBe([nameof(CaseState.Open)]);
  }

  private static Case Open(
    DataSubjectRight[] rights,
    Designation[] designations,
    params DeclaredSystem[] systems)
  {
    return Case.Open(
      CaseId.Next(),
      IdentityDeclaration.ApplicationSession,
      designations,
      rights,
      Manifest.Of(systems),
      ReceptionDate.Declared(Received));
  }

  private static DeclaredSystem ASystem(string id)
  {
    return DeclaredSystem.Declare(
      DeclaredSystemId.From(id),
      SystemLabel.From(id),
      SystemContents.From("Ce qu'il contient, dans les mots de qui l'a déclaré."),
      [],
      adapterAddress: null,
      Declared);
  }

  private async Task SaveAsync(Case opened)
  {
    await using var dbContext = postgres.NewDbContext();

    dbContext.Cases.Add(opened);

    await dbContext.SaveChangesAsync();
  }

  private async Task<Case> RereadAsync(CaseId id)
  {
    // Un contexte neuf : relire depuis celui qui a écrit ne prouverait que le suivi des
    // modifications, jamais l'aller-retour à travers les colonnes.
    await using var dbContext = postgres.NewDbContext();

    var reread = await dbContext.Cases.SingleOrDefaultAsync(one => one.Id == id);

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
