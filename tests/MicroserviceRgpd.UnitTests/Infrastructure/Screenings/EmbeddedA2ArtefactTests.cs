using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using MicroserviceRgpd.Infrastructure.Screenings;
using Microsoft.Extensions.DependencyInjection;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Screenings;

/// <summary>
/// L'artefact A2 embarqué, <b>ancré</b> : les fichiers sont, octet pour octet, ceux de
/// <c>recherche_schema_v2/models/a2_c1_logreg</c> au commit de provenance <c>f0a2654</c> — et un
/// artefact abîmé arrête le démarrage.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Le modèle ne vaut que tel qu'il a été mesuré.</b> Un poids retouché, un seuil arrondi, un
/// prototype renommé : le service détecterait avec un modèle que personne n'a évalué, et le rapport
/// porterait pourtant l'identité de celui qui l'a été.
/// </para>
/// <para>
/// <b>Une empreinte plutôt qu'une comparaison avec le dépôt de recherche</b>, pour la raison que
/// <see cref="FrozenLexiconsTests"/> donne déjà : un test qui remonterait à la source passerait au
/// vert le jour où les deux copies auraient été éditées ensemble.
/// </para>
/// </remarks>
public class EmbeddedA2ArtefactTests
{
  private const string ResourcePrefix = "MicroserviceRgpd.Infrastructure.Screenings.Embeddings.Artefact.";

  /// <summary>Les quatre fichiers embarqués sont ceux de l'artefact, et rien d'autre.</summary>
  [Theory]
  [InlineData("manifest.json", "ce29884fbb4626b7fbdad93f18c11b95945c6cbb43205f65e6ad3d91c42a066f")]
  [InlineData("weights.npz", "74ebdb2ed9f873fe9916cc8351964789df0c172aaf42c578f6e5fad11bff428e")]
  [InlineData("prototypes.npz", "1acf96b7b7ecbdcbad1c0cc01cd7b3fae536e61b9bd0681bd222590ef6f014a0")]
  [InlineData("README.md", "d5fdf43ac7e626f2fe7129ad847b66893c2dd56256f57674a5aa79d080f8415b")]
  public void CarriesTheArtefactExactlyAsItWasExported(string file, string atExport)
  {
    using var stream = Embedded(file);

    Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant().ShouldBe(
      atExport,
      $"Le fichier « {file} » de l'artefact A2 diffère de l'export f0a2654. Le service détecterait "
      + "avec un modèle que personne n'a mesuré, sous l'identité de celui qui l'a été.");
  }

  /// <summary>L'artefact intact démarre : c'est le témoin des refus qui suivent.</summary>
  [Fact]
  public void StartsOnTheIntactArtefact()
  {
    Should.NotThrow(() => Registered(Embedded));
  }

  /// <summary>
  /// ⚠️ <b>Une forme fausse arrête le démarrage</b>, jamais le premier dépôt : un produit scalaire
  /// sur 1023 composantes rendrait des scores sans valeur, sans rien lever.
  /// </summary>
  [Theory]
  [InlineData("weights.npz", "coef", new[] { 1023 })]
  [InlineData("weights.npz", "intercept", new[] { 2 })]
  [InlineData("prototypes.npz", "vectors", new[] { 13, 1024 })]
  [InlineData("prototypes.npz", "vectors", new[] { 14, 1023 })]
  public void RefusesToStartOnAnArrayOfTheWrongShape(string file, string array, int[] shape)
  {
    Should.Throw<InvalidOperationException>(() => Registered(name => name == file
      ? Npz(array, shape)
      : Embedded(name)));
  }

  /// <summary>
  /// ⚠️ <b>Un nom de prototype que la taxonomie ne connaît pas arrête le démarrage.</b> La
  /// correspondance est écrite une seule fois, sur <c>PersonalDataCategory</c> : un prototype sans
  /// valeur rendrait une catégorie que personne ne sait nommer.
  /// </summary>
  [Fact]
  public void RefusesToStartOnAPrototypeTheTaxonomyDoesNotKnow()
  {
    Should.Throw<InvalidOperationException>(() => Registered(name => name == "manifest.json"
      ? Rewritten(name, "\"person_link\"", "\"pet_name\"")
      : Embedded(name)));
  }

  /// <summary>Un gabarit qui ne cite pas les deux noms ne sérialise pas ce que le modèle a lu.</summary>
  [Fact]
  public void RefusesToStartOnATemplateThatDoesNotNameTheTableAndTheColumn()
  {
    Should.Throw<InvalidOperationException>(() => Registered(name => name == "manifest.json"
      ? Rewritten(name, "table: {table_name} | column: {column_name}", "column: {column_name}")
      : Embedded(name)));
  }

  private static void Registered(Func<string, Stream> artefact)
  {
    new ServiceCollection().AddScreeningEngine(AScreeningEngine.Configuration(AScreeningEngine.A2On()), artefact);
  }

  private static Stream Embedded(string file)
  {
    return typeof(ScreeningEngineServiceExtensions).Assembly.GetManifestResourceStream(ResourcePrefix + file)
      ?? throw new InvalidOperationException($"Le fichier « {file} » de l'artefact A2 n'est pas embarqué.");
  }

  private static Stream Rewritten(string file, string from, string to)
  {
    using var reader = new StreamReader(Embedded(file), Encoding.UTF8);
    var text = reader.ReadToEnd();

    text.ShouldContain(from);

    return new MemoryStream(Encoding.UTF8.GetBytes(text.Replace(from, to, StringComparison.Ordinal)));
  }

  /// <summary>
  /// Une archive <c>.npz</c> où <b>ce seul tableau</b> prend cette forme, les autres gardant la leur
  /// — tous remplis de zéros. Un tableau manquant serait un autre refus, et le test en prouverait un
  /// qu'il ne nomme pas.
  /// </summary>
  private static MemoryStream Npz(string array, int[] shape)
  {
    var shapes = new Dictionary<string, int[]>
    {
      ["coef"] = [1024],
      ["intercept"] = [1],
      ["vectors"] = [14, 1024],
    };
    var archive = array == "vectors" ? ["vectors"] : new[] { "coef", "intercept" };
    shapes[array] = shape;

    var buffer = new MemoryStream();

    using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
    {
      foreach (var name in archive)
      {
        using var entry = zip.CreateEntry($"{name}.npy").Open();
        var dimensions = shapes[name].Length == 1 ? $"{shapes[name][0]}," : string.Join(", ", shapes[name]);
        var header = $"{{'descr': '<f4', 'fortran_order': False, 'shape': ({dimensions}), }}";
        header = header.PadRight(((header.Length + 11) / 64 + 1) * 64 - 11) + "\n";

        entry.Write([0x93, .. "NUMPY"u8, 1, 0]);
        entry.Write(BitConverter.GetBytes((ushort)header.Length));
        entry.Write(Encoding.ASCII.GetBytes(header));
        entry.Write(new byte[shapes[name].Aggregate(4, (size, dimension) => size * dimension)]);
      }
    }

    buffer.Position = 0;

    return buffer;
  }
}
