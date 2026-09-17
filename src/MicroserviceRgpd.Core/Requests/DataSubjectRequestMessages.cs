namespace MicroserviceRgpd.Core.Requests;

/// <summary>
/// Les messages par lesquels une saisie est refusée, écrits <b>une seule fois</b>, ici. Le domaine
/// les rend dans le <c>Result</c> de <see cref="DataSubjectRequest.Receive"/> et de
/// <see cref="DataSubjectRequest.Extend"/>, et la page les fournit tels quels au navigateur : le
/// script ne porte que la logique, il n'écrit aucun message.
/// </summary>
/// <remarks>
/// ⚠️ Les nombres sont écrits en toutes lettres dans les phrases, et non interpolés depuis les
/// plafonds des objets valeurs : une constante interpolée cesserait d'être une constante. Un
/// plafond qui change se change donc aux deux endroits — le test de chaque limite y veille.
/// </remarks>
public static class DataSubjectRequestMessages
{
  /// <summary>Ni email, ni nom et prénom ensemble.</summary>
  public const string IdentificationMissing = "Renseignez un email, ou un nom et un prénom.";

  /// <summary>La date de réception est absente ou ne contient que des espaces.</summary>
  public const string ReceivedOnMissing = "La date de réception est obligatoire.";

  /// <summary>La date de réception ne se lit pas comme une date.</summary>
  public const string ReceivedOnMalformed = "La date de réception doit être au format jj/mm/aaaa.";

  /// <summary>La date de réception est postérieure à « aujourd'hui à Paris ».</summary>
  public const string ReceivedOnInTheFuture = "La date de réception ne peut pas être dans le futur.";

  /// <summary>L'email ne suit pas la forme WHATWG, ou dépasse son plafond.</summary>
  public const string EmailInvalid = "L'adresse email n'est pas valide.";

  /// <summary>Le nom dépasse son plafond.</summary>
  public const string LastNameTooLong = "Le nom ne peut pas dépasser 100 caractères.";

  /// <summary>Le prénom dépasse son plafond.</summary>
  public const string FirstNameTooLong = "Le prénom ne peut pas dépasser 100 caractères.";

  /// <summary>Le message est absent ou ne contient que des espaces.</summary>
  public const string MessageMissing = "Le message est obligatoire.";

  /// <summary>Le message dépasse son plafond.</summary>
  public const string MessageTooLong = "Le message ne peut pas dépasser 10 000 caractères.";

  /// <summary>Aucun des six droits n'est choisi — <c>OutOfScope</c> compris, qui n'en est pas un.</summary>
  public const string RightMissing = "Sélectionnez un droit RGPD.";

  /// <summary>Aucun motif de prolongation n'est choisi, ou la valeur envoyée n'en est pas un.</summary>
  public const string ExtensionGroundMissing = "Sélectionnez un motif de prolongation.";

  /// <summary>La justification de la prolongation est absente ou ne contient que des espaces.</summary>
  public const string ExtensionJustificationMissing = "La justification est obligatoire.";

  /// <summary>La justification de la prolongation dépasse son plafond.</summary>
  public const string ExtensionJustificationTooLong = "La justification ne peut pas dépasser 2 000 caractères.";

  /// <summary>
  /// Les <b>dix</b> messages de la saisie d'une demande, sous une clé stable, pour que la page les
  /// sérialise dans l'îlot JSON que lit le script du navigateur. ⚠️ Les refus de la prolongation n'y
  /// sont pas : la modale de prolongation ne rejoue aucune règle avant d'envoyer.
  /// </summary>
  public static IReadOnlyDictionary<string, string> All { get; } = new Dictionary<string, string>
  {
    ["identificationMissing"] = IdentificationMissing,
    ["receivedOnMissing"] = ReceivedOnMissing,
    ["receivedOnMalformed"] = ReceivedOnMalformed,
    ["receivedOnInTheFuture"] = ReceivedOnInTheFuture,
    ["emailInvalid"] = EmailInvalid,
    ["lastNameTooLong"] = LastNameTooLong,
    ["firstNameTooLong"] = FirstNameTooLong,
    ["messageMissing"] = MessageMissing,
    ["messageTooLong"] = MessageTooLong,
    ["rightMissing"] = RightMissing,
  };
}
