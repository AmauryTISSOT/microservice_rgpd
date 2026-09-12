using System.Globalization;
using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.UseCases.Requests.ReadDataSubjectRequests;

namespace MicroserviceRgpd.Web.Pages.Requests;

/// <summary>
/// La <b>ligne d'une demande</b> dans le tableau, chaque cellule sous le libellé que le serveur lui
/// donne. La vue partielle <c>_RequestRow</c> la rend, pour le tableau au chargement comme pour la
/// demande qu'on vient de créer.
/// </summary>
/// <remarks>
/// ⚠️ <b>Tous les libellés sont rendus par le serveur</b> — tirets, dates, « Oui/Non », droit,
/// auteur, statut, jusqu'à la phrase qui confirme la suppression. Le script de l'écran anime les
/// lignes ; il n'en écrit aucun mot. La ligne porte aussi l'identifiant de sa demande, que la
/// suppression envoie, le statut sous son <b>nom canonique</b>, que le badge porte pour que la
/// feuille de style le colore, et la date de réception au <b>format ISO</b>, par laquelle le script
/// place la ligne d'une demande qu'on vient de créer.
/// </remarks>
public sealed record RequestRow(
  string Id,
  string DeletionConfirmation,
  string Email,
  string LastName,
  string FirstName,
  string ReceivedOnIso,
  string ReceivedOn,
  string ResponseDeadline,
  string IdentityVerified,
  string Right,
  string CreatedAt,
  string CreatedBy,
  string StatusLabel,
  string StatusName,
  RequestRow.SearchableText Searchable)
{
  /// <summary>
  /// Ce que la recherche parcourt sur la ligne : l'email, le nom et le prénom <b>tels
  /// qu'enregistrés</b>, vides s'ils sont absents. Le script les normalise, comme il normalise la saisie.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Ce ne sont pas les libellés des cellules</b> : le « — » d'une valeur absente n'est pas un
  /// texte que la personne a donné, et une recherche ne doit pas le trouver.
  /// </remarks>
  public sealed record SearchableText(string Email, string LastName, string FirstName);

  /// <summary>Ce qu'affiche une cellule dont la valeur est absente : une absence, pas une cellule mal rendue.</summary>
  private const string Absent = "—";

  private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

  /// <summary>La ligne d'une demande enregistrée.</summary>
  /// <exception cref="ArgumentNullException"><paramref name="request"/> est absente.</exception>
  public static RequestRow Of(RecordedDataSubjectRequest request)
  {
    ArgumentNullException.ThrowIfNull(request);

    return new RequestRow(
      request.Id.Value.ToString(),
      DeletionConfirmationOf(request),
      request.Email?.Value ?? Absent,
      request.LastName?.Value ?? Absent,
      request.FirstName?.Value ?? Absent,
      request.ReceivedOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
      Day(request.ReceivedOn),
      Day(request.ResponseDeadline),
      request.IdentityVerified ? "Oui" : "Non",
      Capitalized(request.Right.FrenchLabel),
      ParisCalendar.InParis(request.CreatedAt).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
      request.CreatedBy == DataSubjectRequest.OperatorAuthor ? "Opérateur" : request.CreatedBy,
      request.Status.FrenchLabel,
      request.Status.Name,
      new SearchableText(
        request.Email?.Value ?? string.Empty,
        request.LastName?.Value ?? string.Empty,
        request.FirstName?.Value ?? string.Empty));
  }

  /// <summary>Un jour en <c>jj/mm/aaaa</c>.</summary>
  private static string Day(DateOnly day) => day.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

  /// <summary>
  /// « La demande de {Prénom} {Nom} ({email}) sera définitivement supprimée. Cette action est
  /// irréversible. » — le prénom et le nom omis s'ils manquent, l'email entre parenthèses quand un
  /// nom l'accompagne, <b>sans parenthèses quand il est seul</b>.
  /// </summary>
  /// <remarks>
  /// Elle nomme la personne comme l'<c>Operator</c> la lit sur la ligne : c'est elle qu'il regarde
  /// avant une suppression sans retour. « Supprimée », jamais « effacée » : l'effacement est un
  /// droit (art. 17), que la demande peut invoquer.
  /// </remarks>
  private static string DeletionConfirmationOf(RecordedDataSubjectRequest request)
  {
    var name = string.Join(' ', new[] { request.FirstName?.Value, request.LastName?.Value }.OfType<string>());
    var email = request.Email?.Value;

    var whose = (name, email) switch
    {
      ("", _) => email,
      (_, null) => name,
      _ => $"{name} ({email})",
    };

    return $"La demande de {whose} sera définitivement supprimée. Cette action est irréversible.";
  }

  /// <summary>
  /// Le libellé du droit en tête de cellule : « droit d'accès » devient « Droit d'accès ». Le
  /// libellé du noyau partagé se lit dans une phrase, et sans son article du RGPD.
  /// </summary>
  private static string Capitalized(string label) =>
    string.Concat(char.ToUpper(label[0], French).ToString(), label[1..]);
}
