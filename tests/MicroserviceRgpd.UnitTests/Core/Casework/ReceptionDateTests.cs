using MicroserviceRgpd.Core.Casework;

namespace MicroserviceRgpd.UnitTests.Core.Casework;

/// <summary>
/// La date de réception, <b>déclarée ou tenue pour défaut</b> — et le fait que les deux ne se
/// confondent jamais.
/// </summary>
/// <remarks>
/// Le test qui compte ici n'est pas celui du chemin déclaré : c'est
/// <see cref="NeverPassesADefaultOffAsADeclaredFact"/>. Un défaut qui se lirait comme un fait
/// déclaré ferait calculer le délai de l'art. 12.3 sur une date que personne n'a affirmée, et
/// personne ne saurait plus laquelle des deux il lit.
/// </remarks>
public class ReceptionDateTests
{
  private static readonly DateTimeOffset Deposited = new(2026, 8, 5, 10, 0, 0, TimeSpan.Zero);

  /// <summary>Ce que l'humain a dit, tel qu'il l'a dit — et rien de plus.</summary>
  [Fact]
  public void KeepsWhatAHumanDeclaredWordForWord()
  {
    var declared = ReceptionDate.Declared(Deposited);

    declared.On.ShouldBe(Deposited);
    declared.IsDefault.ShouldBeFalse();
  }

  /// <summary>
  /// À défaut de déclaration, le service <b>tient neuf jours pour déjà courus</b> : le mois de
  /// l'art. 12.3 part avant nous, et le supposer parti à l'instant du dépôt rendrait un délai
  /// rassurant à un dossier dont personne ne sait depuis quand il attend.
  /// </summary>
  [Fact]
  public void HoldsNineDaysAlreadyRunWhenNobodyDeclaredTheDate()
  {
    var defaulted = ReceptionDate.Defaulted(Deposited);

    defaulted.On.ShouldBe(Deposited.AddDays(-ReceptionDate.DaysHeldAlreadyRunByDefault));
    defaulted.IsDefault.ShouldBeTrue();
  }

  /// <summary>
  /// <b>Le défaut se sait qu'il en est un.</b> C'est ce drapeau que l'écran nomme
  /// « J+9 (défaut) » et que le <c>EvidenceLog</c> consigne : sans lui, la seule chose que le dossier
  /// porterait serait une date, indiscernable d'une date déclarée.
  /// </summary>
  [Fact]
  public void NeverPassesADefaultOffAsADeclaredFact()
  {
    var defaulted = ReceptionDate.Defaulted(Deposited);
    var declared = ReceptionDate.Declared(defaulted.On);

    declared.On.ShouldBe(defaulted.On);
    declared.ShouldNotBe(defaulted);
  }

  /// <summary>
  /// L'instant est ramené en UTC. Le délai de l'art. 12.3 se compte sur cette date, et la faire
  /// dépendre du fuseau de la machine qui a enregistré la demande ferait varier une échéance
  /// juridique d'un serveur à l'autre.
  /// </summary>
  [Fact]
  public void BringsTheInstantBackToUtcRatherThanTrustingTheMachinesTimeZone()
  {
    var declared = ReceptionDate.Declared(new DateTimeOffset(2026, 8, 5, 12, 0, 0, TimeSpan.FromHours(2)));

    declared.On.Offset.ShouldBe(TimeSpan.Zero);
    declared.On.ShouldBe(new DateTimeOffset(2026, 8, 5, 10, 0, 0, TimeSpan.Zero));
  }
}
