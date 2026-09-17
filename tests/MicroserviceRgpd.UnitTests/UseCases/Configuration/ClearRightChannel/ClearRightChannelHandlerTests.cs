using Ardalis.Result;
using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.UseCases.Configuration.ClearRightChannel;

namespace MicroserviceRgpd.UnitTests.UseCases.Configuration.ClearRightChannel;

/// <summary>
/// <b>Une seule commande d'effacement pour les deux canaux</b> (ADR-0027, écart n° 1) : ramener un
/// droit à « non configuré » est le même geste, qu'il portât une adresse HTTP ou un routage
/// RabbitMQ.
/// </summary>
public class ClearRightChannelHandlerTests
{
  private static readonly ExerciseChannel Http = new ExerciseChannel.HttpEndpoint(
    EndpointUrl.From("https://brocanto.example.fr/rgpd/acces"));

  private static readonly ExerciseChannel Routing = new ExerciseChannel.RabbitMq(
    new RabbitMqRouting(ExchangeName.From("rgpd.exercice"), RoutingKey.From("droit.acces")));

  private readonly IRepository<Settings> _settings = Substitute.For<IRepository<Settings>>();

  /// <summary>Les deux canaux qu'un droit peut porter, et que la même commande efface.</summary>
  public static TheoryData<ExerciseChannel> TheTwoChannels => new() { Http, Routing };

  /// <summary>
  /// <b>Un droit hors des six est refusé</b> — <c>OutOfScope</c> compris —, par un
  /// <c>Result.Invalid</c> rangé sous le champ du droit.
  /// </summary>
  [Fact]
  public async Task RefusesARightOutsideTheSixUnderTheIdentifierOfTheRight()
  {
    var refused = await HandleAsync(DataSubjectRight.OutOfScope);

    refused.Status.ShouldBe(ResultStatus.Invalid);
    refused.ValidationErrors.ShouldHaveSingleItem()
      .Identifier.ShouldBe(nameof(ClearRightChannelCommand.Right));
    refused.ValidationErrors.Single().ErrorMessage.ShouldContain(DataSubjectRight.OutOfScope.Name);
  }

  /// <summary>
  /// <b>Rien à effacer sur un service vierge</b> : les six droits y sont déjà « non configuré », et
  /// l'effacement réussit <b>sans matérialiser la ligne</b> — seul un enregistrement la fait naître.
  /// </summary>
  [Fact]
  public async Task SucceedsWithoutMaterializingARowOnAVirginService()
  {
    _settings.GetByIdAsync(Settings.SingletonId, Arg.Any<CancellationToken>()).Returns((Settings?)null);

    var cleared = await HandleAsync(DataSubjectRight.Access);

    cleared.IsSuccess.ShouldBeTrue();
    await _settings.DidNotReceive().AddAsync(Arg.Any<Settings>(), Arg.Any<CancellationToken>());
    await _settings.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
  }

  /// <summary>
  /// <b>La commande unique ramène à « non configuré » aussi bien un droit en HTTP qu'un droit en
  /// RabbitMQ</b>, et ne touche aucun autre droit.
  /// </summary>
  [Theory]
  [MemberData(nameof(TheTwoChannels))]
  public async Task BringsBackToNotConfiguredWhicheverChannelTheRightCarried(ExerciseChannel carried)
  {
    var persisted = Settings.Unconfigured();
    persisted.SetChannel(DataSubjectRight.Access, carried);
    persisted.SetChannel(DataSubjectRight.Objection, Routing);
    _settings.GetByIdAsync(Settings.SingletonId, Arg.Any<CancellationToken>()).Returns(persisted);

    var cleared = await HandleAsync(DataSubjectRight.Access);

    cleared.IsSuccess.ShouldBeTrue();
    persisted.ChannelFor(DataSubjectRight.Access).ShouldBe(ExerciseChannel.NotConfigured.Instance);
    persisted.ChannelFor(DataSubjectRight.Objection).ShouldBe(Routing);
  }

  /// <summary>
  /// ⚠️ <b>L'effacement passe par le suivi des modifications, jamais par un <c>Update</c>
  /// global</b> : seules les colonnes du droit effacé partent en base, et un enregistrement
  /// concurrent sur un autre droit n'est pas écrasé.
  /// </summary>
  [Fact]
  public async Task SavesByChangeTrackingRatherThanByAGlobalUpdate()
  {
    _settings.GetByIdAsync(Settings.SingletonId, Arg.Any<CancellationToken>())
      .Returns(Settings.Unconfigured());

    await HandleAsync(DataSubjectRight.Access);

    await _settings.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    await _settings.DidNotReceive().UpdateAsync(Arg.Any<Settings>(), Arg.Any<CancellationToken>());
  }

  private ValueTask<Result> HandleAsync(DataSubjectRight right) =>
    new ClearRightChannelHandler(_settings)
      .Handle(new ClearRightChannelCommand(right), CancellationToken.None);
}
