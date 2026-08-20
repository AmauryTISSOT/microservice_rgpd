using System.Globalization;
using MicroserviceRgpd.Core.Qualifications;

namespace MicroserviceRgpd.Web.Pages.Qualifications;

/// <summary>
/// Un avis de moteur tel que le dépliant le met sous les yeux de l'<c>Operator</c> : quatre textes
/// français, et rien du domaine.
/// </summary>
/// <param name="Rights">Les droits que <b>ce moteur-là</b> a reconnus, sous leur libellé français.</param>
/// <param name="Confidence">
/// À quel point ce moteur doute de son propre avis, ou <c>null</c> quand il n'en déclare aucune — ce
/// qui est le cas du lexique, qui n'a aucun avis sur sa propre fiabilité.
/// </param>
/// <param name="EngineName">Le nom sous lequel ce moteur se déclare, tel qu'il est arrivé.</param>
/// <param name="EngineVersion">La version qu'il déclare de lui-même, chaîne opaque rendue verbatim.</param>
/// <param name="Latency">Le temps que ce moteur a mis à rendre son avis.</param>
public sealed record RenderedOpinion(
  string Rights,
  string? Confidence,
  string EngineName,
  string EngineVersion,
  string Latency)
{
  /// <summary>
  /// L'avis d'un moteur, ou <c>null</c> quand il n'a rien rendu : <b>l'absence n'est pas un avis
  /// vide</b>, et le gabarit la nomme plutôt que de laisser une colonne blanche.
  /// </summary>
  /// <remarks>
  /// La latence suit l'avis, et jamais l'inverse : mesurer le temps qu'un moteur a mis à ne rien
  /// rendre ferait passer une panne pour une lenteur. Un avis sans latence est donc traité comme un
  /// avis instantané plutôt que comme une contradiction — le gestionnaire ne produit pas ce cas.
  /// </remarks>
  public static RenderedOpinion? Of(QualificationOpinion? opinion, TimeSpan? latency)
  {
    if (opinion is null)
    {
      return null;
    }

    return new RenderedOpinion(
      Verdict.RightsOf(opinion.Qualification),
      ConfidenceOf(opinion.DeclaredConfidence),
      opinion.Engine.Name,
      opinion.Engine.Version,
      Premises.LatencyOf(latency ?? TimeSpan.Zero));
  }

  /// <summary>
  /// Ce que la confiance déclarée dit à l'humain. <b>Trois degrés, jamais un nombre</b> : un score
  /// invite à poser des seuils, donc à régler des seuils sur rien.
  /// </summary>
  private static string? ConfidenceOf(DeclaredConfidence? declared)
  {
    return declared switch
    {
      null => null,
      DeclaredConfidence.High => "Haute — le moteur ne doute pas de son avis.",
      DeclaredConfidence.Medium => "Moyenne — le moteur doute sans trancher.",
      DeclaredConfidence.Low => "Basse — le moteur doute franchement de son avis.",
      _ => throw new ArgumentOutOfRangeException(
        nameof(declared),
        declared,
        "Une confiance déclarée que l'écran ne sait pas dire ne doit pas se rendre en silence."),
    };
  }
}
