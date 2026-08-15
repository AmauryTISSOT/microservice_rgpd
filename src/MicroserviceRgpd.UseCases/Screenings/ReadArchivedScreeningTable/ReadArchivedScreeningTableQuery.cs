using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UseCases.Screenings.ReadArchivedScreeningTable;

/// <summary>
/// Relire <b>une table</b> d'un rapport archivé : toutes ses colonnes, dans l'ordre du relevé, et
/// ce qu'un humain en avait dit.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Elle nomme le rapport, ce que la lecture du courant ne fait pas</b> — et ce n'est pas la
/// même chose que de le viser pour écrire. Un archivé n'est le plus récent de rien : il n'existe
/// aucune façon de l'atteindre sans le nommer. C'est aussi pourquoi <b>aucun geste d'écriture ne
/// prend un rapport en paramètre</b> : le seul chemin par lequel un archivé se ferait arbitrer
/// serait celui-là.
/// </para>
/// <para>
/// ⚠️ <b>Un archivé se lit en entier, et « en entier » commence ici.</b> Toutes les colonnes de la
/// table, <c>Unflagged</c> comprises : un archivé dont on ne rendrait que les signalées serait un
/// rapport de détection dont personne ne peut plus vérifier ce que la détection n'avait pas vu.
/// </para>
/// </remarks>
/// <param name="Screening">Le rapport archivé qu'on ouvre.</param>
/// <param name="Table">Le schéma et la table.</param>
public sealed record ReadArchivedScreeningTableQuery(ScreeningId Screening, TableIdentity Table)
  : IQuery<ScreeningAnswer<ArchivedTable>?>;
