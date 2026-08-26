using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.Infrastructure.Screenings.Scanning.PostgreSql;
using MicroserviceRgpd.Infrastructure.Screenings.Scanning.Sqlite;

namespace MicroserviceRgpd.Infrastructure.Screenings.Scanning;

/// <summary>Câble le port de scan et les dialectes dont le service a le pilote.</summary>
/// <remarks>
/// ⚠️ <b>Les dialectes s'enregistrent un par un, et l'aiguillage les reçoit tous.</b> PostgreSQL
/// n'a coûté qu'une ligne ici, et MySQL (#307) en coûtera une autre : ni <c>switch</c> à rallonger,
/// ni <see cref="DatabaseScanner"/> à rouvrir.
/// </remarks>
public static class DatabaseScannerServiceExtensions
{
  public static IServiceCollection AddDatabaseScanner(this IServiceCollection services)
  {
    services.TryAddSingleton(TimeProvider.System);

    // ⚠️ Enregistré une fois quoi qu'il arrive : un second appel à cette extension inscrirait un
    // second scanner du même dialecte, et l'aiguillage, qui les range par dialecte, tomberait à la
    // première résolution — une panne au démarrage pour un câblage écrit deux fois, ce qu'aucune
    // autre inscription du dépôt ne punit.
    services.TryAddEnumerable(
      ServiceDescriptor.Singleton<IDialectScanner, SqliteDialectScanner>());
    services.TryAddEnumerable(
      ServiceDescriptor.Singleton<IDialectScanner, PostgreSqlDialectScanner>());
    services.TryAddSingleton<IDatabaseScanner>(provider =>
      new DatabaseScanner(provider.GetServices<IDialectScanner>()));

    return services;
  }
}
