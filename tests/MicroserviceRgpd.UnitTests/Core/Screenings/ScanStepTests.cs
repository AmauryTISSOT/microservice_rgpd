using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UnitTests.Core.Screenings;

/// <summary>
/// L'avancement d'un scan se compte pour de vrai, ou ne se compte pas.
/// </summary>
public class ScanStepTests
{
  /// <summary>
  /// ⚠️ <b>Le catalogue apporte le dénominateur.</b> Un total annoncé avant lui serait le
  /// pourcentage inventé que l'écran d'attente refuse.
  /// </summary>
  [Fact]
  public void BringsTheDenominatorWhenTheCatalogueAnswers()
  {
    var step = ScanStep.CatalogueRead(312);

    step.Phase.ShouldBe(ScanPhase.Cataloguing);
    step.Done.ShouldBe(312);
    step.Total.ShouldBe(312);
  }

  /// <summary>
  /// ⚠️ <b>Le grain est la table, jamais la colonne.</b> Compter en colonnes ferait avancer la barre
  /// par bonds de largeur variable, et le reste à faire ne se lirait plus.
  /// </summary>
  [Fact]
  public void CountsTablesAgainstWhatTheCatalogueReturned()
  {
    var step = ScanStep.TableSampled(148, 312);

    step.Phase.ShouldBe(ScanPhase.Sampling);
    step.Done.ShouldBe(148);
    step.Total.ShouldBe(312);
  }

  /// <summary>
  /// ⚠️ <b>Rien ne dépasse le dénominateur.</b> Une barre qui affiche « 313 sur 312 » a perdu son
  /// compte, et un écran qui ment sur le reste à faire ne vaut pas mieux qu'un écran muet.
  /// </summary>
  [Fact]
  public void RefusesToCountPastTheTotal()
  {
    Should.Throw<ArgumentOutOfRangeException>(() => ScanStep.TableSampled(313, 312));
  }

  [Fact]
  public void RefusesANegativeCount()
  {
    Should.Throw<ArgumentOutOfRangeException>(() => ScanStep.CatalogueRead(-1));
    Should.Throw<ArgumentOutOfRangeException>(() => ScanStep.TableSampled(-1, 312));
  }

  /// <summary>
  /// ⚠️ <b>Aucun nom de table n'y voyage.</b> Un nom de table est du schéma du client, et l'écran
  /// d'attente rafraîchit cet objet toutes les deux secondes.
  /// </summary>
  [Fact]
  public void CarriesNoNameFromTheClientSchema()
  {
    typeof(ScanStep).GetProperties()
      .Select(property => property.PropertyType)
      .ShouldNotContain(typeof(string));
  }

  /// <summary>
  /// La détection n'appartient pas au scan : elle vit dans la phase parce que l'écran d'attente
  /// couvre une phase de plus que le scan, pas parce que le port l'atteindrait.
  /// </summary>
  [Fact]
  public void KnowsFourPhasesInTheOrderTheOperatorCrossesThem()
  {
    ScanPhase.List.OrderBy(phase => phase.Value).Select(phase => phase.Name)
      .ShouldBe(["Connecting", "Cataloguing", "Sampling", "Detecting"]);
  }
}
