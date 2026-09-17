using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using MicroserviceRgpd.Core.Configuration;
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
/// <b>Elle se corrige par <see cref="Modify"/></b>, tant qu'elle est En cours, sous les mêmes règles
/// de saisie : une correction ne peut pas produire une demande que la réception aurait refusée.
/// </para>
/// <para>
/// <b>Elle se prolonge par <see cref="Extend"/></b> : la date limite de réponse est reportée de deux
/// mois, sur un motif fermé et une justification écrite (ADR-0029).
/// </para>
/// <para>
/// <b>Elle se termine par <see cref="Complete"/></b>, quand le système hôte a appliqué le droit
/// invoqué (ADR-0026).
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

  /// <summary>
  /// De combien de mois une prolongation reporte la date limite de réponse. ⚠️ <b>C'est une
  /// constante, pas une saisie</b> : le règlement dit « deux mois », et non « jusqu'à deux mois »
  /// (ADR-0029).
  /// </summary>
  public const int ExtensionInMonths = 2;

  private DataSubjectRequest(ValidatedEntry entry, DateTimeOffset createdAt)
  {
    Id = DataSubjectRequestId.Next();
    Apply(entry);
    Status = RequestStatus.InProgress;
    CreatedBy = OperatorAuthor;
    CreatedAt = createdAt;
  }

  /// <summary>
  /// Le constructeur qu'EF Core emprunte pour rematérialiser une ligne. Il ne rejoue aucun invariant.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Il existe parce que le seul autre constructeur prend une saisie validée</b>, dont EF Core
  /// ne sait pas alimenter les paramètres : il ne lie que ce qui porte le nom d'une propriété.
  /// </remarks>
  private DataSubjectRequest()
  {
    Origin = null!;
    Right = null!;
    Status = null!;
    CreatedBy = null!;
  }

  /// <summary>L'identité engendrée à l'enregistrement.</summary>
  public DataSubjectRequestId Id { get; private set; }

  /// <summary>Le canal par lequel la demande est arrivée.</summary>
  public Origin Origin { get; private set; }

  /// <summary>Le jour où la demande est arrivée chez le responsable, jamais postérieur à aujourd'hui à Paris.</summary>
  public DateOnly ReceivedOn { get; private set; }

  /// <summary>
  /// Le jour avant lequel le responsable doit répondre : <see cref="ReceivedOn"/> plus un mois,
  /// ramené au dernier jour du mois suivant quand ce jour n'y existe pas (ADR-0021). ⚠️ Fixée à la
  /// réception et enregistrée : elle ne se recalcule pas à la lecture, et ne part jamais de l'instant
  /// d'enregistrement. Seule une <see cref="Modify"/> qui change la date de réception la refait.
  /// </summary>
  public DateOnly ResponseDeadline { get; private set; }

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

  /// <summary>
  /// Où en est la demande — un état qu'elle tient, pas la trace d'un <c>Gesture</c>. Elle naît
  /// <see cref="RequestStatus.InProgress"/>, et passe à <see cref="RequestStatus.Completed"/> par
  /// <see cref="Complete"/>.
  /// </summary>
  public RequestStatus Status { get; private set; }

  /// <summary>Qui a enregistré la demande : toujours <see cref="OperatorAuthor"/>.</summary>
  public string CreatedBy { get; private set; }

  /// <summary>L'instant d'enregistrement, en UTC.</summary>
  public DateTimeOffset CreatedAt { get; private set; }

  /// <summary>
  /// Qui a modifié la demande en dernier — <see cref="OperatorAuthor"/> —, ou <c>null</c> tant
  /// qu'aucune modification effective n'a eu lieu.
  /// </summary>
  public string? ModifiedBy { get; private set; }

  /// <summary>
  /// L'instant de la dernière modification, en UTC, ou <c>null</c> tant qu'aucune modification
  /// effective n'a eu lieu. ⚠️ <b>Cette empreinte s'écrase</b> : elle dit la dernière modification,
  /// pas l'histoire des modifications.
  /// </summary>
  public DateTimeOffset? ModifiedAt { get; private set; }

  /// <summary>
  /// La date limite de réponse telle qu'elle valait <b>avant</b> la prolongation, ou <c>null</c> tant
  /// que la demande n'a pas été prolongée.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Enregistrée, et non recalculée par soustraction</b> : <see cref="DateOnly.AddMonths"/>
  /// n'est pas inversible — 31 décembre plus deux mois donne 28 février, dont deux mois en moins
  /// donnent 28 décembre. C'est elle qui fixe l'échéance de l'obligation d'informer la personne
  /// concernée.
  ///
  /// ⚠️ <b>C'est une trace, pas une source</b> : la date qui fait foi partout ailleurs reste
  /// <see cref="ResponseDeadline"/>.
  /// </remarks>
  public DateOnly? InitialResponseDeadline { get; private set; }

  /// <summary>Le motif de la prolongation, ou <c>null</c> tant que la demande n'a pas été prolongée.</summary>
  public ExtensionGround? ExtensionGround { get; private set; }

  /// <summary>Le texte qui justifie la prolongation, ou <c>null</c> tant qu'elle n'a pas eu lieu.</summary>
  public ExtensionJustification? ExtensionJustification { get; private set; }

  /// <summary>L'instant de la prolongation, en UTC, ou <c>null</c> tant qu'elle n'a pas eu lieu.</summary>
  public DateTimeOffset? ExtendedAt { get; private set; }

  /// <summary>
  /// La demande a-t-elle été prolongée ? Les quatre valeurs de la prolongation sont renseignées
  /// ensemble : <see cref="ExtendedAt"/> les dit toutes.
  /// </summary>
  public bool Extended => ExtendedAt is not null;

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
    var validated = Validate(entry, todayInParis);

    if (!validated.IsSuccess)
    {
      return Result<DataSubjectRequest>.Invalid(validated.ValidationErrors);
    }

    return new DataSubjectRequest(validated.Value, recordedAt.ToUniversalTime());
  }

  /// <summary>
  /// <b>Modifie une demande</b> — l'<c>Operator</c> corrige une erreur de saisie. C'est un
  /// <c>Gesture</c> : il laisse une empreinte, <see cref="ModifiedBy"/> et <see cref="ModifiedAt"/>.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>L'ordre des refus n'est pas indifférent.</b> Une demande close est refusée <b>avant</b> toute
  /// validation : lister des erreurs de saisie sous les champs d'un formulaire qui ne pourra jamais
  /// enregistrer n'apprend rien à l'<c>Operator</c>.
  /// </para>
  /// <para>
  /// ⚠️ <b>Une modification qui ne change aucune valeur n'a pas eu lieu</b> : elle ne touche aucune
  /// propriété — pas même <see cref="ResponseDeadline"/> — et ne laisse aucune empreinte. Rien n'étant
  /// touché, le suivi des modifications n'émet aucun <c>UPDATE</c>, et l'empreinte reste celle de la
  /// modification précédente.
  /// </para>
  /// </remarks>
  /// <param name="entry">Les valeurs brutes corrigées, ni trimées ni validées.</param>
  /// <param name="todayInParis">Aujourd'hui à Paris — voir <see cref="ParisCalendar"/>. La date de réception ne le dépasse pas.</param>
  /// <param name="modifiedAt">L'instant de la modification, lu sur l'horloge.</param>
  public Result Modify(DataSubjectRequestEntry entry, DateOnly todayInParis, DateTimeOffset modifiedAt)
  {
    if (Status != RequestStatus.InProgress)
    {
      return Result.Conflict();
    }

    var validated = Validate(entry, todayInParis);

    if (!validated.IsSuccess)
    {
      return Result.Invalid(validated.ValidationErrors);
    }

    var corrected = validated.Value;

    if (corrected == CurrentEntry())
    {
      return Result.Success();
    }

    Apply(corrected);
    ModifiedBy = OperatorAuthor;
    ModifiedAt = modifiedAt.ToUniversalTime();

    return Result.Success();
  }

  /// <summary>
  /// <b>Passe la demande à Terminée</b> : le système hôte a appliqué le droit invoqué (ADR-0026).
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Une demande close est refusée en conflit, avant tout le reste</b> : une Annulée ne devient
  /// pas Terminée, et une Terminée ne l'est pas deux fois.
  /// </para>
  /// <para>
  /// Le passage est un état, pas une trace : il ne pose aucune empreinte. La trace de l'exécution est
  /// l'<see cref="ExecutionAttempt"/>, écrite à côté.
  /// </para>
  /// </remarks>
  public Result Complete()
  {
    if (Status != RequestStatus.InProgress)
    {
      return Result.Conflict();
    }

    Status = RequestStatus.Completed;

    return Result.Success();
  }

  /// <summary>
  /// <b>Prolonge la demande</b> — l'<c>Operator</c> reporte de deux mois la date limite de réponse, au
  /// titre de l'article 12 §3 (ADR-0029). C'est un <c>Gesture</c> : il laisse ses quatre valeurs,
  /// <see cref="InitialResponseDeadline"/>, <see cref="ExtensionGround"/>,
  /// <see cref="ExtensionJustification"/> et <see cref="ExtendedAt"/>, posées ensemble.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>La date limite en vigueur est d'abord recopiée, puis reportée</b> : la prolongation part de
  /// la date limite, jamais de la date de réception. Le repli de fin de mois est celui de
  /// <see cref="DateOnly.AddMonths"/>, comme la règle de l'ADR-0021 — 31 décembre plus deux mois
  /// donne 28 (ou 29) février.
  /// </para>
  /// <para>
  /// ⚠️ <b>La date qui fait foi partout ailleurs reste <see cref="ResponseDeadline"/></b> : aucun
  /// écran, aucun tri, aucun signalement ne change de source. Une demande fraîchement prolongée perd
  /// donc son « Échéance proche », et c'est le comportement voulu.
  /// </para>
  /// <para>
  /// ⚠️ <b>Le geste ne change pas le statut</b> : une demande prolongée reste En cours.
  /// </para>
  /// <para>
  /// ⚠️ <b>Il ne juge pas sa propre fenêtre</b> : c'est <see cref="ExtensionBlockFacing"/> qui dit si
  /// la prolongation est possible, et l'appelant la lui demande <b>juste avant</b> — comme
  /// l'exécution interroge <see cref="ExecutionBlockFacing"/> avant de remettre le droit. Écrite ici
  /// aussi, la règle se dirait deux fois, et l'écran finirait par éteindre un bouton que le serveur
  /// accepte.
  /// </para>
  /// </remarks>
  /// <param name="entry">Les valeurs brutes saisies, ni trimées ni validées.</param>
  /// <param name="extendedAt">L'instant de la prolongation, lu sur l'horloge.</param>
  public Result Extend(ExtensionEntry entry, DateTimeOffset extendedAt)
  {
    var validated = ValidateExtension(entry);

    if (!validated.IsSuccess)
    {
      return Result.Invalid(validated.ValidationErrors);
    }

    var (ground, justification) = validated.Value;

    InitialResponseDeadline = ResponseDeadline;
    ResponseDeadline = DeadlineExtendedFrom(ResponseDeadline);
    ExtensionGround = ground;
    ExtensionJustification = justification;
    ExtendedAt = extendedAt.ToUniversalTime();

    return Result.Success();
  }

  /// <summary>
  /// <b>Dit si la demande s'exécute</b> face au canal d'exercice que le Paramétrage associe à son
  /// droit, <b>et à ce que le déploiement sait publier</b> : <c>null</c> quand elle est exécutable,
  /// sinon le <b>premier</b> <see cref="ExecutionBlock"/> — demande close, identité non vérifiée,
  /// email manquant, droit non configuré, connexion au broker absente sur un droit routé (ADR-0026,
  /// ADR-0027, ADR-0028).
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Le canal est lu tel que <c>Configuration</c> le publie</b>, sans traduction :
  /// <c>Requests</c> s'y conforme. Le <c>switch</c> sur ses trois cas est exhaustif — la hiérarchie
  /// est fermée —, et les deux motifs du canal en tombent ensemble.
  /// </para>
  /// <para>
  /// <b>Il faut les deux arguments pour répondre, et ils ne disent pas la même chose</b> : le canal
  /// est ce que l'intégrateur a déclaré pour ce droit, la connexion est ce que l'exploitant a donné
  /// à ce déploiement. Un routage sans connexion est un Paramétrage juste sur un déploiement muet —
  /// et c'est le seul cas où la connexion pèse : un droit adressé en HTTP se moque du bus.
  /// </para>
  /// <para>
  /// ⚠️ <b>Une connexion configurée ne promet aucun broker joignable</b> : elle dit ce que le
  /// déploiement déclare. Le bouton s'allume, et c'est la publication qui découvrira l'échec.
  /// </para>
  /// </remarks>
  /// <param name="channel">Le canal d'exercice du droit invoqué — une adresse, un routage, ou « non configuré ».</param>
  /// <param name="connection">Ce que le déploiement déclare savoir publier.</param>
  /// <exception cref="ArgumentNullException"><paramref name="channel"/> ou <paramref name="connection"/> est absent.</exception>
  public ExecutionBlock? ExecutionBlockFacing(ExerciseChannel channel, BrokerConnection connection)
  {
    ArgumentNullException.ThrowIfNull(channel);
    ArgumentNullException.ThrowIfNull(connection);

    if (Status != RequestStatus.InProgress)
    {
      return ExecutionBlock.Closed;
    }

    if (!IdentityVerified)
    {
      return ExecutionBlock.IdentityNotVerified;
    }

    if (Email is null)
    {
      return ExecutionBlock.EmailMissing;
    }

    return channel switch
    {
      ExerciseChannel.HttpEndpoint => null,
      ExerciseChannel.RabbitMq =>
        connection is BrokerConnection.Absent ? ExecutionBlock.BrokerConnectionMissing : null,

      // « Non configuré », le troisième et dernier cas : la hiérarchie est fermée.
      _ => ExecutionBlock.RightNotConfigured,
    };
  }

  /// <summary>
  /// <b>Dit si la demande se prolonge</b> le jour <paramref name="todayInParis"/> : <c>null</c> quand
  /// elle le peut, sinon le <b>premier</b> <see cref="ExtensionBlock"/> — demande close, demande déjà
  /// prolongée, date limite de réponse dépassée (ADR-0029).
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Le jour limite est accepté</b> : la fenêtre se ferme le <b>lendemain</b> de la date limite
  /// de réponse. L'<c>Operator</c> ne perd pas le dernier jour que le règlement lui accorde.
  /// </para>
  /// <para>
  /// ⚠️ <b>Il ne regarde ni le Paramétrage ni le déploiement</b>, à la différence de
  /// <see cref="ExecutionBlockFacing"/> : prolonger un délai ne remet rien à personne. La demande et
  /// le jour qu'il est suffisent à répondre.
  /// </para>
  /// <para>
  /// ⚠️ <b>Une demande déjà prolongée se juge sur sa date limite reportée</b>, mais elle est bloquée
  /// avant qu'on la lise : <see cref="ExtensionBlock.AlreadyExtended"/> passe devant — il n'y a pas
  /// de seconde prolongation, fenêtre ouverte ou non.
  /// </para>
  /// </remarks>
  /// <param name="todayInParis">Aujourd'hui à Paris — voir <see cref="ParisCalendar"/>.</param>
  public ExtensionBlock? ExtensionBlockFacing(DateOnly todayInParis)
  {
    if (Status != RequestStatus.InProgress)
    {
      return ExtensionBlock.Closed;
    }

    if (Extended)
    {
      return ExtensionBlock.AlreadyExtended;
    }

    return todayInParis > ResponseDeadline ? ExtensionBlock.DeadlineElapsed : null;
  }

  /// <summary>
  /// La règle « réception plus un mois, ramené au dernier jour du mois quand ce jour n'y existe pas »
  /// (ADR-0021), écrite <b>une seule fois</b> : la réception la pose, la modification la recalcule.
  /// </summary>
  private static DateOnly DeadlineFor(DateOnly receivedOn) => receivedOn.AddMonths(1);

  /// <summary>
  /// La date limite que <paramref name="responseDeadline"/> devient une fois prolongée : deux mois
  /// de plus, au repli de fin de mois de <see cref="DateOnly.AddMonths"/>. Écrite <b>une seule
  /// fois</b> : le geste la pose, et l'écran l'annonce avant qu'il ne soit posé.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>C'est le serveur qui la calcule, jamais le navigateur</b> : <c>Date.setMonth</c> ne fait
  /// pas le même repli — un 31 décembre plus deux mois donne 3 mars en JavaScript. La modale
  /// annoncerait une date, le serveur en écrirait une autre.
  /// </remarks>
  public static DateOnly DeadlineExtendedFrom(DateOnly responseDeadline) =>
    responseDeadline.AddMonths(ExtensionInMonths);

  /// <summary>
  /// Les règles de saisie, partagées par la réception et la modification : <b>toutes</b> les raisons
  /// de refuser à la fois, ou les huit valeurs converties.
  /// </summary>
  private static Result<ValidatedEntry> Validate(DataSubjectRequestEntry entry, DateOnly todayInParis)
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
      return Result<ValidatedEntry>.Invalid(errors);
    }

    return new ValidatedEntry(
      entry.Origin,
      receivedOn!.Value,
      lastName,
      firstName,
      email,
      entry.IdentityVerified,
      message!.Value,
      right!);
  }

  /// <summary>
  /// Pose les huit valeurs d'une saisie validée, et la date limite qui en découle. Écrit une seule
  /// fois : la réception les pose à la naissance, la modification les repose.
  /// </summary>
  [MemberNotNull(nameof(Origin), nameof(Right))]
  private void Apply(ValidatedEntry entry)
  {
    Origin = entry.Origin;
    ReceivedOn = entry.ReceivedOn;
    ResponseDeadline = DeadlineFor(entry.ReceivedOn);
    LastName = entry.LastName;
    FirstName = entry.FirstName;
    Email = entry.Email;
    IdentityVerified = entry.IdentityVerified;
    Message = entry.Message;
    Right = entry.Right;
  }

  /// <summary>Les valeurs courantes sous la forme d'une saisie validée, pour se comparer à une autre.</summary>
  private ValidatedEntry CurrentEntry() => new(
    Origin,
    ReceivedOn,
    LastName,
    FirstName,
    Email,
    IdentityVerified,
    Message,
    Right);

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

  /// <summary>
  /// Les règles de saisie d'une prolongation : <b>toutes</b> les raisons de refuser à la fois, ou les
  /// deux valeurs converties.
  /// </summary>
  private static Result<(ExtensionGround Ground, ExtensionJustification Justification)> ValidateExtension(
    ExtensionEntry entry)
  {
    ArgumentNullException.ThrowIfNull(entry);

    var errors = new List<ValidationError>();

    var ground = ReadExtensionGround(entry.Ground, errors);
    var justification = ReadRequired(
      entry.Justification,
      Requests.ExtensionJustification.TryFrom,
      DataSubjectRequestField.ExtensionJustification,
      errors);

    if (errors.Count > 0)
    {
      return Result<(ExtensionGround, ExtensionJustification)>.Invalid(errors);
    }

    return (ground!, justification!.Value);
  }

  /// <summary>
  /// L'un des deux motifs, sous son nom canonique. ⚠️ <b>Le choix fermé de l'écran ne suffit pas</b> :
  /// un envoi forgé n'en vient pas, et le domaine revérifie.
  /// </summary>
  private static ExtensionGround? ReadExtensionGround(string? raw, List<ValidationError> errors)
  {
    if (Requests.ExtensionGround.TryFromName(raw?.Trim(), out var ground))
    {
      return ground;
    }

    errors.Add(Error(DataSubjectRequestField.ExtensionGround, DataSubjectRequestMessages.ExtensionGroundMissing));
    return null;
  }

  private static ValidationError Error(string field, string message) =>
    new() { Identifier = field, ErrorMessage = message };

  /// <summary>
  /// Les huit valeurs d'une saisie <b>déjà converties</b>, une fois les règles passées. ⚠️ C'est un
  /// artefact d'implémentation, et non un terme du domaine : il n'existe que pour que la réception et
  /// la modification partagent leurs validateurs, et pour que « rien n'a changé » se lise en une
  /// égalité structurelle plutôt qu'en huit comparaisons recopiées.
  /// </summary>
  private sealed record ValidatedEntry(
    Origin Origin,
    DateOnly ReceivedOn,
    LastName? LastName,
    FirstName? FirstName,
    EmailAddress? Email,
    bool IdentityVerified,
    RequestMessage Message,
    DataSubjectRight Right);
}
