using System.Net.Sockets;
using MicroserviceRgpd.Core.Screenings;
using MySqlConnector;

namespace MicroserviceRgpd.Infrastructure.Screenings.Scanning.MySql;

/// <summary>
/// Ce qu'une panne du pilote devient en franchissant le port : une <see cref="ScanFailureFamily"/>
/// quand c'est le scan qui tombe, une <see cref="PreviewAbsenceReason"/> quand c'est une colonne.
/// Deux mots du service, et pas un caractère venu du pilote.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>La traduction se fait sur le numéro, jamais sur le message.</b> Le message d'une
/// <c>MySqlException</c> écrit l'hôte, le compte, parfois la requête — c'est-à-dire la chaîne de
/// connexion reconstituée par morceaux. Le numéro, lui, est un entier.
/// </para>
/// <para>
/// ⚠️ <b>Les numéros sont recopiés en constantes plutôt que lus sur <c>MySqlErrorCode</c>.</b> Le
/// protocole les a figés — <c>1045</c> est « access denied » depuis toujours et le restera —, alors
/// que les <b>noms</b> de l'énumération appartiennent au pilote et peuvent être renommés d'une
/// version à l'autre. Ce qui est stable est ce sur quoi on écrit.
/// </para>
/// <para>
/// ⚠️ <b>Ce qui n'est pas reconnu tombe dans <see cref="ScanFailureFamily.Database"/>.</b> C'est le
/// défaut prudent : la base a répondu, et ce qu'elle a répondu est un échec. Le rangement inverse —
/// « inconnu donc c'est ce qui a été fourni » — enverrait l'<c>Operator</c> corriger une chaîne qui
/// n'a rien.
/// </para>
/// </remarks>
internal static class MySqlFailures
{
  /// <summary>Le serveur n'a pas répondu du tout : rien n'a été joint.</summary>
  internal const int UnableToConnectToHost = 1042;

  /// <summary>La poignée de main a échoué avant l'authentification.</summary>
  internal const int HandshakeError = 1043;

  /// <summary>Le compte n'a aucun droit sur cette base.</summary>
  internal const int DatabaseAccessDenied = 1044;

  /// <summary>Le compte ou le mot de passe est refusé.</summary>
  internal const int AccessDenied = 1045;

  /// <summary>La base nommée n'existe pas.</summary>
  internal const int UnknownDatabase = 1049;

  /// <summary>Le compte n'a pas le droit de lire cette table.</summary>
  internal const int TableAccessDenied = 1142;

  /// <summary>Le compte n'a pas le droit de lire cette colonne.</summary>
  internal const int ColumnAccessDenied = 1143;

  /// <summary>
  /// L'annulation a-t-elle produit cette panne ? ⚠️ Une requête coupée par le jeton revient en
  /// <c>MySqlException</c>, pas en <see cref="OperationCanceledException"/> : la rendre telle quelle
  /// ferait de l'<c>Operator</c> qui quitte l'écran un scan tombé, avec un écran d'échec à la clé.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>C'est le <b>jeton</b> qui répond, et jamais le numéro d'erreur.</b> Le serveur rend
  /// <c>1317</c> — « requête interrompue » — pour l'annulation du service, mais <b>aussi</b> pour un
  /// <c>KILL QUERY</c> lancé par le DBA du client et pour un <c>max_execution_time</c> dépassé. Lire
  /// <c>1317</c> comme une annulation ferait lever une
  /// <see cref="OperationCanceledException"/> portant un jeton qui n'a jamais été annulé : une
  /// exception traverserait le port là où l'écran attend une fin nommée, et l'<c>Operator</c>
  /// verrait « vous avez abandonné » d'un scan que la base a coupé sous lui.
  /// </para>
  /// <para>
  /// Un <c>1317</c> sans annulation retombe donc là où il doit : <see cref="ScanFailureFamily"/>
  /// <c>.Database</c> pour le scan, <see cref="PreviewAbsenceReason"/><c>.ReadFailed</c> pour une
  /// colonne — la base a répondu, et ce qu'elle a répondu est un échec.
  /// </para>
  /// </remarks>
  internal static bool IsInterrupt(CancellationToken cancellationToken)
  {
    return cancellationToken.IsCancellationRequested;
  }

  /// <summary>De quel côté vient ce qui a fait tomber le scan.</summary>
  internal static ScanFailureFamily FamilyOf(MySqlException failure)
  {
    ArgumentNullException.ThrowIfNull(failure);

    return FamilyOf((int)failure.ErrorCode, failure.InnerException);
  }

  /// <inheritdoc cref="FamilyOf(MySqlException)" />
  internal static ScanFailureFamily FamilyOf(int errorCode, Exception? inner)
  {
    // Le pilote n'a rien reçu du serveur : le socket, le nom d'hôte ou le délai ont eu le dernier
    // mot. Aucun de ces trois n'offre à l'Operator un geste que les autres ne lui offrent pas.
    if (inner is SocketException or IOException or TimeoutException)
    {
      return ScanFailureFamily.Network;
    }

    return errorCode switch
    {
      UnableToConnectToHost or HandshakeError => ScanFailureFamily.Network,
      AccessDenied or DatabaseAccessDenied or UnknownDatabase => ScanFailureFamily.Supplied,
      _ => ScanFailureFamily.Database,
    };
  }

  /// <summary>
  /// Pourquoi une colonne n'a rien à montrer. ⚠️ <b>« Droits refusés » vit à part</b> : c'est la
  /// seule des quatre raisons que l'<c>Operator</c> puisse corriger — il ira demander un accès.
  /// </summary>
  internal static PreviewAbsenceReason ReasonFor(MySqlException failure)
  {
    ArgumentNullException.ThrowIfNull(failure);

    return ReasonFor((int)failure.ErrorCode);
  }

  /// <inheritdoc cref="ReasonFor(MySqlException)" />
  internal static PreviewAbsenceReason ReasonFor(int errorCode)
  {
    return errorCode switch
    {
      TableAccessDenied or ColumnAccessDenied or AccessDenied or DatabaseAccessDenied =>
        PreviewAbsenceReason.AccessDenied,
      _ => PreviewAbsenceReason.ReadFailed,
    };
  }
}
