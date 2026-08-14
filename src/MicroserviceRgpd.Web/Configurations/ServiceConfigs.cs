using MicroserviceRgpd.Core.Interfaces;
using MicroserviceRgpd.Infrastructure;
using MicroserviceRgpd.Infrastructure.Email;
using MicroserviceRgpd.UseCases.Casework.CallAdapter;

namespace MicroserviceRgpd.Web.Configurations;

public static class ServiceConfigs
{
  public static IServiceCollection AddServiceConfigs(this IServiceCollection services, Microsoft.Extensions.Logging.ILogger logger, WebApplicationBuilder builder)
  {
    services.AddInfrastructureServices(builder.Configuration, logger)
            .AddMediatorSourceGen(logger);

    // L'appel d'un Adapter au titre d'un Case. Il vit ici plutôt qu'avec le client HTTP :
    // l'Infrastructure ne connaît pas les use cases, et c'est ce qui l'empêche d'apprendre qu'un
    // dossier existe. Scoped, comme l'EvidenceLog dont il écrit la ligne.
    services.AddScoped<AdapterCallsForCase>();

    if (builder.Environment.IsDevelopment())
    {
      // Use a local test email server - configured in Aspire
      // See: https://ardalis.com/configuring-a-local-test-email-server/
      services.AddScoped<IEmailSender, MimeKitEmailSender>();

      // Otherwise use this:
      //builder.Services.AddScoped<IEmailSender, FakeEmailSender>();
    }
    else
    {
      services.AddScoped<IEmailSender, MimeKitEmailSender>();
    }

    logger.LogInformation("{Project} services registered", "Mediator Source Generator and Email Sender");

    return services;
  }


}
