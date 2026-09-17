using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.UseCases.Configuration.ReadSettings;

namespace MicroserviceRgpd.UnitTests.UseCases.Configuration.ReadSettings;

/// <summary>
/// Ce que la lecture du Paramétrage rend : <b>le canal en vigueur de chacun des six droits</b>,
/// « non configuré » compris — et rien du noyau partagé qui serait recopié au passage.
/// </summary>
public class ReadSettingsHandlerTests
{
  private static readonly ExerciseChannel Http = new ExerciseChannel.HttpEndpoint(
    EndpointUrl.From("https://brocanto.example.fr/rgpd/acces"));

  private static readonly ExerciseChannel Routing = new ExerciseChannel.RabbitMq(
    new RabbitMqRouting(ExchangeName.From("rgpd.exercice"), RoutingKey.From("droit.effacement")));

  private readonly IReadRepository<Settings> _settings = Substitute.For<IReadRepository<Settings>>();

  /// <summary>
  /// <b>Les six droits, chacun avec le canal en vigueur</b> : une adresse, un routage, et quatre
  /// « non configuré » — les trois cas du canal se lisent par la même projection.
  /// </summary>
  [Fact]
  public async Task RendersTheChannelInForceOfEachOfTheSixRights()
  {
    var persisted = Settings.Unconfigured();
    persisted.SetChannel(DataSubjectRight.Access, Http);
    persisted.SetChannel(DataSubjectRight.Erasure, Routing);

    var read = await ReadAsync(persisted);

    read.Rights.Select(entry => entry.Right).ShouldBe(Settings.ConfigurableRights);
    read.ChannelFor(DataSubjectRight.Access).ShouldBe(Http);
    read.ChannelFor(DataSubjectRight.Erasure).ShouldBe(Routing);
    read.Rights
      .Where(entry => entry.Right != DataSubjectRight.Access && entry.Right != DataSubjectRight.Erasure)
      .ShouldAllBe(entry => entry.Channel == ExerciseChannel.NotConfigured.Instance);
  }

  /// <summary>
  /// <b>Un service vierge est un résultat, jamais une absence</b> : sans aucune ligne persistée, la
  /// lecture rend les six droits « non configuré ».
  /// </summary>
  [Fact]
  public async Task RendersTheSixRightsNotConfiguredOnAVirginService()
  {
    _settings.ListAsync(Arg.Any<CancellationToken>()).Returns([]);

    var read = await new ReadSettingsHandler(_settings).Handle(
      new ReadSettingsQuery(),
      CancellationToken.None);

    read.Rights.Count.ShouldBe(Settings.ConfigurableRights.Count);
    read.Rights.ShouldAllBe(entry => !entry.IsConfigured);
  }

  /// <summary>
  /// ⚠️ <b>Ni le libellé ni l'article ne sont recopiés</b> : la projection rend le droit lui-même —
  /// le SmartEnum du noyau partagé, qui les porte —, et n'ajoute au droit que ce que lui seul ne sait
  /// pas, son canal.
  /// </summary>
  [Fact]
  public async Task CopiesNeitherTheLabelNorTheArticleOfTheRights()
  {
    var read = await ReadAsync(Settings.Unconfigured());

    var access = read.Rights.Single(entry => entry.Right == DataSubjectRight.Access);

    access.Right.ShouldBeSameAs(DataSubjectRight.Access);
    access.Right.FrenchLabel.ShouldBe(DataSubjectRight.Access.FrenchLabel);
    access.Right.Article.ShouldBe(DataSubjectRight.Access.Article);
  }

  private async Task<Settings> ReadAsync(Settings persisted)
  {
    _settings.ListAsync(Arg.Any<CancellationToken>()).Returns([persisted]);

    return await new ReadSettingsHandler(_settings).Handle(
      new ReadSettingsQuery(),
      CancellationToken.None);
  }
}
