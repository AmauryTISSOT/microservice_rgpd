using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UnitTests.Core.Screenings;

/// <summary>
/// Les comptes d'un rapport chargé, et le fait qu'ils disent chacun ce que leur nom annonce.
/// </summary>
/// <remarks>
/// ⚠️ <b>Il y a deux producteurs pour ces neuf comptes</b> — l'agrégat ici, la base pour l'écran
/// d'une table — et neuf entiers de suite se recopient dans le désordre sans qu'un compilateur ne
/// bronche. Retenues et écartées interverties, l'<c>Operator</c> lit un rapport qui a l'air juste.
/// </remarks>
public class ScreeningCountsTests
{
  private static readonly DateTimeOffset LaunchedOn = new(2026, 8, 6, 9, 30, 0, TimeSpan.Zero);
  private static readonly DateTimeOffset RenderedOn = new(2026, 8, 7, 14, 5, 0, TimeSpan.Zero);

  /// <summary>Chaque compte du rapport arrive au champ qui porte son nom, et à aucun autre.</summary>
  [Fact]
  public void CarriesEachCountOfTheReportUnderTheNameItAnswersTo()
  {
    var screening = AScreening(
      AFlaggedColumn("adr_l1", position: 1),
      AFlaggedColumn("email", position: 2),
      ANothingSeenColumn("id_adh", position: 3),
      ANothingSeenColumn("nom", position: 4),
      ANothingSeenColumn("montant", position: 1, table: "cotisations", tableComment: null));

    // Une signalée retenue, une non signalée retenue — l'Omission relue en acte — et une écartée.
    screening.Arbitrate(
      ColumnIdentity.Of("public", "adherents", "adr_l1"),
      ScreenedColumnState.Retained, RenderedOn);
    screening.Arbitrate(
      ColumnIdentity.Of("public", "adherents", "id_adh"),
      ScreenedColumnState.Retained, RenderedOn);
    screening.Arbitrate(
      ColumnIdentity.Of("public", "cotisations", "montant"),
      ScreenedColumnState.SetAside, RenderedOn);

    var counts = ScreeningCounts.Of(screening);

    counts.Columns.ShouldBe(5);
    counts.Tables.ShouldBe(2);
    counts.ColumnsWithoutAComment.ShouldBe(1);
    counts.Flagged.ShouldBe(2);
    counts.Retained.ShouldBe(2);
    counts.SetAside.ShouldBe(1);
    counts.Awaiting.ShouldBe(2);
    counts.RetainedOnUnflagged.ShouldBe(1);
    counts.UnreadUnflagged.ShouldBe(1);
  }

  /// <summary>
  /// <b>Sur un rapport que personne n'a relu, le verrou tient et la mesure de l'<c>Omission
  /// relue</c> vaut zéro</b> — ce qui est très exactement ce qu'on lui demande de dire.
  /// </summary>
  [Fact]
  public void SaysNothingHasBeenReReadOnAReportNobodyHasTouched()
  {
    var counts = ScreeningCounts.Of(AScreening(
      AFlaggedColumn("email", position: 1),
      ANothingSeenColumn("id_adh", position: 2)));

    counts.Awaiting.ShouldBe(2);
    counts.RetainedOnUnflagged.ShouldBe(0);
    counts.UnreadUnflagged.ShouldBe(1);
  }

  /// <summary>La clause ne se construit pas sur des comptes absents.</summary>
  [Fact]
  public void RefusesToCountAReportThatIsNotThere()
  {
    Should.Throw<ArgumentNullException>(() => ScreeningCounts.Of(null!));
  }

  private static Screening AScreening(params ScreenedColumn[] columns)
  {
    return Screening.Of(
      ScreeningId.Next(),
      "galette_prod",
      "postgresql",
      ListingOrigin.Pasted,
      new ScreeningEngineIdentity("lexique-fr-en", "1.0.0"),
      columns.Length,
      columns,
      LaunchedOn);
  }

  private static ListedColumn AListedLine(
    string column,
    int position,
    string table,
    string? tableComment)
  {
    return ListedColumn.Of(
      ColumnIdentity.Of("public", table, column),
      position,
      dataType: "varchar(255)",
      tableComment: tableComment);
  }

  private static ScreenedColumn AFlaggedColumn(string column, int position, string table = "adherents")
  {
    return ScreenedColumn.Flagged(
      AListedLine(column, position, table, "Les adhérents de l'association."),
      PersonalDataCategory.ContactDetails,
      RuleStrength.Morphological,
      $"« {column} » figure au lexique");
  }

  private static ScreenedColumn ANothingSeenColumn(
    string column,
    int position,
    string table = "adherents",
    string? tableComment = "Les adhérents de l'association.")
  {
    return ScreenedColumn.NothingSeen(AListedLine(column, position, table, tableComment));
  }
}
