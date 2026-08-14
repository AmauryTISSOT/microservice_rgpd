namespace MicroserviceRgpd.Core.Qualifications;

/// <summary>
/// Les deux rôles que tient un <see cref="IQualificationEngine"/>, et par lesquels le service
/// distingue ses moteurs sans jamais les nommer.
/// </summary>
/// <remarks>
/// <para>
/// Deux moteurs derrière <b>un seul port</b> : quelque chose doit dire lequel est demandé, et ce
/// quelque chose est un <b>rôle</b>. Ce que le rôle nomme est une <b>fonction dans la
/// qualification</b> — rendre le verdict, le contrôler —, jamais une implémentation : l'échanger, ou
/// en écrire un troisième, ne touche ni le domaine ni le use case.
/// </para>
/// <para>
/// ⚠️ <b><c>Lexicon</c> emprunte au lexique le mot qui le nomme, et c'est délibéré.</b> Le rôle de
/// contrôle est décrit partout ailleurs — glossaire, <c>LexiconOpinion</c>, colonnes d'audit — par
/// ce mot-là ; lui en donner un autre ici obligerait chaque lecteur à tenir deux noms pour une seule
/// chose. Le mot est repris <b>pour la lisibilité, pas comme une contrainte</b> : rien n'oblige le
/// moteur qui tient ce rôle à être un lexique, et le jour où ce n'en serait plus un, c'est le nom
/// qu'il faudrait revoir — pas le câblage.
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
  public const string Lexicon = "qualification-engine.lexicon";
}
