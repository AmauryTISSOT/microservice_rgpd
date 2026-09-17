using System.Globalization;
using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.UseCases.Requests.ReadDataSubjectRequestExecution;

namespace MicroserviceRgpd.Web.Pages.Requests;

/// <summary>
/// La <b>modale de confirmation d'une exécution</b>, côté serveur : ses mots, écrits ici une seule fois,
/// et le récapitulatif qu'elle montre, rendu en JSON <b>déjà en libellés</b> (ADR-0026).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Le script ne compose aucun mot</b> : il verse les valeurs dans les cibles que la page a rendues,
/// et recopie le motif de blocage tel quel. « — » pour une valeur absente est écrit ici.
/// </para>
/// <para>
/// Les tests navigateur lisent leurs attentes sur ces constantes : une phrase retouchée ici l'est
/// partout.
/// </para>
/// </remarks>
/// <param name="Right">Le libellé du droit, avec son article — « Droit d'accès (art. 15) ».</param>
/// <param name="FirstName">Le prénom, ou « — ».</param>
/// <param name="LastName">Le nom, ou « — ».</param>
/// <param name="Email">L'email, ou « — ».</param>
/// <param name="Exercise">Par où la demande partirait : l'adresse appelée, query string comprise, l'exchange et la routing key, ou « — ».</param>
/// <param name="Block">Le motif de blocage, ou <c>null</c> quand la demande s'exécute.</param>
public sealed record ExecutionConfirmation(
  string Right,
  string FirstName,
  string LastName,
  string Email,
  string Exercise,
  string? Block)
{
  /// <summary>Le titre de la modale : le nom même du geste, celui du bouton de la ligne.</summary>
  public const string Title = RequestRow.ExecutionOffered;

  /// <summary>L'avertissement, sous le récapitulatif.</summary>
  public const string Warning =
    "La demande sera transmise au système hôte puis passera à Terminée. Cette action est irréversible.";

  /// <summary>Le libellé du droit dans le récapitulatif.</summary>
  public const string RightLabel = "Droit invoqué";

  /// <summary>Le libellé du prénom dans le récapitulatif.</summary>
  public const string FirstNameLabel = "Prénom";

  /// <summary>Le libellé du nom dans le récapitulatif.</summary>
  public const string LastNameLabel = "Nom";

  /// <summary>Le libellé de l'email dans le récapitulatif.</summary>
  public const string EmailLabel = "Email";

  /// <summary>Le libellé du canal d'exercice dans le récapitulatif.</summary>
  public const string ExerciseLabel = "Exercice";

  /// <summary>Le bouton qui renonce, sans aucun appel.</summary>
  public const string Cancel = "Annuler";

  /// <summary>Le bouton qui exécute.</summary>
  public const string Confirm = "Exécuter";

  /// <summary>Ce que dit le toast quand le système hôte a appliqué le droit.</summary>
  public const string Executed = "Demande exécutée";

  /// <summary>
  /// Ce que dit le bandeau quand la réponse du service ne se lit pas — coupure réseau, erreur du
  /// serveur : l'exécution a pu aboutir, et rien à l'écran ne le sait.
  /// </summary>
  public const string Unanswered =
    "La réponse du service n'a pas pu être lue. Rechargez la page pour savoir si la demande a été exécutée.";

  /// <summary>Ce qu'affiche une valeur absente : une absence, pas une cible restée vide.</summary>
  private const string Absent = "—";

  /// <summary>Un routage RabbitMQ en toutes lettres : ses deux valeurs, nommées.</summary>
  private const string RoutingFormat = "exchange {0}, routing key {1}";

  /// <summary>Le récapitulatif en libellés.</summary>
  /// <exception cref="ArgumentNullException"><paramref name="summary"/> est absent.</exception>
  public static ExecutionConfirmation Of(DataSubjectRequestExecutionSummary summary)
  {
    ArgumentNullException.ThrowIfNull(summary);

    return new ExecutionConfirmation(
      string.Format(CultureInfo.InvariantCulture, "{0} (art. {1})", RequestRow.Capitalized(summary.Right.FrenchLabel), summary.Right.Article),
      summary.FirstName?.Value ?? Absent,
      summary.LastName?.Value ?? Absent,
      summary.Email?.Value ?? Absent,
      ExerciseOf(summary.Exercise),
      summary.Block?.FrenchLabelFor(summary.Right));
  }

  /// <summary>
  /// Par où la demande partirait, en toutes lettres : l'adresse telle quelle, le routage sous ses deux
  /// valeurs, ou l'absence d'un droit « non configuré » (ADR-0027).
  /// </summary>
  private static string ExerciseOf(ExerciseChannel channel) => channel switch
  {
    ExerciseChannel.HttpEndpoint http => http.Address.Value,
    ExerciseChannel.RabbitMq rabbit => string.Format(
      CultureInfo.InvariantCulture,
      RoutingFormat,
      rabbit.Routing.Exchange.Value,
      rabbit.Routing.RoutingKey.Value),

    // « Non configuré », le troisième et dernier cas : la hiérarchie est fermée.
    _ => Absent,
  };
}
