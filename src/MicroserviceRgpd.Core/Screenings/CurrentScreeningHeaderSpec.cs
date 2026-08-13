namespace MicroserviceRgpd.Core.Screenings;

/// <summary>
/// Le rapport <b>courant</b> du déploiement, <b>son entête seul</b> : ce qui le nomme, le SGBD dont
/// il se déclare, le moteur qui l'a produit et l'instant de son lancement.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Elle ne rapporte aucune colonne, et c'est délibéré.</b> C'est la requête de l'écran d'une
/// table : les colonnes de cette table-là se lisent sur leur propre <c>DbSet</c> — voir
/// <see cref="IScreenedColumns"/> — et les comptes du rapport entier se demandent à la base. Charger
/// les colonnes ici aurait rendu la seconde table inutile.
/// </para>
/// <para>
/// ⚠️ <b>Le rapport qu'elle rend ne sait donc rien compter.</b> Ses comptes vaudraient zéro, et zéro
/// se lit comme un rapport achevé : ne jamais les lui demander. <see cref="ScreeningCounts.Of"/>
/// attend un rapport chargé avec toutes ses colonnes, ce que celle-ci ne rend pas.
/// </para>
/// <para>
/// <b>« Courant » reste le calcul de <see cref="Screening.CurrentAmong"/></b> — le plus grand
/// <see cref="Screening.LaunchedOn"/> — et aucun drapeau d'archivage n'est lu, parce qu'il n'en
/// existe aucun à lire.
/// </para>
/// </remarks>
public sealed class CurrentScreeningHeaderSpec : SingleResultSpecification<Screening>
{
  public CurrentScreeningHeaderSpec()
  {
    Query.OrderByDescending(screening => screening.LaunchedOn)
      .Take(1)
      .AsNoTracking();
  }
}
