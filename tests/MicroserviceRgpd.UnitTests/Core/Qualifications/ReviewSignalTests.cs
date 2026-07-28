using MicroserviceRgpd.Core.Qualifications;

namespace MicroserviceRgpd.UnitTests.Core.Qualifications;

/// <summary>
/// Trois valeurs, pas quatre : ajouter une valeur au signal de relecture est une rupture
/// du contrat public, un appelant ayant écrit un <c>switch</c> exhaustif cassant à la quatrième.
/// </summary>
public class ReviewSignalTests
{
  [Fact]
  public void ExistsAtExactlyThreeValues()
  {
    Enum.GetValues<ReviewSignal>().Length.ShouldBe(3);
  }

  [Fact]
  public void NamesCorroboratedNeedsReviewAndContested()
  {
    Enum.GetNames<ReviewSignal>().ShouldBe(["Corroborated", "NeedsReview", "Contested"], ignoreOrder: true);
  }
}
