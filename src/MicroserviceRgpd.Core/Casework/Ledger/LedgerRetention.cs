namespace MicroserviceRgpd.Core.Casework.Ledger;

/// <summary>
/// La vie du <c>Ledger</c> : <b>cinq ans à compter de la clôture du dossier</b>, et le calcul de son
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
public static class LedgerRetention
{
  /// <summary>
  /// Cinq ans, écrits en dur. La seule façon de changer cette durée est de changer ce fichier —
  /// c'est-à-dire un geste que quelqu'un signe, et non une case cochée dans un déploiement.
  /// </summary>
  public const int Years = 5;

  /// <summary>Le jour où la preuve d'un dossier clos ce jour-là cesse d'être due.</summary>
  /// <param name="closedOn">L'instant de la clôture — d'où court la vie du <c>Ledger</c>.</param>
  public static DateTimeOffset ExpiryOf(DateTimeOffset closedOn) => closedOn.AddYears(Years);

  /// <summary>
  /// La conservation est-elle échue à cet instant ? Fausse à l'échéance même : le dernier jour est
  /// dû, exactement comme celui de l'art. 12.3.
  /// </summary>
  /// <param name="closedOn">L'instant de la clôture.</param>
  /// <param name="instant">L'instant où quelqu'un regarde — jamais lu sur une horloge d'ici.</param>
  public static bool IsExpiredAt(DateTimeOffset closedOn, DateTimeOffset instant) =>
    instant > ExpiryOf(closedOn);
}
