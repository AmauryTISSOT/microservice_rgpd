using System.Globalization;

namespace MicroserviceRgpd.UseCases.Requests.ExecuteDataSubjectRequest;

/// <summary>
/// <b>Ce que l'<c>Operator</c> lit d'une exécution qui n'a pas abouti</b> — écrit ici une seule fois, et
/// lu tel quel par l'écran, qui le recopie dans le bandeau de la confirmation.
/// </summary>
/// <remarks>
/// Les tests navigateur lisent leurs attentes sur ces textes ; les tests du handler et de la surface
/// HTTP gardent les phrases en toutes lettres, pour qu'une retouche de la formulation soit voulue.
/// </remarks>
public static class ExecutionFailure
{
  /// <summary>Le droit est appliqué, mais la demande n'a pas pu passer à Terminée.</summary>
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
}
