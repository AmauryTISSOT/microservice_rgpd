namespace MicroserviceRgpd.Core.Screenings;

/// <summary>
/// <b>Tous</b> les rapports du déploiement, <b>entêtes seuls</b>, du plus récemment lancé au plus
/// ancien.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Elle rapporte le courant avec les autres, et c'est ce qui la rend juste.</b> « Archivé »
/// est le complément d'un calcul sur le lot — voir <see cref="Screening.ArchivedAmong"/> — et une
/// requête qui aurait tenté d'écarter le courant en SQL aurait écrit ce calcul une seconde fois, au
/// seul endroit où personne ne le relit. C'est le geste qui retranche, en appelant le domaine.
/// </para>
/// <para>
/// <b>Elle ne rapporte aucune colonne.</b> Un historique de dix rapports de cinq mille colonnes en
/// aurait rematérialisé cinquante mille pour afficher dix lignes de tableau ; ce que l'historique
/// nomme — la base, le SGBD, le moteur, l'instant, le compte déclaré — vit sur la ligne du rapport.
/// </para>
/// <para>
/// <b>Elle n'est pas bornée.</b> Un déploiement porte quelques dizaines de rapports, jamais plus :
/// dix détections de Dolibarr font dix lignes. Une pagination ici aurait caché des rapports de
/// détection
/// derrière un bouton, ce qui est la façon la plus simple de faire oublier un rapport qu'on croyait
/// supprimé.
/// </para>
/// </remarks>
public sealed class ScreeningHistorySpec : Specification<Screening>
{
  public ScreeningHistorySpec()
  {
    Query.OrderByDescending(screening => screening.LaunchedOn)
      .AsNoTracking();
  }
}
