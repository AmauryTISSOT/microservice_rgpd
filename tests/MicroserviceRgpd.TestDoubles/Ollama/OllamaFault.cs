namespace MicroserviceRgpd.TestDoubles.Ollama;

/// <summary>
/// Les pannes que <see cref="OllamaDouble"/> sait jouer — celles qu'Ollama a en production.
/// </summary>
/// <remarks>
/// ⚠️ <b>Chaque panne laisse un texte d'Ollama derrière elle</b> — <see cref="OllamaDouble.Canary"/>,
/// l'adresse, un digest étranger —, pour qu'un test puisse exiger qu'aucun ne traverse.
/// </remarks>
public enum OllamaFault
{
  /// <summary>Aucune : Ollama sert l'encodeur attendu et rend un vecteur par texte.</summary>
  None,

  /// <summary>La connexion est refusée — Ollama éteint, ou pas encore démarré.</summary>
  Unreachable,

  /// <summary>Ollama ne répond jamais : seule l'échéance du câblage rend la main.</summary>
  NeverAnswers,

  /// <summary>
  /// Ollama rend ses en-têtes, puis plus rien : l'échéance doit couvrir la lecture du corps, et pas
  /// seulement l'attente des en-têtes.
  /// </summary>
  StallsAfterHeaders,

  /// <summary><c>/api/tags</c> déclare un autre digest sous le tag du manifest.</summary>
  AnotherEncoder,

  /// <summary><c>/api/embed</c> rend autre chose que du JSON.</summary>
  MalformedEmbedding,

  /// <summary><c>/api/embed</c> rend une erreur HTTP, avec le texte d'Ollama dans le corps.</summary>
  FailingEmbedding,

  /// <summary><c>/api/embed</c> rend, au second lot, un vecteur de moins que de textes.</summary>
  OneVectorShortOnTheSecondBatch,
}
