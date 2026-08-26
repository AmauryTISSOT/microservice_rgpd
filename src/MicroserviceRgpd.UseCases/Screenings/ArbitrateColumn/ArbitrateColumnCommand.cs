using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UseCases.Screenings.ArbitrateColumn;

/// <summary>
/// Un <c>Operator</c> <b>arbitre une colonne</b> du rapport de détection courant : il dit qu'elle
/// compte, ou qu'elle ne compte pas.
/// </summary>
/// <remarks>
/// <para>
/// <b>C'est le seul chemin par lequel une issue se pose</b>, et il passe par un humain. Le
/// service signale ; il ne retient ni n'écarte jamais. Un <c>Retained</c> prouve qu'un humain l'a
/// déclaré retenu — jamais que la colonne porte réellement des données personnelles :
/// <c>Enregistré, jamais vérifié</c>.
/// </para>
/// <para>
/// ⚠️ <b>Elle ne porte pas de date, et elle ne doit jamais en porter.</b> L'instant est posé par le
/// service, à l'horloge injectée : une date qui viendrait du formulaire serait une date que
/// l'<c>Operator</c> choisit, sur la seule trace que ce contexte garde d'un acte humain.
/// </para>
/// <para>
/// ⚠️ <b>Elle ne CHOISIT aucun rapport — elle dit celui qu'on avait sous les yeux.</b> On arbitre le
/// <b>courant</b>, et « courant » est un calcul : le plus récemment lancé. Laisser l'appelant
/// désigner le rapport à écrire aurait ouvert le seul chemin par lequel un archivé se fait arbitrer,
/// alors qu'un archivé est <b>lisible et non arbitrable</b>. <see cref="ReadScreening"/> ne sert
/// donc qu'à <b>refuser</b>, jamais à viser.
/// </para>
/// <para>
/// <b>Un second arbitrage écrase le premier, sans cérémonie.</b> Il n'y a pas de l'<c>EvidenceLog</c> ici :
/// la trace <b>est</b> l'état courant seul, et se raviser doit rester possible sur une surface qu'on
/// reprend pendant trois jours. Le coût est déclaré — qui avait dit quoi est effacé.
/// </para>
/// </remarks>
/// <param name="Column">Le triplet de la colonne arbitrée.</param>
/// <param name="ReadScreening">
/// Le rapport que l'humain <b>avait sous les yeux</b> quand il a tranché. ⚠️ <b>Il ne désigne pas
/// où écrire : il dit sur quoi la lecture portait</b>, et le geste refuse si ce n'est plus le
/// courant. Sans lui, un collègue qui dépose un relevé pendant qu'un <c>Operator</c> relit une
/// table fait atterrir l'arbitrage de celui-ci sur un rapport qu'il n'a jamais vu — motifs
/// compris. C'est un acte humain porté à tort, sur la seule trace de ce genre que ce contexte
/// garde.
/// </param>
/// <param name="Ruling">L'issue : retenue, ou écartée. Jamais <c>Awaiting</c>.</param>
public sealed record ArbitrateColumnCommand(
  ColumnIdentity Column,
  ScreeningId ReadScreening,
  ScreenedColumnState Ruling) : ICommand<Result>;
