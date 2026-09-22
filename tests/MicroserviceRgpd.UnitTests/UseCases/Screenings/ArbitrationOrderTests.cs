using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.UseCases.Screenings;
using MicroserviceRgpd.UseCases.Screenings.ReadCurrentScreening;

namespace MicroserviceRgpd.UnitTests.UseCases.Screenings;

/// <summary>
/// <b>La table suivante</b> : après la table ouverte, dans l'ordre du rapport, en reprenant depuis
/// le début — et jamais la table ouverte elle-même.
/// </summary>
public class ArbitrationOrderTests
{
  private static readonly SummarisedTable Adherents = Table("adherents", awaiting: 3);
  private static readonly SummarisedTable Cotisations = Table("cotisations", awaiting: 0);
  private static readonly SummarisedTable Dons = Table("dons", awaiting: 2);
  private static readonly SummarisedTable Evenements = Table("evenements", awaiting: 0);

  private static readonly IReadOnlyList<SummarisedTable> Report =
    [Adherents, Cotisations, Dons, Evenements];

  /// <summary>Sans table ouverte, on commence par la première où une colonne attend.</summary>
  [Fact]
  public void StartsWithTheFirstTableWhereAColumnAwaits()
  {
    ArbitrationOrder.NextAfter([Cotisations, Dons, Adherents], after: null).ShouldBe(Dons);
  }

  /// <summary>Après une table, on saute celles qui n'attendent plus rien.</summary>
  [Fact]
  public void SkipsTheTablesWhereNothingAwaits()
  {
    ArbitrationOrder.NextAfter(Report, Adherents.Identity).ShouldBe(Dons);
  }

  /// <summary>
  /// ⚠️ <b>Arrivé au bout, on reprend depuis le début</b> : une table laissée derrière soi n'est
  /// pas perdue.
  /// </summary>
  [Fact]
  public void WrapsAroundToTheTablesLeftBehind()
  {
    ArbitrationOrder.NextAfter(Report, Evenements.Identity).ShouldBe(Adherents);
    ArbitrationOrder.NextAfter(Report, Dons.Identity).ShouldBe(Adherents);
  }

  /// <summary>
  /// ⚠️ <b>La table ouverte n'est jamais sa propre suivante</b>, même s'il y reste une colonne en
  /// attente : un « Table suivante » qui rouvrirait la même page se lirait comme un bouton en panne.
  /// </summary>
  [Fact]
  public void NeverNamesTheOpenTableAsItsOwnNext()
  {
    ArbitrationOrder.NextAfter([Adherents, Cotisations], Adherents.Identity).ShouldBeNull();
  }

  /// <summary>Plus rien n'attend nulle part : il n'y a pas de table suivante.</summary>
  [Fact]
  public void NamesNoTableOnceNothingAwaitsAnywhere()
  {
    ArbitrationOrder.NextAfter([Cotisations, Evenements], after: null).ShouldBeNull();
    ArbitrationOrder.NextAfter([], after: null).ShouldBeNull();
  }

  /// <summary>Une table que le rapport ne porte pas fait repartir du début.</summary>
  [Fact]
  public void StartsOverFromATableTheReportDoesNotHold()
  {
    ArbitrationOrder.NextAfter(Report, new TableIdentity("public", "inconnue")).ShouldBe(Adherents);
  }

  private static SummarisedTable Table(string name, int awaiting)
  {
    return new SummarisedTable(
      new TableIdentity("public", name),
      ColumnCount: 5,
      FlaggedCount: 1,
      AwaitingCount: awaiting,
      FlaggedAwaitingCount: Math.Min(1, awaiting));
  }
}
