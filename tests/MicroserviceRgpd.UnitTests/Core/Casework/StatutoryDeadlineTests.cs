using MicroserviceRgpd.Core.Casework;

namespace MicroserviceRgpd.UnitTests.Core.Casework;

/// <summary>
/// L'échéance de l'art. 12.3, et le <b>calcul</b> du dépassement.
/// </summary>
/// <remarks>
/// <para>
/// Ce qui se garde ici est qu'il n'existe <b>aucun état</b> à atteindre : le dépassement se calcule
/// à l'instant où on le demande, sur la date de réception. Un état persisté ferait dépendre la
/// preuve de ce qu'une minuterie ait tourné, et un retard non détecté deviendrait un retard
/// inexistant.
/// </para>
/// <para>
/// Aucun nombre de jours n'est rendu, et c'est délibéré : la règle des chiffres n'autorise que le
/// dénombrement d'une chose présente que le service détient. Une date et un dépassement sont deux
/// faits ; « il reste 4 jours » serait un compteur sans force juridique.
/// </para>
/// </remarks>
public class StatutoryDeadlineTests
{
  private static readonly DateTimeOffset Received = new(2026, 8, 5, 10, 0, 0, TimeSpan.Zero);

  /// <summary>
  /// Un mois de calendrier depuis la réception, jamais trente jours : c'est le mot du texte, et
  /// trente jours en ferait un délai plus court onze mois sur douze.
  /// </summary>
  [Fact]
  public void FallsOneCalendarMonthAfterTheReception()
  {
    StatutoryDeadline.Of(ReceptionDate.Declared(Received)).On
      .ShouldBe(new DateTimeOffset(2026, 9, 5, 10, 0, 0, TimeSpan.Zero));
  }

  /// <summary>Un mois depuis le 31 janvier tombe fin février : le calendrier tranche, pas nous.</summary>
  [Fact]
  public void LandsOnTheEndOfAShorterMonthWithoutInventingADay()
  {
    var received = new DateTimeOffset(2026, 1, 31, 9, 0, 0, TimeSpan.Zero);

    StatutoryDeadline.Of(ReceptionDate.Declared(received)).On
      .ShouldBe(new DateTimeOffset(2026, 2, 28, 9, 0, 0, TimeSpan.Zero));
  }

  /// <summary>
  /// Le dépassement est un <b>calcul fait à l'instant où l'on regarde</b>, et il n'est vrai
  /// qu'après l'échéance : à l'échéance même, le délai n'est pas dépassé.
  /// </summary>
  [Theory]
  [InlineData(0, false)]
  [InlineData(30, false)]
  // Le 5 août plus un mois tombe le 5 septembre, soit trente et un jours plus tard : à l'échéance
  // même, le délai n'est pas dépassé — le dernier jour est dû.
  [InlineData(31, false)]
  [InlineData(32, true)]
  [InlineData(400, true)]
  public void ComputesTheOverrunAtTheInstantSomebodyLooks(int daysLater, bool overrun)
  {
    var deadline = StatutoryDeadline.Of(ReceptionDate.Declared(Received));

    deadline.IsOverrunAt(Received.AddDays(daysLater)).ShouldBe(overrun);
  }

  /// <summary>
  /// L'échéance se calcule <b>de la même façon sur une date tenue pour défaut</b>. Le défaut se voit
  /// dans ce que l'écran en dit, jamais dans un calcul qui l'épargnerait : un dossier dont personne
  /// n'a déclaré la date n'a droit à aucun délai de faveur.
  /// </summary>
  [Fact]
  public void GivesNoGraceToACaseWhoseDateNobodyDeclared()
  {
    var defaulted = ReceptionDate.Defaulted(Received);

    StatutoryDeadline.Of(defaulted).On.ShouldBe(defaulted.On.AddMonths(1));
  }

  /// <summary>
  /// <b>Rien ici ne rend un nombre de jours.</b> La règle des chiffres n'autorise pas un compteur,
  /// et « sans réponse depuis N jours » est exactement la forme que le contexte refuse : on montre
  /// le fait — une date, un dépassement —, l'<c>Operator</c> juge.
  /// </summary>
  [Fact]
  public void OffersNoCountOfDaysToAnybody()
  {
    typeof(StatutoryDeadline).GetProperties()
      .Concat<System.Reflection.MemberInfo>(typeof(StatutoryDeadline).GetMethods())
      .Select(member => member.Name)
      .ShouldNotContain(name => name.Contains("Day", StringComparison.Ordinal)
                                || name.Contains("Elapsed", StringComparison.Ordinal)
                                || name.Contains("Remaining", StringComparison.Ordinal));
  }
}
