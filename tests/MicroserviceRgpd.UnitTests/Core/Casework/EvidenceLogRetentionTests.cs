using MicroserviceRgpd.Core.Casework.EvidenceLog;

namespace MicroserviceRgpd.UnitTests.Core.Casework;

/// <summary>
/// La vie de l'<c>EvidenceLog</c> : <b>cinq ans à compter de la clôture</b>, et rien d'autre.
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
public class EvidenceLogRetentionTests
{
  private static readonly DateTimeOffset Closed = new(2021, 4, 11, 9, 0, 0, TimeSpan.Zero);

  /// <summary>Cinq ans jour pour jour à compter de la clôture, jamais de la première ligne.</summary>
  [Fact]
  public void ExpiresFiveYearsAfterTheClosure()
  {
    EvidenceLogRetention.ExpiryOf(Closed).ShouldBe(new DateTimeOffset(2026, 4, 11, 9, 0, 0, TimeSpan.Zero));
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
    EvidenceLogRetention.IsExpiredAt(Closed, Closed.AddDays(daysLater)).ShouldBe(expired);
  }

  /// <summary>
  /// ⚠️ <b>La borne large de la base ne perd pas le 29 février.</b> Un dossier clos un 29 février
  /// voit son échéance ramenée au 28 par le calendrier ; une borne calculée à l'envers —
  /// <c>AddYears(-5)</c> sur l'instant du regard — l'écarterait le jour même où sa preuve cesse
  /// d'être due, et la ligne n'apparaîtrait que le lendemain pendant que le geste, lui, l'accepterait
  /// déjà. Deux réponses à la même question, un jour tous les quatre ans.
  /// </summary>
  [Fact]
  public void KeepsWhatALeapDayWouldDropWhenTheBoundIsComputedBackwards()
  {
    var leap = new DateTimeOffset(2020, 2, 29, 9, 0, 0, TimeSpan.Zero);

    // Le 28 février 2025 à dix heures, la preuve close ce 29 février n'est plus due.
    var looking = new DateTimeOffset(2025, 2, 28, 10, 0, 0, TimeSpan.Zero);

    EvidenceLogRetention.IsExpiredAt(leap, looking).ShouldBeTrue();

    // Et la borne que la base applique la laisse passer, là où observedAt.AddYears(-5) l'aurait
    // écartée d'une heure.
    EvidenceLogRetention.ClosedNoLaterThan(looking).ShouldBeGreaterThanOrEqualTo(leap);
  }

  /// <summary>
  /// <b>La borne large n'écarte jamais rien d'échu</b>, quel que soit le jour de clôture — c'est la
  /// seule chose qu'on lui demande. Ce qu'elle laisse passer, <see cref="EvidenceLogRetention.IsExpiredAt"/>
  /// le tranche derrière elle ; ce qu'elle écarterait à tort ne serait jamais rattrapé.
  /// </summary>
  [Fact]
  public void NeverDropsAnythingThatIsAlreadyExpired()
  {
    var looking = new DateTimeOffset(2025, 2, 28, 10, 0, 0, TimeSpan.Zero);

    var bound = EvidenceLogRetention.ClosedNoLaterThan(looking);

    // Quatre ans de clôtures possibles, un jour après l'autre : aucune échue ne doit tomber du côté
    // écarté de la borne.
    for (var closedOn = looking.AddYears(-7); closedOn < looking; closedOn = closedOn.AddDays(1))
    {
      if (EvidenceLogRetention.IsExpiredAt(closedOn, looking))
      {
        closedOn.ShouldBeLessThanOrEqualTo(bound);
      }
    }
  }

  /// <summary>
  /// <b>Cinq ans, écrits en dur.</b> Aucune option, aucun réglage, aucune surcharge : la seule façon
  /// de changer cette durée est de changer ce fichier, ce qui est un geste que quelqu'un signe.
  /// </summary>
  [Fact]
  public void OffersNobodyAWayToShortenOrLengthenIt()
  {
    EvidenceLogRetention.Years.ShouldBe(5);

    typeof(EvidenceLogRetention).GetMethods()
      .Where(method => method.DeclaringType == typeof(EvidenceLogRetention))
      .SelectMany(method => method.GetParameters())
      .Select(parameter => parameter.ParameterType)
      .ShouldAllBe(type => type == typeof(DateTimeOffset));
  }
}
