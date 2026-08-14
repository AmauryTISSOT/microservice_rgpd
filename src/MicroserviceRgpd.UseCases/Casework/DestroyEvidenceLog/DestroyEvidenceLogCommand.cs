using MicroserviceRgpd.Core.Casework;

namespace MicroserviceRgpd.UseCases.Casework.DestroyEvidenceLog;

/// <summary>
/// L'<c>Operator</c> détruit un <c>EvidenceLog</c> <b>échu</b>, en entier.
/// </summary>
/// <remarks>
/// <para>
/// <b>C'est un geste, jamais un processus.</b> Rien ne l'appelle qu'un clic : il n'existe aucun
/// balayage, aucune minuterie, aucun <c>cron</c>. Un <c>Operator</c> inactif garde au-delà de cinq
/// ans, et c'est un coût assumé — visible dans une section de l'écran qui ne se vide pas toute
/// seule.
/// </para>
/// <para>
/// ⚠️ <b>Elle ne porte pas de signataire, et ce n'est pas un oubli.</b> La destruction ne laisse
/// <b>aucune trace d'elle-même</b> : un <c>EvidenceLog</c> détruit ne peut pas consigner sa propre
/// destruction, et écrire ce nom ailleurs — dans une seconde table, dans un journal — aurait
/// rouvert l'expurgation que la définition du <c>EvidenceLog</c> ferme, ou promis une preuve que le
/// service ne tiendrait pas. Réclamer un nom pour ne l'écrire nulle part aurait été pire : la
/// façade d'une signature. La parade du geste irréversible est <b>dans l'écran</b>, comme celle de
/// la clôture : une case cochée délibérément.
/// </para>
/// <para>
/// ⚠️ <b>L'échéance n'est pas un paramètre.</b> Elle est éprouvée au moment du geste, sur l'horloge
/// du service : l'écran d'où part le clic a pu être affiché il y a une heure comme il y a un an.
/// </para>
/// </remarks>
/// <param name="Case">Le dossier dont la preuve est détruite — la seule façon de la désigner.</param>
public sealed record DestroyEvidenceLogCommand(CaseId Case) : ICommand<Result>;
