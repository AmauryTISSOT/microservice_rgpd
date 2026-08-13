using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UseCases.Screenings.ReadScreeningTable;

/// <summary>
/// Lire <b>une table</b> du dépistage courant : toutes ses colonnes, dans l'ordre du relevé.
/// </summary>
/// <remarks>
/// <para>
/// <b>La table est l'unité de travail, et cette requête est la forme de cette décision.</b> Le
/// commentaire de table éclaire toutes ses colonnes, et le voisinage de <c>adr_l1</c>, <c>adr_l2</c>,
/// <c>cp</c>, <c>ville</c> ne se lit pas colonne isolée. Il n'existe donc pas de requête « une
/// colonne » : elle rendrait un écran où l'arbitrage se ferait à l'aveugle.
/// </para>
/// <para>
/// ⚠️ <b>Elle ne porte aucun filtre, et elle n'en portera pas.</b> Pas de « signalées seulement »,
/// pas de pagination : un <c>Screening</c> qui filtre les non signalées cesse d'être ce contexte, et
/// un paramètre facultatif est une porte qu'un appelant finit par ouvrir.
/// </para>
/// <para>
/// ⚠️ <b>Elle rend <c>null</c> quand le déploiement n'a lancé aucun dépistage, ou quand le courant ne
/// porte pas cette table.</b> Rendre une table vide portant la <c>Clause d'incomplétude</c> aurait
/// déclaré l'incomplétude de quelque chose qui n'existe pas — un <b>aveu sans acte</b> — et aurait
/// fait passer une faute de frappe dans l'adresse pour une table réellement dépourvue de colonnes.
/// </para>
/// </remarks>
/// <param name="Table">Le schéma et la table qu'on ouvre.</param>
public sealed record ReadScreeningTableQuery(TableIdentity Table)
  : IQuery<ScreeningAnswer<ScreenedTable>?>;
