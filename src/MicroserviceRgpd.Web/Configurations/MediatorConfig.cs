using Ardalis.SharedKernel;
using MicroserviceRgpd.Core.ContributorAggregate;
using MicroserviceRgpd.Infrastructure;
using MicroserviceRgpd.UseCases.Contributors.Create;

namespace MicroserviceRgpd.Web.Configurations;

public static class MediatorConfig
{
  // Should be called from ServiceConfigs.cs, not Program.cs
  public static IServiceCollection AddMediatorSourceGen(this IServiceCollection services,
    Microsoft.Extensions.Logging.ILogger logger)
  {
    logger.LogInformation("Registering Mediator SourceGen and Behaviors");
    services.AddMediator(options =>
    {
      // Lifetime: Singleton is fastest per docs; Scoped/Transient also supported.
      options.ServiceLifetime = ServiceLifetime.Scoped;

      // Supply any TYPE from each assembly you want scanned (the generator finds the assembly from the type)
      options.Assemblies =
      [
        typeof(Contributor),                       // Core
        typeof(CreateContributorCommand),         // UseCases
        typeof(InfrastructureServiceExtensions), // Infrastructure
        typeof(MediatorConfig)                  // Web
      ];

      // Register pipeline behaviors here (order matters)
      options.PipelineBehaviors =
      [
        typeof(LoggingBehavior<,>)
      ];

      // If you have stream behaviors:
      // options.StreamPipelineBehaviors = [ typeof(YourStreamBehavior<,>) ];
    });

    // Alternative: register behaviors via DI yourself (useful if not doing AOT):
    // services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
    // services.AddScoped(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));

    return services;
  }
}
