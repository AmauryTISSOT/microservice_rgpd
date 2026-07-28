using System.Diagnostics;

namespace MicroserviceRgpd.Infrastructure.Qualifications;

/// <summary>
/// La source de traces des deux appels sortants vers le sidecar.
/// </summary>
/// <remarks>
/// <para>
/// <b>C'est le seul canal qui comptera les échecs.</b> La trace d'audit n'enregistre, par
/// construction, que les verdicts rendus : un moteur muet, une double panne, une annulation n'y
/// laissent rien. Savoir combien de fois le service a échoué est une matière d'exploitation, et
/// elle n'a que cette porte.
/// </para>
/// <para>
/// Une source à nous <b>en plus</b> de l'instrumentation HTTP standard, et non à sa place : les deux
/// moteurs partagent une adresse et ne diffèrent que par leur chemin, si bien qu'une trace de
/// transport seule obligerait à lire une URL pour savoir lequel des deux a échoué. Celle-ci le
/// <b>nomme</b>, et nomme aussi le mode de panne, que le transport ne connaît pas.
/// </para>
/// </remarks>
public static class QualificationTelemetry
{
  /// <summary>Le nom sous lequel la source se déclare à OpenTelemetry, côté hôte.</summary>
  public const string SourceName = "MicroserviceRgpd.Qualifications";

  /// <summary>L'attribut qui porte le point d'entrée interrogé — donc le moteur.</summary>
  internal const string EndpointTag = "qualification.endpoint";

  /// <summary>L'attribut qui porte le mode de panne, absent quand le moteur a rendu un avis.</summary>
  internal const string FailureTag = "qualification.failure";

  /// <summary>Le mode de panne d'un moteur qui n'a pas répondu dans son échéance.</summary>
  internal const string DeadlineFailure = "deadline";

  /// <summary>Le mode de panne d'un moteur qui a répondu autre chose qu'un avis, ou pas du tout.</summary>
  internal const string EngineFailure = "engine";

  /// <summary>La version portée par les traces : celle de l'assemblage, jamais un chiffre à tenir à la main.</summary>
  internal static readonly ActivitySource Source = new(
    SourceName,
    typeof(QualificationTelemetry).Assembly.GetName().Version?.ToString());
}
