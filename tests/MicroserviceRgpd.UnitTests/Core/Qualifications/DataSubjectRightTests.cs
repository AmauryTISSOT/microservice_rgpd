using MicroserviceRgpd.Core.Qualifications;

namespace MicroserviceRgpd.UnitTests.Core.Qualifications;

/// <summary>
/// La taxonomie est fermée par décision : ajouter un membre est une rupture de contrat,
/// pas une extension. Ces tests sont ce qui rend cette rupture bruyante.
/// </summary>
public class DataSubjectRightTests
{
  [Fact]
  public void ListsExactlySevenMembers()
  {
    DataSubjectRight.List.Count.ShouldBe(7);
  }

  [Fact]
  public void NamesTheSixRightsAndTheOutOfScopeVerdict()
  {
    DataSubjectRight.List.Select(right => right.Name).ShouldBe(
      ["Access", "Rectification", "Erasure", "Restriction", "Portability", "Objection", "OutOfScope"],
      ignoreOrder: true);
  }

  [Theory]
  [InlineData("Access", 15)]
  [InlineData("Rectification", 16)]
  [InlineData("Erasure", 17)]
  [InlineData("Restriction", 18)]
  [InlineData("Portability", 20)]
  [InlineData("Objection", 21)]
  public void CarriesTheGdprArticleOfEachRight(string name, int article)
  {
    DataSubjectRight.FromName(name).Article.ShouldBe(article);
  }

  [Fact]
  public void LeavesTheArticleAbsentOnOutOfScope()
  {
    DataSubjectRight.OutOfScope.Article.ShouldBeNull();
  }

  [Theory]
  [InlineData("Access", "droit d'accès")]
  [InlineData("Rectification", "droit de rectification")]
  [InlineData("Erasure", "droit à l'effacement")]
  [InlineData("Restriction", "droit à la limitation du traitement")]
  [InlineData("Portability", "droit à la portabilité")]
  [InlineData("Objection", "droit d'opposition")]
  [InlineData("OutOfScope", "hors périmètre")]
  public void CarriesTheFrenchLabelOfEachMember(string name, string frenchLabel)
  {
    DataSubjectRight.FromName(name).FrenchLabel.ShouldBe(frenchLabel);
  }

  /// <summary>
  /// L'ordinal est stable et ne coïncide avec aucun numéro d'article : sans quoi
  /// <see cref="DataSubjectRight.OutOfScope"/>, qui n'a pas d'article, deviendrait un cas particulier.
  /// </summary>
  [Fact]
  public void NumbersMembersByStableOrdinalNeverByArticle()
  {
    var articles = DataSubjectRight.List.Select(right => right.Article).OfType<int>().ToHashSet();

    DataSubjectRight.List.Select(right => right.Value).ShouldBe([0, 1, 2, 3, 4, 5, 6], ignoreOrder: true);
    DataSubjectRight.List.ShouldAllBe(right => !articles.Contains(right.Value));
  }

  [Fact]
  public void IsSealedSoNoOneAddsAnEighthValue()
  {
    typeof(DataSubjectRight).IsSealed.ShouldBeTrue();
  }
}
