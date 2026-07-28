using MicroserviceRgpd.Core.Qualifications;
using MicroserviceRgpd.Infrastructure.Qualifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Qualifications;

/// <summary>
/// Le câblage des deux moteurs sur leurs rôles. Il est vérifié parce qu'il est la seule chose qui
/// décide <b>quel moteur fait verdict</b> : partout ailleurs, le service ne connaît que des rôles,
/// et échanger les deux ici ferait rendre au lexique un verdict que le contrat réserve au LLM sans
/// qu'aucun autre test ne s'en aperçoive.
/// </summary>
public class QualificationEngineRegistrationTests
{
  [Fact]
  public void GivesTheVerdictToTheLlmAndTheControlToTheLexicon()
  {
    using var services = Registered();

    services.GetRequiredKeyedService<IQualificationEngine>(QualificationEngineRole.Verdict)
      .ShouldBeOfType<LlmQualificationEngine>();
    services.GetRequiredKeyedService<IQualificationEngine>(QualificationEngineRole.Witness)
      .ShouldBeOfType<LexiconQualificationEngine>();
  }

  /// <summary>
  /// Deux clients distincts, et non un seul partagé : ce dont l'un a besoin — une échéance longue —
  /// tuerait précisément ce qui fait l'intérêt de l'autre, dont l'échéance doit rester courte.
  /// </summary>
  [Fact]
  public void GivesEachEngineItsOwnClientRatherThanOneSharedByBoth()
  {
    using var services = Registered();

    var verdict = services.GetRequiredKeyedService<IQualificationEngine>(QualificationEngineRole.Verdict);
    var witness = services.GetRequiredKeyedService<IQualificationEngine>(QualificationEngineRole.Witness);

    verdict.ShouldNotBeSameAs(witness);
  }

  /// <summary>
  /// L'adresse vit en configuration seule : une configuration oubliée arrête le démarrage, là où un
  /// repli codé en dur la transformerait en pannes de qualification au premier appel.
  /// </summary>
  [Fact]
  public void RefusesToStartWithoutASidecarAddress()
  {
    var configuration = new ConfigurationBuilder().Build();

    Should.Throw<ArgumentException>(() => new ServiceCollection().AddQualificationEngines(configuration));
  }

  private static ServiceProvider Registered()
  {
    var configuration = new ConfigurationBuilder()
      .AddInMemoryCollection(new Dictionary<string, string?>
      {
        ["Qualification:SidecarBaseAddress"] = "http://qualification-sidecar",
        ["Qualification:LlmDeadlineSeconds"] = "150",
        ["Qualification:LexiconDeadlineSeconds"] = "5",
      })
      .Build();

    return new ServiceCollection()
      .AddQualificationEngines(configuration)
      .BuildServiceProvider();
  }
}
