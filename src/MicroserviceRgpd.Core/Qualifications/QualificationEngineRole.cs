namespace MicroserviceRgpd.Core.Qualifications;

/// <summary>
/// Les deux rôles que tient un <see cref="IQualificationEngine"/>, et par lesquels le service
/// distingue ses moteurs sans jamais les nommer.
/// </summary>
/// <remarks>
/// <para>
/// Deux moteurs derrière <b>un seul port</b> : quelque chose doit dire lequel est demandé, et ce
/// quelque chose est un <b>rôle</b>, jamais une technologie. Qu'un LLM tienne aujourd'hui le premier
/// et un lexique déterministe le second est un fait d'infrastructure ; l'échanger, ou en écrire un
/// troisième, ne touche ni le domaine ni le use case.
/// </para>
/// <para>
/// Ce sont les clés des services enregistrés : l'infrastructure les enregistre sous ces noms, le use
/// case les demande sous ces noms, et aucun des deux n'écrit la chaîne à la main.
/// </para>
/// </remarks>
public static class QualificationEngineRole
{
  /// <summary>
  /// Le moteur dont l'avis <b>fait verdict</b>, et le seul qui déclare une confiance et justifie.
  /// </summary>
  public const string Verdict = "qualification-engine.verdict";

  /// <summary>
  /// Le moteur qui <b>contrôle</b> le verdict sans y contribuer — sauf en dernier recours, quand le
  /// premier n'a rendu aucun avis.
  /// </summary>
  public const string Witness = "qualification-engine.witness";
}
