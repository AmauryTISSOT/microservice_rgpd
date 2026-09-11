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
/// auteur. Le script de l'écran anime les lignes ; il n'en écrit aucun mot.
/// </remarks>
public sealed record RequestRow(
  string Email,
  string LastName,
  string FirstName,
  string ReceivedOn,
  string IdentityVerified,
  string Right,
  string CreatedAt,
  string CreatedBy)
{
  /// <summary>Ce qu'affiche une cellule dont la valeur est absente : une absence, pas une cellule mal rendue.</summary>
  internal const string Absent = "—";

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
      request.ReceivedOn.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
      request.IdentityVerified ? "Oui" : "Non",
      Capitalized(request.Right.FrenchLabel),
      TimeZoneInfo.ConvertTime(request.CreatedAt, ParisCalendar.TimeZone)
        .ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
      request.CreatedBy == DataSubjectRequest.OperatorAuthor ? "Opérateur" : request.CreatedBy);
  }

  /// <summary>
  /// Le libellé du droit en tête de cellule : « droit d'accès » devient « Droit d'accès ». Le
  /// libellé du noyau partagé se lit dans une phrase, et sans son article du RGPD.
  /// </summary>
  private static string Capitalized(string label) =>
    string.Concat(char.ToUpper(label[0], French).ToString(), label[1..]);
}
