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
/// <para>
/// C'est aussi ici que se vérifie l'autre décision de câblage : <b>éteint, le rôle de verdict n'est
/// pourvu par rien</b>. Ce qui n'est pas branché n'existe pas, et rien n'a donc à décider de ne pas
/// l'appeler.
/// </para>
/// </summary>
public class QualificationEngineRegistrationTests
{
  [Fact]
  public void GivesTheVerdictToTheLlmAndTheControlToTheLexicon()
  {
    using var services = Registered();

    services.GetRequiredKeyedService<IQualificationEngine>(QualificationEngineRole.Verdict)
      .ShouldBeOfType<LlmQualificationEngine>();
    services.GetRequiredKeyedService<IQualificationEngine>(QualificationEngineRole.Lexicon)
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
    var lexicon = services.GetRequiredKeyedService<IQualificationEngine>(QualificationEngineRole.Lexicon);

    verdict.ShouldNotBeSameAs(lexicon);
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

  /// <summary>
  /// <b>Le défaut est « éteint ».</b> C'est la seule clé de la section à disposer d'un repli, et
  /// l'écart à la doctrine du dépôt est délibéré : un déploiement qui ne dit rien ne soumet jamais le
  /// texte d'une personne concernée à un modèle génératif.
  /// </summary>
  [Fact]
  public void LeavesTheVerdictRoleUnprovisionedWhenNothingTurnsTheLlmOn()
  {
    using var services = Registered(llm: null);

    services.GetKeyedService<IQualificationEngine>(QualificationEngineRole.Verdict).ShouldBeNull();
  }

  /// <summary>
  /// Éteint, <b>rien</b> n'est enregistré pour ce rôle : ni entrée clé, ni client typé, donc aucun
  /// pipeline de résilience. Un moteur factice qui lèverait à chaque appel ferait journaliser un
  /// avertissement de panne pour un choix délibéré ; il a été écarté.
  /// </summary>
  [Fact]
  public void RegistersNoLlmClientAtAllWhenTheLlmIsOff()
  {
    using var services = Registered(llm: null);

    services.GetService<LlmQualificationEngine>().ShouldBeNull();
  }

  /// <summary>Éteint ou allumé, le lexique est pourvu : c'est lui qui fait tourner le service sans matériel.</summary>
  [Fact]
  public void StillProvidesTheLexiconRoleWhenTheLlmIsOff()
  {
    using var services = Registered(llm: null);

    services.GetRequiredKeyedService<IQualificationEngine>(QualificationEngineRole.Lexicon)
      .ShouldBeOfType<LexiconQualificationEngine>();
  }

  /// <summary>
  /// Éteindre le LLM ne retire pas la dépendance au sidecar : le lexique vit dans ce même sidecar, et
  /// son adresse comme son échéance restent requises dans tous les cas.
  /// </summary>
  [Theory]
  [InlineData("Qualification:SidecarBaseAddress")]
  [InlineData("Qualification:LexiconDeadlineSeconds")]
  public void StillRefusesToStartWithoutWhatTheLexiconNeedsEvenWhenTheLlmIsOff(string missing)
  {
    var settings = Settings(llm: null);
    settings.Remove(missing);

    var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

    Should.Throw<ArgumentException>(() => new ServiceCollection().AddQualificationEngines(configuration));
  }

  /// <summary>
  /// Allumé sans son échéance : refus au démarrage, comme n'importe quelle autre clé manquante. Une
  /// configuration incomplète est une panne bruyante au démarrage, jamais une panne de qualification
  /// au premier appel.
  /// </summary>
  [Fact]
  public void RefusesToStartWhenTheLlmIsOnWithoutItsDeadline()
  {
    var settings = Settings(llm: null);
    settings["Qualification:Llm:Enabled"] = "true";

    var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

    Should.Throw<ArgumentException>(() => new ServiceCollection().AddQualificationEngines(configuration));
  }

  /// <summary>
  /// Éteint, son échéance est ignorée <b>sans bruit</b> : c'est ce qui permet de rallumer plus tard
  /// sans avoir à recomposer ses réglages.
  /// </summary>
  [Fact]
  public void IgnoresAResidualDeadlineLeftBehindByAnLlmThatIsOff()
  {
    var settings = Settings(llm: null);
    settings["Qualification:Llm:DeadlineSeconds"] = "150";

    var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

    using var services = new ServiceCollection().AddQualificationEngines(configuration).BuildServiceProvider();

    services.GetKeyedService<IQualificationEngine>(QualificationEngineRole.Verdict).ShouldBeNull();
  }

  /// <summary>Écrit « false », le drapeau éteint comme son absence.</summary>
  [Fact]
  public void LeavesTheVerdictRoleUnprovisionedWhenTheFlagSaysSoInSoManyWords()
  {
    var settings = Settings(llm: "150");
    settings["Qualification:Llm:Enabled"] = "false";

    var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

    using var services = new ServiceCollection().AddQualificationEngines(configuration).BuildServiceProvider();

    services.GetKeyedService<IQualificationEngine>(QualificationEngineRole.Verdict).ShouldBeNull();
  }

  /// <summary>
  /// Le drapeau est <b>lu</b>, jamais deviné : une valeur qui n'est ni « true » ni « false » arrête le
  /// démarrage. L'éteindre en silence sur une faute de frappe ferait passer une erreur de
  /// configuration pour une décision.
  /// </summary>
  [Theory]
  [InlineData("oui")]
  [InlineData("1")]
  public void RefusesAFlagThatIsNeitherTrueNorFalse(string raw)
  {
    var settings = Settings(llm: "150");
    settings["Qualification:Llm:Enabled"] = raw;

    var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

    Should.Throw<ArgumentException>(() => new ServiceCollection().AddQualificationEngines(configuration));
  }

  /// <summary>
  /// Les réglages d'un service qualifiant : le sidecar et son lexique toujours, le moteur LLM
  /// seulement quand on lui donne une échéance.
  /// </summary>
  private static Dictionary<string, string?> Settings(string? llm)
  {
    var settings = new Dictionary<string, string?>
    {
      ["Qualification:SidecarBaseAddress"] = "http://qualification-sidecar",
      ["Qualification:LexiconDeadlineSeconds"] = "5",
    };

    if (llm is not null)
    {
      settings["Qualification:Llm:Enabled"] = "true";
      settings["Qualification:Llm:DeadlineSeconds"] = llm;
    }

    return settings;
  }

  private static ServiceProvider Registered(string? llm = "150")
  {
    var configuration = new ConfigurationBuilder()
      .AddInMemoryCollection(Settings(llm))
      .Build();

    return new ServiceCollection()
      .AddQualificationEngines(configuration)
      .BuildServiceProvider();
  }
}
