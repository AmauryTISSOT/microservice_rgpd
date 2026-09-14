using System.Net.Http.Json;
using System.Text.Json;
using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.Infrastructure.Screenings.Embeddings;

/// <summary>
/// Le moteur de détection <b>A2</b> : plongements <c>bge-m3</c> servis par Ollama, régression
/// logistique et prototypes de l'artefact figé (ADR-0025).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Il ne lit que le nom de la table et celui de la colonne</b>, au gabarit du manifest. Ni
/// schéma, ni type, ni commentaire, ni aperçu : le modèle n'a été ni entraîné ni mesuré sur ces
/// champs. Les aperçus du chemin scanné sont reçus et ignorés — un relevé collé et un relevé scanné
/// rendent donc le même rapport.
/// </para>
/// <para>
/// ⚠️ <b>Le score ne sort pas d'ici.</b> Il décide du signalement et meurt : ni la ligne, ni le motif,
/// ni l'identité ne le portent. Non calibré (ECE 0,159), il se lirait comme une probabilité.
/// </para>
/// <para>
/// <b>Il vérifie l'encodeur avant chaque détection.</b> Un autre <c>bge-m3</c> servi sous le même
/// tag produirait des vecteurs sur lesquels la régression n'a jamais été ajustée : un rapport sans
/// valeur, que rien ne distinguerait du bon.
/// </para>
/// </remarks>
internal sealed class A2ScreeningEngine(IHttpClientFactory clients, A2Artefact artefact) : IScreeningEngine
{
  /// <summary>Le nom sous lequel ce moteur se déclare.</summary>
  internal const string EngineName = "a2-bge-m3-logreg";

  /// <summary>
  /// Combien de textes part dans un appel à l'encodeur — la taille de lot de <c>a2_model.py</c>. Un
  /// relevé de vingt mille colonnes en un seul appel serait un corps démesuré et une échéance
  /// impossible à tenir.
  /// </summary>
  private const int BatchSize = 64;

  /// <summary>
  /// Combien de caractères d'une empreinte la version garde — ce qu'<c>ollama list</c> affiche d'un
  /// digest. Assez pour distinguer deux artefacts, assez court pour se lire dans un bandeau.
  /// </summary>
  private const int ShortLength = 12;

  private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);

  /// <inheritdoc />
  public async Task<ScreenedListing> ScreenAsync(
    ColumnListing listing,
    IReadOnlyDictionary<ColumnIdentity, ColumnPreview> previews,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(listing);
    ArgumentNullException.ThrowIfNull(previews);

    var client = clients.CreateClient(ScreeningEngineServiceExtensions.OllamaClientName);
    var digest = await ServedDigestAsync(client, cancellationToken);

    var texts = listing.Columns
      .Select(column => artefact.Serialize(column.Identity.Table, column.Identity.Column))
      .ToList();
    var vectors = await EmbedAsync(client, [.. texts.Distinct(StringComparer.Ordinal)], cancellationToken);

    var screened = listing.Columns
      .Zip(texts, (column, text) => Screen(column, vectors[text]))
      .ToList();

    return new ScreenedListing(
      new ScreeningEngineIdentity(
        EngineName,
        $"modele-{artefact.ManifestSha256[..ShortLength]}+encodeur-{digest[..ShortLength]}"),
      screened);
  }

  /// <summary>Le digest qu'Ollama déclare servir sous le tag du manifest — s'il est celui du manifest.</summary>
  private async Task<string> ServedDigestAsync(HttpClient client, CancellationToken cancellationToken)
  {
    var tags = await client.GetFromJsonAsync<TagsResponse>("api/tags", Wire, cancellationToken);
    var served = tags?.Models?.FirstOrDefault(model => model.Name == artefact.EncoderTag)?.Digest;

    if (served != artefact.EncoderDigest)
    {
      throw new InvalidOperationException(
        $"L'encodeur servi sous « {artefact.EncoderTag} » n'est pas celui sur lequel le modèle A2 a été ajusté.");
    }

    return served;
  }

  /// <summary>Encode les textes par lots, et rend un vecteur <b>normalisé L2</b> par texte.</summary>
  private async Task<Dictionary<string, double[]>> EmbedAsync(
    HttpClient client,
    IReadOnlyList<string> texts,
    CancellationToken cancellationToken)
  {
    var vectors = new Dictionary<string, double[]>(texts.Count, StringComparer.Ordinal);

    foreach (var batch in texts.Chunk(BatchSize))
    {
      using var response = await client.PostAsJsonAsync(
        "api/embed", new EmbedRequest(artefact.EncoderTag, batch), Wire, cancellationToken);
      response.EnsureSuccessStatusCode();

      var embedded = await response.Content.ReadFromJsonAsync<EmbedResponse>(Wire, cancellationToken);

      if (embedded?.Embeddings is not { } embeddings || embeddings.Count != batch.Length)
      {
        throw new InvalidOperationException(
          $"L'encodeur a rendu {embedded?.Embeddings?.Count ?? 0} vecteurs pour {batch.Length} textes.");
      }

      foreach (var (text, vector) in batch.Zip(embeddings))
      {
        if (vector.Length != artefact.Coefficients.Length)
        {
          throw new InvalidOperationException(
            $"L'encodeur a rendu un vecteur de dimension {vector.Length}, et le modèle A2 en attend {artefact.Coefficients.Length}.");
        }

        vectors[text] = Normalised(vector);
      }
    }

    return vectors;
  }

  /// <summary>Ce qu'A2 dit d'<b>une</b> colonne : rien vu, ou le prototype le plus proche.</summary>
  private ScreenedColumn Screen(ListedColumn column, double[] vector)
  {
    if (Score(vector) < artefact.Threshold)
    {
      return ScreenedColumn.NothingSeen(column);
    }

    var category = Nearest(vector);

    return ScreenedColumn.Flagged(
      column,
      category,
      RuleStrength.PrototypeProximity,
      $"le nom « {column.Identity.Table}.{column.Identity.Column} » est proche du prototype "
      + $"« {category.PrototypeFrenchText} » : {category.FrenchLabel}");
  }

  /// <summary><c>sigmoid(v · coef + intercept)</c>, écrite pour ne jamais déborder.</summary>
  private double Score(double[] vector)
  {
    var logit = Dot(vector, artefact.Coefficients) + artefact.Intercept;

    return logit >= 0
      ? 1 / (1 + Math.Exp(-logit))
      : Math.Exp(logit) / (1 + Math.Exp(logit));
  }

  /// <summary>
  /// La valeur du prototype de similarité cosinus maximale — le premier en cas d'égalité, comme
  /// <c>argmax</c>. Les prototypes et le vecteur étant unitaires, le produit scalaire suffit.
  /// </summary>
  private PersonalDataCategory Nearest(double[] vector)
  {
    return artefact.Prototypes.MaxBy(prototype => Dot(vector, prototype.Vector))!.Category;
  }

  private static double Dot(double[] left, double[] right)
  {
    var sum = 0.0;

    for (var i = 0; i < left.Length; i++)
    {
      sum += left[i] * right[i];
    }

    return sum;
  }

  /// <summary>Le vecteur ramené à la norme 1 — laissé tel quel s'il est nul, comme <c>a2_model.py</c>.</summary>
  private static double[] Normalised(double[] vector)
  {
    var norm = Math.Sqrt(Dot(vector, vector));

    return norm == 0 ? vector : [.. vector.Select(component => component / norm)];
  }

  private sealed record EmbedRequest(string Model, IReadOnlyList<string> Input);

  private sealed record EmbedResponse(IReadOnlyList<double[]>? Embeddings);

  private sealed record TagsResponse(IReadOnlyList<ServedModel>? Models);

  private sealed record ServedModel(string? Name, string? Digest);
}
