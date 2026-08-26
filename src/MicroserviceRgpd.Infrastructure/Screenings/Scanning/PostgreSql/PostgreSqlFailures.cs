using MicroserviceRgpd.Core.Screenings;
using Npgsql;

namespace MicroserviceRgpd.Infrastructure.Screenings.Scanning.PostgreSql;

/// <summary>
/// Ce que devient une panne du pilote quand elle arrive à la frontière : une famille d'échec de
/// scan, ou une raison d'absence d'aperçu. <b>Jamais elle-même.</b>
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Aucune <c>NpgsqlException</c> ni <c>PostgresException</c> ne traverse le port.</b> Le
/// message d'une <c>PostgresException</c> porte le nom de la table, parfois la valeur qui a fait
/// échouer la contrainte, et <c>NpgsqlException</c> porte l'hôte et le port. Les recopier dans ce
/// qui est rendu, c'est reconstituer la chaîne de connexion et la donnée du client par morceaux,
/// dans un écran, puis dans un journal, puis dans un ticket. Une famille est un mot du service ;
/// elle ne sait pas porter de prose venue d'ailleurs, et c'est le type qui tient la règle.
/// </para>
/// <para>
/// ⚠️ <b>Le tri se fait sur le <c>SQLSTATE</c>, jamais sur le texte du message.</b> Le message est
/// localisé — <c>lc_messages</c> du serveur —, et un tri sur ses mots ne survivrait pas à une base
/// configurée en français. Le <c>SQLSTATE</c>, lui, est normalisé et ne bouge pas.
/// </para>
/// </remarks>
internal static class PostgreSqlFailures
{
  /// <summary>Le compte n'a pas été authentifié : mot de passe refusé, méthode refusée.</summary>
  private const string InvalidAuthorisation = "28000";

  /// <summary>Le mot de passe fourni est faux.</summary>
  private const string InvalidPassword = "28P01";

  /// <summary>La base nommée dans la chaîne n'existe pas.</summary>
  private const string InvalidCatalogueName = "3D000";

  /// <summary>Le compte est connecté, et n'a pas le droit de lire cet objet.</summary>
  private const string InsufficientPrivilege = "42501";

  /// <summary>Le serveur a coupé la requête — c'est ce que notre propre annulation lui demande.</summary>
  private const string QueryCancelled = "57014";

  /// <summary>
  /// De quel côté vient ce qui a fait tomber le scan.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>« Droits refusés » se range du côté de ce qui a été fourni au service, et c'est le geste
  /// qui en décide.</b> Un compte sans droit de lecture envoie l'<c>Operator</c> demander un accès —
  /// exactement comme un mot de passe faux l'envoie corriger sa chaîne. Le ranger sous
  /// <see cref="ScanFailureFamily.Database"/> lui aurait dit « la base a refusé », c'est-à-dire
  /// « il n'y a rien à faire », pour la seule panne des trois qu'il peut réparer lui-même.
  /// </para>
  /// <para>
  /// ⚠️ <b>Tout ce qui n'est pas une réponse du serveur est du réseau.</b> Une
  /// <c>PostgresException</c> <b>est</b> une réponse : le serveur a parlé. Une
  /// <c>NpgsqlException</c> qui n'en est pas une, c'est la socket, le délai, la connexion tombée —
  /// et la famille <see cref="ScanFailureFamily.Network"/>, qui n'avait aucun emploi sous SQLite,
  /// trouve ici le sien.
  /// </para>
  /// </remarks>
  internal static ScanFailureFamily FamilyOf(Exception failure)
  {
    return failure switch
    {
      PostgresException postgres => postgres.SqlState switch
      {
        InvalidAuthorisation or InvalidPassword or InvalidCatalogueName or InsufficientPrivilege =>
          ScanFailureFamily.Supplied,
        _ => ScanFailureFamily.Database,
      },
      NpgsqlException or TimeoutException => ScanFailureFamily.Network,
      _ => ScanFailureFamily.Supplied,
    };
  }

  /// <summary>
  /// Pourquoi une colonne n'a aucun aperçu à montrer, quand c'est sa table qui a raté.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Les droits refusés vivent à part parce que c'est la seule des quatre familles que
  /// l'<c>Operator</c> puisse corriger.</b> Toutes les autres pannes — délai dépassé, connexion
  /// tombée, table disparue pendant le scan — ne lui offrent rien de plus les unes que les autres,
  /// et se rangent ensemble.
  /// </remarks>
  internal static PreviewAbsenceReason ReasonFor(Exception failure)
  {
    return failure is PostgresException { SqlState: InsufficientPrivilege }
      ? PreviewAbsenceReason.AccessDenied
      : PreviewAbsenceReason.ReadFailed;
  }

  /// <summary>
  /// Cet échec est-il notre propre annulation, revenue par le serveur ?
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Annuler une requête PostgreSQL, c'est demander au serveur de la couper</b> — Npgsql
  /// ouvre pour cela une seconde connexion et envoie une requête d'annulation. Le serveur répond
  /// alors <c>57014</c> sur la première, et le pilote la relève parfois telle quelle plutôt qu'en
  /// <see cref="OperationCanceledException"/>. La laisser passer ferait d'un abandon de
  /// l'<c>Operator</c> un échec de scan — un écran rouge pour un geste volontaire.
  /// </remarks>
  internal static bool IsCancellation(Exception failure)
  {
    return failure is PostgresException { SqlState: QueryCancelled };
  }
}
