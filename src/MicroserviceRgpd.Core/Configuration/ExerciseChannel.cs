namespace MicroserviceRgpd.Core.Configuration;

/// <summary>
/// Le <b>canal d'exercice</b> d'un droit : le moyen par lequel le service le fera exercer. C'est le
/// <b>genre</b>, fermé à <b>trois cas</b> — une adresse HTTP, un routage RabbitMQ, ou « non
/// configuré » (ADR-0027).
/// </summary>
/// <remarks>
/// <para>
/// <b>« Non configuré » est un cas nommé, pas un <c>null</c>.</b> C'est un état du glossaire — celui
/// d'un service qu'on vient d'installer —, pas un trou : le nommer rend exhaustive toute lecture
/// d'un canal, sans branche nulle.
/// </para>
/// <para>
/// <b>La hiérarchie est fermée</b> par un constructeur privé : les trois cas sont déclarés ici, et
/// personne au dehors ne peut en ajouter un quatrième. Un <c>switch</c> sur les trois est donc
/// complet pour toujours.
/// </para>
/// <para>
/// ⚠️ <b>Un droit porte un seul canal.</b> L'exclusivité entre l'adresse et le routage n'est pas
/// tenue par ce type — qui, par construction, ne saurait en porter deux — mais par le <see
/// cref="Settings"/>, au seul endroit qui écrit.
/// </para>
/// </remarks>
public abstract record ExerciseChannel
{
  private ExerciseChannel()
  {
  }

  /// <summary>Un droit exercé par un appel HTTP, à l'adresse déclarée par l'intégrateur.</summary>
  /// <param name="Address">L'adresse à appeler.</param>
  public sealed record HttpEndpoint(EndpointUrl Address) : ExerciseChannel;

  /// <summary>Un droit exercé par une publication sur RabbitMQ, selon le routage déclaré.</summary>
  /// <param name="Routing">L'exchange et la routing key.</param>
  public sealed record RabbitMq(RabbitMqRouting Routing) : ExerciseChannel;

  /// <summary>
  /// Un droit <b>« non configuré »</b> : ni adresse, ni routage. État <b>valide et normal</b>,
  /// jamais un manque à combler. ⚠️ Toutes les instances sont égales — c'est un record sans champ —,
  /// et <see cref="Instance"/> évite d'en allouer une par lecture.
  /// </summary>
  public sealed record NotConfigured : ExerciseChannel
  {
    /// <summary>L'unique « non configuré » dont la lecture d'un Paramétrage a besoin.</summary>
    public static readonly NotConfigured Instance = new();
  }
}
