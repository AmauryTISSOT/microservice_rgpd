using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UnitTests.Core.Screenings;

/// <summary>
/// Le transitoire que l'écran d'attente lit : ce qu'il montre, ce qu'il refuse de montrer trop tôt,
/// et ce qu'il ne laisse plus reculer une fois fini.
/// </summary>
public class ScanProgressTests
{
  private static readonly DateTimeOffset StartedOn =
    new(2026, 8, 26, 14, 5, 30, TimeSpan.Zero);

  /// <summary>
  /// ⚠️ <b>Au départ, ni compte ni dénominateur — et <c>null</c>, jamais zéro.</b> « 0 sur 0 » se
  /// lit comme une mesure ; l'absence, elle, ne se lit pas du tout.
  /// </summary>
  [Fact]
  public void StartsWithoutACountBecauseNothingHasAnsweredYet()
  {
    var progress = AScan();

    progress.Snapshot.Phase.ShouldBe(ScanPhase.Connecting);
    progress.Snapshot.Done.ShouldBeNull();
    progress.Snapshot.Total.ShouldBeNull();
    progress.Snapshot.ShowsABar.ShouldBeFalse();
    progress.Snapshot.HasEnded.ShouldBeFalse();
  }

  /// <summary>Le catalogue rendu, la barre devient possible — et son dénominateur est réel.</summary>
  [Fact]
  public void ShowsABarOnceTheCatalogueHasAnswered()
  {
    var progress = AScan();

    progress.Record(ScanStep.CatalogueRead(312));

    progress.Snapshot.Phase.ShouldBe(ScanPhase.Cataloguing);
    progress.Snapshot.Total.ShouldBe(312);
    progress.Snapshot.ShowsABar.ShouldBeTrue();
  }

  /// <summary>Le prélèvement compte des <b>tables</b>, dans le dénominateur du catalogue.</summary>
  [Fact]
  public void CountsTablesWhileSampling()
  {
    var progress = AScan();

    progress.Record(ScanStep.CatalogueRead(312));
    progress.Record(ScanStep.TableSampled(148, 312));

    progress.Snapshot.Phase.ShouldBe(ScanPhase.Sampling);
    progress.Snapshot.Done.ShouldBe(148);
    progress.Snapshot.Total.ShouldBe(312);
  }

  /// <summary>
  /// La détection compte des <b>colonnes</b> : chaque phase porte son dénominateur dans son unité,
  /// et rien ne les additionne.
  /// </summary>
  [Fact]
  public void CountsColumnsWhileDetectingAndNeverAddsTheTwoUnits()
  {
    var progress = AScan();

    progress.Record(ScanStep.CatalogueRead(312));
    progress.Detecting(4_980);

    progress.Snapshot.Phase.ShouldBe(ScanPhase.Detecting);
    progress.Snapshot.Done.ShouldBe(0);
    progress.Snapshot.Total.ShouldBe(4_980);
  }

  /// <summary>Le rapport écrit, le scan est fini et l'écran d'attente sait où mener.</summary>
  [Fact]
  public void CarriesTheReportItProduced()
  {
    var progress = AScan();
    var report = ScreeningId.Next();

    progress.Detecting(12);
    progress.Produced(report);

    progress.Snapshot.HasEnded.ShouldBeTrue();
    progress.Snapshot.Ending.ShouldBe(ScanEnding.Listed);
    progress.Snapshot.Report.ShouldBe(report);
    progress.Snapshot.Done.ShouldBe(progress.Snapshot.Total);
  }

  /// <summary>
  /// ⚠️ <b>Un pas rapporté après la fin ne fait pas reculer l'écran.</b> Le laisser écraser la fin
  /// ramènerait l'<c>Operator</c> du rapport vers une phase, sur un scan que plus personne n'attend.
  /// </summary>
  [Fact]
  public void IgnoresStepsReportedAfterTheEnd()
  {
    var progress = AScan();

    progress.Produced(ScreeningId.Next());
    progress.Record(ScanStep.CatalogueRead(9));

    progress.Snapshot.Ending.ShouldBe(ScanEnding.Listed);
    progress.Snapshot.Phase.ShouldNotBe(ScanPhase.Cataloguing);
  }

  /// <summary>Les fins sans rapport se disent, et elles portent leur phase et leur famille.</summary>
  [Fact]
  public void RecordsAnEndingThatProducedNoReport()
  {
    var progress = AScan();
    var failure = new ScanFailure(ScanPhase.Connecting, ScanFailureFamily.Network);

    progress.EndedWithoutAReport(ScanEnding.Failed, failure);

    progress.Snapshot.HasEnded.ShouldBeTrue();
    progress.Snapshot.Report.ShouldBeNull();
    progress.Snapshot.Failure.ShouldBe(failure);
  }

  /// <summary>
  /// ⚠️ <b>La fin qui produit un rapport ne passe pas par là.</b> Elle porte l'identité du rapport,
  /// et l'écran d'attente doit pouvoir rediriger vers quelque chose.
  /// </summary>
  [Fact]
  public void RefusesToCallTheProducingEndingAnEndingWithoutAReport()
  {
    var progress = AScan();

    Should.Throw<ArgumentException>(() => progress.EndedWithoutAReport(ScanEnding.Listed));
  }

  /// <summary>
  /// ⚠️ <b>L'identité du scan n'est pas celle du rapport.</b> Un scan meurt parfois sans en produire
  /// aucun : réutiliser l'identité du rapport aurait obligé à en engendrer une avant de savoir s'il
  /// existerait une ligne à lui donner.
  /// </summary>
  [Fact]
  public void CarriesAnIdentityOfItsOwn()
  {
    var first = ScanId.Next();
    var second = ScanId.Next();

    first.ShouldNotBe(second);
    ScanProgress.Starting(first, DatabaseDialect.PostgreSql, StartedOn).Id.ShouldBe(first);
  }

  /// <summary>
  /// ⚠️ <b>Une fin déjà posée ne recule pas</b>, et c'est ici que la règle protège le plus : le
  /// rattrapage d'exception du lanceur appelle <c>EndedWithoutAReport</c> sans savoir si le rapport a
  /// été écrit. Sans ce garde, l'écran d'attente annoncerait à l'<c>Operator</c> que rien n'est parti à
  /// l'archive alors que son rapport existe.
  /// </summary>
  [Fact]
  public void KeepsTheProducedReportWhenAFailureIsRecordedAfterIt()
  {
    var progress = AScan();
    var report = ScreeningId.Next();

    progress.Produced(report);
    progress.EndedWithoutAReport(
      ScanEnding.Failed,
      new ScanFailure(ScanPhase.Detecting, ScanFailureFamily.Database));

    progress.Snapshot.Ending.ShouldBe(ScanEnding.Listed);
    progress.Snapshot.Report.ShouldBe(report);
    progress.Snapshot.Failure.ShouldBeNull();
  }

  /// <summary>Et symétriquement : un rapport annoncé après un échec ne ressuscite rien.</summary>
  [Fact]
  public void KeepsTheFailureWhenAReportIsAnnouncedAfterIt()
  {
    var progress = AScan();

    progress.EndedWithoutAReport(
      ScanEnding.Failed,
      new ScanFailure(ScanPhase.Connecting, ScanFailureFamily.Database));
    progress.Produced(ScreeningId.Next());

    progress.Snapshot.Ending.ShouldBe(ScanEnding.Failed);
    progress.Snapshot.Report.ShouldBeNull();
  }

  /// <summary>Les pas rapportés après la fin ne rouvrent pas l'avancement.</summary>
  [Fact]
  public void IgnoresStepsReportedAfterTheEnding()
  {
    var progress = AScan();
    var report = ScreeningId.Next();

    progress.Produced(report);
    progress.Record(ScanStep.CatalogueRead(312));

    progress.Snapshot.Ending.ShouldBe(ScanEnding.Listed);
    progress.Snapshot.Report.ShouldBe(report);
  }

  /// <summary>
  /// L'abandon s'arrête <b>là où le scan en était</b> : la phase est celle du dernier pas, et c'est
  /// elle que l'écran de fin nomme.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>L'abandon ne porte aucune <see cref="ScanFailure"/>.</b> Rien n'a raté : c'est
  /// l'<c>Operator</c> qui a repris la main, et lui coller une famille de cause l'enverrait chercher
  /// une panne du réseau ou de la base là où il n'y en a eu aucune.
  /// </remarks>
  [Fact]
  public void RemembersThePhaseTheScanWasInWhenItWasAbandoned()
  {
    var progress = AScan();

    progress.Record(ScanStep.CatalogueRead(312));
    progress.Record(ScanStep.TableSampled(148, 312));
    progress.Abandon();

    progress.Snapshot.Ending.ShouldBe(ScanEnding.Abandoned);
    progress.Snapshot.Phase.ShouldBe(ScanPhase.Sampling);
    progress.Snapshot.Failure.ShouldBeNull();
    progress.Snapshot.Report.ShouldBeNull();
  }

  /// <summary>
  /// ⚠️ <b>Un geste d'abandon arrivé après la fin n'a aucune conséquence.</b> Le rapport vient
  /// d'être écrit ; dire « abandonné » par-dessus annoncerait à l'<c>Operator</c> le contraire de ce
  /// qui s'est passé, et son rapport courant serait déjà parti à l'archive.
  /// </summary>
  [Fact]
  public void IgnoresAnAbandonmentThatArrivesAfterTheReportWasWritten()
  {
    var progress = AScan();
    var report = ScreeningId.Next();

    progress.Produced(report);
    progress.Abandon();

    progress.Snapshot.Ending.ShouldBe(ScanEnding.Listed);
    progress.Snapshot.Report.ShouldBe(report);
  }

  /// <summary>
  /// Le SGBD est porté par l'avancement, parce que le refus d'un second lancement doit dire de
  /// <b>quelle sorte</b> de base le service est en train de lire le schéma.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Le SGBD, et rien de plus.</b> Ni hôte, ni nom de base, ni utilisateur : ceux-là ne se
  /// connaissent qu'en découpant la chaîne de connexion, et une chaîne rendue par morceaux reste une
  /// chaîne rendue. Le dialecte, lui, est un choix fait dans une liste fermée de trois — il n'apprend
  /// rien que le formulaire n'ait déjà affiché.
  /// </remarks>
  [Fact]
  public void CarriesTheDialectSoThatARefusalCanNameWhatIsRunning()
  {
    var progress = ScanProgress.Starting(ScanId.Next(), DatabaseDialect.Sqlite, StartedOn);

    progress.Dialect.ShouldBe(DatabaseDialect.Sqlite);
  }

  private static ScanProgress AScan()
  {
    return ScanProgress.Starting(ScanId.Next(), DatabaseDialect.PostgreSql, StartedOn);
  }
}
