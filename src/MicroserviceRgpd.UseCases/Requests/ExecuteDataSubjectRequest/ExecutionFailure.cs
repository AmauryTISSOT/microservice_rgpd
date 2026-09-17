using System.Globalization;

namespace MicroserviceRgpd.UseCases.Requests.ExecuteDataSubjectRequest;

/// <summary>
/// <b>Ce que l'<c>Operator</c> lit d'une exécution qui n'a pas abouti</b>, sur l'un ou l'autre canal —
/// écrit ici une seule fois, et lu tel quel par l'écran, qui le recopie dans le bandeau de la
/// confirmation.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Un délai dépassé et un injoignable se disent différemment selon le canal</b> (ADR-0028) :
/// le même <see cref="MicroserviceRgpd.Core.Requests.ExecutionOutcome"/> ne décide donc pas seul de
/// la phrase. « Le système hôte » nomme une application qui répond ; sur un bus, c'est le broker que
/// le service atteint ou non, et le consommateur, lui, reste invisible.
/// </para>
/// <para>
/// Les tests navigateur lisent leurs attentes sur ces textes ; les tests du handler et de la surface
/// HTTP gardent les phrases en toutes lettres, pour qu'une retouche de la formulation soit voulue.
/// </para>
/// </remarks>
public static class ExecutionFailure
{
  /// <summary>Le droit est remis, mais la demande n'a pas pu passer à Terminée.</summary>
  public const string SucceededButNotRecorded =
    "Le système hôte a appliqué le droit, mais la demande n'a pas pu passer à Terminée.";

  /// <summary>Le système hôte n'a pas pu être joint.</summary>
  public const string Unreachable = "Le système hôte est injoignable. La demande reste En cours.";

  /// <summary>Le système hôte a répondu <paramref name="statusCode"/>, qui n'est pas un 2xx.</summary>
  public static string NonSuccessResponse(int statusCode) =>
    string.Format(CultureInfo.InvariantCulture, "Le système hôte a répondu {0}. La demande reste En cours.", statusCode);

  /// <summary>Le système hôte n'a pas répondu dans les <paramref name="seconds"/> secondes du délai.</summary>
  public static string TimedOut(int seconds) =>
    string.Format(CultureInfo.InvariantCulture, "Le système hôte n'a pas répondu dans les {0} secondes. La demande reste En cours.", seconds);

  /// <summary>
  /// Le message a été publié, et aucune file ne l'a reçu. ⚠️ <b>Il est bien parti</b> : ce n'est pas
  /// un refus du broker, c'est une topologie qui ne mène nulle part.
  /// </summary>
  public const string Unroutable =
    "Le message a été publié, mais aucune file ne l'a reçu. La demande reste En cours.";

  /// <summary>Le broker a refusé la publication — un <c>nack</c>.</summary>
  public const string Rejected = "Le broker a refusé la publication. La demande reste En cours.";

  /// <summary>Le broker n'a pas pu être joint.</summary>
  public const string BrokerUnreachable = "Le broker est injoignable. La demande reste En cours.";

  /// <summary>
  /// Le broker n'a pas confirmé dans les <paramref name="seconds"/> secondes du délai. ⚠️ <b>La
  /// phrase dit le doute</b> : à la différence d'un appel HTTP sans réponse, un message peut très
  /// bien être parti et la confirmation s'être perdue. L'<c>Operator</c> le lit avant de republier.
  /// </summary>
  public static string BrokerTimedOut(int seconds) =>
    string.Format(
      CultureInfo.InvariantCulture,
      "Le broker n'a pas confirmé la publication dans les {0} secondes. Le message a peut-être été publié. La demande reste En cours.",
      seconds);
}
