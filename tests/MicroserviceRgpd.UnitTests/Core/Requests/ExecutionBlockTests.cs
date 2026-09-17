using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.UnitTests.Core.Requests;

/// <summary>
/// Le <b>motif de blocage</b> : une valeur fermée de cinq, dont le libellé français est rendu par le
/// serveur. Les deux derniers — ceux qui regardent le canal d'exercice — nomment le droit.
/// </summary>
public class ExecutionBlockTests
{
  [Fact]
  public void ListsExactlyFiveBlocksInTheirOrder()
  {
    ExecutionBlock.List.OrderBy(block => block.Value).Select(block => block.Name).ShouldBe(
      ["Closed", "IdentityNotVerified", "EmailMissing", "RightNotConfigured", "RabbitMqNotYetSupported"]);
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

  /// <summary>« Le {droit} n'est pas configuré », le droit sous son libellé du noyau partagé.</summary>
  [Theory]
  [InlineData("Access", "Le droit d'accès n'est pas configuré")]
  [InlineData("Rectification", "Le droit de rectification n'est pas configuré")]
  [InlineData("Erasure", "Le droit à l'effacement n'est pas configuré")]
  [InlineData("Restriction", "Le droit à la limitation du traitement n'est pas configuré")]
  [InlineData("Portability", "Le droit à la portabilité n'est pas configuré")]
  [InlineData("Objection", "Le droit d'opposition n'est pas configuré")]
  public void NamesTheRightThatIsNotConfigured(string right, string frenchLabel)
  {
    ExecutionBlock.RightNotConfigured.FrenchLabelFor(DataSubjectRight.FromName(right)).ShouldBe(frenchLabel);
  }

  /// <summary>
  /// ⚠️ <b>Le motif provisoire dit la vérité</b> : le droit est configuré, et c'est le service qui ne
  /// sait pas encore publier. Il nomme lui aussi le droit.
  /// </summary>
  [Theory]
  [InlineData("Access", "Le droit d'accès s'exerce par RabbitMQ, que le service ne sait pas encore publier")]
  [InlineData("Erasure", "Le droit à l'effacement s'exerce par RabbitMQ, que le service ne sait pas encore publier")]
  public void SaysTheServiceCannotPublishYet(string right, string frenchLabel)
  {
    ExecutionBlock.RabbitMqNotYetSupported.FrenchLabelFor(DataSubjectRight.FromName(right)).ShouldBe(frenchLabel);
  }
}
