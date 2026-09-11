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
/// auteur, statut. Le script de l'écran anime les lignes ; il n'en écrit aucun mot. Le statut vient
/// aussi sous son <b>nom canonique</b>, que le badge porte pour que la feuille de style le colore.
/// </remarks>
public sealed record RequestRow(
  string Email,
  string LastName,
  string FirstName,
  string ReceivedOn,
  string ResponseDeadline,
  string IdentityVerified,
  string Right,
  string CreatedAt,
  string CreatedBy,
  string StatusLabel,
  string StatusName)
{
  /// <summary>Ce qu'affiche une cellule dont la valeur est absente : une absence, pas une cellule mal rendue.</summary>
  private const string Absent = "—";

  private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

  /// <summary>La ligne d'une demande enregistrée.</summary>
  /// <exception cref="ArgumentNullException"><paramref name="request"/> est absente.</exception>
  public static RequestRow Of(RecordedDataSubjectRequest request)
  {
    ArgumentNullException.ThrowIfNull(request);

    return new RequestRow(
      request.Email?.Value ?? Absent,
      request.LastName?.Value ?? Absent,
      request.FirstName?.Value ?? Absent,
      Day(request.ReceivedOn),
      Day(request.ResponseDeadline),
      request.IdentityVerified ? "Oui" : "Non",
      Capitalized(request.Right.FrenchLabel),
      ParisCalendar.InParis(request.CreatedAt).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
      request.CreatedBy == DataSubjectRequest.OperatorAuthor ? "Opérateur" : request.CreatedBy,
      request.Status.FrenchLabel,
      request.Status.Name);
  }

  /// <summary>Un jour en <c>jj/mm/aaaa</c>.</summary>
  private static string Day(DateOnly day) => day.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

  /// <summary>
  /// Le libellé du droit en tête de cellule : « droit d'accès » devient « Droit d'accès ». Le
  /// libellé du noyau partagé se lit dans une phrase, et sans son article du RGPD.
  /// </summary>
  private static string Capitalized(string label) =>
    string.Concat(char.ToUpper(label[0], French).ToString(), label[1..]);
}
