using System.Globalization;
using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.UseCases.Requests.ReadDataSubjectRequestValues;

namespace MicroserviceRgpd.Web.Pages.Requests;

/// <summary>
/// <b>Les huit champs que l'<c>Operator</c> saisit</b> dans la modale — <b>des chaînes, et rien que
/// des chaînes</b>, sous les clés mêmes du corps. Rien n'est trimé ni jugé ici : c'est
/// <see cref="DataSubjectRequest.Receive"/> qui en décide.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>L'identifiant de la demande n'y entre pas</b> : ce n'est pas une donnée saisie. Un handler
/// qui en a besoin le prend en paramètre, comme celui de la suppression.
/// </para>
/// <para>
/// Il sert <b>dans les deux sens</b> : lié depuis le corps posté, et rendu en JSON pour pré-remplir
/// la modale. Les clés de lecture sont donc celles d'écriture par construction, sans table de
/// correspondance à tenir à jour.
/// </para>
/// </remarks>
public sealed class RequestForm
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
  /// <b>Les valeurs enregistrées d'une demande, remises en champs</b> — chacune sous la forme même
  /// que la modale renverra si l'<c>Operator</c> n'y touche pas.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Les noms canoniques, jamais les libellés français</b> : ce sont eux que
  /// <see cref="ToEntry"/> relira. « Courrier » n'est pas une valeur du fil.
  /// </remarks>
  /// <param name="values">Les valeurs telles que la demande les tient.</param>
  /// <exception cref="ArgumentNullException"><paramref name="values"/> est absente.</exception>
  public static RequestForm Of(DataSubjectRequestValues values)
  {
    ArgumentNullException.ThrowIfNull(values);

    return new RequestForm
    {
      Origin = values.Origin.Name,
      ReceivedOn = values.ReceivedOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
      LastName = values.LastName?.Value,
      FirstName = values.FirstName?.Value,
      Email = values.Email?.Value,
      IdentityVerified = values.IdentityVerified,
      Message = values.Message.Value,
      Right = values.Right.Name,
    };
  }

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
