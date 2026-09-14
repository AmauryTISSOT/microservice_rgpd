using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.Infrastructure.Screenings.Embeddings;

/// <summary>
/// L'artefact figé du modèle A2 — <c>a2_c1_logreg</c>, commit de provenance <c>f0a2654</c> —, chargé
/// depuis les ressources embarquées et <b>vérifié au démarrage</b>.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Le manifest est l'unique source</b> du seuil, du tag et du digest de l'encodeur, du gabarit
/// de mise en texte, de la dimension, des noms et des textes de prototypes. Aucun n'a de réglage :
/// une configuration pourrait les désaccorder du modèle mesuré, et le rapport porterait l'identité
/// d'un modèle qui n'a pas détecté (ADR-0025, point 4).
/// </para>
/// <para>
/// ⚠️ <b>Tout désaccord arrête le démarrage</b> — une forme fausse, un prototype que la taxonomie ne
/// connaît pas, un gabarit qui ne nomme pas la table et la colonne. Le premier dépôt n'est jamais
/// l'endroit où découvrir qu'un artefact est abîmé : il rendrait un rapport sans valeur, sans rien
/// lever.
/// </para>
/// </remarks>
internal sealed partial class A2Artefact
{
  private const string ResourcePrefix = "MicroserviceRgpd.Infrastructure.Screenings.Embeddings.Artefact.";

  private const string TableSlot = "{table_name}";

  private const string ColumnSlot = "{column_name}";

  private A2Artefact(
    string manifestSha256,
    double threshold,
    string encoderTag,
    string encoderDigest,
    string template,
    double[] coefficients,
    double intercept,
    IReadOnlyList<Prototype> prototypes)
  {
    ManifestSha256 = manifestSha256;
    Threshold = threshold;
    EncoderTag = encoderTag;
    EncoderDigest = encoderDigest;
    Template = template;
    Coefficients = coefficients;
    Intercept = intercept;
    Prototypes = prototypes;
  }

  /// <summary>L'empreinte SHA-256 du manifest, en hexadécimal minuscule : c'est elle qui nomme l'artefact.</summary>
  internal string ManifestSha256 { get; }

  /// <summary>Le seuil publié : une colonne est signalée si son score l'atteint.</summary>
  internal double Threshold { get; }

  /// <summary>Le tag sous lequel Ollama sert l'encodeur.</summary>
  internal string EncoderTag { get; }

  /// <summary>Le digest de l'encodeur sur lequel la régression a été ajustée.</summary>
  internal string EncoderDigest { get; }

  /// <summary>Le gabarit de mise en texte, tel que le manifest l'écrit.</summary>
  internal string Template { get; }

  /// <summary>Les coefficients de la régression, un par composante du vecteur.</summary>
  internal double[] Coefficients { get; }

  /// <summary>L'ordonnée à l'origine de la régression.</summary>
  internal double Intercept { get; }

  /// <summary>Les prototypes, dans l'ordre du manifest, chacun résolu vers sa valeur de la taxonomie.</summary>
  internal IReadOnlyList<Prototype> Prototypes { get; }

  /// <summary>Le fichier de l'artefact tel qu'il est embarqué dans l'assemblage.</summary>
  /// <exception cref="InvalidOperationException">Le fichier n'est pas embarqué.</exception>
  internal static Stream OpenEmbedded(string file)
  {
    return typeof(A2Artefact).Assembly.GetManifestResourceStream(ResourcePrefix + file)
      ?? throw new InvalidOperationException($"Le fichier « {file} » de l'artefact A2 n'est pas embarqué dans l'assemblage.");
  }

  /// <summary>Charge et vérifie l'artefact, fichier par fichier.</summary>
  /// <param name="open">Ouvre un fichier de l'artefact par son nom — <c>manifest.json</c>, <c>weights.npz</c>, <c>prototypes.npz</c>.</param>
  /// <exception cref="InvalidOperationException">L'artefact est illisible, ou ses pièces ne s'accordent pas.</exception>
  internal static A2Artefact Load(Func<string, Stream> open)
  {
    byte[] manifestBytes;

    using (var manifestFile = open("manifest.json"))
    using (var buffer = new MemoryStream())
    {
      manifestFile.CopyTo(buffer);
      manifestBytes = buffer.ToArray();
    }

    var manifest = Manifest.Parse(manifestBytes);

    if (!manifest.Template.Contains(TableSlot, StringComparison.Ordinal)
      || !manifest.Template.Contains(ColumnSlot, StringComparison.Ordinal))
    {
      throw Discordant($"le gabarit « {manifest.Template} » ne nomme pas {TableSlot} et {ColumnSlot}");
    }

    if (manifest.PrototypeNames.Count != manifest.PrototypeTexts.Count)
    {
      throw Discordant($"{manifest.PrototypeNames.Count} noms de prototypes pour {manifest.PrototypeTexts.Count} textes");
    }

    using var weightsFile = open("weights.npz");
    var weights = NumpyArchive.Read(weightsFile, "weights.npz");
    var coef = Shaped(weights, "coef", manifest.Dimension);
    var intercept = Shaped(weights, "intercept", 1);

    using var prototypesFile = open("prototypes.npz");
    var vectors = Shaped(NumpyArchive.Read(prototypesFile, "prototypes.npz"), "vectors", manifest.PrototypeNames.Count, manifest.Dimension);

    var prototypes = manifest.PrototypeNames
      .Select((name, row) => new Prototype(
        CategoryOf(name),
        vectors.Values.AsSpan(row * manifest.Dimension, manifest.Dimension).ToArray()))
      .ToList();

    if (prototypes.Select(prototype => prototype.Category).Distinct().Count() != prototypes.Count)
    {
      throw Discordant("deux prototypes portent le même nom");
    }

    return new A2Artefact(
      Convert.ToHexStringLower(SHA256.HashData(manifestBytes)),
      manifest.Threshold,
      manifest.EncoderTag,
      manifest.EncoderDigest,
      manifest.Template,
      coef.Values,
      intercept.Values[0],
      prototypes);
  }

  /// <summary>
  /// Le texte que le modèle lit pour une colonne : le gabarit du manifest, où ne se substituent que le
  /// nom de la table et celui de la colonne.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>En une seule passe.</b> Deux remplacements successifs substitueraient une seconde fois une
  /// table qui s'appellerait littéralement <c>{column_name}</c>.
  /// </remarks>
  internal string Serialize(string table, string column)
  {
    return SlotPattern().Replace(Template, slot => slot.Value == TableSlot ? table : column);
  }

  /// <summary>La valeur de la taxonomie que ce prototype désigne — écrite une seule fois, sur la valeur.</summary>
  private static PersonalDataCategory CategoryOf(string prototypeName)
  {
    return PersonalDataCategory.List.SingleOrDefault(category => category.PrototypeName == prototypeName)
      ?? throw Discordant($"le prototype « {prototypeName} » n'a pas de valeur dans la taxonomie");
  }

  private static NumpyArray Shaped(IReadOnlyDictionary<string, NumpyArray> archive, string name, params int[] shape)
  {
    if (!archive.TryGetValue(name, out var array))
    {
      throw Discordant($"le tableau « {name} » est absent");
    }

    if (!array.Shape.SequenceEqual(shape))
    {
      throw Discordant($"le tableau « {name} » a la forme ({string.Join(", ", array.Shape)}), "
        + $"et le manifest attend ({string.Join(", ", shape)})");
    }

    return array;
  }

  private static InvalidOperationException Discordant(string why)
  {
    return new InvalidOperationException(
      $"L'artefact A2 embarqué est incohérent : {why}. Le service ne détecte pas avec un modèle que "
      + "personne n'a mesuré.");
  }

  [GeneratedRegex(@"\{table_name\}|\{column_name\}")]
  private static partial Regex SlotPattern();

  /// <summary>Un prototype de l'artefact : la valeur qu'il désigne, et son vecteur unitaire.</summary>
  internal sealed record Prototype(PersonalDataCategory Category, double[] Vector);

  /// <summary>Ce que le service lit du manifest — et rien d'autre.</summary>
  private sealed record Manifest(
    double Threshold,
    string EncoderTag,
    string EncoderDigest,
    int Dimension,
    string Template,
    IReadOnlyList<string> PrototypeNames,
    IReadOnlyList<string> PrototypeTexts)
  {
    internal static Manifest Parse(byte[] json)
    {
      try
      {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var encoder = root.GetProperty("encoder");

        return new Manifest(
          root.GetProperty("threshold").GetDouble(),
          Text(encoder.GetProperty("tag")),
          Text(encoder.GetProperty("digest")),
          encoder.GetProperty("dimension").GetInt32(),
          Text(root.GetProperty("serialization").GetProperty("template")),
          [.. root.GetProperty("prototype_names").EnumerateArray().Select(Text)],
          [.. root.GetProperty("prototype_texts").EnumerateArray().Select(Text)]);
      }
      catch (Exception exception) when (exception is JsonException or KeyNotFoundException or FormatException
        or InvalidOperationException)
      {
        throw new InvalidOperationException($"Le manifest de l'artefact A2 embarqué est illisible : {exception.Message}", exception);
      }
    }

    private static string Text(JsonElement element)
    {
      var text = element.GetString();

      return string.IsNullOrWhiteSpace(text) ? throw new FormatException("un texte attendu est vide") : text;
    }
  }
}
