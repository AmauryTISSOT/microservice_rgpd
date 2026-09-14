using System.Text.Json;

namespace MicroserviceRgpd.AspireHost;

// ⚠️ Ce fichier est aussi compilé dans les tests unitaires, par lien : il ne dépend d'aucun type
// d'Aspire, pour que la décision éprouvée là-bas soit celle que l'AppHost applique ici.

/// <summary>
/// Ce que les deux drapeaux de l'AppHost font naître côté Ollama. Les deux moteurs sont
/// <b>indépendants</b> : chacun tire son modèle, et un seul serveur les sert quand ils sont allumés
/// ensemble.
/// </summary>
/// <param name="Exists">La ressource Ollama entre dans la pile.</param>
/// <param name="PullsQualificationModel">Le modèle génératif du moteur LLM est tiré.</param>
/// <param name="PullsEncoder">L'encodeur du modèle de détection A2 est tiré, et le service l'attend.</param>
/// <param name="RequiresGpu">
/// Le conteneur exige le GPU. Seul le moteur LLM le justifie : l'encodeur est léger, et l'exiger pour
/// lui ferait refuser de démarrer une pile qui tourne très bien sur le processeur.
/// </param>
internal sealed record OllamaWiring(bool Exists, bool PullsQualificationModel, bool PullsEncoder, bool RequiresGpu)
{
  public static OllamaWiring For(bool llmIsOn, bool embeddingsAreOn) =>
    new(
      Exists: llmIsOn || embeddingsAreOn,
      PullsQualificationModel: llmIsOn,
      PullsEncoder: embeddingsAreOn,
      RequiresGpu: llmIsOn);
}

/// <summary>La lecture d'un drapeau de l'AppHost.</summary>
internal static class AppHostFlag
{
  /// <summary>
  /// Un drapeau est le <b>seul</b> réglage de sa section à disposer d'un repli, et ce repli est le
  /// choix sûr : une pile qui ne dit rien démarre sans serveur de modèles — donc sans GPU et sans
  /// téléchargement — et sans mock.
  /// </summary>
  /// <exception cref="InvalidOperationException">
  /// La valeur n'est ni « true » ni « false » : lue comme un « non », elle ferait passer une coquille
  /// pour une décision — même traitement que réservent déjà à leurs drapeaux le service .NET et le
  /// sidecar.
  /// </exception>
  public static bool IsOn(string key, string? raw)
  {
    if (string.IsNullOrEmpty(raw))
    {
      return false;
    }

    if (!bool.TryParse(raw, out var enabled))
    {
      throw new InvalidOperationException(
        $"Le réglage « {key} » de l'AppHost vaut « {raw} », qui n'est ni « true » ni « false ».");
    }

    return enabled;
  }
}

/// <summary>L'encodeur que l'AppHost tire pour A2.</summary>
internal static class EncoderModel
{
  /// <summary>
  /// Le tag de l'encodeur, lu dans le manifest de l'artefact que le service embarque — et nulle part
  /// ailleurs : un tag recopié en configuration tirerait tôt ou tard un encodeur sur lequel le modèle
  /// n'a jamais été mesuré (ADR-0025, point 4).
  /// </summary>
  /// <exception cref="InvalidOperationException">Le manifest ne nomme pas d'encodeur.</exception>
  public static string TagFrom(string manifestPath)
  {
    using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));

    return manifest.RootElement.TryGetProperty("encoder", out var encoder)
      && encoder.TryGetProperty("tag", out var tag)
      && tag.GetString() is { Length: > 0 } value
        ? value
        : throw new InvalidOperationException($"Le manifest « {manifestPath} » ne nomme pas le tag de son encodeur.");
  }
}
