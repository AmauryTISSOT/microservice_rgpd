using MicroserviceRgpd.Core.Interfaces;
using MicroserviceRgpd.Infrastructure.Email;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Email;

/// <summary>
/// Atteste que les tests unitaires atteignent la couche d'infrastructure : c'est là que vivront
/// les adaptateurs HTTP du sidecar, testés avec un <c>HttpMessageHandler</c> doublé.
/// </summary>
public class FakeEmailSenderTests
{
  [Fact]
  public async Task LogsInsteadOfSendingAnything()
  {
    var logger = Substitute.For<ILogger<FakeEmailSender>>();
    IEmailSender sender = new FakeEmailSender(logger);

    await sender.SendEmailAsync("destinataire@exemple.fr", "expediteur@exemple.fr", "Sujet", "Corps");

    logger.ReceivedCalls().ShouldNotBeEmpty();
  }
}
