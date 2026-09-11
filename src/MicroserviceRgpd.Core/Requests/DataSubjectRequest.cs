using System.Globalization;
using MicroserviceRgpd.Core.SharedKernel;
using Vogen;

namespace MicroserviceRgpd.Core.Requests;

/// <summary>
/// Une <b>demande</b> d'exercice d'un droit RGPD, telle que l'<c>Operator</c> l'enregistre à sa
/// réception : ce qui est arrivé, par quel canal, quand, de qui, et quel droit la personne invoque.
/// </summary>
/// <remarks>
/// <para>
/// <b>Elle naît par <see cref="Receive"/>, et seulement valide.</b> La fabrique ne lève pas devant
/// une saisie fautive : elle rend <b>toutes</b> les raisons de la refuser à la fois, chacune
/// rattachée à son champ, pour que l'<c>Operator</c> corrige tout d'un coup.
/// </para>
/// <para>
/// ⚠️ <b>La date de réception n'est pas l'instant d'enregistrement.</b> La première est déclarée par
/// l'<c>Operator</c> — une demande transcrite d'un courrier a été reçue avant d'entrer dans le
/// service —, le second est lu sur l'horloge.
/// </para>
/// </remarks>
public sealed class DataSubjectRequest : IAggregateRoot
{
  /// <summary>
  /// L'auteur de toute demande, tant que le service n'authentifie personne : l'<c>Operator</c> n'a
  /// pas de nom.
  /// </summary>
  public const string OperatorAuthor = "operator";

  private DataSubjectRequest(
    Origin origin,
    DateOnly receivedOn,
    LastName? lastName,
    FirstName? firstName,
    EmailAddress? email,
    bool identityVerified,
    RequestMessage message,
    DataSubjectRight right,
    DateTimeOffset createdAt)
  {
    Id = DataSubjectRequestId.Next();
    Origin = origin;
    ReceivedOn = receivedOn;
    LastName = lastName;
    FirstName = firstName;
    Email = email;
    IdentityVerified = identityVerified;
    Message = message;
    Right = right;
    CreatedBy = OperatorAuthor;
    CreatedAt = createdAt;
  }

  /// <summary>L'identité engendrée à l'enregistrement.</summary>
  public DataSubjectRequestId Id { get; private set; }

  /// <summary>Le canal par lequel la demande est arrivée.</summary>
  public Origin Origin { get; private set; }

  /// <summary>Le jour où la demande est arrivée chez le responsable, jamais postérieur à aujourd'hui à Paris.</summary>
  public DateOnly ReceivedOn { get; private set; }

  /// <summary>Le nom de la personne, ou <c>null</c>.</summary>
  public LastName? LastName { get; private set; }

  /// <summary>Le prénom de la personne, ou <c>null</c>.</summary>
  public FirstName? FirstName { get; private set; }

  /// <summary>L'email de la personne, ou <c>null</c>.</summary>
  public EmailAddress? Email { get; private set; }

  /// <summary>L'attestation, déclarative, que l'<c>Operator</c> a vérifié l'identité de la personne.</summary>
  public bool IdentityVerified { get; private set; }

  /// <summary>Le contenu de la demande tel qu'il a été reçu.</summary>
  public RequestMessage Message { get; private set; }

  /// <summary>Le droit invoqué — l'un des six, jamais <see cref="DataSubjectRight.OutOfScope"/>.</summary>
  public DataSubjectRight Right { get; private set; }

  /// <summary>Qui a enregistré la demande : toujours <see cref="OperatorAuthor"/>.</summary>
  public string CreatedBy { get; private set; }

  /// <summary>L'instant d'enregistrement, en UTC.</summary>
  public DateTimeOffset CreatedAt { get; private set; }

  /// <summary>
  /// <b>Reçoit une demande</b> à partir des valeurs brutes saisies par l'<c>Operator</c>, ou rend
  /// <b>toutes</b> les raisons de la refuser, chacune rattachée à un <see cref="DataSubjectRequestField"/>.
  /// </summary>
  /// <param name="entry">Les valeurs brutes, ni trimées ni validées.</param>
  /// <param name="todayInParis">Aujourd'hui à Paris — voir <see cref="ParisCalendar"/>. La date de réception ne le dépasse pas.</param>
  /// <param name="recordedAt">L'instant d'enregistrement, lu sur l'horloge.</param>
  public static Result<DataSubjectRequest> Receive(
    DataSubjectRequestEntry entry,
    DateOnly todayInParis,
    DateTimeOffset recordedAt)
  {
    ArgumentNullException.ThrowIfNull(entry);
    ArgumentNullException.ThrowIfNull(entry.Origin);

    var errors = new List<ValidationError>();

    var receivedOn = ReadReceivedOn(entry.ReceivedOn, todayInParis, errors);
    var lastName = ReadOptional(entry.LastName, Requests.LastName.TryFrom, DataSubjectRequestField.LastName, errors);
    var firstName = ReadOptional(entry.FirstName, Requests.FirstName.TryFrom, DataSubjectRequestField.FirstName, errors);
    var email = ReadOptional(entry.Email, EmailAddress.TryFrom, DataSubjectRequestField.Email, errors);
    CheckIdentification(entry, errors);
    var message = ReadRequired(entry.Message, RequestMessage.TryFrom, DataSubjectRequestField.Message, errors);
    var right = ReadRight(entry.Right, errors);

    if (errors.Count > 0)
    {
      return Result<DataSubjectRequest>.Invalid(errors);
    }

    return new DataSubjectRequest(
      entry.Origin,
      receivedOn!.Value,
      lastName,
      firstName,
      email,
      entry.IdentityVerified,
      message!.Value,
      right!,
      recordedAt.ToUniversalTime());
  }

  /// <summary>Obligatoire, au format ISO du fil, et jamais postérieure à aujourd'hui à Paris. Pas de borne basse.</summary>
  private static DateOnly? ReadReceivedOn(string? raw, DateOnly todayInParis, List<ValidationError> errors)
  {
    if (string.IsNullOrWhiteSpace(raw))
    {
      errors.Add(Error(DataSubjectRequestField.ReceivedOn, DataSubjectRequestMessages.ReceivedOnMissing));
      return null;
    }

    if (!DateOnly.TryParseExact(raw.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var receivedOn))
    {
      errors.Add(Error(DataSubjectRequestField.ReceivedOn, DataSubjectRequestMessages.ReceivedOnMalformed));
      return null;
    }

    if (receivedOn > todayInParis)
    {
      errors.Add(Error(DataSubjectRequestField.ReceivedOn, DataSubjectRequestMessages.ReceivedOnInTheFuture));
      return null;
    }

    return receivedOn;
  }

  /// <summary>
  /// <b>Un email, ou un nom et un prénom.</b> À défaut, l'erreur va sous l'email et sous chacun des
  /// champs nom et prénom qui manquent. Un email renseigné identifie, même mal formé : sa forme est
  /// refusée ailleurs, et un nom ou un prénom partiel ne bloque plus.
  /// </summary>
  private static void CheckIdentification(DataSubjectRequestEntry entry, List<ValidationError> errors)
  {
    var hasEmail = !string.IsNullOrWhiteSpace(entry.Email);
    var hasLastName = !string.IsNullOrWhiteSpace(entry.LastName);
    var hasFirstName = !string.IsNullOrWhiteSpace(entry.FirstName);

    if (hasEmail || (hasLastName && hasFirstName))
    {
      return;
    }

    errors.Add(Error(DataSubjectRequestField.Email, DataSubjectRequestMessages.IdentificationMissing));

    if (!hasLastName)
    {
      errors.Add(Error(DataSubjectRequestField.LastName, DataSubjectRequestMessages.IdentificationMissing));
    }

    if (!hasFirstName)
    {
      errors.Add(Error(DataSubjectRequestField.FirstName, DataSubjectRequestMessages.IdentificationMissing));
    }
  }

  /// <summary>
  /// L'un des six droits, sous son nom canonique. ⚠️ <see cref="DataSubjectRight.OutOfScope"/> est un
  /// verdict de <c>Qualification</c>, pas un droit qu'on invoque : il est refusé comme l'absence.
  /// </summary>
  private static DataSubjectRight? ReadRight(string? raw, List<ValidationError> errors)
  {
    if (DataSubjectRight.TryFromName(raw?.Trim(), out var right) && right != DataSubjectRight.OutOfScope)
    {
      return right;
    }

    errors.Add(Error(DataSubjectRequestField.Right, DataSubjectRequestMessages.RightMissing));
    return null;
  }

  /// <summary>Un champ facultatif : absent s'il ne contient que des espaces, sinon validé par son objet valeur.</summary>
  private static T? ReadOptional<T>(
    string? raw,
    Func<string, ValueObjectOrError<T>> tryFrom,
    string field,
    List<ValidationError> errors)
    where T : struct =>
    string.IsNullOrWhiteSpace(raw) ? null : ReadRequired(raw, tryFrom, field, errors);

  /// <summary>Un champ lu par son objet valeur, dont le refus devient l'erreur du champ.</summary>
  private static T? ReadRequired<T>(
    string? raw,
    Func<string, ValueObjectOrError<T>> tryFrom,
    string field,
    List<ValidationError> errors)
    where T : struct
  {
    var read = tryFrom(raw ?? string.Empty);
    if (read.IsSuccess)
    {
      return read.ValueObject;
    }

    errors.Add(Error(field, read.Error.ErrorMessage));
    return null;
  }

  private static ValidationError Error(string field, string message) =>
    new() { Identifier = field, ErrorMessage = message };
}
