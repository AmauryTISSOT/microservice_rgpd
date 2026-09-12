namespace MicroserviceRgpd.Core.Requests;

/// <summary>
/// <b>Les noms des champs du formulaire</b> — les clés mêmes du corps envoyé par la page. Une erreur
/// rendue par le domaine s'y rattache, et se place sous son champ sans table de correspondance ;
/// certains champs n'en portent jamais.
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
}
