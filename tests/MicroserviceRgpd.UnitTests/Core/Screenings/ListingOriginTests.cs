using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UnitTests.Core.Screenings;

/// <summary>
/// L'origine du relevé, <b>et son cas nul</b>. Le cas nul est tout l'objet du type : sans lui,
/// <c>Collé</c> serait la valeur qu'on obtient par oubli, et un rapport scanné rendrait la clause du
/// chemin collé — « le service n'a jamais vu une seule valeur » — sur un rapport qui en a lu cinq
/// par colonne.
/// </summary>
public class ListingOriginTests
{
  [Fact]
  public void ListsExactlyThreeCasesAndTheNullOneSitsAtZero()
  {
    ListingOrigin.List.Select(origin => origin.Name).ShouldBe(
      ["Unspecified", "Pasted", "Scanned"],
      ignoreOrder: true);

    ListingOrigin.Unspecified.Value.ShouldBe(0);
  }

  [Theory]
  [InlineData("Unspecified", "origine non renseignée")]
  [InlineData("Pasted", "collé")]
  [InlineData("Scanned", "scanné")]
  public void CarriesTheFrenchLabelOfEachCase(string name, string frenchLabel)
  {
    ListingOrigin.FromName(name).FrenchLabel.ShouldBe(frenchLabel);
  }

  /// <summary>
  /// <b>Deux chemins qui coexistent, et aucun qui remplace l'autre.</b> Le cas nul n'en est pas un
  /// troisième : il est ce qui reste quand la question n'a pas été posée.
  /// </summary>
  [Fact]
  public void KnowsTwoWaysAListingCanEnterAndOneAbsenceOfAnswer()
  {
    ListingOrigin.Pasted.IsKnown.ShouldBeTrue();
    ListingOrigin.Scanned.IsKnown.ShouldBeTrue();
    ListingOrigin.Unspecified.IsKnown.ShouldBeFalse();
  }

  [Fact]
  public void LetsAKnownOriginThrough()
  {
    ListingOrigin.KnownOrThrow(ListingOrigin.Scanned, "origin").ShouldBe(ListingOrigin.Scanned);
  }

  /// <summary>
  /// ⚠️ <b>Le refus est bruyant, et il nomme le paramètre</b> : un cas nul avalé en silence
  /// deviendrait « collé » un cran plus loin, sur la seule surface où l'<c>Operator</c> peut encore
  /// contredire le service.
  /// </summary>
  [Fact]
  public void RefusesTheNullCaseLoudly()
  {
    var refusal = Should.Throw<ArgumentException>(
      () => ListingOrigin.KnownOrThrow(ListingOrigin.Unspecified, "origin"));

    refusal.ParamName.ShouldBe("origin");
    refusal.Message.ShouldContain("cas nul");
  }

  [Fact]
  public void RefusesAnAbsentOrigin()
  {
    Should.Throw<ArgumentNullException>(() => ListingOrigin.KnownOrThrow(null!, "origin"));
  }
}
