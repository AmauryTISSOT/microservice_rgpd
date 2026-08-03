using MicroserviceRgpd.Infrastructure.Qualifications;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Qualifications;

/// <summary>
/// Le garde-fou anti-dérive entre l'AppHost et le service qu'il configure. Les échéances des deux
/// clients de moteur sont posées par variable d'environnement depuis l'AppHost, et lues ici par leur
/// nom de clé : renommer d'un côté sans l'autre n'échouerait à la compilation nulle part, et le
/// service repartirait sur la valeur du fichier versionné — ou refuserait de démarrer — sans que
/// rien ne dise pourquoi.
/// <para>
/// Aucun hôte n'est démarré : démarrer l'AppHost démarrerait le sidecar et Ollama, exigerait un GPU,
/// et ne tournerait donc jamais. Ce test lit un fichier et cherche deux chaînes.
/// </para>
/// <para>
/// S'il échoue, c'est le service qui commande : reportez le nom de clé dans l'AppHost.
/// </para>
/// </summary>
public class AppHostQualificationWiringTests
{
  /// <summary>La ligne de l'AppHost, versionnée : c'est le seul endroit où ces noms sont écrits.</summary>
  private const string AppHostFile = "src/MicroserviceRgpd.AspireHost/AppHost.cs";

  [Fact]
  public void PostsTheLlmDeadlineUnderTheKeyTheServiceReadsIt()
  {
    ReadAppHost().ShouldContain(
      EnvironmentVariable(QualificationEngineServiceExtensions.LlmDeadlineKey),
      customMessage: $"{AppHostFile} ne pose plus l'échéance du moteur LLM sous le nom que le service lit. " +
      "Le service commande : reportez ce nom dans l'AppHost.");
  }

  [Fact]
  public void PostsTheLexiconDeadlineUnderTheKeyTheServiceReadsIt()
  {
    ReadAppHost().ShouldContain(
      EnvironmentVariable(QualificationEngineServiceExtensions.LexiconDeadlineKey),
      customMessage: $"{AppHostFile} ne pose plus l'échéance du moteur lexical sous le nom que le service lit. " +
      "Le service commande : reportez ce nom dans l'AppHost.");
  }

  /// <summary>
  /// Le nom d'une clé de configuration tel qu'une variable d'environnement l'écrit : le séparateur
  /// de section devient un double tiret bas, seule forme que les deux-points ne franchissent pas.
  /// </summary>
  private static string EnvironmentVariable(string key) => key.Replace(":", "__", StringComparison.Ordinal);

  private static string ReadAppHost()
  {
    var path = Path.Combine(RepositoryRoot(), AppHostFile);

    File.Exists(path).ShouldBeTrue($"L'AppHost est introuvable : {path}");

    return File.ReadAllText(path);
  }

  /// <summary>
  /// La racine se déduit du chemin de compilation de ce fichier, non du répertoire de sortie : le
  /// test reste juste quel que soit l'endroit d'où <c>dotnet test</c> est lancé.
  /// </summary>
  private static string RepositoryRoot([CallerFilePath] string thisFile = "")
  {
    var directory = new DirectoryInfo(Path.GetDirectoryName(thisFile)!);

    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MicroserviceRgpd.slnx")))
    {
      directory = directory.Parent;
    }

    directory.ShouldNotBeNull("Racine du dépôt introuvable depuis " + thisFile);

    return directory.FullName;
  }
}
