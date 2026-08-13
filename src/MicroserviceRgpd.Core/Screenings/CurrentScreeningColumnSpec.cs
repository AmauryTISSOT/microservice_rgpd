namespace MicroserviceRgpd.Core.Screenings;

/// <summary>
/// Le rapport <b>courant</b> du déploiement, portant <b>la seule colonne qu'on s'apprête à
/// arbitrer</b> : la lecture du geste d'écriture, et rien de plus.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Elle rend l'agrégat volontairement incomplet, et c'est ce qui la justifie.</b> Un arbitrage
/// écrit <b>une</b> ligne ; passer par <see cref="CurrentScreeningSpec"/> en aurait rematérialisé
/// cinq mille à chaque clic, sur une surface qu'un <c>Operator</c> reprend pendant trois jours. C'est
/// le même ordre de grandeur qui a donné à <see cref="ScreenedColumn"/> son propre <c>DbSet</c>, vu
/// du côté de l'écriture.
/// </para>
/// <para>
/// ⚠️ <b>Un rapport chargé ainsi ne sait rien compter, et ne doit jamais qu'on le lui demande.</b>
/// Ses comptes vaudraient un sur cinq mille, et un compte faux se lit comme un compte.
/// <see cref="ScreeningCounts.Of"/> refuse bruyamment un rapport chargé sans toutes ses colonnes,
/// et c'est ce refus qui rend cette lecture partielle sûre plutôt qu'astucieuse.
/// </para>
/// <para>
/// <b>Elle est suivie</b>, contre <see cref="CurrentScreeningHeaderSpec"/> qui ne l'est pas : c'est
/// la seule lecture de ce contexte dont on écrive le résultat.
/// </para>
/// <para>
/// ⚠️ <b>C'est aussi ce qui tient « un archivé n'est pas arbitrable ».</b> L'agrégat ne peut pas
/// porter cette règle — « courant » est un calcul sur le lot des rapports d'un déploiement, et un
/// agrégat ne voit pas ses frères. Elle se pose donc au geste, et elle s'y pose <b>par la lecture</b>
/// plutôt que par un contrôle qu'on pourrait oublier d'écrire : ce qui n'est pas le courant n'est
/// jamais chargé, donc jamais écrit.
/// </para>
/// </remarks>
public sealed class CurrentScreeningColumnSpec : SingleResultSpecification<Screening>
{
  /// <param name="column">Le triplet de la colonne qu'on s'apprête à arbitrer.</param>
  /// <exception cref="ArgumentNullException"><paramref name="column"/> est absent.</exception>
  public CurrentScreeningColumnSpec(ColumnIdentity column)
  {
    ArgumentNullException.ThrowIfNull(column);

    // ⚠️ Le rapport est rendu MÊME quand le triplet ne désigne rien chez lui, avec zéro colonne :
    // c'est ce qui laisse le geste distinguer « aucun dépistage n'a été lancé » de « le courant ne
    // porte pas cette colonne ». Un filtre sur la racine aurait confondu les deux, et l'Operator
    // aurait lu « aucun dépistage » devant le sien.
    Query.Include(screening => screening.Columns.Where(screened =>
        screened.Listed.Identity.Schema == column.Schema
        && screened.Listed.Identity.Table == column.Table
        && screened.Listed.Identity.Column == column.Column))
      .OrderByDescending(screening => screening.LaunchedOn)
      .Take(1);
  }
}
