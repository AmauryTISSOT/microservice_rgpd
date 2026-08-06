using MicroserviceRgpd.Core.Casework;

namespace MicroserviceRgpd.UseCases.Casework.DeclareExtension;

/// <summary>
/// L'<c>Operator</c> déclare <b>prolonger de deux mois</b> au titre de l'art. 12.3.
/// </summary>
/// <remarks>
/// <para>
/// <b>Le service ne prolonge rien, et n'écrit à personne.</b> L'art. 12.3 exige que la personne soit
/// informée de la prolongation et de ses motifs : c'est un acte de l'<c>Operator</c>, seulement
/// <b>déclaré</b> ici. Aucun courriel ne part de cette commande, ni d'ailleurs.
/// </para>
/// <para>
/// ⚠️ <b>Elle ne barre pas la route à une déclaration tardive.</b> Passé le mois, la déclaration
/// s'inscrit au dossier et au <c>Ledger</c> — le fait est gardé —, et le dénominateur ne bouge pas.
/// Refuser aurait perdu le fait ; déplacer l'échéance aurait blanchi un dépassement déjà acquis.
/// Aucune des deux n'est un service rendu à la personne.
/// </para>
/// </remarks>
/// <param name="Case">Le dossier dont le délai est prolongé.</param>
/// <param name="Motive">
/// Pourquoi. <b>Exigé</b> : l'art. 12.3 met la raison à la charge de qui prolonge. <b>Prose de
/// preuve</b> — elle survit dans le <c>Ledger</c> quand tout le dossier tombe.
/// </param>
/// <param name="InformedOn">
/// Le jour où l'<c>Operator</c> déclare avoir informé la personne. <b>Exigé</b>, et jamais constaté
/// par le service : c'est la charge probatoire qu'il fait tenir à celui qui prolonge.
/// </param>
/// <param name="SignedBy">
/// Le nom que l'<c>Operator</c> a saisi. Non authentifié — la preuve garde le nom <b>et</b> ce
/// régime.
/// </param>
public sealed record DeclareExtensionCommand(
  CaseId Case,
  string? Motive,
  DateTimeOffset InformedOn,
  string? SignedBy)
  : ICommand<Result>;
