namespace MicroserviceRgpd.Core.Screenings;

/// <summary>
/// Le rapport <b>courant</b> du déploiement : le plus récemment lancé, avec toutes ses colonnes.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Le tri vit dans la requête, et il ne persiste rien.</b> « Courant » reste le calcul que
/// décrit <see cref="Screening.CurrentAmong"/> — le plus grand <see cref="Screening.LaunchedOn"/> —
/// et la base ne fait ici que le même calcul sans rapporter les autres rapports. Aucun drapeau
/// d'archivage n'est lu, parce qu'il n'en existe aucun à lire.
/// </para>
/// <para>
/// ⚠️ <b>Elle rend les colonnes, toutes.</b> Le rapport sommaire compte les signalées, les retenues,
/// les écartées et celles où rien n'a été vu : un compte fait sur un sous-ensemble serait un compte
/// faux, et filtrer les <c>Unflagged</c> à la lecture rétablirait l'<c>Omission silencieuse</c> un
/// cran plus bas que l'écran.
/// </para>
/// <para>
/// ⚠️ <b>Le départage de <see cref="Screening.CurrentAmong"/> n'est pas repris ici.</b> Deux
/// rapports lancés sur le même tic sont l'affaire d'un test, jamais d'un déploiement réel — un
/// <c>Operator</c> ne colle pas deux relevés dans le même battement d'horloge — et reproduire en
/// SQL l'ordre que .NET donne à un <c>Guid</c> aurait été un accord de hasard entre deux
/// comparaisons qui n'ont pas la même définition.
/// </para>
/// </remarks>
public sealed class CurrentScreeningSpec : SingleResultSpecification<Screening>
{
  public CurrentScreeningSpec()
  {
    Query.Include(screening => screening.Columns)
      .OrderByDescending(screening => screening.LaunchedOn)
      .Take(1);
  }
}
