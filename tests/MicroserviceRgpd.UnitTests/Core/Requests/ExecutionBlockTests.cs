using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.UnitTests.Core.Requests;

/// <summary>
/// Le <b>motif de blocage</b> : une valeur fermée de quatre, dont le libellé français est rendu par
/// le serveur. Seul le dernier nomme le droit dont l'adresse manque.
/// </summary>
public class ExecutionBlockTests
{
  [Fact]
  public void ListsExactlyFourBlocksInTheirOrder()
  {
    ExecutionBlock.List.OrderBy(block => block.Value).Select(block => block.Name).ShouldBe(
      ["Closed", "IdentityNotVerified", "EmailMissing", "NoEndpoint"]);
  }

  [Theory]
  [InlineData("Closed", "Demande close")]
  [InlineData("IdentityNotVerified", "Identité non vérifiée")]
  [InlineData("EmailMissing", "Email manquant")]
  public void SaysTheSameWordsWhateverTheRight(string name, string frenchLabel)
  {
    foreach (var right in new[] { DataSubjectRight.Access, DataSubjectRight.Erasure })
    {
      ExecutionBlock.FromName(name).FrenchLabelFor(right).ShouldBe(frenchLabel);
    }
  }

  /// <summary>« Aucune adresse configurée pour le {droit} », le droit sous son libellé du noyau partagé.</summary>
  [Theory]
  [InlineData("Access", "Aucune adresse configurée pour le droit d'accès")]
  [InlineData("Rectification", "Aucune adresse configurée pour le droit de rectification")]
  [InlineData("Erasure", "Aucune adresse configurée pour le droit à l'effacement")]
  [InlineData("Restriction", "Aucune adresse configurée pour le droit à la limitation du traitement")]
  [InlineData("Portability", "Aucune adresse configurée pour le droit à la portabilité")]
  [InlineData("Objection", "Aucune adresse configurée pour le droit d'opposition")]
  public void NamesTheRightWhoseEndpointIsMissing(string right, string frenchLabel)
  {
    ExecutionBlock.NoEndpoint.FrenchLabelFor(DataSubjectRight.FromName(right)).ShouldBe(frenchLabel);
  }
}
