using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.Adapters;

namespace MicroserviceRgpd.UnitTests.Core.Casework.Adapters;

/// <summary>
/// Ce qu'un appel d'<c>Adapter</c> peut rapporter, et ce qu'il ne peut pas.
/// </summary>
public class AdapterAnswerTests
{
  private static readonly DateTimeOffset Deadline = new(2026, 8, 5, 9, 0, 0, TimeSpan.FromHours(2));

  /// <summary>
  /// <b>Deux refus, et ils restent deux.</b> Ils ne se réparent pas au même endroit : un secret
  /// invalide se change dans la configuration de déploiement des deux côtés, un système non servi
  /// se tranche entre le <c>Manifest</c> et l'<c>Adapter</c>.
  /// </summary>
  [Fact]
  public void KnowsExactlyTwoRefusalsAndTellsThemApart()
  {
    AdapterVerdict.List.Where(verdict => verdict.IsRefusal)
      .ShouldBe([AdapterVerdict.SecretRefused, AdapterVerdict.SystemNotServed], ignoreOrder: true);

    AdapterVerdict.SecretRefused.ShouldNotBe(AdapterVerdict.SystemNotServed);
  }

  /// <summary>Servir et répondre ne sont pas des refus : un <c>Adapter</c> qui répond n'est pas en désaccord.</summary>
  [Fact]
  public void CountsNeitherServingNorDeferringAsARefusal()
  {
    AdapterVerdict.Served.IsRefusal.ShouldBeFalse();
    AdapterVerdict.Deferred.IsRefusal.ShouldBeFalse();
  }

  /// <summary>Un différé porte son échéance, et rien d'autre — <b>ramenée en UTC</b>, comme la preuve.</summary>
  [Fact]
  public void CarriesTheDeclaredDeadlineOfADeferralInUtc()
  {
    var answer = AdapterAnswer<Found>.Deferring(Deadline);

    answer.Verdict.ShouldBe(AdapterVerdict.Deferred);
    answer.DeclaredDeadline!.Value.Offset.ShouldBe(TimeSpan.Zero);
    answer.DeclaredDeadline.ShouldBe(new DateTimeOffset(2026, 8, 5, 7, 0, 0, TimeSpan.Zero));
    answer.Served.ShouldBeNull();
  }

  /// <summary>Un refus ne porte ni corps ni échéance : il n'a rien servi et n'a rien promis.</summary>
  [Fact]
  public void CarriesNothingButTheRefusalItself()
  {
    var answer = AdapterAnswer<Found>.Refusing(AdapterVerdict.SecretRefused);

    answer.Served.ShouldBeNull();
    answer.DeclaredDeadline.ShouldBeNull();
  }

  /// <summary>
  /// On ne « refuse » pas avec un verdict qui répond : servir rend un corps, différer rend une
  /// échéance, et chacun a sa fabrique — le mélange est une programmation fautive.
  /// </summary>
  [Theory]
  [InlineData(nameof(AdapterVerdict.Served))]
  [InlineData(nameof(AdapterVerdict.Deferred))]
  public void RefusesToBuildARefusalOutOfAnAnswer(string verdict)
  {
    Should.Throw<ArgumentException>(
      () => AdapterAnswer<Found>.Refusing(AdapterVerdict.FromName(verdict)));
  }

  /// <summary>
  /// <b>Un appel ne porte aucun secret</b>, et il n'existe aucun emplacement pour en porter un : le
  /// secret est de la topologie, il vit dans la configuration de déploiement et jamais dans le
  /// <c>Manifest</c> ni dans ce qui en descend.
  /// </summary>
  [Fact]
  public void OffersNoPlaceWhereASecretCouldEverLandOnACall()
  {
    typeof(AdapterCall).GetProperties()
      .Select(property => property.Name)
      .ShouldBe(["Address", "DeclaredSystem", "Capability", "Designations"], ignoreOrder: true);
  }

  private sealed record Found(int Count);
}
