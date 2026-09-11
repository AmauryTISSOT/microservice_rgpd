namespace MicroserviceRgpd.Core.Requests;

/// <summary>
/// Les champs auxquels une erreur de <see cref="DataSubjectRequest.Receive"/> est rattachée. Ce
/// sont les clés mêmes du corps envoyé par la page : une erreur rendue par le domaine se place sous
/// son champ sans table de correspondance.
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

  /// <summary>Le message.</summary>
  public const string Message = "message";

  /// <summary>Le droit invoqué.</summary>
  public const string Right = "right";
}
