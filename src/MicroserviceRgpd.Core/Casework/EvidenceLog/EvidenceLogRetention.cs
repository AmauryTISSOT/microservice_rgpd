namespace MicroserviceRgpd.Core.Casework.EvidenceLog;

/// <summary>
/// La vie de l'<c>EvidenceLog</c> : <b>cinq ans à compter de la clôture du dossier</b>, et le calcul de son
/// échéance.
/// </summary>
/// <remarks>
/// <para>
/// <b>La durée n'est pas réglable, et ce n'est pas un oubli.</b> C'est la prescription civile de
/// droit commun : la preuve vit aussi longtemps que l'action qu'elle sert à défendre. Une durée par
/// client serait une case à régler par déploiement — donc une case qui pourrit en silence, et dont
/// la valeur basse serait celle que tout le monde garderait.
/// </para>
/// <para>
/// <b>Il n'existe aucun état « échu ».</b> L'échéance se calcule sur la date de clôture à l'instant
/// où l'<c>Operator</c> regarde, comme le dépassement de l'art. 12.3 : un drapeau persisté ferait
/// dépendre la purge de ce qu'une minuterie ait tourné.
/// </para>
/// <para>
/// ⚠️ <b>Rien de ce qui est écrit ici ne détruit quoi que ce soit.</b> L'échéance fait <b>naître une
/// ligne</b> dans une section propre de l'écran de la file — la seule échéance du dispositif à faire
/// naître une ligne — où un humain détruit d'un geste délibéré. Un <c>Operator</c> inactif garde
/// au-delà de cinq ans : c'est un coût assumé, visible, et jamais barré.
/// </para>
/// </remarks>
public static class EvidenceLogRetention
{
  /// <summary>
  /// Cinq ans, écrits en dur. La seule façon de changer cette durée est de changer ce fichier —
  /// c'est-à-dire un geste que quelqu'un signe, et non une case cochée dans un déploiement.
  /// </summary>
  public const int Years = 5;

  /// <summary>Le jour où la preuve d'un dossier clos ce jour-là cesse d'être due.</summary>
  /// <param name="closedOn">L'instant de la clôture — d'où court la vie de l'<c>EvidenceLog</c>.</param>
  public static DateTimeOffset ExpiryOf(DateTimeOffset closedOn) => closedOn.AddYears(Years);

  /// <summary>
  /// La conservation est-elle échue à cet instant ? Fausse à l'échéance même : le dernier jour est
  /// dû, exactement comme celui de l'art. 12.3.
  /// </summary>
  /// <param name="closedOn">L'instant de la clôture.</param>
  /// <param name="instant">L'instant où quelqu'un regarde — jamais lu sur une horloge d'ici.</param>
  public static bool IsExpiredAt(DateTimeOffset closedOn, DateTimeOffset instant) =>
    instant > ExpiryOf(closedOn);

  /// <summary>
  /// La borne <b>large</b> qu'une base peut appliquer pour dégrossir : tout ce qui est échu à cet
  /// instant a été clos avant elle. L'inverse n'est pas vrai, et c'est voulu — ce qui passe cette
  /// borne se tranche ensuite par <see cref="IsExpiredAt"/>, seule à dire la règle.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Le jour de marge n'est pas de la prudence en trop.</b> <c>AddYears(-5)</c> n'est pas
  /// l'inverse de <c>AddYears(5)</c> : un dossier clos un 29 février voit son échéance ramenée au 28
  /// par le calendrier, et une borne calculée à l'envers l'écarterait le jour même où sa preuve cesse
  /// d'être due — la ligne n'apparaîtrait que le lendemain, pendant que le geste, lui, l'accepterait
  /// déjà. Deux réponses différentes à la même question, un jour tous les quatre ans.
  /// <para>
  /// Le jour se donne donc <b>du côté qui laisse passer</b>. Une borne trop large ne coûte que
  /// quelques lignes relues et rejetées derrière ; une borne trop étroite fait disparaître de
  /// l'écran une preuve qui n'est plus due, et personne ne vient jamais la chercher.
  /// </para>
  /// </remarks>
  /// <param name="observedAt">L'instant où quelqu'un regarde.</param>
  public static DateTimeOffset ClosedNoLaterThan(DateTimeOffset observedAt) =>
    observedAt.AddYears(-Years).AddDays(1);
}
