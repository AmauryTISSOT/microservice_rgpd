namespace MicroserviceRgpd.ArchitectureTests;

/// <summary>
/// <b>Les noms que le garde emploie désignent des contextes qui existent.</b> Sans ce contrôle,
/// <see cref="ContextIsolationTests"/> porte une panne muette : l'appartenance d'un type à un
/// contexte se lit à son espace de noms <b>par préfixe</b> — parce que le dossier dit
/// <c>Qualifications</c> là où le contexte se nomme <c>Qualification</c>. Un nom mal écrit, un
/// pluriel de trop, et la règle ne trouve <b>jamais</b> rien : le vert d'un contexte pas encore
/// écrit et le vert d'un garde qui ment sont exactement le même vert.
/// <para>
/// L'ancre est <c>docs/contexts/</c>, parce que c'est ce qui existe <i>déjà</i> quand on pose le
/// garde — <c>src/</c> est découpé par couche et ne portera de dossier qu'au premier code écrit,
/// c'est-à-dire trop tard. Elle attrape deux fautes : le nom erroné le jour où on l'écrit, et le
/// quatrième contexte dont le glossaire arriverait sans sa ligne dans la matrice.
/// </para>
/// <para>
/// ⚠️ Ce qu'elle n'attrape pas, et qui reste écrit dans <c>docs/adr/0003</c> comme risque assumé :
/// elle ancre le <b>nom</b>, elle ne promet pas que le dossier de code portera celui-là. La première
/// PR qui écrit du <c>Screening</c> est le premier moment où la règle mord vraiment.
/// </para>
/// </summary>
public class ContextRosterTests
{
  private const string Glossaries = "docs/contexts";

  /// <summary>
  /// ⚠️ <c>SharedKernel</c> est la seule exception, et son motif est écrit ici plutôt que laissé à
  /// deviner : <b>ce n'est pas un contexte</b>, c'est ce que des contextes partagent. Il n'a pas de
  /// glossaire et n'en aura pas — le sien serait la liste du législateur.
  /// </summary>
  private static readonly string[] BoundedContexts =
  [
    .. ContextInspector.All
      .Where(name => name != ContextInspector.SharedKernel)
      .Select(name => name.ToLowerInvariant())
      .Order(StringComparer.Ordinal),
  ];

  /// <summary>
  /// Les contextes que le dépôt documente, lus au répertoire. Ici la découverte est le but : c'est
  /// la liste écrite en dur qu'on vient confronter au réel, et non l'inverse.
  /// </summary>
  private static string[] DocumentedContexts()
  {
    return
    [
      .. Directory.EnumerateDirectories(Path.Combine(RepositoryRoot(), Glossaries))
        .Where(directory => File.Exists(Path.Combine(directory, "CONTEXT.md")))
        .Select(directory => new DirectoryInfo(directory).Name)
        .Order(StringComparer.Ordinal),
    ];
  }

  /// <summary>
  /// La racine du dépôt, trouvée en remontant depuis le répertoire de sortie. L'absence est un échec
  /// bruyant, pour le motif de <see cref="ProductionAssembly.PathOf"/> : un garde qui ne trouve rien
  /// à lire ne doit pas afficher vert.
  /// </summary>
  private static string RepositoryRoot()
  {
    var directory = new DirectoryInfo(AppContext.BaseDirectory);

    while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, Glossaries)))
    {
      directory = directory.Parent;
    }

    directory.ShouldNotBeNull(
      $"Aucun {Glossaries}/ trouvé en remontant depuis {AppContext.BaseDirectory}. " +
      "L'ancre du garde est introuvable : réparez le chemin plutôt que de retirer ce test.");

    return directory.FullName;
  }

  [Fact]
  public void NamesExactlyTheContextsThatHaveAGlossary()
  {
    BoundedContexts.ShouldBe(
      DocumentedContexts(),
      "La liste des contextes du garde et les glossaires de " + Glossaries + "/ ont divergé. " +
      "Un nom que le garde emploie sans qu'aucun dossier ne le porte rend ses règles vertes pour " +
      "toujours ; un contexte documenté absent de la liste n'est gardé par rien. " +
      "Voir docs/adr/0003.");
  }
}
