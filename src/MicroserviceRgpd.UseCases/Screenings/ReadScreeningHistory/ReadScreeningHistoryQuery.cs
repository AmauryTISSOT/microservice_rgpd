namespace MicroserviceRgpd.UseCases.Screenings.ReadScreeningHistory;

/// <summary>
/// Relire l'<b>historique</b> du déploiement : les rapports <b>archivés</b>, du plus récent au plus
/// ancien.
/// </summary>
/// <remarks>
/// <para>
/// <b>« Archivé » est un calcul</b>, et l'historique est son complément : tous les rapports sauf le
/// plus récemment lancé. Rien n'a été écrit pour archiver quoi que ce soit, et supprimer le courant
/// rend son rang au précédent — l'historique le dira au rendu suivant, sans que rien n'ait eu à se
/// mettre à jour.
/// </para>
/// <para>
/// ⚠️ <b>Elle nomme le courant à part, et ne le met jamais dans la liste.</b> Le courant se lit sur
/// son propre écran, où il s'arbitre ; le faire figurer parmi les archivés aurait mis côte à côte le
/// rapport qu'on travaille et ceux qu'on ne peut plus toucher, ce qui est la confusion exacte que la
/// restriction d'écriture existe pour empêcher. Il est nommé parce que la <b>suppression</b> a un
/// seul écran, et qu'elle atteint indifféremment un archivé et le courant.
/// </para>
/// <para>
/// ⚠️ <b>Elle ne porte pas de <c>Clause d'incomplétude</c>, et c'est un refus argumenté.</b> La
/// clause est celle d'<b>un</b> relevé et porte <b>ses</b> comptes ; un historique ne rend ni
/// rapport ni colonne, il nomme des rapports. Dix clauses dans un même écran auraient usé jusqu'à
/// l'invisibilité la seule phrase que ce contexte veut faire lire — et chacune aurait exigé le
/// balayage complet du rapport qu'elle compte, soit cinquante mille lignes pour dix lignes de
/// tableau. Ouvrir un archivé rend sa clause, comme tout ce qui rend un rapport.
/// </para>
/// </remarks>
public sealed record ReadScreeningHistoryQuery : IQuery<ScreeningHistory>;
