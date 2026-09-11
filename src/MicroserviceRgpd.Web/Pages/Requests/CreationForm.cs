using MicroserviceRgpd.Core.Requests;

namespace MicroserviceRgpd.Web.Pages.Requests;

/// <summary>
/// Ce que la modale de création envoie à <c>POST /demandes?handler=Create</c> — <b>des chaînes, et
/// rien que des chaînes</b>, sous les clés mêmes du corps. Rien n'est trimé ni jugé ici : c'est
/// <see cref="DataSubjectRequest.Receive"/> qui en décide.
/// </summary>
public sealed class CreationForm
{
  /// <summary>Le nom du canal d'arrivée — <c>Email</c> ou <c>Letter</c>.</summary>
  public string? Origin { get; set; }

  /// <summary>La date de réception, au format ISO du fil (<c>yyyy-MM-dd</c>).</summary>
  public string? ReceivedOn { get; set; }

  /// <summary>Le nom, facultatif.</summary>
  public string? LastName { get; set; }

  /// <summary>Le prénom, facultatif.</summary>
  public string? FirstName { get; set; }

  /// <summary>L'email, facultatif.</summary>
  public string? Email { get; set; }

  /// <summary>L'identité a-t-elle été vérifiée ? Une case non cochée n'envoie rien, et vaut non.</summary>
  public bool IdentityVerified { get; set; }

  /// <summary>Le message reçu.</summary>
  public string? Message { get; set; }

  /// <summary>Le nom canonique du droit invoqué.</summary>
  public string? Right { get; set; }

  /// <summary>
  /// La saisie, prête pour le domaine — ou <c>null</c> si l'origine n'est pas l'un des deux canaux.
  /// Le formulaire ne propose que ceux-là : une autre valeur n'est pas une saisie humaine, mais un
  /// envoi forgé.
  /// </summary>
  public DataSubjectRequestEntry? ToEntry()
  {
    if (!Core.Requests.Origin.TryFromName(Origin?.Trim(), out var origin))
    {
      return null;
    }

    return new DataSubjectRequestEntry(
      origin,
      ReceivedOn,
      LastName,
      FirstName,
      Email,
      IdentityVerified,
      Message,
      Right);
  }
}
