using System.Text.Json;
using MicroserviceRgpd.Core.Qualifications;

namespace MicroserviceRgpd.UnitTests.Core.Qualifications;

/// <summary>
/// Le garde-fou anti-dérive du contrat interne. La taxonomie existe des deux côtés de la frontière
/// .NET / Python, et une divergence serait silencieuse : le sidecar lit le fichier de projection,
/// ce test confronte ce fichier au domaine. Aucun réseau, aucune base, aucun container —
/// lire un fichier et comparer sept chaînes.
/// <para>
/// Si ce test échoue, le domaine a raison : c'est le fichier qui suit, jamais l'inverse.
/// </para>
/// </summary>
public class WireTaxonomyProjectionTests
{
  /// <summary>Le fichier de projection, versionné à la racine du dépôt, lu par le sidecar Python.</summary>
  private const string FileName = "data-subject-rights.wire.json";

  private static readonly string[] DomainNames = [.. DataSubjectRight.List.Select(right => right.Name)];

  [Fact]
  public void ProjectsEveryMemberOfTheDomainTaxonomy()
  {
    var missing = DomainNames.Except(ReadProjectedNames()).ToArray();

    missing.ShouldBeEmpty(
      $"{FileName} ne projette pas : {string.Join(", ", missing)}. " +
      "Le domaine commande : reportez ces noms dans le fichier.");
  }

  [Fact]
  public void ProjectsNothingTheDomainTaxonomyDoesNotKnow()
  {
    var unknown = ReadProjectedNames().Except(DomainNames).ToArray();

    unknown.ShouldBeEmpty(
      $"{FileName} projette des noms absents du domaine : {string.Join(", ", unknown)}. " +
      "Ne faites pas taire ce test en éditant le fichier : corrigez le domaine, ou retirez ces noms.");
  }

  [Fact]
  public void ProjectsEachNameExactlyOnce()
  {
    ReadProjectedNames().Length.ShouldBe(DomainNames.Length);
  }

  /// <summary>
  /// Sans le commentaire d'autorité, quelqu'un finira par faire taire les tests ci-dessus
  /// en éditant le fichier plutôt que le domaine.
  /// </summary>
  [Fact]
  public void CarriesAHeaderCommentSayingWhichSideCommands()
  {
    var comment = string.Join(
      Environment.NewLine,
      ReadProjection().GetProperty("_comment").EnumerateArray().Select(line => line.GetString()));

    comment.ShouldNotBeNullOrWhiteSpace();
    comment.ShouldContain("DataSubjectRight");
  }

  private static string[] ReadProjectedNames() =>
    [.. ReadProjection().GetProperty("rights").EnumerateArray().Select(name => name.GetString()!)];

  private static JsonElement ReadProjection()
  {
    var path = Path.Combine(RepositoryRoot(), FileName);

    File.Exists(path).ShouldBeTrue($"Le fichier de projection est introuvable : {path}");

    return JsonDocument.Parse(File.ReadAllText(path)).RootElement;
  }

  /// <summary>
  /// La racine se déduit du chemin de compilation de ce fichier, non du répertoire de sortie :
  /// le test reste juste quel que soit l'endroit d'où <c>dotnet test</c> est lancé.
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
