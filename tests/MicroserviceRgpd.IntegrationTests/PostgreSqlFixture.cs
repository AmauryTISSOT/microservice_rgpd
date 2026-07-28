using MicroserviceRgpd.Infrastructure.Data;
using Testcontainers.PostgreSql;

namespace MicroserviceRgpd.IntegrationTests;

/// <summary>
/// Un PostgreSQL réel, monté une fois pour toute la suite, et le schéma posé par les migrations du
/// dépôt — jamais par une création à la volée.
/// </summary>
/// <remarks>
/// <para>
/// <b>La base est réelle parce que rien d'autre ne prouve ce qui est en jeu ici</b> : les types
/// <c>text[]</c> et <c>timestamptz</c>, la nullité déclarée colonne par colonne, le nommage de la
/// table et l'absence d'index secondaire. Une base en mémoire ne partage ni les types, ni les
/// contraintes, et laisserait passer exactement ce qu'on cherche à tenir.
/// </para>
/// <para>
/// Le schéma vient de <c>Migrate</c>, et non d'<c>EnsureCreated</c> : c'est la migration du dépôt
/// que ces tests vérifient, pas le modèle dont elle est issue.
/// </para>
/// </remarks>
public sealed class PostgreSqlFixture : IAsyncLifetime
{
  private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18-alpine").Build();

  /// <summary>La chaîne de connexion du conteneur, une fois démarré.</summary>
  public string ConnectionString => _container.GetConnectionString();

  public async Task InitializeAsync()
  {
    await _container.StartAsync();

    await using var dbContext = NewDbContext();
    await dbContext.Database.MigrateAsync();
  }

  public Task DisposeAsync() => _container.DisposeAsync().AsTask();

  /// <summary>
  /// Un contexte neuf. Chaque test en prend le sien : un contexte partagé rendrait les lectures
  /// dépendantes de ce qu'un autre test a laissé dans le suivi des modifications.
  /// </summary>
  public AppDbContext NewDbContext()
  {
    return new AppDbContext(
      new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionString).Options);
  }
}

/// <summary>
/// Partage un seul conteneur entre toutes les classes de test : en monter un par classe coûterait
/// des dizaines de secondes pour vérifier la même migration.
/// </summary>
[CollectionDefinition(Name)]
public class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>
{
  public const string Name = "PostgreSql";
}
