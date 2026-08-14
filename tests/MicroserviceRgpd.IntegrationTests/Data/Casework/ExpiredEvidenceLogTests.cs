using System.Data.Common;
using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.EvidenceLog;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.Infrastructure.Data;
using MicroserviceRgpd.Infrastructure.Data.Casework;

// Shouldly porte un type du même nom, réservé à ses propres tables de cas.
using Case = MicroserviceRgpd.Core.Casework.Case;
using EvidenceLog = MicroserviceRgpd.Infrastructure.Data.Casework.EvidenceLog;

namespace MicroserviceRgpd.IntegrationTests.Data.Casework;

/// <summary>
/// La <b>seule suppression du dispositif</b>, éprouvée sur la vraie base : un <c>EvidenceLog</c> échu,
/// détruit en entier, et rien d'autre.
/// </summary>
/// <remarks>
/// <para>
/// Ce qui se garde ici est l'équilibre du geste : il emporte <b>toutes</b> les lignes d'un dossier —
/// un <c>EvidenceLog</c> amputé se lirait comme complet, ce qui est pire qu'un <c>EvidenceLog</c> absent —,
/// il n'en emporte <b>aucune</b> d'un autre dossier, et il refuse tout ce qui n'est pas échu.
/// </para>
/// <para>
/// ⚠️ <b>Et il ne laisse aucune trace de lui-même.</b> Aucune ligne n'est écrite à la place, ici ou
/// ailleurs : on ne prouvera jamais avoir purgé, et c'est assumé.
/// </para>
/// </remarks>
[Collection(PostgreSqlCollection.Name)]
public class ExpiredEvidenceLogTests(PostgreSqlFixture postgres)
{
  private static readonly DateTimeOffset Closed = new(2021, 4, 11, 9, 0, 0, TimeSpan.Zero);

  /// <summary>Cinq ans après la clôture, à un jour près : la veille, rien ; le lendemain, la ligne.</summary>
  [Fact]
  public async Task ShowsNothingUntilTheDayAfterTheFiveYearsRanOut()
  {
    var closed = await AClosedCaseWithProofAsync();

    await using var dbContext = postgres.NewDbContext();

    var expired = new ExpiredEvidenceLogs(dbContext);

    var due = await expired.ListAsync(EvidenceLogRetention.ExpiryOf(Closed));

    due.ShouldNotContain(line => line.Case == closed);

    var overdue = await expired.ListAsync(EvidenceLogRetention.ExpiryOf(Closed).AddDays(1));

    var line = overdue.Single(one => one.Case == closed);

    line.ClosedOn.ShouldBe(Closed);
    line.ExpiredOn.ShouldBe(EvidenceLogRetention.ExpiryOf(Closed));
  }

  /// <summary>
  /// <b>Un dossier ouvert, ou clos d'hier, n'a rien à détruire</b> — et la destruction le refuse,
  /// quelle que soit la page d'où part le clic.
  /// </summary>
  [Fact]
  public async Task RefusesToDestroyAProofThatIsStillDue()
  {
    var closed = await AClosedCaseWithProofAsync();

    await using var dbContext = postgres.NewDbContext();

    (await new ExpiredEvidenceLogs(dbContext).DestroyAsync(closed, Closed.AddYears(1)))
      .ShouldBeFalse();

    await using var reread = postgres.NewDbContext();

    (await reread.Set<EvidenceLogRow>().CountAsync(row => row.CaseId == closed.Value)).ShouldBe(2);
  }

  /// <summary>
  /// <b>La destruction emporte tout le dossier de preuve, et rien du voisin.</b> Un <c>EvidenceLog</c>
  /// amputé se lirait comme complet ; un <c>EvidenceLog</c> voisin emporté serait une preuve détruite
  /// avant son terme.
  /// </summary>
  [Fact]
  public async Task DestroysEveryLineOfOneCaseAndNotOneOfAnother()
  {
    var expired = await AClosedCaseWithProofAsync();
    var neighbour = await AClosedCaseWithProofAsync();

    await using var dbContext = postgres.NewDbContext();

    (await new ExpiredEvidenceLogs(dbContext).DestroyAsync(expired, Closed.AddYears(6)))
      .ShouldBeTrue();

    await using var reread = postgres.NewDbContext();

    (await reread.Set<EvidenceLogRow>().AnyAsync(row => row.CaseId == expired.Value)).ShouldBeFalse();
    (await reread.Set<EvidenceLogRow>().CountAsync(row => row.CaseId == neighbour.Value)).ShouldBe(2);

    // ⚠️ RIEN N'A ÉTÉ ÉCRIT À LA PLACE. La destruction ne consigne pas sa propre destruction : la
    // seule ligne où elle aurait pu s'écrire est celle qui vient de disparaître.
    (await reread.Set<EvidenceLogRow>().AnyAsync(row => row.CaseId == expired.Value)).ShouldBeFalse();

    // Et la ligne de l'écran s'en va avec la preuve : c'est ainsi, faute de trace, que le geste se
    // voit avoir été fait.
    (await new ExpiredEvidenceLogs(reread).ListAsync(Closed.AddYears(6)))
      .ShouldNotContain(line => line.Case == expired);

    // Le dossier clos, lui, n'est pas touché : il ne nomme plus personne depuis cinq ans, et ce
    // n'est pas lui que la conservation visait.
    (await reread.Cases.AnyAsync(one => one.Id == expired)).ShouldBeTrue();
  }

  /// <summary>
  /// <b>L'index de <c>case_id</c> est posé</b>, et il est le seul : c'est par lui que la file
  /// demande quels dossiers clos portent encore une preuve, et par lui que la destruction emporte un
  /// dossier de preuve entier.
  /// </summary>
  [Fact]
  public async Task IndexesTheOnlyColumnThatTheQueueAndTheDestructionEverReadBy()
  {
    await using var dbContext = postgres.NewDbContext();
    await using var command = await CommandAsync(
      dbContext,
      """
      select indexname
      from pg_indexes
      where tablename = 'evidence_log_entries'
      """);

    var indexes = new List<string>();

    await using var reader = await command.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
      indexes.Add(reader.GetString(0));
    }

    indexes.Order().ShouldBe(["ix_evidence_log_entries_case_id", "pk_evidence_log_entries"]);
  }

  /// <summary>
  /// <b>L'adaptateur qui détruit ne sait rien écrire, et celui qui écrit ne sait rien détruire.</b>
  /// La seule suppression du dispositif tient dans deux méthodes, et l'ajout seul reste ce qu'il est.
  /// </summary>
  [Fact]
  public void OffersNoWayToWriteAProofNorToDeleteASingleLine()
  {
    typeof(ExpiredEvidenceLogs)
      .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
                  | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static)
      .Where(method => method.DeclaringType == typeof(ExpiredEvidenceLogs))
      .Select(method => method.Name)
      .ShouldBe(["ListAsync", "DestroyAsync"], ignoreOrder: true);

    typeof(EvidenceLog)
      .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
                  | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static)
      .Where(method => method.DeclaringType == typeof(EvidenceLog))
      .Select(method => method.Name)
      .ShouldNotContain(name => name.Contains("Destroy", StringComparison.Ordinal)
                                || name.Contains("Delete", StringComparison.Ordinal));
  }

  /// <summary>Un dossier clos le jour dit, et deux lignes de preuve — l'ouverture et la clôture.</summary>
  private async Task<CaseId> AClosedCaseWithProofAsync()
  {
    await using var dbContext = postgres.NewDbContext();

    var opened = Case.Open(
      CaseId.Next(),
      IdentityDeclaration.Unverified,
      motivation: null,
      [Designation.Of(DesignationKind.Email, "jean.dupont@example.fr")],
      [DataSubjectRight.Access],
      ClaimOrigin.Named,
      Manifest.Empty,
      ReceptionDate.Declared(Closed.AddMonths(-1)));

    opened.Close(ClosingCause.Answered, Closed);

    dbContext.Add(opened);

    await dbContext.SaveChangesAsync();

    var evidenceLog = new EvidenceLog(dbContext);

    await evidenceLog.AppendAsync(EvidenceLogEntry.CaseOpened(
      opened.Id,
      Closed.AddMonths(-1),
      Signatory.Operator("Claire Berger", SignerVerification.Unauthenticated),
      IdentityDeclaration.Unverified,
      designationCount: 1,
      reception: ReceptionDate.Declared(Closed.AddMonths(-1))));

    await evidenceLog.AppendAsync(EvidenceLogEntry.CaseClosed(
      opened.Id,
      Closed,
      ClosingCause.Answered,
      motive: null,
      Signatory.Operator("Camille Roy", SignerVerification.Unauthenticated)));

    return opened.Id;
  }

  private static async Task<DbCommand> CommandAsync(AppDbContext dbContext, string sql)
  {
    var connection = dbContext.Database.GetDbConnection();
    await connection.OpenAsync();

    var command = connection.CreateCommand();
    command.CommandText = sql;

    return command;
  }
}
