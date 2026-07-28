namespace MicroserviceRgpd.Core.Qualifications;

/// <summary>
/// L'avis rendu par un seul moteur sur un <see cref="RightsRequestText"/> : une
/// <see cref="Qualifications.Qualification"/>, accompagnée de la confiance et de la justification
/// <b>lorsque le moteur sait en produire</b>.
/// </summary>
/// <remarks>
/// Un seul type pour les deux moteurs, et non deux : deux types feraient de l'interface de moteur
/// un mensonge. C'est le schéma de chaque point d'entrée du sidecar qui resserre le type — celui du
/// LLM rend la confiance obligatoire, celui du lexique l'interdit.
/// </remarks>
/// <param name="Qualification">Le verdict du moteur, portant déjà ses deux invariants.</param>
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
  Qualification Qualification,
  DeclaredConfidence? DeclaredConfidence = null,
  string? Justification = null);
