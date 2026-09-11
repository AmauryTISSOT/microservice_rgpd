namespace MicroserviceRgpd.Core.Requests;

/// <summary>
/// Les valeurs <b>brutes</b> d'une demande, telles que l'<c>Operator</c> les a saisies : rien n'y
/// est encore trimé, validé ni interprété. C'est ce que <see cref="DataSubjectRequest.Receive"/>
/// transforme en demande, ou en la liste de ce qui l'en empêche.
/// </summary>
/// <param name="Origin">Le canal par lequel la demande est arrivée.</param>
/// <param name="ReceivedOn">La date de réception, au format ISO du fil (<c>yyyy-MM-dd</c>).</param>
/// <param name="LastName">Le nom, facultatif.</param>
/// <param name="FirstName">Le prénom, facultatif.</param>
/// <param name="Email">L'email, facultatif.</param>
/// <param name="IdentityVerified">L'identité a-t-elle été vérifiée par l'<c>Operator</c> ? Déclaratif, sans règle.</param>
/// <param name="Message">Le contenu de la demande, obligatoire.</param>
/// <param name="Right">Le nom canonique du droit invoqué — <c>Access</c>, <c>Erasure</c>…</param>
public sealed record DataSubjectRequestEntry(
  Origin Origin,
  string? ReceivedOn,
  string? LastName,
  string? FirstName,
  string? Email,
  bool IdentityVerified,
  string? Message,
  string? Right);
