using MicroserviceRgpd.Core.Screenings;
using MySqlConnector;

namespace MicroserviceRgpd.Infrastructure.Screenings.Scanning.MySql;

/// <summary>
/// Ce que le scanner MariaDB/MySQL fait d'une chaîne de connexion avant de s'en servir : il en
/// retire le nom de la base, il coupe le pool, il ferme la lecture de fichiers locaux — et il rend
/// la chaîne <b>effective</b>, celle qui part réellement au serveur.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Le nom de la base sort de la chaîne, et c'est ce qui rend les deux fins à zéro objet
/// distinguables.</b> Connecté <i>sur</i> une base, le serveur refuse l'ouverture elle-même quand
/// elle n'existe pas (<c>1049</c>) ou quand le compte n'y a aucun droit (<c>1044</c>) : le scan
/// tomberait en échec de connexion, et « base absente du catalogue » — la fin qui envoie
/// l'<c>Operator</c> demander un accès — ne serait jamais rendue. Connecté <b>sans</b> base, le
/// service ouvre, interroge <c>information_schema.SCHEMATA</c> avec le nom en <b>paramètre</b>, et
/// sait alors dire lequel des deux zéros il a rencontré. Les requêtes de prélèvement, elles,
/// qualifient toujours leur table par son schéma : rien ne dépend d'une base courante.
/// </para>
/// <para>
/// ⚠️ <b><c>AllowLoadLocalInfile</c> est remis à <c>false</c>, même si l'<c>Operator</c> l'a écrit
/// à <c>true</c>.</b> À <c>true</c>, c'est le <b>serveur</b> qui décide quel fichier le client lui
/// envoie : un serveur hostile — ou seulement compromis — répond à un <c>SELECT</c> anodin par une
/// demande de <c>LOAD DATA LOCAL INFILE</c>, et le microservice lui lit un fichier de son
/// <b>propre</b> disque. Le service se connecte à des bases dont il ne sait rien ; ce n'est pas une
/// option qu'il peut laisser à la main de la chaîne saisie.
/// </para>
/// <para>
/// ⚠️ <b><c>Pooling</c> est coupé.</b> Une connexion rendue au pool reste <b>authentifiée</b> et
/// ouverte sur la base du client, dans un casier statique du pilote, bien après que
/// l'<c>Operator</c> a quitté l'écran. Ici, la fin du scan est la fin de la connexion.
/// </para>
/// <para>
/// ⚠️ <b>Ce que le pool coupé ne suffit pas à empêcher, écrit ici plutôt que découvert un jour de
/// revue.</b> <c>MySqlConnector</c> range la chaîne de connexion <b>telle quelle</b> — mot de passe
/// compris — comme <b>clé</b> d'un dictionnaire statique, et il le fait même à <c>Pooling=false</c>.
/// <c>ClearAllPools</c> ne l'en retire pas, <c>MySqlDataSource</c> passe par le même registre, et
/// aucune API publique du pilote n'y donne prise : la chaîne vit dans un statique jusqu'à la fin du
/// processus. Ce qui en découle est borné — aucune session ouverte, aucune valeur lue, rien qui
/// descende en base —, mais ce n'est pas rien. <c>MySqlScanFailureTests</c> l'épingle, pour que le
/// jour où le pilote cessera de le faire se voie.
/// </para>
/// <para>
/// ⚠️ <b>Ce qui ne se coupe pas est écrit ici plutôt que découvert un jour de revue.</b>
/// <c>MySqlConnector</c> active <c>CLIENT_MULTI_STATEMENTS</c> <b>inconditionnellement</b> et
/// n'expose aucune option pour l'éteindre. La conséquence est portée par
/// <see cref="MySqlScanQueries.Quote"/>, qui est le seul endroit où un texte du client entre dans
/// une requête.
/// </para>
/// </remarks>
internal sealed record MySqlConnectionSettings(string ConnectionString, string Database)
{
  /// <summary>
  /// Lit la chaîne fournie et rend celle dont le scanner se servira, ou dit non. ⚠️ <b>Elle ne
  /// rend jamais de message.</b> Ce que le pilote dirait d'une chaîne illisible, c'est l'hôte et le
  /// compte qu'elle porte.
  /// </summary>
  internal static bool TryPrepare(string? connectionString, out MySqlConnectionSettings settings)
  {
    settings = new MySqlConnectionSettings(string.Empty, string.Empty);

    MySqlConnectionStringBuilder builder;

    try
    {
      builder = new MySqlConnectionStringBuilder(connectionString ?? string.Empty);
    }
    catch (ArgumentException)
    {
      // Une chaîne que le pilote ne sait même pas lire — une option inconnue, un port qui n'est pas
      // un nombre. Rien de ce qu'elle contient ne ressort.
      return false;
    }
    catch (FormatException)
    {
      return false;
    }

    if (string.IsNullOrWhiteSpace(builder.Server))
    {
      return false;
    }

    var database = (builder.Database ?? string.Empty).Trim();

    // ⚠️ Un nom que le domaine refuserait ferait rendre un pivot que l'ingestion rejetterait en
    // MissingHeader — un scan qui a réussi, rendu par un refus parlant d'en-tête. Mieux vaut le dire
    // ici, où c'est encore « ce qui a été fourni au service ».
    if (string.IsNullOrWhiteSpace(database) || database.Any(char.IsControl))
    {
      return false;
    }

    // ⚠️ Un nom trop long est refusé, jamais rogné. Le nom part deux fois : dans l'en-tête du pivot
    // et — en paramètre — dans la lecture d'existence. Le rogner pour l'en-tête ferait diverger les
    // deux, et le service chercherait au catalogue une base que personne n'a nommée. MySQL n'accepte
    // de toute façon pas plus de 64 caractères : au-delà de la borne du domaine, ce qui a été
    // fourni ne désigne aucune base.
    if (database.Length > Screening.MaxDatabaseNameLength)
    {
      return false;
    }

    builder.Database = string.Empty;
    builder.Pooling = false;
    builder.AllowLoadLocalInfile = false;

    settings = new MySqlConnectionSettings(builder.ConnectionString, database);

    return true;
  }
}
