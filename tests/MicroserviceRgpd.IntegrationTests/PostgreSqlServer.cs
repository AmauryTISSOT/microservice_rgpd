using MicroserviceRgpd.Infrastructure.Data;
using Npgsql;
using Testcontainers.PostgreSql;

namespace MicroserviceRgpd.IntegrationTests;

/// <summary>
/// <b>Le serveur PostgreSQL du projet</b> : un seul conteneur pour toute la suite, qui rend à qui
/// lui demande une <b>base neuve et vide</b>.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Ce qui coûte est le conteneur, pas la base.</b> Les classes qui éprouvent une migration ont
/// besoin d'une base <i>non migrée</i> : elles remontent à un état d'avant, y écrivent des lignes de
/// cette date-là, puis jouent la migration. Le conteneur partagé arrivant déjà migré, chacune montait
/// le sien — dix conteneurs pour dix schémas. Or un serveur PostgreSQL héberge autant de bases qu'on
/// veut : <c>CREATE DATABASE</c> coûte quelques dizaines de millisecondes, là où démarrer un conteneur
/// en coûte des milliers, et l'isolement est le même — deux bases d'un même serveur ne partagent ni
/// table, ni séquence, ni transaction.
/// </para>
/// <para>
/// ⚠️ <b>Les classes gardent donc leur parallélisme.</b> Elles ne sont pas rassemblées dans une
/// collection — ce qui les sérialiserait : chacune reste sa propre collection aux yeux de xUnit et
/// demande sa base à ce serveur-ci.
/// </para>
/// <para>
/// ⚠️ <b>Le conteneur n'est jamais arrêté en code, et c'est assumé.</b> xUnit v2 n'offre aucun point
/// d'accroche à l'échelle de l'assembly ; c'est le <i>resource reaper</i> de Testcontainers (Ryuk)
/// qui le retire à la mort du processus de test, comme pour tout conteneur qu'il a créé. Le
/// désactiver (<c>TESTCONTAINERS_RYUK_DISABLED</c>) laisserait un conteneur derrière chaque
/// exécution.
/// </para>
/// </remarks>
internal static class PostgreSqlServer
{
  /// <summary>Sérialise le démarrage : plusieurs classes demandent leur base en même temps.</summary>
  private static readonly SemaphoreSlim Starts = new(1, 1);

  private static PostgreSqlContainer? _server;

  /// <summary>
  /// Crée une base vide sur le serveur et rend sa chaîne de connexion. Le nom porte celui que
  /// l'appelant donne — pour qu'un <c>\l</c> sur un conteneur resté ouvert se lise — suivi de ce qui
  /// le rend unique.
  /// </summary>
  public static async Task<string> NewDatabaseAsync(string name)
  {
    var server = await StartedAsync();
    var database = $"{Sanitized(name)}_{Guid.NewGuid():N}";

    await using var connection = new NpgsqlConnection(server.GetConnectionString());
    await connection.OpenAsync();

    // ⚠️ CREATE DATABASE ne tient pas dans une transaction : la commande passe par Npgsql, et non
    // par ExecuteSql d'EF, dont la stratégie d'exécution en ouvrirait une.
    await using var create = new NpgsqlCommand($"""CREATE DATABASE "{database}" """, connection);
    await create.ExecuteNonQueryAsync();

    return new NpgsqlConnectionStringBuilder(server.GetConnectionString()) { Database = database }
      .ConnectionString;
  }

  /// <summary>Un contexte sur cette base. Chaque test prend le sien.</summary>
  public static AppDbContext NewDbContext(string connectionString)
  {
    return new AppDbContext(
      new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options);
  }

  /// <summary>
  /// Le conteneur lui-même, pour qui doit y exécuter autre chose qu'une requête — <c>psql</c>, par
  /// exemple. ⚠️ Il sert toutes les bases : rien de ce qu'on y lance ne doit viser celle par défaut.
  /// </summary>
  public static async Task<PostgreSqlContainer> StartedAsync()
  {
    if (_server is { } started)
    {
      return started;
    }

    await Starts.WaitAsync();

    try
    {
      if (_server is null)
      {
        var server = new PostgreSqlBuilder("postgres:18-alpine").Build();
        await server.StartAsync();

        _server = server;
      }

      return _server;
    }
    finally
    {
      Starts.Release();
    }
  }

  /// <summary>Ce qu'un nom de base accepte : des minuscules, des chiffres et des tirets bas.</summary>
  private static string Sanitized(string name)
  {
    var kept = name.Where(char.IsLetterOrDigit).Take(24).ToArray();

    return new string(kept).ToLowerInvariant();
  }
}
