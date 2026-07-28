namespace MicroserviceRgpd.Core.Qualifications;

/// <summary>
/// L'échelle ordinale à trois degrés par laquelle un moteur dit à quel point il doute de sa propre
/// <see cref="QualificationOpinion"/>. Un moteur peut n'en produire aucune, et le lexique est dans
/// ce cas.
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
