namespace MicroserviceRgpd.Core.Requests;

/// <summary>
/// <b>Les noms des champs des formulaires de la page</b> — les clés mêmes des corps qu'elle envoie :
/// les huit de la saisie d'une demande, puis les deux de la prolongation. Une erreur rendue par le
/// domaine s'y rattache, et se place sous son champ sans table de correspondance ; certains champs
/// n'en portent jamais.
/// </summary>
public static class DataSubjectRequestField
{
  /// <summary>Le canal d'arrivée.</summary>
  public const string Origin = "origin";

  /// <summary>La date de réception.</summary>
  public const string ReceivedOn = "receivedOn";

  /// <summary>Le nom.</summary>
  public const string LastName = "lastName";

  /// <summary>Le prénom.</summary>
  public const string FirstName = "firstName";

  /// <summary>L'email — qui porte aussi l'erreur d'identification.</summary>
  public const string Email = "email";

  /// <summary>L'identité a-t-elle été vérifiée ? ⚠️ Aucun refus ne vise ce champ.</summary>
  public const string IdentityVerified = "identityVerified";

  /// <summary>Le message.</summary>
  public const string Message = "message";

  /// <summary>Le droit invoqué.</summary>
  public const string Right = "right";

  /// <summary>Le motif de prolongation.</summary>
  public const string ExtensionGround = "extensionGround";

  /// <summary>La justification de la prolongation.</summary>
  public const string ExtensionJustification = "extensionJustification";
}
