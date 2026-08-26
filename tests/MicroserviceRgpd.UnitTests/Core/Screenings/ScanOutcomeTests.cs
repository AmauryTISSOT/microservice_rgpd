using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UnitTests.Core.Screenings;

/// <summary>
/// Un scan a quatre fins, et aucune n'est le silence.
/// </summary>
public class ScanOutcomeTests
{
  private static readonly ColumnIdentity Courriel =
    ColumnIdentity.Of("main", "abonne", "courriel");

  [Fact]
  public void CarriesThePivotAndThePreviewsWhenItHasListed()
  {
    var previews = new Dictionary<ColumnIdentity, ColumnPreview>
    {
      [Courriel] = ColumnPreview.Absent(PreviewAbsenceReason.NoValueReturned),
    };

    var outcome = ScanOutcome.Listed("{\"format\":\"screening-pivot/1\"}", previews);

    outcome.Ending.ShouldBe(ScanEnding.Listed);
    outcome.Pivot.ShouldNotBeNullOrWhiteSpace();
    outcome.Previews.ShouldContainKey(Courriel);
    outcome.Failure.ShouldBeNull();
  }

  /// <summary>
  /// ⚠️ <b>Un relevé n'est jamais vide de texte.</b> Un pivot blanc franchirait le port pour être
  /// refusé un cran plus loin, et le refus dirait « collage vide » d'un scan qui, lui, a répondu.
  /// </summary>
  [Fact]
  public void RefusesToCallAnEmptyTextAListing()
  {
    Should.Throw<ArgumentException>(() => ScanOutcome.Listed("   ", new Dictionary<ColumnIdentity, ColumnPreview>()));
  }

  /// <summary>
  /// ⚠️ <b>Les deux fins à zéro objet sont deux, et une seule appelle un geste.</b> Les confondre
  /// enverrait l'<c>Operator</c> chercher une base vide quand il lui faut demander un accès.
  /// </summary>
  [Fact]
  public void TellsAnEmptyDatabaseFromOneItCannotSee()
  {
    var empty = ScanOutcome.NoTable();
    var invisible = ScanOutcome.DatabaseAbsentFromCatalogue();

    empty.Ending.ShouldBe(ScanEnding.NoTable);
    invisible.Ending.ShouldBe(ScanEnding.DatabaseAbsentFromCatalogue);
    empty.Ending.ShouldNotBe(invisible.Ending);

    empty.Ending.FrenchLabel.ShouldNotBe(invisible.Ending.FrenchLabel);
  }

  /// <summary>Ni l'une ni l'autre ne rend un relevé : un rapport de zéro colonne ferait reculer le rapport courant.</summary>
  [Fact]
  public void NeitherOfTheTwoEmptyEndingsCarriesAListing()
  {
    ScanOutcome.NoTable().Pivot.ShouldBeNull();
    ScanOutcome.NoTable().Previews.ShouldBeEmpty();
    ScanOutcome.DatabaseAbsentFromCatalogue().Pivot.ShouldBeNull();
    ScanOutcome.DatabaseAbsentFromCatalogue().Previews.ShouldBeEmpty();
  }

  /// <summary>
  /// ⚠️ <b>L'échec dit où et de quel côté, et rien d'autre.</b> Il n'y a pas de champ où glisser le
  /// message du pilote, l'hôte ou l'utilisateur : c'est le type qui tient la règle.
  /// </summary>
  [Fact]
  public void SaysWhereItFellAndWhichSideTheCauseCameFrom()
  {
    var outcome = ScanOutcome.Failed(ScanPhase.Sampling, ScanFailureFamily.Network);

    outcome.Ending.ShouldBe(ScanEnding.Failed);
    outcome.Failure!.Phase.ShouldBe(ScanPhase.Sampling);
    outcome.Failure.Family.ShouldBe(ScanFailureFamily.Network);
    outcome.Pivot.ShouldBeNull();
    outcome.Previews.ShouldBeEmpty();
  }

  /// <summary>
  /// Le <b>port</b> rend quatre fins, et il n'en rendra pas une cinquième. L'abandon en est une
  /// aussi, mais il ne vient pas de la base : c'est l'<c>Operator</c> qui le pose, et aucune
  /// fabrique de <see cref="ScanOutcome"/> ne sait l'écrire.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Le compte se lit sur <see cref="ScanEnding.ComesFromTheScanner"/>, jamais sur la taille de
  /// la liste.</b> Compter les membres aurait fait tomber ce test le jour où une fin posée par le
  /// service s'ajoute — alors que ce qu'il garde est tout autre : qu'aucune fin <b>de la base</b>
  /// n'apparaisse sans que les écrans déjà écrits sachent la dire.
  /// </remarks>
  [Fact]
  public void KnowsFourEndingsFromTheScannerAndNoMore()
  {
    ScanEnding.List.Count(ending => ending.ComesFromTheScanner).ShouldBe(4);
  }

  /// <summary>
  /// Deux fins ne portent jamais le même libellé : c'est celui-là que l'écran montre, et deux fins
  /// qui se disent pareil sont deux fins que l'<c>Operator</c> ne peut pas distinguer.
  /// </summary>
  [Fact]
  public void GivesEachEndingALabelOfItsOwn()
  {
    ScanEnding.List.Select(ending => ending.FrenchLabel).Distinct().Count()
      .ShouldBe(ScanEnding.List.Count);
  }

  /// <summary>
  /// ⚠️ <b>L'abandon n'est pas une fin que la base rend.</b> Aucune fabrique ne l'écrit : il se
  /// pose sur l'avancement, par le geste de l'<c>Operator</c>, et le port n'en sait rien.
  /// </summary>
  [Fact]
  public void KnowsThatAbandonmentIsNotAnEndingTheScannerCanReturn()
  {
    ScanEnding.Abandoned.ComesFromTheScanner.ShouldBeFalse();

    ScanEnding.List
      .Where(ending => ending.ComesFromTheScanner)
      .ShouldNotContain(ScanEnding.Abandoned);
  }
}
