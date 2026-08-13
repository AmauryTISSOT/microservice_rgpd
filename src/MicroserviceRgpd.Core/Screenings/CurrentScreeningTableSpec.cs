namespace MicroserviceRgpd.Core.Screenings;

/// <summary>
/// Le rapport <b>courant</b> du déploiement, portant les colonnes d'<b>une seule table</b> : la
/// lecture du <b>geste de lot</b>, et rien de plus.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Elle rend l'agrégat volontairement incomplet, comme <see cref="CurrentScreeningColumnSpec"/>
/// et pour la même raison</b> : le lot écrit une table, jamais un rapport. Passer par
/// <see cref="CurrentScreeningSpec"/> en aurait rematérialisé cinq mille lignes pour en écrire
/// quelques dizaines, sur la surface même que ce geste existe pour rendre tenable.
/// </para>
/// <para>
/// ⚠️ <b>Elle charge la table ENTIÈRE, signalées comprises, et non le seul lot.</b> Filtrer ici sur
/// « non signalée et en attente » aurait déplacé dans une requête la règle qui borne le geste, là où
/// personne ne la relit — elle vit sur la ligne, en
/// <see cref="ScreenedColumn.IsWithinReachOfABatchGesture"/>. Et le rapport chargé sans elles n'aurait
/// plus su dire combien de colonnes signalées restent à lire une par une, qui est très exactement ce
/// que le geste doit répondre à l'<c>Operator</c>.
/// </para>
/// <para>
/// ⚠️ <b>Un rapport chargé ainsi ne sait rien compter</b>, et il ne faut jamais le lui demander : ses
/// comptes vaudraient une table sur quarante, et un compte faux se lit comme un compte.
/// <see cref="ScreeningCounts.Of"/> refuse bruyamment un rapport chargé sans toutes ses colonnes.
/// </para>
/// <para>
/// <b>Elle est suivie</b> : c'est une lecture dont on écrit le résultat. ⚠️ Et c'est aussi ce qui
/// tient « un archivé n'est pas arbitrable » — ce qui n'est pas le courant n'est jamais chargé, donc
/// jamais écrit.
/// </para>
/// </remarks>
public sealed class CurrentScreeningTableSpec : SingleResultSpecification<Screening>
{
  /// <param name="table">Le schéma et la table que le geste de lot ouvre.</param>
  /// <exception cref="ArgumentNullException"><paramref name="table"/> est absent.</exception>
  public CurrentScreeningTableSpec(TableIdentity table)
  {
    ArgumentNullException.ThrowIfNull(table);

    // ⚠️ Le rapport est rendu MÊME quand il ne porte pas cette table, avec zéro colonne : c'est ce
    // qui laisse le geste distinguer « aucun dépistage n'a été lancé » de « le courant ne porte pas
    // cette table ». Un filtre sur la racine aurait confondu les deux, et l'Operator aurait lu
    // « aucun dépistage » devant le sien.
    Query.Include(screening => screening.Columns.Where(screened =>
        screened.Listed.Identity.Schema == table.Schema
        && screened.Listed.Identity.Table == table.Table))
      .OrderByDescending(screening => screening.LaunchedOn)
      .Take(1);
  }
}
