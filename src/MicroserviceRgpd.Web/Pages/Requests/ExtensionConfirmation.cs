using System.Globalization;
using MicroserviceRgpd.UseCases.Requests.ReadDataSubjectRequestExtension;

namespace MicroserviceRgpd.Web.Pages.Requests;

/// <summary>
/// La <b>modale de prolongation d'une demande</b>, côté serveur : ses mots, écrits ici une seule fois,
/// et le récapitulatif qu'elle montre, rendu en JSON <b>déjà en libellés</b> — les deux dates
/// comprises (ADR-0029).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Le script ne compose aucun mot, et ne calcule aucune date</b> : il verse les valeurs dans les
/// cibles que la page a rendues. <c>Date.setMonth</c> ne fait pas le même repli de fin de mois que
/// <see cref="DateOnly.AddMonths"/> — la modale annoncerait une date, le serveur en écrirait une
/// autre.
/// </para>
/// <para>
/// ⚠️ <b>L'avertissement est daté de la date limite <i>initiale</i></b> — celle qui vaut encore au
/// moment où l'<c>Operator</c> décide : c'est avant elle que la personne concernée doit être
/// informée, et non avant la nouvelle.
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
/// <param name="CurrentDeadline">La date limite de réponse en vigueur, en <c>jj/mm/aaaa</c>.</param>
/// <param name="ResultingDeadline">La date limite de réponse qui résultera de la prolongation, en <c>jj/mm/aaaa</c>.</param>
/// <param name="Warning">L'avertissement d'information de la personne concernée, daté de la date limite initiale.</param>
public sealed record ExtensionConfirmation(
  string Right,
  string FirstName,
  string LastName,
  string Email,
  string CurrentDeadline,
  string ResultingDeadline,
  string Warning)
{
  /// <summary>Le titre de la modale : le nom même du geste, celui du bouton de la ligne.</summary>
  public const string Title = RequestRow.ExtensionOffered;

  /// <summary>
  /// Le libellé du droit dans le récapitulatif. ⚠️ <b>Le même mot que dans la confirmation
  /// d'exécution</b> : même fait de la même demande, sur le même écran.
  /// </summary>
  public const string RightLabel = ExecutionConfirmation.RightLabel;

  /// <summary>Le libellé du prénom dans le récapitulatif.</summary>
  public const string FirstNameLabel = ExecutionConfirmation.FirstNameLabel;

  /// <summary>Le libellé du nom dans le récapitulatif.</summary>
  public const string LastNameLabel = ExecutionConfirmation.LastNameLabel;

  /// <summary>Le libellé de l'email dans le récapitulatif.</summary>
  public const string EmailLabel = ExecutionConfirmation.EmailLabel;

  /// <summary>Le libellé de la date limite de réponse en vigueur.</summary>
  public const string CurrentDeadlineLabel = "Date limite de réponse actuelle";

  /// <summary>Le libellé de la date limite de réponse qui résultera de la prolongation.</summary>
  public const string ResultingDeadlineLabel = "Nouvelle date limite de réponse";

  /// <summary>Le libellé du choix fermé des deux motifs.</summary>
  public const string GroundLabel = "Motif de prolongation";

  /// <summary>L'invite du choix fermé, avant que l'<c>Operator</c> n'ait choisi.</summary>
  public const string GroundPrompt = "Sélectionner un motif";

  /// <summary>Le libellé du texte libre qui dit le fait concret.</summary>
  public const string JustificationLabel = "Justification";

  /// <summary>
  /// L'avertissement, sous le récapitulatif : <c>{0}</c> est la <b>date limite initiale</b>, celle qui
  /// vaut avant la prolongation.
  /// </summary>
  public const string WarningFormat =
    "Vous devez informer la personne concernée de cette prolongation et de ses motifs avant le {0}.";

  /// <summary>Le bouton qui renonce, sans rien écrire.</summary>
  public const string Cancel = "Annuler";

  /// <summary>Le bouton qui prolonge.</summary>
  public const string Confirm = "Prolonger";

  /// <summary>Ce que dit le toast quand la demande a été prolongée.</summary>
  public const string Extended = "Demande prolongée";

  /// <summary>
  /// Ce que dit le bandeau quand la réponse du service ne se lit pas — coupure réseau, erreur du
  /// serveur : la prolongation a pu aboutir, et rien à l'écran ne le sait.
  /// </summary>
  public const string Unanswered =
    "La réponse du service n'a pas pu être lue. Rechargez la page pour savoir si la demande a été prolongée.";

  /// <summary>Ce qu'affiche une valeur absente : une absence, pas une cible restée vide.</summary>
  private const string Absent = "—";

  /// <summary>Le récapitulatif en libellés, les deux dates déjà écrites par le serveur.</summary>
  /// <exception cref="ArgumentNullException"><paramref name="summary"/> est absent.</exception>
  public static ExtensionConfirmation Of(DataSubjectRequestExtensionSummary summary)
  {
    ArgumentNullException.ThrowIfNull(summary);

    var currentDeadline = RequestRow.Day(summary.ResponseDeadline);

    return new ExtensionConfirmation(
      string.Format(CultureInfo.InvariantCulture, "{0} (art. {1})", RequestRow.Capitalized(summary.Right.FrenchLabel), summary.Right.Article),
      summary.FirstName?.Value ?? Absent,
      summary.LastName?.Value ?? Absent,
      summary.Email?.Value ?? Absent,
      currentDeadline,
      RequestRow.Day(summary.ExtendedResponseDeadline),
      string.Format(CultureInfo.InvariantCulture, WarningFormat, currentDeadline));
  }
}
