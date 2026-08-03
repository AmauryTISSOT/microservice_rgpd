using MicroserviceRgpd.Core.Qualifications;
using MicroserviceRgpd.Core.Qualifications.Audit;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.Infrastructure.Data.Audit;

namespace MicroserviceRgpd.IntegrationTests.Data.Audit;

/// <summary>
/// Ce que la trace conserve réellement, écrit dans un PostgreSQL réel et relu depuis lui.
/// </summary>
/// <remarks>
/// La relecture n'existe que dans ces tests : le port n'offre aucune lecture, aucun <c>GET</c>
/// n'expose la table, et c'est bien pour cela que ce projet est le seul endroit d'où l'on peut
/// vérifier ce qui s'y écrit.
/// </remarks>
[Collection(PostgreSqlCollection.Name)]
public class QualificationAuditTrailTests(PostgreSqlFixture postgres)
{
  private static readonly RightsRequestText Text =
    RightsRequestText.From("Supprimez toutes les données que vous avez sur moi.");

  private static readonly QualificationEngineIdentity HoldingTheVerdict = new("llm", "qwen3:8b+prompt.1");
  private static readonly QualificationEngineIdentity HoldingTheWitness = new("lexicon", "1.0.0");

  /// <summary>
  /// La ligne conserve le verdict <b>et ses prémisses</b> : les deux avis bruts, avec le nom et la
  /// version de chaque moteur. Une conclusion sans ses prémisses ne permettrait de répondre de rien.
  /// </summary>
  [Fact]
  public async Task KeepsTheVerdictItsPremisesAndTheIdentityOfBothEngines()
  {
    var entry = NominalEntry();

    await RecordAsync(entry);

    var row = await RowOfAsync(entry.QualificationId);
    row.Text.ShouldBe(Text.Value);
    row.Rights.ShouldBe(["Erasure"]);
    row.ReviewSignal.ShouldBe("Corroborated");
    row.VerdictRights.ShouldBe(["Erasure"]);
    row.VerdictDeclaredConfidence.ShouldBe("High");
    row.VerdictEngineName.ShouldBe("llm");
    row.VerdictEngineVersion.ShouldBe("qwen3:8b+prompt.1");
    row.WitnessRights.ShouldBe(["Access", "Erasure"]);
    // Le témoin n'en déclare aucune : la colonne reste nulle plutôt que de porter une confiance
    // constante, que le domaine refuse d'inventer pour lui.
    row.WitnessDeclaredConfidence.ShouldBeNull();
    row.WitnessEngineName.ShouldBe("lexicon");
    row.WitnessEngineVersion.ShouldBe("1.0.0");
    row.Justification.ShouldBe("Le texte demande la suppression des données.");
    row.CallerReference.ShouldBe("DSAR-8871");
    row.TraceId.ShouldBe("4bf92f3577b34da6a3ce929d0e0e4736");
    row.OccurredAt.ShouldBe(entry.OccurredAt);
    row.TotalLatencyMs.ShouldBe(1_400);
    row.VerdictLatencyMs.ShouldBe(1_390);
    row.WitnessLatencyMs.ShouldBe(2);
  }

  /// <summary>
  /// <b>Aucune colonne ne nomme le mode dégradé</b> : c'est la nullité des colonnes d'avis qui
  /// l'enregistre, et elle dit davantage que le booléen public — un repli lexical et un lexique
  /// absent laissent deux formes de ligne distinguables, là où le booléen les recouvre.
  /// </summary>
  [Fact]
  public async Task DistinguishesTheLexicalFallbackFromTheMissingWitnessByNullityAlone()
  {
    var fallback = NominalEntry() with
    {
      QualificationId = Guid.CreateVersion7(),
      VerdictOpinion = null,
      VerdictLatency = null,
      ReviewSignal = ReviewSignal.NeedsReview,
      Justification = null,
    };

    var uncontrolled = NominalEntry() with
    {
      QualificationId = Guid.CreateVersion7(),
      WitnessOpinion = null,
      WitnessLatency = null,
      ReviewSignal = ReviewSignal.NeedsReview,
    };

    await RecordAsync(fallback);
    await RecordAsync(uncontrolled);

    var replied = await RowOfAsync(fallback.QualificationId);
    replied.VerdictRights.ShouldBeNull();
    replied.VerdictEngineName.ShouldBeNull();
    replied.VerdictEngineVersion.ShouldBeNull();
    replied.VerdictDeclaredConfidence.ShouldBeNull();
    replied.VerdictLatencyMs.ShouldBeNull();
    replied.WitnessRights.ShouldNotBeNull();

    var uncontrolledRow = await RowOfAsync(uncontrolled.QualificationId);
    uncontrolledRow.WitnessRights.ShouldBeNull();
    uncontrolledRow.WitnessDeclaredConfidence.ShouldBeNull();
    uncontrolledRow.WitnessEngineName.ShouldBeNull();
    uncontrolledRow.WitnessEngineVersion.ShouldBeNull();
    uncontrolledRow.WitnessLatencyMs.ShouldBeNull();
    uncontrolledRow.VerdictRights.ShouldNotBeNull();

    // Et dans les deux cas le verdict, lui, est écrit : la dégradation n'ampute pas la ligne de ce
    // dont il s'agit de répondre.
    replied.Rights.ShouldBe(["Erasure"]);
    uncontrolledRow.Rights.ShouldBe(["Erasure"]);
  }

  /// <summary>
  /// Les droits sont conservés aux <b>noms canoniques anglais</b> de la taxonomie : la base
  /// n'introduit aucun troisième vocabulaire, et l'ordre de la taxonomie est figé pour que deux
  /// lignes identiques se lisent identiquement.
  /// </summary>
  [Fact]
  public async Task StoresRightsUnderTheirCanonicalNamesInTaxonomyOrder()
  {
    var entry = NominalEntry() with
    {
      QualificationId = Guid.CreateVersion7(),
      Qualification = Qualification.Of([DataSubjectRight.Portability, DataSubjectRight.Access]),
    };

    await RecordAsync(entry);

    (await RowOfAsync(entry.QualificationId)).Rights.ShouldBe(["Access", "Portability"]);
  }

  /// <summary>
  /// L'instant est conservé en UTC, quel que soit le fuseau de la machine qui a écrit : une trace
  /// dont l'heure dépendrait de l'endroit où tourne le service ne prouverait pas grand-chose.
  /// </summary>
  [Fact]
  public async Task KeepsTheInstantOfTheActInUtc()
  {
    var entry = NominalEntry() with
    {
      QualificationId = Guid.CreateVersion7(),
      OccurredAt = new DateTimeOffset(2026, 3, 14, 9, 30, 0, TimeSpan.FromHours(2)),
    };

    await RecordAsync(entry);

    var row = await RowOfAsync(entry.QualificationId);
    row.OccurredAt.Offset.ShouldBe(TimeSpan.Zero);
    row.OccurredAt.UtcDateTime.ShouldBe(new DateTime(2026, 3, 14, 7, 30, 0, DateTimeKind.Utc));
  }

  private static QualificationAuditEntry NominalEntry()
  {
    return new QualificationAuditEntry(
      Guid.CreateVersion7(),
      new DateTimeOffset(2026, 3, 14, 7, 30, 0, TimeSpan.Zero),
      Text,
      Qualification.Of([DataSubjectRight.Erasure]),
      ReviewSignal.Corroborated,
      new QualificationOpinion(
        Qualification.Of([DataSubjectRight.Erasure]),
        HoldingTheVerdict,
        DeclaredConfidence.High,
        "Le texte demande la suppression des données."),
      new QualificationOpinion(
        Qualification.Of([DataSubjectRight.Erasure, DataSubjectRight.Access]),
        HoldingTheWitness),
      "Le texte demande la suppression des données.",
      "DSAR-8871",
      "4bf92f3577b34da6a3ce929d0e0e4736",
      TimeSpan.FromMilliseconds(1_400),
      TimeSpan.FromMilliseconds(1_390),
      TimeSpan.FromMilliseconds(2));
  }

  private async Task RecordAsync(QualificationAuditEntry entry)
  {
    await using var dbContext = postgres.NewDbContext();

    await new QualificationAuditTrail(dbContext).RecordAsync(entry, CancellationToken.None);
  }

  private async Task<QualificationAuditRow> RowOfAsync(Guid qualificationId)
  {
    await using var dbContext = postgres.NewDbContext();

    return await dbContext.QualificationAuditEntries
      .AsNoTracking()
      .SingleAsync(row => row.QualificationId == qualificationId);
  }
}
