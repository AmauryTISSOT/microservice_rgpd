using MicroserviceRgpd.Core.Casework;

namespace MicroserviceRgpd.UseCases.Casework.CloseCase;

/// <summary>
/// L'<c>Operator</c> <b>clôt le dossier</b>, et tout le nominatif est détruit à l'instant même.
/// </summary>
/// <remarks>
/// <para>
/// <b>Le geste est irréversible, seul du dispositif à l'être.</b> Sa parade est un geste
/// <b>délibéré dans l'écran</b>, jamais de la donnée gardée en réserve : aucun risque juridique ne
/// demande le nominatif, la preuve d'une procédure étant anonyme, et une fenêtre « au cas où »
/// n'aurait entreposé que le sac de désignations de gens ayant demandé à disparaître.
/// </para>
/// <para>
/// <b>Il n'existe aucune commande d'effacement de <c>Case</c> distincte.</b> Une personne qui
/// demande l'effacement de son dossier encore ouvert est servie par celle-ci, sous
/// <c>Abandoned</c> : le seul arbitrage réel — poursuivre exige ses <c>Designations</c>, donc
/// poursuivre ou effacer, jamais les deux — lui est posé franchement.
/// </para>
/// <para>
/// ⚠️ <b>Elle ne réclame ni <c>Step</c> déclaré ni <c>Claim</c> répondu.</b> L'écran les montre
/// avant de laisser signer ; la commande, elle, ne barre jamais la route. Un <c>Step</c> laissé
/// <c>ToDo</c> reste <c>ToDo</c> dans le dossier clos, où il se lit comme l'oubli qu'il est.
/// </para>
/// </remarks>
/// <param name="Case">Le dossier qui se clôt.</param>
/// <param name="Cause">Ce par quoi il se clôt, nommé par l'humain qui signe.</param>
/// <param name="Motive">
/// Le motif en prose libre. <b>Exigé si — et seulement si</b> — la cause le réclame, ce qui est le
/// cas d'<c>Abandoned</c> et de lui seul. <b>Texte qui reste</b> : il survit dans le
/// <c>EvidenceLog</c> quand tout le dossier tombe.
/// </param>
/// <param name="SignedBy">
/// Le nom que l'<c>Operator</c> a saisi. Non authentifié — la preuve garde le nom <b>et</b> ce
/// régime, et continue de le nommer cinq ans.
/// </param>
public sealed record CloseCaseCommand(
  CaseId Case,
  ClosingCause Cause,
  string? Motive,
  string? SignedBy)
  : ICommand<Result>;
