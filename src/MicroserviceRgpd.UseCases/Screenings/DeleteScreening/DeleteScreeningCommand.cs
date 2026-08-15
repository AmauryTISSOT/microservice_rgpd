using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UseCases.Screenings.DeleteScreening;

/// <summary>
/// Un <c>Operator</c> <b>supprime un rapport</b> — celui qu'il nomme, courant ou archivé, avec
/// toutes ses colonnes et tous leurs arbitrages.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Elle nomme le rapport, et c'est la seule commande de ce contexte qui le fasse.</b>
/// L'arbitrage écrit toujours le courant et ne se laisse pas viser — sans quoi un archivé se ferait
/// arbitrer. La suppression, elle, désigne : supprimer « le courant » sans le nommer aurait fait
/// porter un geste irréversible sur un rapport qu'un collègue vient peut-être de déposer.
/// </para>
/// <para>
/// ⚠️ <b>Elle est irréversible et sans trace.</b> Il n'y a pas de l'<c>EvidenceLog</c> ici — le grain est
/// le déploiement — et rien ne consigne qu'un rapport a existé : après ce geste, le travail
/// d'arbitrage qu'il portait n'est nulle part. C'est un coût déclaré, pas un oubli.
/// </para>
/// <para>
/// ⚠️ <b>Aucune échéance ne la déclenche, et rien ne tourne.</b> Un rapport vit jusqu'à ce qu'un
/// <c>Operator</c> le supprime — pas une heure de plus, pas une heure de moins. Une purge
/// périodique aurait été le processus de fond que ce dépôt interdit, et elle aurait effacé du
/// travail humain sans que personne ne l'ait demandé.
/// </para>
/// <para>
/// ⚠️ <b>Elle exige que le nom de base soit retapé, et ce n'est pas une politesse de surface.</b>
/// Ce geste efface d'un clic des jours d'arbitrage humain, sur une surface qui existe tout entière
/// pour que ce travail-là ne se perde pas ; un bouton nu au bas d'un écran qu'on parcourt est le
/// mode de panne le plus probable, et le seul qui n'ait pas de remède. Le nom de base est ce que
/// l'<c>Operator</c> a sous les yeux, et le retaper oblige à <b>regarder quel rapport</b> on
/// supprime — ce qu'une case à cocher n'aurait pas obtenu. La confrontation est faite par le
/// service, contre ce qu'il détient : un champ caché reposté aurait été une cérémonie sans juge.
/// </para>
/// <para>
/// <b>Supprimer le courant rend son rang au précédent</b>, sans qu'aucune écriture n'ait lieu :
/// « courant » est un calcul, et le calcul se refait au rendu suivant. C'est le seul geste de ce
/// contexte qui <em>désarchive</em>, et il ne le fait qu'en retirant celui qui archivait.
/// </para>
/// </remarks>
/// <param name="Screening">Le rapport qu'on supprime.</param>
/// <param name="ConfirmedDatabase">
/// Le nom de base du rapport, <b>retapé par l'humain</b>, et confronté par le service à celui qu'il
/// détient.
/// </param>
public sealed record DeleteScreeningCommand(ScreeningId Screening, string? ConfirmedDatabase)
  : ICommand<Result<DeletedScreening>>;

/// <summary>
/// Ce qu'était le rapport qu'on vient de supprimer : de quoi le <b>dire</b> à celui qui l'a
/// supprimé.
/// </summary>
/// <remarks>
/// ⚠️ <b>Il est rendu parce qu'après le geste, plus rien ne peut le décrire.</b> Un renvoi muet
/// aurait laissé l'<c>Operator</c> devant une liste plus courte d'une ligne, sans savoir laquelle
/// est partie — sur un geste sans retour. Il ne rend <b>aucune colonne</b> : il n'y a plus de
/// rapport à lire, et la <c>Clause d'incomplétude</c> n'a rien à accompagner.
/// </remarks>
/// <param name="Database">Le nom de base que le relevé rapportait.</param>
/// <param name="LaunchedOn">Quand ce rapport de détection avait été lancé.</param>
/// <param name="ColumnCount">Combien de colonnes sont parties avec lui.</param>
/// <param name="WasCurrent">
/// Ce rapport était-il le <b>courant</b> du déploiement ? L'écran en a besoin pour dire ce qui vient
/// de changer : supprimer le courant remet le précédent au rang de courant, et un
/// <c>Operator</c> qui ne le sait pas reprendra son arbitrage sur un rapport qu'il croit archivé.
/// </param>
public sealed record DeletedScreening(
  string Database,
  DateTimeOffset LaunchedOn,
  int ColumnCount,
  bool WasCurrent);
