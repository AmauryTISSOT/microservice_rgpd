namespace MicroserviceRgpd.Web.Qualifications;

/// <summary>
/// Ce qu'une application tierce envoie pour faire qualifier un texte.
/// </summary>
/// <remarks>
/// <b>Deux champs, et trois métadonnées écartées.</b> L'horodatage de l'appelant doublonnerait
/// l'heure de réception ; le canal d'origine n'atteindrait aucun moteur — il serait collecté pour
/// être stocké, exactement ce qu'un service de conformité ne devrait pas faire ; le contexte de la
/// relation n'entre pas en v1. <b>Pas de champ de langue non plus</b> : le français est la seule
/// option.
/// </remarks>
public sealed record QualifyRequest
{
  /// <summary>
  /// Le texte libre, en français, présumé exercer un droit sans qu'on préjuge qu'il en exerce un.
  /// <para>
  /// <b>Aucun plancher au-delà de la non-vacuité.</b> Un seul caractère est un texte valide : sur
  /// <c>🙂</c> seul, la bonne réponse est « hors périmètre », pas un refus. Rejeter un texte parce
  /// qu'il est court, ce serait confondre « je n'y reconnais aucun droit » avec « ta requête est
  /// malformée ».
  /// </para>
  /// </summary>
  public string? Text { get; init; }

  /// <summary>
  /// La référence de corrélation de l'appelant, facultative et <b>opaque</b>. Renvoyée verbatim,
  /// jamais interprétée, jamais normalisée au-delà du nettoyage de ses bordures.
  /// <para>
  /// <b>Ce n'est pas une clé d'idempotence</b> : deux requêtes qui la partagent produisent deux
  /// qualifications distinctes, deux identifiants et deux traces.
  /// </para>
  /// <para>
  /// <b>Elle ne doit pas contenir de donnée personnelle.</b> Contrainte de documentation,
  /// explicitement inapplicable techniquement — le service ne peut pas la vérifier, et elle pèse
  /// donc sur le responsable de traitement.
  /// </para>
  /// </summary>
  public string? CallerReference { get; init; }
}
