using Ardalis.Result;
using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.UseCases.Configuration.SetRightRabbitMqRouting;

namespace MicroserviceRgpd.UnitTests.UseCases.Configuration.SetRightRabbitMqRouting;

/// <summary>
/// Poser un routage RabbitMQ sur un droit : la ligne naît au premier enregistrement, le routage se
/// relit, et les cinq autres droits ne bougent pas.
/// </summary>
/// <remarks>
/// <b>Le routage est écrit, pas éprouvé</b> : rien ici n'ouvre de connexion au broker — un routage
/// doit rester enregistrable quand aucun broker n'est joignable.
/// </remarks>
public class SetRightRabbitMqRoutingHandlerTests
{
  private static readonly RabbitMqRouting Routing =
    new(ExchangeName.From("rgpd.exercice"), RoutingKey.From("droit.effacement"));

  private static readonly RabbitMqRouting AnotherRouting =
    new(ExchangeName.From("rgpd.exercice"), RoutingKey.From("droit.acces"));

  private static readonly EndpointUrl Address =
    EndpointUrl.From("https://brocanto.example.fr/rgpd/effacement");

  private readonly IRepository<Settings> _settings = Substitute.For<IRepository<Settings>>();

  private Settings? _added;

  /// <summary>
  /// <b>Un droit hors des six est refusé</b> — <c>OutOfScope</c> compris —, par un
  /// <c>Result.Invalid</c> qui range le refus sous le champ du droit et nomme celui qu'on lui a
  /// donné. C'est le refus des commandes existantes, au caractère près.
  /// </summary>
  [Fact]
  public async Task RefusesARightOutsideTheSixUnderTheIdentifierOfTheRight()
  {
    var refused = await HandleAsync(DataSubjectRight.OutOfScope);

    refused.Status.ShouldBe(ResultStatus.Invalid);
    refused.ValidationErrors.ShouldHaveSingleItem()
      .Identifier.ShouldBe(nameof(SetRightRabbitMqRoutingCommand.Right));
    refused.ValidationErrors.Single().ErrorMessage.ShouldContain(DataSubjectRight.OutOfScope.Name);

    await _settings.DidNotReceive().AddAsync(Arg.Any<Settings>(), Arg.Any<CancellationToken>());
    await _settings.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
  }

  /// <summary>
  /// <b>Naissance paresseuse</b> : sur un service vierge, la ligne unique n'existe pas encore ; le
  /// premier enregistrement la matérialise, et le routage s'y relit.
  /// </summary>
  [Fact]
  public async Task MaterializesTheRowOnAVirginServiceAndTheRoutingReadsBack()
  {
    GiveNoRow();

    var written = await HandleAsync(DataSubjectRight.Erasure);

    written.IsSuccess.ShouldBeTrue();

    var born = _added.ShouldNotBeNull();

    born.ChannelFor(DataSubjectRight.Erasure).ShouldBe(new ExerciseChannel.RabbitMq(Routing));
    born.Rights
      .Where(entry => entry.Right != DataSubjectRight.Erasure)
      .ShouldAllBe(entry => !entry.IsConfigured);
  }

  /// <summary>
  /// <b>L'écriture suivante remplace le routage</b> du droit visé — l'adresse HTTP qu'il portait
  /// part avec (ADR-0027) — et <b>les autres droits sont intacts</b>.
  /// </summary>
  [Fact]
  public async Task ReplacesTheChannelOfTheRightAndLeavesTheOtherRightsUntouched()
  {
    var persisted = Settings.Unconfigured();
    persisted.SetChannel(DataSubjectRight.Erasure, new ExerciseChannel.HttpEndpoint(Address));
    persisted.SetChannel(DataSubjectRight.Access, new ExerciseChannel.RabbitMq(AnotherRouting));
    GiveTheRow(persisted);

    var written = await HandleAsync(DataSubjectRight.Erasure);

    written.IsSuccess.ShouldBeTrue();
    persisted.ChannelFor(DataSubjectRight.Erasure).ShouldBe(new ExerciseChannel.RabbitMq(Routing));
    persisted.ChannelFor(DataSubjectRight.Access).ShouldBe(new ExerciseChannel.RabbitMq(AnotherRouting));
    persisted.Rights
      .Where(entry => entry.Right != DataSubjectRight.Erasure && entry.Right != DataSubjectRight.Access)
      .ShouldAllBe(entry => !entry.IsConfigured);
  }

  /// <summary>
  /// ⚠️ <b>L'écriture passe par le suivi des modifications, jamais par un <c>Update</c> global</b> :
  /// un <c>Update</c> marquerait les dix-huit colonnes comme modifiées et réécrirait les cinq autres
  /// droits avec la valeur lue un instant plus tôt — un enregistrement concurrent sur un autre droit
  /// serait écrasé en silence.
  /// </summary>
  [Fact]
  public async Task SavesTheExistingRowByChangeTrackingRatherThanByAGlobalUpdate()
  {
    GiveTheRow(Settings.Unconfigured());

    await HandleAsync(DataSubjectRight.Erasure);

    await _settings.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    await _settings.DidNotReceive().UpdateAsync(Arg.Any<Settings>(), Arg.Any<CancellationToken>());
  }

  /// <summary>
  /// Un service vierge : aucune ligne à lire, et le Paramétrage que le handler fera naître est
  /// retenu au passage.
  /// </summary>
  private void GiveNoRow()
  {
    _settings.GetByIdAsync(Settings.SingletonId, Arg.Any<CancellationToken>()).Returns((Settings?)null);
    _settings.AddAsync(Arg.Do<Settings>(born => _added = born), Arg.Any<CancellationToken>());
  }

  private void GiveTheRow(Settings persisted) =>
    _settings.GetByIdAsync(Settings.SingletonId, Arg.Any<CancellationToken>()).Returns(persisted);

  private ValueTask<Result> HandleAsync(DataSubjectRight right) =>
    new SetRightRabbitMqRoutingHandler(_settings)
      .Handle(new SetRightRabbitMqRoutingCommand(right, Routing), CancellationToken.None);
}
