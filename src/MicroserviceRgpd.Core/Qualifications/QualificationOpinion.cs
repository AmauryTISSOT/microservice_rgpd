namespace MicroserviceRgpd.Core.Qualifications;

/// <summary>
/// L'avis rendu par un seul moteur sur un <see cref="RightsRequestText"/> : les droits qu'il y
/// reconnaît, accompagnés de la confiance et de la justification <b>lorsqu'il sait en produire</b>.
/// </summary>
/// <remarks>
/// Un seul type pour les deux moteurs, et non deux : deux types feraient de l'interface de moteur
/// un mensonge. C'est le schéma de chaque point d'entrée du sidecar qui resserre le type — celui du
/// LLM rend la confiance obligatoire, celui du lexique l'interdit.
/// </remarks>
/// <param name="Rights">Les droits reconnus, dans le vocabulaire fermé de la taxonomie.</param>
/// <param name="DeclaredConfidence">
/// À quel point le moteur doute de son propre avis. <b>Facultative par le domaine, pas par
/// concession</b> : le lexique n'a aucun avis sur sa propre fiabilité, et une confiance constante
/// serait un mensonge typé — quelqu'un finirait par écrire une règle qui la consomme.
/// </param>
/// <param name="Justification">
/// La phrase que le moteur oppose à l'opérateur humain, quand il sait en produire une. Le lexique
/// n'en rend pas : sa « raison » est une table de scores, qui est du diagnostic, pas de l'aide à
/// la décision.
/// </param>
public sealed record QualificationOpinion(
  IReadOnlyList<DataSubjectRight> Rights,
  DeclaredConfidence? DeclaredConfidence = null,
  string? Justification = null);

/// <summary>
/// L'échelle ordinale à trois degrés par laquelle un moteur dit à quel point il doute de son propre
/// avis. Un moteur peut n'en produire aucune, et le lexique est dans ce cas.
/// </summary>
/// <remarks>
/// Un score numérique est refusé : <c>0,73</c> rendu par un modèle de 8 milliards de paramètres
/// n'est pas une probabilité, c'est un mot choisi qui ressemble à un nombre — et un nombre invite à
/// poser des seuils, donc à régler des seuils sur rien. La règle de corroboration n'en lit qu'un
/// bit — haute ou non —, mais aplatir l'échelle détruirait l'information avant qu'elle n'atteigne
/// la trace d'audit, où la mesure de calibration en aura besoin.
/// </remarks>
public enum DeclaredConfidence
{
  /// <summary>Le moteur doute franchement de son avis.</summary>
  Low,

  /// <summary>Le moteur doute sans trancher.</summary>
  Medium,

  /// <summary>Le seul degré qui autorise <see cref="ReviewSignal.Corroborated"/>.</summary>
  High,
}
