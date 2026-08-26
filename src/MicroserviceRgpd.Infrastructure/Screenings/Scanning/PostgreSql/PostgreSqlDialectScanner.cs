using MicroserviceRgpd.Core.Screenings;
using Npgsql;

namespace MicroserviceRgpd.Infrastructure.Screenings.Scanning.PostgreSql;

/// <summary>
/// Le dialecte PostgreSQL : il joint la base, et confie le reste à <see cref="PostgreSqlScan"/>. Ce
/// fichier ne porte qu'une chose, et c'est <b>la connexion</b>.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>La connexion naît d'une <see cref="NpgsqlDataSource"/> jetable, et jamais d'un
/// <c>new NpgsqlConnection(chaîne)</c>.</b> Ce constructeur-là n'ouvre pas seulement une connexion :
/// Npgsql y range une source de données dans un <b>cache statique de processus</b>, indexé par la
/// chaîne. La chaîne du client — hôte, utilisateur, mot de passe — survivrait alors à l'écran, au
/// scan et à la session d'arbitrage, dans une variable que personne ne vide. Couper le pool sans
/// couper ce cache-là n'aurait fermé qu'une moitié du casier. Ici, la source vit dans un
/// <c>await using</c> : sa disposition ferme les connexions <b>et</b> emporte la chaîne. Un garde
/// d'IL le tient — voir <c>NoDriverStateOutlivesAScanTests</c>.
/// </para>
/// <para>
/// ⚠️ <b>Le pool est coupé.</b> Une connexion rendue au pool garderait une session ouverte sur la
/// base du client après la fin du scan ; ici, la fin du scan est la fin de la connexion.
/// </para>
/// <para>
/// ⚠️ <b>Le pilote est asynchrone de bout en bout, et son annulation coupe la requête.</b> Npgsql
/// ouvre une seconde connexion pour envoyer une demande d'annulation au serveur : le <c>SELECT</c>
/// s'arrête <b>dans la base</b>, et non seulement dans la boucle qui l'entoure. C'est toute la
/// différence avec SQLite, où il a fallu aller chercher <c>sqlite3_interrupt</c>.
/// </para>
/// <para>
/// ⚠️ <b>Aucune exception du pilote ne traverse.</b> Un hôte injoignable devient un
/// <see cref="ScanOutcome.Failed"/> à phase et famille nommées — sans message du pilote, sans hôte,
/// sans utilisateur. Voir <see cref="PostgreSqlFailures"/>.
/// </para>
/// </remarks>
internal sealed class PostgreSqlDialectScanner : IDialectScanner
{
  private readonly TimeProvider _clock;

  public PostgreSqlDialectScanner(TimeProvider clock)
  {
    ArgumentNullException.ThrowIfNull(clock);

    _clock = clock;
  }

  /// <inheritdoc />
  public DatabaseDialect Dialect => DatabaseDialect.PostgreSql;

  /// <inheritdoc />
  public async Task<ScanOutcome> ScanAsync(
    string connectionString,
    IProgress<ScanStep>? progress,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    if (!TryPrepare(connectionString, out var prepared))
    {
      return ScanOutcome.Failed(ScanPhase.Connecting, ScanFailureFamily.Supplied);
    }

    await using var source = new NpgsqlDataSourceBuilder(prepared).Build();

    NpgsqlConnection connection;

    try
    {
      connection = await source.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
    }
    catch (Exception failure) when (PostgreSqlScan.IsDriverFailure(failure))
    {
      return ScanOutcome.Failed(ScanPhase.Connecting, PostgreSqlFailures.FamilyOf(failure));
    }

    await using (connection.ConfigureAwait(false))
    {
      return await PostgreSqlScan.RunAsync(
        new NpgsqlSession(connection),
        _clock,
        progress,
        cancellationToken).ConfigureAwait(false);
    }
  }

  /// <summary>
  /// Prépare la chaîne que le service emploiera réellement : le pool coupé, et rien d'autre de
  /// changé à ce que l'<c>Operator</c> a fourni.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Le multiplexage se coupe avec le pool, et pas par précaution.</b> Npgsql refuse de
  /// construire une source multiplexée sans pool : la chaîne d'un <c>Operator</c> qui l'a activé
  /// ferait lever le constructeur, donc traverser une exception du pilote là où l'écran attend une
  /// fin nommée.
  /// </remarks>
  internal static bool TryPrepare(string connectionString, out string prepared)
  {
    prepared = string.Empty;

    NpgsqlConnectionStringBuilder builder;

    try
    {
      builder = new NpgsqlConnectionStringBuilder(connectionString);
    }
    catch (Exception failure) when (failure is ArgumentException or FormatException)
    {
      // Une chaîne que le pilote ne sait même pas lire. Rien de ce qu'elle contient ne ressort :
      // c'est le message du pilote qui porterait l'hôte et l'utilisateur.
      return false;
    }

    if (string.IsNullOrWhiteSpace(builder.Host))
    {
      return false;
    }

    builder.Pooling = false;
    builder.Multiplexing = false;
    prepared = builder.ConnectionString;

    return true;
  }
}
