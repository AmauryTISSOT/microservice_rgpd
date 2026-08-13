using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UseCases.Screenings.ReadArchivedScreening;

/// <summary>
/// Relire un rapport <b>archivé</b>, en entier — ce qu'un moteur a vu à une date donnée, et ce
/// qu'un humain en avait dit avant qu'un re-dépôt ne le range.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Un archivé est lisible et non arbitrable</b>, et les deux moitiés comptent. Lisible :
/// toutes ses colonnes, arbitrages compris, sans qu'aucune ne soit filtrée — un archivé amputé
/// serait un archivé dont personne ne peut vérifier ce qui avait été dit. Non arbitrable : le geste
/// d'écriture ne sait charger que le courant, si bien que cette lecture n'ouvre aucun chemin
/// d'écriture — elle rend un type qui ne porte ni verrou ni bouton.
/// </para>
/// <para>
/// ⚠️ <b>Elle rend <c>null</c> quand le rapport nommé est le courant</b>, plutôt que de le rendre
/// en lecture seule. Le courant a son écran, où il s'arbitre : le servir ici l'aurait montré comme
/// un document figé à un <c>Operator</c> qui a du travail dessus, et c'est la façon la plus simple
/// de faire abandonner une relecture en cours.
/// </para>
/// <para>
/// <b>Elle rend <c>null</c> aussi quand le rapport n'existe plus</b> : un écran affiché il y a une
/// minute peut nommer un rapport qu'un geste vient de supprimer, et la suppression est sans
/// échéance et sans trace.
/// </para>
/// </remarks>
/// <param name="Screening">Le rapport qu'on ouvre.</param>
public sealed record ReadArchivedScreeningQuery(ScreeningId Screening)
  : IQuery<ScreeningAnswer<ArchivedScreeningReport>?>;
