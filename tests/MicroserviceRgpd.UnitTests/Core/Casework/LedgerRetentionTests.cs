using MicroserviceRgpd.Core.Casework.Ledger;

namespace MicroserviceRgpd.UnitTests.Core.Casework;

/// <summary>
/// La vie du <c>Ledger</c> : <b>cinq ans à compter de la clôture</b>, et rien d'autre.
/// </summary>
/// <remarks>
/// <para>
/// Ce qui se garde ici est que la durée <b>ne se règle pas</b>. Une durée réglable par client serait
/// une case qui pourrit en silence, et sa valeur basse serait celle que tout le monde garderait : la
/// preuve vit aussi longtemps que l'action qu'elle sert à défendre, ni plus, ni moins.
/// </para>
/// <para>
/// ⚠️ <b>L'échéance ne détruit rien.</b> Elle fait <b>naître une ligne</b> à l'écran de la file, où
/// un humain détruit d'un geste délibéré : rien ne tourne, donc rien ne peut purger en silence.
/// </para>
/// </remarks>
public class LedgerRetentionTests
{
  private static readonly DateTimeOffset Closed = new(2021, 4, 11, 9, 0, 0, TimeSpan.Zero);

  /// <summary>Cinq ans jour pour jour à compter de la clôture, jamais de la première ligne.</summary>
  [Fact]
  public void ExpiresFiveYearsAfterTheClosure()
  {
    LedgerRetention.ExpiryOf(Closed).ShouldBe(new DateTimeOffset(2026, 4, 11, 9, 0, 0, TimeSpan.Zero));
  }

  /// <summary>
  /// L'échéance est un <b>calcul fait à l'instant où l'on regarde</b>, et elle n'est vraie qu'après :
  /// au jour même, la preuve est encore due.
  /// </summary>
  [Theory]
  [InlineData(0, false)]
  [InlineData(1825, false)]
  // Cinq ans depuis le 11 avril 2021 tombent le 11 avril 2026, soit mille huit cent vingt-six jours
  // plus tard — le calendrier compte le 29 février 2024, pas nous. À l'échéance même, la preuve est
  // encore due.
  [InlineData(1826, false)]
  [InlineData(1827, true)]
  [InlineData(4000, true)]
  public void ComputesTheExpiryAtTheInstantSomebodyLooks(int daysLater, bool expired)
  {
    LedgerRetention.IsExpiredAt(Closed, Closed.AddDays(daysLater)).ShouldBe(expired);
  }

  /// <summary>
  /// <b>Cinq ans, écrits en dur.</b> Aucune option, aucun réglage, aucune surcharge : la seule façon
  /// de changer cette durée est de changer ce fichier, ce qui est un geste que quelqu'un signe.
  /// </summary>
  [Fact]
  public void OffersNobodyAWayToShortenOrLengthenIt()
  {
    LedgerRetention.Years.ShouldBe(5);

    typeof(LedgerRetention).GetMethods()
      .Where(method => method.DeclaringType == typeof(LedgerRetention))
      .SelectMany(method => method.GetParameters())
      .Select(parameter => parameter.ParameterType)
      .ShouldAllBe(type => type == typeof(DateTimeOffset));
  }
}
