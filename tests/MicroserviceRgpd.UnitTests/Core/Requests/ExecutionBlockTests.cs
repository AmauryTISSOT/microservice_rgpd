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
      ["Closed", "IdentityNotVerified", "EmailMissing", "RightNotConfigured", "BrokerConnectionMissing"]);
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
  /// ⚠️ <b>Le cinquième motif dit ce qui manque au déploiement, non au Paramétrage</b> : le routage
  /// est bon, et c'est la connexion au broker qui n'existe pas. Il nomme lui aussi le droit.
  /// </summary>
  [Theory]
  [InlineData("Access", "Le droit d'accès s'exerce par RabbitMQ, mais aucune connexion n'est configurée")]
  [InlineData("Erasure", "Le droit à l'effacement s'exerce par RabbitMQ, mais aucune connexion n'est configurée")]
  public void SaysTheDeploymentHasNoBrokerConnection(string right, string frenchLabel)
  {
    ExecutionBlock.BrokerConnectionMissing.FrenchLabelFor(DataSubjectRight.FromName(right)).ShouldBe(frenchLabel);
  }

  /// <summary>
  /// ⚠️ <b>Le motif provisoire de l'ADR-0027 n'existe plus</b> : rien à l'écran ne parle d'une limite
  /// qui n'existe pas — le service publie (ADR-0028). Le nom est cité en texte, faute de symbole.
  /// </summary>
  [Fact]
  public void NoLongerSaysTheServiceCannotPublishYet()
  {
    ExecutionBlock.List.Select(block => block.Name).ShouldNotContain("RabbitMqNotYetSupported");

    ExecutionBlock.List.Select(block => block.FrenchLabelFor(DataSubjectRight.Access))
      .ShouldAllBe(label => !label.Contains("pas encore", StringComparison.Ordinal));
  }
}
