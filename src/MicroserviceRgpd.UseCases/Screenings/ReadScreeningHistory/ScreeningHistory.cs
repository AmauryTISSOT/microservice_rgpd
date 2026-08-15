using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UseCases.Screenings.ReadScreeningHistory;

/// <summary>
/// L'<b>historique</b> du déploiement : les rapports archivés, du plus récent au plus ancien, et le
/// courant qu'ils ne sont pas.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Le courant y figure à part, jamais dans la liste.</b> Les mettre côte à côte aurait mis
/// sous un même tableau le rapport qu'on travaille et ceux qu'on ne peut plus toucher, ce qui est
/// la confusion exacte que la restriction d'écriture existe pour empêcher.
/// </para>
/// <para>
/// <b>Il est là parce que c'est ici qu'on supprime</b> : la suppression est le seul geste de ce
/// contexte qui atteigne indifféremment un archivé et le courant, et lui donner un écran unique est
/// ce qui garde le bouton irréversible loin des surfaces où l'on travaille.
/// </para>
/// </remarks>
/// <param name="Archived">Les archivés, du plus récemment lancé au plus ancien. Vide au premier rapport de détection.</param>
/// <param name="Current">
/// Le rapport de détection courant du déploiement, ou <c>null</c> si aucune détection n'a été
/// lancée. <b>Il n'est
/// pas dans <paramref name="Archived"/></b>.
/// </param>
public sealed record ScreeningHistory(
  IReadOnlyList<ScreeningHeading> Archived,
  ScreeningHeading? Current)
{
  /// <summary>Ce déploiement a-t-il lancé la moindre détection ?</summary>
  /// <remarks>
  /// <b>La question ne se pose pas sur la liste des archivés</b> : un déploiement d'un seul rapport
  /// a une liste vide et n'est pas vierge pour autant.
  /// </remarks>
  public bool HasNeverScreened => Current is null;

  /// <summary>L'historique d'un déploiement, à l'instant où on le regarde.</summary>
  /// <remarks>
  /// <b>Le retranchement du courant est celui du domaine</b>, jamais un second calcul écrit ici :
  /// deux façons de dire « archivé » finissent par ne plus désigner le même rapport, et celui qu'on
  /// arbitre est en jeu.
  /// </remarks>
  /// <param name="deployment">Tous les rapports du déploiement, entêtes seuls.</param>
  /// <exception cref="ArgumentNullException"><paramref name="deployment"/> est absent.</exception>
  internal static ScreeningHistory Of(IReadOnlyList<Screening> deployment)
  {
    ArgumentNullException.ThrowIfNull(deployment);

    var current = Screening.CurrentAmong(deployment);

    return new ScreeningHistory(
      [.. Screening.ArchivedAmong(deployment).Select(ScreeningHeading.Of)],
      current is null ? null : ScreeningHeading.Of(current));
  }
}

/// <summary>
/// Un rapport tel que l'historique le nomme : de quoi le reconnaître, et rien de plus.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Il ne porte aucun compte d'arbitrage</b> — ni retenues, ni jamais tranchées. Les compter
/// aurait demandé de balayer chaque rapport de l'historique pour rendre dix lignes de tableau, et
/// surtout « quatre-vingts en attente » sur un rapport qu'on ne peut plus arbitrer se lirait comme
/// du travail à finir, alors que c'est du travail <b>perdu</b> — le prix déclaré du re-dépôt, qui ne
/// fusionne rien. Ce compte-là se lit en ouvrant le rapport, où la phrase qui l'accompagne dit ce
/// qu'il veut dire.
/// </para>
/// <para>
/// <b>Le moteur est là parce que c'est la question de l'historique.</b> Un re-dépôt produit un
/// rapport neuf et coûte du travail humain réel : la <c>ScreeningEngineIdentity</c> est ce qui dit
/// au moins <b>pourquoi</b> le nouveau diffère de l'ancien.
/// </para>
/// </remarks>
/// <param name="Id">L'identité du rapport, celle sous laquelle on l'ouvre ou on le supprime.</param>
/// <param name="Database">
/// Le nom de base que le relevé rapportait — un repère pour l'humain, <b>jamais une identité sur
/// laquelle bâtir une comparaison</b> : deux rapports portant le même nom peuvent venir de deux
/// bases différentes, et le service n'en sait rien. C'est aussi ce que l'<c>Operator</c> retape
/// pour confirmer une suppression.
/// </param>
/// <param name="Dialect">Le SGBD dont le relevé se déclarait.</param>
/// <param name="Engine">Qui a détecté, et dans quelle version.</param>
/// <param name="LaunchedOn">Quand il a été lancé — le seul fait dont dépend son rang.</param>
/// <param name="ColumnCount">Combien de colonnes le relevé portait.</param>
public sealed record ScreeningHeading(
  ScreeningId Id,
  string Database,
  string Dialect,
  ScreeningEngineIdentity Engine,
  DateTimeOffset LaunchedOn,
  int ColumnCount)
{
  /// <summary>
  /// L'entrée d'historique d'un rapport <b>chargé sans ses colonnes</b>.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Le compte de colonnes est celui que le relevé <em>déclarait</em></b>, et non le compte
  /// des lignes filles — qu'un entête seul rendrait à zéro. Les deux sont égaux par construction :
  /// un rapport de détection n'existe que si la détection a rendu autant de lignes que le relevé en
  /// annonçait.
  /// </remarks>
  /// <exception cref="ArgumentNullException"><paramref name="screening"/> est absent.</exception>
  internal static ScreeningHeading Of(Screening screening)
  {
    ArgumentNullException.ThrowIfNull(screening);

    return new ScreeningHeading(
      screening.Id,
      screening.Database,
      screening.Dialect,
      screening.Engine,
      screening.LaunchedOn,
      screening.DeclaredColumnCount);
  }
}
