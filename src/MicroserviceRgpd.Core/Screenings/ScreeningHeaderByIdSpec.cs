namespace MicroserviceRgpd.Core.Screenings;

/// <summary>
/// Un rapport <b>nommé</b>, <b>son entête seul</b> : ce qui le nomme, le SGBD dont il se déclare, le
/// moteur qui l'a produit et l'instant de son lancement.
/// </summary>
/// <remarks>
/// <para>
/// <b>C'est la requête de l'écran d'une table d'un archivé</b> — les colonnes de cette table-là se
/// lisent sur leur propre <c>DbSet</c>, voir <see cref="IScreenedColumns"/> — <b>et celle de la
/// suppression</b>, qui n'écrit qu'une ligne.
/// </para>
/// <para>
/// ⚠️ <b>Elle est suivie, contre <see cref="CurrentScreeningHeaderSpec"/> qui ne l'est pas</b>, et
/// c'est parce que la suppression l'emprunte : c'est la seule lecture par identité dont on écrive
/// le résultat.
/// </para>
/// <para>
/// ⚠️ <b>Le rapport qu'elle rend ne sait rien compter.</b> Ses comptes vaudraient zéro, et zéro se
/// lit comme un rapport achevé : ne jamais les lui demander. <see cref="ScreeningCounts.Of"/>
/// refuse bruyamment un rapport chargé sans ses colonnes, et c'est ce refus qui rend cette lecture
/// partielle sûre plutôt qu'astucieuse.
/// </para>
/// <para>
/// ⚠️ <b>Elle ne dit pas si le rapport est archivé</b>, et elle ne le pourra jamais : « courant »
/// est un calcul sur le <b>lot</b> des rapports d'un déploiement. Le geste qui a besoin de le savoir
/// lit l'historique et demande au domaine — voir <see cref="Screening.IsArchivedAmong"/>.
/// </para>
/// </remarks>
public sealed class ScreeningHeaderByIdSpec : SingleResultSpecification<Screening>
{
  /// <param name="screening">Le rapport qu'on nomme.</param>
  public ScreeningHeaderByIdSpec(ScreeningId screening)
  {
    Query.Where(candidate => candidate.Id == screening);
  }
}
