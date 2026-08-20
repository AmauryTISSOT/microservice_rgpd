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
/// qui est le cas du lexique, qui ne dit rien de son propre doute.
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
  /// ⚠️ <b>La latence suit l'avis, et jamais l'inverse</b> : mesurer le temps qu'un moteur a mis à
  /// ne rien rendre ferait passer une panne pour une lenteur, et le gestionnaire ne rend donc l'une
  /// jamais sans l'autre. Un avis arrivé <b>sans</b> sa latence est une contradiction, et elle est
  /// <b>refusée</b> plutôt que comblée : un « 0 ms » posé d'office serait une mesure inventée, que
  /// l'<c>Operator</c> lirait comme un moteur instantané.
  /// </remarks>
  public static RenderedOpinion? Of(QualificationOpinion? opinion, TimeSpan? latency)
  {
    if (opinion is null)
    {
      return null;
    }

    if (latency is null)
    {
      throw new InvalidOperationException(
        "Un avis est arrivé sans le temps qu'il a pris : l'écran ne sait pas dire cette latence-là, "
        + "et n'en invente pas.");
    }

    return new RenderedOpinion(
      Verdict.RightsOf(opinion.Qualification),
      ConfidenceOf(opinion.DeclaredConfidence),
      opinion.Engine.Name,
      opinion.Engine.Version,
      Premises.LatencyOf(latency.Value));
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
