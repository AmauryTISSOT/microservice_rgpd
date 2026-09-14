using System.Text.Json;

namespace MicroserviceRgpd.TestDoubles.Ollama;

/// <summary>
/// Le jeu figé du contrôle d'équivalence : des colonnes, le vecteur que le double d'Ollama rend pour
/// chacune, et ce que <c>a2_model.py</c> en a décidé — score, décision, prototype le plus proche.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Les attendus sortent de Python, jamais du C#.</b> Le fichier est produit par
/// <c>a2_equivalence.py</c>, qui appelle <c>a2_model.py</c> sur l'artefact embarqué ; les recopier
/// depuis la sortie du moteur ferait de ce contrôle une tautologie.
/// </para>
/// <para>
/// ⚠️ <b>Les vecteurs sont fabriqués, pas encodés</b>, et c'est suffisant pour ce qu'on éprouve : ce
/// que le service fait d'un vecteur — normalisation, régression, seuil, prototype. Deux colonnes y
/// sont posées à ±1e-5 du seuil, si bien qu'un score qui s'écarterait davantage de celui de Python
/// rendrait l'une des deux du mauvais côté.
/// </para>
/// </remarks>
public static class A2Equivalence
{
  private static readonly Fixture Frozen = Load();

  /// <summary>Le seuil du manifest sur lequel Python a décidé.</summary>
  public static double Threshold => Frozen.Threshold;

  /// <summary>Le tag de l'encodeur que le manifest attend.</summary>
  public static string EncoderTag => Frozen.Encoder.Tag;

  /// <summary>Le digest de l'encodeur que le manifest attend, et que le double déclare servir.</summary>
  public static string EncoderDigest => Frozen.Encoder.Digest;

  /// <summary>L'empreinte SHA-256 du manifest sur lequel Python a calculé les attendus.</summary>
  public static string ManifestSha256 => Frozen.ManifestSha256;

  /// <summary>Les colonnes du jeu, avec ce que Python en a décidé.</summary>
  public static IReadOnlyList<FrozenColumn> Columns => Frozen.Columns;

  /// <summary>Le vecteur que le double rend pour ce texte — celui du jeu, ou le vecteur par défaut.</summary>
  public static IReadOnlyList<double> VectorOf(string text)
  {
    return Frozen.Columns.FirstOrDefault(column => column.Text == text)?.Vector ?? Frozen.Default.Vector;
  }

  private static Fixture Load()
  {
    using var stream = typeof(A2Equivalence).Assembly.GetManifestResourceStream("a2-equivalence.json")
      ?? throw new InvalidOperationException("Le jeu figé a2-equivalence.json n'est pas embarqué.");

    return JsonSerializer.Deserialize<Fixture>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
      PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    })!;
  }

  /// <summary>Une colonne du jeu.</summary>
  /// <param name="Table">Le nom de la table.</param>
  /// <param name="Column">Le nom de la colonne.</param>
  /// <param name="Text">Le texte au gabarit du manifest, tel que Python l'a sérialisé.</param>
  /// <param name="Score">Le score que Python a calculé — un attendu de test, jamais une sortie du service.</param>
  /// <param name="IsPersonal">La décision que Python a rendue.</param>
  /// <param name="Category">Le nom du prototype le plus proche, tel que le manifest l'écrit.</param>
  /// <param name="Vector">Le vecteur, non normalisé.</param>
  public sealed record FrozenColumn(
    string Table,
    string Column,
    string Text,
    double Score,
    bool IsPersonal,
    string Category,
    IReadOnlyList<double> Vector);

  private sealed record Fixture(
    string ManifestSha256,
    double Threshold,
    Encoder Encoder,
    IReadOnlyList<FrozenColumn> Columns,
    DefaultVector Default);

  private sealed record Encoder(string Tag, string Digest);

  private sealed record DefaultVector(double Score, bool IsPersonal, IReadOnlyList<double> Vector);
}
