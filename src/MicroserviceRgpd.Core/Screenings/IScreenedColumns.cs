namespace MicroserviceRgpd.Core.Screenings;

/// <summary>
/// Le port par lequel on lit les <see cref="ScreenedColumn"/> d'un rapport <b>une table à la
/// fois</b>, sans charger le rapport.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Il existe parce que <see cref="ScreenedColumn"/> a son propre <c>DbSet</c>, et le dépôt
/// générique est contraint aux agrégats racines.</b> L'emprunter aurait déclaré agrégat une entité
/// qui naît et se modifie par sa racine — même raison que <c>ILedger</c> ou <c>IRetrievedData</c>,
/// qui vivent hors du dépôt générique sans être des racines pour autant.
/// </para>
/// <para>
/// <b>Ce qu'il achète est un ordre de grandeur, jamais un goût.</b> L'écran d'arbitrage ouvre une
/// table de quelques dizaines de colonnes ; passer par la racine en aurait rematérialisé cinq mille
/// à chaque rafraîchissement, sur une surface qu'un <c>Operator</c> reprend pendant trois jours.
/// </para>
/// <para>
/// ⚠️ <b>Aucune de ses lectures ne filtre les <c>Unflagged</c>, et aucune ne le pourra.</b> Il n'a
/// pas de paramètre pour cela : filtrer les non signalées — dans une requête, dans l'écran, dans une
/// pagination par défaut — rétablit l'<c>Omission silencieuse</c> sans qu'aucune ligne de doctrine
/// n'ait été modifiée.
/// </para>
/// </remarks>
public interface IScreenedColumns
{
  /// <summary>
  /// Les colonnes d'<b>une</b> table du rapport, <b>toutes</b>, dans l'ordre du relevé.
  /// </summary>
  /// <remarks>
  /// L'ordre est celui du schéma, jamais l'alphabétique : c'est le seul qui garde à <c>adr_l1</c> le
  /// voisinage de <c>adr_l2</c>, <c>cp</c> et <c>ville</c>, et ce voisinage est ce qui rend
  /// l'arbitrage possible.
  /// </remarks>
  /// <param name="screening">Le rapport dont on ouvre une table.</param>
  /// <param name="table">Le schéma et la table.</param>
  /// <param name="cancellationToken">L'annulation de l'appelant.</param>
  /// <returns>Ses colonnes, ou la liste vide si ce rapport ne porte pas cette table.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="table"/> est absent.</exception>
  Task<IReadOnlyList<ScreenedColumn>> OfTableAsync(
    ScreeningId screening,
    TableIdentity table,
    CancellationToken cancellationToken = default);

  /// <summary>
  /// Les comptes du rapport <b>entier</b>, calculés par la base — ceux de la clause, ceux de l'écran
  /// et celui du verrou.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Ils portent sur tout le rapport, et l'écran d'une table en a besoin pour cela même.</b>
  /// Un verrou calculé sur la seule table ouverte s'éteindrait dès qu'elle serait relue, et
  /// l'<c>Operator</c> lirait « tout a été relu » devant trente-neuf tables intactes.
  /// </remarks>
  /// <param name="screening">Le rapport qu'on compte.</param>
  /// <param name="cancellationToken">L'annulation de l'appelant.</param>
  Task<ScreeningCounts> CountsOfAsync(
    ScreeningId screening,
    CancellationToken cancellationToken = default);
}
