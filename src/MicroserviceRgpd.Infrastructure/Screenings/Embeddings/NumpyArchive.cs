using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace MicroserviceRgpd.Infrastructure.Screenings.Embeddings;

/// <summary>
/// Lit une archive <c>.npz</c> de numpy : un zip de fichiers <c>.npy</c>, chacun un en-tête texte et
/// des <c>float32</c> petit-boutistes.
/// </summary>
/// <remarks>
/// <para>
/// <b>Sans dépendance, et c'est ce qui la justifie.</b> L'artefact a été exporté pour se recharger
/// « avec numpy seul » ; son pendant C# ne mérite pas davantage qu'un paquet tiers pour lire trois
/// tableaux. Elle ne sait donc lire que ce que l'artefact contient — <c>&lt;f4</c>, ordre C — et
/// refuse tout le reste plutôt que de le deviner.
/// </para>
/// </remarks>
internal static partial class NumpyArchive
{
  private static ReadOnlySpan<byte> Magic => [0x93, (byte)'N', (byte)'U', (byte)'M', (byte)'P', (byte)'Y'];

  /// <summary>Les tableaux de l'archive, par nom sans extension, avec leur forme.</summary>
  /// <exception cref="InvalidOperationException">L'archive porte autre chose que des <c>float32</c> petit-boutistes en ordre C.</exception>
  internal static IReadOnlyDictionary<string, NumpyArray> Read(Stream npz, string archiveName)
  {
    using var zip = new ZipArchive(npz, ZipArchiveMode.Read);
    var arrays = new Dictionary<string, NumpyArray>(StringComparer.Ordinal);

    foreach (var entry in zip.Entries.Where(entry => entry.Name.EndsWith(".npy", StringComparison.Ordinal)))
    {
      using var content = entry.Open();
      using var bytes = new MemoryStream();
      content.CopyTo(bytes);

      arrays[entry.Name[..^".npy".Length]] = ArrayOf(bytes.GetBuffer().AsSpan(0, (int)bytes.Length), $"{archiveName}/{entry.Name}");
    }

    return arrays;
  }

  private static NumpyArray ArrayOf(ReadOnlySpan<byte> npy, string name)
  {
    if (npy.Length < 10 || !npy[..6].SequenceEqual(Magic))
    {
      throw Unreadable(name, "l'en-tête numpy est absent");
    }

    var (lengthSize, start) = npy[6] == 1 ? (2, 10) : (4, 12);
    var headerLength = lengthSize == 2
      ? BinaryPrimitives.ReadUInt16LittleEndian(npy[8..])
      : (int)BinaryPrimitives.ReadUInt32LittleEndian(npy[8..]);
    var header = Encoding.ASCII.GetString(npy.Slice(start, headerLength));

    if (DescrPattern().Match(header) is not { Success: true } descr || descr.Groups[1].Value != "<f4")
    {
      throw Unreadable(name, "seuls les float32 petit-boutistes sont lus");
    }

    if (FortranOrderPattern().Match(header) is not { Success: true } order || order.Groups[1].Value != "False")
    {
      throw Unreadable(name, "seul l'ordre C est lu");
    }

    if (ShapePattern().Match(header) is not { Success: true } shapeText)
    {
      throw Unreadable(name, "la forme est absente");
    }

    int[] shape = [.. shapeText.Groups[1].Value
      .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
      .Select(int.Parse)];
    var data = npy[(start + headerLength)..];
    var count = shape.Aggregate(1, (size, dimension) => size * dimension);

    if (data.Length != count * sizeof(float))
    {
      throw Unreadable(name, $"la forme annonce {count} valeurs et les données en portent {data.Length / sizeof(float)}");
    }

    var values = new double[count];

    for (var i = 0; i < count; i++)
    {
      values[i] = BinaryPrimitives.ReadSingleLittleEndian(data[(i * sizeof(float))..]);
    }

    return new NumpyArray(shape, values);
  }

  private static InvalidOperationException Unreadable(string name, string why)
  {
    return new InvalidOperationException($"Le tableau « {name} » de l'artefact A2 est illisible : {why}.");
  }

  [GeneratedRegex(@"'descr':\s*'([^']*)'")]
  private static partial Regex DescrPattern();

  [GeneratedRegex(@"'fortran_order':\s*(True|False)")]
  private static partial Regex FortranOrderPattern();

  [GeneratedRegex(@"'shape':\s*\(([^)]*)\)")]
  private static partial Regex ShapePattern();
}

/// <summary>Un tableau numpy relu : sa forme, et ses valeurs à plat en ordre C.</summary>
internal sealed record NumpyArray(IReadOnlyList<int> Shape, double[] Values);
