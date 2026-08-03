namespace MicroserviceRgpd.ArchitectureTests;

/// <summary>
/// Les quatre assemblages que les règles de frontière inspectent — les quatre couches que chaque
/// contexte traverse. Ils arrivent à côté de celui-ci par référence de projet, et c'est leur
/// <b>IL compilé</b> qu'on lit, jamais leur code source.
/// </summary>
internal static class ProductionAssembly
{
  /// <summary>
  /// La liste est écrite en toutes lettres plutôt que découverte au répertoire : un assemblage
  /// oublié se lirait sinon comme une règle respectée.
  /// <para>
  /// <c>AspireHost</c> et <c>ServiceDefaults</c> en sont absents sciemment : ils composent la pile
  /// et branchent la télémétrie, ils ne portent aucun contexte. Le jour où l'un d'eux en porterait,
  /// c'est cette liste qu'il faudrait allonger — pas la règle qu'il faudrait assouplir.
  /// </para>
  /// </summary>
  internal static readonly string[] All =
  [
    "MicroserviceRgpd.Core",
    "MicroserviceRgpd.UseCases",
    "MicroserviceRgpd.Infrastructure",
    "MicroserviceRgpd.Web",
  ];

  /// <summary>
  /// Le chemin de l'assemblage dans le répertoire de sortie de ce projet de tests. L'absence est
  /// un échec bruyant : un garde qui ne trouve rien à lire ne doit pas afficher vert.
  /// </summary>
  internal static string PathOf(string name)
  {
    var path = Path.Combine(AppContext.BaseDirectory, name + ".dll");

    File.Exists(path).ShouldBeTrue(
      $"L'assemblage {name} est introuvable : {path}. " +
      "Ajoutez sa référence de projet ici plutôt que de retirer son nom de la liste.");

    return path;
  }
}
