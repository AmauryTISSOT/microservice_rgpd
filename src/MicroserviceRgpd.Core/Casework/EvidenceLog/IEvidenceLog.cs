namespace MicroserviceRgpd.Core.Casework.EvidenceLog;

/// <summary>
/// La matière de preuve, en <b>ajout seul</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Une seule opération, et c'est tout le propos de ce type.</b> Il n'existe ici ni mise à jour,
/// ni suppression ligne à ligne, ni relecture : la déclaration d'aujourd'hui ne réécrit pas la
/// preuve d'hier, et ce que le type ne sait pas faire, personne n'aura à jurer qu'il ne l'a pas
/// fait. L'ajouter plus tard serait un geste visible dans ce fichier, pas un oubli.
/// </para>
/// <para>
/// <b>Il ne passe pas par le dépôt générique.</b> Celui-ci est contraint aux agrégats racines, et
/// l'emprunter aurait déclaré agrégat ce qui est hors de l'agrégat par construction — le
/// <c>EvidenceLog</c> survit au <c>Case</c> de cinq ans.
/// </para>
/// <para>
/// ⚠️ La seule suppression qui existera jamais est celle d'un <c>EvidenceLog</c> <b>entier</b>, échu
/// depuis la clôture, détruite d'un geste délibéré d'<c>Operator</c> — jamais par un processus, et
/// jamais ligne à ligne.
/// </para>
/// </remarks>
public interface IEvidenceLog
{
  /// <summary>
  /// Écrit une ligne de plus. Rien de cette écriture ne dépend du contenu d'une ligne antérieure.
  /// </summary>
  /// <param name="entry">La ligne à consigner.</param>
  /// <param name="cancellationToken">L'annulation de l'échange en cours.</param>
  Task AppendAsync(EvidenceLogEntry entry, CancellationToken cancellationToken = default);
}
