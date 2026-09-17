using MicroserviceRgpd.Infrastructure.Data;

namespace MicroserviceRgpd.IntegrationTests;

/// <summary>
/// Un PostgreSQL réel — une base à elle sur le serveur partagé du projet, et le schéma posé par les
/// migrations du dépôt, jamais par une création à la volée.
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
  /// <summary>La chaîne de connexion de la base de la suite, une fois créée et migrée.</summary>
  public string ConnectionString { get; private set; } = string.Empty;

  public async Task InitializeAsync()
  {
    ConnectionString = await PostgreSqlServer.NewDatabaseAsync(nameof(PostgreSqlFixture));

    await using var dbContext = NewDbContext();
    await dbContext.Database.MigrateAsync();
  }

  /// <summary>
  /// Rien à défaire : la base vit dans le serveur partagé, que le <i>reaper</i> de Testcontainers
  /// retire à la mort du processus. Voir <see cref="PostgreSqlServer"/>.
  /// </summary>
  public Task DisposeAsync() => Task.CompletedTask;

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
/// Partage une seule base migrée entre les classes de test qui en attendent une : la migrer par
/// classe coûterait des secondes pour poser le même schéma.
/// </summary>
[CollectionDefinition(Name)]
public class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>
{
  public const string Name = "PostgreSql";
}
