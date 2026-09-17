using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.Infrastructure.Configuration;
using MicroserviceRgpd.Infrastructure.Requests;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Requests;

/// <summary>
/// L'adaptateur qui publie, éprouvé contre un <c>IChannel</c> substitué <b>pour le seul cas qu'un
/// broker sain ne produit pas</b> : un <c>nack</c> (ADR-0028).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Le succès, le délai dépassé et le non routable ne sont pas ici, et ce n'est pas un oubli.</b>
/// La couture fonctionnelle les prouve contre un <b>vrai broker en conteneur</b> : le message reçu
/// dans une vraie file, la confirmation vraiment attendue, et un exchange sans binding qui rend
/// vraiment le message. Les redire ici sur une doublure ne prouverait que la doublure.
/// </para>
/// <para>
/// Un <c>nack</c>, lui, ne se provoque pas sur un broker sain : il faudrait une panne de disque ou un
/// exchange en faute. C'est le cas dont cette couture, la plus basse, est le domicile.
/// </para>
/// <para>
/// ⚠️ <b>Le délai dépassé y est aussi, et le ticket ne le demandait pas.</b> Il le renvoyait à la
/// couture fonctionnelle — mais un broker en bonne santé confirme en quelques millisecondes, et
/// aucun test contre un vrai broker ne peut le faire tarder. Sans ce test, la traduction
/// « annulation par le délai → <c>TimedOut</c> » ne serait éprouvée nulle part.
/// </para>
/// </remarks>
public class RabbitMqHostSystemTests
{
  private static readonly RabbitMqRouting Routing =
    new(ExchangeName.From("rgpd.exercice"), RoutingKey.From("droit.acces"));

  private readonly IChannel _channel = Substitute.For<IChannel>();

  private readonly IBrokerChannels _channels = Substitute.For<IBrokerChannels>();

  public RabbitMqHostSystemTests() => _channels.OpenAsync(Arg.Any<CancellationToken>()).Returns(_channel);

  /// <summary>
  /// <b>Un <c>nack</c> rend <c>Rejected</c></b> : le broker a refusé la publication. La demande reste
  /// En cours, et rien n'est republié.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Un <c>PublishException</c> dont <c>IsReturn</c> est faux est un <c>nack</c></b>, et un seul
  /// booléen le sépare d'un message non routable. C'est la distinction que ce test ancre.
  /// </remarks>
  [Fact]
  public async Task RendersARejectionWhenTheBrokerNacksThePublication()
  {
    PublishingThrows(new PublishException(publishSequenceNumber: 1, isReturn: false));

    var call = await Publish();

    call.Outcome.ShouldBe(ExecutionOutcome.Rejected);
    call.StatusCode.ShouldBeNull("Une publication ne porte pas de statut HTTP.");
    call.Timeout.ShouldBeNull();
  }

  /// <summary>
  /// ⚠️ <b>Un refus ne jette pas la connexion</b> : le broker a répondu, rien n'est cassé, et la
  /// prochaine publication n'a pas à payer un aller-retour TCP. Le channel, lui, est jeté.
  /// </summary>
  [Fact]
  public async Task KeepsTheConnectionAndDiscardsTheChannelOnARejection()
  {
    PublishingThrows(new PublishException(publishSequenceNumber: 1, isReturn: false));

    await Publish();

    await _channels.DidNotReceive().DiscardAsync();
    await _channel.Received(1).DisposeAsync();
  }

  /// <summary>
  /// ⚠️ <b>Un refus n'est pas une exception pour l'appelant</b> : c'est un résultat de remise comme un
  /// autre, que le journal d'exécution retient.
  /// </summary>
  [Fact]
  public async Task DoesNotThrowWhenThePublicationIsRefused()
  {
    PublishingThrows(new PublishException(publishSequenceNumber: 7, isReturn: false));

    await Should.NotThrowAsync(async () => await Publish());
  }

  /// <summary>
  /// <b>Une confirmation qui n'arrive pas dans le délai rend <c>TimedOut</c></b>, et porte le délai
  /// qui a couru : c'est ce nombre de secondes que l'<c>Operator</c> lit.
  /// </summary>
  [Fact]
  public async Task RendersATimeoutWhenTheBrokerDoesNotConfirmInTime()
  {
    // La publication n'aboutit que lorsque le délai l'annule : exactement ce qu'un broker muet fait.
    PublishingNeverConfirms();

    var call = await Publish(publishTimeoutSeconds: 1);

    call.Outcome.ShouldBe(ExecutionOutcome.TimedOut);
    call.Timeout.ShouldBe(TimeSpan.FromSeconds(1));
    call.StatusCode.ShouldBeNull();
  }

  /// <summary>
  /// <b>Un broker qu'on ne peut pas joindre rend <c>NetworkError</c></b>, et la connexion est jetée :
  /// la prochaine exécution en rouvrira une, et aucune n'est rouverte dans le dos du service.
  /// </summary>
  [Fact]
  public async Task RendersANetworkErrorAndDiscardsTheConnectionWhenTheChannelCannotBeOpened()
  {
    _channels.OpenAsync(Arg.Any<CancellationToken>())
      .Returns<IChannel>(_ => throw new BrokerUnreachableException(new IOException("Rien n'écoute.")));

    var call = await Publish();

    call.Outcome.ShouldBe(ExecutionOutcome.NetworkError);
    await _channels.Received(1).DiscardAsync();
  }

  private void PublishingNeverConfirms() =>
    _channel.BasicPublishAsync(
        Arg.Any<string>(),
        Arg.Any<string>(),
        Arg.Any<bool>(),
        Arg.Any<BasicProperties>(),
        Arg.Any<ReadOnlyMemory<byte>>(),
        Arg.Any<CancellationToken>())
      .Returns(call => new ValueTask(Task.Delay(Timeout.Infinite, call.Arg<CancellationToken>())));

  private void PublishingThrows(Exception refusal) =>
    _channel.BasicPublishAsync(
        Arg.Any<string>(),
        Arg.Any<string>(),
        Arg.Any<bool>(),
        Arg.Any<BasicProperties>(),
        Arg.Any<ReadOnlyMemory<byte>>(),
        Arg.Any<CancellationToken>())
      .Returns(ValueTask.FromException(refusal));

  private Task<HostSystemCall> Publish(int publishTimeoutSeconds = RabbitMqOptions.DefaultPublishTimeoutSeconds) =>
    new RabbitMqHostSystem(
        _channels,
        new RabbitMqOptions { PublishTimeoutSeconds = publishTimeoutSeconds },
        TimeProvider.System)
      .PublishAsync(Routing, ABody(), CancellationToken.None);

  private static ExecutionBody ABody() => new(
    DataSubjectRequestId.Next(),
    DataSubjectRight.Access,
    EmailAddress.From("jeanne.dupont@exemple.fr"),
    null,
    null);
}
