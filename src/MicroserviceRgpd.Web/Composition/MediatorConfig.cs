using Ardalis.SharedKernel;
using MicroserviceRgpd.Infrastructure;
using MicroserviceRgpd.UseCases;

namespace MicroserviceRgpd.Web.Composition;

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
      // Core n'est pas liste : tant qu'il ne contient ni message ni handler, le compilateur n'emet
      // pas sa reference a Mediator.Abstractions et le generateur le rejette. Le remettre
      // (typeof(IEmailSender)) des le premier handler qui y vivrait.
      options.Assemblies =
      [
        typeof(Constants),                       // UseCases
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
